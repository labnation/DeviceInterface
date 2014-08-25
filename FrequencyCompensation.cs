using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using Microsoft.Xna.Framework;
using System.IO;
using AForge.Math;

namespace ECore
{
    public static class FrequencyCompensation
    {
        public static Complex[] ArtificialSpectrum;

        public static Complex[] CreateArtificialSpectrum(Dictionary<int, float> magnitudes, Dictionary<int, float> phases)//int[] magnitudeIndices, float[] magnitudeValues, int[] phaseIndices, float[] phaseValues)
        {
            //precondition incoming data for easier lookups
            int[] magnitudeIndices = new int[magnitudes.Count];
            float[] magnitudeValues = new float[magnitudes.Count];            
            for (int i = 0; i < magnitudes.Count; i++)
            {
                magnitudeIndices[i] = magnitudes.ElementAt(i).Key;
                magnitudeValues[i] = magnitudes.ElementAt(i).Value;
            }

            int[] phaseIndices = new int[phases.Count];
            float[] phaseValues = new float[phases.Count];
            for (int i = 0; i < phases.Count; i++)
            {
                phaseIndices[i] = phases.ElementAt(i).Key;
                
                //WHOOHAH!! Dirtiness alert!!!!!!!!
                //the FFT/IFFT implemented in AForge uses opposite phases compared to matlab!!!
                //since the spectrum is calibrated in matlab, but compensated with AForge, we need to negate the phase somewhere.
                phaseValues[i] = -phases.ElementAt(i).Value;
            }

            ///////////////////////////
            // Phase data

            //start by extending/mirroring the phase data
            int nbrPhaseSpikes = phaseIndices.Length;
            int[] phaseIndices_full = new int[nbrPhaseSpikes*2];
            float[] phaseValues_full = new float[nbrPhaseSpikes*2];
            for (int i = 0; i < nbrPhaseSpikes; i++)
			{
                phaseValues_full[i] = phaseValues[i];
                phaseValues_full[nbrPhaseSpikes+i]= -phaseValues[nbrPhaseSpikes-1-i];
                phaseIndices_full[i] = phaseIndices[i];
                phaseIndices_full[nbrPhaseSpikes+i] = 2048-phaseIndices[nbrPhaseSpikes-1-i];
			}

            //next: interpolate phase data            
            float[] interpolatedPhaseValues = Interpolate1D_Linear(phaseIndices_full, phaseValues_full, 2048);

            ///////////////////////////
            // Magnitude data

            //start by extending/mirroring the magnitude data
            int nbrMagnitudeSpikes = magnitudeIndices.Length;
            int[] magnitudeIndices_full = new int[nbrMagnitudeSpikes * 2];
            float[] magnitudeValues_full = new float[nbrMagnitudeSpikes * 2];
            for (int i = 0; i < nbrMagnitudeSpikes; i++)
            {
                magnitudeValues_full[i] = magnitudeValues[nbrMagnitudeSpikes - 1 - i];
                magnitudeValues_full[nbrMagnitudeSpikes + i] = magnitudeValues[i];
                magnitudeIndices_full[i] = magnitudeIndices[nbrMagnitudeSpikes-1-i];
                magnitudeIndices_full[nbrMagnitudeSpikes + i] = 2048 - magnitudeIndices[i];
            }

            
            //next: interpolate magnitude data            
            float[] interpolatedMagnitudeValues = Interpolate1D_Linear(magnitudeIndices_full, magnitudeValues_full, 2048);

            //small DC correction on magnitude data
            for (int i = 1023-7; i < 1024+7; i++)
                interpolatedMagnitudeValues[i] = interpolatedMagnitudeValues[1024 + 7];

            //then normalize magnitude data
            float[] normalizedMagnitudeValues = new float[interpolatedMagnitudeValues.Length];
            for (int i = 0; i < normalizedMagnitudeValues.Length; i++)
                normalizedMagnitudeValues[i] = interpolatedMagnitudeValues[i] / interpolatedMagnitudeValues[1024];

            //now generate artificial spectrum!            
            Complex[] artSpectrum = new Complex[2048];
            float[] artReal = new float[artSpectrum.Length];
            float[] artImaginary = new float[artSpectrum.Length];
            for (int i = 0; i < artSpectrum.Length; i++)
            {
                artReal[i] = (float)(Math.Cos(interpolatedPhaseValues[i])*normalizedMagnitudeValues[i]);
                artImaginary[i] = (float)(Math.Sin(interpolatedPhaseValues[i])*normalizedMagnitudeValues[i]);
                artSpectrum[i] = new Complex(artReal[i], artImaginary[i]);
            }

            //fftshift
            Complex[] finalSpectrum = new Complex[artSpectrum.Length];
            for (int i = 0; i < artSpectrum.Length; i++)
                finalSpectrum[i] = artSpectrum[(i + finalSpectrum.Length/2) % finalSpectrum.Length];

            //invert
            Complex nominator = new Complex(1, 0);
            for (int i = 0; i < artSpectrum.Length; i++)
                finalSpectrum[i] = Complex.Divide(nominator, finalSpectrum[i]);

            //split up for debug
            float[] finalReal = new float[artSpectrum.Length];
            float[] finalImaginary = new float[artSpectrum.Length];
            for (int i = 0; i < artSpectrum.Length; i++)
            {
                finalReal[i] = (float)finalSpectrum[i].Re;
                finalImaginary[i] = (float)finalSpectrum[i].Im;
            }

            return finalSpectrum;
        }

        public static void DumpToCSV(float[] inp, string filename)
        {
            StringBuilder sb = new StringBuilder();
            foreach (float f in inp)
                sb.Append(f.ToString() + ",");
            sb.Remove(sb.Length - 1, 1);

            File.WriteAllText(filename, sb.ToString());
        }

        public static float[] Interpolate1D_Linear(int[] sortedIndices, float[] correspondingValues, int numberOfPoints)
        {
            int currentPoint = 1; //matlab is 1-based!! ==> first actual usage has to be 1
            float[] finalValues = new float[numberOfPoints];
                        
            //fill beginning
            while (currentPoint < sortedIndices[0])
            {
                finalValues[currentPoint-1] = correspondingValues[0] - (correspondingValues[1] - correspondingValues[0]) / (sortedIndices[1] - sortedIndices[0]) * (sortedIndices[0] - currentPoint);
                currentPoint++;
            }

            //fill region defined by calibcoeffs
            for (int i = 1; i < sortedIndices.Length; i++)
            {
                while (currentPoint < sortedIndices[i])
                {
                    finalValues[currentPoint-1] = correspondingValues[i-1] + (correspondingValues[i] - correspondingValues[i-1]) / (sortedIndices[i] - sortedIndices[i-1]) * (currentPoint-sortedIndices[i-1]);
                    currentPoint++;
                }
            }

            //fill ending
            int finalIndex = correspondingValues.Length - 1;
            while (currentPoint < numberOfPoints+1)
            {
                finalValues[currentPoint - 1] = correspondingValues[finalIndex] + (correspondingValues[finalIndex] - correspondingValues[finalIndex - 1]) / (sortedIndices[finalIndex] - sortedIndices[finalIndex-1]) * (currentPoint - sortedIndices[finalIndex]);
                currentPoint++;
            }
            
            //and return resulting interpolated series to calling method
            return finalValues;
        }
        /*
        public static float[] Interpolate1D_XNA(int[] sortedIndices, float[] correspondingValues, int numberOfPoints)
        {
            int currentPoint = 1; //matlab is 1-based!! ==> first actual usage has to be 1
            float[] finalValues = new float[numberOfPoints];

            //CatmullRom requires 4 given points -> fill from begining till second-last index
            for (int i = 2; i < sortedIndices.Length-1; i++)
            {
                while (currentPoint < sortedIndices[i])
                {
                    float interpolationPosition = (float)(currentPoint - sortedIndices[i-1])/(float)(sortedIndices[i] - sortedIndices[i-1]);
                    finalValues[currentPoint - 1] = MathHelper.CatmullRom(correspondingValues[i - 2], correspondingValues[i - 1], correspondingValues[i], correspondingValues[i + 1], interpolationPosition);
                    
                    currentPoint++;
                }
            }

            //fill the final points
            int lastI = sortedIndices.Length - 1;
            while (currentPoint < numberOfPoints+1) //+1, because matlab is 1-based
            {
                float interpolationPosition = (float)(currentPoint - sortedIndices[lastI - 2]) / (float)(sortedIndices[lastI - 1] - sortedIndices[lastI - 2]);
                finalValues[currentPoint - 1] = MathHelper.CatmullRom(correspondingValues[lastI-3], correspondingValues[lastI - 2], correspondingValues[lastI - 1], correspondingValues[lastI], interpolationPosition);

                currentPoint++;
            }

            //and return resulting interpolated series to calling method
            return finalValues;
        }*/

        public static float[] Compensate(Complex[] compensationSpectrum, float[] dataToCompensate)
        {
            //take mean and subtract
            float meanValue = dataToCompensate.Average();
            float[] dcRemoved = new float[dataToCompensate.Length];
            for (int i = 0; i < dcRemoved.Length; i++)
                dcRemoved[i] = dataToCompensate[i] - meanValue;

            //take FFT
            Func<float, Complex> floatToComplex = o => (Complex)o;
            Complex[] fftd = Common.Utils.TransformArray(dcRemoved, floatToComplex);
            FourierTransform.FFT(fftd, FourierTransform.Direction.Forward);

            //compensate
            for (int i = 0; i < fftd.Length; i++)
                fftd[i] = Complex.Multiply(fftd[i], compensationSpectrum[i]);

            //convert back through IFFT
            FourierTransform.FFT(fftd, FourierTransform.Direction.Backward);

            //take only the real part, and shift back
            float[] compensatedData = new float[dataToCompensate.Length];
            for (int i = 0; i < compensatedData.Length; i++)
                compensatedData[i] = (float)fftd[i].Re +meanValue;

            return TimeDomainCompensation(compensatedData);
        }

        private static float[] TimeDomainCompensation(float[] spectrallyCompensatedData)
        {
            float[] finalData = new float[spectrallyCompensatedData.Length];

            for (int i = 1; i < spectrallyCompensatedData.Length; i++)
            {
                finalData[i] = (spectrallyCompensatedData[i] + spectrallyCompensatedData[i - 1]) / 2f;
            }

            return finalData;
        }

    }
}

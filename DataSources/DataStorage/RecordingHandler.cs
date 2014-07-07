using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ECore.DataSources;
using System.IO;
using MatlabFileIO;
using ExcelFileIO;
using Common;

namespace ECore.DataSources
{
    public enum StorageFileFormat
    {
        MATLAB,
        CSV
    }

    public static class Extension 
    {
        public static string GetFileExtension(this StorageFileFormat f) {
            switch (f)
            {
                case StorageFileFormat.MATLAB: return ".mat";
                case StorageFileFormat.CSV: return ".csv";
            }
            throw new Exception("Unknown file format");
        }
    }

    public struct StorageFile
    {
        public FileInfo info;
        public StorageFileFormat format;
        public string proposedPath;
    }

    public static class RecordingHandler
    {
        public static void FinishRecordingAsync(RecordingScope r, StorageFileFormat format, Action<float> progress, Action<StorageFile> success, Action<Exception> failure)
        {
            //Check if recording is done (i.e. no data is gonna be added and we can store it safely)
            Thread frt = new Thread(FinishRecording);
            frt.Start(new object[] { r, format, progress, success, failure });
        }
        private static void FinishRecording(object arg)
        {
            object[] args = arg as object[];
            RecordingScope recording = (RecordingScope)args[0];
            StorageFileFormat format = (StorageFileFormat)args[1];
            Action<float> progress = args[2] as Action<float>;
            Action<StorageFile> success = args[3] as Action<StorageFile>;
            Action<Exception> failure = args[4] as Action<Exception>;
            success(FinishRecording(recording, format, progress));
        }
        public static StorageFile FinishRecording(RecordingScope recording, StorageFileFormat format, Action<float> progress)
        {
            string filename = null;
            switch (format)
            {
                case StorageFileFormat.MATLAB:
                    filename = StoreMatlab(recording, progress);
                    break;
                case StorageFileFormat.CSV:
                    filename = StoreCsv(recording, progress);
                    break;
                default:
                    break;
            }
            //and clean up
            recording.Dispose();

            return new StorageFile() { info = new FileInfo(filename), format = format };
        }

        private static string StoreMatlab(RecordingScope recording, Action<float> progress)
        {
            string filename = Utils.GetTempFileName(".mat");
            MatlabFileWriter matFileWriter = new MatlabFileWriter(filename);
            matFileWriter.Write("Description", "Scope data");

            Type dataType;
            MatLabFileArrayWriter arrayWriter;
            foreach (var pair in recording.channelBuffers)
            {
                dataType = pair.Value.GetDataType();
                arrayWriter = matFileWriter.OpenArray(dataType, pair.Value.GetName());
                int offset = 0;
                foreach (RecordingScope.AcquisitionInfo acqInfo in recording.acqInfo)
                {
                    arrayWriter.AddRow(pair.Value.GetData(offset, acqInfo.samples));
                    offset += acqInfo.samples;
                }
                arrayWriter.FinishArray(dataType);
                if(progress != null)
                    progress(.3f);
            }

#if INTERNAL
            //Save dirty dirty matlab variables to disk
            foreach (var kvp in recording.matlabVariables)
                matFileWriter.Write(kvp.Key, kvp.Value);
#endif

            //Store time axis
            dataType = typeof(double);
            arrayWriter = matFileWriter.OpenArray(dataType, "time");
            for (int i = 0; i < recording.acqInfo.Count; i++)
                arrayWriter.AddRow(getTimeAxis(recording, i, 1));
            arrayWriter.FinishArray(dataType);
            if (progress != null)
                progress(.6f);

            //Store acquisition times
            dataType = typeof(double);
            arrayWriter = matFileWriter.OpenArray(dataType, "acquisitionStartTime");
            UInt64 timeOrigin = recording.acqInfo[0].firstSampleTime;
            arrayWriter.AddRow(recording.acqInfo.Select(x => (double)(x.firstSampleTime - timeOrigin) / 1.0e9).ToArray());
            arrayWriter.FinishArray(dataType);
            if (progress != null)
                progress(.9f);

            //Store settings
            //FIXME: a struct would be better than just dropping all the variables straight in the top level
            foreach (var kvp in recording.settings)
            {
                dataType = typeof(double);
                arrayWriter = matFileWriter.OpenArray(dataType, kvp.Key);
                arrayWriter.AddRow(kvp.Value.ToArray());
                arrayWriter.FinishArray(dataType);
            }

            matFileWriter.Close();
            return filename;
        }

        private static string StoreCsv(RecordingScope recording, Action<float> progress)
        {
            string filename = Utils.GetTempFileName(".csv");
            ExcelFileWriter csvFileWriter = new ExcelFileWriter(filename);
            string[] header = new List<string>() { "Time" }.Concat(recording.channelBuffers.Values.Select(x => x.GetName())).ToArray();
            csvFileWriter.WriteArrayRow(header);

            object[] dataChunk;
            int offset = 0;
            int sampleOffset = 0;
            do
            {
                //Add time axis
                dataChunk = new object[recording.channelBuffers.Count() + 1];
                dataChunk[0] = getTimeAxis(recording, offset, 1);

                //Add data
                int samples = recording.acqInfo[offset].samples;
                int i = 1;
                foreach (IChannelBuffer b in recording.channelBuffers.Values)
                {
                    dataChunk[i] = b.GetData(sampleOffset, samples);
                    i++;
                }
                sampleOffset += samples;
                offset++;

                //Write away
                csvFileWriter.WriteArrayRows(dataChunk);
                if (progress != null)
                    progress(offset / (float)recording.acqInfo.Count);
            } while (offset < recording.acqInfo.Count);
            csvFileWriter.Close();
            return filename;
        }

        private static double[] getTimeAxis(RecordingScope r, int offset = 0, int number = -1)
        {
            if (number < 1) number = r.acqInfo.Count() - offset;
            int totalNumberOfSamples = r.acqInfo.Skip(offset).Take(number).Select(x => x.samples).Sum();
            double[] timeAxis = new double[totalNumberOfSamples];

            int sampleOffset = 0;
            UInt64 timeOrigin = r.acqInfo[0].firstSampleTime;
            for (int l = offset; l < offset + number; l++)
            {
                RecordingScope.AcquisitionInfo inf = r.acqInfo[l];
                double timeZero = (inf.firstSampleTime - timeOrigin) / 1.0e9;
                for (int i = 0; i < inf.samples; i++)
                    timeAxis[sampleOffset + i] = timeZero + inf.samplePeriod * i;

                sampleOffset += inf.samples;
                if (sampleOffset >= totalNumberOfSamples) break;
            }
            return timeAxis;
        }
    }
}

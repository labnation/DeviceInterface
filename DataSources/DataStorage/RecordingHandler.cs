using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using System.IO;
using MatlabFileIO;
using LabNation.Common;
using CsvHelper;
using CsvHelper.Configuration;
using LabNation.Interfaces;

namespace LabNation.DeviceInterface.DataSources
{
    public enum StorageFileFormat
    {
        MATLAB,
        CSV,
        CSV_ZIP
    }

    public static class EnumExtensions
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
            if (recording.Busy)
                throw new Exception("Recording is still ongoing. Stop the recording before storing it");
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
            matFileWriter.Write("Description", "SmartScope storage - data recorded on " + DateTime.Now.ToString());

            Type dataType;
            MatLabFileArrayWriter arrayWriter;
            foreach (var pair in recording.channelBuffers)
            {
                if (pair.Value.BytesStored() > 0)
                {
                    dataType = pair.Value.GetDataType();
                    string matlabFriendlyVariableName = pair.Value.GetName().Replace("-", "_").Replace(" ", "");

                    if (dataType != typeof(DecoderOutput))
                    { // for simple datatypes
                        //matlab doesn't support bools -> save as bytes
                        if (dataType == typeof(bool))
                            arrayWriter = matFileWriter.OpenArray(typeof(byte), matlabFriendlyVariableName, true);
                        else
                            arrayWriter = matFileWriter.OpenArray(dataType, matlabFriendlyVariableName, true);                            
                        
                        for (int i = 0; i < recording.acqInfo.Count; i++)
                        {
                            Array acqData = pair.Value.GetDataOfNextAcquisition();

                            //convert when basetype is not supported by matlab file format
                            if (dataType == typeof(bool))
                                arrayWriter.AddRow(Array.ConvertAll((bool[])acqData, b => b ? (byte)1 : (byte)0));
                            else
                                arrayWriter.AddRow(acqData);
                        }
                        arrayWriter.FinishArray(dataType);
                    }
                    else // array of DecoderOutput objects
                    {
                        List<List<int>> allDecoderOutputTypes = new List<List<int>>();
                        List<List<int>> allStartIndices = new List<List<int>>();
                        List<List<int>> allEndIndices = new List<List<int>>();
                        List<List<string>> allTexts = new List<List<string>>();
                        List<List<byte>> allValues = new List<List<byte>>();

                        int highestRank = 0;
                        for (int i = 0; i < recording.acqInfo.Count; i++)
                        {
                            List<int> decoderOutputTypes = new List<int>();
                            List<int> startIndices = new List<int>();
                            List<int> endIndices = new List<int>();
                            List<string> texts = new List<string>();
                            List<byte> values = new List<byte>();

                            DecoderOutput[] acqData = (DecoderOutput[])pair.Value.GetDataOfNextAcquisition();
                            if (acqData != null)
                            {
                                foreach (DecoderOutput decOut in acqData)
                                {
                                    startIndices.Add(decOut.StartIndex);
                                    endIndices.Add(decOut.EndIndex);
                                    texts.Add(decOut.Text);
                                    if (decOut is DecoderOutputEvent)
                                    {
                                        decoderOutputTypes.Add(0);
                                        values.Add(0);
                                    }
                                    else
                                    {
                                        decoderOutputTypes.Add(1);
                                        if (!(decOut is DecoderOutputValue<byte>))
                                            throw new Exception("Storage of decoder values other than bytes not yet supported!");
                                        values.Add((decOut as DecoderOutputValue<byte>).Value);
                                    }
                                }
                            }

                            allDecoderOutputTypes.Add(decoderOutputTypes);
                            allStartIndices.Add(startIndices);
                            allEndIndices.Add(endIndices);
                            allTexts.Add(texts);
                            allValues.Add(values);

                            highestRank = (int)Math.Max(highestRank, values.Count);
                        }
                        
                        //save all resulting data to file
                        arrayWriter = matFileWriter.OpenArray(typeof(int), matlabFriendlyVariableName + "_DecoderOutputTypes", true);
                        foreach (var row in allDecoderOutputTypes)
                        {
                            int[] arr = row.ToArray();
                            Array.Resize(ref arr, highestRank);
                            arrayWriter.AddRow(arr);
                        }
                        arrayWriter.FinishArray(typeof(int));

                        arrayWriter = matFileWriter.OpenArray(typeof(int), matlabFriendlyVariableName + "_StartIndex", true);
                        foreach (var row in allStartIndices)
                        {
                            int[] arr = row.ToArray();
                            Array.Resize(ref arr, highestRank);
                            arrayWriter.AddRow(arr);
                        }
                        arrayWriter.FinishArray(typeof(int));

                        arrayWriter = matFileWriter.OpenArray(typeof(int), matlabFriendlyVariableName + "_EndIndex", true);
                        foreach (var row in allEndIndices)
                        {
                            int[] arr = row.ToArray();
                            Array.Resize(ref arr, highestRank);
                            arrayWriter.AddRow(arr);
                        }
                        arrayWriter.FinishArray(typeof(int));

                        for (int i = 0; i < allTexts.Count; i++)
                        {
                            List<string> acqTexts = allTexts.ElementAt(i);

                            //first find max string length of this acq
                            int maxLength = 0;
                            foreach (string txt in acqTexts)
                                maxLength = (int)Math.Max(maxLength, txt.Length);

                            //matlab string array requires even length
                            if (maxLength % 2 != 0) maxLength++;

                            arrayWriter = matFileWriter.OpenArray(typeof(char), matlabFriendlyVariableName + "_Text_Acq"+(i+1).ToString("00000"), true);
                            foreach (string str in acqTexts)
                                arrayWriter.AddRow(str.PadRight(maxLength).ToCharArray());
                            arrayWriter.FinishArray(typeof(char));
                        }

                        arrayWriter = matFileWriter.OpenArray(typeof(byte), matlabFriendlyVariableName + "_Value", true);
                        foreach (var row in allValues)
                        {
                            byte[] arr = row.ToArray();
                            Array.Resize(ref arr, highestRank);
                            arrayWriter.AddRow(arr);
                        }
                        arrayWriter.FinishArray(typeof(byte));
                    }

                    if (progress != null)
                        progress(.3f);
                }
            }

            if (progress != null)
                progress(.6f);

            //Store acquisition times
            dataType = typeof(double);
            arrayWriter = matFileWriter.OpenArray(dataType, "AcquisitionStartTimeInSeconds", true);
            UInt64 timeOrigin = recording.acqInfo[0].firstSampleTime;
            arrayWriter.AddRow(recording.acqInfo.Select(x => (double)(x.firstSampleTime - timeOrigin) / (double)1.0e9).ToArray());
            arrayWriter.FinishArray(dataType);
            
            //store sampleFrequency
            matFileWriter.Write("SamplePeriodInSeconds", recording.acqInfo[0].samplePeriod);

            if (progress != null)
                progress(.9f);

            //Store settings
            //FIXME: a struct would be better than just dropping all the variables straight in the top level
            foreach (var kvp in recording.settings)
            {
                dataType = typeof(double);
                arrayWriter = matFileWriter.OpenArray(dataType, kvp.Key, true);
                arrayWriter.AddRow(kvp.Value.ToArray());
                arrayWriter.FinishArray(dataType);
            }

            matFileWriter.Close();
            return filename;
        }

        private static string StoreCsv(RecordingScope recording, Action<float> progress)
        {
            string filename = Utils.GetTempFileName(".csv");
            StreamWriter streamWriter = File.CreateText(filename);
            string delimiter = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;

            /* Header definition */
            //description
            string[] column0 = new string[2] {"Description" , "SmartScope storage - data recorded on " + DateTime.Now.ToString() };
            
            //acq IDs + their starttime
            UInt64 timeOrigin = recording.acqInfo[0].firstSampleTime;
            string[] column1 = new string[recording.acqInfo.Count+1];
            column1[0] = "AcquisitionID";            
            string[] column2 = new string[recording.acqInfo.Count+1];
            column2[0] = "AcquisitionStartTimeInSeconds";
            for (int i = 0; i < recording.acqInfo.Count; i++)
            {
                column1[i+1] = i.ToString();
                column2[i+1] = (((double)recording.acqInfo[i].firstSampleTime - (double)timeOrigin)/(double)1.0e9).ToString();
            }

            //sample period
            string[] column3 = new string[2] { "SamplePeriodInSeconds", recording.acqInfo[0].samplePeriod.ToString() };

            List<string[]> headerColumns = new List<string[]>();
            headerColumns.Add(column0);
            headerColumns.Add(column1);
            headerColumns.Add(column2);
            headerColumns.Add(column3);

            /* First row */

            Type dataType;
            int nbrOfSamples = 0;
            int nbrColumns = 0;
            
            //first row: header columns
            foreach (string[] headerColumn in headerColumns)
                streamWriter.Write(headerColumn[0] + delimiter);

            //first row: channel names
            foreach (var pair in recording.channelBuffers)
            {
                if (pair.Value.BytesStored() > 0)
                {
                    dataType = pair.Value.GetDataType();
                    Array acqData = pair.Value.GetDataOfNextAcquisition();
                    nbrOfSamples = (int)Math.Max(nbrOfSamples, acqData.Length);
                    string variableName = pair.Value.GetName().Replace("-", "_").Replace(" ", "");

                    if (dataType != typeof(DecoderOutput))
                    {
                        for (int a = 0; a < recording.AcquisitionsRecorded; a++)
                        {
                            streamWriter.Write(variableName + "_Acq" + a.ToString("00000") + delimiter);
                            nbrColumns++;
                        }
                    }
                    else
                    {
                        for (int a = 0; a < recording.AcquisitionsRecorded; a++)
                        {
                            streamWriter.Write(variableName + "_Acq" + a.ToString("00000") + "_StartIndices" +delimiter);
                            streamWriter.Write(variableName + "_Acq" + a.ToString("00000") + "_EndIndices" +delimiter);
                            streamWriter.Write(variableName + "_Acq" + a.ToString("00000") + "_Texts" + delimiter);
                            streamWriter.Write(variableName + "_Acq" + a.ToString("00000") + "_Values" + delimiter);
                            nbrColumns++; //only count each decoder as 1 column
                        }
                    }
                }
            }
            streamWriter.WriteLine();

            /* All other rows */

            //coming up: data!
            int sampleCounter = 0;
            int maxElementsInRAM = 1000000; //defining max RAM consumption. Higher will go faster but consume more RAM!
            int bufferSize = maxElementsInRAM / nbrColumns; 
            Dictionary<Array, Type> buffers = new Dictionary<Array, Type>();
            while (sampleCounter < nbrOfSamples)
            {                
                buffers.Clear();
                int toBufferNow = 0;
                
                //pop data from streams into buffers
                foreach (var pair in recording.channelBuffers)
                {                    
                    if (pair.Value.BytesStored() > 0)
                    {
                        dataType = pair.Value.GetDataType();
                        pair.Value.Rewind();
                        for (int a = 0; a < recording.AcquisitionsRecorded; a++)
                        {
                            Array acqData = pair.Value.GetDataOfNextAcquisition();
                            
                            if (acqData != null) //should never be the case, just here for code safey
                                toBufferNow = (int)Math.Min(bufferSize, acqData.Length - sampleCounter);
                            if (toBufferNow < 0)
                                toBufferNow = 0;

                            Array buffer = Array.CreateInstance(dataType, toBufferNow);
                            if (toBufferNow > 0)
                                Array.Copy(acqData, sampleCounter, buffer, 0, toBufferNow);
                            buffers.Add(buffer, dataType);
                        }
                    }
                }                

                //now write all buffers from RAM to disk
                for (int i = 0; i < toBufferNow; i++)
                {
                    progress((float)(sampleCounter+i) / (float)nbrOfSamples);

                    //first write header columns
                    foreach (string[] headerColumn in headerColumns)
                    {
                        if (headerColumn.Length > 1 + sampleCounter + i)
                            streamWriter.Write(headerColumn[1 + sampleCounter + i] + delimiter);
                        else
                            streamWriter.Write(delimiter);
                    }

                    //write all data columns
                    foreach (var kvp in buffers)
                    {
                        Array acqData = kvp.Key;
                        dataType = kvp.Value;
                        if (dataType != typeof(DecoderOutput))
                        {
                            if (acqData.Length >= i + 1)
                            {
                                streamWriter.Write(acqData.GetValue(i).ToString() + delimiter);
                            }
                            else
                            {
                                streamWriter.Write(delimiter);
                            }
                        }
                        else
                        {//DecoderOutput
                            if (acqData.Length >= i + 1)
                            {
                                DecoderOutput decOut = (DecoderOutput)acqData.GetValue(i);
                                streamWriter.Write(decOut.StartIndex.ToString() + delimiter);
                                streamWriter.Write(decOut.EndIndex.ToString() + delimiter);
                                streamWriter.Write(decOut.Text.ToString() + delimiter);
                                if (decOut is DecoderOutputValue<byte>)
                                    streamWriter.Write((decOut as DecoderOutputValue<byte>).Value.ToString() + delimiter);
                                else
                                    streamWriter.Write(delimiter);
                            }
                            else
                            {
                                streamWriter.Write(delimiter + delimiter + delimiter + delimiter);
                            }
                        }
                    }
                    streamWriter.WriteLine();
                }

                //update while loop constraint
                sampleCounter += bufferSize;
            }

            streamWriter.Close();
            return filename;
        }

        private static string StoreCsvHorizontal(RecordingScope recording, Action<float> progress)
        {
            string filename = Utils.GetTempFileName(".csv");
            StreamWriter streamWriter = File.CreateText(filename);
            string delimiter = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;

            streamWriter.WriteLine("Description"+delimiter+ "SmartScope storage - data recorded on " + DateTime.Now.ToString());
            
            //acq IDs + their starttime
            UInt64 timeOrigin = recording.acqInfo[0].firstSampleTime;
            streamWriter.Write("AcquisitionID"+delimiter);
            for (int i = 0; i < recording.acqInfo.Count; i++)
                streamWriter.Write(i.ToString() + delimiter);
            streamWriter.WriteLine();
            streamWriter.Write("AcquisitionStartTimeInSeconds" + delimiter);
            streamWriter.WriteLine(String.Join(delimiter, recording.acqInfo.Select(x => ((double)(x.firstSampleTime - timeOrigin) / (double)1.0e9).ToString()).ToArray()));            

            Type dataType;
            int nbrOfSamples = 0;
            foreach (var pair in recording.channelBuffers)
            {
                if (pair.Value.BytesStored() > 0)
                {
                    string variableName = pair.Value.GetName().Replace("-", "_").Replace(" ", "");                    

                    dataType = pair.Value.GetDataType();                    
                    { // for simple datatypes                        
                        for (int i = 0; i < recording.acqInfo.Count; i++)
                        {
                            Array acqData = pair.Value.GetDataOfNextAcquisition();
                            nbrOfSamples = (int)Math.Max(nbrOfSamples, acqData.Length);

                            if (dataType != typeof(DecoderOutput))
                            {                                
                                string stringData = "Datatype not supported for CSV output";
                                if (dataType == typeof(float))
                                    stringData = String.Join(delimiter, ((float[])acqData).Select(x => x.ToString()).ToArray());
                                else if (dataType == typeof(byte))
                                    stringData = String.Join(delimiter, ((byte[])acqData).Select(x => x.ToString()).ToArray());
                                else if (dataType == typeof(int))
                                    stringData = String.Join(delimiter, ((int[])acqData).Select(x => x.ToString()).ToArray());
                                if (dataType == typeof(bool))
                                    stringData = String.Join(delimiter, ((bool[])acqData).Select(x => x.ToString()).ToArray());
                                streamWriter.WriteLine(variableName + "_Acq" + i.ToString("00000") + delimiter + stringData);
                            }
                            else
                            {// in case of decoder output
                                DecoderOutput[] decOut = (DecoderOutput[])acqData;
                                var unsupportedOutputs = decOut.Where(x => !(x is DecoderOutputEvent || x is DecoderOutputValue<byte>)).ToList();
                                if (unsupportedOutputs.Count > 0)
                                    streamWriter.WriteLine("Decoder " + variableName + " contains types which are not supported for CSV export. Please request support on the forum.");
                                else
                                { //only DecoderOutputEvent or DecoderOutputValue<byte>

                                    string startIndices = String.Join(delimiter, (decOut).Select(x => x.StartIndex.ToString()).ToArray());
                                    string endIndices = String.Join(delimiter, (decOut).Select(x => x.EndIndex.ToString()).ToArray());
                                    string texts = String.Join(delimiter, (decOut).Select(x => x.Text).ToArray());
                                    string values = String.Join(delimiter, (decOut).Select(x => (x is DecoderOutputEvent) ? "" : (x as DecoderOutputValue<byte>).Value.ToString()).ToArray());

                                    streamWriter.WriteLine(variableName + "_Acq" + i.ToString("00000") + "_StartIndices" + delimiter + startIndices);
                                    streamWriter.WriteLine(variableName + "_Acq" + i.ToString("00000") + "_EndIndices" + delimiter + endIndices);
                                    streamWriter.WriteLine(variableName + "_Acq" + i.ToString("00000") + "_Texts" + delimiter + texts);
                                    streamWriter.WriteLine(variableName + "_Acq" + i.ToString("00000") + "_Values" + delimiter + values);

                                    //create visual record
                                    streamWriter.Write(variableName + "_Acq" + i.ToString("00000") + "_Visual" + delimiter);
                                    int currentIndex = 0;
                                    for (int x = 0; x < decOut.Length; x++)
                                    {
                                        //put . in between communications
                                        for (int y = currentIndex; y < decOut[x].StartIndex; y++)
                                            streamWriter.Write("."+delimiter);

                                        streamWriter.Write(decOut[x].Text);
                                        if (decOut[x] is DecoderOutputValue<byte>)
                                            streamWriter.Write(": " + (decOut[x] as DecoderOutputValue<byte>).Value.ToString());
                                        streamWriter.Write(delimiter);

                                        //put ---- till communication is finished
                                        for (int y = decOut[x].StartIndex + 1; y < decOut[x].EndIndex; y++)
                                            streamWriter.Write("---------"+delimiter);

                                        currentIndex = decOut[x].EndIndex;
                                    }
                                    //put . till the end
                                    for (int y = currentIndex; y < nbrOfSamples; y++)
                                        streamWriter.Write("." + delimiter);
                                    streamWriter.WriteLine(); //end visual record
                                }
                            }
                        }
                    }
                }
            }

            streamWriter.Close();
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

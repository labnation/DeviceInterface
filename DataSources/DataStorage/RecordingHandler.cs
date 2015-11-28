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
            matFileWriter.Write("Description", "Scope data");

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
                        arrayWriter = matFileWriter.OpenArray(dataType, matlabFriendlyVariableName, true);
                        for (int i = 0; i < recording.acqInfo.Count; i++)
                        {
                            Array acqData = pair.Value.GetDataOfNextAcquisition();
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
                            row.Capacity = highestRank;
                            arrayWriter.AddRow(row.ToArray());
                        }
                        arrayWriter.FinishArray(typeof(int));

                        arrayWriter = matFileWriter.OpenArray(typeof(int), matlabFriendlyVariableName + "_StartIndex", true);
                        foreach (var row in allStartIndices)
                        {
                            row.Capacity = highestRank;
                            arrayWriter.AddRow(row.ToArray());
                        }
                        arrayWriter.FinishArray(typeof(int));

                        arrayWriter = matFileWriter.OpenArray(typeof(int), matlabFriendlyVariableName + "_EndIndex", true);
                        foreach (var row in allEndIndices)
                        {
                            row.Capacity = highestRank;
                            arrayWriter.AddRow(row.ToArray());
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
                            row.Capacity = highestRank;
                            arrayWriter.AddRow(row.ToArray());
                        }
                        arrayWriter.FinishArray(typeof(byte));
                    }

                    if (progress != null)
                        progress(.3f);
                }
            }

            #if false
            //Store time axis
            dataType = typeof(double);
            arrayWriter = matFileWriter.OpenArray(dataType, "time", true);
            for (int i = 0; i < recording.acqInfo.Count; i++)
                arrayWriter.AddRow(getTimeAxis(recording, i, 1));
            arrayWriter.FinishArray(dataType);
            #endif
            if (progress != null)
                progress(.6f);

            //Store acquisition times
            dataType = typeof(double);
            arrayWriter = matFileWriter.OpenArray(dataType, "acquisitionStartTime", true);
            UInt64 timeOrigin = recording.acqInfo[0].firstSampleTime;
            arrayWriter.AddRow(recording.acqInfo.Select(x => (double)(x.firstSampleTime - timeOrigin) / (double)1.0e9).ToArray());
            arrayWriter.FinishArray(dataType);
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

        public class RecordingRecord
        {
            public RecordingRecord() {}
            public RecordingRecord(double sampleTime)
            {
                this.sampleTime = sampleTime;
            }

            public double? sampleTime{get;set;}
            public float? chA {get; set;}
            public float? chB {get; set;}
            public byte? logicAnalyser {get; set;}
        }

        public sealed class RecordingRecordMapper : CsvClassMap<RecordingRecord>
        {
            public RecordingRecordMapper()
            {
                Map(x=>x.sampleTime).Name("SampleTime");
                Map(x => x.chA).Name("Channel A");
                Map(x => x.chB).Name("Channel B");
                Map(x => x.logicAnalyser).Name("Logic Analyser");
            }
        }

        private static string StoreCsv(RecordingScope recording, Action<float> progress)
        {
            string filename = Utils.GetTempFileName(".csv");
            StreamWriter textWriter = File.CreateText(filename);
            CsvWriter csvFileWriter = new CsvWriter(textWriter);

            //Construct records
            List<RecordingRecord> records = new List<RecordingRecord>();
            int nSamples = 0;
            UInt64 timeOrigin = recording.acqInfo[0].firstSampleTime;
            for(int i =0; i< recording.acqInfo.Count(); i++)
            {
                var acqInfo = recording.acqInfo[i];
                records.Add(new RecordingRecord((double)(acqInfo.firstSampleTime - timeOrigin) / (double)1.0e9));
                for(int j = 1; j < acqInfo.samples; j++)
                    records.Add(new RecordingRecord());
                nSamples += acqInfo.samples;
            }


            foreach (var pair in recording.channelBuffers)
            {
                object data = pair.Value.GetDataOfNextAcquisition();
                float[] floatData = null;
                byte[] byteData = null;
                if(data.GetType() == typeof(float[]))
                    floatData = (float[])data;
                if(data.GetType() == typeof(byte[]))
                    byteData = (byte[])data;

                for(int i = 0; i < nSamples; i++)
                {
                    if(pair.Key == AnalogChannel.ChA && floatData.Length == nSamples)
                        records[i].chA = floatData[i];
					else if(pair.Key == AnalogChannel.ChB && floatData.Length == nSamples)
                        records[i].chB = floatData[i];
					else if(pair.Key == LogicAnalyserChannel.LA && byteData.Length == nSamples)
                        records[i].logicAnalyser = byteData[i];
                }
            }
            csvFileWriter.Configuration.RegisterClassMap<RecordingRecordMapper>();
            csvFileWriter.Configuration.HasExcelSeparator=true;
            csvFileWriter.WriteRecords(records);
            textWriter.Close();
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

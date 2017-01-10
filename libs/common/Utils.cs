using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Reflection;
#if WINDOWS
using System.Management;
#elif ANDROID
using Android.OS.Storage;
#endif

namespace LabNation.Common
{
    public static class Utils
    {
        public static List<FileInfo> GetFiles(string dir, string pattern, List<FileInfo> files = null)
        {
            if (files == null)
                files = new List<FileInfo>();
            foreach (string file in Directory.GetFiles(dir, pattern,
                                                   SearchOption.TopDirectoryOnly))
            {
                files.Add(new FileInfo(file));
            }
            foreach (string subDir in Directory.GetDirectories(dir))
            {
                try
                {
                    GetFiles(subDir, pattern, files);
                }
                catch
                {
                }
            }
            return files;
        }

        public static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern)
                              .Replace(@"\*", ".*")
                              .Replace(@"\?", ".")
                       + "$";
        }

        public static string parseSerialFromDeviceID(string deviceId)
        {
            string[] splitDeviceId = deviceId.Split('\\');
            string[] serialArray;
            string serial;
            int arrayLen = splitDeviceId.Length - 1;

            serialArray = splitDeviceId[arrayLen].Split('&');
            serial = serialArray[0];

            return serial;
        }


        static public bool HasMethod(Object o, String methodName)
        {
            return o.GetType().GetMethod(methodName) != null;
        }

        static public String SnakeToCamel(String input)
        {
            bool new_word = true;
            string result = string.Concat(input.Select((x, i) =>
            {
                String ret = "";
                if (x == '_')
                    new_word = true;
                else if (new_word)
                {
                    ret = x.ToString().ToUpper();
                    new_word = false;
                }
                else
                    ret = x.ToString().ToLower();
                return ret;
            }));
            return result;
        }

        static public O[] CastArray<I, O>(I[] input)
        {
            O[] output = new O[input.Length];
            for (int i = 0; i < input.Length; i++)
                output[i] = (O)Convert.ChangeType(input[i], typeof(O));
            return output;
        }

        /// <summary>
        /// Applies operator on each element of an input array
        /// </summary>
        /// <typeparam name="I">Type of input array elements</typeparam>
        /// <typeparam name="O">Type of output array elements</typeparam>
        /// <param name="input">Input array</param>
        /// <param name="op">Operator lambda expression</param>
        public static O[] TransformArray<I, O>(Array input, Func<I, O> op)
        {
            if (input == null) return null;
            I[] inputCast = (I[])input;
            O[] output = new O[input.Length];
            for (int i = 0; i < inputCast.Length; i++)
            {
                output[i] = op(inputCast[i]);
            }
            return output;
        }
        /// <summary>
        /// Combines 2 arrays into a new one by applying lamba on each element
        /// </summary>
        /// <typeparam name="I1">Type of input1 array elements</typeparam>
        /// <typeparam name="I2">Type of input2 array elements</typeparam>
        /// <typeparam name="O">Type of returned array elemenets</typeparam>
        /// <param name="input1">Array with first argument of lambda</param>
        /// <param name="input2">Array with second argument of lambda</param>
        /// <param name="op">Lambda, i.e. to sum 2 arrays: Func&lt;T,T,T&gt; sum = (x, y) => x + y"/></param>
        /// <returns></returns>
        public static O[] CombineArrays<I1, I2, O>(I1[] input1, I2[] input2, Func<I1, I2, O> op)
        {
            if (input1 == null || input2 == null) return null;
            int resultLength = Math.Min(input1.Length, input2.Length);
            O[] output = new O[resultLength];
            for (int i = 0; i < resultLength; i++)
            {
                output[i] = op(input1[i], input2[i]);
            }
            return output;
        }

        public static bool[][] ByteArrayToBoolArrays(byte[] input)
        {
            bool[][] output = new bool[8][];
            for (int i = 0; i < 8; i++)
                output[i] = new bool[input.Length];

            for (int i = 0; i < input.Length; i++)
            {
                for (int j = 0; j < 8; j++)
                    output[j][i] = IsBitSet(input[i], j);
            }
            return output;
        }
        /// <summary>
        /// Shift the elements of an array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="shift">Positive shift to the right, negative to the left</param>
        /// <returns></returns>
        public static T[] ShiftArray<T>(T[] input, int shift)
        {
            if (shift == 0) return input;

            T[] output = new T[input.Length];
            if (Math.Abs(shift) >= input.Length) return output;

            for (int i = Math.Max(0, shift); i < Math.Min(input.Length, input.Length + shift); i++)
            {
                output[i] = input[i - shift];
            }
            return output;
        }

        /// <summary>
        /// Returns new array of size input.Length/decimation containing every [decimation]-th sample of input
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="decimation"></param>
        /// <returns></returns>
        public static T[] DecimateArray<T>(T[] input, uint decimation)
        {
            T[] output = new T[input.Length / decimation];
            for (int i = 0; i < output.Length; i++)
                output[i] = input[decimation * i];
            return output;
        }

        public static string ApplicationDataPath
        {
            get
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
#if !__IOS__ && !ANDROID
                path = Path.Combine(path, "LabNation");
#endif
                System.IO.Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string ExecutablePath {
			get {
				#if ANDROID
				throw new IOException("Can't use executable path on Android!");
				#else
				return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				#endif
			}
		}

		public static string PluginPathDropbox {
			get {
				return Path.Combine(StoragePath, "Plugins", "dropbox");
			}
		}

        public static string[] PluginPaths
        {
            get
            {
                return new string[] {
                    Path.Combine(StoragePath, "Plugins"),
                    PluginPathDropbox
                };
            }
        }

        public static string StoragePath
        {
            get
            {
                string path = 
#if ANDROID
                Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
#else
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.DoNotVerify);
#endif
#if !__IOS__
                path = Path.Combine(path, "LabNation");
#endif
                System.IO.Directory.CreateDirectory(path);
                return path;
            }
        }

        public static bool Schmitt(float value, bool previousValue, float thresholdHigh, float thresholdLow)
        {
            if (value > thresholdHigh)
                return true;
            else if (value < thresholdLow)
                return false;
            else
                return previousValue;
        }

        public static bool[] Schmitt(float[] analogData)
        {
            return Schmitt(analogData, analogData.Min(), analogData.Max());
        }
        public static bool[] Schmitt(float[] analogData, float minValue, float maxValue)
        {
            if (analogData == null) return null;

            float H = minValue + (maxValue - minValue) * 0.7f;
            float L = minValue + (maxValue - minValue) * 0.3f;

            bool[] digitalData = new bool[analogData.Length];
            bool digitalDataPrevious = analogData[0] >= H;
            for (int i = 0; i < analogData.Length; i++)
                digitalDataPrevious = digitalData[i] = Utils.Schmitt(analogData[i], digitalDataPrevious, H, L);
            return digitalData;
        }

        public static string GetPrettyDate(DateTime d)
        {
            // 1.
            // Get time span elapsed since the date.
            TimeSpan s = DateTime.Now.Subtract(d);

            // 2.
            // Get total number of days elapsed.
            int dayDiff = (int)s.TotalDays;

            // 3.
            // Get total number of seconds elapsed.
            int secDiff = (int)s.TotalSeconds;

            // 4.
            // Don't allow out of range values.
            if (dayDiff < 0 || dayDiff >= 31)
            {
                return null;
            }

            // 5.
            // Handle same-day times.
            if (dayDiff == 0)
            {
                // A.
                // Less than one minute ago.
                if (secDiff < 60)
                {
                    return "just now";
                }
                // B.
                // Less than 2 minutes ago.
                if (secDiff < 120)
                {
                    return "1 minute ago";
                }
                // C.
                // Less than one hour ago.
                if (secDiff < 3600)
                {
                    return string.Format("{0} minutes ago",
                        Math.Floor((double)secDiff / 60));
                }
                // D.
                // Less than 2 hours ago.
                if (secDiff < 7200)
                {
                    return "1 hour ago";
                }
                // E.
                // Less than one day ago.
                if (secDiff < 86400)
                {
                    return string.Format("{0} hours ago",
                        Math.Floor((double)secDiff / 3600));
                }
            }
            // 6.
            // Handle previous days.
            if (dayDiff == 1)
            {
                return "yesterday";
            }
            if (dayDiff < 7)
            {
                return string.Format("{0} days ago",
                dayDiff);
            }
            if (dayDiff < 31)
            {
                return string.Format("{0} weeks ago",
                Math.Ceiling((double)dayDiff / 7));
            }
            return null;
        }
        /// <summary>
        /// Reads file into byte array of length N*multiple. Stuffs with stuffing
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="multiple">0 disables stuffing</param>
        /// <param name="stuffing"></param>
        /// <returns></returns>
        public static byte[] FileToByteArray(string fileName, int multiple, byte stuffing)
        {
            FileStream fs = new FileStream(fileName,
                                           FileMode.Open,
                                           FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            long numBytes = new FileInfo(fileName).Length;

            byte[] buff = null;
            buff = br.ReadBytes((int)numBytes);
            return ByteBufferStuffer(buff, multiple, stuffing);
        }

        public static byte[] ByteBufferStuffer(byte[] buff, int multiple, byte stuffing)
        {
            //Check if a multiple was specified or if we happen to be lucky
            if (multiple <= 0 || buff.Length % multiple == 0)
                return buff;

            //Stuff otherwise
            byte[] stuffed = new byte[buff.Length + multiple - (buff.Length % multiple)];
            Array.Copy(buff, stuffed, buff.Length);
            for (int i = buff.Length; i < stuffed.Length; i++)
                stuffed[i] = stuffing;

            return stuffed;

        }

        public static void SetBit(ref byte b, int bit)
        {
            int mask = 0x01 << bit;
            b |= (byte)mask;
        }

        public static void ClearBit(ref byte b, int bit)
        {
            int mask = 0x01 << bit;
            b &= (byte)(~mask);
        }


        public static bool IsBitSet(byte b, int bit)
        {
            return ((b >> bit) & 0x01) != 0;
        }

        public static UInt16 nextFpgaTestVector(UInt16 input)
        {
            if (input == 0x8000)
                return 0;
            else
            {
                if ((input & 0x8000) == 0x0000)
                    return (UInt16)~input;
                else
                    return (UInt16)(~input + 1);
            }
        }
        public static byte[] BitReverseTable =
        {
            0x00, 0x80, 0x40, 0xc0, 0x20, 0xa0, 0x60, 0xe0,
            0x10, 0x90, 0x50, 0xd0, 0x30, 0xb0, 0x70, 0xf0,
            0x08, 0x88, 0x48, 0xc8, 0x28, 0xa8, 0x68, 0xe8,
            0x18, 0x98, 0x58, 0xd8, 0x38, 0xb8, 0x78, 0xf8,
            0x04, 0x84, 0x44, 0xc4, 0x24, 0xa4, 0x64, 0xe4,
            0x14, 0x94, 0x54, 0xd4, 0x34, 0xb4, 0x74, 0xf4,
            0x0c, 0x8c, 0x4c, 0xcc, 0x2c, 0xac, 0x6c, 0xec,
            0x1c, 0x9c, 0x5c, 0xdc, 0x3c, 0xbc, 0x7c, 0xfc,
            0x02, 0x82, 0x42, 0xc2, 0x22, 0xa2, 0x62, 0xe2,
            0x12, 0x92, 0x52, 0xd2, 0x32, 0xb2, 0x72, 0xf2,
            0x0a, 0x8a, 0x4a, 0xca, 0x2a, 0xaa, 0x6a, 0xea,
            0x1a, 0x9a, 0x5a, 0xda, 0x3a, 0xba, 0x7a, 0xfa,
            0x06, 0x86, 0x46, 0xc6, 0x26, 0xa6, 0x66, 0xe6,
            0x16, 0x96, 0x56, 0xd6, 0x36, 0xb6, 0x76, 0xf6,
            0x0e, 0x8e, 0x4e, 0xce, 0x2e, 0xae, 0x6e, 0xee,
            0x1e, 0x9e, 0x5e, 0xde, 0x3e, 0xbe, 0x7e, 0xfe,
            0x01, 0x81, 0x41, 0xc1, 0x21, 0xa1, 0x61, 0xe1,
            0x11, 0x91, 0x51, 0xd1, 0x31, 0xb1, 0x71, 0xf1,
            0x09, 0x89, 0x49, 0xc9, 0x29, 0xa9, 0x69, 0xe9,
            0x19, 0x99, 0x59, 0xd9, 0x39, 0xb9, 0x79, 0xf9,
            0x05, 0x85, 0x45, 0xc5, 0x25, 0xa5, 0x65, 0xe5,
            0x15, 0x95, 0x55, 0xd5, 0x35, 0xb5, 0x75, 0xf5,
            0x0d, 0x8d, 0x4d, 0xcd, 0x2d, 0xad, 0x6d, 0xed,
            0x1d, 0x9d, 0x5d, 0xdd, 0x3d, 0xbd, 0x7d, 0xfd,
            0x03, 0x83, 0x43, 0xc3, 0x23, 0xa3, 0x63, 0xe3,
            0x13, 0x93, 0x53, 0xd3, 0x33, 0xb3, 0x73, 0xf3,
            0x0b, 0x8b, 0x4b, 0xcb, 0x2b, 0xab, 0x6b, 0xeb,
            0x1b, 0x9b, 0x5b, 0xdb, 0x3b, 0xbb, 0x7b, 0xfb,
            0x07, 0x87, 0x47, 0xc7, 0x27, 0xa7, 0x67, 0xe7,
            0x17, 0x97, 0x57, 0xd7, 0x37, 0xb7, 0x77, 0xf7,
            0x0f, 0x8f, 0x4f, 0xcf, 0x2f, 0xaf, 0x6f, 0xef,
            0x1f, 0x9f, 0x5f, 0xdf, 0x3f, 0xbf, 0x7f, 0xff
        };
        public static byte ReverseWithLookupTable(byte toReverse)
        {
            return BitReverseTable[toReverse];
        }

        static public float[] CastBoolArrayToFloatArray(bool[] input)
        {
            Func<bool, float> boolToFloat = x => x ? 1f : 0f;
            return Utils.TransformArray(input, boolToFloat);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="number">The number to format</param>
        /// <param name="significance">Significant numbers</param>
        /// <returns></returns>
        static public string numberSignificanceFormat(double number, int significance)
        {
            //If no significance specified, return the entire number
            if (significance == 0 || double.IsNaN(number))
                return String.Format("{0}", number);

            number = significanceTruncate(number, significance);
            //Split into whole and decimal parts
            //i.e. 123.456
            //parts[0] = "123"
            //parts[1] = "456"
            string[] parts = Math.Abs(number).ToString().Split(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]);

            //Take the whole part for as far as possible
            //i.e. if significance is 2 --> result becomes "12"
            string result = parts[0].Substring(0, (Math.Min(parts[0].Length, significance)));

            //If we achieved the significance return but add 0's if necessary
            if (result.Length == significance)
            {
                //i.e. in the example above, need to add one "0" to achieve "120"
                if (result.Length < parts[0].Length)
                {
                    result = String.Concat(result, new String('0', parts[0].Length - result.Length));
                }
            }
            //Add decimal part
            //i.e. if significance is 4, result so far is "123"
            else
            {
                //Add decimal dot
                result += System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
                //Make sure we actually have a decimal part
                if(parts.Length == 2)
                    result += parts[1].Substring(0, Math.Min(parts[1].Length, significance - parts[0].Length));
                if (result.Length - 1 < significance)
                {
                    //Pad with zeroes to achieve significance
                    //i.e. if significance was 8, will result in "123.45600"
                    result = String.Concat(result, new String('0', significance - (result.Length-1)));
                }
            }

            return number < 0 ? "-" + result : result;
        }

        static public double precisionRound(double number, double precision)
        {
            if (precision == 0)
                return number;
            return Math.Round(number / precision) * precision;
        }

        static public double precisionFloor(double number, double precision)
        {
            if (precision == 0)
                return number;
            return Math.Floor(number / precision) * precision;
        }


        /// <summary>
        /// Truncates a number to have [significance] significant figures
        /// </summary>
        /// <param name="number"></param>
        /// <param name="significance"></param>
        /// <returns></returns>
        static public double significanceTruncate(double number, int significance)
        {
            if (significance == 0)
                return number;
            if (number == 0 || double.IsNaN(number))
                return number;
            int scale = (int)Math.Floor(Math.Log10(Math.Abs(number)));
            int maxScale = scale - significance + 1;
            double rounder = Math.Pow(10, maxScale);
            return Math.Round(Math.Abs(number) / rounder) /(1/ rounder) * Math.Sign(number);
        }

        static public string precisionFormat(double number, double precision, int significance)
        {
            return numberSignificanceFormat(precisionRound(number, precision), significance);
        }

		static public string siPrint(double number, double precision, int significance, string unit, int thousand = 1000)
        {
			return siScale(number, precision, significance, thousand) + " " + siPrefix(number, precision, unit, thousand);
        }

        /// <summary>
        /// Convert a number to SI scale, given a certain precision and significance
        /// 
        /// (123456.789,   100, 4) --> 123.4
        /// (123456.789, 10000, 1) --> 100
        /// (123456.789, 10000, 3) --> 120
        /// ( 23456.789, 10000, 3) --> 20
        /// ( 23456.789,    10, 3) --> 23.4
        /// ( 23456.789,    10, 5) --> 23.450
        /// </summary>
        /// <param name="number">The number to scale</param>
        /// <param name="precision">The precision to which to round the number</param>
        /// <param name="significance">The number of significant figure in the result</param>
		/// <param name="thousand">Decimal value of a kilo (e.g. 1000 or 1024)</param>
        /// <returns></returns>
        static public string siScale(double number, double precision, int significance, int thousand = 1000)
        {
            //Round to the specified precision
            number = precisionRound(number, precision);
            //Then scale it to si scale
            double divider = number == 0 ? 1 : Math.Floor((Math.Log(Math.Abs(number), thousand)));
            divider = Math.Pow(thousand, divider);
            number = number / divider;
            return numberSignificanceFormat(number, significance);
        }
        static public string siReferencedScale(double reference, double number, double precision, int significance, int thousand = 1000)
        {
            //Round to the specified precision
            reference = precisionRound(reference, precision);
            //Then scale it to si scale
            double divider = reference == 0 ? 1 : Math.Floor((Math.Log(Math.Abs(reference), thousand)));
            divider = Math.Pow(thousand, divider);

            number = precisionRound(number, precision);
            number = number / divider;
            return number.ToString();
        }
        static public string siPrefix(double number, double precision, string unit, int thousand = 1000)
        {
            number = precisionRound(number, precision);
            int scale = number == 0 ? 0 : (int)(Math.Floor(Math.Log(Math.Abs(number), thousand)));
            switch (scale)
            {
                case -4: return "p" + unit;
                case -3: return "n" + unit;
                case -2: return "µ" + unit;
                case -1: return "m" + unit;
                case 0: return unit;
                case 1: return "k" + unit;
                case 2: return "M" + unit;
                case 3: return "G" + unit;
                case 4: return "T" + unit;
                case 5: return "P" + unit;
                default: return unit;
            }
        }
		static public string siRound(double number)
		{
			string prefix = "k";
			double divider = 1000;
			if (number > 1000) {
				divider = 1000000;
				prefix = "M";
			}
			if (number > 1000000) {
				divider = 1000000000;
				prefix = "G";
			}
			
			//Then scale it to si scale
			double divided = number/divider;

			string str;
			if (divided >= 1)
				str = divided.ToString("0");
			else
				str = divided.ToString(".0");

			return str + prefix;
		}
            
        public static IEnumerable<double> EnumerableRange(double min, double max, int steps)
        {
            return Enumerable.Range(0, steps)
                 .Select(i => min + (max - min) * ((double)i / (steps - 1)));
        }

        public static string GetTempFileName(string extension)
        {
            return System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + extension;
        }


        public static bool findSerialAddress(string defaultSerial, byte[] data, out int serialAddress)
        {
            serialAddress = -1;
            byte[] serialBytes = System.Text.Encoding.Unicode.GetBytes(defaultSerial);
            for (int i = 0; i < data.Length - serialBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < serialBytes.Length; j++)
                {
                    match &= data[j + i] == serialBytes[j];
                }
                if (match)
                {
                    serialAddress = i;
                    break;
                }
            }
            if (serialAddress < 0)
                return false;
            return true;
        }

        public static T GetNextEnum<T>(T v) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum) throw new ArgumentException("T must be an enumerated type");
            var enumValues = Enum.GetValues(typeof(T)).Cast<T>().OrderBy(e => e).ToList();
            var nextValues = enumValues.Where(e => Convert.ToInt32(e) > Convert.ToInt32(v));
            if (nextValues.Count() == 0)
                return enumValues.First();

            return nextValues.First(); ;
        }

        public static int RunProcessElevated(string filename, string arguments, bool hidden = false)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = filename;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.Verb= "runas";
                if(hidden)
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.Start();
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        public static int RunProcess(string filename, string arguments, string workPath, int timeout, out string output, out string error)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = filename;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                if (workPath != null)
                    process.StartInfo.WorkingDirectory = workPath;

                StringBuilder outputBuilder = new StringBuilder();
                StringBuilder errorBuilder = new StringBuilder();

                using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            outputWaitHandle.Set();
                        }
                        else
                        {
                            outputBuilder.AppendLine(e.Data);
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            errorWaitHandle.Set();
                        }
                        else
                        {
                            errorBuilder.AppendLine(e.Data);
                        }
                    };

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (timeout > 0)
                        process.WaitForExit(timeout);
                    else
                        process.WaitForExit();
                    outputWaitHandle.WaitOne(timeout);
                    errorWaitHandle.WaitOne(timeout);

                    output = outputBuilder.ToString();
                    error = errorBuilder.ToString();
                    if (process.HasExited)
                        return process.ExitCode;
                    else
                        return -1;
                }
            }
        }

#if WINDOWS
        public static bool TestUsbDeviceFound(int VID, int PID, out string serial)
        {
            serial = null;
            try
            {
                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    String.Format("SELECT DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_{0:X4}&PID_{1:X4}%'", VID, PID));

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    Logger.Debug("-----------------------------------");
                    Logger.Debug("Win32_PnPEntity instance");
                    Logger.Debug("-----------------------------------");
                    Logger.Debug(String.Format("DeviceID: {0}", queryObj["DeviceID"]));
                    serial = Utils.parseSerialFromDeviceID((string)queryObj["DeviceID"]);
                    Logger.Debug(String.Format("Found device with VID:PID {0:X4}:{1:X4} with serial {2}", VID, PID, serial));
                    return true;
                }
                return false;
            }
            catch (ManagementException e)
            {
                Logger.Error("An error occurred while querying for WMI data: " + e.Message);
                return false;
            }
        }
#endif

        public static bool VerifyRamp(byte[] p)
        {
            if (p.Length < 2) return false;

            List<int> failingIndices = new List<int>();

            for (int i = 1; i < p.Length; i++)
                if ((byte)(p[i - 1] + 1) != p[i])
                    failingIndices.Add(i);

            Logger.Error(failingIndices.Count + " failing indices out of " + p.Length);

            return failingIndices.Count == 0;
        }

		public static string Overwrite(this string input, int start, string replacement)
		{
			if (start >= input.Length)
				start = input.Length - 1;
			if (start < 0)
				start = 0;
			 

			int repLen = replacement.Length;
			if (start + replacement.Length > input.Length)
				repLen = input.Length - start;

			return input.Substring (0, start) + replacement.Substring (0, repLen) + input.Substring (start + repLen);
		}

		public static string YesNo(this bool b) {
			return b ? "Yes" : "No";
		}
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using AForge.Math;

namespace ECore
{
    public static class Utils
    {
        static public bool HasMethod(Object o, String methodName)
        {
            return o.GetType().GetMethod(methodName) != null;
        }

        static public String SnakeToCamel(String input)
        {
            bool new_word = true;
            string result = string.Concat(input.Select((x, i) => {
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

        static public O[] CastArray<I, O>(I[] input) {
            O[] output = new O[input.Length];
            for (int i = 0; i < input.Length; i++)
                output[i] = (O)Convert.ChangeType(input[i], typeof(O));
            return output;
        }

        /// <summary>
        /// Applies operator on each element of an array
        /// </summary>
        /// <typeparam name="I">Type of array element</typeparam>
        /// <param name="input">Array to transform</param>
        /// <param name="op">Operator lambda expression</param>
        public static O[] TransformArray<I, O>(I[] input, Func<I, O> op)
        {
            if (input == null) return null;
            O[] output = new O[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = op(input[i]);
            }
            return output;
        }
        /// <summary>
        /// Combines 2 arrays into a new one by applying lamba on each element
        /// </summary>
        /// <typeparam name="I"></typeparam>
        /// <param name="input1">Array with first argument of lambda</param>
        /// <param name="input2">Array with second argument of lambda</param>
        /// <param name="op">Lambda, i.e. to sum 2 arrays: Func&lt;T,T,T&gt; sum = (x, y) => x + y"/></param>
        public static O[] CombineArrays<I1, I2, O>(I1[] input1, I2[] input2, ref Func<I1, I2, O> op)
        {
            if (input1 == null || input2 == null) return null;
            if (input1.Length != input2.Length)
                throw new Exception("Cannot combine arrays of different length");
            O[] output = new O[input1.Length];
            for (int i = 0; i < input1.Length; i++)
            {
                output[i] = op(input1[i], input2[i]);
            }
            return output;
        }

        public static bool[][] ByteArrayToBoolArrays(byte[] input)
        {
            bool[][] output = new bool[8][];
            for(int i = 0; i < 8; i++)
                output[i] = new bool[input.Length];

            for (int i = 0; i < input.Length; i++)
            {
                for(int j = 0; j < 8; j++)
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
		#if !__IOS__
                path = Path.Combine(path, "LabNation");
		#endif
                System.IO.Directory.CreateDirectory(path);
                return path;
            }
        }
        public static string StoragePath
        {
            get
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.DoNotVerify);
		#if !__IOS__
			path = Path.Combine(path, "LabNation");
		#endif
                System.IO.Directory.CreateDirectory(path);
                return path;
            }
        }

        public static bool Schmitt(float value, bool previousValue, float thresholdHigh, float thresholdLow)
        {
            if (value >= thresholdHigh)
                return true;
            else if (value <= thresholdLow)
                return false;
            else
                return previousValue;
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
	
	    return BinaryReaderStuffer(br, numBytes, multiple, stuffing);
        }
		
	public static byte[] BinaryReaderStuffer (BinaryReader br, long numBytes, int multiple, byte stuffing)
	{
	    byte[] buff = null;
            buff = br.ReadBytes((int)numBytes);
            
            //Check if a multiple was specified or if we happen to be lucky
            if(multiple <= 0 || buff.Length % multiple == 0)
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
    }
}

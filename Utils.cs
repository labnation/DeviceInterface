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
                path += "//LabNation//";
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

    }
}

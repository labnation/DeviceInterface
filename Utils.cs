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

    }
}

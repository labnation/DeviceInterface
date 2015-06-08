using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LabNation.Common
{ 
    public class SerialNumber
    {
        public Plant plant { get; private set; }
        public Model model { get; private set; }
        public int week { get; private set; }
        public int year { get; private set; }
        public long number { get; private set; }

        public SerialNumber(string serial)
        {
            this.plant = (Plant)(Base36.Decode(serial.Substring(0, 2)));
            this.year = Convert.ToInt32(serial.Substring(2, 1), 10) + (DateTime.Now.Year / 10 * 10);
            this.week = Convert.ToInt32(serial.Substring(3, 2), 10);
            this.number= Base36.Decode(serial.Substring(5, 3));
            this.model = (Model)(Base36.Decode(serial.Substring(8, 3)));
        }

        override public string ToString()
        {
            return Generate(plant, model, number, FirstDateOfIso8601Week(year, week));
        }

        public enum Plant
        {
            Tolstraat = 0,
            Poperinge = 1,
            Oradea = 2,
        };
        
        const int MODEL_OFFSET = 36*36; //"?00";
        const int MODEL_SMARTSCOPE = 0xA;

        const int GENERATION_OFFSET = 36; //"?0";
        public enum Model
        {
            SmartScope_A00     = (MODEL_SMARTSCOPE * MODEL_OFFSET) + (0 * GENERATION_OFFSET) + 0,
            SmartScope_A10     = (MODEL_SMARTSCOPE * MODEL_OFFSET) + (1 * GENERATION_OFFSET) + 0,
            SmartScope_A11     = (MODEL_SMARTSCOPE * MODEL_OFFSET) + (1 * GENERATION_OFFSET) + 1,
            SmartScope_A12     = (MODEL_SMARTSCOPE * MODEL_OFFSET) + (1 * GENERATION_OFFSET) + 2,
            SmartScope_A14     = (MODEL_SMARTSCOPE * MODEL_OFFSET) + (1 * GENERATION_OFFSET) + 4,
            SmartScope_A15     = (MODEL_SMARTSCOPE * MODEL_OFFSET) + (1 * GENERATION_OFFSET) + 5,
        }

        public static void LogModelsAndPlants()
        {
            string format = "{0,10} {1,15:G} {1,8:D} {2,5}";
            foreach (Plant p in Enum.GetValues(typeof(Plant)))
            {
                if((int)p > 0)
                    Logger.Info(String.Format(format, "Plant", p, Base36.Encode((long)p, 2)));
            }
            foreach (Model m in Enum.GetValues(typeof(Model)))
            {
                if ((int)m > 0)
                    Logger.Info(String.Format(format, "Model", m, Base36.Encode((long)m, 3)));
            }
        }

        public static string Generate(Plant p, Model m, long number, DateTime d)
        {
            String serial = "";
            serial += Base36.Encode((long)p, 2);                            //Plant(2 digits)
            serial += d.ToString("yy").Substring(1);                        //Year (1 digit)
            serial += String.Format("{0:D2}", GetIso8601WeekOfYear(d));     //Week (2 digits)
            serial += Base36.Encode(number, 3);                             //Serial number (3 digits)
            serial += Base36.Encode((long)m, 3);                            //Model (3 digits)
            return serial.ToUpper();
        }

        public static int GetIso8601WeekOfYear(DateTime time)
        {
            // Seriously cheat.  If its Monday, Tuesday or Wednesday, then it'll 
            // be the same week# as whatever Thursday, Friday or Saturday are,
            // and we always get those right
            DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                time = time.AddDays(3);
            }

            // Return the week of our adjusted day
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        public static DateTime FirstDateOfIso8601Week(int year, int weekOfYear)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;

            DateTime firstThursday = jan1.AddDays(daysOffset);
            var cal = CultureInfo.CurrentCulture.Calendar;
            int firstWeek = cal.GetWeekOfYear(firstThursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            var weekNum = weekOfYear;
            if (firstWeek <= 1)
            {
                weekNum -= 1;
            }
            var result = firstThursday.AddDays(weekNum * 7);
            return result.AddDays(-3);
        }

        public static bool GenerateUnique(Plant p, Model m, DateTime t, List<string> serialList, ref SerialNumber serial)
        {
            string serialPattern = Generate(p, m, 0, t);
            //Replace number part in serial with ?
            serialPattern = serialPattern.Substring(0, 5) + "???" + serialPattern.Substring(8);
            Regex r = new Regex(Utils.WildcardToRegex(serialPattern));
            List<long> filteredSerials = serialList.Where(x => r.IsMatch(x)).Select(x=>Base36.Decode(x.Substring(5,3))).ToList();
            long number = 0;
            if (filteredSerials.Count > 0)
            {
                if (filteredSerials.Max() >= Base36.MaxValue(3))
                {
                    Logger.Error("Can't make more serials with this combo, maximum reached");
                    return false;
                }
                number = filteredSerials.Max() + 1;
            }
            serial = new SerialNumber(Generate(p, m, number, t));
            return true;
        }
    }

    public static class Base36
    {
        private const string CharList = "0123456789abcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// Encode the given number into a Base36 string
        /// </summary>
        /// <param name="input"></param>
		/// <param name="length"></param>
        /// <returns></returns>
        public static String Encode(long input, int length)
        {
            if (input < 0) throw new ArgumentOutOfRangeException("input", input, "input cannot be negative");

            char[] clistarr = CharList.ToCharArray();
            var result = new Stack<char>();
            while (input != 0)
            {
                result.Push(clistarr[input % 36]);
                input /= 36;
            }
            char[] padding = new char[length - result.Count];
            for(int i = 0; i < padding.Length; i++) {
                padding[i] = clistarr[0];
            }
            return new string(padding) + new string(result.ToArray());
        }

        /// <summary>
        /// Decode the Base36 Encoded string into a number
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static Int64 Decode(string input)
        {
            var reversed = input.ToLower().Reverse();
            long result = 0;
            int pos = 0;
            foreach (char c in reversed)
            {
                result += CharList.IndexOf(c) * (long)Math.Pow(36, pos);
                pos++;
            }
            return result;
        }

        public static int MaxValue(int length)
        {
            return (int)Math.Pow(36, length) - 1;
        }
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ECore
{
    static class Utils
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
        //scans a section of VHDL for constants beginning with prefix, then returns then name and the constant value
        //how: split lines up by 'prefix' and ';', then keep correct parts
        static public Dictionary<string, int> VhdlReader(string multilineInput, string prefix)
        {
            //set up
            StringReader reader = new StringReader(multilineInput);
            Dictionary<string, int> dict = new Dictionary<string, int>();
            string[] splitStrings = { ";", " ", "\t", "\n"};

            //scan line by line              
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                //try to find the prefix and the number
                string name = "";
                string number = "";
                string[] splitParts = line.Split(splitStrings, StringSplitOptions.RemoveEmptyEntries);
                bool nextSplitContainsNumber = false;
                foreach (string currentSplit in splitParts)
                {
                    if (currentSplit.IndexOf(prefix) >= 0)
                        name = currentSplit;
                    if (nextSplitContainsNumber) //if previously := was detected
                    {
                        number = currentSplit;
                        nextSplitContainsNumber = false;
                    }
                    if (currentSplit.IndexOf("=") >= 0)
                        nextSplitContainsNumber = true;       
                }

                //if prefix and number were found, add them to the output list
                if (name != "")
                    if (number != "")
                        dict.Add(name, int.Parse(number));
            }

            return dict;
        }
    }
}

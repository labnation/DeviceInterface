using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore
{
    public class MissingSettingException : Exception
    {
        public MissingSettingException(String msg) : base(msg) { }
    }

    public enum Setting
    {
        Y_OFFSET,
        TRIGGER_LEVEL,
    };

}

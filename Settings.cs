using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore
{
    public class MissingSettingException : Exception
    {
        public MissingSettingException(EDeviceImplementation d, Setting s) : 
            base(
                "The setting " + Enum.GetName(s.GetType(), s) +
                " was not found in device of type " + d.GetType().Name) 
        { }
    }
    public class SettingParameterWrongNumberException : Exception
    {
        public SettingParameterWrongNumberException(EDeviceImplementation d, Setting s, int expected, int received) :
            base(
                "The setting " + Enum.GetName(s.GetType(), s) + " in device of type " + d.GetType().Name +
                " requires " + expected + " parameters, only received " + received)
        { }
    }
    public class SettingParameterTypeMismatchException : Exception
    {
        public SettingParameterTypeMismatchException(EDeviceImplementation d, Setting s, int number, Type expected, Type received) :
            base(
                "The setting " + Enum.GetName(s.GetType(), s) + " in device of type " + d.GetType().Name +
                " requires parameter " + number + " to be of type " + expected.Name + ", got " + received.Name)
        { }
    }
    public class ValidationException : Exception { public ValidationException(String msg) : base(msg) { } }

    public enum Setting
    {
        #region vertical parameters
        Y_OFFSET,
        DIVIDER,
        MULTIPLIER,
        TRIGGER_LEVEL,
        #endregion

        #region horizontal
        DECIMATION,
        ENABLE_FREE_RUNNING,
        HOLD_OFF,
        ENABLE_DC_COUPLING,
        #endregion

        #region other
        AWG_DATA,
        #endregion

        //FIXME: guard this so it's only in internal builds
        #region develop
        DISABLE_VOLTAGE_CONVERSION,
        #endregion
    };

}

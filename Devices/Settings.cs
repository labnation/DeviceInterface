using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Common;

namespace ECore.Devices
{
    public class MissingSettingException : Exception
    {
        public MissingSettingException(EDevice d, Setting s) : 
            base(
                "The setting " + Enum.GetName(s.GetType(), s) +
                " was not found in device of type " + d.GetType().Name) 
        { }
    }
    public class SettingParameterWrongNumberException : Exception
    {
        public SettingParameterWrongNumberException(EDevice d, Setting s, int expected, int received) :
            base(
                "The setting " + Enum.GetName(s.GetType(), s) + " in device of type " + d.GetType().Name +
                " requires " + expected + " parameters, only received " + received)
        { }
    }
    public class SettingParameterTypeMismatchException : Exception
    {
        public SettingParameterTypeMismatchException(EDevice d, Setting s, int number, Type expected, Type received) :
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
        ENABLE_LOGIC_ANALYSER,
        #endregion

        //FIXME: guard this so it's only in internal builds
        #region develop
        DISABLE_VOLTAGE_CONVERSION,
        #endregion
    };

    #region EDevice settings

    partial class EDevice
    {

        static public String SettingSetterMethodName(Setting s)
        {
            String methodName = "Set" + Utils.SnakeToCamel(Enum.GetName(s.GetType(), s));
            return methodName;
        }

        public bool HasSetting(Setting s)
        {
            return this.HasSetting(s);
        }

        public void Set(Setting s, Object[] parameters) {
            if (!this.HasSetting(s))
                throw new MissingSettingException(this, s);
            MethodInfo m = this.GetType().GetMethod(SettingSetterMethodName(s));
            ParameterInfo[] pi = m.GetParameters();
            if (parameters == null || pi.Length != parameters.Length)
                throw new SettingParameterWrongNumberException(this, s,
                    pi.Length, parameters != null ? parameters.Length : 0);
            //Match parameters with method arguments
            
            for(int i = 0; i < pi.Length; i++)
            {
                if (!pi[i].ParameterType.Equals(parameters[i].GetType())) {
                    throw new SettingParameterTypeMismatchException(this, s,
                        i+1, pi[i].ParameterType, parameters[i].GetType());
                }
            }
            m.Invoke(this, parameters);
        }

    }
    #endregion 
}

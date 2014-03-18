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
    /*
    public class InvalidSettingParameterException : Exception
    {
        public InvalidSettingParameterException(String setting, String msg) :
            base(
                "While updating setting '"+ setting + "':\n" + msg)
        { }
    }
    */

    public enum Setting
    {
        #region vertical parameters
        Y_OFFSET,
        DIVIDER,
        MULTIPLIER,
        ///<summary>
        /// Trigger level(float level)
        /// <list type="bullet">
        /// <item><term>level</term><description>trigger level in Volt</description></item>
        /// </list>
        ///</summary>
        TRIGGER_LEVEL,
        #endregion

        #region horizontal
        ///<summary>
        /// Hold off(unsigned integer samples)
        /// <list type="bullet">
        /// <item><term>samples</term><description>Store [samples] before trigger</decimation></description></item>
        /// </list>
        ///</summary>
        HOLD_OFF,
        ///<summary>
        /// Enable free running(bool freerunning)
        /// <list type="bullet">
        /// <item><term>freerunning</term><description>Whether to enable free running mode</description></item>
        /// </list>
        ///</summary>
        ENABLE_FREE_RUNNING,
        ///<summary>
        /// Decimation(unsigned integer decimation)
        /// <list type="bullet">
        /// <item><term>decimation</term><description>Store every [decimation]nt sample</decimation></description></item>
        /// </list>
        ///</summary>
        DECIMATION,
        #endregion

        #region other
        ///<summary>
        /// AWG Data(unsigned byte[] data)
        /// <list type="bullet">
        /// <item><term>data</term><description>byte array to load into AWG</description></item>
        /// </list>
        ///</summary>
        AWG_DATA,
        #endregion
    };

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore
{
    public class EDataPackage
    {
        private float[] voltageValues;

        public EDataPackage(float[] voltageValues)
        {
            this.voltageValues = voltageValues;
        }

        public Type DataType
        {
            get
            {
                float f = 0;                
                return f.GetType();
            }
        }

        public float[] Data
        {
            get
            {
                return voltageValues;
            }
        }
    }
    /*
    public abstract class EDataPackage<T> : EDataPackage
    {
        public override Type DataType
        {
            get { return T; }
        }
    }*/
}

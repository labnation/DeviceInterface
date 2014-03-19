using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore
{
    public delegate void NewDataAvailableHandler(EDataNode sender, EventArgs e);

    public abstract class EDataNode
    {
        public EDataNode() { }

        abstract public EDataPackage LatestDataPackage { get; }
        abstract public void Update(EDataNode sender, EventArgs e);
    }
}

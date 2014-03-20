using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore.DeviceImplementations
{
    public class ScopeDummy : Scope
    {
        public ScopeDummy(EDevice d) : base(d) { }

        public override void InitializeDataSources()
        {
            dataSources.Add(new DataSources.DataSourceFile());
        }
        public override void InitializeHardwareInterface()
        {
            //Dummy has no hardware interface. So sad, living in a computer's memory
        }
        public override void InitializeMemories()
        {
            //Dummy has not memory. Yep, it's *that* dumb.
        }
        public override void Start()
        {
            throw new NotImplementedException();
        }
        public override void Stop()
        {
            throw new NotImplementedException();
        }
        public override DataPackageScope GetScopeData()
        {
            throw new NotImplementedException();
        }

    }
}

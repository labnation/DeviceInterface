using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore
{
    //abstract class for all device-specific implementations
    //all new HW devices should inherit from this class
    //groups all information in memories and functionalities
    abstract public class EDeviceImplementation
    {
        //////////////////////////////////////////////////////////////////
        //private properties        
        private Dictionary<Type, List<object>> sortedInterfaceDictionary;

        //////////////////////////////////////////////////////////////////
        //shared properties        
        protected EDevice eDevice;
        protected List<EDeviceMemory> memories;
        protected List<object> functionalities;

        //////////////////////////////////////////////////////////////////
        //contract for inheriters
        abstract public List<EDeviceMemory> CreateMemories();
        abstract public List<object> CreateFunctionalities();
        abstract public EDeviceHWInterface CreateHWInterface();
        abstract public DeviceImplementations.Scop3v2.Scop3v2RomManager CreateRomManager();
        abstract public float[] GetDataAndConvertToVoltageValues();
        abstract public void StartDevice();
        abstract public void StopDevice();
		abstract public void FlashHW ();

        //////////////////////////////////////////////////////////////////
        //base functionality implementation
        protected EDeviceImplementation(EDevice eDevice)
        {
            this.eDevice = eDevice;

            //automatically calls the methods which need to be executed during initialization
            this.memories = CreateMemories();
            this.functionalities = CreateFunctionalities();
        }

        //getters
        virtual public List<EDeviceMemory> Memories { get { return memories; } }
        virtual public List<object> Functionalities { get { return functionalities; } }

        //searches if this implementation contains an instance of a sub-class which implements a certain interface T. 
        //if so, returns this instance, so it can be used immediately by the calling code
        /*virtual public List<T> GetInterface<T>()
        {
            foreach (object item in functionalities)
            {
                T castedObject = item as T;
                if (castedObject != null)
                    return castedObject;
            }
            return default(T);
        }*/

        public virtual List<object> GetInterfaces(Type interfaceType)
        {
            //if this method is called the first time: init list
            if (this.sortedInterfaceDictionary == null)
                InitSortedInterfaceDictionary();

            //if the interface is implemented, return it. otherwise, return null
            if (sortedInterfaceDictionary.ContainsKey(interfaceType))
                return sortedInterfaceDictionary[interfaceType];
            else
                return null;
        }

        private void InitSortedInterfaceDictionary()
        {
            this.sortedInterfaceDictionary = new Dictionary<Type, List<object>>();

            //scroll over all implemented interfaces
            foreach (object functionality in this.functionalities)
            {
                Type currentType = functionality.GetType();
                Type[] iList = currentType.FindInterfaces(new System.Reflection.TypeFilter(MyInterfaceFilter), null);
                Type currentInterface = iList[0];

                //if dictionary does not contains a list for this type of funcs: create and add
                if (!sortedInterfaceDictionary.ContainsKey(currentType)) 
                    sortedInterfaceDictionary.Add(currentInterface, new List<object>());

                //now add this functionality to the correct list
                sortedInterfaceDictionary[currentInterface].Add(functionality);//if dictionary does not contain a list for this type of funcs: create
            }
        }

        private bool MyInterfaceFilter(Type typeObj, Object criteriaObj)
        {
            return true;
        }
    }
}

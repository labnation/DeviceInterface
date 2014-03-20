using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using ECore.DataPackages;

namespace ECore.EDataNodes
{
    public class EDataNodeFromEmbeddedResource: EDataNode
    {
        private int sleepTime = 10;
        private DataPackageWaveAnalog lastDataPackage;
        DateTime lastUpdate;
        StreamReader reader = null;
        List<float[]> dataList = new List<float[]>();
        int index = 0;

		public EDataNodeFromEmbeddedResource()
        {
            Stream inStream;            

            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assyIndex = 0; assyIndex < assemblies.Length; assyIndex++)
            {
                //FIXME: dirty patch! otherwise this loop will crash, as there are some assemblies at the end of the list that don't support the following operations and crash
                if (reader == null) 
                {
                    System.Reflection.Assembly assy = assemblies[assyIndex];
                    string[] assetList = assy.GetManifestResourceNames();
                    for (int a = 0; a < assetList.Length; a++)
                    {
                        //Logger.AddEntry (this, LogMessageType.Persistent, "ER: " + assetList[a]);
                        if (assetList[a].Contains("stringOutput.txt"))
                        {
                            inStream = assy.GetManifestResourceStream(assetList[a]);
                            reader = new StreamReader(inStream);
                            //Logger.AddEntry (this, LogMessageType.Persistent, "Connected to FW Flash file");
                        }
                    }
                }
            }

            lastUpdate = DateTime.Now;

            while (!reader.EndOfStream)
            {
				try{
                //read data and convert
                string line = reader.ReadLine();
                string[] splitLine = line.Split(new char[] { ';' });

                if (splitLine.Length == 4097)
                {
                    float[] voltageValues = new float[splitLine.Length - 1];
                    for (int i = 0; i < splitLine.Length - 1; i++)
                    {
                        float tempFloat = float.Parse(splitLine[i]) / 255f * 10f - 5f;
                        voltageValues[i] = tempFloat;
                    }

                    dataList.Add(voltageValues);
                }
				}catch{
				}
            }
        }
        
        private void OpenArray()
        {
        }

        public override void Update()
        {

            //since this is a source node, it should fire its event at a certain interval.
            //in order to emulate this, thread will be suspended.
            DateTime now = DateTime.Now;
            int slackTime = sleepTime - (int)now.Subtract(lastUpdate).TotalMilliseconds;


            if (slackTime > 0)
                System.Threading.Thread.Sleep(slackTime);
            lastUpdate = DateTime.Now;

			if (dataList.Count == 0) {
				float [] dummy = new float[4096];
				lastDataPackage = new DataPackageWaveAnalog(dummy, 0);  
			}

            if (index++ >= dataList.Count-1)
                index = 0;                      
            

            //convert data into an EDataPackage if valid
            lastDataPackage = new DataPackageWaveAnalog(dataList[index], 0);
        }
    }
}

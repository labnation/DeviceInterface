using System;
using System.Reflection;
using System.IO;

namespace LabNation.DeviceInterface
{
	public static class Resources
	{
		internal static byte[] Load(string name) 
		{
			Assembly ass = Assembly.GetExecutingAssembly();

			using(Stream s = ass.GetManifestResourceStream(String.Format("LabNation.DeviceInterface.{0}", name)))
			using(BinaryReader r = new BinaryReader(s))
				return r.ReadBytes((int)s.Length);

		}
	}
}


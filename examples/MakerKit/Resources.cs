using System;
using System.Reflection;
using System.IO;

namespace MakerKit
{
	public static class Resources
	{
		internal static byte[] Load(string name) 
		{
			Assembly ass = Assembly.GetExecutingAssembly();

			using(Stream s = ass.GetManifestResourceStream(String.Format("{0}.Resources.{1}", ass.GetName().Name, name)))
			using(BinaryReader r = new BinaryReader(s))
				return r.ReadBytes((int)s.Length);

		}
	}
}
using System.Reflection;
using System.Runtime.CompilerServices;
#if ANDROID
using Android.App;
#else
using System.Runtime.InteropServices;
#endif

[assembly: AssemblyTitle("MakerKit")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("LabNation")]
[assembly: AssemblyProduct("SmartScope MakerKit")]
[assembly: AssemblyCopyright("Copyright ©  2016")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
#if ANDROID
[assembly: Application(Icon = "@drawable/icon")]
#else
[assembly: ComVisible(false)]
[assembly: Guid("cb6f0484-50fc-405a-baec-cf5d1c5f7d5c")]
[assembly: AssemblyFileVersion("1.0.0.0")]
#endif
[assembly: AssemblyVersion("1.0.0.0")]
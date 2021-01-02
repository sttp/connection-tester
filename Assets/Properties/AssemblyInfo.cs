using System.Reflection;
using System.Runtime.InteropServices;
using ConnectionTester;

// Informational attributes.

[assembly: AssemblyCompany("Grid Protection Alliance")]
[assembly: AssemblyCopyright("Copyright © 2018-2021.  All Rights Reserved")]
[assembly: AssemblyProduct("STTP")]

// Assembly manifest attributes.
#if DEBUG
[assembly: AssemblyConfiguration("Debug Build")]
#else
[assembly: AssemblyConfiguration("Release Build")]
#endif

[assembly: AssemblyDescription(GraphLines.DefaultTitle + " Application")]
[assembly: AssemblyTitle(GraphLines.DefaultTitle)]
[assembly: AssemblyMetadata("TargetName", "STTPConnectionTester")]

// Other configuration attributes.

[assembly: ComVisible(false)]
[assembly: Guid("07977870-4a6c-42ca-ad97-84706b0e340b")]

// Assembly identity attributes.

[assembly: AssemblyVersion("1.0.5.0")]
[assembly: AssemblyFileVersion("1.0.5.0")]
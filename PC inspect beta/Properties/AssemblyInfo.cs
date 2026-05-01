using System.Reflection;
using System.Runtime.InteropServices;

// These are the only attributes safe to keep here.
// Title, Version, Company, Product, Configuration, FileVersion
// are already emitted by the .NET SDK via the .csproj properties,
// so they must NOT be repeated here — doing so causes CS0579 duplicate errors.

[assembly: ComVisible(false)]
[assembly: Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
[assembly: AssemblyCulture("")]

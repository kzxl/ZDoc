using System;
using System.IO;
using System.Reflection;

namespace ZDoc.Tests
{
    /// <summary>
    /// Locates the compiled ZDoc.SampleLib assembly and its XML doc file, which are
    /// copied next to the test assembly at build time via the project reference.
    /// </summary>
    internal static class SampleAssembly
    {
        public static string DllPath
        {
            get
            {
                string dir = Path.GetDirectoryName(typeof(SampleAssembly).Assembly.Location)!;
                string path = Path.Combine(dir, "ZDoc.SampleLib.dll");
                if (!File.Exists(path))
                    throw new FileNotFoundException("Sample assembly not found. Build the solution first.", path);
                return path;
            }
        }

        public static string XmlPath => Path.ChangeExtension(DllPath, ".xml");

        /// <summary>The sample assembly loaded for in-process reflection (DocId tests).</summary>
        public static Assembly Loaded => typeof(ZDoc.SampleLib.Money).Assembly;
    }
}

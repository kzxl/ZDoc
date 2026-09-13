using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;
using ZeroDoc.Core.Reflection;

namespace ZeroDoc.Tests;

public class NupkgInspectorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _testNupkgPath;

    public NupkgInspectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZeroDoc_NupkgTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _testNupkgPath = Path.Combine(_tempDir, "SamplePackage.1.2.3.nupkg");

        CreateSyntheticNupkg(_testNupkgPath);
    }

    private void CreateSyntheticNupkg(string destinationPath)
    {
        using var zip = ZipFile.Open(destinationPath, ZipArchiveMode.Create);

        // 1. Nuspec entry
        var nuspecEntry = zip.CreateEntry("SamplePackage.nuspec");
        using (var writer = new StreamWriter(nuspecEntry.Open(), Encoding.UTF8))
        {
            writer.Write("""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
              <metadata>
                <id>SamplePackage</id>
                <version>1.2.3</version>
                <authors>ZeroUniverse Devs</authors>
                <description>High performance ZeroUniverse demonstration package.</description>
                <dependencies>
                  <group targetFramework="net8.0">
                    <dependency id="System.Text.Json" version="8.0.0" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """);
        }

        // 2. Lib entries for net8.0
        var net8Dll = zip.CreateEntry("lib/net8.0/SamplePackage.dll");
        using (var writer = new StreamWriter(net8Dll.Open(), Encoding.UTF8))
        {
            writer.Write("SYNTHETIC_DLL_CONTENT_NET8");
        }

        var net8Xml = zip.CreateEntry("lib/net8.0/SamplePackage.xml");
        using (var writer = new StreamWriter(net8Xml.Open(), Encoding.UTF8))
        {
            writer.Write("<doc><assembly><name>SamplePackage</name></assembly></doc>");
        }

        // 3. Lib entries for netstandard2.0
        var stdDll = zip.CreateEntry("lib/netstandard2.0/SamplePackage.dll");
        using (var writer = new StreamWriter(stdDll.Open(), Encoding.UTF8))
        {
            writer.Write("SYNTHETIC_DLL_CONTENT_NETSTD");
        }
    }

    [Fact]
    public void Inspect_ParsesMetadataAndFrameworksCorrectly()
    {
        var meta = NupkgInspector.Inspect(_testNupkgPath);

        Assert.Equal("SamplePackage", meta.PackageId);
        Assert.Equal("1.2.3", meta.Version);
        Assert.Equal("ZeroUniverse Devs", meta.Authors);
        Assert.Contains("ZeroUniverse demonstration", meta.Description);
        Assert.Contains("net8.0", meta.AvailableFrameworks);
        Assert.Contains("netstandard2.0", meta.AvailableFrameworks);
        Assert.Single(meta.Dependencies);
        Assert.Contains("System.Text.Json", meta.Dependencies[0]);
    }

    [Fact]
    public void Extract_PullsPreferredFrameworkFiles()
    {
        var outDir = Path.Combine(_tempDir, "Extracted");
        var res = NupkgInspector.Extract(_testNupkgPath, outDir, "net8.0");

        Assert.Equal("net8.0", res.Metadata.SelectedFramework);
        Assert.True(File.Exists(res.ExtractedAssemblyPath));
        Assert.NotNull(res.ExtractedXmlPath);
        Assert.True(File.Exists(res.ExtractedXmlPath));
        Assert.Equal("SYNTHETIC_DLL_CONTENT_NET8", File.ReadAllText(res.ExtractedAssemblyPath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore test directory cleanup failure
        }
    }
}

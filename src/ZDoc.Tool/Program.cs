using System;
using System.IO;
using ZDoc.Core.Reflection;
using ZDoc.Core.Rendering;

namespace ZDoc.Tool;

/// <summary>
/// Command-line entry point for ZDoc. Generates a self-contained HTML API reference
/// from a .NET assembly and its XML documentation file.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            if (string.IsNullOrEmpty(options.AssemblyPath))
            {
                Console.Error.WriteLine("error: missing required <assembly> argument.\n");
                PrintHelp();
                return 2;
            }

            if (!File.Exists(options.AssemblyPath))
            {
                Console.Error.WriteLine($"error: assembly not found: {options.AssemblyPath}");
                return 2;
            }

            bool isMarkdown = options.Format == "markdown" || options.Format == "md";
            string defaultExt = isMarkdown ? ".md" : ".html";
            string output = options.OutputPath
                ?? Path.ChangeExtension(Path.GetFileName(options.AssemblyPath), defaultExt);

            string? description = null;
            if (!string.IsNullOrEmpty(options.ReadmePath))
            {
                if (!File.Exists(options.ReadmePath))
                {
                    Console.Error.WriteLine($"warning: readme not found, ignoring: {options.ReadmePath}");
                }
                else
                {
                    description = File.ReadAllText(options.ReadmePath);
                }
            }

            string assemblyToExtract = options.AssemblyPath;
            string? xmlToExtract = options.XmlPath;
            string? tempExtractDir = null;
            string? docTitle = options.Title;

            if (options.AssemblyPath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
            {
                tempExtractDir = Path.Combine(Path.GetTempPath(), "ZDoc_nupkg_" + Guid.NewGuid().ToString("N"));
                Console.WriteLine($"Inspecting NuGet package: {Path.GetFileName(options.AssemblyPath)} ...");
                var nupkgRes = NupkgInspector.Extract(options.AssemblyPath, tempExtractDir);

                assemblyToExtract = nupkgRes.ExtractedAssemblyPath;
                xmlToExtract = nupkgRes.ExtractedXmlPath;

                if (string.IsNullOrEmpty(docTitle))
                {
                    docTitle = $"{nupkgRes.Metadata.PackageId} v{nupkgRes.Metadata.Version} ({nupkgRes.Metadata.SelectedFramework})";
                }
                if (string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(nupkgRes.Metadata.Description))
                {
                    description = nupkgRes.Metadata.Description;
                }

                Console.WriteLine($"  Package ID: {nupkgRes.Metadata.PackageId} ({nupkgRes.Metadata.Version})");
                Console.WriteLine($"  Framework:  {nupkgRes.Metadata.SelectedFramework}");
                if (nupkgRes.Metadata.Dependencies.Count > 0)
                {
                    Console.WriteLine($"  Dependencies: {string.Join(", ", nupkgRes.Metadata.Dependencies)}");
                }
            }

            try
            {
                var extractor = new ApiExtractor(new ExtractionOptions
                {
                    IncludeProtected = !options.PublicOnly,
                    Title = docTitle,
                    Description = description,
                });

                Console.WriteLine($"Reading {Path.GetFileName(assemblyToExtract)} ...");
                var model = extractor.Extract(assemblyToExtract, xmlToExtract);

                int typeCount = 0;
                foreach (var ns in model.Namespaces) typeCount += ns.Types.Count;
                Console.WriteLine($"  {typeCount} type(s) in {model.Namespaces.Count} namespace(s).");

                if (isMarkdown)
                {
                    var renderer = new ApiMarkdownRenderer();
                    renderer.RenderToFile(model, output);
                }
                else
                {
                    var renderer = new HtmlRenderer();
                    renderer.RenderToFile(model, output);
                }

                var info = new FileInfo(output);
                Console.WriteLine($"Wrote {info.FullName} ({info.Length / 1024.0:0.0} KB).");
                return 0;
            }
            finally
            {
                if (tempExtractDir != null && Directory.Exists(tempExtractDir))
                {
                    try { Directory.Delete(tempExtractDir, recursive: true); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
@"ZDoc — generate a self-contained HTML or Markdown API reference for a .NET assembly.

USAGE:
  ZDoc <assembly|nupkg> [options]

ARGUMENTS:
  <assembly|nupkg>     Path to the .dll or .nupkg to document.

OPTIONS:
  -o, --output <file>  Output documentation path (default: <assembly>.html or .md).
  -f, --format <fmt>   Output format: html (default) or markdown / md.
  -x, --xml <file>     XML doc file (default: <assembly>.xml next to the dll).
  -t, --title <text>   Page title (default: ""<AssemblyName> API"").
  -r, --readme <file>  Markdown/text file shown on the overview page.
      --public-only    Exclude protected members.
  -h, --help           Show this help.

EXAMPLES:
  ZDoc bin/Release/netstandard2.0/MyLib.dll
  ZDoc MyLib.dll -f markdown -o docs/API.md
  ZDoc packages/MyPackage.1.0.0.nupkg -o docs/index.html");
    }
}

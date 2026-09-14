using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace ZeroDoc.Core.Reflection;

/// <summary>
/// Metadata parsed directly from a NuGet package (.nupkg).
/// </summary>
public class NupkgMetadata
{
    /// <summary>NuGet package identifier.</summary>
    public string PackageId { get; set; } = string.Empty;
    /// <summary>Package semantic version string.</summary>
    public string Version { get; set; } = string.Empty;
    /// <summary>Package authors.</summary>
    public string Authors { get; set; } = string.Empty;
    /// <summary>Package description.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Project URL if specified in nuspec.</summary>
    public string ProjectUrl { get; set; } = string.Empty;
    /// <summary>List of target frameworks available in the package lib directory.</summary>
    public List<string> AvailableFrameworks { get; set; } = new List<string>();
    /// <summary>Framework selected for API extraction.</summary>
    public string SelectedFramework { get; set; } = string.Empty;
    /// <summary>Target assembly file name.</summary>
    public string AssemblyFileName { get; set; } = string.Empty;
    /// <summary>Package dependencies with target frameworks and versions.</summary>
    public List<string> Dependencies { get; set; } = new List<string>();
}

/// <summary>
/// Result of extracting a NuGet package for documentation generation.
/// </summary>
public class NupkgExtractionResult
{
    /// <summary>Package metadata.</summary>
    public NupkgMetadata Metadata { get; set; } = new NupkgMetadata();
    /// <summary>Absolute path to the extracted assembly DLL.</summary>
    public string ExtractedAssemblyPath { get; set; } = string.Empty;
    /// <summary>Absolute path to the extracted XML documentation file, if present.</summary>
    public string? ExtractedXmlPath { get; set; }
    /// <summary>Target directory containing extracted package contents.</summary>
    public string ExtractionDirectory { get; set; } = string.Empty;
}

/// <summary>
/// Direct inspector and extractor for NuGet package (.nupkg) archives.
/// </summary>
public static class NupkgInspector
{
    /// <summary>
    /// Inspects a .nupkg file without extracting the entire archive.
    /// </summary>
    public static NupkgMetadata Inspect(string nupkgPath)
    {
        if (!File.Exists(nupkgPath))
        {
            throw new FileNotFoundException($"NuGet package not found: {nupkgPath}", nupkgPath);
        }

        var metadata = new NupkgMetadata();

        using (var archive = ZipFile.OpenRead(nupkgPath))
        {
            // 1. Locate and parse .nuspec
            var nuspecEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            if (nuspecEntry != null)
            {
                using var stream = nuspecEntry.Open();
                var doc = XDocument.Load(stream);
                XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
                var metaElem = doc.Root?.Element(ns + "metadata") ?? doc.Root?.Element("metadata");

                if (metaElem != null)
                {
                    metadata.PackageId = (string?)metaElem.Element(ns + "id") ?? (string?)metaElem.Element("id") ?? "";
                    metadata.Version = (string?)metaElem.Element(ns + "version") ?? (string?)metaElem.Element("version") ?? "";
                    metadata.Authors = (string?)metaElem.Element(ns + "authors") ?? (string?)metaElem.Element("authors") ?? "";
                    metadata.Description = (string?)metaElem.Element(ns + "description") ?? (string?)metaElem.Element("description") ?? "";
                    metadata.ProjectUrl = (string?)metaElem.Element(ns + "projectUrl") ?? (string?)metaElem.Element("projectUrl") ?? "";

                    var depElems = metaElem.Descendants(ns + "dependency").Concat(metaElem.Descendants("dependency"));
                    foreach (var dep in depElems)
                    {
                        var id = (string?)dep.Attribute("id");
                        var ver = (string?)dep.Attribute("version");
                        if (!string.IsNullOrEmpty(id))
                        {
                            metadata.Dependencies.Add($"{id} ({ver})");
                        }
                    }
                }
            }

            // 2. Discover available target frameworks in lib/
            var libEntries = archive.Entries.Where(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)).ToList();
            var tfms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in libEntries)
            {
                var parts = entry.FullName.Split('/');
                if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    tfms.Add(parts[1]);
                }
            }

            metadata.AvailableFrameworks = tfms.OrderByDescending(t => t).ToList();
        }

        return metadata;
    }

    /// <summary>
    /// Extracts the target assembly and XML documentation file from the .nupkg into a directory.
    /// </summary>
    public static NupkgExtractionResult Extract(string nupkgPath, string targetDirectory, string? preferredTfm = null)
    {
        var meta = Inspect(nupkgPath);
        Directory.CreateDirectory(targetDirectory);

        using var archive = ZipFile.OpenRead(nupkgPath);

        // Pick best framework
        string selectedTfm = preferredTfm ?? meta.AvailableFrameworks.FirstOrDefault() ?? "";
        if (!meta.AvailableFrameworks.Contains(selectedTfm, StringComparer.OrdinalIgnoreCase) && meta.AvailableFrameworks.Count > 0)
        {
            selectedTfm = meta.AvailableFrameworks[0];
        }
        meta.SelectedFramework = selectedTfm;

        string prefix = string.IsNullOrEmpty(selectedTfm) ? "lib/" : $"lib/{selectedTfm}/";
        var tfmEntries = archive.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

        // If no lib/ matches, fallback to any .dll in the archive
        if (tfmEntries.Count == 0)
        {
            tfmEntries = archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        string assemblyPath = string.Empty;
        string? xmlPath = null;

        foreach (var entry in tfmEntries)
        {
            var fileName = Path.GetFileName(entry.FullName);
            if (string.IsNullOrEmpty(fileName)) continue;

            var destPath = Path.Combine(targetDirectory, fileName);
            entry.ExtractToFile(destPath, overwrite: true);

            if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(assemblyPath))
                {
                    assemblyPath = destPath;
                    meta.AssemblyFileName = fileName;
                }
            }
            else if (fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                xmlPath = destPath;
            }
        }

        return new NupkgExtractionResult
        {
            Metadata = meta,
            ExtractedAssemblyPath = assemblyPath,
            ExtractedXmlPath = xmlPath,
            ExtractionDirectory = targetDirectory
        };
    }

    /// <summary>
    /// Loads assembly and XML documentation from .nupkg directly into memory streams without disk extraction.
    /// </summary>
    public static NupkgInMemoryStreams ExtractToMemory(string nupkgPath, string? preferredTfm = null)
    {
        var meta = Inspect(nupkgPath);
        using var archive = ZipFile.OpenRead(nupkgPath);

        string selectedTfm = preferredTfm ?? meta.AvailableFrameworks.FirstOrDefault() ?? "";
        if (!meta.AvailableFrameworks.Contains(selectedTfm, StringComparer.OrdinalIgnoreCase) && meta.AvailableFrameworks.Count > 0)
        {
            selectedTfm = meta.AvailableFrameworks[0];
        }
        meta.SelectedFramework = selectedTfm;

        string prefix = string.IsNullOrEmpty(selectedTfm) ? "lib/" : $"lib/{selectedTfm}/";
        var tfmEntries = archive.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

        if (tfmEntries.Count == 0)
        {
            tfmEntries = archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        MemoryStream? assemblyStream = null;
        MemoryStream? xmlStream = null;

        foreach (var entry in tfmEntries)
        {
            var fileName = Path.GetFileName(entry.FullName);
            if (string.IsNullOrEmpty(fileName)) continue;

            if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                if (assemblyStream == null)
                {
                    assemblyStream = new MemoryStream();
                    using var s = entry.Open();
                    s.CopyTo(assemblyStream);
                    assemblyStream.Position = 0;
                    meta.AssemblyFileName = fileName;
                }
            }
            else if (fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                xmlStream = new MemoryStream();
                using var s = entry.Open();
                s.CopyTo(xmlStream);
                xmlStream.Position = 0;
            }
        }

        if (assemblyStream == null)
        {
            throw new InvalidOperationException($"No target assembly DLL found in package {nupkgPath} for framework {selectedTfm}");
        }

        return new NupkgInMemoryStreams
        {
            Metadata = meta,
            AssemblyStream = assemblyStream,
            XmlDocStream = xmlStream
        };
    }
}

/// <summary>
/// In-memory stream representation of assembly and documentation extracted from a .nupkg archive.
/// </summary>
public class NupkgInMemoryStreams : IDisposable
{
    /// <summary>Package metadata.</summary>
    public NupkgMetadata Metadata { get; set; } = new NupkgMetadata();
    /// <summary>In-memory stream of the extracted assembly.</summary>
    public Stream AssemblyStream { get; set; } = Stream.Null;
    /// <summary>In-memory stream of the XML documentation file, if present.</summary>
    public Stream? XmlDocStream { get; set; }

    /// <summary>Disposes underlying memory streams.</summary>
    public void Dispose()
    {
        AssemblyStream?.Dispose();
        XmlDocStream?.Dispose();
    }
}

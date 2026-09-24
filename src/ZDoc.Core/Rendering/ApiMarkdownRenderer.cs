using System;
using System.IO;
using System.Linq;
using System.Text;
using ZDoc.Core.Model;

namespace ZDoc.Core.Rendering
{
    /// <summary>
    /// Renders an <see cref="ApiDocument"/> into clean, GitHub-flavored Markdown (GFM).
    /// Ideal for GitHub Wikis, repository documentation, and static site generators.
    /// </summary>
    public sealed class ApiMarkdownRenderer
    {
        /// <summary>
        /// Renders the document to a markdown string.
        /// </summary>
        public string Render(ApiDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var sb = new StringBuilder();

            // Title & Meta Header
            sb.AppendLine($"# {document.Title}");
            sb.AppendLine();
            sb.AppendLine($"> **Assembly:** `{document.AssemblyName}` | **Version:** `{document.AssemblyVersion}`");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(document.Description))
            {
                sb.AppendLine("## Overview");
                sb.AppendLine();
                sb.AppendLine(document.Description!.Trim());
                sb.AppendLine();
            }

            // Table of Contents
            sb.AppendLine("## Table of Contents");
            sb.AppendLine();
            foreach (var ns in document.Namespaces)
            {
                sb.AppendLine($"- **Namespace** `{ns.Name}`");
                foreach (var type in ns.Types)
                {
                    sb.AppendLine($"  - [{type.Kind} {type.Name}](#{type.Slug})");
                }
            }
            sb.AppendLine();

            // Detailed API by Namespace
            foreach (var ns in document.Namespaces)
            {
                sb.AppendLine("---");
                sb.AppendLine($"## Namespace: `{ns.Name}`");
                sb.AppendLine();

                foreach (var type in ns.Types)
                {
                    RenderType(sb, type);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Renders the document and writes it to the specified file path.
        /// </summary>
        public void RenderToFile(ApiDocument document, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(filePath, Render(document), Encoding.UTF8);
        }

        private static void RenderType(StringBuilder sb, ApiType type)
        {
            sb.AppendLine($"<a id=\"{type.Slug}\"></a>");
            sb.AppendLine($"### {type.Kind} `{type.Name}`");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(type.Docs.Summary))
            {
                sb.AppendLine(type.Docs.Summary!.Trim());
                sb.AppendLine();
            }

            // Declaration Code Block
            sb.AppendLine("```csharp");
            sb.AppendLine(type.Declaration);
            sb.AppendLine("```");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(type.Docs.Remarks))
            {
                sb.AppendLine($"**Remarks:** {type.Docs.Remarks!.Trim()}");
                sb.AppendLine();
            }

            // Members grouped by kind
            var membersByKind = type.Members.GroupBy(m => m.Kind).OrderBy(g => g.Key);
            foreach (var group in membersByKind)
            {
                sb.AppendLine($"#### {group.Key}s");
                sb.AppendLine();

                foreach (var member in group)
                {
                    sb.AppendLine($"##### `{member.Name}`");
                    sb.AppendLine();
                    sb.AppendLine("```csharp");
                    sb.AppendLine(member.Signature);
                    sb.AppendLine("```");
                    sb.AppendLine();

                    if (!string.IsNullOrWhiteSpace(member.Docs.Summary))
                    {
                        sb.AppendLine(member.Docs.Summary!.Trim());
                        sb.AppendLine();
                    }

                    if (member.Parameters.Count > 0)
                    {
                        sb.AppendLine("| Parameter | Type | Description |");
                        sb.AppendLine("| :--- | :--- | :--- |");
                        foreach (var param in member.Parameters)
                        {
                            var desc = string.IsNullOrWhiteSpace(param.Description) ? "-" : param.Description!.Trim().Replace("|", "\\|");
                            sb.AppendLine($"| `{param.Name}` | `{param.TypeName}` | {desc} |");
                        }
                        sb.AppendLine();
                    }

                    if (!string.IsNullOrWhiteSpace(member.Docs.Returns))
                    {
                        sb.AppendLine($"**Returns:** {member.Docs.Returns!.Trim()}");
                        sb.AppendLine();
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ZDoc.Core.Model;

namespace ZDoc.Core.Rendering
{
    /// <summary>
    /// Renders an <see cref="ApiDocument"/> into a single, self-contained HTML file with the
    /// CSS and JavaScript inlined. The output has no external dependencies and can be opened
    /// directly from disk or served as a static file.
    /// </summary>
    public sealed class HtmlRenderer
    {
        private readonly string _shell;
        private readonly string _css;
        private readonly string _js;

        /// <summary>Maps a simple type name (without generic args) to its view slug, for cross-links.</summary>
        private Dictionary<string, string> _typeLinks = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Creates a renderer, loading the embedded front-end template assets.</summary>
        public HtmlRenderer()
        {
            _shell = ReadAsset("shell.html");
            _css = ReadAsset("ZDoc.css");
            _js = ReadAsset("ZDoc.js");
        }

        /// <summary>Renders the document to an HTML string.</summary>
        public string Render(ApiDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            _typeLinks = BuildTypeLinkMap(document);

            string nav = BuildNav(document);
            string views = BuildViews(document);

            return _shell
                .Replace("{{TITLE}}", Escape(document.Title))
                .Replace("{{ASSEMBLY}}", Escape(document.AssemblyName))
                .Replace("{{VERSION}}", Escape(document.AssemblyVersion))
                .Replace("{{CSS}}", _css)
                .Replace("{{JS}}", _js)
                .Replace("{{NAV}}", nav)
                .Replace("{{VIEWS}}", views);
        }

        private static Dictionary<string, string> BuildTypeLinkMap(ApiDocument document)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var ns in document.Namespaces)
            {
                foreach (var type in ns.Types)
                {
                    // Key by the simple name without generic arguments, e.g. "AsyncDebouncer".
                    string key = StripGenerics(type.Name);
                    if (!map.ContainsKey(key)) map[key] = type.Slug;
                }
            }
            return map;
        }

        private static string StripGenerics(string name)
        {
            int lt = name.IndexOf('<');
            return lt >= 0 ? name.Substring(0, lt) : name;
        }

        /// <summary>Renders the document and writes it to <paramref name="outputPath"/>.</summary>
        public void RenderToFile(ApiDocument document, string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("Output path is required.", nameof(outputPath));

            string html = Render(document);
            string? dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            File.WriteAllText(outputPath, html, new UTF8Encoding(false));
        }

        // ---- Navigation sidebar ----

        private static string BuildNav(ApiDocument document)
        {
            var sb = new StringBuilder();
            sb.Append("<a class=\"nav-type\" data-target=\"view-landing\" href=\"#\">")
              .Append("<span class=\"badge\" style=\"background:#64748b\">i</span> Overview</a>");

            foreach (var ns in document.Namespaces)
            {
                sb.Append("<div class=\"nav-ns\">");
                sb.Append("<div class=\"nav-ns-name\">").Append(Escape(ns.Name)).Append("</div>");
                foreach (var type in ns.Types)
                {
                    // Index the type name, its summary, and all member names so a search for a
                    // member (e.g. "InvokeAsync") surfaces the type that declares it.
                    var memberNames = string.Join(" ", type.Members.Select(m => StripGenerics(m.Name)).Distinct());
                    string search = Escape((type.Name + " " + (type.Docs.Summary ?? "") + " " + memberNames).Trim());
                    sb.Append("<a class=\"nav-type\" data-target=\"view-").Append(type.Slug).Append("\" ")
                      .Append("data-search=\"").Append(search).Append("\" ")
                      .Append("href=\"#").Append(type.Slug).Append("\">")
                      .Append(KindBadge(type.Kind))
                      .Append("<span>").Append(Escape(type.Name)).Append("</span></a>");
                }
                sb.Append("</div>");
            }
            return sb.ToString();
        }

        // ---- Content views ----

        private string BuildViews(ApiDocument document)
        {
            var sb = new StringBuilder();

            // Landing / overview.
            sb.Append("<section id=\"view-landing\" class=\"content landing\">");
            sb.Append("<h2>").Append(Escape(document.Title)).Append("</h2>");
            sb.Append("<p class=\"crumb\">Assembly <code class=\"inline\">")
              .Append(Escape(document.AssemblyName)).Append("</code> &middot; version ")
              .Append(Escape(document.AssemblyVersion)).Append("</p>");
            if (!string.IsNullOrWhiteSpace(document.Description))
            {
                sb.Append("<div class=\"md\">").Append(MarkdownRenderer.ToHtml(document.Description)).Append("</div>");
            }
            int typeCount = document.Namespaces.Sum(n => n.Types.Count);
            sb.Append("<p>This reference documents <strong>").Append(typeCount)
              .Append("</strong> public type(s) across <strong>").Append(document.Namespaces.Count)
              .Append("</strong> namespace(s). Use the sidebar or search (press <code class=\"inline\">/</code>) to navigate.</p>");
            sb.Append("</section>");

            // One view per type.
            foreach (var ns in document.Namespaces)
            {
                foreach (var type in ns.Types)
                {
                    sb.Append(RenderTypeView(type));
                }
            }
            return sb.ToString();
        }

        private string RenderTypeView(ApiType type)
        {
            var sb = new StringBuilder();
            sb.Append("<section id=\"view-").Append(type.Slug).Append("\" class=\"content hidden\">");

            sb.Append("<div class=\"type-header\">");
            sb.Append("<p class=\"crumb\">").Append(Escape(type.Namespace)).Append("</p>");
            sb.Append("<h2>").Append(KindBadge(type.Kind)).Append(' ').Append(Escape(type.Name));
            if (type.IsStatic) sb.Append("<span class=\"pill\">static</span>");
            sb.Append("</h2>");
            sb.Append("</div>");

            sb.Append("<pre class=\"sig\"><code>").Append(Highlight(type.Declaration)).Append("</code></pre>");

            if (!string.IsNullOrWhiteSpace(type.Docs.Summary))
                sb.Append("<p class=\"summary\">").Append(RenderText(type.Docs.Summary!)).Append("</p>");
            if (!string.IsNullOrWhiteSpace(type.Docs.Remarks))
                sb.Append("<div class=\"remarks\">").Append(RenderProse(type.Docs.Remarks!)).Append("</div>");
            if (!string.IsNullOrWhiteSpace(type.Docs.Example))
                sb.Append(RenderExample(type.Docs.Example!));

            // Group members by kind for readability.
            RenderMemberGroup(sb, type, "Constructors", MemberKind.Constructor);
            RenderMemberGroup(sb, type, "Properties", MemberKind.Property);
            RenderMemberGroup(sb, type, "Methods", MemberKind.Method);
            RenderMemberGroup(sb, type, "Events", MemberKind.Event);
            RenderMemberGroup(sb, type, "Fields", MemberKind.Field);
            RenderMemberGroup(sb, type, "Values", MemberKind.EnumField);

            sb.Append("</section>");
            return sb.ToString();
        }

        private void RenderMemberGroup(StringBuilder sb, ApiType type, string title, MemberKind kind)
        {
            var members = type.Members.Where(m => m.Kind == kind).ToList();
            if (members.Count == 0) return;

            sb.Append("<h3 class=\"section-title\">").Append(title).Append("</h3>");
            foreach (var m in members)
            {
                sb.Append("<div class=\"member\" id=\"").Append(m.Slug).Append("\">");
                sb.Append("<h4>").Append(Escape(m.Name));
                if (m.IsStatic) sb.Append(" <span class=\"pill\">static</span>");
                sb.Append("</h4>");

                if (kind != MemberKind.EnumField)
                    sb.Append("<pre class=\"sig\"><code>").Append(Highlight(m.Signature)).Append("</code></pre>");

                if (!string.IsNullOrWhiteSpace(m.Docs.Summary))
                    sb.Append("<p>").Append(RenderText(m.Docs.Summary!)).Append("</p>");

                if (m.Parameters.Count > 0 && m.Parameters.Any(p => !string.IsNullOrWhiteSpace(p.Description)))
                {
                    sb.Append("<div class=\"member-section\"><h5>Parameters</h5>");
                    sb.Append("<table class=\"params\"><thead><tr><th>Name</th><th>Type</th><th>Description</th></tr></thead><tbody>");
                    foreach (var p in m.Parameters)
                    {
                        sb.Append("<tr><td class=\"pname\">").Append(Escape(p.Name)).Append("</td>")
                          .Append("<td><code class=\"inline\">").Append(Escape(p.TypeName)).Append("</code></td>")
                          .Append("<td>").Append(RenderText(p.Description ?? "")).Append("</td></tr>");
                    }
                    sb.Append("</tbody></table></div>");
                }

                if (!string.IsNullOrWhiteSpace(m.Docs.Returns))
                    sb.Append("<div class=\"member-section\"><h5>Returns</h5><p>")
                      .Append(RenderText(m.Docs.Returns!)).Append("</p></div>");

                if (m.Docs.Exceptions.Count > 0)
                {
                    sb.Append("<div class=\"member-section\"><h5>Exceptions</h5><table class=\"params\"><tbody>");
                    foreach (var ex in m.Docs.Exceptions)
                    {
                        sb.Append("<tr><td class=\"pname\"><code class=\"inline\">").Append(Escape(ex.TypeName))
                          .Append("</code></td><td>").Append(RenderText(ex.Description)).Append("</td></tr>");
                    }
                    sb.Append("</tbody></table></div>");
                }

                if (!string.IsNullOrWhiteSpace(m.Docs.Remarks))
                    sb.Append("<div class=\"member-section\"><h5>Remarks</h5><div class=\"remarks\">")
                      .Append(RenderProse(m.Docs.Remarks!)).Append("</div></div>");

                if (!string.IsNullOrWhiteSpace(m.Docs.Example))
                    sb.Append(RenderExample(m.Docs.Example!));

                sb.Append("</div>");
            }
        }

        // ---- Rendering helpers ----

        private string RenderExample(string code)
        {
            return "<div class=\"member-section\"><h5>Example</h5><pre class=\"code\"><code>"
                   + Highlight(code) + "</code></pre></div>";
        }

        /// <summary>Renders flattened doc text, converting blank lines to paragraph breaks.</summary>
        private static string RenderProse(string text)
        {
            var paragraphs = text.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var p in paragraphs)
            {
                sb.Append("<p>").Append(RenderText(p.Trim())).Append("</p>");
            }
            return sb.ToString();
        }

        /// <summary>Escapes text and converts single newlines to &lt;br&gt;.</summary>
        private static string RenderText(string text)
        {
            return Escape(text).Replace("\n", "<br/>");
        }

        private static string KindBadge(TypeKind kind)
        {
            string cls, letter;
            switch (kind)
            {
                case TypeKind.Struct: cls = "k-struct"; letter = "S"; break;
                case TypeKind.Interface: cls = "k-interface"; letter = "I"; break;
                case TypeKind.Enum: cls = "k-enum"; letter = "E"; break;
                case TypeKind.Delegate: cls = "k-delegate"; letter = "D"; break;
                default: cls = "k-class"; letter = "C"; break;
            }
            return "<span class=\"badge " + cls + "\" title=\"" + kind + "\">" + letter + "</span>";
        }

        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "public","private","protected","internal","static","sealed","abstract","virtual",
            "override","class","struct","interface","enum","delegate","event","const","readonly",
            "void","get","set","init","this","ref","out","in","params","async","partial"
        };

        /// <summary>
        /// Lightweight keyword highlighting for signature/code blocks (HTML-escaped). Identifiers
        /// that match a documented type are turned into in-page cross-links.
        /// </summary>
        private string Highlight(string code)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < code.Length)
            {
                char c = code[i];
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) i++;
                    string word = code.Substring(start, i - start);
                    if (Keywords.Contains(word))
                        sb.Append("<span class=\"kw\">").Append(word).Append("</span>");
                    else if (_typeLinks.TryGetValue(word, out var slug))
                        sb.Append("<a class=\"tlink\" href=\"#").Append(slug).Append("\">").Append(Escape(word)).Append("</a>");
                    else
                        sb.Append(Escape(word));
                }
                else
                {
                    sb.Append(EscapeChar(c));
                    i++;
                }
            }
            return sb.ToString();
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string EscapeChar(char c)
        {
            switch (c)
            {
                case '&': return "&amp;";
                case '<': return "&lt;";
                case '>': return "&gt;";
                case '"': return "&quot;";
                default: return c.ToString();
            }
        }

        private static string ReadAsset(string name)
        {
            var asm = typeof(HtmlRenderer).Assembly;
            string resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Embedded asset not found: " + name);

            using var stream = asm.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ZDoc.Core.Model;

namespace ZDoc.Core.Xml
{
    /// <summary>
    /// Parses a compiler-generated XML documentation file into a lookup of doc-id to
    /// <see cref="XmlDocEntry"/>. Inline tags such as <c>&lt;see&gt;</c>, <c>&lt;paramref&gt;</c>
    /// and <c>&lt;c&gt;</c> are flattened to readable text; <c>&lt;code&gt;</c> blocks are
    /// de-indented and preserved.
    /// </summary>
    public sealed class XmlDocParser
    {
        private readonly Dictionary<string, XmlDocEntry> _entries =
            new Dictionary<string, XmlDocEntry>(StringComparer.Ordinal);

        /// <summary>Number of member entries parsed.</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Loads and parses the XML documentation at <paramref name="path"/>.
        /// Returns an empty parser (no throw) if the file does not exist, so documentation is
        /// treated as optional.
        /// </summary>
        public static XmlDocParser Load(string? path)
        {
            var parser = new XmlDocParser();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return parser;
            }

            XDocument doc;
            using (var stream = File.OpenRead(path!))
            {
                doc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            }

            var members = doc.Root?.Element("members")?.Elements("member");
            if (members == null) return parser;

            foreach (var member in members)
            {
                string? id = member.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(id)) continue;
                parser._entries[id!] = ParseMember(member);
            }

            return parser;
        }

        /// <summary>
        /// Returns the documentation for a doc-id, or an empty (non-null) entry if absent.
        /// </summary>
        public XmlDocEntry Get(string docId)
        {
            if (docId != null && _entries.TryGetValue(docId, out var entry))
            {
                return entry;
            }
            return new XmlDocEntry();
        }

        /// <summary>True if a doc-id has a documentation entry.</summary>
        public bool Contains(string docId) => docId != null && _entries.ContainsKey(docId);

        private static XmlDocEntry ParseMember(XElement member)
        {
            var entry = new XmlDocEntry();

            var summary = member.Element("summary");
            if (summary != null) entry.Summary = Flatten(summary);

            var remarks = member.Element("remarks");
            if (remarks != null) entry.Remarks = Flatten(remarks);

            var returns = member.Element("returns");
            if (returns != null) entry.Returns = Flatten(returns);

            // <example> may contain prose plus a <code> block; prefer the code text if present.
            var example = member.Element("example");
            if (example != null)
            {
                var codeEl = example.Element("code");
                entry.Example = codeEl != null ? NormalizeCode(codeEl.Value) : Flatten(example);
            }

            foreach (var param in member.Elements("param"))
            {
                string? name = param.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                    entry.Parameters[name!] = Flatten(param);
            }

            foreach (var tparam in member.Elements("typeparam"))
            {
                string? name = tparam.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                    entry.TypeParameters[name!] = Flatten(tparam);
            }

            foreach (var ex in member.Elements("exception"))
            {
                string cref = ShortenCref(ex.Attribute("cref")?.Value ?? "");
                entry.Exceptions.Add(new ExceptionDoc(cref, Flatten(ex)));
            }

            return entry;
        }

        /// <summary>
        /// Flattens an element's mixed content (text + inline tags) into a single readable
        /// string, collapsing insignificant whitespace.
        /// </summary>
        private static string Flatten(XElement element)
        {
            var sb = new StringBuilder();
            FlattenInto(element, sb);
            return CollapseWhitespace(sb.ToString());
        }

        private static void FlattenInto(XElement element, StringBuilder sb)
        {
            foreach (var node in element.Nodes())
            {
                switch (node)
                {
                    case XText text:
                        sb.Append(text.Value);
                        break;

                    case XElement child:
                        switch (child.Name.LocalName)
                        {
                            case "see":
                            case "seealso":
                                // Prefer explicit link text; else shorten the cref/href.
                                string seeText = child.Value;
                                if (string.IsNullOrWhiteSpace(seeText))
                                {
                                    seeText = ShortenCref(
                                        child.Attribute("cref")?.Value
                                        ?? child.Attribute("href")?.Value
                                        ?? child.Attribute("langword")?.Value
                                        ?? "");
                                }
                                sb.Append(seeText);
                                break;

                            case "paramref":
                            case "typeparamref":
                                sb.Append(child.Attribute("name")?.Value ?? child.Value);
                                break;

                            case "c":
                                sb.Append(child.Value);
                                break;

                            case "para":
                                sb.Append('\n');
                                FlattenInto(child, sb);
                                sb.Append('\n');
                                break;

                            case "code":
                                // Inline-flattened code: keep on its own lines.
                                sb.Append('\n').Append(NormalizeCode(child.Value)).Append('\n');
                                break;

                            case "list":
                                FlattenList(child, sb);
                                break;

                            default:
                                FlattenInto(child, sb);
                                break;
                        }
                        break;
                }
            }
        }

        private static void FlattenList(XElement list, StringBuilder sb)
        {
            foreach (var item in list.Elements("item"))
            {
                sb.Append("\n- ");
                var term = item.Element("term");
                var desc = item.Element("description");
                if (term != null)
                {
                    sb.Append(Flatten(term));
                    if (desc != null) sb.Append(": ");
                }
                if (desc != null) sb.Append(Flatten(desc));
                if (term == null && desc == null) FlattenInto(item, sb);
            }
            sb.Append('\n');
        }

        /// <summary>
        /// Removes the common leading indentation from a <c>&lt;code&gt;</c> block and trims
        /// surrounding blank lines, so embedded examples render cleanly.
        /// </summary>
        internal static string NormalizeCode(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            var lines = raw.Replace("\r\n", "\n").Split('\n').ToList();

            // Drop leading/trailing blank lines.
            while (lines.Count > 0 && lines[0].Trim().Length == 0) lines.RemoveAt(0);
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
            if (lines.Count == 0) return "";

            // Compute the minimum indentation across non-empty lines.
            int minIndent = int.MaxValue;
            foreach (var line in lines)
            {
                if (line.Trim().Length == 0) continue;
                int indent = line.Length - line.TrimStart().Length;
                if (indent < minIndent) minIndent = indent;
            }
            if (minIndent == int.MaxValue) minIndent = 0;

            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                sb.Append(line.Length >= minIndent ? line.Substring(minIndent) : line.TrimStart());
                if (i < lines.Count - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Turns a cref like <c>T:System.Action</c> or <c>M:Ns.Type.Method(...)</c> into a short,
        /// human-friendly name (<c>Action</c>, <c>Method</c>).
        /// </summary>
        internal static string ShortenCref(string cref)
        {
            if (string.IsNullOrEmpty(cref)) return "";

            // Strip the "X:" prefix.
            string body = cref.Length > 2 && cref[1] == ':' ? cref.Substring(2) : cref;

            // Drop any parameter list.
            int paren = body.IndexOf('(');
            if (paren >= 0) body = body.Substring(0, paren);

            // Take the last dotted segment.
            int lastDot = body.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < body.Length - 1) body = body.Substring(lastDot + 1);

            // Strip generic arity markers.
            int backtick = body.IndexOf('`');
            if (backtick >= 0) body = body.Substring(0, backtick);

            return body;
        }

        private static string CollapseWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var sb = new StringBuilder(text.Length);
            bool lastWasSpace = false;
            foreach (char c in text.Replace("\r\n", "\n"))
            {
                if (c == '\n')
                {
                    // Preserve intentional line breaks (from <para>/<code>).
                    sb.Append('\n');
                    lastWasSpace = true;
                    continue;
                }
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace) { sb.Append(' '); lastWasSpace = true; }
                    continue;
                }
                sb.Append(c);
                lastWasSpace = false;
            }

            // Trim and collapse 3+ newlines to a maximum of two.
            var result = sb.ToString().Trim();
            while (result.Contains("\n\n\n")) result = result.Replace("\n\n\n", "\n\n");
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace ZDoc.Core.Rendering
{
    /// <summary>
    /// A small, dependency-free Markdown to HTML converter covering the subset commonly used
    /// in README and doc prose: ATX headings, fenced and indented code, unordered/ordered
    /// lists, blockquotes, pipe tables, horizontal rules, paragraphs, and the inline spans
    /// bold, italic, inline code, and links. All text is HTML-escaped; raw HTML in the input
    /// is treated as plain text (escaped), so output is safe to inline.
    /// </summary>
    public static class MarkdownRenderer
    {
        /// <summary>Converts a Markdown string to an HTML fragment.</summary>
        public static string ToHtml(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return "";

            var lines = markdown!.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var html = new StringBuilder();
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];

                // Blank line.
                if (line.Trim().Length == 0) { i++; continue; }

                // Fenced code block: ``` or ~~~
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
                {
                    i = RenderFence(lines, i, html);
                    continue;
                }

                // ATX heading: # .. ######
                if (trimmed.StartsWith("#"))
                {
                    int level = 0;
                    while (level < trimmed.Length && trimmed[level] == '#') level++;
                    if (level >= 1 && level <= 6 && level < trimmed.Length && trimmed[level] == ' ')
                    {
                        string text = trimmed.Substring(level + 1).Trim();
                        html.Append("<h").Append(level).Append('>')
                            .Append(RenderInline(text))
                            .Append("</h").Append(level).Append('>');
                        i++;
                        continue;
                    }
                }

                // Horizontal rule: ---, ***, ___
                if (IsHorizontalRule(trimmed))
                {
                    html.Append("<hr/>");
                    i++;
                    continue;
                }

                // Blockquote.
                if (trimmed.StartsWith("> "))
                {
                    i = RenderBlockquote(lines, i, html);
                    continue;
                }

                // Table: a header row followed by a separator row of dashes/pipes.
                if (line.Contains("|") && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
                {
                    i = RenderTable(lines, i, html);
                    continue;
                }

                // List (unordered or ordered).
                if (IsUnorderedItem(trimmed) || IsOrderedItem(trimmed))
                {
                    i = RenderList(lines, i, html);
                    continue;
                }

                // Indented code block (4 spaces or a tab).
                if (line.StartsWith("    ") || line.StartsWith("\t"))
                {
                    i = RenderIndentedCode(lines, i, html);
                    continue;
                }

                // Paragraph: gather consecutive non-blank, non-structural lines.
                i = RenderParagraph(lines, i, html);
            }

            return html.ToString();
        }

        // ---- Block helpers ----

        private static int RenderFence(string[] lines, int start, StringBuilder html)
        {
            string opener = lines[start].TrimStart();
            string fence = opener.StartsWith("~~~") ? "~~~" : "```";
            string lang = opener.Substring(fence.Length).Trim();

            var code = new StringBuilder();
            int i = start + 1;
            for (; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith(fence)) { i++; break; }
                code.Append(Escape(lines[i])).Append('\n');
            }

            html.Append("<pre class=\"code\">");
            if (lang.Length > 0) html.Append("<code data-lang=\"").Append(Escape(lang)).Append("\">");
            else html.Append("<code>");
            html.Append(TrimTrailingNewline(code.ToString()));
            html.Append("</code></pre>");
            return i;
        }

        private static int RenderIndentedCode(string[] lines, int start, StringBuilder html)
        {
            var code = new StringBuilder();
            int i = start;
            for (; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Trim().Length == 0) { code.Append('\n'); continue; }
                if (!(line.StartsWith("    ") || line.StartsWith("\t"))) break;
                string stripped = line.StartsWith("\t") ? line.Substring(1) : line.Substring(4);
                code.Append(Escape(stripped)).Append('\n');
            }
            html.Append("<pre class=\"code\"><code>").Append(TrimTrailingNewline(code.ToString())).Append("</code></pre>");
            return i;
        }

        private static int RenderBlockquote(string[] lines, int start, StringBuilder html)
        {
            var inner = new StringBuilder();
            int i = start;
            for (; i < lines.Length; i++)
            {
                string t = lines[i].TrimStart();
                if (t.StartsWith("> ")) inner.Append(t.Substring(2)).Append('\n');
                else if (t == ">") inner.Append('\n');
                else break;
            }
            html.Append("<blockquote>").Append(ToHtml(inner.ToString())).Append("</blockquote>");
            return i;
        }

        private static int RenderList(string[] lines, int start, StringBuilder html)
        {
            bool ordered = IsOrderedItem(lines[start].TrimStart());
            html.Append(ordered ? "<ol>" : "<ul>");

            int i = start;
            while (i < lines.Length)
            {
                string trimmed = lines[i].TrimStart();
                bool isItem = ordered ? IsOrderedItem(trimmed) : IsUnorderedItem(trimmed);
                if (!isItem)
                {
                    if (trimmed.Length == 0) { i++; continue; } // allow blank lines between items
                    break;
                }

                string content = StripItemMarker(trimmed, ordered);
                html.Append("<li>").Append(RenderInline(content)).Append("</li>");
                i++;
            }

            html.Append(ordered ? "</ol>" : "</ul>");
            return i;
        }

        private static int RenderTable(string[] lines, int start, StringBuilder html)
        {
            var headers = SplitRow(lines[start]);
            int i = start + 2; // skip header + separator

            html.Append("<table class=\"md-table\"><thead><tr>");
            foreach (var h in headers) html.Append("<th>").Append(RenderInline(h)).Append("</th>");
            html.Append("</tr></thead><tbody>");

            for (; i < lines.Length; i++)
            {
                if (!lines[i].Contains("|") || lines[i].Trim().Length == 0) break;
                var cells = SplitRow(lines[i]);
                html.Append("<tr>");
                foreach (var c in cells) html.Append("<td>").Append(RenderInline(c)).Append("</td>");
                html.Append("</tr>");
            }

            html.Append("</tbody></table>");
            return i;
        }

        private static int RenderParagraph(string[] lines, int start, StringBuilder html)
        {
            var para = new StringBuilder();
            // Always consume the starting line so the parser is guaranteed to make progress,
            // even if it begins with a character that looks structural (e.g. an invalid heading).
            para.Append(lines[start].TrimStart());
            int i = start + 1;

            for (; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                if (trimmed.Length == 0) break;
                if (trimmed.StartsWith("#") || trimmed.StartsWith("```") || trimmed.StartsWith("~~~")
                    || trimmed.StartsWith("> ") || IsHorizontalRule(trimmed)
                    || IsUnorderedItem(trimmed) || IsOrderedItem(trimmed))
                    break;

                para.Append(' ').Append(trimmed);
            }
            html.Append("<p>").Append(RenderInline(para.ToString())).Append("</p>");
            return i;
        }

        // ---- Inline rendering ----

        /// <summary>
        /// Renders inline Markdown spans within already block-delimited text. Inline code is
        /// processed first (and its content is not re-parsed), then links, bold, and italic.
        /// </summary>
        internal static string RenderInline(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var sb = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];

                // Inline code: `...`
                if (c == '`')
                {
                    int end = text.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        string code = text.Substring(i + 1, end - i - 1);
                        sb.Append("<code class=\"inline\">").Append(Escape(code)).Append("</code>");
                        i = end + 1;
                        continue;
                    }
                }

                // Link: [text](url)
                if (c == '[')
                {
                    int close = text.IndexOf(']', i + 1);
                    if (close > i && close + 1 < text.Length && text[close + 1] == '(')
                    {
                        int urlEnd = text.IndexOf(')', close + 2);
                        if (urlEnd > close)
                        {
                            string label = text.Substring(i + 1, close - i - 1);
                            string url = text.Substring(close + 2, urlEnd - close - 2).Trim();
                            sb.Append("<a href=\"").Append(EscapeAttribute(url)).Append("\"");
                            if (IsExternal(url)) sb.Append(" rel=\"noopener noreferrer\" target=\"_blank\"");
                            sb.Append('>').Append(RenderInline(label)).Append("</a>");
                            i = urlEnd + 1;
                            continue;
                        }
                    }
                }

                // Bold: **...** or __...__
                if ((c == '*' && Peek(text, i, '*')) || (c == '_' && Peek(text, i, '_')))
                {
                    string marker = c.ToString() + c;
                    int end = text.IndexOf(marker, i + 2, StringComparison.Ordinal);
                    if (end > i)
                    {
                        string inner = text.Substring(i + 2, end - i - 2);
                        sb.Append("<strong>").Append(RenderInline(inner)).Append("</strong>");
                        i = end + 2;
                        continue;
                    }
                }

                // Italic: *...* or _..._
                if (c == '*' || c == '_')
                {
                    int end = text.IndexOf(c, i + 1);
                    if (end > i)
                    {
                        string inner = text.Substring(i + 1, end - i - 1);
                        sb.Append("<em>").Append(RenderInline(inner)).Append("</em>");
                        i = end + 1;
                        continue;
                    }
                }

                sb.Append(EscapeChar(c));
                i++;
            }
            return sb.ToString();
        }

        // ---- Predicates and small helpers ----

        private static bool Peek(string text, int i, char ch) => i + 1 < text.Length && text[i + 1] == ch;

        private static bool IsHorizontalRule(string trimmed)
        {
            if (trimmed.Length < 3) return false;
            char c = trimmed[0];
            if (c != '-' && c != '*' && c != '_') return false;
            foreach (char ch in trimmed)
                if (ch != c && ch != ' ') return false;
            int count = 0;
            foreach (char ch in trimmed) if (ch == c) count++;
            return count >= 3;
        }

        private static bool IsUnorderedItem(string trimmed) =>
            trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ ");

        private static bool IsOrderedItem(string trimmed)
        {
            int dot = 0;
            while (dot < trimmed.Length && char.IsDigit(trimmed[dot])) dot++;
            return dot > 0 && dot + 1 < trimmed.Length && trimmed[dot] == '.' && trimmed[dot + 1] == ' ';
        }

        private static string StripItemMarker(string trimmed, bool ordered)
        {
            if (!ordered) return trimmed.Substring(2);
            int dot = trimmed.IndexOf('.');
            return trimmed.Substring(dot + 2);
        }

        private static bool IsTableSeparator(string line)
        {
            string t = line.Trim();
            if (!t.Contains("-") || !t.Contains("|") && !t.StartsWith("-")) { /* allow no leading pipe */ }
            bool sawDash = false;
            foreach (char c in t)
            {
                if (c == '-') sawDash = true;
                else if (c != '|' && c != ':' && c != ' ') return false;
            }
            return sawDash;
        }

        private static List<string> SplitRow(string line)
        {
            string t = line.Trim();
            if (t.StartsWith("|")) t = t.Substring(1);
            if (t.EndsWith("|")) t = t.Substring(0, t.Length - 1);
            var cells = new List<string>();
            foreach (var part in t.Split('|')) cells.Add(part.Trim());
            return cells;
        }

        private static bool IsExternal(string url) =>
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        private static string TrimTrailingNewline(string s) =>
            s.EndsWith("\n") ? s.Substring(0, s.Length - 1) : s;

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static string EscapeAttribute(string text) =>
            Escape(text).Replace("\"", "&quot;");

        private static string EscapeChar(char c)
        {
            switch (c)
            {
                case '&': return "&amp;";
                case '<': return "&lt;";
                case '>': return "&gt;";
                default: return c.ToString();
            }
        }
    }
}

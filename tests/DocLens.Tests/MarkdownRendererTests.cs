using DocLens.Core.Rendering;
using Xunit;

namespace DocLens.Tests
{
    /// <summary>Unit tests for the dependency-free Markdown to HTML converter.</summary>
    public class MarkdownRendererTests
    {
        [Theory]
        [InlineData("# Title", "<h1>Title</h1>")]
        [InlineData("### Sub", "<h3>Sub</h3>")]
        [InlineData("###### Six", "<h6>Six</h6>")]
        public void Headings_Render(string md, string expected)
        {
            Assert.Equal(expected, MarkdownRenderer.ToHtml(md));
        }

        [Fact]
        public void Heading_TooManyHashes_IsParagraph()
        {
            // 7 hashes is not a valid heading.
            string html = MarkdownRenderer.ToHtml("####### Nope");
            Assert.Contains("<p>", html);
            Assert.DoesNotContain("<h7>", html);
        }

        [Fact]
        public void Paragraph_JoinsWrappedLines()
        {
            string html = MarkdownRenderer.ToHtml("line one\nline two");
            Assert.Equal("<p>line one line two</p>", html);
        }

        [Fact]
        public void Bold_And_Italic_Render()
        {
            Assert.Equal("<p><strong>x</strong></p>", MarkdownRenderer.ToHtml("**x**"));
            Assert.Equal("<p><em>y</em></p>", MarkdownRenderer.ToHtml("*y*"));
            Assert.Equal("<p><strong>a</strong> and <em>b</em></p>", MarkdownRenderer.ToHtml("**a** and *b*"));
        }

        [Fact]
        public void InlineCode_IsEscapedAndNotReparsed()
        {
            string html = MarkdownRenderer.ToHtml("use `a < b && *x*` here");
            Assert.Contains("<code class=\"inline\">a &lt; b &amp;&amp; *x*</code>", html);
            // The asterisks inside code must NOT become italic.
            Assert.DoesNotContain("<em>", html);
        }

        [Fact]
        public void FencedCode_PreservesContentAndLang()
        {
            string md = "```csharp\nvar x = 1 < 2;\n```";
            string html = MarkdownRenderer.ToHtml(md);
            Assert.Contains("<pre class=\"code\"><code data-lang=\"csharp\">", html);
            Assert.Contains("var x = 1 &lt; 2;", html);
        }

        [Fact]
        public void UnorderedList_Renders()
        {
            string html = MarkdownRenderer.ToHtml("- one\n- two\n- three");
            Assert.Equal("<ul><li>one</li><li>two</li><li>three</li></ul>", html);
        }

        [Fact]
        public void OrderedList_Renders()
        {
            string html = MarkdownRenderer.ToHtml("1. a\n2. b");
            Assert.Equal("<ol><li>a</li><li>b</li></ol>", html);
        }

        [Fact]
        public void Link_Internal_And_External()
        {
            string ext = MarkdownRenderer.ToHtml("[site](https://example.com)");
            Assert.Contains("href=\"https://example.com\"", ext);
            Assert.Contains("target=\"_blank\"", ext);

            string rel = MarkdownRenderer.ToHtml("[doc](page.html)");
            Assert.Contains("href=\"page.html\"", rel);
            Assert.DoesNotContain("target=\"_blank\"", rel);
        }

        [Fact]
        public void Table_Renders()
        {
            string md = "| A | B |\n| --- | --- |\n| 1 | 2 |";
            string html = MarkdownRenderer.ToHtml(md);
            Assert.Contains("<table class=\"md-table\">", html);
            Assert.Contains("<th>A</th>", html);
            Assert.Contains("<td>1</td>", html);
        }

        [Fact]
        public void HorizontalRule_Renders()
        {
            Assert.Equal("<hr/>", MarkdownRenderer.ToHtml("---"));
        }

        [Fact]
        public void Blockquote_Renders()
        {
            string html = MarkdownRenderer.ToHtml("> quoted text");
            Assert.Contains("<blockquote>", html);
            Assert.Contains("quoted text", html);
        }

        [Fact]
        public void RawHtml_IsEscaped()
        {
            string html = MarkdownRenderer.ToHtml("a <script>alert(1)</script> b");
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
        }

        [Fact]
        public void Empty_ReturnsEmpty()
        {
            Assert.Equal("", MarkdownRenderer.ToHtml(""));
            Assert.Equal("", MarkdownRenderer.ToHtml(null));
            Assert.Equal("", MarkdownRenderer.ToHtml("   "));
        }
    }
}

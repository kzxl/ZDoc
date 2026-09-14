using System.Linq;
using ZeroDoc.Core.Reflection;
using ZeroDoc.Core.Rendering;
using Xunit;

namespace ZeroDoc.Tests
{
    /// <summary>Tests that the HTML renderer produces a complete, self-contained document.</summary>
    public class HtmlRendererTests
    {
        private static string RenderSample()
        {
            var extractor = new ApiExtractor(new ExtractionOptions
            {
                Title = "Sample API",
                Description = "A test fixture library.",
            });
            var model = extractor.Extract(SampleAssembly.DllPath, SampleAssembly.XmlPath);
            return new HtmlRenderer().Render(model);
        }

        [Fact]
        public void Render_ProducesCompleteHtml()
        {
            string html = RenderSample();

            Assert.StartsWith("<!DOCTYPE html>", html.TrimStart());
            Assert.Contains("</html>", html);
            // CSS and JS are inlined (self-contained).
            Assert.Contains("<style>", html);
            Assert.Contains("--accent", html);            // from ZeroDoc.css
            Assert.Contains("zerodoc-theme", html);        // from ZeroDoc.js
        }

        [Fact]
        public void Render_LeavesNoUnreplacedPlaceholders()
        {
            string html = RenderSample();
            Assert.DoesNotContain("{{", html);
            Assert.DoesNotContain("}}", html);
        }

        [Fact]
        public void Render_IncludesEveryTypeAsAView()
        {
            var extractor = new ApiExtractor();
            var model = extractor.Extract(SampleAssembly.DllPath, SampleAssembly.XmlPath);
            string html = new HtmlRenderer().Render(model);

            foreach (var type in model.Namespaces.SelectMany(n => n.Types))
            {
                Assert.Contains($"id=\"view-{type.Slug}\"", html);
                Assert.Contains($"data-target=\"view-{type.Slug}\"", html);
            }
        }

        [Fact]
        public void Render_EscapesGenericAngleBrackets()
        {
            string html = RenderSample();
            // Repository<T> must be HTML-escaped in output, never raw "<T>" creating a bogus tag.
            Assert.Contains("Repository&lt;T&gt;", html);
        }

        [Fact]
        public void Render_IncludesAccessibilityLandmarks()
        {
            string html = RenderSample();
            Assert.Contains("skip-link", html);
            Assert.Contains("aria-label", html);
            Assert.Contains("<main", html);
            Assert.Contains("<nav", html);
        }

        [Fact]
        public void Render_CrossLinksKnownTypes()
        {
            // Money's operator returns/takes Money; the signature should link to its own view.
            string html = RenderSample();
            Assert.Contains("<a class=\"tlink\" href=\"#", html);
        }

        [Fact]
        public void Render_SidebarSearchIndexesMemberNames()
        {
            string html = RenderSample();
            // The Repository nav entry's data-search must include a member name (e.g. LoadAsync),
            // so searching by member surfaces the declaring type.
            int navStart = html.IndexOf("data-target=\"view-t-ZeroDoc-samplelib-repository", System.StringComparison.OrdinalIgnoreCase);
            Assert.True(navStart >= 0, "Repository nav entry not found.");
            int dataSearchStart = html.IndexOf("data-search=\"", navStart, System.StringComparison.Ordinal);
            int dataSearchEnd = html.IndexOf('"', dataSearchStart + "data-search=\"".Length);
            string dataSearch = html.Substring(dataSearchStart, dataSearchEnd - dataSearchStart);
            Assert.Contains("LoadAsync", dataSearch);
        }

        [Fact]
        public void Render_MarkdownDescription_ProducesHeadingsNotRawHashes()
        {
            var extractor = new ApiExtractor(new ExtractionOptions
            {
                Description = "# Hello\n\nSome **bold** intro.",
            });
            var model = extractor.Extract(SampleAssembly.DllPath, SampleAssembly.XmlPath);
            string html = new HtmlRenderer().Render(model);

            Assert.Contains("<h1>Hello</h1>", html);
            Assert.Contains("<strong>bold</strong>", html);
            Assert.DoesNotContain("# Hello", html);
        }

        [Fact]
        public void Render_IncludesFuzzySearchAndArrowKeyNavigation()
        {
            string html = RenderSample();
            Assert.Contains("levenshtein", html);
            Assert.Contains("fuzzyMatch", html);
            Assert.Contains("highlighted", html);
            Assert.Contains("ArrowDown", html);
        }
    }
}

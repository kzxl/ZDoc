using System.Linq;
using DocLens.Core.Reflection;
using DocLens.Core.Rendering;
using Xunit;

namespace DocLens.Tests
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
            Assert.Contains("--accent", html);            // from doclens.css
            Assert.Contains("doclens-theme", html);        // from doclens.js
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
    }
}

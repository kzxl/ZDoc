using System.IO;
using DocLens.Core.Xml;
using Xunit;

namespace DocLens.Tests
{
    /// <summary>Unit tests for XML doc parsing, inline-tag flattening, and code normalization.</summary>
    public class XmlDocParserTests
    {
        private static XmlDocParser ParseInline(string membersXml)
        {
            string xml = $@"<?xml version=""1.0""?><doc><assembly><name>X</name></assembly><members>{membersXml}</members></doc>";
            string temp = Path.GetTempFileName();
            File.WriteAllText(temp, xml);
            try { return XmlDocParser.Load(temp); }
            finally { File.Delete(temp); }
        }

        [Fact]
        public void Load_MissingFile_ReturnsEmptyParser()
        {
            var parser = XmlDocParser.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-123.xml"));
            Assert.Equal(0, parser.Count);
            Assert.True(parser.Get("T:Whatever").IsEmpty);
        }

        [Fact]
        public void Summary_FlattensSeeAndParamref()
        {
            var parser = ParseInline(
                @"<member name=""M:T.M(System.Int32)"">
                    <summary>Calls <see cref=""T:System.Action""/> with <paramref name=""count""/> items.</summary>
                  </member>");

            var entry = parser.Get("M:T.M(System.Int32)");
            Assert.Equal("Calls Action with count items.", entry.Summary);
        }

        [Fact]
        public void Params_AndReturns_AreCaptured()
        {
            var parser = ParseInline(
                @"<member name=""M:T.M(System.Int32)"">
                    <summary>Does a thing.</summary>
                    <param name=""count"">How many.</param>
                    <returns>The total.</returns>
                  </member>");

            var entry = parser.Get("M:T.M(System.Int32)");
            Assert.Equal("How many.", entry.Parameters["count"]);
            Assert.Equal("The total.", entry.Returns);
        }

        [Fact]
        public void Exceptions_ShortenCref()
        {
            var parser = ParseInline(
                @"<member name=""M:T.M"">
                    <exception cref=""T:System.ArgumentNullException"">If null.</exception>
                  </member>");

            var entry = parser.Get("M:T.M");
            Assert.Single(entry.Exceptions);
            Assert.Equal("ArgumentNullException", entry.Exceptions[0].TypeName);
            Assert.Equal("If null.", entry.Exceptions[0].Description);
        }

        [Fact]
        public void Example_Code_IsDeindented()
        {
            var parser = ParseInline(
                "<member name=\"M:T.M\"><example><code>\n        var x = 1;\n        var y = 2;\n      </code></example></member>");

            var entry = parser.Get("M:T.M");
            Assert.Equal("var x = 1;\nvar y = 2;", entry.Example);
        }

        [Theory]
        [InlineData("T:System.Action", "Action")]
        [InlineData("M:Ns.Type.Method(System.Int32)", "Method")]
        [InlineData("T:System.Collections.Generic.List`1", "List")]
        public void ShortenCref_Works(string input, string expected)
        {
            Assert.Equal(expected, XmlDocParser.ShortenCref(input));
        }
    }
}

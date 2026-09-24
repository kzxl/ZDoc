using System;
using System.Linq;
using ZDoc.Core.Model;
using ZDoc.Core.Reflection;
using Xunit;

namespace ZDoc.Tests
{
    /// <summary>
    /// End-to-end extraction tests that run ZDoc over the compiled sample assembly
    /// (dogfood), verifying types, members, signatures, and bound XML documentation.
    /// </summary>
    public class ApiExtractorTests
    {
        private static ApiDocument Extract()
        {
            var extractor = new ApiExtractor(new ExtractionOptions { Title = "Sample API" });
            return extractor.Extract(SampleAssembly.DllPath, SampleAssembly.XmlPath);
        }

        private static ApiType GetType(ApiDocument doc, string name) =>
            doc.Namespaces.SelectMany(n => n.Types).Single(t => t.Name == name || t.Name.StartsWith(name + "<"));

        [Fact]
        public void Extract_FindsAllPublicTypes()
        {
            var doc = Extract();
            var names = doc.Namespaces.SelectMany(n => n.Types).Select(t => t.Name).ToList();

            Assert.Contains(names, n => n.StartsWith("Repository<"));
            Assert.Contains("Money", names);
            Assert.Contains("Severity", names);
            Assert.Contains("LogHandler", names);
            Assert.Contains("IOperation", names);
            Assert.Contains("Constants", names);
            // Nested type is included.
            Assert.Contains("Options", names);
        }

        [Fact]
        public void Extract_ClassifiesKindsCorrectly()
        {
            var doc = Extract();
            Assert.Equal(TypeKind.Struct, GetType(doc, "Money").Kind);
            Assert.Equal(TypeKind.Enum, GetType(doc, "Severity").Kind);
            Assert.Equal(TypeKind.Delegate, GetType(doc, "LogHandler").Kind);
            Assert.Equal(TypeKind.Interface, GetType(doc, "IOperation").Kind);
            Assert.Equal(TypeKind.Class, GetType(doc, "Repository").Kind);
            Assert.True(GetType(doc, "Constants").IsStatic);
        }

        [Fact]
        public void Extract_BindsTypeSummaryFromXml()
        {
            var doc = Extract();
            var repo = GetType(doc, "Repository");
            Assert.False(repo.Docs.IsEmpty);
            Assert.Contains("repository abstraction", repo.Docs.Summary, StringComparison.OrdinalIgnoreCase);
            // Generic type-param doc is captured.
            Assert.True(repo.Docs.TypeParameters.ContainsKey("T"));
        }

        [Fact]
        public void Extract_CapturesMethodParametersAndReturns()
        {
            var doc = Extract();
            var repo = GetType(doc, "Repository");
            var load = repo.Members.Single(m => m.Name.StartsWith("LoadAsync"));

            Assert.Equal(MemberKind.Method, load.Kind);
            Assert.False(load.Docs.IsEmpty);
            Assert.Contains("loaded", load.Docs.Returns, StringComparison.OrdinalIgnoreCase);

            var sourceParam = load.Parameters.Single(p => p.Name == "source");
            Assert.False(string.IsNullOrWhiteSpace(sourceParam.Description));
            // Async return type formatted with C# alias + generics.
            Assert.Contains("Task<int>", load.Signature);
        }

        [Fact]
        public void Extract_CapturesExceptionsOnIndexer()
        {
            var doc = Extract();
            var repo = GetType(doc, "Repository");
            var indexer = repo.Members.Single(m => m.Kind == MemberKind.Property && m.Name == "this[]");

            Assert.Contains(indexer.Docs.Exceptions, e => e.TypeName == "ArgumentOutOfRangeException");
        }

        [Fact]
        public void Extract_EnumMembersHaveValuesAndDocs()
        {
            var doc = Extract();
            var severity = GetType(doc, "Severity");

            var values = severity.Members.Where(m => m.Kind == MemberKind.EnumField).Select(m => m.Name).ToList();
            Assert.Equal(new[] { "Debug", "Info", "Warning", "Error" }, values);
            Assert.All(severity.Members, m => Assert.False(m.Docs.IsEmpty));
        }

        [Fact]
        public void Extract_OperatorsAreIncluded()
        {
            var doc = Extract();
            var money = GetType(doc, "Money");
            Assert.Contains(money.Members, m => m.Name.StartsWith("op_Addition"));
            Assert.Contains(money.Members, m => m.Name.StartsWith("op_Explicit"));
        }

        [Fact]
        public void Extract_PublicOnly_ExcludesProtected()
        {
            // Sample has no protected members, but the option must not throw and must still work.
            var extractor = new ApiExtractor(new ExtractionOptions { IncludeProtected = false });
            var publicOnly = extractor.Extract(SampleAssembly.DllPath, SampleAssembly.XmlPath);
            Assert.NotEmpty(publicOnly.Namespaces);
        }
    }
}

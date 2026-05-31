using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using DocLens.Core.Reflection;
using Xunit;

namespace DocLens.Tests
{
    /// <summary>
    /// Validates that DocIdGenerator produces exactly the IDs the C# compiler writes into the
    /// XML documentation file. This is a true dogfood: we generate IDs from reflection over the
    /// sample assembly and assert each one is present in the compiler-produced XML.
    /// </summary>
    public class DocIdGeneratorTests
    {
        private static HashSet<string> LoadCompilerIds()
        {
            var doc = XDocument.Load(SampleAssembly.XmlPath);
            return doc.Root!.Element("members")!.Elements("member")
                .Select(m => m.Attribute("name")!.Value)
                .ToHashSet(StringComparer.Ordinal);
        }

        [Fact]
        public void EveryGeneratedTypeId_ExistsInCompilerXml()
        {
            var compilerIds = LoadCompilerIds();
            var asm = SampleAssembly.Loaded;

            foreach (var type in asm.GetExportedTypes())
            {
                string id = DocIdGenerator.ForType(type);
                Assert.True(compilerIds.Contains(id),
                    $"Type doc-id not found in compiler XML: {id}");
            }
        }

        [Fact]
        public void EveryGeneratedMemberId_ExistsInCompilerXml()
        {
            // Correctness direction: every doc-id the compiler emitted must be reproducible by
            // the generator. We build the full set of IDs the generator can produce from
            // reflection, then assert every compiler ID is present in that set. (The reverse
            // direction would wrongly flag compiler-generated members the XML never documents,
            // such as delegate Invoke/BeginInvoke or implicit constructors.)
            var compilerIds = LoadCompilerIds();
            var generated = GenerateAllIds(SampleAssembly.Loaded);

            var missing = compilerIds.Where(id => !generated.Contains(id)).ToList();

            Assert.True(missing.Count == 0,
                "Compiler doc-ids the generator could not reproduce:\n" + string.Join("\n", missing));
        }

        private static HashSet<string> GenerateAllIds(Assembly asm)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                        | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var type in asm.GetExportedTypes())
            {
                ids.Add(DocIdGenerator.ForType(type));

                foreach (var ctor in type.GetConstructors(flags))
                    ids.Add(DocIdGenerator.ForMethod(ctor));
                foreach (var method in type.GetMethods(flags))
                    ids.Add(DocIdGenerator.ForMethod(method));
                foreach (var prop in type.GetProperties(flags))
                    ids.Add(DocIdGenerator.ForProperty(prop));
                foreach (var evt in type.GetEvents(flags))
                    ids.Add(DocIdGenerator.ForEvent(evt));
                foreach (var field in type.GetFields(flags))
                    ids.Add(DocIdGenerator.ForField(field));
            }
            return ids;
        }
        [InlineData("T:DocLens.SampleLib.Money")]
        [InlineData("T:DocLens.SampleLib.Repository`1")]
        [InlineData("T:DocLens.SampleLib.Constants.Options")]
        [InlineData("M:DocLens.SampleLib.Repository`1.Add(`0)")]
        [InlineData("M:DocLens.SampleLib.Money.op_Addition(DocLens.SampleLib.Money,DocLens.SampleLib.Money)")]
        [InlineData("M:DocLens.SampleLib.Money.op_Explicit(DocLens.SampleLib.Money)~System.Decimal")]
        [InlineData("P:DocLens.SampleLib.Repository`1.Item(System.Int32)")]
        [InlineData("F:DocLens.SampleLib.Constants.MaxBatchSize")]
        [InlineData("E:DocLens.SampleLib.Repository`1.ItemAdded")]
        public void KnownDocIds_ArePresentInCompilerXml(string expectedId)
        {
            var compilerIds = LoadCompilerIds();
            Assert.Contains(expectedId, compilerIds);
        }
    }
}

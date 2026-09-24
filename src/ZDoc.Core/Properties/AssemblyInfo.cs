using System.Runtime.CompilerServices;

// Expose internal helpers (e.g. XmlDocParser.ShortenCref, ApiExtractor.Slugify) to the
// test project so they can be unit-tested directly without widening the public API.
[assembly: InternalsVisibleTo("ZDoc.Tests")]

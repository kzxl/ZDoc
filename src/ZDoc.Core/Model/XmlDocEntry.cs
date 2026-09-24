using System.Collections.Generic;

namespace ZDoc.Core.Model
{
    /// <summary>
    /// Structured documentation parsed from an XML doc comment block
    /// (<c>&lt;summary&gt;</c>, <c>&lt;param&gt;</c>, <c>&lt;returns&gt;</c>, etc.).
    /// </summary>
    public sealed class XmlDocEntry
    {
        /// <summary>The <c>&lt;summary&gt;</c> text (plain, inline tags flattened).</summary>
        public string? Summary { get; set; }

        /// <summary>The <c>&lt;remarks&gt;</c> text, if present.</summary>
        public string? Remarks { get; set; }

        /// <summary>The <c>&lt;returns&gt;</c> text, if present.</summary>
        public string? Returns { get; set; }

        /// <summary>Example code from <c>&lt;example&gt;</c>/<c>&lt;code&gt;</c>, if present.</summary>
        public string? Example { get; set; }

        /// <summary>Map of parameter name to its <c>&lt;param&gt;</c> description.</summary>
        public Dictionary<string, string> Parameters { get; } = new Dictionary<string, string>();

        /// <summary>Map of type-parameter name to its <c>&lt;typeparam&gt;</c> description.</summary>
        public Dictionary<string, string> TypeParameters { get; } = new Dictionary<string, string>();

        /// <summary>Documented exceptions: type reference to description.</summary>
        public List<ExceptionDoc> Exceptions { get; } = new List<ExceptionDoc>();

        /// <summary>True when no documentation was found for the member.</summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Summary)
            && string.IsNullOrWhiteSpace(Remarks)
            && string.IsNullOrWhiteSpace(Returns)
            && string.IsNullOrWhiteSpace(Example)
            && Parameters.Count == 0
            && TypeParameters.Count == 0
            && Exceptions.Count == 0;
    }

    /// <summary>A documented exception (from an <c>&lt;exception&gt;</c> tag).</summary>
    public sealed class ExceptionDoc
    {
        /// <summary>Creates an exception doc entry.</summary>
        public ExceptionDoc(string typeName, string description)
        {
            TypeName = typeName;
            Description = description;
        }

        /// <summary>Display name of the exception type.</summary>
        public string TypeName { get; }

        /// <summary>When the exception is thrown.</summary>
        public string Description { get; }
    }
}

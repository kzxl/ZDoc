using System.Collections.Generic;

namespace ZDoc.Core.Model
{
    /// <summary>Kind of a documented type.</summary>
    public enum TypeKind
    {
        /// <summary>A reference type declared with <c>class</c>.</summary>
        Class,
        /// <summary>A value type declared with <c>struct</c>.</summary>
        Struct,
        /// <summary>An <c>interface</c>.</summary>
        Interface,
        /// <summary>An <c>enum</c>.</summary>
        Enum,
        /// <summary>A <c>delegate</c>.</summary>
        Delegate
    }

    /// <summary>Kind of a documented member.</summary>
    public enum MemberKind
    {
        /// <summary>A constructor.</summary>
        Constructor,
        /// <summary>A method.</summary>
        Method,
        /// <summary>A property or indexer.</summary>
        Property,
        /// <summary>A field.</summary>
        Field,
        /// <summary>An event.</summary>
        Event,
        /// <summary>An enum member (named constant).</summary>
        EnumField
    }

    /// <summary>The root of an extracted API documentation model for one assembly.</summary>
    public sealed class ApiDocument
    {
        /// <summary>Display title (assembly name unless overridden).</summary>
        public string Title { get; set; } = "API Reference";

        /// <summary>Simple assembly name, e.g. <c>WinFlow</c>.</summary>
        public string AssemblyName { get; set; } = "";

        /// <summary>Assembly version string.</summary>
        public string AssemblyVersion { get; set; } = "";

        /// <summary>Optional free-text introduction (e.g. from a README), rendered as-is.</summary>
        public string? Description { get; set; }

        /// <summary>Namespaces in the assembly, ordered by name.</summary>
        public List<ApiNamespace> Namespaces { get; } = new List<ApiNamespace>();
    }

    /// <summary>A namespace grouping a set of public types.</summary>
    public sealed class ApiNamespace
    {
        /// <summary>Creates a namespace node.</summary>
        public ApiNamespace(string name) => Name = name;

        /// <summary>Fully qualified namespace name.</summary>
        public string Name { get; }

        /// <summary>Public types declared in this namespace.</summary>
        public List<ApiType> Types { get; } = new List<ApiType>();
    }

    /// <summary>A documented type (class, struct, interface, enum, or delegate).</summary>
    public sealed class ApiType
    {
        /// <summary>Short name including generic arity markers, e.g. <c>AsyncDebouncer&lt;TInput, TResult&gt;</c>.</summary>
        public string Name { get; set; } = "";

        /// <summary>Fully qualified name including namespace.</summary>
        public string FullName { get; set; } = "";

        /// <summary>Stable slug used for anchors/URLs (derived from the doc id).</summary>
        public string Slug { get; set; } = "";

        /// <summary>Owning namespace name.</summary>
        public string Namespace { get; set; } = "";

        /// <summary>The type's kind.</summary>
        public TypeKind Kind { get; set; }

        /// <summary>C#-style declaration line, e.g. <c>public sealed class Foo : IBar</c>.</summary>
        public string Declaration { get; set; } = "";

        /// <summary>Whether the type is static.</summary>
        public bool IsStatic { get; set; }

        /// <summary>Documentation pulled from the XML doc file for this type.</summary>
        public XmlDocEntry Docs { get; set; } = new XmlDocEntry();

        /// <summary>Documented members, grouped/ordered for display.</summary>
        public List<ApiMember> Members { get; } = new List<ApiMember>();
    }

    /// <summary>A documented member of a type.</summary>
    public sealed class ApiMember
    {
        /// <summary>Display name, e.g. <c>Invoke(string)</c> or <c>Count</c>.</summary>
        public string Name { get; set; } = "";

        /// <summary>Stable slug used for anchors.</summary>
        public string Slug { get; set; } = "";

        /// <summary>The member's kind.</summary>
        public MemberKind Kind { get; set; }

        /// <summary>C#-style signature line.</summary>
        public string Signature { get; set; } = "";

        /// <summary>Whether the member is static.</summary>
        public bool IsStatic { get; set; }

        /// <summary>Parameters (empty for fields/properties/events).</summary>
        public List<ApiParameter> Parameters { get; } = new List<ApiParameter>();

        /// <summary>Documentation pulled from the XML doc file for this member.</summary>
        public XmlDocEntry Docs { get; set; } = new XmlDocEntry();
    }

    /// <summary>A method/constructor parameter.</summary>
    public sealed class ApiParameter
    {
        /// <summary>Parameter name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Display type name.</summary>
        public string TypeName { get; set; } = "";

        /// <summary>Documentation text (from <c>&lt;param&gt;</c>), if any.</summary>
        public string? Description { get; set; }
    }
}

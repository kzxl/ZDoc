using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ZDoc.Core.Model;
using ZDoc.Core.Xml;

namespace ZDoc.Core.Reflection
{
    /// <summary>
    /// Options controlling what <see cref="ApiExtractor"/> includes.
    /// </summary>
    public sealed class ExtractionOptions
    {
        /// <summary>Include protected members of non-sealed types. Default true.</summary>
        public bool IncludeProtected { get; set; } = true;

        /// <summary>Optional free-text description shown on the landing page.</summary>
        public string? Description { get; set; }

        /// <summary>Optional title override (defaults to the assembly name).</summary>
        public string? Title { get; set; }

        /// <summary>
        /// Additional directories to search for dependency assemblies (e.g. third-party DLLs
        /// not sitting next to the target). Framework assemblies are discovered automatically.
        /// </summary>
        public List<string> AdditionalSearchDirectories { get; } = new List<string>();
    }

    /// <summary>
    /// Extracts a documentation model from a .NET assembly using
    /// <see cref="System.Reflection.MetadataLoadContext"/>, so the assembly is inspected
    /// without executing any of its code. The matching XML documentation file (if present)
    /// is bound to each type and member.
    /// </summary>
    public sealed class ApiExtractor
    {
        private readonly ExtractionOptions _options;

        /// <summary>Creates an extractor with the given options (or defaults).</summary>
        public ApiExtractor(ExtractionOptions? options = null)
        {
            _options = options ?? new ExtractionOptions();
        }

        /// <summary>
        /// Loads <paramref name="assemblyPath"/> and produces an <see cref="ApiDocument"/>.
        /// The XML doc file is auto-discovered next to the assembly unless
        /// <paramref name="xmlDocPath"/> is supplied.
        /// </summary>
        public ApiDocument Extract(string assemblyPath, string? xmlDocPath = null)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException("Assembly not found.", assemblyPath);

            xmlDocPath ??= Path.ChangeExtension(assemblyPath, ".xml");
            var docs = XmlDocParser.Load(xmlDocPath);

            var resolver = new PathAssemblyResolver(BuildResolverPaths(assemblyPath, _options.AdditionalSearchDirectories));
            using var mlc = new MetadataLoadContext(resolver);

            Assembly assembly = mlc.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
            var asmName = assembly.GetName();

            var document = new ApiDocument
            {
                AssemblyName = asmName.Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
                AssemblyVersion = asmName.Version?.ToString() ?? "",
                Description = _options.Description,
            };
            document.Title = _options.Title ?? document.AssemblyName + " API";

            var publicTypes = SafeGetTypes(assembly)
                .Where(IsDocumentable)
                .OrderBy(t => t.Namespace, StringComparer.Ordinal)
                .ThenBy(t => t.Name, StringComparer.Ordinal);

            var byNamespace = new Dictionary<string, ApiNamespace>(StringComparer.Ordinal);

            foreach (var type in publicTypes)
            {
                string ns = type.Namespace ?? "(global)";
                if (!byNamespace.TryGetValue(ns, out var nsNode))
                {
                    nsNode = new ApiNamespace(ns);
                    byNamespace[ns] = nsNode;
                }
                nsNode.Types.Add(BuildType(type, docs));
            }

            document.Namespaces.AddRange(byNamespace.Values.OrderBy(n => n.Name, StringComparer.Ordinal));
            return document;
        }

        private ApiType BuildType(Type type, XmlDocParser docs)
        {
            var apiType = new ApiType
            {
                Name = SignatureBuilder.TypeDisplayName(type),
                FullName = (type.Namespace != null ? type.Namespace + "." : "") + SignatureBuilder.TypeDisplayName(type),
                Namespace = type.Namespace ?? "(global)",
                Kind = ClassifyType(type),
                IsStatic = type.IsAbstract && type.IsSealed && !type.IsInterface,
                Declaration = SignatureBuilder.TypeDeclaration(type),
                Docs = docs.Get(DocIdGenerator.ForType(type)),
            };
            apiType.Slug = Slugify(DocIdGenerator.ForType(type));

            // Enum members.
            if (apiType.Kind == TypeKind.Enum)
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
                             .Where(f => f.IsLiteral))
                {
                    apiType.Members.Add(new ApiMember
                    {
                        Name = field.Name,
                        Slug = Slugify(DocIdGenerator.ForField(field)),
                        Kind = MemberKind.EnumField,
                        Signature = field.Name,
                        IsStatic = true,
                        Docs = docs.Get(DocIdGenerator.ForField(field)),
                    });
                }
                return apiType;
            }

            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            if (_options.IncludeProtected) flags |= BindingFlags.NonPublic;

            // Constructors.
            foreach (var ctor in type.GetConstructors(flags).Where(ShouldInclude))
            {
                apiType.Members.Add(BuildMethodMember(ctor, MemberKind.Constructor, docs));
            }

            // Properties.
            foreach (var prop in type.GetProperties(flags).Where(ShouldIncludeProperty))
            {
                apiType.Members.Add(new ApiMember
                {
                    Name = prop.GetIndexParameters().Length > 0 ? "this[]" : prop.Name,
                    Slug = Slugify(DocIdGenerator.ForProperty(prop)),
                    Kind = MemberKind.Property,
                    Signature = SignatureBuilder.PropertySignature(prop),
                    IsStatic = (prop.GetMethod ?? prop.SetMethod)?.IsStatic ?? false,
                    Docs = docs.Get(DocIdGenerator.ForProperty(prop)),
                });
            }

            // Methods (excluding accessors/operators-as-special-names handled below).
            foreach (var method in type.GetMethods(flags).Where(ShouldIncludeMethod))
            {
                apiType.Members.Add(BuildMethodMember(method, MemberKind.Method, docs));
            }

            // Events.
            foreach (var evt in type.GetEvents(flags).Where(e => ShouldIncludeEvent(e)))
            {
                apiType.Members.Add(new ApiMember
                {
                    Name = evt.Name,
                    Slug = Slugify(DocIdGenerator.ForEvent(evt)),
                    Kind = MemberKind.Event,
                    Signature = SignatureBuilder.EventSignature(evt),
                    IsStatic = (evt.AddMethod ?? evt.RemoveMethod)?.IsStatic ?? false,
                    Docs = docs.Get(DocIdGenerator.ForEvent(evt)),
                });
            }

            // Public/protected constant and static/instance fields (rare in public APIs).
            foreach (var field in type.GetFields(flags).Where(ShouldIncludeField))
            {
                apiType.Members.Add(new ApiMember
                {
                    Name = field.Name,
                    Slug = Slugify(DocIdGenerator.ForField(field)),
                    Kind = MemberKind.Field,
                    Signature = SignatureBuilder.FieldSignature(field),
                    IsStatic = field.IsStatic,
                    Docs = docs.Get(DocIdGenerator.ForField(field)),
                });
            }

            return apiType;
        }

        private ApiMember BuildMethodMember(MethodBase method, MemberKind kind, XmlDocParser docs)
        {
            var member = new ApiMember
            {
                Name = SignatureBuilder.MethodDisplayName(method),
                Slug = Slugify(DocIdGenerator.ForMethod(method)),
                Kind = kind,
                Signature = SignatureBuilder.MethodSignature(method),
                IsStatic = method.IsStatic,
                Docs = docs.Get(DocIdGenerator.ForMethod(method)),
            };

            foreach (var p in method.GetParameters())
            {
                member.Parameters.Add(new ApiParameter
                {
                    Name = p.Name ?? "",
                    TypeName = TypeNameFormatter.Format(p.ParameterType),
                    Description = member.Docs.Parameters.TryGetValue(p.Name ?? "", out var d) ? d : null,
                });
            }

            return member;
        }

        // ---- Filters ----

        private bool IsDocumentable(Type type)
        {
            // Top-level public, or nested public/protected.
            if (type.IsNested)
            {
                if (!(type.IsNestedPublic || (_options.IncludeProtected && type.IsNestedFamily))) return false;
            }
            else if (!type.IsPublic)
            {
                return false;
            }

            // Skip compiler-generated types.
            if (type.Name.IndexOf('<') >= 0) return false;
            if (HasAttribute(type, "System.Runtime.CompilerServices.CompilerGeneratedAttribute")) return false;

            return true;
        }

        private bool ShouldInclude(MethodBase method)
        {
            if (method.IsPublic) return true;
            if (_options.IncludeProtected && (method.IsFamily || method.IsFamilyOrAssembly)) return true;
            return false;
        }

        private bool ShouldIncludeMethod(MethodInfo method)
        {
            // Operators are special-name but should be documented.
            if (method.IsSpecialName)
            {
                if (!method.Name.StartsWith("op_", StringComparison.Ordinal)) return false;
            }
            if (!ShouldInclude(method)) return false;
            return true;
        }

        private bool ShouldIncludeProperty(PropertyInfo prop)
        {
            var accessor = prop.GetMethod ?? prop.SetMethod;
            return accessor != null && ShouldInclude(accessor);
        }

        private bool ShouldIncludeEvent(EventInfo evt)
        {
            var accessor = evt.AddMethod ?? evt.RemoveMethod;
            return accessor != null && ShouldInclude(accessor);
        }

        private bool ShouldIncludeField(FieldInfo field)
        {
            if (field.IsSpecialName) return false;
            if (field.Name.IndexOf('<') >= 0) return false; // backing fields
            if (field.IsPublic) return true;
            if (_options.IncludeProtected && (field.IsFamily || field.IsFamilyOrAssembly)) return true;
            return false;
        }

        private static TypeKind ClassifyType(Type type)
        {
            if (type.IsEnum) return TypeKind.Enum;
            if (type.IsInterface) return TypeKind.Interface;
            if (IsDelegate(type)) return TypeKind.Delegate;
            if (type.IsValueType) return TypeKind.Struct;
            return TypeKind.Class;
        }

        private static bool IsDelegate(Type type)
        {
            for (Type? b = type.BaseType; b != null; b = b.BaseType)
            {
                if (b.FullName == "System.MulticastDelegate" || b.FullName == "System.Delegate")
                    return true;
            }
            return false;
        }

        private static bool HasAttribute(Type type, string attributeFullName)
        {
            return type.GetCustomAttributesData()
                .Any(a => a.AttributeType.FullName == attributeFullName);
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null)!;
            }
        }

        /// <summary>Turns a doc-id into a stable, URL-safe anchor slug.</summary>
        internal static string Slugify(string docId)
        {
            var sb = new StringBuilder(docId.Length);
            foreach (char c in docId)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else if (c == '.' || c == '_') sb.Append('-');
                else if (c == '`') sb.Append('-');
                else if (c == '(' || c == ')' || c == ',' || c == '{' || c == '}' || c == '@' || c == ':')
                    sb.Append('-');
                // other characters dropped
            }
            // Collapse repeated dashes.
            var slug = sb.ToString();
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            return slug.Trim('-');
        }

        private static string[] BuildResolverPaths(string assemblyPath)
        {
            return BuildResolverPaths(assemblyPath, new List<string>());
        }

        private static string[] BuildResolverPaths(string assemblyPath, List<string> additionalDirs)
        {
            var paths = new List<string>();

            // The runtime assemblies of the SDK running this tool (so System.* resolves).
            string tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";
            foreach (var p in tpa.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) paths.Add(p);
            }

            // Installed shared frameworks (WindowsDesktop -> System.Windows.Forms/PresentationCore,
            // AspNetCore, etc.). The core runtime lives at <root>/shared/Microsoft.NETCore.App/<ver>;
            // sibling shared frameworks sit next to it.
            foreach (var dll in EnumerateSharedFrameworkAssemblies())
            {
                paths.Add(dll);
            }

            // Caller-provided dependency directories.
            foreach (var dir in additionalDirs)
            {
                if (Directory.Exists(dir))
                {
                    foreach (var dll in Directory.GetFiles(dir, "*.dll")) paths.Add(dll);
                }
            }

            // The target assembly and any DLLs sitting beside it.
            string? targetDir = Path.GetDirectoryName(Path.GetFullPath(assemblyPath));
            if (targetDir != null && Directory.Exists(targetDir))
            {
                foreach (var dll in Directory.GetFiles(targetDir, "*.dll")) paths.Add(dll);
            }
            paths.Add(Path.GetFullPath(assemblyPath));

            // Distinct by file name (keep the first occurrence) to avoid duplicate-assembly errors,
            // while still preferring framework/runtime copies discovered earlier.
            var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (var p in paths)
            {
                string fileName = Path.GetFileName(p);
                if (seenFileNames.Add(fileName)) result.Add(p);
            }
            return result.ToArray();
        }

        private static IEnumerable<string> EnumerateSharedFrameworkAssemblies()
        {
            // Locate the dotnet "shared" root from the currently running core assembly.
            string coreAsmDir = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? "";
            if (coreAsmDir.Length == 0) yield break;

            // coreAsmDir = <root>/shared/Microsoft.NETCore.App/<version>
            DirectoryInfo? versionDir = new DirectoryInfo(coreAsmDir);
            DirectoryInfo? netcoreAppDir = versionDir.Parent;          // Microsoft.NETCore.App
            DirectoryInfo? sharedRoot = netcoreAppDir?.Parent;          // shared
            if (sharedRoot == null || !sharedRoot.Exists) yield break;

            foreach (var framework in sharedRoot.GetDirectories())
            {
                // Pick the highest installed version directory for each shared framework.
                var newestVersion = framework.GetDirectories()
                    .OrderByDescending(d => ParseVersion(d.Name))
                    .FirstOrDefault();
                if (newestVersion == null) continue;

                foreach (var dll in newestVersion.GetFiles("*.dll"))
                {
                    yield return dll.FullName;
                }
            }
        }

        private static Version ParseVersion(string name)
        {
            // Strip any prerelease suffix, e.g. "8.0.3-preview".
            int dash = name.IndexOf('-');
            string core = dash >= 0 ? name.Substring(0, dash) : name;
            return Version.TryParse(core, out var v) ? v : new Version(0, 0);
        }
    }
}

using System;
using System.Reflection;
using System.Text;

namespace ZDoc.Core.Reflection
{
    /// <summary>
    /// Produces XML documentation comment IDs (the strings that appear as
    /// <c>&lt;member name="..."&gt;</c> in a compiler-generated XML doc file) from reflection
    /// metadata. The format follows the C# language specification (ECMA-334, "Processing the
    /// documentation file"), so the IDs match exactly what the compiler emits.
    /// <para>
    /// Examples:
    /// <list type="bullet">
    ///   <item><c>T:WinFlow.UiDispatcher</c></item>
    ///   <item><c>M:WinFlow.UiDispatcher.Post(System.Action)</c></item>
    ///   <item><c>M:WinFlow.AsyncDebouncer`2.Invoke(`0)</c></item>
    /// </list>
    /// </para>
    /// </summary>
    public static class DocIdGenerator
    {
        /// <summary>Returns the documentation ID for a type, prefixed with <c>T:</c>.</summary>
        public static string ForType(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return "T:" + GetTypeNameForId(type, topLevel: true);
        }

        /// <summary>Returns the documentation ID for a field, prefixed with <c>F:</c>.</summary>
        public static string ForField(FieldInfo field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            return "F:" + GetDeclaringPrefix(field.DeclaringType!) + field.Name;
        }

        /// <summary>Returns the documentation ID for an event, prefixed with <c>E:</c>.</summary>
        public static string ForEvent(EventInfo evt)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            return "E:" + GetDeclaringPrefix(evt.DeclaringType!) + evt.Name;
        }

        /// <summary>Returns the documentation ID for a property/indexer, prefixed with <c>P:</c>.</summary>
        public static string ForProperty(PropertyInfo property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));

            var sb = new StringBuilder("P:");
            sb.Append(GetDeclaringPrefix(property.DeclaringType!));
            sb.Append(property.Name);

            var indexParams = property.GetIndexParameters();
            if (indexParams.Length > 0)
            {
                AppendParameterList(sb, indexParams, property.DeclaringType!);
            }

            return sb.ToString();
        }

        /// <summary>Returns the documentation ID for a method/constructor, prefixed with <c>M:</c>.</summary>
        public static string ForMethod(MethodBase method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            var sb = new StringBuilder("M:");
            sb.Append(GetDeclaringPrefix(method.DeclaringType!));

            // Constructor name is "#ctor" (or "#cctor" for static constructors).
            if (method.IsConstructor)
            {
                sb.Append(method.IsStatic ? "#cctor" : "#ctor");
            }
            else
            {
                sb.Append(method.Name);
                if (method.IsGenericMethod)
                {
                    sb.Append("``").Append(method.GetGenericArguments().Length);
                }
            }

            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                AppendParameterList(sb, parameters, method.DeclaringType!);
            }

            // Conversion operators encode their return type after a '~'.
            if (!method.IsConstructor && method is MethodInfo mi &&
                (method.Name == "op_Implicit" || method.Name == "op_Explicit"))
            {
                sb.Append('~').Append(GetTypeNameForId(mi.ReturnType, topLevel: false));
            }

            return sb.ToString();
        }

        private static void AppendParameterList(StringBuilder sb, ParameterInfo[] parameters, Type declaringType)
        {
            sb.Append('(');
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(GetTypeNameForId(parameters[i].ParameterType, topLevel: false));
            }
            sb.Append(')');
        }

        private static string GetDeclaringPrefix(Type declaringType)
        {
            // For members, the declaring type uses its generic-arity (`n) form, not {T} args.
            return GetTypeNameForId(declaringType, topLevel: true) + ".";
        }

        /// <summary>
        /// Builds the doc-id form of a type name. <paramref name="topLevel"/> distinguishes a
        /// type being named for its own ID / as a member's declaring type (uses <c>`n</c> arity)
        /// from a type used as a parameter type (uses <c>{...}</c> generic arguments).
        /// </summary>
        private static string GetTypeNameForId(Type type, bool topLevel)
        {
            // By-ref: encode element then '@'.
            if (type.IsByRef)
            {
                return GetTypeNameForId(type.GetElementType()!, topLevel: false) + "@";
            }

            // Pointer.
            if (type.IsPointer)
            {
                return GetTypeNameForId(type.GetElementType()!, topLevel: false) + "*";
            }

            // Arrays (single- and multi-dimensional).
            if (type.IsArray)
            {
                string element = GetTypeNameForId(type.GetElementType()!, topLevel: false);
                int rank = type.GetArrayRank();
                if (rank == 1)
                {
                    return element + "[]";
                }

                var dims = new StringBuilder("[");
                for (int i = 0; i < rank; i++)
                {
                    if (i > 0) dims.Append(',');
                    dims.Append("0:");
                }
                dims.Append(']');
                return element + dims;
            }

            // Generic parameter references.
            if (type.IsGenericParameter)
            {
                // Method generic parameters use ``index; type generic parameters use `index.
                return (type.DeclaringMethod != null ? "``" : "`") + type.GenericParameterPosition;
            }

            return BuildQualifiedName(type, topLevel);
        }

        private static string BuildQualifiedName(Type type, bool topLevel)
        {
            var sb = new StringBuilder();

            // Namespace.
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.Append(type.Namespace).Append('.');
            }

            // Walk the nesting chain from outermost to innermost.
            var chain = new System.Collections.Generic.List<Type>();
            for (Type? t = type; t != null; t = t.IsNested ? t.DeclaringType : null)
            {
                chain.Add(t);
            }
            chain.Reverse();

            // Generic arguments are shared across the nesting chain in declaration order.
            Type[] genericArgs = type.IsGenericType ? type.GetGenericArguments() : Array.Empty<Type>();
            int consumed = 0;

            for (int ci = 0; ci < chain.Count; ci++)
            {
                Type current = chain[ci];
                if (ci > 0) sb.Append('.');

                string rawName = current.Name;
                int backtick = rawName.IndexOf('`');
                int arityHere = 0;
                if (backtick >= 0)
                {
                    arityHere = int.Parse(rawName.Substring(backtick + 1));
                    rawName = rawName.Substring(0, backtick);
                }

                sb.Append(rawName);

                if (arityHere > 0)
                {
                    if (topLevel)
                    {
                        // Declaring/own form: keep arity marker `n.
                        sb.Append('`').Append(arityHere);
                    }
                    else
                    {
                        // Parameter form: emit the concrete generic arguments in {a,b,...}.
                        sb.Append('{');
                        for (int g = 0; g < arityHere; g++)
                        {
                            if (g > 0) sb.Append(',');
                            sb.Append(GetTypeNameForId(genericArgs[consumed + g], topLevel: false));
                        }
                        sb.Append('}');
                    }
                }
                consumed += arityHere;
            }

            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ZeroDoc.Core.Reflection
{
    /// <summary>
    /// Renders reflection <see cref="Type"/> instances as readable C#-style names, using
    /// language aliases (e.g. <c>int</c> instead of <c>System.Int32</c>), angle-bracket
    /// generics, nullable shorthand, arrays, by-ref markers, and tuple syntax where possible.
    /// </summary>
    public static class TypeNameFormatter
    {
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["System.Void"] = "void",
            ["System.Object"] = "object",
            ["System.String"] = "string",
            ["System.Boolean"] = "bool",
            ["System.Byte"] = "byte",
            ["System.SByte"] = "sbyte",
            ["System.Char"] = "char",
            ["System.Int16"] = "short",
            ["System.UInt16"] = "ushort",
            ["System.Int32"] = "int",
            ["System.UInt32"] = "uint",
            ["System.Int64"] = "long",
            ["System.UInt64"] = "ulong",
            ["System.Single"] = "float",
            ["System.Double"] = "double",
            ["System.Decimal"] = "decimal",
        };

        /// <summary>
        /// Returns a short, C#-style display name for <paramref name="type"/>
        /// (no namespace for the outer type, e.g. <c>Task&lt;IReadOnlyList&lt;string&gt;&gt;</c>).
        /// </summary>
        public static string Format(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            if (type.IsByRef)
            {
                return Format(type.GetElementType()!);
            }

            if (type.IsArray)
            {
                int rank = type.GetArrayRank();
                string commas = rank > 1 ? new string(',', rank - 1) : "";
                return Format(type.GetElementType()!) + "[" + commas + "]";
            }

            if (type.IsPointer)
            {
                return Format(type.GetElementType()!) + "*";
            }

            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            // Nullable<T> -> T?
            Type? underlying = GetNullableUnderlying(type);
            if (underlying != null)
            {
                return Format(underlying) + "?";
            }

            string fullName = (type.Namespace != null ? type.Namespace + "." : "") + StripArity(type.Name);
            if (!type.IsGenericType)
            {
                if (Aliases.TryGetValue(fullName, out var alias)) return alias;
                return StripArity(type.Name);
            }

            // Generic type: Name<arg, arg, ...>
            var sb = new StringBuilder();
            sb.Append(StripArity(type.Name));
            sb.Append('<');
            var args = type.GetGenericArguments();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Format(args[i]));
            }
            sb.Append('>');
            return sb.ToString();
        }

        /// <summary>
        /// Returns the C# parameter modifier keyword for a parameter, including
        /// <c>ref</c>/<c>out</c>/<c>in</c>/<c>params</c>, or an empty string.
        /// </summary>
        public static string FormatParameterPrefix(ParameterInfo parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));

            if (parameter.ParameterType.IsByRef)
            {
                if (parameter.IsOut) return "out ";
                if (parameter.IsIn) return "in ";
                return "ref ";
            }

            if (parameter.GetCustomAttributesData()
                .Any(a => a.AttributeType.FullName == "System.ParamArrayAttribute"))
            {
                return "params ";
            }

            return "";
        }

        private static Type? GetNullableUnderlying(Type type)
        {
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var def = type.GetGenericTypeDefinition();
                if (def.FullName == "System.Nullable`1")
                {
                    return type.GetGenericArguments()[0];
                }
            }
            return null;
        }

        private static string StripArity(string name)
        {
            int backtick = name.IndexOf('`');
            return backtick >= 0 ? name.Substring(0, backtick) : name;
        }
    }
}

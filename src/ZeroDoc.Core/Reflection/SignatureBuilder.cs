using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ZeroDoc.Core.Reflection
{
    /// <summary>
    /// Builds C#-style declaration and signature strings for types and members from reflection
    /// metadata (suitable for display, not compilation).
    /// </summary>
    public static class SignatureBuilder
    {
        /// <summary>Short display name including generic parameters, e.g. <c>AsyncDebouncer&lt;TInput, TResult&gt;</c>.</summary>
        public static string TypeDisplayName(Type type)
        {
            string name = StripArity(type.Name);
            if (!type.IsGenericType) return name;

            var args = type.GetGenericArguments();
            return name + "<" + string.Join(", ", args.Select(a => a.Name)) + ">";
        }

        /// <summary>Builds a type declaration line, e.g. <c>public sealed class Foo : Bar, IBaz</c>.</summary>
        public static string TypeDeclaration(Type type)
        {
            var sb = new StringBuilder();
            sb.Append(Accessibility(type));

            if (type.IsEnum)
            {
                sb.Append(" enum ").Append(TypeDisplayName(type));
                var underlying = type.GetEnumUnderlyingType();
                if (underlying.FullName != "System.Int32")
                    sb.Append(" : ").Append(TypeNameFormatter.Format(underlying));
                return sb.ToString();
            }

            if (type.IsInterface)
            {
                sb.Append(" interface ").Append(TypeDisplayName(type));
            }
            else if (IsDelegate(type))
            {
                var invoke = type.GetMethod("Invoke");
                string ret = invoke != null ? TypeNameFormatter.Format(invoke.ReturnType) : "void";
                string pars = invoke != null ? FormatParameters(invoke.GetParameters()) : "";
                sb.Append(" delegate ").Append(ret).Append(' ').Append(TypeDisplayName(type)).Append('(').Append(pars).Append(')');
                return sb.ToString();
            }
            else if (type.IsValueType)
            {
                sb.Append(" struct ").Append(TypeDisplayName(type));
            }
            else
            {
                if (type.IsAbstract && type.IsSealed) sb.Append(" static");
                else if (type.IsAbstract) sb.Append(" abstract");
                else if (type.IsSealed) sb.Append(" sealed");
                sb.Append(" class ").Append(TypeDisplayName(type));
            }

            // Base type + interfaces.
            var bases = new List<string>();
            if (!type.IsValueType && !type.IsInterface && type.BaseType != null &&
                type.BaseType.FullName != "System.Object")
            {
                bases.Add(TypeNameFormatter.Format(type.BaseType));
            }
            foreach (var iface in GetDirectInterfaces(type))
            {
                bases.Add(TypeNameFormatter.Format(iface));
            }
            if (bases.Count > 0)
            {
                sb.Append(" : ").Append(string.Join(", ", bases));
            }

            return sb.ToString();
        }

        /// <summary>Display name for a method/constructor, e.g. <c>Invoke(string)</c>.</summary>
        public static string MethodDisplayName(MethodBase method)
        {
            string name;
            if (method.IsConstructor)
            {
                name = StripArity(method.DeclaringType?.Name ?? "ctor");
            }
            else
            {
                name = method.Name;
                if (method.IsGenericMethod)
                {
                    name += "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">";
                }
            }

            string pars = string.Join(", ",
                method.GetParameters().Select(p => TypeNameFormatter.Format(p.ParameterType)));
            return name + "(" + pars + ")";
        }

        /// <summary>Full method/constructor signature line.</summary>
        public static string MethodSignature(MethodBase method)
        {
            var sb = new StringBuilder();
            sb.Append(Accessibility(method));
            sb.Append(Modifiers(method));

            if (method is MethodInfo mi)
            {
                sb.Append(' ').Append(TypeNameFormatter.Format(mi.ReturnType));
            }

            sb.Append(' ');
            if (method.IsConstructor)
            {
                sb.Append(StripArity(method.DeclaringType?.Name ?? "ctor"));
            }
            else
            {
                sb.Append(method.Name);
                if (method.IsGenericMethod)
                {
                    sb.Append('<').Append(string.Join(", ", method.GetGenericArguments().Select(a => a.Name))).Append('>');
                }
            }

            sb.Append('(').Append(FormatParameters(method.GetParameters())).Append(')');
            return sb.ToString();
        }

        /// <summary>Property/indexer signature, e.g. <c>public int Count { get; }</c>.</summary>
        public static string PropertySignature(PropertyInfo prop)
        {
            var getter = prop.GetMethod;
            var setter = prop.SetMethod;
            var primary = getter ?? setter!;

            var sb = new StringBuilder();
            sb.Append(Accessibility(primary));
            sb.Append(Modifiers(primary));
            sb.Append(' ').Append(TypeNameFormatter.Format(prop.PropertyType));
            sb.Append(' ');

            var indexParams = prop.GetIndexParameters();
            if (indexParams.Length > 0)
            {
                sb.Append("this[").Append(FormatParameters(indexParams)).Append(']');
            }
            else
            {
                sb.Append(prop.Name);
            }

            sb.Append(" { ");
            if (getter != null && IsVisible(getter)) sb.Append("get; ");
            if (setter != null && IsVisible(setter))
            {
                bool initOnly = setter.ReturnParameter?.GetRequiredCustomModifiers()
                    .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit") ?? false;
                sb.Append(initOnly ? "init; " : "set; ");
            }
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Event signature, e.g. <c>public event EventHandler Changed</c>.</summary>
        public static string EventSignature(EventInfo evt)
        {
            var accessor = evt.AddMethod ?? evt.RemoveMethod!;
            var sb = new StringBuilder();
            sb.Append(Accessibility(accessor));
            sb.Append(Modifiers(accessor));
            sb.Append(" event ");
            sb.Append(evt.EventHandlerType != null ? TypeNameFormatter.Format(evt.EventHandlerType) : "EventHandler");
            sb.Append(' ').Append(evt.Name);
            return sb.ToString();
        }

        /// <summary>Field/constant signature.</summary>
        public static string FieldSignature(FieldInfo field)
        {
            var sb = new StringBuilder();
            sb.Append(FieldAccessibility(field));
            if (field.IsLiteral) sb.Append(" const");
            else
            {
                if (field.IsStatic) sb.Append(" static");
                if (field.IsInitOnly) sb.Append(" readonly");
            }
            sb.Append(' ').Append(TypeNameFormatter.Format(field.FieldType));
            sb.Append(' ').Append(field.Name);
            return sb.ToString();
        }

        // ---- helpers ----

        private static string FormatParameters(ParameterInfo[] parameters)
        {
            return string.Join(", ", parameters.Select(p =>
            {
                string prefix = TypeNameFormatter.FormatParameterPrefix(p);
                string type = TypeNameFormatter.Format(p.ParameterType);
                string name = p.Name ?? "";
                string suffix = p.HasDefaultValue ? " = " + FormatDefault(p.RawDefaultValue) : "";
                return prefix + type + " " + name + suffix;
            }));
        }

        private static string FormatDefault(object? value)
        {
            if (value == null) return "null";
            if (value is string s) return "\"" + s + "\"";
            if (value is bool b) return b ? "true" : "false";
            if (value is char c) return "'" + c + "'";
            return value.ToString() ?? "null";
        }

        private static string Accessibility(Type type)
        {
            if (type.IsNested)
            {
                if (type.IsNestedPublic) return "public";
                if (type.IsNestedFamily) return "protected";
                if (type.IsNestedFamORAssem) return "protected internal";
                return "internal";
            }
            return type.IsPublic ? "public" : "internal";
        }

        private static string Accessibility(MethodBase method)
        {
            if (method.IsPublic) return "public";
            if (method.IsFamily) return "protected";
            if (method.IsFamilyOrAssembly) return "protected internal";
            if (method.IsFamilyAndAssembly) return "private protected";
            if (method.IsAssembly) return "internal";
            return "private";
        }

        private static string FieldAccessibility(FieldInfo field)
        {
            if (field.IsPublic) return "public";
            if (field.IsFamily) return "protected";
            if (field.IsFamilyOrAssembly) return "protected internal";
            if (field.IsAssembly) return "internal";
            return "private";
        }

        private static string Modifiers(MethodBase method)
        {
            var sb = new StringBuilder();
            if (method.IsStatic) sb.Append(" static");
            if (method is MethodInfo mi)
            {
                if (mi.IsAbstract) sb.Append(" abstract");
                else if (mi.IsVirtual && !mi.IsFinal && IsOverride(mi)) sb.Append(" override");
                else if (mi.IsVirtual && !mi.IsFinal) sb.Append(" virtual");
            }
            return sb.ToString();
        }

        private static bool IsOverride(MethodInfo method)
        {
            // GetBaseDefinition() is unsupported under MetadataLoadContext, so infer from
            // metadata: a virtual method that does NOT occupy a new slot reuses a base slot,
            // i.e. it overrides an inherited virtual method.
            return method.IsVirtual && (method.Attributes & MethodAttributes.NewSlot) == 0;
        }

        private static bool IsVisible(MethodInfo accessor)
        {
            return accessor.IsPublic || accessor.IsFamily || accessor.IsFamilyOrAssembly;
        }

        private static IEnumerable<Type> GetDirectInterfaces(Type type)
        {
            var all = type.GetInterfaces();
            // Only those not also implemented by the base type or by another listed interface.
            var inherited = new HashSet<Type>();
            if (type.BaseType != null)
            {
                foreach (var i in type.BaseType.GetInterfaces()) inherited.Add(i);
            }
            foreach (var i in all)
            {
                foreach (var sub in i.GetInterfaces()) inherited.Add(sub);
            }
            return all.Where(i => !inherited.Contains(i));
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

        private static string StripArity(string name)
        {
            int backtick = name.IndexOf('`');
            return backtick >= 0 ? name.Substring(0, backtick) : name;
        }
    }
}

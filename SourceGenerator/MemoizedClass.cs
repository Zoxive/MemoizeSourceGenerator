﻿using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace SourceGenerator
{
    internal static class MemoizedClass
    {
        public static string Generate(ScopedMemoizerCall scopedCall)
        {
            var fullInterfaceName = scopedCall.InterfaceType.ToDisplayString();

            var sb = new StringBuilder(@$"using Microsoft.Extensions.Caching.Memory;
using System;

namespace {scopedCall.Namespace}
{{
    public class {scopedCall.ClassName} : {fullInterfaceName}
    {{
");
            sb.AppendLine($"\t\tprivate readonly {fullInterfaceName} _impl;");
            sb.AppendLine($"\t\tprivate readonly IMemoryCache _cache;");
            sb.AppendLine($"\t\tpublic {scopedCall.ClassName}(IMemoryCache cache, {fullInterfaceName} impl)");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t_cache = cache;");
            sb.AppendLine("\t\t\t_impl = impl;");
            sb.AppendLine("\t\t}");

            sb.AppendLine();

            var methods= scopedCall.InterfaceType.GetMembers().OfType<IMethodSymbol>().ToList();
            foreach (var method in methods)
            {
                if (method.ReturnsVoid)
                {
                    // nothing to cache here.. skip
                    continue;
                }

                var returnType = method.ReturnType.ToDisplayString();
                var methodName = method.Name;

                sb.Append($"\t\tpublic {returnType} {methodName}(");

                var lastArg = method.Parameters.LastOrDefault();
                foreach (var arg in method.Parameters)
                {
                    sb.Append(arg.ToDisplayString());
                    sb.Append(" ");
                    sb.Append(arg.Name);

                    if (!ReferenceEquals(arg, lastArg))
                        sb.Append(", ");
                }
                sb.AppendLine(")");
                sb.AppendLine("\t\t{");

                sb.Append($"\t\t\tvar key = new ArgKey_{methodName}(");
                foreach (var arg in method.Parameters)
                {
                    sb.Append(arg.Name);
                    if (!ReferenceEquals(arg, lastArg))
                        sb.Append(", ");
                }
                sb.AppendLine(");");
                sb.AppendLine($"\t\t\tif (_cache.TryGetValue<{returnType}>(key, out var value))");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine("\t\t\t\treturn value;");
                sb.AppendLine("\t\t\t}");
                sb.AppendLine("\t\t\tvar entry = _cache.CreateEntry(key);");
                sb.Append($"\t\t\tvar result = _impl.{methodName}(");
                foreach (var arg in method.Parameters)
                {
                    sb.Append(arg.Name);
                    if (!ReferenceEquals(arg, lastArg))
                        sb.Append(", ");
                }
                sb.AppendLine(");");
                sb.AppendLine("\t\t\tentry.SetValue(result);");
                sb.AppendLine("\t\t\treturn result;");

                sb.AppendLine("\t\t}");
            }

            sb.AppendLine();

            foreach (var method in methods.Where(x => !x.ReturnsVoid))
            {
                GenerateArgStruct(method, sb);
            }

            sb.AppendLine("\t}");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void GenerateArgStruct(IMethodSymbol method, StringBuilder sb)
        {
            var methodName = method.Name;
            var lastArg = method.Parameters.LastOrDefault();


            sb.AppendLine($"\t\tinternal struct ArgKey_{methodName} : IEquatable<ArgKey_{methodName}>");
            sb.AppendLine("\t\t{");

            foreach (var arg in method.Parameters)
            {
                sb.AppendLine($"\t\t\tprivate readonly {arg.ToDisplayString()} _{arg.Name};");
            }

            sb.AppendLine();
            sb.Append($"\t\t\tinternal ArgKey_{methodName}(");
            foreach (var arg in method.Parameters)
            {
                sb.Append(arg.ToDisplayString());
                sb.Append(" ");
                sb.Append(arg.Name);
                if (!ReferenceEquals(arg, lastArg))
                    sb.Append(", ");
            }

            sb.AppendLine(")");
            sb.AppendLine("\t\t\t{");
            foreach (var arg in method.Parameters)
            {
                sb.AppendLine($"\t\t\t\t_{arg.Name} = {arg.Name};");
            }

            sb.AppendLine("\t\t\t}");

            sb.AppendLine();

            sb.AppendLine($"\t\t\tpublic bool Equals(ArgKey_{methodName} other)");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine("\t\t\t\tif (ReferenceEquals(null, other)) return false;");
            sb.AppendLine("\t\t\t\tif (ReferenceEquals(this, other)) return true;");

            sb.Append($"\t\t\t\treturn ");
            foreach (var arg in method.Parameters)
            {
                sb.Append($"_{arg.Name} == other._{arg.Name}");
                if (!ReferenceEquals(arg, lastArg))
                    sb.Append(" && ");
            }

            sb.AppendLine(";");

            sb.AppendLine("\t\t\t}");
            sb.AppendLine();

            sb.AppendLine($"\t\t\tpublic override bool Equals(object obj)");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine("\t\t\t\tif (ReferenceEquals(null, obj)) return false;");
            sb.AppendLine("\t\t\t\tif (ReferenceEquals(this, obj)) return true;");
            sb.AppendLine($"\t\t\t\treturn obj is ArgKey_{methodName} castedObj && Equals(castedObj);");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine();

            sb.AppendLine($"\t\t\tpublic override int GetHashCode()");
            sb.AppendLine("\t\t\t{");
            sb.Append("\t\t\t\treturn HashCode.Combine(");
            foreach (var arg in method.Parameters)
            {
                sb.Append($"_{arg.Name}");
                if (!ReferenceEquals(arg, lastArg))
                    sb.Append(", ");
            }

            sb.AppendLine(");");
            sb.AppendLine("\t\t\t}");

            sb.AppendLine("\t\t}");
        }
    }
}
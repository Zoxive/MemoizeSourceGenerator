using System.Linq;
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
using Microsoft.Extensions.Logging;
using System;
using SourceGenerator.Attribute;

namespace {scopedCall.Namespace}
{{
    public class {scopedCall.ClassName} : {fullInterfaceName}
    {{
");
            sb.AppendLine($"\t\tprivate readonly {fullInterfaceName} _impl;");
            sb.AppendLine($"\t\tprivate readonly IMemoryCache _cache;");
            sb.AppendLine($"\t\tprivate readonly ILogger<{scopedCall.ClassName}> _logger;");
            sb.AppendLine($"\t\tpublic {scopedCall.ClassName}(IMemoryCache cache, {fullInterfaceName} impl, ILogger<{scopedCall.ClassName}> logger)");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t_cache = cache;");
            sb.AppendLine("\t\t\t_impl = impl;");
            sb.AppendLine("\t\t\t_logger = logger;");
            sb.AppendLine("\t\t}");

            sb.AppendLine();

            var methods= scopedCall.Methods;
            foreach (var method in methods)
            {
                if (method.ReturnsVoid)
                {
                    // nothing to cache here.. skip
                    // TODO just call impl
                    continue;
                }

                var returnType = method.ReturnType;
                var methodName = method.Name;

                sb.Append($"\t\tpublic {returnType} {methodName}(");

                method.WriteParameters(sb, writeType: true);
                sb.AppendLine(")");
                sb.AppendLine("\t\t{");

                sb.Append($"\t\t\tvar key = new {method.ClassName}(");
                method.WriteParameters(sb);
                sb.AppendLine(");");
                sb.AppendLine($"\t\t\tif (_cache.TryGetValue<{returnType}>(key, out var value))");
                sb.AppendLine("\t\t\t{");
                //sb.AppendLine("\t\t\t\t_logger.LogInformation(\"CACHE HIT!!\");");
                sb.AppendLine("\t\t\t\treturn value;");
                sb.AppendLine("\t\t\t}");
                //sb.AppendLine("\t\t\t_logger.LogInformation(\"CACHE MISS\");");
                sb.AppendLine("\t\t\tvar entry = _cache.CreateEntry(key);");
                sb.Append($"\t\t\tvar result = _impl.{methodName}(");
                method.WriteParameters(sb);
                sb.AppendLine(");");
                sb.AppendLine("\t\t\tentry.SetValue(result);");
                sb.AppendLine("\t\t\t// TODO fix cache duration");
                sb.AppendLine("\t\t\t// TODO fix cache token expiration");
                sb.AppendLine("\t\t\tentry.SetSlidingExpiration(MemoizedInterfaceOptions.DefaultExpirationTime * MemoizedInterfaceOptions.DefaultCacheDurationFactor);");
                sb.AppendLine("\t\t\t/// need to manually call dispose instead of having a using");
                sb.AppendLine("\t\t\t// in case the factory passed in throws, in which case we");
                sb.AppendLine("\t\t\t// do not want to add the entry to the cache");
                sb.AppendLine("\t\t\tentry.Dispose();");
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

        private static void GenerateArgStruct(MemoizedMethodMember method, StringBuilder sb)
        {
            var methodClassName = method.ClassName;
            var lastArg = method.Parameters.LastOrDefault();


            sb.AppendLine($"\t\tpublic struct {methodClassName} : IEquatable<{methodClassName}>");
            sb.AppendLine("\t\t{");

            foreach (var arg in method.Parameters)
            {
                sb.AppendLine($"\t\t\tprivate readonly {arg.ArgType} _{arg.Name};");
            }

            sb.AppendLine();
            sb.Append($"\t\t\tpublic {methodClassName}(");
            method.WriteParameters(sb, writeType: true);

            sb.AppendLine(")");
            sb.AppendLine("\t\t\t{");
            foreach (var arg in method.Parameters)
            {
                sb.AppendLine($"\t\t\t\t_{arg.Name} = {arg.Name};");
            }

            sb.AppendLine("\t\t\t}");

            sb.AppendLine();

            sb.AppendLine($"\t\t\tpublic bool Equals({methodClassName} other)");
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
            sb.AppendLine($"\t\t\t\treturn obj is {methodClassName} castedObj && Equals(castedObj);");
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
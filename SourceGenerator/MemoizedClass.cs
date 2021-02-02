using System.Linq;
using System.Text;
using SourceGenerator.Models;

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

namespace {scopedCall.Namespace}.Memoized
{{
    public class {scopedCall.ClassName} : {fullInterfaceName}
    {{
");
            sb.AppendLine($"\t\tprivate readonly {fullInterfaceName} _impl;");
            sb.AppendLine($"\t\tprivate readonly ILogger<{scopedCall.ClassName}> _logger;");
            sb.AppendLine($"\t\tprivate readonly IMemoizerFactory _cacheFactory;");
            sb.AppendLine($"\t\tpublic {scopedCall.ClassName}(IMemoizerFactory cacheFactory, {fullInterfaceName} impl, ILogger<{scopedCall.ClassName}> logger)");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t_cacheFactory = cacheFactory;");
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
                method.WriteParameters(sb, writeType: true, prefix: "__");
                sb.AppendLine(")");
                sb.AppendLine("\t\t{");

                if (method.PartitionedParameter != null)
                {
                    sb.AppendLine($"\t\t\tvar cache = _cacheFactory.GetOrCreatePartition(__{method.PartitionedParameter.Name});");
                }
                else
                {
                    sb.AppendLine($"\t\t\tvar cache = _cacheFactory.GetGlobal();");
                }

                sb.AppendLine($"\t\t\tcache.RecordAccessCount();");
                sb.AppendLine();
                sb.Append($"\t\t\tvar key = new {method.ClassName}(");
                method.WriteParameters(sb, prefix: "__");
                sb.AppendLine(");");
                sb.AppendLine($"\t\t\tif (cache.TryGetValue<{returnType}>(key, out var value))");
                sb.AppendLine("\t\t\t{");
                sb.Append("\t\t\t\tif (_logger.IsEnabled(LogLevel.Debug))");
                sb.AppendLine(" _logger.LogDebug(\"Cache hit. {CacheName} {key} {value}\", cache.Name, key, value);");
                sb.AppendLine();
                sb.AppendLine("\t\t\t\treturn value;");
                sb.AppendLine("\t\t\t}");

                sb.AppendLine();
                sb.AppendLine($"\t\t\tvar clearCacheTokenSource = cache.ClearCacheTokenSource;");
                sb.AppendLine("\t\t\tcache.RecordMiss();");
                sb.AppendLine();
                sb.AppendLine("\t\t\tvar entry = cache.CreateEntry(key);");
                sb.Append($"\t\t\tvar result = _impl.{methodName}(");
                method.WriteParameters(sb, prefix: "__");
                sb.AppendLine(");");
                sb.AppendLine("\t\t\tentry.SetValue(result);");

                sb.AppendLine();

                sb.Append("\t\t\tif (_logger.IsEnabled(LogLevel.Debug))");
                sb.AppendLine(" _logger.LogDebug(\"Cache Miss. {CacheName} {key} {value}\", cache.Name, key, result);");
                sb.AppendLine();

                var slidingDuration = method.SlidingCache?.InMinutes ?? scopedCall.SlidingCache?.InMinutes ?? 10; // TODO fallback in global options

                sb.AppendLine($"\t\t\tcache.SetExpiration(entry, clearCacheTokenSource, {slidingDuration});");
                sb.AppendLine("");
                sb.AppendLine("\t\t\t// need to manually call dispose instead of having a using");
                sb.AppendLine("\t\t\t// in case the factory passed in throws, in which case we");
                sb.AppendLine("\t\t\t// do not want to add the entry to the cache");
                sb.AppendLine("\t\t\tentry.Dispose();");
                sb.AppendLine("\t\t\treturn result;");

                sb.AppendLine("\t\t}");
                sb.AppendLine();
            }

            foreach (var method in methods.Where(x => !x.ReturnsVoid))
            {
                GenerateArgStruct(method, sb);

                sb.AppendLine();
            }

            sb.AppendLine("\t}");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void GenerateArgStruct(MemoizedMethodMember method, StringBuilder sb)
        {
            var methodClassName = method.ClassName;
            var lastArg = method.Parameters.LastOrDefault();


            sb.AppendLine($"\t\tpublic readonly struct {methodClassName} : IEquatable<{methodClassName}>");
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
                sb.Append($"_{arg.Name}.Equals(other._{arg.Name})");
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

            sb.AppendLine($"\t\t\tpublic override string ToString()");
            sb.AppendLine("\t\t\t{");
            sb.Append($"\t\t\t\treturn $\"{methodClassName}(");
            foreach (var arg in method.Parameters)
            {
                sb.Append($"{{_{arg.Name}}}");
                if (!ReferenceEquals(arg, lastArg))
                    sb.Append(", ");
            }
            sb.AppendLine(")\";");
            sb.AppendLine("\t\t\t}");


            sb.AppendLine("\t\t}");
        }
    }
}
﻿using System.Linq;
using System.Text;
using MemoizeSourceGenerator.Models;

namespace MemoizeSourceGenerator
{
    internal static class MemoizedClass
    {
        public static string Generate(MemoizerCall call)
        {
            var fullInterfaceName = call.InterfaceType.ToDisplayString();

            var sb = new StringBuilder(@$"// <auto-generated />
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using MemoizeSourceGenerator.Attribute;
#nullable enable

namespace {call.Namespace}
{{
    public class {call.ClassName} : {fullInterfaceName}
    {{
");
            sb.AppendLine($"\t\tprivate readonly {fullInterfaceName} _impl;");
            sb.AppendLine($"\t\tprivate readonly ILogger<{call.ClassName}> _logger;");
            sb.AppendLine($"\t\tprivate readonly IMemoizerFactory _cacheFactory;");
            sb.AppendLine($"\t\tprivate readonly Action<ICacheEntry>? _configureEntry;");
            sb.AppendLine($"\t\tpublic {call.ClassName}(IMemoizerFactory cacheFactory, {fullInterfaceName} impl, ILogger<{call.ClassName}> logger, Action<ICacheEntry>? configureEntry = null)");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t_cacheFactory = cacheFactory;");
            sb.AppendLine("\t\t\t_impl = impl;");
            sb.AppendLine("\t\t\t_logger = logger;");
            sb.AppendLine("\t\t\t_configureEntry = configureEntry;");
            sb.AppendLine("\t\t}");

            sb.AppendLine();

            var methods= call.Methods;
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

                sb.Append($"\t\tpublic ");
                if (method.IsAsync)
                {
                    sb.Append("async ");
                }
                sb.Append($"{returnType} {methodName}(");
                method.WriteParameters(sb, writeType: true, prefix: "__");
                sb.AppendLine(")");
                sb.AppendLine("\t\t{");

                if (method.PartitionedParameter != null)
                {
                    sb.AppendLine($"\t\t\tvar cache = _cacheFactory.GetOrCreatePartition(\"{call.HumanId}\", __{method.PartitionedParameter.Name}, out var __);");
                }
                else
                {
                    sb.AppendLine($"\t\t\tvar cache = _cacheFactory.Get(\"{call.HumanId}\");");
                }

                sb.AppendLine($"\t\t\tcache.RecordAccessCount();");
                sb.AppendLine();
                sb.Append($"\t\t\tvar key = new {method.ClassName}(");
                method.WriteParameters(sb, prefix: "__");
                sb.AppendLine(");");
                sb.AppendLine($"\t\t\tif (cache.TryGetValue<{returnType}>(key, out var value))");
                sb.AppendLine("\t\t\t{");
                sb.Append("\t\t\t\tif (_logger.IsEnabled(LogLevel.Debug))");
                sb.AppendLine(" _logger.LogDebug(\"Cache hit. {CacheName} {key} {value}\", cache.DisplayName, key, value);");
                sb.AppendLine();
                sb.AppendLine("\t\t\t\treturn value;");
                sb.AppendLine("\t\t\t}");

                sb.AppendLine();
                sb.AppendLine($"\t\t\tvar clearCacheTokenSource = cache.ClearCacheTokenSource;");
                sb.AppendLine("\t\t\tcache.RecordMiss();");
                sb.AppendLine();
                sb.AppendLine("\t\t\tvar entry = cache.CreateEntry(key);");
                sb.Append($"\t\t\tvar result = ");
                if (method.IsAsync)
                {
                    sb.Append("await ");
                }
                sb.Append($"_impl.{methodName}(");
                method.WriteParameters(sb, prefix: "__");
                sb.AppendLine(");");
                sb.AppendLine("\t\t\tentry.SetValue(result);");

                sb.AppendLine();

                sb.Append("\t\t\tif (_logger.IsEnabled(LogLevel.Debug))");
                sb.AppendLine(" _logger.LogDebug(\"Cache Miss. {CacheName} {key} {value}\", cache.DisplayName, key, result);");
                sb.AppendLine();

                var slidingDuration = method.SlidingCache?.InMinutes ?? call.SlidingCache?.InMinutes ?? 10; // TODO fallback in global options

                sb.AppendLine($"\t\t\tcache.SetExpiration(entry, clearCacheTokenSource, {slidingDuration}, null, _configureEntry);");
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

            if (method.Parameters.Count > 0)
            {
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
            }

            sb.AppendLine();

            sb.AppendLine($"\t\t\tpublic bool Equals({methodClassName} other)");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine("\t\t\t\tif (ReferenceEquals(null, other)) return false;");
            sb.AppendLine("\t\t\t\tif (ReferenceEquals(this, other)) return true;");

            sb.Append($"\t\t\t\treturn ");
            foreach (var arg in method.Parameters)
            {
                if (arg.IsNullable)
                {
                    sb.Append($"(_{arg.Name} == null? other._{arg.Name} == null : _{arg.Name}.Equals(other._{arg.Name}))");
                }
                else
                {
                    sb.Append($"_{arg.Name}.Equals(other._{arg.Name})");
                }
                if (!ReferenceEquals(arg, lastArg))
                    sb.Append(" && ");
            }
            if (method.Parameters.Count == 0)
            {
                sb.Append("true");
            }

            sb.AppendLine(";");

            sb.AppendLine("\t\t\t}");
            sb.AppendLine();

            sb.AppendLine($"\t\t\tpublic override bool Equals(object? obj)");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine("\t\t\t\tif (ReferenceEquals(null, obj)) return false;");
            sb.AppendLine("\t\t\t\tif (ReferenceEquals(this, obj)) return true;");
            sb.AppendLine($"\t\t\t\treturn obj is {methodClassName} castedObj && Equals(castedObj);");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine();

            sb.AppendLine($"\t\t\tpublic override int GetHashCode()");
            sb.AppendLine("\t\t\t{");
            if (method.Parameters.Count == 0)
            {
                sb.AppendLine("\t\t\t\treturn 0;");
            }
            else
            {
                sb.Append("\t\t\t\treturn HashCode.Combine(");
                foreach (var arg in method.Parameters)
                {
                    sb.Append($"_{arg.Name}");
                    if (!ReferenceEquals(arg, lastArg))
                        sb.Append(", ");
                }
                sb.AppendLine(");");
            }
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
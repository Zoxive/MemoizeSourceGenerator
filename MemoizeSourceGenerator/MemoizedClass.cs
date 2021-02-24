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

namespace {call.ClassNamespace}
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
                    sb.AppendLine($"\t\t\tvar cache = _cacheFactory.GetOrCreatePartition(__{method.PartitionedParameter.Name});");
                }
                else
                {
                    sb.AppendLine($"\t\t\tvar cache = _cacheFactory.GetGlobal();");
                }

                sb.Append($"\t\t\tvar key = new {method.ClassName}(cache.PartitionKey,");
                method.WriteParameters(sb, prefix: "__");
                sb.AppendLine(");");
                sb.Append($"\t\t\tif (cache.TryGetValue<{method.TypeInCache}>(key, out var returnValue)");

                if (method.TypeIsReferenceType && !method.TypeCanBeNull)
                {
                    sb.Append(" && returnValue != null");
                }

                sb.AppendLine(")");
                sb.AppendLine("\t\t\t{");
                sb.Append("\t\t\t\tif (_logger.IsEnabled(LogLevel.Trace))");
                sb.AppendLine(" _logger.LogTrace(\"Cache hit. {CacheName}~{key} => {value}\", cache.DisplayName, key, returnValue);");
                sb.AppendLine();
                sb.AppendLine("\t\t\t\treturn returnValue;");
                sb.AppendLine("\t\t\t}");

                sb.AppendLine();
                sb.AppendLine($"\t\t\tvar tokenSourceBeforeComputingEntry = cache.ClearCacheTokenSource;");
                sb.AppendLine();
                sb.Append($"\t\t\tvar result = ");
                if (method.IsAsync)
                {
                    sb.Append("await ");
                }
                sb.Append($"_impl.{methodName}(");
                method.WriteParameters(sb, prefix: "__");
                sb.AppendLine(");");

                sb.AppendLine();

                sb.Append("\t\t\tif (_logger.IsEnabled(LogLevel.Debug))");
                sb.AppendLine(" _logger.LogDebug(\"Cache miss. {CacheName}~{key} => {value}\", cache.DisplayName, key, result);");
                sb.AppendLine();

                sb.Append("\t\t\tvar size = ");

                method.MemoizedMethodSizeOfFunction.Write(sb, "result");


                var slidingDuration = method.SlidingCache?.InMinutes ?? call.SlidingCache?.InMinutes ?? 10; // TODO fallback in global options
                sb.AppendLine($"\t\t\tcache.CreateEntry(key, result, tokenSourceBeforeComputingEntry, {slidingDuration}, size, _configureEntry);");

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


            sb.AppendLine($"\t\tpublic readonly struct {methodClassName} : IEquatable<{methodClassName}>, IPartitionObjectKey");
            sb.AppendLine("\t\t{");

            sb.AppendLine("\t\t\tpublic IPartitionKey PartitionKey { get; }");
            foreach (var arg in method.Parameters)
            {
                sb.AppendLine($"\t\t\tprivate readonly {arg.ArgType} _{arg.Name};");
            }

            if (method.Parameters.Count > 0)
            {
                sb.AppendLine();
                sb.Append($"\t\t\tpublic {methodClassName}(IPartitionKey partitionKey,");
                method.WriteParameters(sb, writeType: true, prefix: "__");

                sb.AppendLine(")");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine($"\t\t\t\tPartitionKey = partitionKey;");
                foreach (var arg in method.Parameters)
                {
                    sb.AppendLine($"\t\t\t\t_{arg.Name} = __{arg.Name};");
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

            sb.AppendLine($"\t\t\tpublic bool Equals(IPartitionObjectKey? obj)");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine("\t\t\t\tif (ReferenceEquals(null, obj)) return false;");
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
            sb.Append($"\t\t\t\treturn $\"{method.SimpleName}(");
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
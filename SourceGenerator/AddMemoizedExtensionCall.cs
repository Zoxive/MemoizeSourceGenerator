﻿using System;
using System.Collections.Generic;
using System.Text;
using SourceGenerator.Models;

namespace SourceGenerator
{
    public static class AddMemoizedExtensionCall
    {
        private static string Empty => @"using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMemoizedScoped<TInterface, TImplementation>(this IServiceCollection services) where TInterface : class where TImplementation : class, TInterface
        {
            throw new NotSupportedException(""This will be replaced with a SourceGenerator call"");
        }
    }
}";


        public static string Generate(IReadOnlyList<ScopedMemoizerCall> calls)
        {
            var sb = new StringBuilder(
@"using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SourceGenerator.Attribute;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
");
            GenerateExtensionMethod(calls, sb, "Scoped");
            GenerateExtensionMethod(calls, sb, "Singleton");

            sb.AppendLine("\t}");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void GenerateExtensionMethod(IReadOnlyList<ScopedMemoizerCall> calls, StringBuilder sb, string mode)
        {
            sb.AppendLine($"\t\tpublic static IServiceCollection AddMemoized{mode}<TInterface, TImplementation>(this IServiceCollection services) where TInterface : class where TImplementation : class, TInterface");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\tservices.TryAddSingleton<IMemoizerFactory, MemoizerFactory>();");
            foreach (var call in calls)
            {
                GenerateCall(call, sb, mode);
            }

            sb.AppendLine("\t\t\tthrow new NotSupportedException(\"Unmapped Types\");");
            sb.AppendLine("\t\t}");
        }

        private static void GenerateCall(ScopedMemoizerCall call, StringBuilder sb, string mode)
        {
            var interfaceName = call.InterfaceType.ToDisplayString();
            var implName = call.ImplementationsType.ToDisplayString();

            sb.AppendLine($"\t\t\tif (typeof(TInterface) == typeof({interfaceName}) && typeof(TImplementation) == typeof({implName}))");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine($"\t\t\t\tservices.TryAdd{mode}<{implName}>();");
            sb.AppendLine($"\t\t\t\tservices.Add{mode}<{interfaceName}>(s =>");
            sb.AppendLine("\t\t\t\t{");
            if (call.MemoizerFactoryType != null)
            {
                sb.AppendLine($"\t\t\t\t\tvar factory = s.GetRequiredService<{call.MemoizerFactoryType.ToDisplayString()}>();");
            }
            else
            {
                sb.AppendLine($"\t\t\t\t\tvar factory = s.GetRequiredService<IMemoizerFactory>();");
            }

            sb.AppendLine(
                $"\t\t\t\t\treturn new {call.Namespace}.Memoized.{call.ClassName}(factory, s.GetRequiredService<{implName}>(), s.GetRequiredService<ILogger<{call.Namespace}.Memoized.{call.ClassName}>>());");
            sb.AppendLine("\t\t\t\t});");
            sb.AppendLine("\t\t\t\treturn services;");
            sb.AppendLine("\t\t\t}");
        }
    }
}
using System;
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


        public static string Generate(IEnumerable<ScopedMemoizerCall> calls)
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
        public static IServiceCollection AddMemoizedScoped<TInterface, TImplementation>(this IServiceCollection services) where TInterface : class where TImplementation : class, TInterface
        {
            services.TryAddSingleton<IMemoizerFactory, MemoizerFactory>();
");
            /*
        }
    }
}");
*/
            foreach (var call in calls)
            {
                var interfaceName = call.InterfaceType.ToDisplayString();
                var implName = call.ImplementationsType.ToDisplayString();

                sb.AppendLine($"\t\t\tif (typeof(TInterface) == typeof({interfaceName}) && typeof(TImplementation) == typeof({implName}))");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine($"\t\t\t\tservices.TryAddScoped<{implName}>();");
                sb.AppendLine($"\t\t\t\tservices.AddScoped<{interfaceName}>(s =>");
                sb.AppendLine("\t\t\t\t{");
                sb.AppendLine($"\t\t\t\t\treturn new {call.Namespace}.Memoized.{call.ClassName}(s.GetRequiredService<IMemoizerFactory>(), s.GetRequiredService<{implName}>(), s.GetRequiredService<ILogger<{call.Namespace}.Memoized.{call.ClassName}>>());");
                sb.AppendLine("\t\t\t\t});");
                sb.AppendLine("\t\t\t\treturn services;");
                sb.AppendLine("\t\t\t}");
            }

            sb.AppendLine("\t\t\tthrow new NotSupportedException(\"Unmapped Types\");");

            sb.AppendLine("\t\t}");
            sb.AppendLine("\t}");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
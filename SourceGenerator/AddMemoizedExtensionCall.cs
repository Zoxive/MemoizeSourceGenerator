using System;
using System.Collections.Generic;
using System.Text;

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

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMemoizedScoped<TInterface, TImplementation>(this IServiceCollection services) where TInterface : class where TImplementation : class, TInterface
        {
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
                sb.AppendLine("\t\t\t\treturn services.AddScoped<TInterface, TImplementation>();");
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
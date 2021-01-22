using System;

namespace SourceGenerator.Attribute
{
    [AttributeUsage(AttributeTargets.Class, Inherited =  false)]
    public class Class1Attribute : System.Attribute
    {
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMemoizedScoped<TInterface, TImplementation>(this IServiceCollection services) where TInterface : class where TImplementation : class, TInterface
        {
            throw new NotSupportedException("This will be replaced with a SourceGenerator call");
        }
    }
}
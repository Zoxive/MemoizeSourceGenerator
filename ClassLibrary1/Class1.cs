using System;
using Microsoft.Extensions.DependencyInjection;
using Zoxive.Memoized;
using Zoxive.MemoizeSourceGenerator.Attribute;

namespace ClassLibrary1
{
    public static class Class1
    {
        public static void AddServices(IServiceCollection services)
        {
            services.AddMemoizedSingleton<IExample, Example>();
        }
    }

    public class Example : IExample
    {
    }

    [CreateMemoizedImplementation]
    public interface IExample
    {
    }
}
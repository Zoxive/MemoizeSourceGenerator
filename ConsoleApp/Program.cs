using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SourceGenerator.Attribute;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Types in this assembly:");
            foreach (var t in typeof(Program).Assembly.GetTypes())
            {
                Console.WriteLine(t.FullName);
            }

            Console.WriteLine();
            Console.WriteLine();

            var s = new ServiceCollection();
            s.AddLogging(o =>
            {
                o.AddConsole();
            });
            s.AddMemoryCache();
            s.AddMemoizedScoped<IDoMaths, DoMaths>();

            var services = s.BuildServiceProvider();

            using (var scope = services.CreateScope())
            {
                var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var maths = scope.ServiceProvider.GetRequiredService<IDoMaths>();

                var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

                log.LogInformation("Result: {result}", maths.Add(5, 10));

                // Should not see Adding {arg1} with {arg2} log as it was cached
                log.LogInformation("Cached Result: {result}", maths.Add(5, 10));

                log.LogInformation("New Result: {result}", maths.Add(10, 10));

                // manual bust
                cache.Remove(new DoMaths_Memoized.ArgKey_Add(5, 10));

                log.LogInformation("Result: {result}", maths.Add(5, 10));
            }
        }
    }

    [CreateMemoizedImplementation]
    public interface IDoMaths
    {
        int Add(int arg1, int arg2);
    }

    public class DoMaths : IDoMaths
    {
        private readonly ILogger<DoMaths> _logger;

        public DoMaths(ILogger<DoMaths> logger)
        {
            _logger = logger;
        }

        public int Add(int arg1, int arg2)
        {
            _logger.LogInformation("Calculating {arg1} + {arg2}", arg1, arg2);
            return arg1 + arg2;
        }
    }
}

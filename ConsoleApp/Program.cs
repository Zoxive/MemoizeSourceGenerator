using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
            s.AddLogging();
            s.AddMemoizedScoped<IDoMaths, DoMaths>();

            var services = s.BuildServiceProvider();

            using (var scope = services.CreateScope())
            {
                var maths = scope.ServiceProvider.GetRequiredService<IDoMaths>();

                var result = maths.Add(5, 10);
                Console.WriteLine($"Result: {result}");

                // Should not see Adding {arg1} with {arg2} log as it was cached
                var result2 = maths.Add(5, 10);
                Console.WriteLine($"Result: {result2}");
            }
        }
    }

    public interface IDoMaths
    {
        int Add(int arg1, int arg2);
    }

    public class DoMaths : IDoMaths
    {
        public int Add(int arg1, int arg2)
        {
            Console.WriteLine($"Adding {arg1} with {arg2}");
            return arg1 + arg2;
        }
    }
}

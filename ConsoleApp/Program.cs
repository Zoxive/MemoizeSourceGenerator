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

            var s = new ServiceCollection();
            s.AddLogging();
            s.AddMemoizedScoped<IDoMaths, DoMaths>();
        }
    }

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
            _logger.LogInformation("Adding {arg1} with {arg2}", arg1, arg2);
            return arg1 + arg2;
        }
    }
}

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

                var result = maths.Add(5, 10);
                log.LogInformation("Result: {result}", result);

                // Should not see Adding {arg1} with {arg2} log as it was cached
                var result2 = maths.Add(5, 10);
                log.LogInformation("Result: {result2}", result2);
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
            _logger.LogInformation("Adding {arg1} with {arg2}", arg1, arg2);
            return arg1 + arg2;
        }
    }

    public class DoMaths_Memoized : IDoMaths
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDoMaths _impl;

        public DoMaths_Memoized(IMemoryCache memoryCache, IDoMaths impl)
        {
            _memoryCache = memoryCache;
            _impl = impl;
        }

        public int Add(int arg1, int arg2)
        {
            var key = new ArgKey_Add2(arg1, arg2);
            if (_memoryCache.TryGetValue<int>(key, out var value))
            {
                return value;
            }
            var entry = _memoryCache.CreateEntry(key);
            var result = _impl.Add(arg1, arg2);
            entry.SetValue(result);
            return result;
        }

        internal class ArgKey_Add2 : IEquatable<ArgKey_Add2>
        {
            private readonly int _arg1;
            private readonly int _arg2;

            public ArgKey_Add2(int arg1, int arg2)
            {
                _arg1 = arg1;
                _arg2 = arg2;
            }

            public bool Equals(ArgKey_Add2 other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return _arg1 == other._arg1 && _arg2 == other._arg2;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is ArgKey_Add2 castedObj && Equals(castedObj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_arg1, _arg2);
            }
        }
    }
}

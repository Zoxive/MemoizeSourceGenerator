using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SourceGenerator.Attribute;

namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
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
                o.SetMinimumLevel(LogLevel.Debug);
            })
                .AddScoped<RequestScope>()
                .AddScoped<ScopedMemoizer>()
                .AddMemoizedScoped<IDoMaths, DoMaths>()
                .AddMemoizedScoped<ICustomMemoizerTest, CustomMemoizerTest>();

            var services = s.BuildServiceProvider();

            var log = services.GetRequiredService<ILogger<Program>>();
            try
            {
                //await WorkAsync(services);
                await WorkScopedTestAsync(services);
            }
            catch (Exception e)
            {
                log.LogError(e, "Err");
                throw;
            }
        }

        private static async Task WorkScopedTestAsync(ServiceProvider services)
        {
            using (var scope = services.CreateScope())
            {
                scope.SetTenant("123");

                var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var myClass = scope.ServiceProvider.GetRequiredService<ICustomMemoizerTest>();

                log.LogInformation("Result: {result}", myClass.Result("Hello"));
                log.LogInformation("Result: {result}", myClass.Result("Hello"));

                log.LogInformation("Result: {result}", myClass.Partition("auf wiedersehn", 1));
                log.LogInformation("Result: {result}", myClass.Partition("auf wiedersehn", 1));
                log.LogInformation("Result: {result}", myClass.Partition("auf wiedersehn", 2));
            }

            using (var scope = services.CreateScope())
            {
                scope.SetTenant("567");

                var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var myClass = scope.ServiceProvider.GetRequiredService<ICustomMemoizerTest>();

                log.LogInformation("Result: {result}", myClass.Result("Hello"));
                log.LogInformation("Result: {result}", myClass.Result("Hello"));
                log.LogInformation("Result: {result}", myClass.Result("Hello2"));

                log.LogInformation("Result: {result}", myClass.Partition("auf wiedersehn", 1));
                log.LogInformation("Result: {result}", myClass.Partition("auf wiedersehn", 1));
                log.LogInformation("Result: {result}", myClass.Partition("auf wiedersehn", 2));
            }

            using (var scope = services.CreateScope())
            {
                var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var cacheFactory = scope.ServiceProvider.GetRequiredService<IMemoizerFactory>();
                var stats = cacheFactory.Partitions.Select(x => x.GetStatistics());

                foreach (var stat in stats)
                {
                    log.LogInformation("Cache {@Statistics} for {Id}", stat.ToLogArg, stat.Id);
                }
            }

            // Writing to the console isnt instant do delay slightly to see all logs
            await Task.Delay(100);
        }

        private static async Task WorkAsync(ServiceProvider services)
        {
            using (var scope = services.CreateScope())
            {
                var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var maths = scope.ServiceProvider.GetRequiredService<IDoMaths>();
                var cacheFactory = scope.ServiceProvider.GetRequiredService<IMemoizerFactory>();

                // ValueTypes


                log.LogWarning("Maths");

                log.LogInformation("Result: {result}", maths.Add(5, 10));

                // Should not see Adding {arg1} with {arg2} log as it was cached
                log.LogInformation("Cached Result: {result}", maths.Add(5, 10));

                log.LogInformation("New Result: {result}", maths.Add(10, 10));

                // manual bust
                cacheFactory.GetGlobal().Remove(new Memoized.DoMaths.ArgKey_int_Add_int_int(5, 10));

                log.LogInformation("Result: {result}", maths.Add(5, 10));

                log.LogWarning("ValueTypes:");

                /*
                var org2 = new ValueType1(77);

                var key1 = new DoMaths_Memoized.ArgKey_int_GetValue_ConsoleApp_IValueType1(org);
                var key2 = new DoMaths_Memoized.ArgKey_int_GetValue_ConsoleApp_IValueType1(org2);
                */
                var org = new ValueType1(77);

                log.LogInformation("ValueType {value}", maths.GetValue(org));
                log.LogInformation("ValueType {value}", maths.GetValue(new ValueType1(77)));
                log.LogInformation("ValueTypeReference {value}", maths.GetValue(org));

                maths.SpecialMath("Hello!", 1, 2);
                maths.SpecialMath("Hello!", 1, 2);

                var stats = cacheFactory.Partitions.Select(x => x.GetStatistics());

                foreach (var stat in stats)
                {
                    log.LogInformation("Cache {@Statistics} for {Id}", stat.ToLogArg, stat.Id);
                }

                // Writing to the console isnt instant do delay slightly to see all logs
                await Task.Delay(100);
            }
        }
    }

    [CreateMemoizedImplementation]
    public interface IDoMaths
    {
        int Add(int arg1, int arg2);

        int GetValue(IValueType1 valueType);

        [SlidingCache(1.25)]
        int GetValue(ValueType2 valueType);

        ValueType2 FindValueTypeByName(string name);

        [SlidingCache(5)]
        int SpecialMath([PartitionCache]string name, int arg1, int arg2);
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

        public int GetValue(IValueType1 valueType)
        {
            _logger.LogInformation("Calculating Value {value}", valueType.Value);
            return valueType.Value;
        }

        public int GetValue(ValueType2 valueType)
        {
            _logger.LogInformation("Calculating Value2 {value}", valueType.Value);
            return valueType.Value;
        }

        public ValueType2 FindValueTypeByName(string name)
        {
            return new ValueType2(name.Length);
        }

        public int SpecialMath(string name, int arg1, int arg2)
        {
            _logger.LogInformation("Calculating SpecialMath ({Name}) - {arg1} + {arg2}", name, arg1, arg2);
            return arg1 + arg2;
        }
    }

    public class ValueType1 : IValueType1
    {
        public int Value { get; }

        public ValueType1(int value)
        {
            Value = value;
        }

        public bool Equals(IValueType1 other)
        {
            return Value == other?.Value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((IValueType1) obj);
        }

        public override int GetHashCode()
        {
            return Value;
        }
    }

    public interface IValueType1 : IEquatable<IValueType1>
    {
        int Value { get; }
    }

    public class ValueType2 : IEquatable<ValueType2>
    {
        public int Value { get; }

        public ValueType2(int value)
        {
            Value = value;
        }

        public bool Equals(ValueType2 other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ValueType2) obj);
        }

        public override int GetHashCode()
        {
            return Value;
        }
    }
}

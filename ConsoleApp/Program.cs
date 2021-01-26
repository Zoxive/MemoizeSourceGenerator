﻿using System;
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

            var log = services.GetRequiredService<ILogger<Program>>();
            try
            {
                Work(services);
            }
            catch (Exception e)
            {
                log.LogError(e, "Err");
                throw;
            }
        }

        private static void Work(ServiceProvider services)
        {
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
                cache.Remove(new DoMaths_Memoized.ArgKey_int_Add_int_int(5, 10));

                log.LogInformation("Result: {result}", maths.Add(5, 10));

                // ValueTypes
                log.LogInformation("ValueTypes:");

                var org = new ValueType1(77);

                log.LogInformation("ValueType {value}", maths.GetValue(org));
                log.LogInformation("ValueType {value}", maths.GetValue(new ValueType1(77)));
                log.LogInformation("ValueTypeReference {value}", maths.GetValue(org));

                log.LogWarning("WHAT?");
            }
        }
    }

    [CreateMemoizedImplementation]
    public interface IDoMaths
    {
        int Add(int arg1, int arg2);

        int GetValue(IValueType1 valueType);

        int GetValue(ValueType2 valueType);
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

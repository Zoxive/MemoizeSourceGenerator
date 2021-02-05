#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using MemoizeSourceGenerator.Attribute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConsoleApp
{
    public sealed class ScopedMemoizer : IMemoizerFactory
    {
        private static readonly ConcurrentDictionary<string, MemoizerFactory> TenantMemoizerFactories = new ();
        private readonly RequestScope _requestScope;
        private readonly ILoggerFactory _loggerFactory;

        public ScopedMemoizer(RequestScope requestScope, ILoggerFactory loggerFactory)
        {
            _requestScope = requestScope;
            _loggerFactory = loggerFactory;
        }

        private string Application => _requestScope.Tenant;
        private MemoizerFactory Factory => TenantMemoizerFactories.GetOrAdd(Application, CreateFactory);
        private MemoizerFactory CreateFactory(string _)
        {
            var factory = new MemoizerFactory(_loggerFactory);
            return factory;
        }

        private ApplicationKey? _key;
        public IPartitionKey FactoryKey
        {
            get
            {
                if (_key != null) return _key;

                return _key = new ApplicationKey(Application);
            }
        }

        public CachePartition GetGlobal() => Factory.GetOrCreatePartition(FactoryKey, rootKey: true);
        public CachePartition GetOrCreatePartition(IPartitionKey partitionKey) => Factory.GetOrCreatePartition(new CompositeKey(FactoryKey, partitionKey), rootKey: true);

        public void InvalidateAll() => Factory.InvalidateAll();
        public void InvalidatePartition(IPartitionKey partitionKey) => Factory.InvalidatePartition(partitionKey);
        public IEnumerable<CachePartition> Partitions => Factory.Partitions;
    }

    public sealed class ApplicationKey : IPartitionKey, IEquatable<ApplicationKey?>
    {
        public ApplicationKey(string application)
        {
            DisplayName = application;
        }

        public string DisplayName { get; }

        public bool Equals(IPartitionKey? obj)
        {
            return obj is ApplicationKey other && Equals(other);
        }

        public bool Equals(ApplicationKey? obj)
        {
            return DisplayName == obj?.DisplayName;
        }

        public override bool Equals(object? obj)
        {
            return obj is ApplicationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return DisplayName.GetHashCode();
        }
    }

    [CreateMemoizedImplementation(MemoizerFactory = typeof(ScopedMemoizer))]
    public interface ICustomMemoizerTest
    {
        string Result(string name);

        string Partition([PartitionCache] string name, int arg);

        Task<string> GetAsync(string arg);

        ValueTask<string> GetValueTaskAsync(string arg);
    }

    public sealed class CustomMemoizerTest : ICustomMemoizerTest
    {
        public string Result(string name)
        {
            return name + "Test!@#";
        }

        public string Partition(string name, int arg)
        {
            return $"{name}.{arg}";
        }

        public Task<string> GetAsync(string arg)
        {
            return Task.FromResult(arg);
        }

        public ValueTask<string> GetValueTaskAsync(string arg)
        {
            return new ValueTask<string>(arg);
        }
    }

    public static class ScopeExtensions
    {
        public static void SetTenant(this IServiceScope scope, string name)
        {
            scope.ServiceProvider
                .GetRequiredService<RequestScope>()
                .SetTenant(name);
        }
    }

    public sealed class RequestScope
    {
        public string Tenant { get; private set; } = string.Empty;

        public void SetTenant(string name)
        {
            Tenant = name;
        }
    }
}
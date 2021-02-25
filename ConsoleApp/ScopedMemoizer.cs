#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zoxive.MemoizeSourceGenerator.Attribute;

namespace ConsoleApp
{
    public sealed class ScopedMemoizer : IMemoizerFactory
    {
        private static readonly ConcurrentDictionary<IPartitionKey, MemoizerFactory> TenantMemoizerFactories = new ();
        private readonly ILoggerFactory _loggerFactory;
        private readonly RequestScope _requestScope;
        private string Application => _requestScope.Tenant;

        public ScopedMemoizer(ILoggerFactory loggerFactory, RequestScope requestScope)
        {
            _loggerFactory = loggerFactory;
            _requestScope = requestScope;
        }

        private MemoizerFactory Factory => TenantMemoizerFactories.GetOrAdd(FactoryKey, CreateFactory);
        private IPartitionKey? _factoryKey;
        public IPartitionKey FactoryKey
        {
            get
            {
                if (_factoryKey != null)
                    return _factoryKey;

                return _factoryKey = new StringPartitionKey(Application);
            }
        }

        private MemoizerFactory CreateFactory(IPartitionKey _)
        {
            var factory = new MemoizerFactory(FactoryKey, _loggerFactory);

            //_invalidator.GetInvalidator().Add(factory);

            return factory;
        }

        public CachePartition GetGlobal()
        {
            return Factory.GetOrCreatePartition(FactoryKey, rootKey: true);
        }

        public CachePartition GetOrCreatePartition(IPartitionKey partitionKey)
        {
            var f = Factory;

            CachePartition Create(IPartitionKey _)
            {
                var tenantSpecificPartitionKey = new CompositeKey(FactoryKey, partitionKey);
                return f.CreatePartition(tenantSpecificPartitionKey);
            }

            return f.GetOrCreatePartition(partitionKey, Create);
        }

        public void InvalidateAll() => Factory.InvalidateAll();
        public void InvalidatePartition(IPartitionKey partitionKey) => Factory.InvalidatePartition(new CompositeKey(FactoryKey, partitionKey));

        public IEnumerable<CachePartition> Partitions => Factory.Partitions;
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
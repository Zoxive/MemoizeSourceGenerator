#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        public CachePartition GetGlobal() => Factory.GetGlobal();
        public CachePartition GetOrCreatePartition(IPartitionKey partitionKey, out bool wasCreated) => Factory.GetOrCreatePartition(partitionKey, out wasCreated);
        public void InvalidateAll() => Factory.InvalidateAll();
        public void InvalidatePartition(IPartitionKey partitionKey) => Factory.InvalidatePartition(partitionKey);
        public IEnumerable<CachePartition> Partitions => Factory.Partitions;
    }

    [CreateMemoizedImplementation(MemoizerFactory = typeof(ScopedMemoizer))]
    public interface ICustomMemoizerTest
    {
        string Result(string name);

        string Partition([PartitionCache] string name, int arg);
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
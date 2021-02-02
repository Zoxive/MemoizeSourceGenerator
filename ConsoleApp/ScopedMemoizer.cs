#nullable enable
using System.Collections.Generic;
using MemoizeSourceGenerator.Attribute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConsoleApp
{
    public sealed class ScopedMemoizer : IMemoizerFactory
    {
        private readonly RequestScope _requestScope;
        private readonly MemoizerFactory _memoizerFactory;

        public ScopedMemoizer(RequestScope requestScope, ILoggerFactory loggerFactory)
        {
            _requestScope = requestScope;
            _memoizerFactory = new MemoizerFactory(loggerFactory);
        }

        private string Key(string? partition = null)
        {
            return $"{_requestScope.Tenant}-{partition ?? _memoizerFactory.Name}";
        }

        public IEnumerable<CachePartition> Partitions => _memoizerFactory.Partitions;

        public string Name => _requestScope.Tenant;

        public CachePartition GetGlobal()
        {
            return _memoizerFactory.GetOrCreatePartition(Key());
        }

        public CachePartition GetOrCreatePartition(string name)
        {
            return _memoizerFactory.GetOrCreatePartition(Key(name));
        }
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
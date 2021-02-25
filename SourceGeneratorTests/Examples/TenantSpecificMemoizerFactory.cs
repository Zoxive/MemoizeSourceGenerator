#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Zoxive.MemoizeSourceGenerator.Attribute;

namespace SourceGeneratorTests.Examples
{
    public sealed class RequestScope
    {
        public string Application { get; private set; } = string.Empty;

        public void SetApplication(string application)
        {
            Application = application;
        }

        public static RequestScope Static(string application)
        {
            return new RequestScope
            {
                Application = application
            };
        }
    }

    // Not released but an intended example of the library for scoped service
    public sealed class TenantSpecificMemoizerFactory : IMemoizerFactory
    {
        private static readonly ConcurrentDictionary<IPartitionKey, MemoizerFactory> TenantMemoizerFactories = new ();
        private readonly ILoggerFactory _loggerFactory;
        private readonly RequestScope _requestScope;
        private string Application => _requestScope.Application;

        public TenantSpecificMemoizerFactory(ILoggerFactory loggerFactory, RequestScope requestScope)
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
}
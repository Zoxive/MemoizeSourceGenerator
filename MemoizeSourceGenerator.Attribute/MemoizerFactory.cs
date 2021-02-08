using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MemoizeSourceGenerator.Attribute
{
    public sealed class MemoizerFactory : IMemoizerFactory
    {
        // One global cache with partitions inside that
        private static MemoryCache? _memoryCache;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<IPartitionKey, CachePartition> _cachePartitions = new();

        public MemoizerFactory(ILoggerFactory loggerFactory, MemoryCacheOptions? options = null)
        {
            _loggerFactory = loggerFactory;

            if (_memoryCache == null)
            {
                _memoryCache = new MemoryCache(options ?? new MemoryCacheOptions());
            }
        }

        public IEnumerable<CachePartition> Partitions => _cachePartitions.Values;

        public IPartitionKey FactoryKey => GlobalKey.Instance;

        public CachePartition GetGlobal()
        {
            return GetOrCreatePartition(FactoryKey, rootKey: true);
        }

        public CachePartition GetOrCreatePartition(IPartitionKey partitionKey)
        {
            return GetOrCreatePartition(partitionKey, false);
        }

        public void InvalidateAll()
        {
            foreach (var c in _cachePartitions.Values)
            {
                c.Invalidate();
            }
        }

        public void InvalidatePartition(IPartitionKey partitionKey)
        {
            if (_cachePartitions.TryGetValue(partitionKey, out var cachePartition))
            {
                cachePartition.Invalidate();
            }
        }

        // Not on interface, but exposed for composition purposes
        public CachePartition GetOrCreatePartition(IPartitionKey partitionKey, bool rootKey)
        {
            var key = rootKey? partitionKey : new CompositeKey(FactoryKey, partitionKey);

            CachePartition Create(IPartitionKey _)
            {
                 return CreatePartition(key);
            }
            return GetOrCreatePartition(key, Create);
        }

        // Not on the interface, but exposed for composition purposes
        public CachePartition GetOrCreatePartition(IPartitionKey partitionKey, Func<IPartitionKey, CachePartition> createPartition)
        {
            return _cachePartitions.GetOrAdd(partitionKey, createPartition);
        }

        public CachePartition CreatePartition(IPartitionKey partitionKey)
        {
             return new CachePartition(partitionKey, _loggerFactory.CreateLogger<CachePartition>(), _memoryCache!);
        }
    }

    public static class MemoizerFactoryExtensions
    {
        public static CachePartition GetOrCreatePartition(this IMemoizerFactory factory, string partition)
        {
            return factory.GetOrCreatePartition(new StringPartitionKey(partition));
        }
    }
}
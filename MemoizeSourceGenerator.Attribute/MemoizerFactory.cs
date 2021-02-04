using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MemoizeSourceGenerator.Attribute
{
    public sealed class MemoizerFactory : IMemoizerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<IPartitionKey, CachePartition> _cachePartitions = new();

        public MemoizerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public IEnumerable<CachePartition> Partitions => _cachePartitions.Values;

        public CachePartition GetGlobal()
        {
            return GetOrCreatePartition( GlobalKey.Instance);
        }

        public CachePartition GetOrCreatePartition(IPartitionKey partitionKey)
        {
            CachePartition Create(IPartitionKey _)
            {
                 return CreatePartition(partitionKey);
            }
            return GetOrCreatePartition(partitionKey, Create);
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

        // Not on the interface, but exposed for composition purposes
        public CachePartition GetOrCreatePartition(IPartitionKey partitionKey, Func<IPartitionKey, CachePartition> createPartition)
        {
            return _cachePartitions.GetOrAdd(partitionKey, createPartition);
        }
        public CachePartition CreatePartition(IPartitionKey partitionKey)
        {
             return new CachePartition(partitionKey, _loggerFactory.CreateLogger<CachePartition>(), new MemoryCache(new MemoryCacheOptions()));
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
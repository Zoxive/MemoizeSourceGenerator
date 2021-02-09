using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MemoizeSourceGenerator.Attribute
{
    // TODO split this into a Global vs Instance? right now its both.. and little confusing
    public sealed class MemoizerFactory : IMemoizerFactory
    {
        // One global cache with partitions inside that
        private static MemoryCache? _memoryCache;

        private static MemoizerFactory? _global;

        public static bool GlobalIsCreated => _global != null;

        public static MemoizerFactory Global
        {
            get
            {
                if (_global != null)
                {
                    return _global;
                }

                return _global = new MemoizerFactory(GlobalKey.Instance, NullLoggerFactory.Instance);
            }

            // This allows users to set MemoryCacheOptions and ILoggerFactory to their choosing
            set
            {
                if (_global != null)
                    throw new ArgumentOutOfRangeException(nameof(value), "Cant override global factory once it was originally set..");
                _global = value;
            }
        }

        private readonly ILoggerFactory _loggerFactory;

        // Instance specific CachePartitions
        private readonly ConcurrentDictionary<IPartitionKey, CachePartition> _cachePartitions = new();

        public MemoizerFactory(IPartitionKey factoryKey, ILoggerFactory loggerFactory, MemoryCacheOptions? options = null)
        {
            FactoryKey = factoryKey;
            _loggerFactory = loggerFactory;

            if (_memoryCache == null)
            {
                _memoryCache = new MemoryCache(options ?? new MemoryCacheOptions());
            }
        }

        public IEnumerable<CachePartition> Partitions => _cachePartitions.Values;

        public IPartitionKey FactoryKey { get; }

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
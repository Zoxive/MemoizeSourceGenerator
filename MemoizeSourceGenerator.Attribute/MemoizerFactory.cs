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
        private static CachePartition? _globalPartition;

        public MemoizerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public IEnumerable<CachePartition> Partitions => _cachePartitions.Values;

        public CachePartition Get(string callerId)
        {
            if (_globalPartition != null)
            {
                return _globalPartition;
            }

            var globalKey = GlobalKey.Instance;

            _globalPartition = new CachePartition(callerId, globalKey, _loggerFactory.CreateLogger<CachePartition>(), new MemoryCache(new MemoryCacheOptions()));
            _cachePartitions[globalKey] = _globalPartition;
            return _globalPartition;
        }

        public CachePartition GetOrCreatePartition(string callerId, IPartitionKey partitionKey, out bool created)
        {
            var wasCreated = false;
            CachePartition Create(IPartitionKey _)
            {
                 wasCreated = true;
                 return new CachePartition(callerId, partitionKey, _loggerFactory.CreateLogger<CachePartition>(), new MemoryCache(new MemoryCacheOptions()));
            }
            var result = _cachePartitions.GetOrAdd(partitionKey, Create);
            created = wasCreated;
            return result;
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
    }

    public static class MemoizerFactoryExtensions
    {
        public static CachePartition GetOrCreatePartition(this IMemoizerFactory factory, string callerId, string partition, out bool created)
        {
            var partitionKey = new StringPartitionKey(partition);
            return factory.GetOrCreatePartition(callerId, partitionKey, out created);
        }
    }
}
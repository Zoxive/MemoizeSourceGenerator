using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MemoizeSourceGenerator.Attribute
{
    public sealed class MemoizerFactory : IMemoizerFactory
    {
        private static readonly ConcurrentDictionary<string, CachePartition> CachePartitions = new();
        private static CachePartition? _globalPartition;
        private readonly ILoggerFactory _loggerFactory;

        public MemoizerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public IEnumerable<CachePartition> Partitions => CachePartitions.Values;

        public string Name { get; } = "|GLOBAL|";

        public CachePartition GetGlobal()
        {
            if (_globalPartition != null)
            {
                return _globalPartition;
            }

            _globalPartition = new CachePartition(Name, _loggerFactory.CreateLogger<CachePartition>(), new MemoryCache(new MemoryCacheOptions()));
            CachePartitions[Name] = _globalPartition;
            return _globalPartition;
        }

        public CachePartition GetOrCreatePartition(string name)
        {
            return CachePartitions.GetOrAdd(name, ValueFactory);
        }

        private CachePartition ValueFactory(string arg)
        {
            return new(arg, _loggerFactory.CreateLogger<CachePartition>(), new MemoryCache(new MemoryCacheOptions()));
        }
    }
}
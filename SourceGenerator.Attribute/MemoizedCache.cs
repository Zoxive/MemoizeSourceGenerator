using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

namespace SourceGenerator.Attribute
{
    public static class MemoizerFactory
    {
        private static readonly ConcurrentDictionary<string, CachePartition> CachePartitions = new();
        private static readonly CachePartition GlobalPartition;

        static MemoizerFactory()
        {
            GlobalPartition = new CachePartition("____GLOBAL____");
            CachePartitions[GlobalPartition.Name] = GlobalPartition;
        }

        public static IEnumerable<CachePartition> Partitions => CachePartitions.Values;

        public static CachePartition GetGlobal()
        {
            return GlobalPartition;
        }

        public static CachePartition GetOrCreatePartition(string name)
        {
            return CachePartitions.GetOrAdd(name, ValueFactory);
        }

        private static CachePartition ValueFactory(string arg)
        {
            return new(arg);
        }
    }

    public sealed class CacheStatistics
    {
        public CacheStatistics(string id, int accessCount, double hitRatio, int entryCount, int totalSize)
        {
            Id = id;
            AccessCount = accessCount;
            HitRatio = hitRatio;
            EntryCount = entryCount;
            TotalSize = totalSize;
        }

        public string Id { get; }
        public int AccessCount { get; }
        public double HitRatio { get; }
        public int EntryCount { get; }
        public int TotalSize { get; }
    }

    public sealed class CachePartition : IMemoryCache
    {
        private MemoryCache Cache { get; }
        public string Name { get; }

        private CancellationTokenSource _clearCacheTokenSource;
        private readonly object _tokenSourceSync;
        private int _accessCount = 0;
        private int _misses = 0;
        private int _totalSize = 0;

        public CachePartition(string name)
        {
            Name = name;

            // TODO add a way for users to customize this
            Cache = new MemoryCache(new MemoryCacheOptions());
            _tokenSourceSync = new object();
            _clearCacheTokenSource = new CancellationTokenSource();
        }

        public bool TryGetValue(object key, out object value) => Cache.TryGetValue(key, out value);
        public ICacheEntry CreateEntry(object key) => Cache.CreateEntry(key);
        public void Remove(object key) => Cache.Remove(key);

        public void Invalidate()
        {
            var tokenSource = _clearCacheTokenSource;

            lock (_tokenSourceSync)
            {
                if (tokenSource?.IsCancellationRequested == false && tokenSource.Token.CanBeCanceled)
                {
                    tokenSource.Cancel();
                    tokenSource.Dispose();
                }
                _clearCacheTokenSource = new CancellationTokenSource();
            }
        }

        public void RecordAccessCount()
        {
            Interlocked.Increment(ref _accessCount);
        }

        public void RecordMiss()
        {
            Interlocked.Increment(ref _misses);
        }

        public CacheStatistics GetStatistics()
        {
            var count = Interlocked.Exchange(ref _accessCount, 0);
            var misses = Interlocked.Exchange(ref _misses, 0);

            // force expired items scan
            Cache.Remove(this);

            return new CacheStatistics(Name, count, Math.Round((double)(count - misses) / count, 3), Cache.Count, _totalSize);
        }

        public void Dispose()
        {
            _clearCacheTokenSource.Cancel();
            _clearCacheTokenSource.Dispose();
            Cache.Dispose();
        }
    }

    internal readonly struct Key<TKey> : IEquatable<Key<TKey>> where TKey : IEquatable<TKey>
    {
        public Key(TKey ownerKey, Type implType)
        {
            OwnerKey = ownerKey;
            ImplType = implType;
        }

        public TKey OwnerKey { get; }
        public Type ImplType { get; }

        public override bool Equals(object? obj)
        {
            return obj is Key<TKey> key && Equals(key);
        }

        public bool Equals(Key<TKey> other)
        {
            return EqualityComparer<TKey>.Default.Equals(OwnerKey, other.OwnerKey)
                   && EqualityComparer<Type>.Default.Equals(ImplType, other.ImplType);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OwnerKey, ImplType);
        }
    }
}
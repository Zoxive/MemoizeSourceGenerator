using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

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

        public CancellationTokenSource ClearCacheTokenSource { get; private set; }
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
            ClearCacheTokenSource = new CancellationTokenSource();
        }

        public bool TryGetValue(object key, out object value) => Cache.TryGetValue(key, out value);
        public ICacheEntry CreateEntry(object key) => Cache.CreateEntry(key);
        public void Remove(object key) => Cache.Remove(key);

        public void Invalidate()
        {
            var tokenSource = ClearCacheTokenSource;

            lock (_tokenSourceSync)
            {
                if (tokenSource?.IsCancellationRequested == false && tokenSource.Token.CanBeCanceled)
                {
                    tokenSource.Cancel();
                    tokenSource.Dispose();
                }
                ClearCacheTokenSource = new CancellationTokenSource();
            }
        }

        public void SetExpiration(ICacheEntry entry, CancellationTokenSource clearCacheTokenSource, double inMinutes, long? size = null)
        {
            lock (_tokenSourceSync)
            {
                if (clearCacheTokenSource != ClearCacheTokenSource)
                {
                    entry.SetAbsoluteExpiration(TimeSpan.FromTicks(1));
                }
                else
                {
                    entry.SetSlidingExpiration(TimeSpan.FromMinutes(inMinutes))
                        .AddExpirationToken(new CancellationChangeToken(ClearCacheTokenSource.Token));
                }
            }

            if (size.HasValue)
            {
                entry.Size = size;
                Interlocked.Add(ref _totalSize, (int)size.Value);
            }

            entry.RegisterPostEvictionCallback(EvictionCallback);
            void EvictionCallback(object _, object value, EvictionReason __, object ___)
            {
                if (size.HasValue)
                {
                    Interlocked.Add(ref _totalSize, -(int)size.Value);
                }
            }
        }

        public void RecordAccessCount() => Interlocked.Increment(ref _accessCount);
        public void RecordMiss() => Interlocked.Increment(ref _misses);

        public CacheStatistics GetStatistics()
        {
            var count = Interlocked.Exchange(ref _accessCount, 0);
            var misses = Interlocked.Exchange(ref _misses, 0);

            // force expired items scan
            Cache.Remove(this);

            return new CacheStatistics(Name, count, Math.Round((double)(count - misses) / count, 3), Cache.Count, _totalSize);
        }

        /*
        private void ComputeSizeAndUpdateResult(CacheResult result, CachePartition cachePartition, ICacheEntry e)
        {
            var sw = Stopwatch.StartNew();

            var partitionName = cachePartition.Name;
            var m = MemorySizeComputePool.Get();
            var size = (int)m.SizeOf(result.Value);
            lock(result)
            {
                // don't cache larger objects if we are already running low on available memory
                if (DefaultCacheDurationFactor < 0.2 && size > 20*1024)
                {
                    result.Status = CacheStatus.NotCached;
                    result.ByteSize = 0;
                    // reset expiration to the non scaled default
                    e.SetSlidingExpiration(DefaultExpirationTime);
                    _logger.LogWarning("Not caching item due to memory constraints ({Id}) {Factory} {size}", partitionName != null ? $"{Id}-{partitionName}" : Id, DefaultCacheDurationFactor, size);
                }
                else if (result.Status != CacheStatus.NotCached)
                {
                    result.ByteSize = size;
                    Interlocked.Add(ref cachePartition.TotalSize, size);
                    result.Status = CacheStatus.Cached;
                }
                else
                {
                    result.ByteSize = 0;
                    _logger.LogWarning("Not caching item status? {status} ({Id}) {Factory} {size}", result.Status, partitionName != null ? $"{Id}-{partitionName}" : Id, DefaultCacheDurationFactor, size);
                }
            }
            MemorySizeComputePool.Return(m);

            sw.Stop();

            if (sw.Elapsed.TotalMilliseconds > 0.1 || size >= 10000)
            {
                _logger.LogTrace("Computing size of cached item took {timeMs}ms, {size} bytes ({Id})", sw.Elapsed.TotalMilliseconds, size, partitionName != null ? $"{Id}-{partitionName}" : Id);
            }
        }
        */

        public void Dispose()
        {
            ClearCacheTokenSource.Cancel();
            ClearCacheTokenSource.Dispose();
            Cache.Dispose();
        }

        internal sealed class CacheResult
        {
            public int ByteSize { get; set; }
            public object? Value { get; set; }
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
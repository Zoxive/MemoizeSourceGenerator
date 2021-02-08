using System;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace MemoizeSourceGenerator.Attribute
{
    public sealed class CachePartition
    {
        private readonly ILogger<CachePartition> _logger;
        public string DisplayName => PartitionKey.DisplayName;
        public IPartitionKey PartitionKey { get; }
        private IMemoryCache Cache { get; }

        public CancellationTokenSource ClearCacheTokenSource { get; private set; }
        private readonly object _tokenSourceSync;
        private int _accessCount = 0;
        private int _misses = 0;
        private int _totalSize = 0;
        private int _partitionCount = 0;

        public CachePartition(IPartitionKey partitionKey, ILogger<CachePartition> logger, IMemoryCache memoryCache)
        {
            _logger = logger;
            PartitionKey = partitionKey;
            Cache = memoryCache;
            _tokenSourceSync = new object();
            ClearCacheTokenSource = new CancellationTokenSource();
        }

        public bool TryGetValue<TValue>(IPartitionObjectKey key, out TValue? value)
        {
            // Prevent keys from other partitions
            if (!key.PartitionKey.Equals(PartitionKey))
            {
                value = default;
                return false;
            }

            Interlocked.Increment(ref _accessCount);

            if (Cache.TryGetValue(key, out value))
            {
                return true;
            }

            Interlocked.Increment(ref _misses);
            return false;

        }

        public bool Remove(IPartitionObjectKey key)
        {
            // Prevent keys from other partitions
            if (!key.PartitionKey.Equals(PartitionKey))
            {
                return false;
            }
            Cache.Remove(key);

            return true;
        }

        public bool CreateEntry<TValue>(IPartitionObjectKey key, TValue? value, CancellationTokenSource tokenSourceBeforeComputingValue, double slidingCacheInMinutes, long? size = null, Action<ICacheEntry>? configureEntry = null)
        {
            // Prevent keys from other partitions
            if (!key.PartitionKey.Equals(PartitionKey))
            {
                return false;
            }

            var entry = Cache.CreateEntry(key);

            entry.SetValue(value);

            // TODO size will not be optional
            if (size.HasValue)
            {
                entry.Size = size;
                Interlocked.Add(ref _totalSize, (int)size.Value);
            }

            Interlocked.Increment(ref _partitionCount);

            SetExpiration(entry, tokenSourceBeforeComputingValue, slidingCacheInMinutes, configureEntry);
            RegisterEvictionCallback(size, entry);

            // need to manually call dispose instead of having a using");
            // in case the factory passed in throws, in which case we");
            // do not want to add the entry to the cache");
            entry.Dispose();

            return true;
        }

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

            Interlocked.Exchange(ref _totalSize, 0);
            Interlocked.Exchange(ref _partitionCount, 0);

            // force expired items scan
            Cache.Remove(this);
        }

        private void RegisterEvictionCallback(long? size, ICacheEntry entry)
        {
            entry.RegisterPostEvictionCallback(EvictionCallback);

            void EvictionCallback(object callbackObjKey, object callbackObjValue, EvictionReason reason, object ___)
            {
                if (size.HasValue)
                {
                    Interlocked.Add(ref _totalSize, -(int) size.Value);
                }

                Interlocked.Decrement(ref _partitionCount);

                if (reason == EvictionReason.Capacity)
                {
                    _logger.LogWarning("Cache Item removed due to Capacity. {Key} {Value}", callbackObjKey, callbackObjValue);
                }
            }
        }

        private void SetExpiration(ICacheEntry entry, CancellationTokenSource tokenSourceBeforeComputingValue, double inMinutes, Action<ICacheEntry>? configureEntry = null)
        {
            lock (_tokenSourceSync)
            {
                configureEntry?.Invoke(entry);
                if (tokenSourceBeforeComputingValue != ClearCacheTokenSource)
                {
                    entry.SetAbsoluteExpiration(TimeSpan.FromTicks(1));
                }
                else
                {
                    entry.SetSlidingExpiration(TimeSpan.FromMinutes(inMinutes))
                        .AddExpirationToken(new CancellationChangeToken(ClearCacheTokenSource.Token));
                }
            }
        }

        public CacheStatistics GetStatistics()
        {
            // force expired items scan
            Cache.Remove(this);

            var accessCount = Interlocked.Exchange(ref _accessCount, 0);
            var misses = Interlocked.Exchange(ref _misses, 0);

            // Invalidate() directly sets these to 0, so if they appear below 0 its from EvictionCallback
            if (_partitionCount < 0)
                Interlocked.Exchange(ref  _partitionCount, 0);

            if (_totalSize < 0)
                Interlocked.Exchange(ref _totalSize, 0);

            return new CacheStatistics(DisplayName, accessCount, misses, _partitionCount, _totalSize);
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
    }
}
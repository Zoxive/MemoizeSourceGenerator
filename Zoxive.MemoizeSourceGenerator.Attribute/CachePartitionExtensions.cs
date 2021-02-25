using System;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

namespace Zoxive.MemoizeSourceGenerator.Attribute
{
    public static class CachePartitionExtensions
    {
        public static bool TryGetValue<TValue>(this CachePartition partition, string key, out TValue? value)
        {
            var stringKey = new PartitionObjectKeyString(partition.PartitionKey, key);
            return partition.TryGetValue(stringKey, out value);
        }


        public static bool CreateEntry<TValue>(this CachePartition partition, string key, TValue? value, CancellationTokenSource tokenSourceBeforeComputingValue, double slidingCacheInMinutes, long? size = null, Action<ICacheEntry>? configureEntry = null)
        {
            var stringKey = new PartitionObjectKeyString(partition.PartitionKey, key);
            return partition.CreateEntry(stringKey, value, tokenSourceBeforeComputingValue, slidingCacheInMinutes, size, configureEntry);
        }

        public static bool Remove(this CachePartition partition, string key)
        {
            var stringKey = new PartitionObjectKeyString(partition.PartitionKey, key);
            return partition.Remove(stringKey);
        }
    }
}
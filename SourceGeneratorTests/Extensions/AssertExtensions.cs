using System;
using FluentAssertions;
using FluentAssertions.Primitives;
using MemoizeSourceGenerator.Attribute;
using Microsoft.Extensions.Caching.Memory;

namespace SourceGeneratorTests.Extensions
{
    public static class AssertExtensions
    {
        public static void Is<TType>(this TType actual, TType expected)
        {
            actual.Should().Be(expected);
        }

        public static ObjectAssertions HaveMisses(this ObjectAssertions objectAssertions, int misses)
        {
            (objectAssertions.Subject as CacheStatistics)!.Misses.Should().Be(misses);
            return objectAssertions;
        }

        public static ObjectAssertions HaveAccessCount(this ObjectAssertions objectAssertions, int count)
        {
            (objectAssertions.Subject as CacheStatistics)!.AccessCount.Should().Be(count);
            return objectAssertions;
        }

        public static ObjectAssertions HaveEntryCount(this ObjectAssertions objectAssertions, int count)
        {
            (objectAssertions.Subject as CacheStatistics)!.EntryCount.Should().Be(count);
            return objectAssertions;
        }

        public static ObjectAssertions HaveTotalSize(this ObjectAssertions objectAssertions, int totalSize)
        {
            (objectAssertions.Subject as CacheStatistics)!.TotalSize.Should().Be(totalSize);
            return objectAssertions;
        }

        // Make unit tests easier to read by defaulting some params
        public static bool CreateEntry<T>(this CachePartition cache, string key, T value, Action<ICacheEntry> configureEntry = null)
        {
            return cache.CreateEntry(key, value, cache.ClearCacheTokenSource, 10, null, configureEntry);
        }

        public static bool CreateEntry<T>(this CachePartition cache, IPartitionObjectKey key, T value, Action<ICacheEntry> configureEntry = null)
        {
            return cache.CreateEntry(key, value, cache.ClearCacheTokenSource, 10, null, configureEntry);
        }
    }
}
using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace SourceGenerator.Attribute
{
    public sealed class MemoizedCache
    {
        private readonly IMemoryCache _cache;

        public MemoizedCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public bool TryGetValue(object key, out object value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public ICacheEntry CreateEntry(object key) => _cache.CreateEntry(key);
        public void Remove(object key) => _cache.Remove(key);
        public void Dispose() => _cache.Dispose();
    }

    public abstract class AbstractMemoizer
    {
        protected AbstractMemoizer()
        {
        }
    }
}
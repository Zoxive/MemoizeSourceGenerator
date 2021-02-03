using System;

namespace MemoizeSourceGenerator.Attribute
{
    public sealed class CacheStatistics
    {
        public CacheStatistics(string id, int accessCount, int misses, int entryCount, int totalSize)
        {
            Id = id;
            AccessCount = accessCount;
            Misses = misses;
            EntryCount = entryCount;
            TotalSize = totalSize;
        }

        public string Id { get; }
        public int AccessCount { get; }
        public int Misses { get; }
        public int EntryCount { get; }
        public int TotalSize { get; }

        public double HitRatio => Math.Round((double)(AccessCount - Misses) / AccessCount, 3);

        public object ToLogArg => new { AccessCount, Misses, EntryCount, TotalSize };
    }
}
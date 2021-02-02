using System.Collections.Generic;

namespace MemoizeSourceGenerator.Attribute
{
    public interface IMemoizerFactory
    {
        string Name { get; }

        CachePartition GetGlobal();
        CachePartition GetOrCreatePartition(string name);

        public IEnumerable<CachePartition> Partitions { get; }
    }
}
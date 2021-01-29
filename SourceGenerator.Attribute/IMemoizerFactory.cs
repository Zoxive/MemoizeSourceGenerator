using System.Collections.Generic;

namespace SourceGenerator.Attribute
{
    public interface IMemoizerFactory
    {
        CachePartition GetGlobal();
        CachePartition GetOrCreatePartition(string name);

        public IEnumerable<CachePartition> Partitions { get; }
    }
}
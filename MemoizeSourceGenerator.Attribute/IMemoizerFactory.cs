using System.Collections.Generic;

namespace MemoizeSourceGenerator.Attribute
{
    public interface IMemoizerFactory
    {
        CachePartition GetGlobal();
        CachePartition GetOrCreatePartition(IPartitionKey partitionKey);

        void InvalidateAll();
        void InvalidatePartition(IPartitionKey partitionKey);

        public IEnumerable<CachePartition> Partitions { get; }
    }
}
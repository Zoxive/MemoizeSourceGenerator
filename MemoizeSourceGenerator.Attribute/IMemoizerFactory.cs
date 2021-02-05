using System.Collections.Generic;

namespace MemoizeSourceGenerator.Attribute
{
    public interface IMemoizerFactory
    {
        IPartitionKey FactoryKey { get; }

        CachePartition GetGlobal();
        CachePartition GetOrCreatePartition(IPartitionKey partitionKey);

        void InvalidateAll();
        void InvalidatePartition(IPartitionKey partitionKey);

        public IEnumerable<CachePartition> Partitions { get; }
    }
}
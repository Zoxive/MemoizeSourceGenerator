using System.Collections.Generic;

namespace MemoizeSourceGenerator.Attribute
{
    public interface IMemoizerFactory
    {
        CachePartition Get(string callerId);
        CachePartition GetOrCreatePartition(string callerId, IPartitionKey partitionKey, out bool wasCreated);

        void InvalidateAll();
        void InvalidatePartition(IPartitionKey partitionKey);

        public IEnumerable<CachePartition> Partitions { get; }
    }
}
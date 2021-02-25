using SourceGeneratorTests.Extensions;
using Xunit;
using Zoxive.MemoizeSourceGenerator.Attribute;

namespace SourceGeneratorTests
{
    public class CachePartitionTests
    {
		private readonly IMemoizerFactory _factory = MemoizerFactory.Global;

        [Fact]
        public void GlobalPartitionName()
        {
            var p = _factory.GetGlobal();
            p.DisplayName.Is("GLOBAL");
        }

        [Fact]
        public void PartitionName()
        {
            var p = _factory.GetOrCreatePartition("Part1");
            p.DisplayName.Is("GLOBAL>Part1");
        }

        [Fact]
        public void ReturnsSamePartition()
        {
            var p = _factory.GetOrCreatePartition("Part3");

            // Same as p
            var p2 = _factory.GetOrCreatePartition("Part3");

            ReferenceEquals(p, p2).Is(true);
        }

        [Fact]
        public void CanGetItemFromCache()
        {
            var p = _factory.GetGlobal();

            p.CreateEntry("Test123", "Value").Is(true);

            p.TryGetValue<string>("Test123", out var value).Is(true);
            value.Is("Value");
        }

        [Fact]
        public void Partition_Misses_GlobalCache()
        {
            var global = _factory.GetGlobal();
            global.CreateEntry("Test2", "Value").Is(true);

            var partition = _factory.GetOrCreatePartition("Part1");
            partition.TryGetValue<string>("Test2", out var value).Is(false);
            global.TryGetValue<string>("Test2", out value).Is(true);
        }

        [Fact]
        public void Global_Misses_Partition()
        {
            var partition = _factory.GetOrCreatePartition("Part1");
            partition.CreateEntry("Blue", "Value").Is(true);

            var global = _factory.GetGlobal();
            global.TryGetValue<string>("Blue", out var value).Is(false);
            partition.TryGetValue<string>("Blue", out value).Is(true);
        }

        [Fact]
        public void Cache_Invalidate()
        {
            var p = _factory.GetGlobal();
            p.CreateEntry("Green", "Value").Is(true);

            p.TryGetValue<string>("Green", out _).Is(true);
            p.Invalidate();
            p.TryGetValue<string>("Green", out _).Is(false);
        }

        [Fact]
        public void Wrong_Key_TryGetValue()
        {
            // Setup
            var g = _factory.GetGlobal();
            var p = _factory.GetOrCreatePartition("Matrix5");
            var globalKey = new PartitionObjectKeyString(g.PartitionKey, "Yellow");
            var partitionObjectKey = new PartitionObjectKeyString(p.PartitionKey, "Purple");
            g.CreateEntry(globalKey, "Value").Is(true);
            p.CreateEntry(partitionObjectKey, "Value2").Is(true);

            // Cant get global cache key from Partition
            p.TryGetValue<string>(globalKey, out _).Is(false);
            g.TryGetValue<string>(globalKey, out _).Is(true);

            // Cant get partition key from global
            g.TryGetValue<string>(partitionObjectKey, out _).Is(false);
            p.TryGetValue<string>(partitionObjectKey, out _).Is(true);
        }

        [Fact]
        public void Wrong_Key_Remove()
        {
            // Setup
            var g = _factory.GetGlobal();
            var p = _factory.GetOrCreatePartition("Matrix6");

            var globalKey = new PartitionObjectKeyString(g.PartitionKey, "Yellow2");
            var partitionObjectKey = new PartitionObjectKeyString(p.PartitionKey, "Purple2");

            g.CreateEntry(globalKey, "Value").Is(true);
            p.CreateEntry(partitionObjectKey, "Value2").Is(true);

            // Cant remove global cache key from Partition
            g.Remove(globalKey).Is(true);
            p.Remove(globalKey).Is(false);

            // Cant remove partition key from global
            g.Remove(partitionObjectKey).Is(false);
            p.Remove(partitionObjectKey).Is(true);
        }

        [Fact]
        public void Wrong_Key_Add()
        {
            var g = _factory.GetGlobal();
            var p = _factory.GetOrCreatePartition("Matrix7");

            var globalKey = new PartitionObjectKeyString(g.PartitionKey, "Yellow3");
            var partitionObjectKey = new PartitionObjectKeyString(p.PartitionKey, "Purple3");

            g.CreateEntry(globalKey, "Value").Is(true);
            p.CreateEntry(globalKey, "Value2").Is(false);

            g.CreateEntry(partitionObjectKey, "Value").Is(false);
            p.CreateEntry(partitionObjectKey, "Value2").Is(true);
        }

        [Fact]
        public void InvalidatePartitionDoesntAffectGlobal()
        {
            var g = _factory.GetGlobal();
            var p = _factory.GetOrCreatePartition("Matrix8");

            var globalKey = new PartitionObjectKeyString(g.PartitionKey, "Yellow4");
            var partitionObjectKey = new PartitionObjectKeyString(p.PartitionKey, "Purple4");

            g.CreateEntry(globalKey, "Value").Is(true);
            p.CreateEntry(partitionObjectKey, "Value").Is(true);

            p.TryGetValue<string>(partitionObjectKey, out _).Is(true);
            g.TryGetValue<string>(globalKey, out _).Is(true);

            // Invalidate Partition only
            p.Invalidate();

            // Partition cache not found
            p.TryGetValue<string>(partitionObjectKey, out _).Is(false);
            // Global values still found
            g.TryGetValue<string>(globalKey, out _).Is(true);
        }
    }
}
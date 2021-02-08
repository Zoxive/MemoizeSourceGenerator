using FluentAssertions;
using MemoizeSourceGenerator.Attribute;
using Microsoft.Extensions.Logging.Abstractions;
using SourceGeneratorTests.Extensions;
using Xunit;

namespace SourceGeneratorTests
{
    public class CachePartitionTests
    {
		private readonly IMemoizerFactory _factory = new MemoizerFactory(NullLoggerFactory.Instance);

        [Fact]
        public void GlobalPartitionName()
        {
            var p = _factory.GetGlobal();
            p.DisplayName.Is("|GLOBAL|");
        }

        [Fact]
        public void PartitionName()
        {
            var p = _factory.GetOrCreatePartition("Part1");
            p.DisplayName.Is("|GLOBAL|>Part1");
        }

        [Fact]
        public void CanGetItemFromCache()
        {
            var p = _factory.GetGlobal();

            p.CreateEntry("Test", "Value").Is(true);

            p.TryGetValue<string>("Test", out var value).Is(true);
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
    }
}
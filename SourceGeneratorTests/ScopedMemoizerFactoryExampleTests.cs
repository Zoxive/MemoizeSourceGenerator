#nullable enable
using System.Linq;
using System.Net.Mime;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SourceGeneratorTests.Examples;
using SourceGeneratorTests.Extensions;
using Xunit;
using Zoxive.MemoizeSourceGenerator.Attribute;

namespace SourceGeneratorTests
{
    public class ScopedMemoizerFactoryExampleTests
    {
        private RequestScope _requestScope;

        public ScopedMemoizerFactoryExampleTests()
        {
            GlobalFactory = MemoizerFactory.Global;
            _requestScope = RequestScope.Static("Tenant1");
        }

        public MemoizerFactory GlobalFactory { get; }

        [Fact]
        public void GlobalPartitionName()
        {
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);
            var p = factory.GetGlobal();
            p.DisplayName.Is("Tenant1");
        }

        [Fact]
        public void PartitionName()
        {
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);
            var p = factory.GetOrCreatePartition("Part1");
            p.DisplayName.Is("Tenant1>Part1");

            var factory2 = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, RequestScope.Static("Tenant2"));
            var p2 = factory2.GetOrCreatePartition("Part2");
            p2.DisplayName.Is("Tenant2>Part2");
        }

        [Fact]
        public void ReturnsSamePartition()
        {
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);
            var p = factory.GetOrCreatePartition("Part3");

            // Same as factory1
            var factory2 = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);
            var p2 = factory2.GetOrCreatePartition("Part3");

            ReferenceEquals(p, p2).Is(true);
        }

        [Fact]
        public void TenantGlobal_andGlobalNotSame()
        {
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);
            var tg = factory.GetGlobal();

            var g = GlobalFactory.GetGlobal();

            // Not same
            ReferenceEquals(tg, g).Is(false);

            // Extra checks that when we cache something they are different
            tg.CreateEntry("Key1", "Value1").Is(true);
            g.TryGetValue<string>("Key1", out _).Is(false);
            tg.TryGetValue<string>("Key1", out _).Is(true);
        }

        [Fact]
        public void Partitions()
        {
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, RequestScope.Static("Tenant3"));
            factory.Partitions.Should().BeEmpty();

            var tenantPartition = factory.GetOrCreatePartition("TenantPartition");

            factory.Partitions.Should()
                .ContainSingle()
                .And
                .Contain(tenantPartition);
            GlobalFactory.Partitions.Should().NotContain(tenantPartition);
        }

        #region CopyPastedFromCachePartitionTests
        [Fact]
        public void CanGetItemFromCache()
        {
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);
            var p = factory.GetGlobal();

            p.CreateEntry("Test", "Value").Is(true);

            p.TryGetValue<string>("Test", out var value).Is(true);
            value.Is("Value");
        }

        [Fact]
        public void Partition_Misses_GlobalCache()
        {
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);
            var global = factory.GetGlobal();
            global.CreateEntry("Test2", "Value").Is(true);

            var partition = factory.GetOrCreatePartition("Part1");
            partition.TryGetValue<string>("Test2", out var value).Is(false);
            global.TryGetValue<string>("Test2", out value).Is(true);
        }

        [Fact]
        public void Global_Misses_Partition()
        {
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);
            var partition = factory.GetOrCreatePartition("Part1");
            partition.CreateEntry("Blue", "Value").Is(true);

            var global = factory.GetGlobal();
            global.TryGetValue<string>("Blue", out var value).Is(false);
            partition.TryGetValue<string>("Blue", out value).Is(true);
        }

        [Fact]
        public void Cache_Invalidate()
        {
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);
            var p = factory.GetGlobal();
            p.CreateEntry("Green", "Value").Is(true);

            p.TryGetValue<string>("Green", out _).Is(true);
            p.Invalidate();
            p.TryGetValue<string>("Green", out _).Is(false);
        }

        [Fact]
        public void Wrong_Key_TryGetValue()
        {
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);
            // Setup
            var g = factory.GetGlobal();
            var p = factory.GetOrCreatePartition("Matrix5");
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
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);

            // Setup
            var g = factory.GetGlobal();
            var p = factory.GetOrCreatePartition("Matrix6");

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
            var factory = new TenantSpecificMemoizerFactory(NullLoggerFactory.Instance, _requestScope);

            var g = factory.GetGlobal();
            var p = factory.GetOrCreatePartition("Matrix7");

            var globalKey = new PartitionObjectKeyString(g.PartitionKey, "Yellow3");
            var partitionObjectKey = new PartitionObjectKeyString(p.PartitionKey, "Purple3");

            g.CreateEntry(globalKey, "Value").Is(true);
            p.CreateEntry(globalKey, "Value2").Is(false);

            g.CreateEntry(partitionObjectKey, "Value").Is(false);
            p.CreateEntry(partitionObjectKey, "Value2").Is(true);
        }
        #endregion
    }
}
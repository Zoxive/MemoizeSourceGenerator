using System;
using System.Threading;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SourceGeneratorTests.Extensions;
using Xunit;
using Xunit.Abstractions;
using Zoxive.MemoizeSourceGenerator.Attribute;

namespace SourceGeneratorTests
{
    public class CacheStatsTests
    {
		private readonly ITestOutputHelper _output;
		private readonly IMemoizerFactory _factory;

		public CacheStatsTests(ITestOutputHelper output)
		{
			_output = output;
			_factory = new MemoizerFactory(new StringPartitionKey("CacheStatsTest"), NullLoggerFactory.Instance, new MemoryCacheOptions
			{
				// Check every call for test purposes
				ExpirationScanFrequency = TimeSpan.FromTicks(1)
			});
	}

		[Fact]
		public void Global_Empty()
		{
			var p = _factory.GetGlobal();
			IsEmpty(p);
		}

		[Fact]
		public void Global_OneItem()
		{
			var p = _factory.GetGlobal();
			IsEmpty(p);

			p.CreateEntry("Key", "Value").Is(true);

			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 0, 0, 1, 0));

			// Access
			p.TryGetValue<string>("Key", out _).Is(true);
			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 1, 0, 1, 0));

			// Miss - counts as access count
			p.TryGetValue<string>("KeyNotFound", out _).Is(false);
			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 1, 1, 1, 0));
		}

		[Fact]
		public void Global_OneItem_Miss()
		{
			var p = _factory.GetGlobal();
			IsEmpty(p);

			// Straight Miss
			p.TryGetValue<string>("KeyNotFound", out _).Is(false);
			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 1, 1, 0, 0));
		}

		[Fact]
		public void Partition_Empty()
		{
			var p = _factory.GetOrCreatePartition("Matrix1");
			IsEmpty(p);
		}

		[Fact]
		public void Partition_OneItem()
		{
			var p = _factory.GetOrCreatePartition("Matrix1");
			IsEmpty(p);

			p.CreateEntry("Key2", "Value").Is(true);

			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 0, 0, 1, 0));

			// Access
			p.TryGetValue<string>("Key2", out _).Is(true);
			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 1, 0, 1, 0));

			// Miss - counts as access count
			p.TryGetValue<string>("KeyNotFound", out _).Is(false);
			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 1, 1, 1, 0));
		}

		[Fact]
		public void Invalidate_RemovesEntry()
		{
			var p = _factory.GetOrCreatePartition("Matrix3");
			IsEmpty(p);

			var removeInvoked = new ManualResetEvent(false);

			p.CreateEntry("Key", "Value").Is(true);

			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 0, 0, 1, 0));

			p.Invalidate();

			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 0, 0, 0, 0));
		}

		[Fact]
		public void Invalidate_RemovesEntry_CallsPostEvictionCallback()
		{
			var p = _factory.GetOrCreatePartition("Matrix1");
			IsEmpty(p);

			var removeInvoked = new ManualResetEvent(false);

			void ConfigureEntry(ICacheEntry entry)
			{
				entry.RegisterPostEvictionCallback((key, value, reason, state) =>
				{
					((ManualResetEvent)state).Set();
				}, removeInvoked);
			}

			p.CreateEntry("Key3", "Value", ConfigureEntry).Is(true);

			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 0, 0, 1, 0));

			p.Invalidate();

			Assert.True(removeInvoked.WaitOne(TimeSpan.FromSeconds(30)));

			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 0, 0, 0, 0));
		}

		[Fact]
		public void Entry_CreatedWithExpiredToken()
		{
			var p = _factory.GetOrCreatePartition("Matrix2");
			IsEmpty(p);

			var removeInvoked = new ManualResetEvent(false);

			void ConfigureEntry(ICacheEntry entry)
			{
				entry.RegisterPostEvictionCallback((key, value, reason, state) =>
				{
					((ManualResetEvent)state).Set();
				}, removeInvoked);
			}

			// Token grabbed
			var token = p.ClearCacheTokenSource;

			// But another process invalidates..
			p.Invalidate();

			// Entry is created with an AbsoluteExpiration of 1 tick
			p.CreateEntry("Key", "Value", token, 10, null, ConfigureEntry).Is(true);

			// Trigger remove scan but also notice its already missing when trying to grab it
			p.TryGetValue<string>("Key", out _).Is(false);

			Assert.True(removeInvoked.WaitOne(TimeSpan.FromSeconds(10)));

			p.GetStatistics().Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 1, 1, 0, 0));
		}

		private static void IsEmpty(CachePartition p)
		{
			var stats = p.GetStatistics();
			stats.Should()
				.BeEquivalentTo(new CacheStatistics(p.DisplayName, 0, 0, 0, 0));
		}
    }
}

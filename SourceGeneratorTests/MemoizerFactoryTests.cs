using System;
using System.Linq;
using FluentAssertions;
using MemoizeSourceGenerator.Attribute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SourceGeneratorTests.Extensions;
using SourceGeneratorTests.GenTests;
using Xunit;
using Memoized;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SourceGeneratorTests.Examples;

namespace SourceGeneratorTests
{
    public class MemoizerFactoryTests
    {
        private readonly IMemoizerFactory _globalCache;
        public MemoizerFactoryTests()
        {

            var serviceProvider = new ServiceCollection()
                .AddLogging(o =>
                {
                    o.SetMinimumLevel(LogLevel.Debug);
                })
                .AddMemoizedSingleton<ISimpleValues, SimpleValues>()
                .BuildServiceProvider();
            _globalCache = serviceProvider.GetRequiredService<IMemoizerFactory>();

            // Clear everything
            _globalCache.InvalidateAll();
        }

        [Fact]
        public void GlobalIsRegisteredInIoc()
        {
            ReferenceEquals(_globalCache, MemoizerFactory.Global).Is(true);
        }

        [Fact]
        public void CanCreateSeperateInstanceWithDifferentPartitions()
        {
            var myFactoryWithItsOwnPartitions = new MemoizerFactory(new StringPartitionKey("Instance1"), NullLoggerFactory.Instance);

            ReferenceEquals(_globalCache, myFactoryWithItsOwnPartitions).Is(false);

            var globalP = _globalCache.GetOrCreatePartition("PartitionXYZ");
            var myP = myFactoryWithItsOwnPartitions.GetOrCreatePartition("PartitionXYZ");

            ReferenceEquals(globalP, myP).Is(false);

            globalP.DisplayName.Is("GLOBAL>PartitionXYZ");
            myP.DisplayName.Is("Instance1>PartitionXYZ");

            myFactoryWithItsOwnPartitions.Partitions.Should()
                .ContainSingle()
                .And.Contain(myP);

            // Global doesnt contain
            _globalCache.Partitions
                .Should()
                .NotContain(myP)
                .And
                .Contain(globalP);
        }
    }


}
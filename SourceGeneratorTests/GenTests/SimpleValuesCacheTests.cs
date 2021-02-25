using System;
using Microsoft.Extensions.DependencyInjection;
using SourceGeneratorTests.Extensions;
using Xunit;
using Zoxive.Memoized;
using Microsoft.Extensions.Logging;
using Moq;
using SourceGeneratorTests.Examples;
using Zoxive.MemoizeSourceGenerator.Attribute;

namespace SourceGeneratorTests.GenTests
{
    public class SimpleValuesCacheTests
    {
        private readonly ISimpleValues _sut;
        private readonly IMemoizerFactory _globalCache;

        public SimpleValuesCacheTests()
        {
            var serviceProvider = new ServiceCollection()
                .AddLogging(o =>
                {
                    o.SetMinimumLevel(LogLevel.Debug);
                })
                .AddMemoizedSingleton<ISimpleValues, SimpleValues>()
                .BuildServiceProvider();
            _sut = serviceProvider.GetRequiredService<ISimpleValues>();
            _globalCache = serviceProvider.GetRequiredService<IMemoizerFactory>();

            // Clear everything
            _globalCache.InvalidateAll();
        }

        [Fact]
        public void ResolvedServiceIsMemoized()
        {
            _sut.GetType().Name.Is("Memoized_SimpleValues");
        }

        [Fact]
        public void CachesResult()
        {
            var mockLogger = AssertExtensions.MockedDebugLogger<Memoized_SimpleValues>();

            var sut = new Memoized_SimpleValues(_globalCache, new SimpleValues(), mockLogger.Object);

            var p = _globalCache.GetGlobal();
            var cacheObjectKey = new Memoized_SimpleValues.ArgKey_ISimpleValues_int_Add_int_int(p.PartitionKey, 1, 2);

            // Cache doesnt exist
            p.TryGetValue<int>(cacheObjectKey, out _).Is(false);

            // Hit the interface like normal
            sut.Add(1, 2).Is(3);
            mockLogger.VerifyDebugWasCalled("Cache miss. GLOBAL~ISimpleValues.Add(1, 2) => 3");
            mockLogger.VerifyDebugWasCalledTimes(Times.Exactly(1));

            // Cache Exists
            sut.Add(1, 2).Is(3);
            mockLogger.VerifyDebugWasCalled("Cache hit. GLOBAL~ISimpleValues.Add(1, 2) => 3");
            mockLogger.VerifyDebugWasCalledTimes(Times.Exactly(2));
            p.TryGetValue<int>(cacheObjectKey, out var value).Is(true);
            value.Is(3);
        }

        [Fact]
        public void CachesPartitionResult()
        {
            var mockLogger = AssertExtensions.MockedDebugLogger<Memoized_SimpleValues>();

            var sut = new Memoized_SimpleValues(_globalCache, new SimpleValues(), mockLogger.Object);

            var name = "Bob";

            var p = _globalCache.GetOrCreatePartition(name);
            var cacheObjectKey = new Memoized_SimpleValues.ArgKey_ISimpleValues_decimal_GetPrice_string(p.PartitionKey, name);

            // Cache doesnt exist
            p.TryGetValue<int>(cacheObjectKey, out _).Is(false);

            // Hit the interface like normal
            sut.GetPrice(name).Is(6);
            mockLogger.VerifyDebugWasCalled("Cache miss. GLOBAL>Bob~ISimpleValues.GetPrice(Bob) => 6");
            mockLogger.VerifyDebugWasCalledTimes(Times.Exactly(1));

            // Cache Exists
            sut.GetPrice(name).Is(6);
            mockLogger.VerifyDebugWasCalled("Cache hit. GLOBAL>Bob~ISimpleValues.GetPrice(Bob) => 6");
            mockLogger.VerifyDebugWasCalledTimes(Times.Exactly(2));
            p.TryGetValue<decimal>(cacheObjectKey, out var value).Is(true);
            value.Is(6);
        }
    }


}
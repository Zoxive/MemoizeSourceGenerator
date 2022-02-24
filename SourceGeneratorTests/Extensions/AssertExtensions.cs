using System;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Primitives;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Zoxive.MemoizeSourceGenerator.Attribute;

namespace SourceGeneratorTests.Extensions
{
    public static class AssertExtensions
    {
        public static void Is<TType>(this TType actual, TType expected)
        {
            actual.Should().Be(expected);
        }

        public static ObjectAssertions HaveMisses(this ObjectAssertions objectAssertions, int misses)
        {
            (objectAssertions.Subject as CacheStatistics)!.Misses.Should().Be(misses);
            return objectAssertions;
        }

        public static ObjectAssertions HaveAccessCount(this ObjectAssertions objectAssertions, int count)
        {
            (objectAssertions.Subject as CacheStatistics)!.AccessCount.Should().Be(count);
            return objectAssertions;
        }

        public static ObjectAssertions HaveEntryCount(this ObjectAssertions objectAssertions, int count)
        {
            (objectAssertions.Subject as CacheStatistics)!.EntryCount.Should().Be(count);
            return objectAssertions;
        }

        public static ObjectAssertions HaveTotalSize(this ObjectAssertions objectAssertions, int totalSize)
        {
            (objectAssertions.Subject as CacheStatistics)!.TotalSize.Should().Be(totalSize);
            return objectAssertions;
        }

        // Make unit tests easier to read by defaulting some params
        public static bool CreateEntry<T>(this CachePartition cache, string key, T value, Action<ICacheEntry> configureEntry = null)
        {
            return cache.CreateEntry(key, value, cache.ClearCacheTokenSource, 10, null, configureEntry);
        }

        public static bool CreateEntry<T>(this CachePartition cache, IPartitionObjectKey key, T value, Action<ICacheEntry> configureEntry = null)
        {
            return cache.CreateEntry(key, value, cache.ClearCacheTokenSource, 10, null, configureEntry);
        }

        public static Mock<ILogger<T>> VerifyWasCalled<T>(this Mock<ILogger<T>> logger, string expectedMessage, LogLevel logLevel)
        {
            Func<object, Type, bool> state = (v, t) => string.Equals(v.ToString(), expectedMessage, StringComparison.Ordinal);

            logger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == logLevel),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => state(v, t)),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)));

            return logger;
        }

        public static Mock<ILogger<T>> VerifyWasCalledTimes<T>(this Mock<ILogger<T>> logger, Times times, LogLevel logLevel)
        {
            logger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == logLevel),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), times);

            return logger;
        }

        public static Mock<ILogger<T>> VerifyDebugWasCalled<T>(this Mock<ILogger<T>> logger, string expectedMessage)
        {
            return VerifyWasCalled(logger, expectedMessage, LogLevel.Debug);
        }

        public static Mock<ILogger<T>> VerifyDebugWasCalledTimes<T>(this Mock<ILogger<T>> logger, Times times)
        {
            return VerifyWasCalledTimes(logger, times, LogLevel.Debug);
        }

        public static Mock<ILogger<T>> VerifyTraceWasCalled<T>(this Mock<ILogger<T>> logger, string expectedMessage)
        {
            return VerifyWasCalled(logger, expectedMessage, LogLevel.Trace);
        }

        public static Mock<ILogger<T>> VerifyTraceWasCalledTimes<T>(this Mock<ILogger<T>> logger, Times times)
        {
            logger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Trace),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), times);

            return logger;
        }

        public static Mock<ILogger<T>> MockedDebugLogger<T>()
        {
            var mockLogger = new Mock<ILogger<T>>();
            mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            return mockLogger;
        }
    }
}
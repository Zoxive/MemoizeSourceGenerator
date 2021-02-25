using System;
using Zoxive.Memoized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SourceGeneratorTests.Examples;
using SourceGeneratorTests.Extensions;
using Xunit;

namespace SourceGeneratorTests.GenTests
{
    public class ScopedClassWithTenantSpecificMemoizerTests
    {
        private readonly ServiceProvider _serviceProvider;
        private Mock<ILogger> _mockLogger;

        public ScopedClassWithTenantSpecificMemoizerTests()
        {
            _serviceProvider = new ServiceCollection()
                .AddSingleton<ILoggerFactory, MockableLoggerFactory>()
                .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
                .AddScoped<TenantSpecificMemoizerFactory>()
                .AddScoped<RequestScope>()
                .AddMemoizedScoped<IScopedClassExample, ScopedClassExample>()
                .BuildServiceProvider();

            _mockLogger = MockableLoggerFactory.Logger;
            _mockLogger.Invocations.Clear();
        }

        [Fact]
        public void ResolvedServiceIsMemoized()
        {
            using (var scope = ScopeFor("Tenant1"))
            {
                scope.Sut.GetType().Name.Is("Memoized_ScopedClassExample");

                scope.TenantSpecificFactory.FactoryKey.DisplayName.Is("Tenant1");
            }

            using (var scope = ScopeFor("Tenant2"))
            {
                scope.Sut.GetType().Name.Is("Memoized_ScopedClassExample");

                scope.TenantSpecificFactory.FactoryKey.DisplayName.Is("Tenant2");
            }
        }

        private Scope ScopeFor(string app)
        {
            return new Scope(_serviceProvider.CreateScope(), app);
        }

        [Fact]
        public void CachesResult()
        {
            using (var scope = ScopeFor("Tenant1"))
            {
                // Hit the interface like normal
                scope.Sut.Add(1, 2).Is(new ValueType1(3));
                _mockLogger.VerifyDebugWasCalled("Cache miss. Tenant1~IScopedClassExample.Add(1, 2) => ValueType1-3");
                _mockLogger.VerifyDebugWasCalledTimes(Times.Exactly(1));

                // Cache Exists
                scope.Sut.Add(1, 2).Is(new ValueType1(3));
                _mockLogger.VerifyDebugWasCalled("Cache hit. Tenant1~IScopedClassExample.Add(1, 2) => ValueType1-3");
                _mockLogger.VerifyDebugWasCalledTimes(Times.Exactly(2));
            }
        }

        [Fact]
        public void CachesPartitionResult()
        {
            var name = "Bob";

            using (var scope = ScopeFor("Tenant1"))
            {
                // Hit the interface like normal
                scope.Sut.TestPartition(name, 3).Is(new ValueType1(9));
                _mockLogger.VerifyDebugWasCalled("Cache miss. Tenant1>Bob~IScopedClassExample.TestPartition(Bob, 3) => ValueType1-9");
                _mockLogger.VerifyDebugWasCalledTimes(Times.Exactly(1));

                // Cache Exists
                scope.Sut.TestPartition(name, 3).Is(new ValueType1(9));
                _mockLogger.VerifyDebugWasCalled("Cache hit. Tenant1>Bob~IScopedClassExample.TestPartition(Bob, 3) => ValueType1-9");
                _mockLogger.VerifyDebugWasCalledTimes(Times.Exactly(2));
            }
        }
    }

    internal class Scope : IDisposable
    {
        private readonly IServiceScope _serviceScope;

        public Scope(IServiceScope serviceScope, string app)
        {
            _serviceScope = serviceScope;

            // Purposely resolve class before app is set since that can happen
            Sut = _serviceScope.ServiceProvider.GetRequiredService<IScopedClassExample>();
            TenantSpecificFactory = _serviceScope.ServiceProvider.GetRequiredService<TenantSpecificMemoizerFactory>();

            _serviceScope.ServiceProvider
                .GetRequiredService<RequestScope>()
                .SetApplication(app);
        }

        public TenantSpecificMemoizerFactory TenantSpecificFactory { get; }

        public IScopedClassExample Sut { get; }

        public void Dispose()
        {
            _serviceScope.Dispose();
        }
    }
}
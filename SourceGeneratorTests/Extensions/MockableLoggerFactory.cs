using System;
using Microsoft.Extensions.Logging;
using Moq;

namespace SourceGeneratorTests.Extensions
{
    public class MockableLoggerFactory : ILoggerFactory
    {
        public static Mock<ILogger<MockableLoggerFactory>> Logger = new Mock<ILogger<MockableLoggerFactory>>();

        static MockableLoggerFactory()
        {
            Logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return Logger.Object;
        }

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() {}
    }
}
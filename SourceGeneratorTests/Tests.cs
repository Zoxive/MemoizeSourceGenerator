using Xunit;
using Xunit.Abstractions;

namespace SourceGeneratorTests
{
    public class Tests
    {
		private readonly ITestOutputHelper _output;
		public Tests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void Test1()
		{
			//
		}
    }
}

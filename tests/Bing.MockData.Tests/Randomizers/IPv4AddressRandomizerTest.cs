using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Randomizers
{
    public class IPv4AddressRandomizerTest:TestBase
    {
        public IPv4AddressRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            var randomizer = RandomizerFactory.GetRandomizer(new IPv4AddressFieldOptions());
            for (var i = 0; i < 100; i++)
            {
                var result = randomizer.Generate();
                Output.WriteLine(result);
            }
        }
    }
}

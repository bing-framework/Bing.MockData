using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Randomizers
{
    public class MACAddressRandomizerTest:TestBase
    {
        public MACAddressRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            var randomizer = RandomizerFactory.GetRandomizer(new MACAddressFieldOptions());
            for (var i = 0; i < 100; i++)
            {
                var result = randomizer.Generate();
                Output.WriteLine(result);
            }
        }
    }
}

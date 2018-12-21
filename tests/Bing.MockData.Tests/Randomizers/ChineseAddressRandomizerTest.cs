using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Randomizers
{
    public class ChineseAddressRandomizerTest:TestBase
    {
        public ChineseAddressRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            var randomizer = RandomizerFactory.GetRandomizer(new ChineseAddressFieldOptions());
            for (var i = 0; i < 100; i++)
            {
                var result = randomizer.Generate();
                var region = randomizer.GenerateRegion();
                Output.WriteLine($"{result}/{region}");
            }
        }
    }
}

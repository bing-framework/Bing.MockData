using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

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
            AssertSeeded<MACAddressFieldOptions, string>(
                new MACAddressFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.Matches(@"^(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$", value));
        }
    }
}

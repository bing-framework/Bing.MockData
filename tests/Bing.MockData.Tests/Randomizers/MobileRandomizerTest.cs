using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{    
    public class MobileRandomizerTest:TestBase
    {
        public MobileRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<MobileFieldOptions, string>(
                new MobileFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.Matches(@"^[0-9]{11}$", value));
        }
    }
}

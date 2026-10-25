using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class ChineseNameRandomizerTest:TestBase
    {
        public ChineseNameRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<ChineseNameFieldOptions, string>(
                new ChineseNameFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.False(string.IsNullOrWhiteSpace(value)));
        }
    }
}

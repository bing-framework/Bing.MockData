using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class TextWordsRandomizerTest:TestBase
    {
        public TextWordsRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<TextWordsFieldOptions, string>(
                new TextWordsFieldOptions { Min = 3, Max = 20 },
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.InRange(
                    value.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries).Length,
                    3,
                    20));
        }
    }
}

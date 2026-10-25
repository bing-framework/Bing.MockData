using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class NumberRandomizerTest:TestBase
    {
        public NumberRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<NumberFieldOptions<int>, int>(
                new NumberFieldOptions<int> { Min = 0, Max = 1000 },
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer<int>(options);
                    return randomizer.Generate;
                },
                value => Assert.InRange(value, 0, 1000));
        }
    }
}

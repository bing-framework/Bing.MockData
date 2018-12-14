using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;
using Xunit.Abstractions;

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
            var randomizer = RandomizerFactory.GetRandomizer<int>(new NumberFieldOptions<int>());
            for (var i = 0; i < 100; i++)
            {
                var result = randomizer.Generate();
                Output.WriteLine(result.ToString());
            }
        }
    }
}

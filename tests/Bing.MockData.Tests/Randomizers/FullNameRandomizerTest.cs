using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class FullNameRandomizerTest:TestBase
    {
        public FullNameRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<FullNameFieldOptions, string>(
                new FullNameFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.False(string.IsNullOrWhiteSpace(value)));
        }
    }
}

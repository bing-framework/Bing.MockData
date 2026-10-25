using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

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
            AssertSeeded<ChineseAddressFieldOptions, (string, string)>(
                new ChineseAddressFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return () => (randomizer.Generate(), randomizer.GenerateRegion());
                },
                value =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(value.Item1));
                    Assert.False(string.IsNullOrWhiteSpace(value.Item2));
                });
        }
    }
}

using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Randomizers
{
    public class DateTimeRandomizerTest:TestBase
    {
        public DateTimeRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            var randomizer = RandomizerFactory.GetRandomizer(new DateTimeFieldOptions());
            for (var i = 0; i < 100; i++)
            {
                var result = randomizer.GenerateAsString();
                Output.WriteLine(result);
            }
        }
    }
}

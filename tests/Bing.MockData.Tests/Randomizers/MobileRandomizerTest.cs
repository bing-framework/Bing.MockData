using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;
using Xunit.Abstractions;

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
            var randomizer = RandomizerFactory.GetRandomizer(new MobileFieldOptions());
            for (var i = 0; i < 100; i++)
            {
                var result = randomizer.Generate();
                Output.WriteLine(result);
            }            
        }
    }
}

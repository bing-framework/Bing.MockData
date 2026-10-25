using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class IPv4AddressRandomizerTest:TestBase
    {
        public IPv4AddressRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<IPv4AddressFieldOptions, string>(
                new IPv4AddressFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value =>
                {
                    Assert.True(System.Net.IPAddress.TryParse(value, out var address));
                    Assert.Equal(System.Net.Sockets.AddressFamily.InterNetwork, address.AddressFamily);
                });
        }
    }
}

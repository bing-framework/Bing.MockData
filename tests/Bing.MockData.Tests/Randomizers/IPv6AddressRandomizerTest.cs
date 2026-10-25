using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class IPv6AddressRandomizerTest:TestBase
    {
        public IPv6AddressRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<IPv6AddressFieldOptions, string>(
                new IPv6AddressFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value =>
                {
                    Assert.True(System.Net.IPAddress.TryParse(value, out var address));
                    Assert.Equal(System.Net.Sockets.AddressFamily.InterNetworkV6, address.AddressFamily);
                });
        }
    }
}

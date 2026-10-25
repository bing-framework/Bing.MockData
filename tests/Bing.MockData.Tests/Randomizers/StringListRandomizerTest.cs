using System.Collections.Generic;
using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class StringListRandomizerTest:TestBase
    {
        public StringListRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            var values = new List<string> { "张三", "李四", "王五" };
            AssertSeeded<StringListFieldOptions, string>(
                new StringListFieldOptions { Values = values },
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.Contains(value, values));
        }
    }
}

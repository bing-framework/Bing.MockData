using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>模拟中文地址生成器。</summary>
    public class ChineseAddressRandomizer : RandomizerBase<ChineseAddressFieldOptions>, IAddressRandomizer
    {
        private readonly RandomGenerator _generator;
        /// <summary>复制生成配置。</summary>
        public ChineseAddressRandomizer(ChineseAddressFieldOptions options) : base(options)
        { _generator = new RandomGenerator(Rnd); }
        /// <summary>生成地址。</summary>
        public string Generate() => Execute(() =>
        {
            var cities = CommonData.Instance.ProvinceCityList;
            return cities[Rnd.Next(cities.Count)] + _generator.GenerateRandomLengthChinese(2, 4) + "路" +
                RandomValueGenerator.NextInt(Rnd, 1, 8000) + "号" + _generator.GenerateRandomLengthChinese(2, 4) + "小区" +
                RandomValueGenerator.NextInt(Rnd, 1, 20) + "单元" + RandomValueGenerator.NextInt(Rnd, 101, 2500) + "室";
        });
        /// <summary>生成地区。</summary>
        public string GenerateRegion() => Execute(() => CommonData.Instance.RegionList[Rnd.Next(CommonData.Instance.RegionList.Count)]);
    }
}

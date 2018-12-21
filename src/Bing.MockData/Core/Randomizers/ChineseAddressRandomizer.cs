using System.Text;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 地址随机生成器
    /// </summary>
    public class ChineseAddressRandomizer:RandomizerBase<ChineseAddressFieldOptions>,IAddressRandomizer
    {
        /// <summary>
        /// 生成器
        /// </summary>
        private readonly RandomGenerator _generator;

        /// <summary>
        /// 初始化一个<see cref="ChineseAddressRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">地址配置</param>
        public ChineseAddressRandomizer(ChineseAddressFieldOptions options) : base(options)
        {
            _generator = new RandomGenerator();
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(GetProvinceAndCity());
            sb.Append(_generator.GenerateRandomLengthChinese(2, 4) + "路");
            sb.Append(_generator.GenerateInt(1, 8001) + "号");
            sb.Append(_generator.GenerateRandomLengthChinese(2, 4) + "小区");
            sb.Append(_generator.GenerateInt(1, 21) + "单元");
            sb.Append(_generator.GenerateInt(101, 2501) + "室");
            return sb.ToString();
        }

        /// <summary>
        /// 生成大区
        /// </summary>
        /// <returns></returns>
        public string GenerateRegion()
        {
            return ListData.Instance.RegionList[_generator.GenerateInt(0, ListData.Instance.RegionList.Count)];
        }

        /// <summary>
        /// 获取省份城市
        /// </summary>
        /// <returns></returns>
        private string GetProvinceAndCity()
        {
            return ListData.Instance.ProvinceCityList[
                _generator.GenerateInt(0, ListData.Instance.ProvinceCityList.Count)];
        }
    }
}

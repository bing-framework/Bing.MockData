using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 姓氏随机生成器
    /// </summary>
    public class LastNameRandomizer : RandomizerBase<LastNameFieldOptions>, IStringRandomizer
    {
        /// <summary>
        /// 生成器
        /// </summary>
        private readonly RandomStringFromListGenerator _generator;

        /// <summary>
        /// 初始化一个<see cref="LastNameRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">姓氏配置</param>
        public LastNameRandomizer(LastNameFieldOptions options) : base(options)
        {
            _generator = new RandomStringFromListGenerator(ListData.Instance.LastNames);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            return IsNull() ? null : _generator.Generate();
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="upperCase">是否大写</param>
        /// <returns></returns>
        public string Generate(bool upperCase)
        {
            return Generate().ToCasedInvariant(upperCase);
        }
    }
}

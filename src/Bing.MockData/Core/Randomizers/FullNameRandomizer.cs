using System.Collections.Generic;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 全名随机生成器
    /// </summary>
    public class FullNameRandomizer:RandomizerBase<FullNameFieldOptions>,IStringRandomizer
    {
        /// <summary>
        /// 数字生成器
        /// </summary>
        private readonly RandomThingsGenerator<int> _numberGenerator;

        /// <summary>
        /// 姓氏生成器
        /// </summary>
        private readonly RandomStringFromListGenerator _lastNameGenerator;

        /// <summary>
        /// 性别生成器
        /// </summary>
        private readonly List<RandomStringFromListGenerator> _genderSetGenerators =
            new List<RandomStringFromListGenerator>();

        /// <summary>
        /// 初始化一个<see cref="FullNameRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">全名配置</param>
        public FullNameRandomizer(FullNameFieldOptions options) : base(options)
        {
            _lastNameGenerator = new RandomStringFromListGenerator(CommonData.Instance.LastNames);

            if (options.Male)
            {
                _genderSetGenerators.Add(new RandomStringFromListGenerator(CommonData.Instance.MaleNames));
            }

            if (options.Female)
            {
                _genderSetGenerators.Add(new RandomStringFromListGenerator(CommonData.Instance.FemaleNames));
            }

            _numberGenerator = new RandomThingsGenerator<int>(0, _genderSetGenerators.Count);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            if (IsNull())
            {
                return null;
            }

            int maleOrFemale = _numberGenerator.Generate();

            var firstNamesSet = _genderSetGenerators[maleOrFemale];
            string firstName = firstNamesSet.Generate();
            string lastName = _lastNameGenerator.Generate();

            return $"{firstName} {lastName}";
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

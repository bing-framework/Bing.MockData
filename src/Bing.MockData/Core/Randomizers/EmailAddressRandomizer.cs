using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
using NLipsum.Core;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 邮箱地址随机生成器
    /// </summary>
    public class EmailAddressRandomizer:RandomizerBase<EmailAddressFieldOptions>,IStringRandomizer
    {
        /// <summary>
        /// 生成器
        /// </summary>
        private readonly LipsumGenerator _generator = new LipsumGenerator();

        /// <summary>
        /// 数字生成器
        /// </summary>
        private readonly RandomThingsGenerator<int> _numberGenerator;

        /// <summary>
        /// 顶级域名生成器
        /// </summary>
        private readonly RandomStringFromListGenerator _topLevelDomainGenerator;

        /// <summary>
        /// 姓氏生成器
        /// </summary>
        private readonly RandomStringFromListGenerator _lastNamesGenerator;

        /// <summary>
        /// 性别生成器
        /// </summary>
        private readonly List<RandomStringFromListGenerator> _genderSetGenerators =
            new List<RandomStringFromListGenerator>();

        /// <summary>
        /// 初始化一个<see cref="EmailAddressRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">邮箱地址配置</param>
        public EmailAddressRandomizer(EmailAddressFieldOptions options) : base(options)
        {
            _lastNamesGenerator =
                new RandomStringFromListGenerator(ListData.Instance.LastNames.Select(l => l.ToLower()));
            _topLevelDomainGenerator =
                new RandomStringFromListGenerator(ListData.Instance.TopLevelDomains.Select(l => l.ToLower()));
            if (options.Male)
            {
                _genderSetGenerators.Add(
                    new RandomStringFromListGenerator(ListData.Instance.MaleNames.Select(l => l.ToLower())));
            }

            if (options.Female)
            {
                _genderSetGenerators.Add(
                    new RandomStringFromListGenerator(ListData.Instance.FemaleNames.Select(l => l.ToLower())));
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

            var firstName = firstNamesSet.Generate();
            var lastName = _lastNamesGenerator.Generate();
            var company = _generator.RandomWord();
            var domain = _topLevelDomainGenerator.Generate();

            return $"{firstName}.{lastName}@{company}.{domain}";
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

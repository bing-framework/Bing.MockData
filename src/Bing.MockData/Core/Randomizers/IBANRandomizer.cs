using System;
using System.Linq;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Datas.Models;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 银行账号随机生成器
    /// </summary>
    public class IBANRandomizer:RandomizerBase<IBANFieldOptions>,IStringRandomizer
    {
        /// <summary>
        /// 项生成器
        /// </summary>
        private readonly RandomItemFromListGenerator<IBAN> _itemGenerator;

        /// <summary>
        /// 初始化一个<see cref="IBANRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">银行账号配置</param>
        public IBANRandomizer(IBANFieldOptions options) : base(options)
        {
            Func<IBAN, bool> predicate = null;
            if (!string.IsNullOrEmpty(options.CountryCode))
            {
                predicate = (iban) => iban.CountryCode == options.CountryCode;
            }

            var list = ListData.Instance.IBANs;
            switch (options.Type)
            {
                case "BBAN":
                    list = ListData.Instance.BBANs;
                    break;
                case "BOTH":
                    list = list.Union(ListData.Instance.BBANs);
                    break;
            }

            _itemGenerator = new RandomItemFromListGenerator<IBAN>(list, predicate);
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

            var iban = _itemGenerator.Generate();
            return iban.Generator.Generate();
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

using System;
using System.Globalization;
using Bing.MockData.Core.Options;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 随机生成器基类
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    public abstract class RandomizerBase<TOptions> where TOptions : FieldOptionsBase
    {
        /// <summary>
        /// 随机数器
        /// </summary>
        protected readonly Random Rnd =
            new Random(int.Parse(Guid.NewGuid().ToString().Substring(0, 8), NumberStyles.HexNumber));

        /// <summary>
        /// 字段配置
        /// </summary>
        protected readonly TOptions Options;

        /// <summary>
        /// 初始化一个<see cref="RandomizerBase{TOptions}"/>类型的实例
        /// </summary>
        /// <param name="options">字段配置</param>
        protected RandomizerBase(TOptions options)
        {
            Options = options;
        }

        /// <summary>
        /// 是否为空
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsNull()
        {
            return Options.UseNullValues && Rnd.Next(0, 10) == 5;
        }
    }
}

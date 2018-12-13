using System;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 时间跨度随机生成器
    /// </summary>
    public class TimeSpanRandomizer:RandomizerBase<TimeSpanFieldOptions>,ITimeSpanRandomizer
    {
        /// <summary>
        /// 生成器
        /// </summary>
        private readonly RandomThingsGenerator<TimeSpan> _generator;

        /// <summary>
        /// 初始化一个<see cref="TimeSpanRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">时间跨度配置</param>
        public TimeSpanRandomizer(TimeSpanFieldOptions options) : base(options)
        {
            _generator = new RandomThingsGenerator<TimeSpan>(options.From, options.To);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public TimeSpan? Generate()
        {
            if (IsNull())
            {
                return null;
            }

            var ts = _generator.Generate();
            return Options.IncludeMilliseconds ? ts : new TimeSpan(ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
        }

        /// <summary>
        /// 生成并转换成字符串
        /// </summary>
        /// <returns></returns>
        public string GenerateAsString()
        {
            TimeSpan? ts = Generate();

            var format = !string.IsNullOrEmpty(Options.Format) ? Options.Format : TimeSpanFieldOptions.DefaultFormat;
            return ts != null ? string.Format($"{{0:{format}}}", ts) : null;
        }
    }
}

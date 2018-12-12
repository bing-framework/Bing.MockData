using System;
using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 时间跨度字段配置
    /// </summary>
    public class TimeSpanFieldOptions:FieldOptionsBase,ITimeSpanFieldOptions
    {
        /// <summary>
        /// 默认格式。c -> 常量格式
        /// </summary>
        public const string DefaultFormat = "c";

        /// <summary>
        /// 格式化。
        /// 请参考：https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-timespan-format-strings
        /// </summary>
        public string Format { get; set; } = DefaultFormat;

        /// <summary>
        /// 开始-从
        /// </summary>
        public TimeSpan From { get; set; }

        /// <summary>
        /// 结束-至
        /// </summary>
        public TimeSpan To { get; set; } = TimeSpan.MaxValue;

        /// <summary>
        /// 是否包含毫秒数
        /// </summary>
        public bool IncludeMilliseconds { get; set; } = true;
    }
}

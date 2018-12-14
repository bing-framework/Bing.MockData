using System;

namespace Bing.MockData.Abstractions.Options
{
    /// <summary>
    /// 时间跨度配置
    /// </summary>
    public interface ITimeSpanFieldOptions
    {
        /// <summary>
        /// 格式化。
        /// 请参考：https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-timespan-format-strings
        /// </summary>
        string Format { get; set; }

        /// <summary>
        /// 开始-从
        /// </summary>
        TimeSpan From { get; set; }

        /// <summary>
        /// 结束-至
        /// </summary>
        TimeSpan To { get; set; }
    }
}

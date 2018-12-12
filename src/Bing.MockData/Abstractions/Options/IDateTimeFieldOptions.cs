using System;

namespace Bing.MockData.Abstractions.Options
{
    /// <summary>
    /// 时间字段配置
    /// </summary>
    public interface IDateTimeFieldOptions
    {
        /// <summary>
        /// 开始-从
        /// </summary>
        DateTime From { get; set; }

        /// <summary>
        /// 结束-至
        /// </summary>
        DateTime To { get; set; }

        /// <summary>
        /// 是否包含时间
        /// </summary>
        bool IncludeTime { get; set; }
    }
}

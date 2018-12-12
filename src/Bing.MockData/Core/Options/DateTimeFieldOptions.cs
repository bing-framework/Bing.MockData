using System;
using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 时间字段配置
    /// </summary>
    public class DateTimeFieldOptions:FieldOptionsBase,IDateTimeFieldOptions
    {
        /// <summary>
        /// 默认格式。s -> 可排序的日期/时间模式
        /// </summary>
        public const string DefaultFormat = "s";

        /// <summary>
        /// 格式化
        /// </summary>
        public string Format { get; set; } = DefaultFormat;

        /// <summary>
        /// 开始-从
        /// </summary>
        public DateTime From { get; set; } = DateTime.UtcNow.Date;

        /// <summary>
        /// 结束-至
        /// </summary>
        public DateTime To { get; set; } = DateTime.UtcNow.Date.AddYears(1);

        /// <summary>
        /// 是否包含时间
        /// </summary>
        public bool IncludeTime { get; set; } = true;
    }
}

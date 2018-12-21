using Bing.MockData.Core.Enums;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 配置基类
    /// </summary>
    public abstract class FieldOptionsBase
    {
        /// <summary>
        /// 是否可空值
        /// </summary>
        public bool UseNullValues { get; set; }

        /// <summary>
        /// 是否可输出字符串
        /// </summary>
        public bool ValueAsString { get; set; }

        /// <summary>
        /// 语言类型。默认：中文
        /// </summary>
        public LanguageType Language { get; set; } = LanguageType.Cn;

        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        public FieldOptionsBase Clone()
        {
            return (FieldOptionsBase) MemberwiseClone();
        }
    }
}

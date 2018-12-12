namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 字段配置基类
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
        /// 克隆
        /// </summary>
        /// <returns></returns>
        public FieldOptionsBase Clone()
        {
            return (FieldOptionsBase) MemberwiseClone();
        }
    }
}

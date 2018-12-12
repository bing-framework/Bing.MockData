namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// short 字段配置
    /// </summary>
    public class ShortFieldOptions:NumberFieldOptions<short>
    {
        /// <summary>
        /// 最小值
        /// </summary>
        public override short Min { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public override short Max { get; set; } = short.MaxValue;
    }
}

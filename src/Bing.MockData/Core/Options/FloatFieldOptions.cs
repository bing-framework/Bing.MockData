namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// float 配置
    /// </summary>
    public class FloatFieldOptions:NumberFieldOptions<float>
    {
        /// <summary>
        /// 最小值
        /// </summary>
        public override float Min { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public override float Max { get; set; } = float.MaxValue;
    }
}

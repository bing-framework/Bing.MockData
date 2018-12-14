namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// double 配置
    /// </summary>
    public class DoubleFieldOptions:NumberFieldOptions<double>
    {
        /// <summary>
        /// 最小值
        /// </summary>
        public override double Min { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public override double Max { get; set; } = double.MaxValue;
    }
}

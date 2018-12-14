namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// long 配置
    /// </summary>
    public class LongFieldOptions:NumberFieldOptions<long>
    {
        /// <summary>
        /// 最小值
        /// </summary>
        public override long Min { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public override long Max { get; set; } = long.MaxValue;
    }
}

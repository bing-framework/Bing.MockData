namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// int 配置
    /// </summary>
    public class IntFieldOptions:NumberFieldOptions<int>
    {
        /// <summary>
        /// 最小值
        /// </summary>
        public override int Min { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public override int Max { get; set; } = int.MaxValue;
    }
}

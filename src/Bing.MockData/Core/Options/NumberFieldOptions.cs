using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 数值配置
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public abstract class NumberFieldOptions<T> : FieldOptionsBase, INumberFieldOptions<T> where T : struct
    {
        /// <summary>
        /// 最小值
        /// </summary>
        public virtual T Min { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public virtual T Max { get; set; }
    }
}

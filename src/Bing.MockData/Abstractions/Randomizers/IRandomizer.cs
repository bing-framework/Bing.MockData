namespace Bing.MockData.Abstractions.Randomizers
{
    /// <summary>
    /// 生成指定类型模拟值的统一契约。
    /// </summary>
    /// <typeparam name="T">输出类型，支持协变。</typeparam>
    public interface IRandomizer<out T>
    {
        /// <summary>
        /// 生成一个值。
        /// </summary>
        /// <returns>生成结果；可空包装器可按配置概率返回空值。</returns>
        T Generate();
    }
}

namespace Bing.MockData.Abstractions.Randomizers
{
    /// <summary>
    /// 随机生成器
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public interface IRandomizer<T>
    {
        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        T Generate();

        /// <summary>
        /// 生成并转换成字符串
        /// </summary>
        /// <returns></returns>
        string GenerateAsString();
    }
}

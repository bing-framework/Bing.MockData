namespace Bing.MockData.Abstractions.Randomizers
{
    /// <summary>
    /// 数值随机生成器
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public interface INumberRandomizer<T> where T:struct
    {
        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        T? Generate();
    }
}

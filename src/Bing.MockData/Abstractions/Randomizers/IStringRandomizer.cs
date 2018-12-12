namespace Bing.MockData.Abstractions.Randomizers
{
    /// <summary>
    /// 字符串随机生成器
    /// </summary>
    public interface IStringRandomizer
    {
        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        string Generate();

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="upperCase">是否大写</param>
        /// <returns></returns>
        string Generate(bool upperCase);
    }
}

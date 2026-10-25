namespace Bing.MockData.Abstractions.Randomizers
{
    /// <summary>
    /// 地址随机生成器
    /// </summary>
    public interface IAddressRandomizer : IRandomizer<string>
    {
/// <summary>
        /// 生成大区
        /// </summary>
        /// <returns></returns>
        string GenerateRegion();
    }
}

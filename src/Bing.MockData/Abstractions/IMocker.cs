using Bing.MockData.Core.Enums;

namespace Bing.MockData.Abstractions
{
    /// <summary>
    /// 模拟器
    /// </summary>
    public interface IMocker
    {
        /// <summary>
        /// 本地化
        /// </summary>
        /// <param name="local">本地化</param>
        IMocker Local(string local);

        /// <summary>
        /// 本地化
        /// </summary>
        /// <param name="local">本地化</param>
        IMocker Local(LocalType local);
    }
}

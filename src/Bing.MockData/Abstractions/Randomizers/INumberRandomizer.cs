namespace Bing.MockData.Abstractions.Randomizers
{
    /// <summary>非空数值生成器。</summary>
    public interface INumberRandomizer<T> : IRandomizer<T> where T : struct { }
}

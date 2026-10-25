namespace Bing.MockData.Core.Randomizers
{
    internal interface IRandomizerSnapshot
    {
        object Restart(int? seed = null);
        int Seed { get; }
    }
    internal interface IFormattedRandomizer { string Format { get; } }
}

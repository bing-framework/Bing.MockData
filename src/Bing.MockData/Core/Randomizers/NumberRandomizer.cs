using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>闭区间数值生成器。</summary>
    public class NumberRandomizer<T> : RandomizerBase<NumberFieldOptions<T>>, INumberRandomizer<T> where T : struct
    {
        private readonly RandomThingsGenerator<T> _generator;
        /// <summary>验证并复制数值配置。</summary>
        public NumberRandomizer(NumberFieldOptions<T> options) : base(options)
        { _generator = new RandomThingsGenerator<T>(Rnd, Options.Min, Options.Max); }
        /// <summary>生成闭区间内的数值。</summary>
        public T Generate() => Execute(() => _generator.Generate());
    }
}

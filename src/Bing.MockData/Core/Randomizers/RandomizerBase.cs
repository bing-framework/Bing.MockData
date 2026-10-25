using System;
using System.Security.Cryptography;
using Bing.MockData.Core.Options;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>保存配置快照与实例随机状态的生成器基类。</summary>
    /// <typeparam name="TOptions">配置类型。</typeparam>
    public abstract class RandomizerBase<TOptions> : IRandomizerSnapshot where TOptions : FieldOptionsBase
    {
        /// <summary>实例随机源；仅在 Execute 的同步范围内使用。</summary>
        protected readonly Random Rnd;
        /// <summary>构造时复制的配置。</summary>
        protected readonly TOptions Options;
        private readonly object _gate = new object();
        /// <summary>复制配置并初始化独立随机源。</summary>
        protected RandomizerBase(TOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            Options = (TOptions)options.Clone();
            Options.Seed = Options.Seed ?? CreateSeed();
            if (Options.MaxTextLength < 0) throw new ArgumentOutOfRangeException(nameof(options.MaxTextLength));
            Rnd = new Random(Options.Seed.Value);
        }
        private static int CreateSeed()
        {
            var bytes = new byte[4];
            using (var source = RandomNumberGenerator.Create()) source.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
        /// <summary>同步执行一次完整生成操作。</summary>
        protected TResult Execute<TResult>(Func<TResult> generate)
        {
            lock (_gate) { Options.CancellationToken.ThrowIfCancellationRequested(); var result = generate(); Options.CancellationToken.ThrowIfCancellationRequested();
                if (result is string text && text.Length > Options.MaxTextLength) throw new MockLimitException(nameof(FieldOptionsBase.MaxTextLength), Options.MaxTextLength);
                return result; }
        }
        object IRandomizerSnapshot.Restart(int? seed) { var copy = (TOptions)Options.Clone(); copy.Seed = seed ?? Options.Seed; return Factories.RandomizerFactory.Create(copy); }
        int IRandomizerSnapshot.Seed => Options.Seed.Value;
    }
}

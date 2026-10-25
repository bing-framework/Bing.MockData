using System;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>可按种子重放的模拟 GUID 生成器。</summary>
    public class GuidRandomizer : RandomizerBase<GuidFieldOptions>, IGuidRandomizer, IFormattedRandomizer
    {
        string IFormattedRandomizer.Format => Options.Uppercase ? "D!" : "D";
        /// <summary>复制生成配置。</summary>
        public GuidRandomizer(GuidFieldOptions options) : base(options) { }
        /// <summary>生成具有版本 4 和 RFC 变体位的 GUID。</summary>
        public Guid Generate() => Execute<Guid>(() =>
        {
            var bytes = new byte[16];
            Rnd.NextBytes(bytes);
            bytes[7] = (byte)((bytes[7] & 0x0f) | 0x40);
            bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            return new Guid(bytes);
        });
    }
}

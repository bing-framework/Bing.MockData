namespace Bing.MockData.Core.Options
{
    /// <summary>生成配置。生成器在构造时保存配置快照。</summary>
    public abstract class FieldOptionsBase
    {
        /// <summary>可重放的随机种子；为空时自动初始化。</summary>
        public int? Seed { get; set; }
        /// <summary>最大文本长度（UTF-16 单元）。</summary>
        public int MaxTextLength { get; set; } = 100000;
        /// <summary>取消当前长生成操作。</summary>
        public System.Threading.CancellationToken CancellationToken { get; set; }
        /// <summary>复制配置；派生类型应同时复制其中的可变集合。</summary>
        public virtual FieldOptionsBase Clone() => (FieldOptionsBase)MemberwiseClone();
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace Bing.MockData
{
    /// <summary>
    /// 保存生成结果重放所需的上下文元数据。
    /// </summary>
    /// <remarks>
    /// 重建结果还要求调用方提供相同业务规则和外部字典；该对象不包含这些定义或字典条目。
    /// </remarks>
    public sealed class ReplayInfo
    {
        /// <summary>模板和扩展契约版本；旧记录不能静默解释为新模板。</summary>
        public string GenerationContractVersion { get; }
        /// <summary>冻结的已挂载目录、Schema 及扩展身份。</summary>
        public IReadOnlyDictionary<string, string> ExtensionVersions { get; }
        /// <summary>
        /// 获取用于派生随机序列的种子。
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// 获取重放时使用的固定参考时间。
        /// </summary>
        public DateTimeOffset ReferenceTime { get; }

        /// <summary>
        /// 获取生成内容使用的区域标记。
        /// </summary>
        public string Locale { get; }

        /// <summary>
        /// 获取随机派生算法的版本标记。
        /// </summary>
        public string AlgorithmVersion { get; }

        /// <summary>
        /// 获取内置数据快照的版本标记。
        /// </summary>
        public string DataVersion { get; }

        /// <summary>
        /// 获取生成此信息的程序集版本。
        /// </summary>
        public string LibraryVersion { get; }

        /// <summary>
        /// 获取生成此信息的运行时版本。
        /// </summary>
        public string RuntimeVersion { get; }

        /// <summary>
        /// 获取生成结果对应的记录索引。
        /// </summary>
        public long RecordIndex { get; }

        /// <summary>
        /// 获取生成结果对应的字段路径。
        /// </summary>
        public string FieldPath { get; }

        /// <summary>
        /// 保存不可变的重放预算快照。
        /// </summary>
        private readonly MockLimits _limits;

        /// <summary>
        /// 获取重放时使用的独立预算副本。
        /// </summary>
        /// <returns>修改后不会改变此重放信息的预算副本。</returns>
        public MockLimits Limits => _limits.Copy();

        /// <summary>
        /// 从当前上下文捕获重放元数据。
        /// </summary>
        /// <param name="context">要记录其生成参数的上下文。</param>
        internal ReplayInfo(MockContext context)
            : this(context.Seed, context.ReferenceTime, context.Locale, "fnv64-mix-v1", "china-2025-12-31-domain-v3",
                typeof(MockContext).Assembly.GetName().Version.ToString(), Environment.Version.ToString(), context.RecordIndex, context.FieldPath, context.Budget, "domain-v3", context.ExtensionVersions) { }

        /// <summary>
        /// 从序列化字段创建重放元数据。
        /// </summary>
        /// <param name="seed">随机序列派生种子。</param>
        /// <param name="referenceTime">日期生成使用的固定参考时间。</param>
        /// <param name="locale">生成内容使用的区域标记。</param>
        /// <param name="algorithmVersion">随机派生算法版本。</param>
        /// <param name="dataVersion">内置数据快照版本。</param>
        /// <param name="libraryVersion">生成信息时的程序集版本。</param>
        /// <param name="runtimeVersion">生成信息时的运行时版本。</param>
        /// <param name="recordIndex">非负记录索引。</param>
        /// <param name="fieldPath">生成结果对应的字段路径。</param>
        /// <param name="limits">重放使用的生成预算。</param>
        /// <param name="generationContractVersion">生成契约版本，缺失表示旧格式。</param>
        /// <param name="extensionVersions">挂载扩展及数据版本。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordIndex"/> 小于零，或预算配置无效。</exception>
        /// <exception cref="ArgumentNullException">任一字符串字段或 <paramref name="limits"/> 为 <see langword="null"/>。</exception>
        [JsonConstructor]
        public ReplayInfo(int seed, DateTimeOffset referenceTime, string locale, string algorithmVersion, string dataVersion,
            string libraryVersion, string runtimeVersion, long recordIndex, string fieldPath, MockLimits limits,
            string generationContractVersion = null, IReadOnlyDictionary<string, string> extensionVersions = null)
        {
            if (recordIndex < 0) throw new ArgumentOutOfRangeException(nameof(recordIndex));
            Seed = seed; ReferenceTime = referenceTime; Locale = locale ?? throw new ArgumentNullException(nameof(locale));
            AlgorithmVersion = algorithmVersion ?? throw new ArgumentNullException(nameof(algorithmVersion));
            DataVersion = dataVersion ?? throw new ArgumentNullException(nameof(dataVersion));
            LibraryVersion = libraryVersion ?? throw new ArgumentNullException(nameof(libraryVersion));
            RuntimeVersion = runtimeVersion ?? throw new ArgumentNullException(nameof(runtimeVersion));
            RecordIndex = recordIndex; FieldPath = fieldPath ?? throw new ArgumentNullException(nameof(fieldPath));
            _limits = (limits ?? throw new ArgumentNullException(nameof(limits))).Copy(); _limits.Validate();
            GenerationContractVersion = generationContractVersion;
            var versions = new Dictionary<string, string>(StringComparer.Ordinal);
            if (extensionVersions != null) foreach (var entry in extensionVersions) versions.Add(entry.Key, entry.Value);
            ExtensionVersions = new ReadOnlyDictionary<string, string>(versions);
        }

        /// <summary>
        /// 将兼容的重放元数据转换为上下文选项。
        /// </summary>
        /// <remarks>
        /// 仅当算法、内置数据、程序集和运行时版本与当前环境完全一致时才允许转换。
        /// 返回选项保留种子、参考时间、区域和预算，不包含记录索引、字段路径、业务规则或外部字典。
        /// </remarks>
        /// <returns>可用于创建上下文的配置副本。</returns>
        /// <exception cref="InvalidOperationException">重放版本与当前环境不匹配。</exception>
        public MockOptions ToOptions()
        {
            if (GenerationContractVersion != "domain-v3" || AlgorithmVersion != "fnv64-mix-v1" || DataVersion != "china-2025-12-31-domain-v3" ||
                LibraryVersion != typeof(MockContext).Assembly.GetName().Version.ToString() || RuntimeVersion != Environment.Version.ToString())
                throw new InvalidOperationException("重放版本不匹配；需要相同库、运行时、派生算法和内置数据版本。");
            return new MockOptions { Seed = Seed, ReferenceTime = ReferenceTime, Locale = Locale, Limits = Limits, ExpectedExtensionVersions = ExtensionVersions };
        }
    }

    /// <summary>
    /// 表示模拟记录中的生成规则执行失败。
    /// </summary>
    /// <remarks>
    /// 异常保留规则标识、字段路径、记录索引、重放信息和内部异常；取消异常不会包装为此类型。
    /// </remarks>
    public sealed class MockGenerationException : Exception
    {
        /// <summary>
        /// 获取失败的生成规则标识。
        /// </summary>
        public string RuleId { get; }

        /// <summary>
        /// 获取失败记录的重放参数。
        /// </summary>
        public ReplayInfo Replay { get; }

        /// <summary>
        /// 获取失败规则所在的字段路径。
        /// </summary>
        public string FieldPath => Replay.FieldPath;

        /// <summary>
        /// 获取失败记录的索引。
        /// </summary>
        public long RecordIndex => Replay.RecordIndex;

        /// <summary>
        /// 使用规则标识和上下文创建生成失败异常。
        /// </summary>
        /// <param name="rule">失败的规则标识。</param>
        /// <param name="context">发生失败的字段上下文。</param>
        /// <param name="inner">导致生成失败的异常。</param>
        internal MockGenerationException(string rule, MockContext context, Exception inner)
            : base($"记录 {context.RecordIndex} 的 '{context.FieldPath}' 规则 '{rule}' 生成失败：{inner.Message}", inner)
        { RuleId = rule; Replay = context.Replay; }
    }

    /// <summary>
    /// 表示生成请求超过了显式预算上限。
    /// </summary>
    public sealed class MockLimitException : InvalidOperationException
    {
        /// <summary>
        /// 获取被超过的预算名称。
        /// </summary>
        public string LimitName { get; }

        /// <summary>
        /// 获取该预算允许的上限。
        /// </summary>
        public long Limit { get; }

        /// <summary>
        /// 创建包含预算名称和上限的限制异常。
        /// </summary>
        /// <param name="name">被超过的预算名称。</param>
        /// <param name="limit">该预算允许的上限。</param>
        public MockLimitException(string name, long limit) : base($"超过 {name} 上限 {limit}。") { LimitName = name; Limit = limit; }
    }
}

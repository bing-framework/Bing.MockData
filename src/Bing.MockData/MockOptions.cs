using System;
using System.Collections.Generic;

namespace Bing.MockData
{
    /// <summary>
    /// 单个模拟上下文使用的生成预算。
    /// </summary>
    /// <remarks>
    /// 创建上下文时会复制预算；之后修改此实例不会影响已创建的上下文。
    /// </remarks>
    public sealed class MockLimits
    {
        /// <summary>
        /// 获取或设置一次物化批量生成允许的最大记录数。
        /// </summary>
        /// <remarks>默认值为 10000；必须为非负数。</remarks>
        public int MaxMaterializedRecords { get; set; } = 10000;
        /// <summary>
        /// 获取或设置一次流式生成允许的最大记录数。
        /// </summary>
        /// <remarks>默认值为 1000000；必须为非负数。</remarks>
        public int MaxStreamingRecords { get; set; } = 1000000;
        /// <summary>
        /// 获取或设置单条生成文本允许的最大 UTF-16 单元数。
        /// </summary>
        /// <remarks>默认值为 100000；必须为非负数。</remarks>
        public int MaxTextLength { get; set; } = 100000;
        /// <summary>
        /// 获取或设置对象嵌套生成允许的最大深度。
        /// </summary>
        /// <remarks>默认值为 8；必须大于零。</remarks>
        public int MaxDepth { get; set; } = 8;
        /// <summary>
        /// 获取或设置单个集合允许包含的最大元素数。
        /// </summary>
        /// <remarks>默认值为 1000；必须为非负数。</remarks>
        public int MaxCollectionSize { get; set; } = 1000;
        /// <summary>
        /// 获取或设置条件生成和唯一值生成的最大尝试次数。
        /// </summary>
        /// <remarks>默认值为 100；必须大于零。</remarks>
        public int MaxAttempts { get; set; } = 100;
        /// <summary>
        /// 获取或设置去重作用域最多保留的唯一值数。
        /// </summary>
        /// <remarks>默认值为 100000；必须大于零。</remarks>
        public int MaxUniqueValues { get; set; } = 100000;
        /// <summary>
        /// 获取或设置读取外部字典允许的最大字节数。
        /// </summary>
        /// <remarks>默认值为 16 MiB；必须大于零。</remarks>
        public long MaxDictionaryBytes { get; set; } = 16 * 1024 * 1024;
        /// <summary>
        /// 获取或设置外部字典允许包含的最大条目数。
        /// </summary>
        /// <remarks>默认值为 100000；必须大于零。</remarks>
        public int MaxDictionaryEntries { get; set; } = 100000;
        /// <summary>单个上下文允许拥有的命名编译 Schema 数量。</summary>
        public int MaxCompiledSchemas { get; set; } = 64;
        /// <summary>
        /// 创建当前预算的浅副本。
        /// </summary>
        /// <returns>属性值与当前实例相同的新预算对象。</returns>
        internal MockLimits Copy() => (MockLimits)MemberwiseClone();
        /// <summary>
        /// 验证所有预算均处于允许范围内。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">任一预算为负数，或要求为正数的预算不大于零时抛出。</exception>
        internal void Validate()
        {
            if (MaxMaterializedRecords < 0 || MaxStreamingRecords < 0 || MaxTextLength < 0 ||
                MaxDepth < 1 || MaxCollectionSize < 0 || MaxAttempts < 1 || MaxUniqueValues < 1 ||
                MaxDictionaryBytes < 1 || MaxDictionaryEntries < 1 || MaxCompiledSchemas < 1)
                throw new ArgumentOutOfRangeException(nameof(MockLimits), "预算必须为有效的非负值；深度、尝试、唯一值和字典预算必须为正。");
        }
    }

    /// <summary>
    /// 配置模拟上下文的种子、基准时间、区域、预算和扩展生成规则。
    /// </summary>
    /// <remarks>
    /// 注册与资源在上下文创建时被捕获；上下文创建后对此对象的修改不会改变已有上下文。
    /// </remarks>
    public sealed partial class MockOptions
    {
        /// <summary>
        /// 获取或设置可复现随机序列的种子；为空时由上下文生成种子。
        /// </summary>
        public int? Seed { get; set; }
        /// <summary>
        /// 获取或设置用于日期生成和重放的固定参考时间；为空时由上下文确定默认值。
        /// </summary>
        public DateTimeOffset? ReferenceTime { get; set; }
        /// <summary>
        /// 获取或设置生成内容使用的区域标记。
        /// </summary>
        /// <remarks>当前仅接受 <c>zh-CN</c> 和 <c>en-US</c>。</remarks>
        public string Locale { get; set; } = "zh-CN";
        /// <summary>
        /// 获取或设置物化、流式、文本、嵌套、集合、尝试、唯一值和字典读取预算。
        /// </summary>
        public MockLimits Limits { get; set; } = new MockLimits();
        /// <summary>
        /// 存储尚未创建上下文的命名生成器注册表。
        /// </summary>
        internal readonly Dictionary<string, Registration> Registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);
        /// <summary>
        /// 存储由当前选项显式接收所有权的外部资源。
        /// </summary>
        internal readonly List<object> OwnedResources = new List<object>();

        /// <summary>
        /// 注册一个按名称和结果类型查找的上下文生成规则。
        /// </summary>
        /// <typeparam name="T">生成规则返回的值类型。</typeparam>
        /// <param name="name">区分大小写的非空注册名称。</param>
        /// <param name="generate">接收生成上下文并返回一个值的委托。</param>
        /// <returns>当前配置对象，便于继续链式配置。</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为空白或已注册时抛出。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="generate"/> 为空时抛出。</exception>
        public MockOptions Register<T>(string name, Func<MockContext, T> generate)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("注册名称不能为空。", nameof(name));
            if (generate == null) throw new ArgumentNullException(nameof(generate));
            if (Registrations.ContainsKey(name)) throw new ArgumentException("名称已经注册。", nameof(name));
            Registrations.Add(name, new Registration(typeof(T), c => generate(c)));
            return this;
        }

        /// <summary>
        /// 注册一个按名称创建生成器的工厂。
        /// </summary>
        /// <typeparam name="T">工厂生成器返回的值类型。</typeparam>
        /// <param name="name">区分大小写的非空注册名称。</param>
        /// <param name="factory">接收生成上下文并返回生成器的委托。</param>
        /// <returns>当前配置对象，便于继续链式配置。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> 为空时抛出。</exception>
        /// <remarks>工厂逐条调用；返回按索引生成器时沿用当前记录序号，其他实现调用 Generate。</remarks>
        public MockOptions RegisterFactory<T>(string name, Func<MockContext, Abstractions.Randomizers.IRandomizer<T>> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return Register(name, c =>
            {
                var generator = factory(c) ?? throw new InvalidOperationException("工厂返回了空生成器。");
                return generator is MockGenerator<T> indexed ? indexed.At(c.RecordIndex, c.CancellationToken) : generator.Generate();
            });
        }

        /// <summary>
        /// 将扩展资源登记到上下文，并可选择把释放责任转交给上下文。
        /// </summary>
        /// <param name="resource">扩展生成规则使用的资源对象。</param>
        /// <param name="ownsResource">为 <see langword="true"/> 时由根上下文释放资源；否则资源由调用方释放。</param>
        /// <returns>当前配置对象，便于继续链式配置。</returns>
        /// <remarks>转移所有权的资源必须实现 <see cref="IDisposable"/>；支持异步释放的目标框架还接受仅实现 <c>IAsyncDisposable</c> 的资源。重复登记同一实例不会重复转移所有权。</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="resource"/> 为空时抛出。</exception>
        /// <exception cref="ArgumentException"><paramref name="ownsResource"/> 为 <see langword="true"/> 且资源不支持释放时抛出。</exception>
        public MockOptions UseResource(object resource, bool ownsResource = false)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (ownsResource && !OwnedResources.Exists(x => ReferenceEquals(x, resource)))
            {
                if (!(resource is IDisposable)
#if NET10_0_OR_GREATER
                    && !(resource is IAsyncDisposable)
#endif
                    ) throw new ArgumentException("转移的资源必须支持释放。", nameof(resource));
                OwnedResources.Add(resource);
            }
            return this;
        }
        /// <summary>
        /// 保存单个命名生成器的结果类型和创建委托。
        /// </summary>
        internal sealed class Registration
        {
            /// <summary>
            /// 初始化一项已校验名称的生成器注册。
            /// </summary>
            /// <param name="type">生成结果的运行时类型。</param>
            /// <param name="factory">将上下文转换为生成结果的委托。</param>
            internal Registration(Type type, Func<MockContext, object> factory) { Type = type; Factory = factory; }
            /// <summary>
            /// 记录工厂所声明的精确结果类型。
            /// </summary>
            internal readonly Type Type;
            /// <summary>
            /// 在指定上下文中生成并装箱结果的委托。
            /// </summary>
            internal readonly Func<MockContext, object> Factory;
        }
    }
}

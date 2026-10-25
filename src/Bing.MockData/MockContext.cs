using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Threading;
using Bing.MockData.Abstractions.Randomizers;

namespace Bing.MockData
{
    /// <summary>
    /// 为模拟数据生成提供实例级状态和资源作用域。
    /// </summary>
    /// <remarks>
    /// 根上下文拥有注册资源；派生上下文共享状态但不拥有释放权。
    /// 调用方应先停止使用上下文的活动任务，再释放根上下文。
    /// </remarks>
    public sealed partial class MockContext : IDisposable
#if NET10_0_OR_GREATER
        , IAsyncDisposable
#endif
    {
        /// <summary>
        /// 保存根上下文共享的种子、预算、注册表和资源状态。
        /// </summary>
        private readonly State _state;
        /// <summary>
        /// 指示此实例是否为拥有共享状态的根上下文。
        /// </summary>
        private readonly bool _owner;
        /// <summary>
        /// 保存此派生上下文可见的字典目录快照；根上下文使用实时目录。
        /// </summary>
        private readonly IReadOnlyDictionary<string, object> _catalog;
        /// <summary>
        /// 串行化当前上下文实例对其随机数生成器的访问。
        /// </summary>
        private readonly object _randomGate = new object();
        /// <summary>
        /// 保存当前上下文实例延迟创建的随机数生成器。
        /// </summary>
        private Random _random;
        /// <summary>
        /// 追踪根上下文直接生成的模板记录序号。
        /// </summary>
        private long _templateRecord = -1;

        /// <summary>
        /// 获取此上下文当前使用的共享预算快照。
        /// </summary>
        internal MockLimits Budget => _state.Limits;

        /// <summary>
        /// 获取当前预算的独立副本。
        /// </summary>
        /// <returns>修改后不会影响当前上下文的预算副本。</returns>
        public MockLimits Limits => Budget.Copy();

        /// <summary>
        /// 获取当前上下文的随机种子。
        /// </summary>
        public int Seed => _state.Seed;

        /// <summary>
        /// 获取用于日期生成和重放的固定参考时间。
        /// </summary>
        public DateTimeOffset ReferenceTime => _state.ReferenceTime;

        /// <summary>
        /// 获取当前上下文使用的区域标记。
        /// </summary>
        public string Locale => _state.Locale;

        /// <summary>
        /// 获取当前生成记录的索引。
        /// </summary>
        public long RecordIndex { get; }

        /// <summary>
        /// 获取当前生成字段的路径。
        /// </summary>
        public string FieldPath { get; }

        /// <summary>
        /// 获取当前对象嵌套深度。
        /// </summary>
        internal int Depth { get; }

        /// <summary>
        /// 获取当前上下文继承的取消令牌。
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// 获取包含当前记录和字段的重放元数据。
        /// </summary>
        /// <returns>当前上下文的重放信息副本。</returns>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public ReplayInfo Replay { get { Check(); return new ReplayInfo(this); } }

        /// <summary>
        /// 获取使用当前上下文的数值生成入口。
        /// </summary>
        public NumberData Numbers => new NumberData(this);

        /// <summary>
        /// 获取使用当前上下文的文本生成入口。
        /// </summary>
        public TextData Text => new TextData(this);

        /// <summary>
        /// 获取使用当前上下文的日期生成入口。
        /// </summary>
        public DateData Dates => new DateData(this);

        /// <summary>
        /// 获取使用当前上下文的集合抽样入口。
        /// </summary>
        public CollectionData Collections => new CollectionData(this);

        /// <summary>
        /// 获取使用当前上下文的网络数据生成入口。
        /// </summary>
        public NetworkData Network => new NetworkData(this);

        /// <summary>
        /// 创建一个拥有资源和共享状态的根上下文。
        /// </summary>
        /// <remarks>
        /// 预算、注册表和已登记资源引用会从选项复制；资源对象本身不会克隆。
        /// 使用同一选项对象创建多个根上下文时，同一资源会被多个根上下文登记释放。
        /// </remarks>
        /// <param name="options">可选的上下文配置；省略时使用默认配置。</param>
        /// <exception cref="ArgumentException"><paramref name="options"/> 使用了不支持的区域标记。</exception>
        /// <exception cref="ArgumentNullException">配置中的预算对象为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException">任一预算值不在允许范围内。</exception>
        public MockContext(MockOptions options = null)
        {
            options = options ?? new MockOptions();
            if (options.Locale != "zh-CN" && options.Locale != "en-US") throw new ArgumentException("仅支持 zh-CN 和 en-US。", nameof(options));
            var limits = (options.Limits ?? throw new ArgumentNullException(nameof(options.Limits))).Copy();
            limits.Validate();
            _state = new State(options, limits); _owner = true; FieldPath = "";
        }

        /// <summary>
        /// 创建共享根状态的派生上下文。
        /// </summary>
        /// <remarks>
        /// 派生上下文不拥有根状态或已注册资源，只携带自己的记录、字段、深度、取消令牌和字典快照。
        /// </remarks>
        /// <param name="source">提供共享根状态的上下文。</param>
        /// <param name="index">此上下文使用的记录索引。</param>
        /// <param name="path">此上下文的字段路径。</param>
        /// <param name="depth">此上下文的对象嵌套深度。</param>
        /// <param name="token">此上下文继承的取消令牌。</param>
        /// <param name="catalog">此上下文可见的字典目录快照；可以为空，表示由后续操作选择捕获时机。</param>
        private MockContext(MockContext source, long index, string path, int depth, CancellationToken token, IReadOnlyDictionary<string, object> catalog)
        { _state = source._state; RecordIndex = index; FieldPath = path; Depth = depth; CancellationToken = token; _catalog = catalog; }

        /// <summary>
        /// 创建使用显式记录序号的派生上下文。
        /// </summary>
        /// <remarks>
        /// 首次创建记录上下文时捕获字典目录快照；提供可取消令牌时使用它，否则继承当前令牌。
        /// 派生上下文不消耗父上下文的顺序随机序列，也不拥有共享资源。
        /// </remarks>
        /// <param name="index">要重放的非负记录索引。</param>
        /// <param name="cancellationToken">可选的取消令牌；不可取消时继承当前上下文令牌。</param>
        /// <returns>具有指定记录索引的派生上下文。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> 小于零。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public MockContext ForRecord(long index, CancellationToken cancellationToken = default)
        {
            Check(); if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            return new MockContext(this, index, FieldPath, Depth, cancellationToken.CanBeCanceled ? cancellationToken : CancellationToken, _catalog ?? CaptureCatalog());
        }
        /// <summary>
        /// 创建指向指定字段路径的派生上下文。
        /// </summary>
        /// <remarks>
        /// 字段路径与记录索引共同参与随机种子派生；相同种子、索引和路径可重复生成相同序列。
        /// 若当前实例已有字典快照则继续沿用；否则字典读取仍查看根目录，直到创建记录或生成器快照。
        /// 派生上下文不拥有共享资源。
        /// </remarks>
        /// <param name="name">要追加到字段路径的非空字段名称。</param>
        /// <returns>字段路径追加后的派生上下文。</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为 <see langword="null"/> 或空字符串。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public MockContext ForField(string name)
        {
            Check(); if (string.IsNullOrEmpty(name)) throw new ArgumentException("字段名称不能为空。", nameof(name));
            return new MockContext(this, RecordIndex, FieldPath.Length == 0 ? name : FieldPath + "." + name, Depth, CancellationToken, _catalog);
        }
        /// <summary>
        /// 创建对象嵌套深度加一的派生上下文。
        /// </summary>
        /// <exception cref="MockLimitException">当前深度已达到最大嵌套深度。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        internal MockContext EnterObject()
        {
            Check(); if (Depth >= Budget.MaxDepth) throw new MockLimitException(nameof(MockLimits.MaxDepth), Budget.MaxDepth);
            return new MockContext(this, RecordIndex, FieldPath, Depth + 1, CancellationToken, _catalog);
        }
        /// <summary>
        /// 创建捕获当前字典目录的生成快照上下文。
        /// </summary>
        /// <remarks>
        /// 快照共享根状态和生命周期，但之后替换或移除根字典不会改变此快照可见的目录。
        /// </remarks>
        /// <returns>携带当前记录、字段和字典目录快照的派生上下文。</returns>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        internal MockContext Snapshot()
        { Check(); return new MockContext(this, RecordIndex, FieldPath, Depth, CancellationToken, _catalog ?? CaptureCatalog()); }
        /// <summary>
        /// 为根上下文分配下一个模板记录，派生上下文则保持原样。
        /// </summary>
        /// <returns>根上下文的下一个记录上下文，或当前派生上下文。</returns>
        internal MockContext TemplateRecord() => _owner ? ForRecord(Interlocked.Increment(ref _templateRecord)) : this;

        /// <summary>
        /// 在当前上下文的随机数生成器锁内执行抽样委托。
        /// </summary>
        /// <typeparam name="TResult">抽样委托的结果类型。</typeparam>
        /// <param name="generate">使用当前上下文随机源产生结果的委托。</param>
        /// <returns>委托返回的结果。</returns>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        internal TResult WithRandom<TResult>(Func<Random, TResult> generate)
        {
            Check(); lock (_randomGate) { Check(); return generate(_random ?? (_random = new Random(DeriveSeed()))); }
        }
        /// <summary>
        /// 从当前上下文随机源派生一个整数种子。
        /// </summary>
        /// <returns>用于初始化字段级生成器的种子。</returns>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        internal int NextSeed() => WithRandom(r => r.Next(int.MinValue, int.MaxValue));

        /// <summary>
        /// 根据种子、记录索引和字段路径推导当前上下文的随机种子。
        /// </summary>
        /// <remarks>
        /// 派生过程是重放格式的一部分，变更时应同步更新重放算法版本。
        /// </remarks>
        /// <returns>当前上下文派生随机源所使用的整数种子。</returns>
        private int DeriveSeed()
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                void Add(ulong value) { for (int i = 0; i < 8; i++) { hash ^= (byte)value; hash *= 1099511628211UL; value >>= 8; } }
                Add((ulong)(long)Seed); Add((ulong)RecordIndex);
                foreach (char c in FieldPath) { hash ^= (byte)c; hash *= 1099511628211UL; hash ^= (byte)(c >> 8); hash *= 1099511628211UL; }
                hash ^= hash >> 33; hash *= 0xff51afd7ed558ccdUL; hash ^= hash >> 33;
                return (int)(hash ^ (hash >> 32));
            }
        }
        /// <summary>
        /// 检查共享根状态和当前取消令牌是否仍允许操作。
        /// </summary>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        internal void Check() { if (_state.Disposed) throw new ObjectDisposedException(nameof(MockContext)); CancellationToken.ThrowIfCancellationRequested(); }

        /// <summary>
        /// 创建使用指定规则委托的生成器。
        /// </summary>
        /// <typeparam name="T">生成值的类型。</typeparam>
        /// <param name="generate">接收记录上下文并产生一个值的委托。</param>
        /// <param name="ruleId">用于生成失败诊断的规则标识。</param>
        /// <returns>关联当前上下文的按记录索引生成器。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="generate"/> 或 <paramref name="ruleId"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public MockGenerator<T> Generator<T>(Func<MockContext, T> generate, string ruleId = "generator")
        { Check(); return new MockGenerator<T>(this, generate, ruleId); }
        /// <summary>
        /// 创建指定名称和结果类型的已注册生成器。
        /// </summary>
        /// <typeparam name="T">注册时声明的生成结果类型。</typeparam>
        /// <param name="name">已注册生成器的名称。</param>
        /// <returns>调用已注册工厂并沿用当前上下文的生成器。</returns>
        /// <exception cref="ArgumentException">名称不存在或注册的结果类型与 <typeparamref name="T"/> 不一致。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public MockGenerator<T> Registered<T>(string name)
        {
            Check();
            if (!_state.Registrations.TryGetValue(name, out var registration) || registration.Type != typeof(T))
                throw new ArgumentException("未注册该名称及结果类型。", nameof(name));
            return Generator(c => (T)registration.Factory(c), name);
        }
        /// <summary>
        /// 在共享状态锁内复制当前字典目录。
        /// </summary>
        /// <remarks>
        /// 目录映射被复制为只读字典，但其中的字典列表和条目对象不会深复制。
        /// </remarks>
        /// <returns>当前字典名称到只读列表的映射快照。</returns>
        private IReadOnlyDictionary<string, object> CaptureCatalog()
        {
            lock (_state.Gate) return new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(_state.Catalog, StringComparer.Ordinal));
        }
        /// <summary>
        /// 在根上下文中替换一个字典快照。
        /// </summary>
        /// <typeparam name="T">字典条目的类型。</typeparam>
        /// <param name="name">字典名称。</param>
        /// <param name="values">要保存的条目数组。</param>
        /// <exception cref="InvalidOperationException">当前实例不是根上下文。</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为 <see langword="null"/>、空字符串或空白字符串。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        internal void StoreDictionary<T>(string name, T[] values)
        {
            Check(); if (!_owner) throw new InvalidOperationException("只能在根上下文更新字典。");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("字典名称不能为空。", nameof(name));
            lock (_state.Gate) _state.Catalog[name] = Array.AsReadOnly(values);
        }
        /// <summary>
        /// 从根上下文中移除指定名称的字典。
        /// </summary>
        /// <param name="name">要移除的字典名称。</param>
        /// <returns>若该名称此前存在并已移除，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        /// <exception cref="InvalidOperationException">当前实例不是根上下文。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public bool RemoveDictionary(string name)
        { Check(); if (!_owner) throw new InvalidOperationException("只能在根上下文更新字典。"); lock (_state.Gate) return _state.Catalog.Remove(name); }
        /// <summary>
        /// 获取指定名称和条目类型的只读字典快照。
        /// </summary>
        /// <remarks>
        /// 根上下文读取当前目录；记录或生成器快照读取创建快照时捕获的目录。
        /// 列表为浅快照，条目对象的内部状态不会复制。
        /// </remarks>
        /// <typeparam name="T">预期的字典条目类型。</typeparam>
        /// <param name="name">字典名称。</param>
        /// <returns>具有所请求条目类型的只读列表。</returns>
        /// <exception cref="KeyNotFoundException">当前可见目录中不存在该名称。</exception>
        /// <exception cref="InvalidOperationException">字典条目类型与 <typeparamref name="T"/> 不一致。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public IReadOnlyList<T> Dictionary<T>(string name)
        {
            Check(); object value;
            if (_catalog != null) { if (!_catalog.TryGetValue(name, out value)) throw new KeyNotFoundException(name); }
            else { lock (_state.Gate) if (!_state.Catalog.TryGetValue(name, out value)) throw new KeyNotFoundException(name); }
            return value as IReadOnlyList<T> ?? throw new InvalidOperationException("字典条目类型不匹配。");
        }
        /// <summary>
        /// 释放根上下文拥有的同步资源并关闭共享状态。
        /// </summary>
        /// <remarks>
        /// 派生上下文调用此方法不执行释放；根上下文按注册逆序尝试释放所有资源。
        /// 多个释放操作失败时会在继续尝试后以聚合异常报告。
        /// 目标框架支持异步释放且存在仅支持异步释放的资源时，应改用 <c>DisposeAsync</c>。
        /// </remarks>
        /// <exception cref="AggregateException">一个或多个拥有的同步资源释放失败。</exception>
        public void Dispose()
        {
            if (!_owner) return;
            List<object> resources;
            lock (_state.Gate)
            {
                if (_state.Disposed) return;
#if NET10_0_OR_GREATER
                if (_state.Resources.Exists(x => x is IAsyncDisposable && !(x is IDisposable)))
                    throw new InvalidOperationException("作用域拥有仅支持异步释放的资源，请使用 DisposeAsync。");
#endif
                resources = BeginDispose();
            }
            List<Exception> errors = null;
            for (int i = resources.Count - 1; i >= 0; i--) try { ((IDisposable)resources[i]).Dispose(); } catch (Exception e) { (errors ?? (errors = new List<Exception>())).Add(e); }
            if (errors != null) throw new AggregateException(errors);
        }
        /// <summary>
        /// 原子地标记共享状态已关闭并取出所有已拥有对象。
        /// </summary>
        /// <returns>按注册顺序复制的待释放对象列表。</returns>
        private List<object> BeginDispose()
        {
            _state.Disposed = true; var resources = new List<object>(_state.Resources);
            _state.Resources.Clear(); _state.Catalog.Clear(); _state.Extensions.Clear(); _state.Registrations.Clear(); return resources;
        }
#if NET10_0_OR_GREATER
        /// <summary>
        /// 异步释放根上下文拥有的资源并关闭共享状态。
        /// </summary>
        /// <remarks>
        /// 资源按注册逆序释放；支持异步释放时优先使用异步方法，否则调用同步释放方法。
        /// 多个释放操作失败时会在继续尝试后以聚合异常报告。
        /// </remarks>
        /// <exception cref="AggregateException">一个或多个拥有的资源释放失败。</exception>
        public async System.Threading.Tasks.ValueTask DisposeAsync()
        {
            if (!_owner) return;
            List<object> resources; lock (_state.Gate) { if (_state.Disposed) return; resources = BeginDispose(); }
            List<Exception> errors = null;
            for (int i = resources.Count - 1; i >= 0; i--)
                try { if (resources[i] is IAsyncDisposable a) await a.DisposeAsync().ConfigureAwait(false); else ((IDisposable)resources[i]).Dispose(); }
                catch (Exception e) { (errors ?? (errors = new List<Exception>())).Add(e); }
            if (errors != null) throw new AggregateException(errors);
        }
#endif
        /// <summary>
        /// 保存根上下文共享且仅创建一次的状态。
        /// </summary>
        /// <remarks>
        /// 所有派生上下文共享此对象；根上下文释放后，派生上下文的检查也会失败。
        /// </remarks>
        private sealed class State
        {
            internal readonly Dictionary<string, object> Extensions = new Dictionary<string, object>(StringComparer.Ordinal);
            internal readonly Dictionary<string, string> ExtensionVersions = new Dictionary<string, string>(StringComparer.Ordinal);
            /// <summary>
            /// 保存确定性生成使用的种子。
            /// </summary>
            internal readonly int Seed;
            /// <summary>
            /// 保存生成日期时使用的固定参考时间。
            /// </summary>
            internal readonly DateTimeOffset ReferenceTime;
            /// <summary>
            /// 保存上下文使用的区域标记。
            /// </summary>
            internal readonly string Locale;
            /// <summary>
            /// 保存创建上下文时复制的生成预算。
            /// </summary>
            internal readonly MockLimits Limits;
            /// <summary>
            /// 保存创建上下文时复制的具名生成器注册表。
            /// </summary>
            internal readonly Dictionary<string, MockOptions.Registration> Registrations;
            /// <summary>
            /// 保存根上下文当前的具名字典目录。
            /// </summary>
            internal readonly Dictionary<string, object> Catalog = new Dictionary<string, object>(StringComparer.Ordinal);
            /// <summary>
            /// 保存由根上下文拥有并负责释放的资源。
            /// </summary>
            internal readonly List<object> Resources;
            /// <summary>
            /// 同步保护注册表、目录和释放状态。
            /// </summary>
            internal readonly object Gate = new object();
            /// <summary>
            /// 指示共享状态是否已关闭。
            /// </summary>
            internal volatile bool Disposed;

            /// <summary>
            /// 从选项快照初始化共享状态。
            /// </summary>
            /// <param name="options">提供种子、区域、注册和资源的选项。</param>
            /// <param name="limits">已复制并验证的预算。</param>
            internal State(MockOptions options, MockLimits limits)
            {
                if (options.Seed.HasValue) Seed = options.Seed.Value;
                else { var bytes = new byte[4]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes); Seed = BitConverter.ToInt32(bytes, 0); }
                ReferenceTime = options.ReferenceTime ?? DateTimeOffset.UtcNow; Locale = options.Locale; Limits = limits;
                Registrations = new Dictionary<string, MockOptions.Registration>(options.Registrations, StringComparer.Ordinal);
                Resources = new List<object>(options.OwnedResources);
                try
                {
                    if (System.Linq.Enumerable.Count(options.Extensions.Keys, key => key.StartsWith("schema:", StringComparison.Ordinal)) > limits.MaxCompiledSchemas)
                        throw new MockLimitException(nameof(MockLimits.MaxCompiledSchemas), limits.MaxCompiledSchemas);
                    foreach (var entry in options.Extensions)
                    {
                        var resource = entry.Value.Factory == null ? entry.Value.Instance : entry.Value.Factory();
                        if (resource == null) throw new InvalidOperationException("扩展工厂返回 null：" + entry.Key);
                        if (entry.Value.Factory != null)
                        {
                            if (!(resource is IDisposable)) throw new InvalidOperationException("扩展工厂资源必须实现 IDisposable。");
                            if (!Resources.Exists(x => ReferenceEquals(x, resource))) Resources.Add(resource);
                        }
                        var version = entry.Value.VersionFactory == null ? entry.Value.Version : entry.Value.VersionFactory(resource);
                        if (string.IsNullOrWhiteSpace(version)) throw new InvalidOperationException("扩展版本不能为空：" + entry.Key);
                        Extensions.Add(entry.Key, resource); ExtensionVersions.Add(entry.Key, version);
                    }
                    if (options.ExpectedExtensionVersions != null &&
                        (options.ExpectedExtensionVersions.Count != ExtensionVersions.Count ||
                         System.Linq.Enumerable.Any(options.ExpectedExtensionVersions, p => !ExtensionVersions.TryGetValue(p.Key, out var v) || v != p.Value)))
                        throw new InvalidOperationException("重放需要完全相同的扩展和数据版本。");
                }
                catch (Exception constructionError)
                {
                    List<Exception> cleanupErrors = null;
                    for (int i = Resources.Count - 1; i >= 0; i--)
                    {
                        try
                        {
#if NET10_0_OR_GREATER
                            if (Resources[i] is IAsyncDisposable asyncResource)
                                System.Threading.Tasks.Task.Run(async () => await asyncResource.DisposeAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
                            else
#endif
                                (Resources[i] as IDisposable)?.Dispose();
                        }
                        catch (Exception cleanupError)
                        {
                            (cleanupErrors ?? (cleanupErrors = new List<Exception>())).Add(cleanupError);
                        }
                    }

                    if (cleanupErrors != null)
                    {
                        cleanupErrors.Insert(0, constructionError);
                        throw new AggregateException("MockContext 初始化失败，且一个或多个自有资源未能释放。", cleanupErrors);
                    }
                    throw;
                }
            }
        }
    }
}

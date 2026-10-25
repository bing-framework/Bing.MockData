using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Randomizers;

namespace Bing.MockData
{



    /// <summary>
    /// 为随机生成器提供格式化、组合和按索引枚举扩展。
    /// </summary>
    /// <remarks>
    /// 批量及流式操作遵循生成器上下文中的相应记录预算。
    /// </remarks>
    public static class RandomizerExtensions
    {



        /// <summary>
        /// 生成一个值并将其格式化为文本。 未显式传入格式时使用格式化生成器配置的格式，默认使用不变区域性。 格式为 <c>D!</c> 时使用 <c>D</c> 格式并将结果转换为大写。
        /// </summary>
        /// <typeparam name="T">源生成器生成的值类型。</typeparam>
        /// <param name="source">用于生成值的随机生成器。</param>
        /// <param name="format">可选的格式字符串；未指定时使用生成器配置。</param>
        /// <param name="provider">可选的格式提供程序；未指定时使用不变区域性。</param>
        /// <returns>格式化后的文本；生成值或其文本表示为 <see langword="null"/> 时返回 <see langword="null"/>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/>。</exception>
        public static string GenerateAsString<T>(this IRandomizer<T> source, string format = null, IFormatProvider provider = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var configured = source as IFormattedRandomizer;
            format = format ?? configured?.Format;
            bool upper = format == "D!"; if (upper) format = "D";
            var value = source.Generate();
            var text = value is IFormattable f ? f.ToString(format, provider ?? CultureInfo.InvariantCulture) : value?.ToString();
            return upper ? text?.ToUpperInvariant() : text;
        }




        /// <summary>
        /// 生成一个字符串并统一其大小写。 根据 <paramref name="upperCase"/> 将非空结果转换为不变区域性的大写或小写。 源生成器返回 <see langword="null"/> 时仍返回 <see langword="null"/>。
        /// </summary>
        /// <param name="source">用于生成字符串的随机生成器。</param>
        /// <param name="upperCase"><see langword="true"/> 表示转换为大写；否则转换为小写。</param>
        /// <returns>转换大小写后的字符串，或源生成器返回的 <see langword="null"/>。</returns>
        public static string Generate(this IRandomizer<string> source, bool upperCase)
        { var value = source.Generate(); return upperCase ? value?.ToUpperInvariant() : value?.ToLowerInvariant(); }




        /// <summary>
        /// 将内置生成器转为按索引生成的计划；外部实现请通过 MockContext.Generator 注册。 已索引的生成器原样返回，支持快照的生成器会转换为可重放生成器。 不支持快照的自定义生成器无法转换为按索引生成计划。
        /// </summary>
        /// <typeparam name="T">生成值的类型。</typeparam>
        /// <param name="source">要转换的随机生成器。</param>
        /// <returns>与源生成器对应的按索引生成计划。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="NotSupportedException">源生成器未提供重放快照。</exception>
        public static MockGenerator<T> AsGenerator<T>(this IRandomizer<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source is MockGenerator<T> indexed) return indexed;
            if (source is IRandomizerSnapshot snapshot)
            {
                var context = new MockContext(new MockOptions { Seed = snapshot.Seed });
                return context.Generator(c => ((IRandomizer<T>)snapshot.Restart(c.NextSeed())).Generate(), source.GetType().Name);
            }
            throw new NotSupportedException("该生成器没有重放配方；请使用 MockContext.Generator 或 RegisterFactory。");
        }




        /// <summary>
        /// 将值类型生成器包装为可空值生成器。 每条记录按指定概率生成空值，否则使用源生成器生成值。 默认空值概率为 0.1，源生成器必须支持按索引重放。
        /// </summary>
        /// <typeparam name="T">源生成器生成的值类型。</typeparam>
        /// <param name="source">用于生成非空值的生成器。</param>
        /// <param name="probability">生成空值的概率，范围为零到一，包含端点。</param>
        /// <returns>生成可空值的按索引生成器。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> 超出零到一的范围。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="NotSupportedException">源生成器未提供重放快照。</exception>
        public static MockGenerator<T?> Nullable<T>(this IRandomizer<T> source, double probability = 0.1) where T : struct
        {
            NumberData.Probability(probability); var g = source.AsGenerator();
            return g.Context.Generator<T?>(c => c.ForField("null").Numbers.Boolean(probability) ? (T?)null : g.Evaluate(c.ForField("value")), "nullable");
        }




        /// <summary>
        /// 将引用类型生成器包装为可空生成器。 每条记录按指定概率生成空值，否则使用源生成器生成对象。 默认空值概率为 0.1，源生成器必须支持按索引重放。
        /// </summary>
        /// <typeparam name="T">源生成器生成的引用类型。</typeparam>
        /// <param name="source">用于生成非空对象的生成器。</param>
        /// <param name="probability">生成空值的概率，范围为零到一，包含端点。</param>
        /// <returns>生成可空对象的按索引生成器。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> 超出零到一的范围。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="NotSupportedException">源生成器未提供重放快照。</exception>
        public static MockGenerator<T?> OrNull<T>(this IRandomizer<T> source, double probability = 0.1) where T : class
        {
            NumberData.Probability(probability); var g = source.AsGenerator();
            return g.Context.Generator<T?>(c => c.ForField("null").Numbers.Boolean(probability) ? null : g.Evaluate(c.ForField("value")), "nullable");
        }




        /// <summary>
        /// 将生成值映射为另一种类型。 映射委托在对应记录的生成过程中执行。 返回的生成器复用源计划的上下文和记录索引。
        /// </summary>
        /// <typeparam name="T">源生成器生成的值类型。</typeparam>
        /// <typeparam name="TResult">映射后的值类型。</typeparam>
        /// <param name="source">用于提供源值的生成器。</param>
        /// <param name="select">将每个源值转换为结果值的委托。</param>
        /// <returns>生成映射结果的按索引生成器。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="select"/> 或 <paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="NotSupportedException">源生成器未提供重放快照。</exception>
        public static MockGenerator<TResult> Select<T, TResult>(this IRandomizer<T> source, Func<T, TResult> select)
        {
            if (select == null) throw new ArgumentNullException(nameof(select)); var g = source.AsGenerator();
            return g.Context.Generator(c => select(g.Evaluate(c)), "select");
        }




        /// <summary>
        /// 仅保留满足条件的生成值。 每条记录最多尝试上下文允许的次数，尝试之间使用不同字段路径。 达到尝试上限仍未匹配时抛出限制异常。
        /// </summary>
        /// <typeparam name="T">生成值的类型。</typeparam>
        /// <param name="source">用于提供候选值的生成器。</param>
        /// <param name="predicate">判断候选值是否满足条件的委托。</param>
        /// <returns>生成符合条件值的按索引生成器。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> 或 <paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="MockLimitException">达到上下文最大尝试次数仍没有候选值满足条件。</exception>
        /// <exception cref="NotSupportedException">源生成器未提供重放快照。</exception>
        public static MockGenerator<T> Where<T>(this IRandomizer<T> source, Func<T, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate)); var g = source.AsGenerator();
            return g.Context.Generator(c =>
            {
                for (int i = 0; i < c.Budget.MaxAttempts; i++)
                { c.Check(); var value = g.Evaluate(c.ForField("attempt[" + i.ToString(CultureInfo.InvariantCulture) + "]")); if (predicate(value)) return value; }
                throw new MockLimitException(nameof(MockLimits.MaxAttempts), c.Budget.MaxAttempts);
            }, "condition");
        }




        /// <summary>
        /// 按记录索引生成指定数量的值并物化为只读列表。 值在调用期间立即生成，数量受上下文最大物化记录数预算限制。 取消会中止当前操作，但不会撤销已执行的生成工作。
        /// </summary>
        /// <typeparam name="T">生成值的类型。</typeparam>
        /// <param name="source">用于生成记录的随机生成器。</param>
        /// <param name="count">要生成的记录数量，可以为零。</param>
        /// <param name="cancellationToken">用于取消生成的令牌。</param>
        /// <returns>按记录索引排序且不可通过此接口修改的结果列表。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于零。</exception>
        /// <exception cref="MockLimitException"><paramref name="count"/> 超过最大物化记录数预算。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="OperationCanceledException">生成被取消。</exception>
        /// <exception cref="NotSupportedException">源生成器未提供重放快照。</exception>
        public static IReadOnlyList<T> GenerateMany<T>(this IRandomizer<T> source, int count, CancellationToken cancellationToken = default)
        {
            var g = source.AsGenerator(); ValidateCount(count, g.Context.Budget.MaxMaterializedRecords, nameof(MockLimits.MaxMaterializedRecords));
            // 集合与流式额度分别校验，不把集合接口隐式限制到另一额度。
            var snapshot = g.Snapshot(); var result = new T[count];
            for (int i = 0; i < count; i++) result[i] = snapshot.At(i, cancellationToken);
            return Array.AsReadOnly(result);
        }




        /// <summary>
        /// 创建按记录索引惰性生成的有限序列。 每次开始枚举时创建独立快照，重复枚举会从索引零重新生成。 枚举数量受上下文最大流式记录数预算限制。
        /// </summary>
        /// <typeparam name="T">生成值的类型。</typeparam>
        /// <param name="source">用于生成记录的随机生成器。</param>
        /// <param name="count">要枚举的记录数量，可以为零。</param>
        /// <param name="cancellationToken">用于取消枚举的令牌。</param>
        /// <returns>按记录索引顺序生成值的惰性序列。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于零。</exception>
        /// <exception cref="MockLimitException"><paramref name="count"/> 超过最大流式记录数预算。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="OperationCanceledException">枚举被取消。</exception>
        /// <exception cref="NotSupportedException">源生成器未提供重放快照。</exception>
        public static IEnumerable<T> Enumerate<T>(this IRandomizer<T> source, int count, CancellationToken cancellationToken = default)
        {
            var g = source.AsGenerator(); ValidateCount(count, g.Context.Budget.MaxStreamingRecords, nameof(MockLimits.MaxStreamingRecords));
            return Iterator();
            IEnumerable<T> Iterator()
            {
                cancellationToken.ThrowIfCancellationRequested(); var snapshot = g.Snapshot();
                for (int i = 0; i < count; i++) yield return snapshot.At(i, cancellationToken);
            }
        }




        /// <summary>
        /// 以有界并行度按记录索引生成有限序列。 每个窗口最多生成 256 项，结果仍按记录索引顺序返回。 消费者取完当前窗口前不会启动后续窗口；指标对象可累计记录多次运行的数据。
        /// </summary>
        /// <typeparam name="T">生成值的类型。</typeparam>
        /// <param name="source">用于生成记录的随机生成器。</param>
        /// <param name="count">要枚举的记录数量，可以为零。</param>
        /// <param name="maxDegreeOfParallelism">单个窗口内允许的最大并行生成数，必须至少为一。</param>
        /// <param name="cancellationToken">用于取消生成和枚举的令牌。</param>
        /// <param name="metrics">可选的并行诊断计数器；如需独立统计，应为每次运行创建新实例。</param>
        /// <returns>按记录索引顺序产生值的惰性序列。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于零，或 <paramref name="maxDegreeOfParallelism"/> 小于一。</exception>
        /// <exception cref="MockLimitException"><paramref name="count"/> 超过最大流式记录数预算。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="OperationCanceledException">生成或枚举被取消。</exception>
        /// <exception cref="NotSupportedException">源生成器未提供重放快照。</exception>
        public static IEnumerable<T> EnumerateParallel<T>(this IRandomizer<T> source, int count, int maxDegreeOfParallelism,
            CancellationToken cancellationToken = default, GenerationMetrics metrics = null)
        {
            var g = source.AsGenerator(); ValidateCount(count, g.Context.Budget.MaxStreamingRecords, nameof(MockLimits.MaxStreamingRecords));
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
            return Iterator();
            IEnumerable<T> Iterator()
            {
                cancellationToken.ThrowIfCancellationRequested(); var snapshot = g.Snapshot();
                var diagnostics = metrics ?? new GenerationMetrics();
                for (int offset = 0; offset < count;)
                {
                    int size = Math.Min(256, count - offset); var values = new T[size]; int start = offset;
                    diagnostics.Queue(size);
                    try
                    {
                        try
                        {
                            Parallel.For(0, size, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = maxDegreeOfParallelism }, i =>
                            { diagnostics.Start(); try { values[i] = snapshot.At(start + i, cancellationToken); diagnostics.Complete(); } finally { diagnostics.End(); } });
                        }
                        catch (AggregateException error)
                        { var failures = error.Flatten().InnerExceptions; if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw(); throw; }
                        for (int i = 0; i < size; i++) { cancellationToken.ThrowIfCancellationRequested(); var value = values[i]; values[i] = default; yield return value; }
                    }
                    finally
                    {
                        diagnostics.ClearQueue();
                        // 即使调用方保留已释放的枚举器，也不保留尚未消费的窗口内容。
                        Array.Clear(values, 0, values.Length);
                    }
                    offset += size;
                }
            }
        }
#if NET10_0_OR_GREATER



        /// <summary>
        /// 创建异步可枚举的有限生成序列。 当前实现按索引依次生成值，并提供异步枚举接口。 枚举数量受上下文最大流式记录数预算限制。
        /// </summary>
        /// <typeparam name="T">生成值的类型。</typeparam>
        /// <param name="source">用于生成记录的随机生成器。</param>
        /// <param name="count">要枚举的记录数量，可以为零。</param>
        /// <param name="cancellationToken">用于取消异步枚举的令牌。</param>
        /// <returns>按记录索引顺序异步产生值的序列。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于零。</exception>
        /// <exception cref="MockLimitException"><paramref name="count"/> 超过最大流式记录数预算。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="OperationCanceledException">异步枚举被取消。</exception>
        /// <exception cref="NotSupportedException">源生成器未提供重放快照。</exception>
        public static async IAsyncEnumerable<T> EnumerateAsync<T>(this IRandomizer<T> source, int count,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            foreach (var value in source.Enumerate(count, cancellationToken)) yield return value;
        }
#endif




        /// <summary>
        /// 验证请求的记录数是否位于允许范围内。 负数触发参数范围异常，超过上限触发生成限制异常。 此方法不执行生成或枚举。
        /// </summary>
        /// <param name="count">请求的记录数量。</param>
        /// <param name="limit">允许的最大记录数量。</param>
        /// <param name="name">超限异常中用于标识预算的名称。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于零。</exception>
        /// <exception cref="MockLimitException"><paramref name="count"/> 大于 <paramref name="limit"/>。</exception>
        internal static void ValidateCount(int count, int limit, string name)
        { if (count < 0) throw new ArgumentOutOfRangeException(nameof(count)); if (count > limit) throw new MockLimitException(name, limit); }
    }




    /// <summary>
    /// 记录并行生成过程中的诊断计数。
    /// </summary>
    /// <remarks>
    /// 最大并行度和排队量记录历史峰值，已生成记录数会在重复使用实例时累积。
    /// </remarks>
    public sealed class GenerationMetrics
    {
        /// <summary>
        /// 保存当前活动数、历史并行峰值及排队计数。
        /// </summary>
        private int _active, _maxActive, _queued, _maxQueued;
        /// <summary>
        /// 保存成功生成记录的累计数量。
        /// </summary>
        private long _completed;




        /// <summary>
        /// 获取同时执行生成的历史最大数量。 此值在实例生命周期内只增不减。 若尚未开始生成则返回零。
        /// </summary>
        /// <returns>观测到的最大并行生成数。</returns>
        public int MaxActualParallelism => _maxActive;




        /// <summary>
        /// 获取同时排队等待生成的历史最大记录数。 统计的是当前并行窗口的排队量，不包括后续窗口。 若尚无记录排队则返回零。
        /// </summary>
        /// <returns>观测到的最大排队记录数。</returns>
        public int MaxQueuedRecords => _maxQueued;




        /// <summary>
        /// 获取此计数器记录的成功生成总数。 读取使用原子操作进行。 重复使用计数器时此值会跨运行累积。
        /// </summary>
        /// <returns>累计成功生成的记录数。</returns>
        public long GeneratedRecords => Interlocked.Read(ref _completed);




        /// <summary>
        /// 以原子方式更新整数的历史最大值。 并发调用不会覆盖更大的已记录数值。 此方法仅供指标实现内部使用。
        /// </summary>
        /// <param name="target">要更新的最大值字段。</param>
        /// <param name="value">候选值。</param>
        private static void Max(ref int target, int value) { int old; do { old = Volatile.Read(ref target); if (old >= value) return; } while (Interlocked.CompareExchange(ref target, value, old) != old); }




        /// <summary>
        /// 记录当前窗口的排队记录数。 同时更新排队量的历史最大值。 此方法供并行枚举实现调用。
        /// </summary>
        /// <param name="count">当前排队记录数。</param>
        internal void Queue(int count) { Volatile.Write(ref _queued, count); Max(ref _maxQueued, count); }




        /// <summary>
        /// 记录一个生成任务开始执行。 减少当前排队数并更新活动任务峰值。 此方法供并行枚举实现调用。
        /// </summary>
        internal void Start() { Interlocked.Decrement(ref _queued); Max(ref _maxActive, Interlocked.Increment(ref _active)); }




        /// <summary>
        /// 记录一条记录成功生成。 成功计数使用原子增量。 此方法供并行枚举实现调用。
        /// </summary>
        internal void Complete() => Interlocked.Increment(ref _completed);




        /// <summary>
        /// 记录一个生成任务结束执行。 减少当前活动任务数。 此方法供并行枚举实现调用。
        /// </summary>
        internal void End() => Interlocked.Decrement(ref _active);




        /// <summary>
        /// 将当前窗口的排队任务数清零。 用于并行窗口结束或失败后的计数收尾。 历史最大排队数和成功记录数不受影响。
        /// </summary>
        internal void ClearQueue() => Volatile.Write(ref _queued, 0);
    }
}

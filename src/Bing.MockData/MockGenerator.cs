using System;
using System.Threading;
using Bing.MockData.Abstractions.Randomizers;

namespace Bing.MockData
{
    /// <summary>
    /// 提供按显式记录索引可重复生成的模拟值计划。
    /// </summary>
    /// <remarks>
    /// 每个实例的 <see cref="Generate"/> 序号独立递增；快照生成器从记录索引零开始。
    /// 生成器共享上下文生命周期，不拥有或释放上下文资源。
    /// </remarks>
    public sealed class MockGenerator<T> : IRandomizer<T>
    {
        /// <summary>
        /// 保存此生成器关联的上下文。
        /// </summary>
        internal readonly MockContext Context;
        /// <summary>
        /// 保存根据记录上下文生成值的委托。
        /// </summary>
        private readonly Func<MockContext, T> _generate;
        /// <summary>
        /// 保存生成失败诊断使用的规则标识。
        /// </summary>
        private readonly string _ruleId;
        /// <summary>
        /// 保存此生成器下一条顺序生成记录的索引。
        /// </summary>
        private long _next = -1;

        /// <summary>
        /// 创建与指定上下文关联的按索引生成器。
        /// </summary>
        /// <param name="context">提供种子、预算和生命周期的上下文。</param>
        /// <param name="generate">接收记录上下文并生成一个值的委托。</param>
        /// <param name="ruleId">用于生成失败诊断的规则标识。</param>
        /// <exception cref="ArgumentNullException"><paramref name="generate"/> 或 <paramref name="ruleId"/> 为 <see langword="null"/>。</exception>
        internal MockGenerator(MockContext context, Func<MockContext, T> generate, string ruleId)
        { Context = context; _generate = generate ?? throw new ArgumentNullException(nameof(generate)); _ruleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId)); }

        /// <summary>
        /// 按此实例的下一个顺序记录索引生成一个值。
        /// </summary>
        /// <returns>由生成委托产生的值。</returns>
        /// <exception cref="MockGenerationException">生成委托抛出未被专门传播的异常。</exception>
        /// <exception cref="MockLimitException">生成值超过上下文预算。</exception>
        /// <exception cref="ObjectDisposedException">关联的根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">关联上下文已取消。</exception>
        public T Generate() => At(Interlocked.Increment(ref _next));

        /// <summary>
        /// 按指定记录索引生成一个值。
        /// </summary>
        /// <remarks>
        /// 每次调用均创建该索引对应的记录上下文，不改变此实例的顺序生成计数。
        /// </remarks>
        /// <param name="index">要生成的非负记录索引。</param>
        /// <param name="cancellationToken">可选的取消令牌；不可取消时继承生成器上下文令牌。</param>
        /// <returns>指定记录索引对应的生成值。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> 小于零。</exception>
        /// <exception cref="MockGenerationException">生成委托抛出未被专门传播的异常。</exception>
        /// <exception cref="MockLimitException">生成值超过上下文预算。</exception>
        /// <exception cref="ObjectDisposedException">关联的根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">关联上下文或传入令牌已取消。</exception>
        public T At(long index, CancellationToken cancellationToken = default) => Evaluate(Context.ForRecord(index, cancellationToken));

        /// <summary>
        /// 在指定上下文中执行生成委托并统一处理生成结果。
        /// </summary>
        /// <param name="context">用于本次求值的记录或字段上下文。</param>
        /// <returns>生成委托返回的值。</returns>
        /// <exception cref="MockGenerationException">生成委托抛出未被专门传播的异常。</exception>
        /// <exception cref="MockLimitException">生成的字符串超过文本长度预算。</exception>
        /// <exception cref="ObjectDisposedException">关联的根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">上下文已取消。</exception>
        internal T Evaluate(MockContext context)
        {
            context.Check();
            try
            {
                var value = _generate(context);
                context.Check();
                if (value is string text && text.Length > context.Budget.MaxTextLength)
                    throw new MockLimitException(nameof(MockLimits.MaxTextLength), context.Budget.MaxTextLength);
                return value;
            }
            catch (OperationCanceledException) { throw; }
            catch (MockGenerationException) { throw; }
            catch (MockLimitException) { throw; }
            catch (ObjectDisposedException) { throw; }
            catch (Exception e) { throw new MockGenerationException(_ruleId, context, e); }
        }

        /// <summary>
        /// 创建从记录索引零开始的独立生成器快照。
        /// </summary>
        /// <remarks>
        /// 快照捕获创建时可见的字典目录，但共享根上下文的资源生命周期和取消状态。
        /// </remarks>
        /// <returns>规则相同且顺序计数重置为零的生成器。</returns>
        /// <exception cref="ObjectDisposedException">关联的根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">关联上下文已取消。</exception>
        internal MockGenerator<T> Snapshot() => new MockGenerator<T>(Context.Snapshot(), _generate, _ruleId);
    }
}

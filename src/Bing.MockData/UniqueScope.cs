using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bing.MockData
{



    /// <summary>
    /// 在显式作用域内为生成器生成不重复的值。
    /// </summary>
    /// <remarks>
    /// 历史值受上下文预算限制；释放作用域会清除历史值，但不会释放关联的生成器或上下文。
    /// </remarks>
    public sealed class UniqueScope<T> : IDisposable
    {
        /// <summary>
        /// 保存用于生成候选值的生成器。
        /// </summary>
        private readonly MockGenerator<T> _generator;
        /// <summary>
        /// 保存当前作用域内已返回的值。
        /// </summary>
        private readonly HashSet<T> _seen;
        /// <summary>
        /// 串行化生成、去重和释放操作。
        /// </summary>
        private readonly object _gate = new object();
        /// <summary>
        /// 指示作用域是否已释放。
        /// </summary>
        private bool _disposed;




        /// <summary>
        /// 创建一个新的去重作用域。 作用域从空历史开始，并使用提供的相等比较器去重。 构造函数不取得生成器或其上下文的释放所有权。
        /// </summary>
        /// <param name="generator">生成候选值的生成器。</param>
        /// <param name="comparer">用于比较候选值的比较器；省略时使用默认相等比较器。</param>
        /// <exception cref="ArgumentNullException"><paramref name="generator"/> 为 <see langword="null"/>。</exception>
        public UniqueScope(MockGenerator<T> generator, IEqualityComparer<T> comparer = null)
        { _generator = generator ?? throw new ArgumentNullException(nameof(generator)); _seen = new HashSet<T>(comparer); }




        /// <summary>
        /// 生成并返回一个此前未在此作用域中返回的值。 并发调用会在作用域锁内串行执行，且每次最多进行上下文允许的尝试次数。 无法在预算内找到新值时抛出限制异常。
        /// </summary>
        /// <returns>此作用域内新的唯一值。</returns>
        /// <exception cref="ObjectDisposedException">此作用域已释放。</exception>
        /// <exception cref="MockLimitException">达到唯一值上限，或尝试次数耗尽仍未找到新值。</exception>
        /// <exception cref="OperationCanceledException">生成过程被上下文取消。</exception>
        public T Next()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(UniqueScope<T>));
                var context = _generator.Context; context.Check();
                if (_seen.Count >= context.Budget.MaxUniqueValues) throw new MockLimitException(nameof(MockLimits.MaxUniqueValues), context.Budget.MaxUniqueValues);
                for (int i = 0; i < context.Budget.MaxAttempts; i++) { context.Check(); var value = _generator.Generate(); if (_seen.Add(value)) return value; }
                throw new MockLimitException(nameof(MockLimits.MaxAttempts), context.Budget.MaxAttempts);
            }
        }




        /// <summary>
        /// 释放此去重作用域。 释放后清空已记录的值，后续调用 <see cref="Next"/> 将失败。 关联的生成器和上下文仍由其原有所有者管理。
        /// </summary>
        public void Dispose() { lock (_gate) { _disposed = true; _seen.Clear(); } }
    }
    public sealed partial class MockContext
    {



        /// <summary>
        /// 创建按记录索引递增的长整数生成器。 索引从零开始，生成值为起始值加记录索引乘步长。 生成值超出 <see cref="long"/> 范围时会失败。
        /// </summary>
        /// <param name="start">记录索引为零时的起始值。</param>
        /// <param name="step">每条记录的增量；不能为零，可以为负数。</param>
        /// <returns>根据记录索引生成序列值的生成器。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> 为零。</exception>
        public MockGenerator<long> Sequence(long start = 1, long step = 1)
        { if (step == 0) throw new ArgumentOutOfRangeException(nameof(step)); return Generator(c => checked(start + checked(c.RecordIndex * step)), "sequence"); }




        /// <summary>
        /// 创建按记录索引递增的文本标识符生成器。 标识符由前缀和格式化后的序列数字组成，序列从记录索引零开始。 生成文本受上下文最大文本长度预算约束。
        /// </summary>
        /// <param name="prefix">添加在序列数字之前的前缀。</param>
        /// <param name="start">记录索引为零时的起始值。</param>
        /// <param name="step">每条记录的增量；不能为零，可以为负数。</param>
        /// <param name="format">传递给不变区域性数值格式化的格式字符串。</param>
        /// <returns>根据记录索引生成格式化标识符的生成器。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="prefix"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> 为零。</exception>
        /// <exception cref="FormatException"><paramref name="format"/> 不是有效的数值格式字符串。</exception>
        public MockGenerator<string> Identifier(string prefix = "", long start = 1, long step = 1, string format = "D")
        {
            if (prefix == null) throw new ArgumentNullException(nameof(prefix)); if (step == 0) throw new ArgumentOutOfRangeException(nameof(step));
            start.ToString(format, CultureInfo.InvariantCulture);
            return Generator(c => { var value = prefix + checked(start + checked(c.RecordIndex * step)).ToString(format, CultureInfo.InvariantCulture);
                if (value.Length > c.Budget.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), c.Budget.MaxTextLength); return value; }, "identifier");
        }
    }
}

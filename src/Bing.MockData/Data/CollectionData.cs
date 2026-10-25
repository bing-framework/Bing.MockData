using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.MockData
{
    /// <summary>
    /// 提供候选集合的均匀、有权重和不放回抽样操作。
    /// </summary>
    public readonly struct CollectionData
    {
        /// <summary>
        /// 提供集合容量预算、随机状态和取消检查的上下文。
        /// </summary>
        private readonly MockContext _context;
        /// <summary>
        /// 绑定集合数据入口与其生成上下文。
        /// </summary>
        /// <param name="context">提供随机状态和集合预算的上下文。</param>
        internal CollectionData(MockContext context) { _context = context; }

        /// <summary>
        /// 从非空候选列表中等概率抽取一个元素。
        /// </summary>
        /// <typeparam name="T">候选元素类型。</typeparam>
        /// <param name="values">不能为 <see langword="null"/> 的候选列表；列表元素可以为空值，且列表必须至少包含一个元素。</param>
        /// <returns>列表中的一个随机元素。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="values"/> 为空时抛出。</exception>
        /// <exception cref="ArgumentException"><paramref name="values"/> 为空列表时抛出。</exception>
        public T Pick<T>(IReadOnlyList<T> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count == 0) throw new ArgumentException("候选集合不能为空。", nameof(values));
            return values[_context.Numbers.Int(0, values.Count - 1)];
        }
        /// <summary>
        /// 按给定权重从候选列表中抽取一个元素。
        /// </summary>
        /// <typeparam name="T">候选元素类型。</typeparam>
        /// <param name="values">与权重逐项对应的候选列表。</param>
        /// <param name="weights">有限、非负且至少有一项大于零的权重列表。</param>
        /// <returns>按归一化权重选出的候选元素；零权重项不会被选中。</returns>
        /// <exception cref="ArgumentNullException">任一列表为空引用时抛出。</exception>
        /// <exception cref="ArgumentException">列表为空或元素数量不一致时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">权重包含非有限值或负数，或权重总和不是正有限值时抛出。</exception>
        public T Weighted<T>(IReadOnlyList<T> values, IReadOnlyList<double> weights)
        {
            if (values == null || weights == null) throw new ArgumentNullException(values == null ? nameof(values) : nameof(weights));
            if (values.Count == 0 || values.Count != weights.Count) throw new ArgumentException("候选项与权重数量必须相等且非空。");
            double total = 0;
            for (int i = 0; i < weights.Count; i++) { _context.Check(); if (double.IsNaN(weights[i]) || double.IsInfinity(weights[i]) || weights[i] < 0) throw new ArgumentOutOfRangeException(nameof(weights)); total += weights[i]; }
            if (total <= 0 || double.IsInfinity(total)) throw new ArgumentOutOfRangeException(nameof(weights));
            var point = _context.WithRandom(r => r.NextDouble()) * total;
            int last = -1;
            for (int i = 0; i < values.Count; i++) { _context.Check(); if (weights[i] == 0) continue; last = i; point -= weights[i]; if (point < 0) return values[i]; }
            return values[last];
        }
        /// <summary>
        /// 从候选列表中均匀地无放回抽取指定数量的元素。
        /// </summary>
        /// <typeparam name="T">候选元素类型。</typeparam>
        /// <param name="values">候选列表；原列表不会被重排或修改。</param>
        /// <param name="count">抽取数量，范围为 0 到候选项数，并受集合预算限制。</param>
        /// <returns>包含抽取结果的只读列表。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="values"/> 为空时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 为负数或大于候选项数时抛出。</exception>
        /// <exception cref="MockLimitException"><paramref name="count"/> 超过上下文的集合容量预算时抛出。</exception>
        public IReadOnlyList<T> Sample<T>(IReadOnlyList<T> values, int count)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (count < 0 || count > values.Count) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > _context.Budget.MaxCollectionSize) throw new MockLimitException(nameof(MockLimits.MaxCollectionSize), _context.Budget.MaxCollectionSize);
            _context.Check();
            var result = new T[count]; var swaps = new Dictionary<int, int>();
            for (int i = 0; i < count; i++)
            {
                _context.Check(); int j = _context.Numbers.Int(i, values.Count - 1);
                int selected = swaps.TryGetValue(j, out var sj) ? sj : j;
                swaps[j] = swaps.TryGetValue(i, out var si) ? si : i;
                result[i] = values[selected];
            }
            return Array.AsReadOnly(result);
        }

        /// <summary>
        /// 返回候选列表中所有元素的一份随机排列副本。
        /// </summary>
        /// <typeparam name="T">候选元素类型。</typeparam>
        /// <param name="values">要打乱的候选列表；原列表不会被修改。</param>
        /// <returns>包含所有原元素的只读随机排列。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="values"/> 为空时抛出。</exception>
        /// <exception cref="MockLimitException">列表元素数超过上下文的集合容量预算时抛出。</exception>
        public IReadOnlyList<T> Shuffle<T>(IReadOnlyList<T> values) => Sample(values, values?.Count ?? throw new ArgumentNullException(nameof(values)));
    }
}

using System;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData
{
    /// <summary>
    /// 提供日期、时间和时长的上下文范围抽样入口。
    /// </summary>
    public readonly struct DateData
    {
        /// <summary>
        /// 提供冻结参考时间、随机状态和取消检查的上下文。
        /// </summary>
        private readonly MockContext _context;
        /// <summary>
        /// 绑定日期数据入口与其生成上下文。
        /// </summary>
        /// <param name="context">提供时间基准和随机状态的上下文。</param>
        internal DateData(MockContext context) { _context = context; }

        /// <summary>
        /// 在两个日期时间端点之间按闭区间抽取一个值。
        /// </summary>
        /// <param name="from">包含在抽样范围内的下界。</param>
        /// <param name="to">包含在抽样范围内的上界；其 <see cref="DateTime.Kind"/> 必须与 <paramref name="from"/> 相同。</param>
        /// <returns>闭区间内且保留相同 <see cref="DateTime.Kind"/> 的日期时间。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> 早于 <paramref name="from"/> 时抛出。</exception>
        /// <exception cref="ArgumentException">两个端点的 <see cref="DateTime.Kind"/> 不一致时抛出。</exception>
        public DateTime Between(DateTime from, DateTime to) => _context.Numbers.Between(from, to);

        /// <summary>
        /// 沿 UTC 时间轴在两个偏移时间端点之间抽样。
        /// </summary>
        /// <param name="from">闭区间下界；其偏移也作为结果偏移。</param>
        /// <param name="to">闭区间上界。</param>
        /// <returns>与 UTC 轴上抽样时刻相同、并使用 <paramref name="from"/> 偏移的结果。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> 早于 <paramref name="from"/> 时抛出。</exception>
        /// <exception cref="ArgumentException">给定偏移下没有可表示的结果时抛出。</exception>
        public DateTimeOffset Between(DateTimeOffset from, DateTimeOffset to)
        {
            if (from > to) throw new ArgumentOutOfRangeException(nameof(to));
            // 限制在指定输出偏移下也可表示的 UTC 时间范围。
            long low = Math.Max(from.UtcTicks, Math.Max(0, -from.Offset.Ticks));
            long high = Math.Min(to.UtcTicks, Math.Min(DateTime.MaxValue.Ticks, DateTime.MaxValue.Ticks - from.Offset.Ticks));
            if (low > high) throw new ArgumentException("该偏移下没有可表示日期。");
            return new DateTimeOffset(_context.Numbers.Long(low, high), TimeSpan.Zero).ToOffset(from.Offset);
        }

        /// <summary>
        /// 在参考时间之前的指定长度内抽取一个日期时间。
        /// </summary>
        /// <param name="maximumAge">向过去延伸的最大时长，必须非负。</param>
        /// <returns>闭区间 [参考时间减去最大时长, 参考时间] 内的时间。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumAge"/> 为负数或计算边界超出日期时间范围时抛出。</exception>
        public DateTimeOffset Past(TimeSpan maximumAge)
        { if (maximumAge < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumAge)); return Between(_context.ReferenceTime - maximumAge, _context.ReferenceTime); }

        /// <summary>
        /// 在参考时间之后的指定长度内抽取一个日期时间。
        /// </summary>
        /// <param name="maximumDelay">向未来延伸的最大时长，必须非负。</param>
        /// <returns>闭区间 [参考时间, 参考时间加最大时长] 内的时间。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumDelay"/> 为负数或计算边界超出日期时间范围时抛出。</exception>
        public DateTimeOffset Future(TimeSpan maximumDelay)
        { if (maximumDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumDelay)); return Between(_context.ReferenceTime, _context.ReferenceTime + maximumDelay); }

        /// <summary>
        /// 在两个时长端点之间按闭区间抽取一个值。
        /// </summary>
        /// <param name="min">包含在范围内的最小时长。</param>
        /// <param name="max">包含在范围内的最大时长。</param>
        /// <returns>闭区间内的随机时长。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> 小于 <paramref name="min"/> 时抛出。</exception>
        public TimeSpan Duration(TimeSpan min, TimeSpan max) => _context.Numbers.Between(min, max);
#if NET10_0_OR_GREATER

        /// <summary>
        /// 在两个日历日期端点之间按闭区间抽取一天。
        /// </summary>
        /// <param name="from">包含在范围内的起始日期。</param>
        /// <param name="to">包含在范围内的结束日期。</param>
        /// <returns>闭区间内的日期。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> 早于 <paramref name="from"/> 时抛出。</exception>
        public DateOnly Between(DateOnly from, DateOnly to)
        { if (from > to) throw new ArgumentOutOfRangeException(nameof(to)); return DateOnly.FromDayNumber(_context.Numbers.Int(from.DayNumber, to.DayNumber)); }

        /// <summary>
        /// 在两个当日时间端点之间按闭区间抽取一个值。
        /// </summary>
        /// <param name="from">包含在范围内的起始时刻。</param>
        /// <param name="to">包含在范围内的结束时刻。</param>
        /// <returns>闭区间内的当日时间。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> 早于 <paramref name="from"/> 时抛出。</exception>
        public TimeOnly Between(TimeOnly from, TimeOnly to)
        { if (from > to) throw new ArgumentOutOfRangeException(nameof(to)); return new TimeOnly(_context.Numbers.Long(from.Ticks, to.Ticks)); }
#endif
    }
}

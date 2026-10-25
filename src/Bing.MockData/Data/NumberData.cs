using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData
{
    /// <summary>
    /// 提供共享上下文随机状态的数值、概率和枚举抽样入口。
    /// </summary>
    public readonly struct NumberData
    {
        /// <summary>
        /// 持有当前字段路径、记录序号和随机状态的上下文。
        /// </summary>
        private readonly MockContext _context;
        /// <summary>
        /// 绑定数值数据入口与其生成上下文。
        /// </summary>
        /// <param name="context">提供随机状态、取消和生命周期检查的上下文。</param>
        internal NumberData(MockContext context) { _context = context; }

        /// <summary>
        /// 在两个给定端点之间按闭区间生成一个值。
        /// </summary>
        /// <typeparam name="T">支持的数值、布尔、日期或时长结构类型。</typeparam>
        /// <param name="min">允许生成的最小值。</param>
        /// <param name="max">允许生成的最大值，必须不小于 <paramref name="min"/>。</param>
        /// <returns>包含两个端点在内的随机值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">上下界顺序错误、浮点边界不是有限值时抛出。</exception>
        /// <exception cref="ArgumentException">日期上下界的 <see cref="DateTime.Kind"/> 不一致时抛出。</exception>
        /// <exception cref="NotSupportedException"><typeparamref name="T"/> 不属于支持的类型时抛出。</exception>
        public T Between<T>(T min, T max) where T : struct
        { RandomValueGenerator.Validate(min, max); return _context.WithRandom(r => RandomValueGenerator.Next(r, min, max)); }

        /// <summary>
        /// 生成闭区间内的有符号整数。
        /// </summary>
        /// <param name="min">最小整数，默认值为 0。</param>
        /// <param name="max">最大整数，默认值为 100。</param>
        /// <returns>大于或等于 <paramref name="min"/> 且小于或等于 <paramref name="max"/> 的整数。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> 小于 <paramref name="min"/> 时抛出。</exception>
        public int Int(int min = 0, int max = 100) => Between(min, max);

        /// <summary>
        /// 生成闭区间内的长整数。
        /// </summary>
        /// <param name="min">最小整数，默认值为 0。</param>
        /// <param name="max">最大整数，默认值为 <see cref="long.MaxValue"/>。</param>
        /// <returns>大于或等于 <paramref name="min"/> 且小于或等于 <paramref name="max"/> 的长整数。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> 小于 <paramref name="min"/> 时抛出。</exception>
        public long Long(long min = 0, long max = long.MaxValue) => Between(min, max);

        /// <summary>
        /// 生成闭区间内的无符号整数。
        /// </summary>
        /// <param name="min">最小整数，默认值为 0。</param>
        /// <param name="max">最大整数，默认值为 <see cref="uint.MaxValue"/>。</param>
        /// <returns>处于指定闭区间内的无符号整数。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> 小于 <paramref name="min"/> 时抛出。</exception>
        public uint UInt(uint min = 0, uint max = uint.MaxValue) => Between(min, max);

        /// <summary>
        /// 生成闭区间内的无符号长整数。
        /// </summary>
        /// <param name="min">最小整数，默认值为 0。</param>
        /// <param name="max">最大整数，默认值为 <see cref="ulong.MaxValue"/>。</param>
        /// <returns>处于指定闭区间内的无符号长整数。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> 小于 <paramref name="min"/> 时抛出。</exception>
        public ulong ULong(ulong min = 0, ulong max = ulong.MaxValue) => Between(min, max);

        /// <summary>
        /// 生成闭区间内的双精度浮点数。
        /// </summary>
        /// <param name="min">有限的最小值，默认值为 0。</param>
        /// <param name="max">有限的最大值，默认值为 1。</param>
        /// <returns>处于指定闭区间内的有限浮点数。</returns>
        /// <exception cref="ArgumentOutOfRangeException">边界不是有限值或 <paramref name="max"/> 小于 <paramref name="min"/> 时抛出。</exception>
        public double Double(double min = 0, double max = 1) => Between(min, max);

        /// <summary>
        /// 按指定概率生成 <see langword="true"/>。
        /// </summary>
        /// <param name="probability"><see langword="true"/> 的概率，必须位于 0 到 1 之间，默认值为 0.5。</param>
        /// <returns>以给定概率为 <see langword="true"/> 的布尔值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">概率为 NaN 或不在闭区间 [0,1] 内时抛出。</exception>
        public bool Boolean(double probability = 0.5)
        { Probability(probability); return _context.WithRandom(r => r.NextDouble() < probability); }

        /// <summary>
        /// 从枚举已声明的不同成员值中等概率抽取一个值。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <returns>从该枚举的不同常量值中选出的值。</returns>
        /// <exception cref="ArgumentException">枚举没有任何成员时，无法从空集合中抽样并抛出。</exception>
        public T Enum<T>() where T : struct, Enum
        { return _context.Collections.Pick(System.Enum.GetValues(typeof(T)).Cast<T>().Distinct().ToArray()); }

        /// <summary>
        /// 在从零点起算的十进制步长刻度上等概率抽取一个值。
        /// </summary>
        /// <param name="min">允许值的下界。</param>
        /// <param name="max">允许值的上界，必须不小于 <paramref name="min"/>。</param>
        /// <param name="decimalPlaces">参与步长的最大小数位数，范围为 0 到 28。</param>
        /// <param name="step">可选的正步长；必须能由 <paramref name="decimalPlaces"/> 表示。为空时使用 10 的负 <paramref name="decimalPlaces"/> 次幂。</param>
        /// <returns>范围内与零对齐的有效步长倍数；上下界会向内对齐。</returns>
        /// <exception cref="ArgumentOutOfRangeException">小数位数超出范围或步长不为正数时抛出。</exception>
        /// <exception cref="ArgumentException">步长精度超出配置或区间内没有有效刻度时抛出。</exception>
        public decimal Decimal(decimal min = 0, decimal max = 100, int decimalPlaces = 2, decimal? step = null)
        {
            if (decimalPlaces < 0 || decimalPlaces > 28) throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
            decimal defaultStep = 1; for (int i = 0; i < decimalPlaces; i++) defaultStep /= 10;
            var actualStep = step ?? defaultStep;
            return _context.WithRandom(r => DecimalMath.Next(r, min, max, decimalPlaces, actualStep));
        }
        /// <summary>
        /// 验证概率参数是否处于闭区间 [0,1] 内。
        /// </summary>
        /// <param name="value">待验证的概率值。</param>
        /// <exception cref="ArgumentOutOfRangeException">概率为 NaN 或超出允许范围时抛出。</exception>
        internal static void Probability(double value)
        { if (double.IsNaN(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value), "概率必须在 [0,1] 内。"); }
    }

    /// <summary>
    /// 执行十进制定点数转换和随机刻度抽样的内部工具。
    /// </summary>
    internal static class DecimalMath
    {
        /// <summary>
        /// 将十进制值精确缩放到指定的小数位数。
        /// </summary>
        /// <param name="value">待转换的十进制数。</param>
        /// <param name="scale">目标缩放位数，当前调用使用 28 位。</param>
        /// <returns>表示缩放后整数的任意精度值。</returns>
        private static BigInteger Scaled(decimal value, int scale)
        {
            int[] bits = decimal.GetBits(value);
            var integer = (BigInteger)(uint)bits[0] | ((BigInteger)(uint)bits[1] << 32) | ((BigInteger)(uint)bits[2] << 64);
            if (bits[3] < 0) integer = -integer;
            return integer * BigInteger.Pow(10, scale - ((bits[3] >> 16) & 255));
        }
        /// <summary>
        /// 从两个端点内的零起算步长刻度中等概率抽样。
        /// </summary>
        /// <param name="random">提供随机字节的实例随机源。</param>
        /// <param name="min">闭区间下界。</param>
        /// <param name="max">闭区间上界。</param>
        /// <param name="places">输出刻度允许的小数位数。</param>
        /// <param name="step">正且能由 <paramref name="places"/> 表示的刻度步长。</param>
        /// <returns>位于范围内并对齐指定刻度的十进制值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">端点倒置或步长不为正时抛出。</exception>
        /// <exception cref="ArgumentException">步长精度无效或区间内不存在刻度时抛出。</exception>
        internal static decimal Next(Random random, decimal min, decimal max, int places, decimal step)
        {
            if (min > max) throw new ArgumentOutOfRangeException(nameof(max));
            if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
            var unit = Scaled(step, 28);
            var precision = BigInteger.Pow(10, 28 - places);
            if (unit % precision != 0) throw new ArgumentException("步长超出指定的小数位数。", nameof(step));
            var lower = BigInteger.DivRem(Scaled(min, 28), unit, out var loRemainder);
            if (loRemainder > 0) lower++;
            var upper = BigInteger.DivRem(Scaled(max, 28), unit, out var hiRemainder);
            if (hiRemainder < 0) upper--;
            if (lower > upper) throw new ArgumentException("区间内没有有效金额刻度。");
            var maximumCoefficient = (BigInteger.One << 96) - 1;
            BigInteger value;
            if (BigInteger.Max(BigInteger.Abs(lower * unit), BigInteger.Abs(upper * unit)) <= maximumCoefficient * precision)
                value = (lower + NextBigInteger(random, upper - lower)) * unit;
            else
                value = Representable(random, lower, upper, unit, places, maximumCoefficient);
            int scale = 28; var coefficient = BigInteger.Abs(value);
            while (scale > 0 && coefficient % 10 == 0) { coefficient /= 10; scale--; }
            // 直接构造 96 位系数，禁止字符串解析在极值附近静默舍入。
            return new decimal(unchecked((int)(uint)(coefficient & uint.MaxValue)),
                unchecked((int)(uint)((coefficient >> 32) & uint.MaxValue)),
                unchecked((int)(uint)(coefficient >> 64)), value.Sign < 0, (byte)scale);
        }
        /// <summary>
        /// 在极大区间内按规范小数位分组，均匀选择可被 decimal 精确表示的刻度。
        /// </summary>
        /// <remarks>每组剔除系数末尾为零的重复表示，组数最多为 29，不枚举金额候选。</remarks>
        private static BigInteger Representable(Random random, BigInteger lower, BigInteger upper, BigInteger unit, int places, BigInteger maximumCoefficient)
        {
            var bands = new System.Collections.Generic.List<DecimalBand>(); BigInteger total = 0;
            for (int scale = 0; scale <= places; scale++)
            {
                var factor = BigInteger.Pow(10, 28 - scale);
                var period = factor / BigInteger.GreatestCommonDivisor(unit, factor);
                var start = Ceiling(BigInteger.Max(lower, Ceiling(-maximumCoefficient * factor, unit)), period);
                var end = Floor(BigInteger.Min(upper, Floor(maximumCoefficient * factor, unit)), period);
                var previousFactor = factor * 10;
                var excludedPeriod = scale == 0 ? BigInteger.Zero : (previousFactor / BigInteger.GreatestCommonDivisor(unit, previousFactor)) / period;
                var count = Count(start, end, excludedPeriod);
                if (count > 0) { bands.Add(new DecimalBand(start, end, period, excludedPeriod, count)); total += count; }
            }
            if (total == 0) throw new ArgumentException("区间内没有可被 decimal 精确表示的金额刻度。");
            var rank = NextBigInteger(random, total - 1);
            foreach (var band in bands)
            {
                if (rank >= band.Count) { rank -= band.Count; continue; }
                var left = band.Start; var right = band.End;
                while (left < right)
                {
                    var middle = left + (right - left) / 2;
                    if (Count(band.Start, middle, band.ExcludedPeriod) > rank) right = middle; else left = middle + 1;
                }
                return left * band.Period * unit;
            }
            throw new InvalidOperationException("金额刻度分组为空。");
        }
        /// <summary>
        /// 对正除数执行向下取整的整数除法。
        /// </summary>
        private static BigInteger Floor(BigInteger value, BigInteger divisor)
        { var result = BigInteger.DivRem(value, divisor, out var remainder); return remainder < 0 ? result - 1 : result; }
        /// <summary>
        /// 对正除数执行向上取整的整数除法。
        /// </summary>
        private static BigInteger Ceiling(BigInteger value, BigInteger divisor) => -Floor(-value, divisor);
        /// <summary>
        /// 计算闭区间内排除指定倍数后的整数数量。
        /// </summary>
        private static BigInteger Count(BigInteger start, BigInteger end, BigInteger excludedPeriod)
        { return end < start ? BigInteger.Zero : end - start + 1 - (excludedPeriod.IsZero ? BigInteger.Zero : Floor(end, excludedPeriod) - Floor(start - 1, excludedPeriod)); }
        /// <summary>
        /// 保存一个小数精度分组的有效整数区间和排除周期。
        /// </summary>
        private readonly struct DecimalBand
        {
            internal readonly BigInteger Start, End, Period, ExcludedPeriod, Count;
            internal DecimalBand(BigInteger start, BigInteger end, BigInteger period, BigInteger excludedPeriod, BigInteger count)
            { Start = start; End = end; Period = period; ExcludedPeriod = excludedPeriod; Count = count; }
        }
        /// <summary>
        /// 在包含零和 <paramref name="inclusiveMax"/> 的范围内均匀抽取非负整数。
        /// </summary>
        /// <param name="random">提供随机字节的实例随机源。</param>
        /// <param name="inclusiveMax">允许的最大整数，必须非负。</param>
        /// <returns>闭区间 [0, <paramref name="inclusiveMax"/>] 内的整数。</returns>
        internal static BigInteger NextBigInteger(Random random, BigInteger inclusiveMax)
        {
            if (inclusiveMax.IsZero) return BigInteger.Zero;
            var bytes = inclusiveMax.ToByteArray();
            byte mask = 255; byte highest = bytes[bytes.Length - 1];
            if (highest == 0) mask = 0;
            else { while ((highest & 128) == 0) { mask >>= 1; highest <<= 1; } }
            BigInteger result;
            do { random.NextBytes(bytes); bytes[bytes.Length - 1] &= mask; result = new BigInteger(bytes); } while (result > inclusiveMax || result.Sign < 0);
            return result;
        }
    }
}

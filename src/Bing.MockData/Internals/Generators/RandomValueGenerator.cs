using System;
using System.Collections.Generic;
namespace Bing.MockData.Internals.Generators
{
    /// <summary>使用调用方随机源的闭区间抽样，不保留全局状态。</summary>
    internal static class RandomValueGenerator
    {
        public static readonly Type[] SupportedTypes = {
            typeof(bool), typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(decimal),
            typeof(float), typeof(double), typeof(DateTime), typeof(TimeSpan) };
        public static void Validate<T>(T min, T max)
        {
            if (Array.IndexOf(SupportedTypes, typeof(T)) < 0)
                throw new NotSupportedException($"类型 '{typeof(T)}' 尚未支持。");
            if (typeof(T) == typeof(float))
            {
                var a = (float)(object)min; var b = (float)(object)max;
                if (float.IsNaN(a) || float.IsInfinity(a)) throw new ArgumentOutOfRangeException(nameof(min));
                if (float.IsNaN(b) || float.IsInfinity(b)) throw new ArgumentOutOfRangeException(nameof(max));
            }
            if (typeof(T) == typeof(double))
            {
                var a = (double)(object)min; var b = (double)(object)max;
                if (double.IsNaN(a) || double.IsInfinity(a)) throw new ArgumentOutOfRangeException(nameof(min));
                if (double.IsNaN(b) || double.IsInfinity(b)) throw new ArgumentOutOfRangeException(nameof(max));
            }
            if (typeof(T) == typeof(DateTime) && ((DateTime)(object)min).Kind != ((DateTime)(object)max).Kind)
                throw new ArgumentException("日期上下界的 Kind 必须一致。", nameof(max));
            if (Comparer<T>.Default.Compare(min, max) > 0)
                throw new ArgumentOutOfRangeException(nameof(max), "最大值不得小于最小值。");
        }
        public static T Next<T>(Random random, T min, T max)
        {
            if (typeof(T) == typeof(bool))
                return (T)(object)((bool)(object)min == (bool)(object)max ? (bool)(object)min : random.Next(2) == 1);
            if (typeof(T) == typeof(sbyte)) return (T)(object)(sbyte)NextInt(random, (sbyte)(object)min, (sbyte)(object)max);
            if (typeof(T) == typeof(ushort)) return (T)(object)(ushort)NextInt(random, (ushort)(object)min, (ushort)(object)max);
            if (typeof(T) == typeof(uint)) return (T)(object)(uint)NextLong(random, (uint)(object)min, (uint)(object)max);
            if (typeof(T) == typeof(ulong)) return (T)(object)((ulong)(object)min + (ulong)DecimalMath.NextBigInteger(random, (System.Numerics.BigInteger)(ulong)(object)max - (ulong)(object)min));
            if (typeof(T) == typeof(decimal)) return (T)(object)DecimalMath.Next(random, (decimal)(object)min, (decimal)(object)max, 28, 0.0000000000000000000000000001m);
            if (typeof(T) == typeof(byte)) return (T)(object)(byte)NextInt(random, (byte)(object)min, (byte)(object)max);
            if (typeof(T) == typeof(short)) return (T)(object)(short)NextInt(random, (short)(object)min, (short)(object)max);
            if (typeof(T) == typeof(int)) return (T)(object)NextInt(random, (int)(object)min, (int)(object)max);
            if (typeof(T) == typeof(long)) return (T)(object)NextLong(random, (long)(object)min, (long)(object)max);
            if (typeof(T) == typeof(float)) return (T)(object)(float)NextDouble(random, (float)(object)min, (float)(object)max);
            if (typeof(T) == typeof(double)) return (T)(object)NextDouble(random, (double)(object)min, (double)(object)max);
            if (typeof(T) == typeof(DateTime))
            {
                var first = (DateTime)(object)min;
                return (T)(object)new DateTime(NextLong(random, first.Ticks, ((DateTime)(object)max).Ticks), first.Kind);
            }
            if (typeof(T) == typeof(TimeSpan))
                return (T)(object)new TimeSpan(NextLong(random, ((TimeSpan)(object)min).Ticks, ((TimeSpan)(object)max).Ticks));
            throw new NotSupportedException($"类型 '{typeof(T)}' 尚未支持。");
        }
        public static int NextInt(Random random, int min, int max)
        {
            if (min == max) return min;
            return max < int.MaxValue ? random.Next(min, max + 1) : (int)NextLong(random, min, max);
        }
        public static long NextLong(Random random, long min, long max)
        {
            if (min == max) return min;
            const ulong sign = 1UL << 63;
            var lower = unchecked((ulong)min) ^ sign;
            var upper = unchecked((ulong)max) ^ sign;
            var count = unchecked(upper - lower + 1);
            // 完整 64 位区间时 count 为 0，任意位模式均有效。
            if (count == 0) return unchecked((long)NextUInt64(random));
            var threshold = unchecked(0UL - count) % count;
            ulong sample;
            do { sample = NextUInt64(random); } while (sample < threshold);
            return unchecked((long)((lower + sample % count) ^ sign));
        }
        private static ulong NextUInt64(Random random)
        {
            var bytes = new byte[8];
            random.NextBytes(bytes);
            ulong value = 0;
            for (var i = 0; i < bytes.Length; i++) value |= (ulong)bytes[i] << (8 * i);
            return value;
        }
        private static double NextDouble(Random random, double min, double max)
        {
            if (min == max) return min;
            var unit = (NextUInt64(random) >> 11) / 9007199254740991d;
            if (unit == 0) return min;
            if (unit == 1) return max;
            var value = min < 0 && max > 0 ? min * (1 - unit) + max * unit : min + (max - min) * unit;
            return Math.Max(min, Math.Min(max, value));
        }
    }
}

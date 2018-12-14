using System;

namespace Bing.MockData.Internals.Generators
{
    /// <summary>
    /// 随机值生成器
    /// </summary>
    internal static class RandomValueGenerator
    {
        /// <summary>
        /// 容错值
        /// </summary>
        private const double Tolerance = double.Epsilon;        

        /// <summary>
        /// 随机数
        /// </summary>
        private static Random _random;

        /// <summary>
        /// 存储统一偏差
        /// </summary>
        private static double _storedUniformDeviate;

        /// <summary>
        /// 是否存储统一偏差
        /// </summary>
        private static bool _storedUniformDeviateIsGood;

        /// <summary>
        /// 已支持类型
        /// </summary>
        public static Type[] SupportedTypes { get; } =
        {
            typeof(bool), typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double),
            typeof(DateTime), typeof(TimeSpan)
        };

        /// <summary>
        /// 静态构造函数
        /// </summary>
        static RandomValueGenerator()
        {
            Reset();
        }

        /// <summary>
        /// 重置
        /// </summary>
        public static void Reset()
        {
            _random = new Random(Environment.TickCount);
        }

        /// <summary>
        /// 获取一个介于0.0和1.0之间的随机数
        /// </summary>
        /// <returns></returns>
        public static double NextDouble()
        {
            return _random.NextDouble();
        }

        /// <summary>
        /// 获取一个介于true和false的随机布尔值
        /// </summary>
        /// <returns></returns>
        public static bool NextBoolean()
        {
            return _random.Next(0, 2) != 0;
        }

        /// <summary>
        /// 获取指定范围的随机整数，该范围包括最小值，但不包括最大值
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        public static T Next<T>(T min, T max)
        {
            if (typeof(T) == typeof(bool))
            {
                return (T) (object) NextBoolean();
            }

            if (typeof(T) == typeof(byte))
            {
                return (T) (object) Next((byte) (object) min, (byte) (object) max);
            }

            if (typeof(T) == typeof(short))
            {
                return (T)(object)Next((short)(object)min, (short)(object)max);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)Next((int)(object)min, (int)(object)max);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)Next((long)(object)min, (long)(object)max);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)Next((float)(object)min, (float)(object)max);
            }

            if (typeof(T) == typeof(double))
            {
                return (T)(object)Next((double)(object)min, (double)(object)max);
            }

            if (typeof(T) == typeof(DateTime))
            {
                return (T)(object)Next((DateTime)(object)min, (DateTime)(object)max);
            }

            if (typeof(T) == typeof(TimeSpan))
            {
                return (T)(object)Next((TimeSpan)(object)min, (TimeSpan)(object)max);
            }

            throw new NotSupportedException($"类型 '{typeof(T)}' 尚未支持.");
        }

        /// <summary>
        /// 获取指定范围的随机数，该范围包括最小值，但不包括最大值
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        public static byte Next(byte min, byte max)
        {
            if (max <= min)
            {
                throw new ArgumentException("最大值必须大于等于最小值.");
            }

            double rn = (max * 1.0 - min * 1.0) * _random.NextDouble() + min * 1.0;
            return Convert.ToByte(rn);
        }

        /// <summary>
        /// 获取指定范围的随机数，该范围包括最小值，但不包括最大值
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        public static short Next(short min, short max)
        {
            if (max <= min)
            {
                throw new ArgumentException("最大值必须大于等于最小值.");
            }

            double rn = (max * 1.0 - min * 1.0) * _random.NextDouble() + min * 1.0;
            return Convert.ToInt16(rn);
        }

        /// <summary>
        /// 获取指定范围的随机数，该范围包括最小值，但不包括最大值
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        public static int Next(int min, int max)
        {
            return _random.Next(min, max);
        }

        /// <summary>
        /// 获取指定范围的随机数，该范围包括最小值，但不包括最大值
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        public static long Next(long min, long max)
        {
            if (max <= min)
            {
                throw new ArgumentException("最大值必须大于等于最小值.");
            }

            double rn = (max * 1.0 - min * 1.0) * _random.NextDouble() + min * 1.0;
            return Convert.ToInt64(rn);
        }

        /// <summary>
        /// 获取指定范围的随机数，该范围包括最小值，但不包括最大值
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        public static float Next(float min, float max)
        {
            if (max <= min)
            {
                throw new ArgumentException("最大值必须大于等于最小值.");
            }

            double rn = (max * 1.0 - min * 1.0) * _random.NextDouble() + min * 1.0;
            return Convert.ToSingle(rn);
        }

        /// <summary>
        /// 获取指定范围的随机数，该范围包括最小值，但不包括最大值
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        public static double Next(double min, double max)
        {
            if (max <= min)
            {
                throw new ArgumentException("最大值必须大于等于最小值.");
            }

            double rn = (max - min) * _random.NextDouble() + min;
            return rn;
        }

        /// <summary>
        /// 获取指定范围的随机时间，该范围包括最小值，但不包括最大值
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        public static DateTime Next(DateTime min, DateTime max)
        {
            if (max <= min)
            {
                throw new ArgumentException("最大值必须大于等于最小值.");
            }

            long minTicks = min.Ticks;
            long maxTicks = max.Ticks;
            double rn = (Convert.ToDouble(maxTicks) - Convert.ToDouble(minTicks)) * _random.NextDouble() +
                        Convert.ToDouble(minTicks);
            return new DateTime(Convert.ToInt64(rn));
        }

        /// <summary>
        /// 获取指定范围的随机时间跨度，该范围包括最小值，但不包括最大值
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        public static TimeSpan Next(TimeSpan min, TimeSpan max)
        {
            if (max <= min)
            {
                throw new ArgumentException("最大值必须大于等于最小值.");
            }

            long minTicks = min.Ticks;
            long maxTicks = max.Ticks;
            double rn = (Convert.ToDouble(maxTicks) - Convert.ToDouble(minTicks)) * _random.NextDouble() +
                        Convert.ToDouble(minTicks);
            return new TimeSpan(Convert.ToInt64(rn));
        }

        /// <summary>
        /// 获取一个介于0.0和1.0之间的随机数
        /// </summary>
        /// <returns></returns>
        public static double NextUniform()
        {
            return NextDouble();
        }

        /// <summary>
        /// 获取指定枚举类型的随机枚举项
        /// </summary>
        /// <param name="enumType">枚举类型</param>
        /// <returns></returns>
        public static int NextEnum(Type enumType)
        {
            var values = (int[]) Enum.GetValues(enumType);
            int randomIndex = Next(0, values.Length);
            return values[randomIndex];
        }

        /// <summary>
        /// 获取随机指数方差
        /// </summary>
        /// <returns></returns>
        public static double NextExponential()
        {
            double dum = 0.0;

            while (Math.Abs(dum)<Tolerance)
            {
                dum = NextUniform();
            }

            return -1.0 * Math.Log(dum, Math.E);
        }

        /// <summary>
        /// 获取随机正态分布的偏差
        /// </summary>
        /// <returns></returns>
        public static double NextNormal()
        {
            if (_storedUniformDeviateIsGood)
            {
                _storedUniformDeviateIsGood = false;
                return _storedUniformDeviate;
            }

            double rsq = 0.0;
            double v1 = 0.0, v2 = 0.0;
            while (rsq>=1.0||Math.Abs(rsq)<Tolerance)
            {
                v1 = 2.0 * NextDouble() - 1.0;
                v2 = 2.0 * NextDouble() - 1.0;
                rsq = v1 * v1 + v2 * v2;
            }

            double fac = Math.Sqrt(-2.0 * Math.Log(rsq, Math.E) / rsq);
            _storedUniformDeviate = v1 * fac;
            _storedUniformDeviateIsGood = true;
            return v2 * fac;
        }
    }
}

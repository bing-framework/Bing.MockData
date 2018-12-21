using System;
using System.Text;

namespace Bing.MockData.Internals.Generators
{
    /// <summary>
    /// 随机生成器
    /// </summary>
    internal class RandomGenerator
    {
        /// <summary>
        /// 随机数
        /// </summary>
        private readonly Random _random;

        /// <summary>
        /// 重复数
        /// </summary>
        private int _repeat = 0;

        /// <summary>
        /// 初始化一个<see cref="RandomGenerator"/>类型的实例
        /// </summary>
        public RandomGenerator()
        {
            _random = new Random();
        }

        /// <summary>
        /// 初始化一个<see cref="RandomGenerator"/>类型的实例
        /// </summary>
        /// <param name="seed">种子数</param>
        public RandomGenerator(int seed)
        {
            _random = new Random(seed);
        }

        /// <summary>
        /// 生成随机数字
        /// </summary>
        /// <param name="length">数字长度</param>
        /// <returns></returns>
        public string GenerateNumber(int length)
        {
            StringBuilder sb = new StringBuilder();
            long ticks = DateTime.Now.Ticks + this._repeat;
            this._repeat++;
            Random random = new Random((int)((ulong)ticks & 0xffffffffL) | (int)(ticks >> this._repeat));
            for (int i = 0; i < length; i++)
            {
                int num = random.Next();
                string temp = ((char)(0x30 + (ushort)(num % 10))).ToString();
                sb.Append(temp);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 生成随机整数
        /// </summary>
        /// <param name="maxValue">最大值，包含边界</param>
        /// <returns></returns>
        public int GenerateInt(int maxValue)
        {
            return GenerateInt(0, maxValue + 1);
        }

        /// <summary>
        /// 生成随机整数
        /// </summary>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值，不包含边界</param>
        /// <returns></returns>
        public int GenerateInt(int minValue, int maxValue)
        {
            return _random.Next(minValue, maxValue);
        }

        /// <summary>
        /// 生成随机日期
        /// </summary>
        /// <param name="beginYear">起始年份</param>
        /// <param name="endYear">结束年份</param>
        /// <returns></returns>
        public DateTime GenerateDate(int beginYear = 1970, int endYear = 2080)
        {
            var year = GenerateInt(beginYear, endYear);
            var month = GenerateInt(1, 13);
            var day = GenerateInt(1, (month == 2 && IsLeapYear(year) ? 29 : 28) + 1);
            var hour = GenerateInt(0, 24);
            var minute = GenerateInt(0, 60);
            var second = GenerateInt(0, 60);
            return new DateTime(year, month, day, hour, minute, second);
        }

        /// <summary>
        /// 是否闰年
        /// </summary>
        /// <param name="year">年份</param>
        /// <returns></returns>
        private bool IsLeapYear(int year)
        {
            if (year % 4 == 0 && year % 100 != 0 || year % 400 == 0)
            {
                return true;
            }

            return false;
        }
    }
}

namespace Bing.MockData.Internals.Generators
{
    /// <summary>
    /// 随机数据生成器
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    internal class RandomThingsGenerator<T>
    {
        /// <summary>
        /// 最小值
        /// </summary>
        private readonly T _min;

        /// <summary>
        /// 最大值
        /// </summary>
        private readonly T _max;

        /// <summary>
        /// 初始化一个<see cref="RandomThingsGenerator{T}"/>类型的实例
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public RandomThingsGenerator(T min, T max)
        {
            _min = min;
            _max = max;
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public T Generate()
        {
            return RandomValueGenerator.Next(_min, _max);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.MockData.Internals.Generators
{
    /// <summary>
    /// 随机列表项生成器
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    internal class RandomItemFromListGenerator<T>
    {
        /// <summary>
        /// 列表
        /// </summary>
        private readonly T[] _list;

        /// <summary>
        /// 初始化一个<see cref="RandomItemFromListGenerator{T}"/>类型的实例
        /// </summary>
        /// <param name="list">数据源</param>
        /// <param name="predicate">条件</param>
        public RandomItemFromListGenerator(IEnumerable<T> list, Func<T, bool> predicate = null)
        {
            _list = predicate == null ? list.ToArray() : list.Where(predicate).ToArray();
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public T Generate()
        {
            return _list.Length > 0 ? _list[RandomValueGenerator.Next(0, _list.Length)] : default(T);
        }
    }
}

using System;
using System.Collections.Generic;

namespace Bing.MockData.Internals.Generators
{
    /// <summary>
    /// 随机字符串项生成器
    /// </summary>
    internal class RandomStringFromListGenerator:RandomItemFromListGenerator<string>
    {
        /// <summary>
        /// 初始化一个<see cref="RandomStringFromListGenerator"/>类型的实例
        /// </summary>
        /// <param name="list">数据源</param>
        public RandomStringFromListGenerator(IEnumerable<string> list) : base(list)
        {
        }
    }
}

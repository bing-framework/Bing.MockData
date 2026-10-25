using System;
using System.Collections.Generic;
using System.Linq;
namespace Bing.MockData.Internals.Generators
{
    internal class RandomItemFromListGenerator<T>
    {
        private readonly IReadOnlyList<T> _list;
        private readonly Random _random;
        public RandomItemFromListGenerator(Random random, IEnumerable<T> list, Func<T, bool> predicate = null)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            _list = predicate == null ? (list as IReadOnlyList<T> ?? list.ToArray()) : list.Where(predicate).ToArray();
            if (_list.Count == 0) throw new ArgumentException("候选列表不能为空。", nameof(list));
            if (_list.Any(item => ReferenceEquals(item, null)))
                throw new ArgumentException("候选列表不能包含 null。", nameof(list));
            _random = random;
        }
        public T Generate() => _list[_random.Next(_list.Count)];
    }
}

using System;
using System.Collections.Generic;
using Bing.MockData.Datas;
namespace Bing.MockData.Internals.Generators
{
    internal sealed class NameParts
    {
        private readonly Random _random;
        private readonly List<RandomStringFromListGenerator> _groups = new List<RandomStringFromListGenerator>();
        private readonly RandomStringFromListGenerator _lastNames;
        public NameParts(Random random, bool male, bool female)
        {
            if (!male && !female) throw new ArgumentException("至少选择一种性别。", "Male");
            _random = random;
            if (male) _groups.Add(new RandomStringFromListGenerator(random, CommonData.Instance.MaleNames));
            if (female) _groups.Add(new RandomStringFromListGenerator(random, CommonData.Instance.FemaleNames));
            _lastNames = new RandomStringFromListGenerator(random, CommonData.Instance.LastNames);
        }
        public string First() => _groups[_random.Next(_groups.Count)].Generate();
        public string Last() => _lastNames.Generate();
    }
}

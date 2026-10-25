using System;
using System.Collections.Generic;
namespace Bing.MockData.Internals.Generators
{
    internal sealed class RandomStringFromListGenerator : RandomItemFromListGenerator<string>
    {
        public RandomStringFromListGenerator(Random random, IEnumerable<string> list) : base(random, list) { }
    }
}

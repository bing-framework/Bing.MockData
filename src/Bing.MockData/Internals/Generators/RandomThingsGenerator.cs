using System;
namespace Bing.MockData.Internals.Generators
{
    internal sealed class RandomThingsGenerator<T>
    {
        private readonly Random _random;
        private readonly T _min;
        private readonly T _max;
        public RandomThingsGenerator(Random random, T min, T max)
        {
            RandomValueGenerator.Validate(min, max);
            _random = random; _min = min; _max = max;
        }
        public T Generate() => RandomValueGenerator.Next(_random, _min, _max);
    }
}

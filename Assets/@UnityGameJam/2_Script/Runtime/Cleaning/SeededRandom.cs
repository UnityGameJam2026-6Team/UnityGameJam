using System;

namespace UnityGameJam.Cleaning
{
    internal sealed class SeededRandom : IRandom
    {
        private readonly Random _random;

        public SeededRandom(int seed)
        {
            _random = new Random(seed);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }
    }
}

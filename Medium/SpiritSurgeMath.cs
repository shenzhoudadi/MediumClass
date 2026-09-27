using System;

namespace MediumClass.Medium
{
    internal static class SpiritSurgeMath
    {
        internal static int Roll(int level, bool prowler, bool hardenedSoul, bool legendaryMarshal, Func<int, int> die)
        {
            // Legendary Marshal has its own fixed die. Hardened Soul replaces
            // the ordinary surge with two independent eight-sided dice.
            if (legendaryMarshal) return die(6);
            if (hardenedSoul) return die(8) + die(8);
            if (prowler) return die(6);
            return die(level >= 20 ? 10 : level >= 10 ? 8 : 6);
        }
    }
}

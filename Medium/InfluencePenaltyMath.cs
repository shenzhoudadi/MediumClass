using System;

namespace MediumClass.Medium
{
    internal static class InfluencePenaltyMath
    {
        // Divide the positive magnitude, then negate at the point of application.
        // C# integer division therefore rounds the penalty magnitude down.
        internal static int Magnitude(int originalMagnitude, int formalSpiritCount, bool multipleSpirits)
        {
            int divisor = multipleSpirits ? Math.Max(1, formalSpiritCount) : 1;
            return Math.Max(0, originalMagnitude) / divisor;
        }
    }
}

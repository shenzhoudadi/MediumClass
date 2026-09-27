using System;

namespace MediumClass.Medium
{
    internal static class InfluenceMath
    {
        internal static int DailyFreeSurges(int forgonePowers, bool mastery) =>
            Math.Max(0, forgonePowers) * (mastery ? 4 : 2) + (mastery ? 2 : 0);
        internal const int BaseCap = 5;
        internal const int PenaltyThreshold = 3;
        internal static int MythicBonus(int rank) => 1 + Math.Max(0, rank) / 3;
        internal static bool CanAccept(int current, int cost, int cap) =>
            cost >= 0 && (cost == 0 || (current <= cap && cost <= cap - current));
        internal static int Migrate(int oldRemaining, bool channelled, bool hadPropitiation) =>
            channelled ? Math.Max(1, BaseCap + (hadPropitiation ? 1 : 0) - Math.Max(0, oldRemaining)) : 0;
        internal static int Soothe(int current, bool channelled) => Math.Max(channelled ? 1 : 0, current - 1);
    }
}

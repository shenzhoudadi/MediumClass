using System;
using System.Collections.Generic;

namespace MediumClass.Prowler
{
    /// <summary>
    /// 绝境巡行者等级进阶与血脉延迟规则策略（保持纯逻辑，无游戏引擎直接依赖，便于测试与复用）。
    /// </summary>
    public static class ProwlerProgressionPolicy
    {
        public static int Delay(int original)
        {
            switch (original)
            {
                case 1: return 4;
                case 4: return 9;
                case 8: return 12;
                case 12: return 15;
                case 16: return 20;
                default: return -1;
            }
        }

        public static int SpiritBonus(int level) => Math.Min(6, 1 + Math.Max(0, level) / 4);

        public static bool KeepBonusFeat(int level) => level == 6 || level == 18;

        public static bool CoreTier(int level) =>
            level == 1 || level == 4 || level == 8 || level == 12 || level == 16 || level == 20;

        public static int FeatureLevel(int original, bool identity, bool spell, bool bonusFeat, bool auxiliary, bool repeatedRank)
        {
            if (bonusFeat) return KeepBonusFeat(original) ? original : -1;
            if (identity || spell || auxiliary || repeatedRank || !CoreTier(original)) return original;
            return Delay(original);
        }

        public static IEnumerable<int> NewlyReached(int previous, int current)
        {
            for (int level = Math.Max(0, previous) + 1; level <= current; level++)
            {
                yield return level;
            }
        }
    }
}

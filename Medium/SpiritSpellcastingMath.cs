using System;

namespace MediumClass.Medium
{
    // Matches Spellbook.GetSpellsPerDay in the installed game. Kept separate from Unity
    // so low-level progression and attribute/extra-slot boundaries can be tested offline.
    internal static class SpiritSpellcastingMath
    {
        internal static int DailySlots(int tableCount, int spellLevel, int modifiedAttribute,
            int permanentAttribute, int baseAttribute, bool isPlayerFaction, int extraSlots)
        {
            if (spellLevel < 0 || spellLevel > 6 || tableCount < 0
                || Math.Min(modifiedAttribute, permanentAttribute) < 10 + spellLevel)
                return 0;

            int modifier = ((isPlayerFaction ? permanentAttribute : baseAttribute) - 10) / 2;
            int difference = modifier - spellLevel;
            int attributeSlots = spellLevel > 0 && difference >= 0 ? 1 + difference / 4 : 0;
            return tableCount + extraSlots + attributeSlots;
        }

        internal static int MaxSpellLevel(int[] row)
        {
            if (row == null) return 0;
            for (int level = Math.Min(6, row.Length - 1); level >= 0; level--)
                if (row[level] >= 0) return level;
            return 0;
        }
    }
}

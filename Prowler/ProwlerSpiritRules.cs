using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using MediumClass.Utilities;
using System;

namespace MediumClass.Prowler
{
    /// <summary>
    /// 英灵系统的“职业等级口径”。
    ///
    /// 取通灵者职业等级，并在角色确实选择了绝境巡行者变体时，额外取血怒者职业等级（二者取大）。
    /// 未选择该变体的血怒者（包括原怒者等其他变体）绝不参与计算，因此“通灵者兼职普通血怒者”
    /// 不会把血怒者等级当成通灵者等级。
    ///
    /// 蓝图侧的职业等级档位由 <see cref="ProwlerSpiritScaling"/> 用引擎原生的 archetype 字段实现，
    /// 与本类保持同一套语义（取大，且仅在选了变体时计入血怒者）。
    /// </summary>
    internal static class ProwlerSpiritRules
    {
        private static BlueprintCharacterClass _bloodrager;
        private static BlueprintCharacterClass _medium;
        private static BlueprintArchetype _archetype;

        internal static BlueprintCharacterClass Bloodrager
            => _bloodrager ??= ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>(BlueprintGuid.Parse(ProwlerContent.Bloodrager));

        internal static BlueprintArchetype Archetype
            => _archetype ??= ResourcesLibrary.TryGetBlueprint<BlueprintArchetype>(BlueprintGuid.Parse(Guids.ProwlerArchetype));

        private static BlueprintCharacterClass MediumClass
            => _medium ??= ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>(BlueprintGuid.Parse(Guids.Medium));

        internal static bool IsProwler(UnitDescriptor unit)
        {
            var archetype = Archetype;
            var bloodrager = Bloodrager;
            if (archetype == null || bloodrager == null || unit?.Progression == null) { return false; }
            var classData = unit.Progression.GetClassData(bloodrager);
            return classData?.Archetypes != null && classData.Archetypes.Contains(archetype);
        }

        internal static int SpiritClassLevel(UnitEntityData unit) => SpiritClassLevel(unit?.Descriptor);

        internal static int SpiritClassLevel(UnitDescriptor unit)
        {
            if (unit?.Progression == null) { return 0; }
            int level = unit.Progression.GetClassLevel(MediumClass);
            if (!IsProwler(unit)) { return level; }
            var classData = unit.Progression.GetClassData(Bloodrager);
            return classData == null ? level : Math.Max(level, classData.Level);
        }
    }
}

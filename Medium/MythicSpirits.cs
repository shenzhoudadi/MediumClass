using System.Linq;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Abilities;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;

namespace MediumClass.Medium
{
    internal static class MythicSpirits
    {
        internal const string MultipleSpiritsGuid = "e7cf953e-cc45-42dd-806e-faf29dcf49da";

        internal static void Configure()
        {
            // New() automatically populates every matching MythicAbility
            // selection, checking the GUID first. A manual append adds a duplicate.
            FeatureConfigurator.New("MythicMultipleSpirits", MultipleSpiritsGuid)
                .SetDisplayName("MythicMultipleSpirits.Name")
                .SetDescription("MythicMultipleSpirits.Description")
                .SetIcon("assets/icons/tranceofthree.png")
                .AddToGroups(FeatureGroup.MythicAbility)
                .AddPrerequisiteFeature(Guids.MediumChannelSpirit, group: Prerequisite.GroupType.Any)
                .AddPrerequisiteFeature(Guids.ProwlerSpirit, group: Prerequisite.GroupType.Any)
                .Configure();
        }
    }

    internal static class MultiSpiritRules
    {
        private static readonly (string Spirit, string Ability, string Buff)[] Trances = {
            (Guids.Archmage, Guids.MediumTranceOfThreeArchmageAbility, Guids.MediumTranceOfThreeArchmageBuff),
            (Guids.Champion, Guids.MediumTranceOfThreeChampionAbility, Guids.MediumTranceOfThreeChampionBuff),
            (Guids.Guardian, Guids.MediumTranceOfThreeGuardianAbility, Guids.MediumTranceOfThreeGuardianBuff),
            (Guids.Hierophant, Guids.MediumTranceOfThreeHierophantAbility, Guids.MediumTranceOfThreeHierophantBuff),
            (Guids.Marshal, Guids.MediumTranceOfThreeMarshalAbility, Guids.MediumTranceOfThreeMarshalBuff),
            (Guids.Trickster, Guids.MediumTranceOfThreeTricksterAbility, Guids.MediumTranceOfThreeTricksterBuff)
        };

        internal static bool IsTranceOfActiveSpirit(AbilityData ability)
        {
            var trance = Trances.FirstOrDefault(t => ability.Blueprint.AssetGuid == BlueprintGuid.Parse(t.Ability));
            return trance.Spirit != null && ability.Caster.Unit.Get<UnitPartMedium>()?
                .IsActiveSpirit(BlueprintTool.GetRef<BlueprintCharacterClassReference>(trance.Spirit)) == true;
        }

        internal static void RemoveTrance(UnitEntityData owner, BlueprintCharacterClassReference spirit)
        {
            var trance = Trances.FirstOrDefault(t => spirit.Get().AssetGuid == BlueprintGuid.Parse(t.Spirit));
            if (trance.Buff != null) owner.Buffs.RemoveFact(BlueprintTool.Get<BlueprintBuff>(trance.Buff));
        }

        internal static bool CanChannel(UnitEntityData owner, BlueprintCharacterClassReference spirit)
        {
            var state = owner?.Get<UnitPartMedium>();
            if (spirit?.Get() == null || state == null || !state.Spirits.ContainsKey(spirit)
                || state.IsActiveSpirit(spirit)) return false;
            if (!state.ActiveSpiritClasses.Any())
                // A saved channel with unrecoverable choice is not a new session.
                // Replacing its buff would reset influence inside OnDeactivate.
                return !owner.HasFact(BlueprintTool.Get<BlueprintBuff>(Guids.MediumChannelSpiritPrimarySpiritBuff));
            return owner.HasFact(BlueprintTool.Get<BlueprintFeature>(MythicSpirits.MultipleSpiritsGuid));
        }

        internal static int NextChannelCost(UnitEntityData owner) =>
            System.Math.Max(1, owner?.Get<UnitPartMedium>()?.ActiveSpiritClasses.Count() ?? 0);
    }
}

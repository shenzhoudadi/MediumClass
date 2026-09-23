using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using Kingmaker.Utility;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;
using MediumClass.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Medium.NewComponents.AbilitySpecific
{
    [TypeId("4c5cd1ca-7eed-4a21-aa7d-2e79d5953009")]
    class MediumInfluencePenaltyComponent : UnitFactComponentDelegate
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(MediumInfluencePenaltyComponent));
        public override void OnTurnOn()
        {
            RemoveOwnModifiers();
            UnitPartMedium unitPartMedium = base.Context?.MaybeCaster?.Get<UnitPartMedium>();
            if (unitPartMedium == null) { return; }

            if(isSecondaryCheck)
            {
                if(unitPartMedium.SecondarySpirit == null || !unitPartMedium.Spirits.ContainsKey(unitPartMedium.SecondarySpirit)) { return; }
                Penalties = unitPartMedium.Spirits[unitPartMedium.SecondarySpirit].SpiritPenalty.Stats;
                SpiritBonus = base.Context.MaybeCaster.Progression.Features.GetRank(unitPartMedium.Spirits[unitPartMedium.SecondarySpirit].SpiritBonus.SpiritBonusFeature) + unitPartMedium.Spirits[unitPartMedium.SecondarySpirit].SpiritFocus;
                if (unitPartMedium.SecondarySpirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Marshal))
                {
                    fightDefensively = true;
                    base.Owner.Buffs.AddBuff(BuffRefs.FightDefensivelyBuff.Reference.Get(), base.Context, new TimeSpan(24, 0, 0));
                }
            }
            else
            {
                if (unitPartMedium.PrimarySpirit == null || !unitPartMedium.Spirits.ContainsKey(unitPartMedium.PrimarySpirit)) { return; }
                Penalties = unitPartMedium.Spirits[unitPartMedium.PrimarySpirit].SpiritPenalty.Stats;
                SpiritBonus = base.Context.MaybeCaster.Progression.Features.GetRank(unitPartMedium.Spirits[unitPartMedium.PrimarySpirit].SpiritBonus.SpiritBonusFeature) + unitPartMedium.Spirits[unitPartMedium.PrimarySpirit].SpiritFocus;
                if (unitPartMedium.PrimarySpirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Marshal))
                {
                    fightDefensively = true;
                    base.Owner.Buffs.AddBuff(BuffRefs.FightDefensivelyBuff.Reference.Get(), base.Context, new TimeSpan(24, 0, 0));
                }
            }
            

            base.Owner.Stats.GetStat(StatType.Initiative).AddModifier((-2), base.Runtime, ModifierDescriptor.Penalty);
            base.Owner.Stats.GetStat(StatType.SaveWill).AddModifier((2), base.Runtime, ModifierDescriptor.UntypedStackable);

            foreach (StatType statType in Penalties ?? Array.Empty<StatType>())
            {
                base.Owner.Stats.GetStat(statType)?.AddModifier((SpiritBonus * -1), base.Runtime, ModifierDescriptor.Penalty);
            }    
        }

        public override void OnTurnOff()
        {
            RemoveOwnModifiers();
            if (fightDefensively)
                base.Owner.Buffs.RemoveFact(BuffRefs.FightDefensivelyBuff.Reference.Get());
        }

        private void RemoveOwnModifiers()
        {
            // Cleanup is independent of volatile Penalties/Context fields after loading.
            foreach (var component in BlueprintTool.Get<BlueprintFeature>(Guids.MediumChannelSpirit)
                .GetComponents<MediumSpiritComponent>())
                foreach (var stat in component.Penalties ?? Array.Empty<StatType>())
                    Owner.Stats.GetStat(stat)?.RemoveModifiersFrom(Runtime);
            Owner.Stats.GetStat(StatType.Initiative)?.RemoveModifiersFrom(Runtime);
            Owner.Stats.GetStat(StatType.SaveWill)?.RemoveModifiersFrom(Runtime);
        }

        private StatType[] Penalties;
        private int SpiritBonus = 0;
        private bool fightDefensively = false;
        public bool isSecondaryCheck = false;
    }
}

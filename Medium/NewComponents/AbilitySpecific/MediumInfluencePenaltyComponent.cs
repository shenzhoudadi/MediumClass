using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;


namespace MediumClass.Medium.NewComponents.AbilitySpecific
{
    [TypeId("4c5cd1ca-7eed-4a21-aa7d-2e79d5953009")]
    class MediumInfluencePenaltyComponent : UnitFactComponentDelegate
    {
        public override void OnTurnOn() => RefreshModifiers();

        internal void RefreshModifiers()
        {
            RemoveOwnModifiers();
            var medium = Owner.Get<UnitPartMedium>();
            if (medium == null) return;
            // Only formally selected spirits divide influence penalties. A
            // temporary Trance or Beacon-granted power is not another channel.
            int formalCount = medium.ActiveSpiritClasses.Count();
            bool mitigate = Owner.HasFact(BlueprintTool.Get<BlueprintFeature>(MythicSpirits.MultipleSpiritsGuid));
            var classes = isSecondaryCheck
                ? new[] { medium.SecondarySpirit }
                : medium.ActiveSpiritClasses.ToArray();
            var values = new Dictionary<StatType, int>();
            foreach (var spirit in classes.Where(s => s != null && medium.Spirits.ContainsKey(s)))
            {
                var entry = medium.Spirits[spirit];
                int bonus = Owner.Progression.Features.GetRank(entry.SpiritBonus.SpiritBonusFeature) + entry.SpiritFocus;
                foreach (var stat in entry.SpiritPenalty.Stats ?? Array.Empty<StatType>())
                    values[stat] = Math.Max(values.TryGetValue(stat, out var prior) ? prior : 0, bonus);
                if (spirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Marshal)
                    && !Owner.HasFact(BuffRefs.FightDefensivelyBuff.Reference.Get()))
                    (Fact as Buff)?.StoreFact(Owner.Buffs.AddBuff(BuffRefs.FightDefensivelyBuff.Reference.Get(), Context, new TimeSpan(24, 0, 0)));
            }
            ApplyPenalty(StatType.Initiative, 2, formalCount, mitigate);
            Owner.Stats.GetStat(StatType.SaveWill)?.AddModifier(2, Runtime, ModifierDescriptor.UntypedStackable);
            foreach (var value in values)
                ApplyPenalty(value.Key, value.Value, formalCount, mitigate);
        }

        private void ApplyPenalty(StatType stat, int magnitude, int formalCount, bool mitigate)
        {
            int reduced = InfluencePenaltyMath.Magnitude(magnitude, formalCount, mitigate);
            if (reduced > 0)
                Owner.Stats.GetStat(stat)?.AddModifier(-reduced, Runtime, ModifierDescriptor.Penalty);
        }

        public override void OnTurnOff()
        {
            RemoveOwnModifiers();


        }

        private void RemoveOwnModifiers()
        {
            // Full catalogue cleanup also covers restored buffs before their
            // source spirit has finished deserializing.
            foreach (var component in BlueprintTool.Get<BlueprintFeature>(Guids.MediumChannelSpirit).GetComponents<MediumSpiritComponent>())
                foreach (var stat in component.Penalties ?? Array.Empty<StatType>())
                    Owner.Stats.GetStat(stat)?.RemoveModifiersFrom(Runtime);
            Owner.Stats.GetStat(StatType.Initiative)?.RemoveModifiersFrom(Runtime);
            Owner.Stats.GetStat(StatType.SaveWill)?.RemoveModifiersFrom(Runtime);
        }


        public bool isSecondaryCheck;
    }
}


using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Prowler;
using MediumClass.Utilities;
using System;
using System.Linq;

namespace MediumClass.Medium.NewComponents.AbilitySpecific
{
    [TypeId("96306dd2-f947-44d4-a6d8-1eafe7c937dc")]
    class MediumSpiritSurgeComponent : UnitFactComponentDelegate, IConcentrationBonusProvider
    {
        public override void OnTurnOn()
        {
            var caster = Context?.MaybeCaster;
            var medium = caster?.Get<UnitPartMedium>();
            if (medium == null) return;
            var marshal = BlueprintTool.GetRef<BlueprintCharacterClassReference>(Guids.Marshal);
            bool legendary = Context.SourceAbility == BlueprintTool.Get<BlueprintAbility>(Guids.MarshalLegendaryMarshalAbility);
            bool alliedOrder = Owner != caster && Context.SourceAbility == BlueprintTool.Get<BlueprintAbility>(Guids.MarshalMarshalsOrdersAbility);
            var classes = legendary || alliedOrder ? new[] { marshal } : medium.ActiveSpiritClasses.ToArray();
            var entries = classes.Where(medium.Spirits.ContainsKey).Select(s => medium.Spirits[s]).ToArray();
            var Stats = entries.SelectMany(e => e.SpiritBonus.Stats ?? Array.Empty<StatType>()).Distinct().ToArray();
            
            
            int MarshalBonus = 0;
            var MarshalStats = Array.Empty<StatType>();
            // Orders and Legendary Marshal lend only the surge die to allies.
            // Additional spirits never transfer the marshal's own spirit bonus.
            if (!legendary && !alliedOrder && medium.IsActiveSpirit(marshal) && medium.Spirits.TryGetValue(marshal, out var marshalEntry))
            {
                MarshalBonus = caster.Progression.Features.GetRank(marshalEntry.SpiritBonus.SpiritBonusFeature.Get()) + marshalEntry.SpiritFocus;
                MarshalStats = marshalEntry.SpiritBonus.Stats ?? Array.Empty<StatType>();
            }
            foreach (StatType stat in Stats)
                Owner.Stats.GetStat(stat)?.AddModifier(GetBonus() + (MarshalStats.Contains(stat) ? MarshalBonus : 0), Runtime, ModifierDescriptor.UntypedStackable);
            // Free uses are consumed atomically by InfluenceAbilitySpendPatch.
        }

        public override void OnTurnOff()
        {
            // The catalogue survives save/load and no per-caster state is stored
            // on the shared blueprint component.
            foreach (var entry in BlueprintTool.Get<BlueprintFeature>(Guids.MediumChannelSpirit).GetComponents<MediumSpiritComponent>())
                foreach (var stat in entry.Stats ?? Array.Empty<StatType>())
                    Owner.Stats.GetStat(stat)?.RemoveModifiersFrom(Runtime);
        }

        public int GetStaticConcentrationBonus(EntityFactComponent runtime)
        {
            using (runtime.RequestEventContext())
            {
                var caster = Context?.MaybeCaster;
                var medium = caster?.Get<UnitPartMedium>();
                if (medium == null) return 0;
                bool legendary = Context.SourceAbility == BlueprintTool.Get<BlueprintAbility>(Guids.MarshalLegendaryMarshalAbility);
                bool alliedOrder = Owner != caster && Context.SourceAbility == BlueprintTool.Get<BlueprintAbility>(Guids.MarshalMarshalsOrdersAbility);
                var marshal = BlueprintTool.GetRef<BlueprintCharacterClassReference>(Guids.Marshal);
                var classes = legendary || alliedOrder ? new[] { marshal } : medium.ActiveSpiritClasses.ToArray();
                if (!classes.Where(medium.Spirits.ContainsKey).Any(s => medium.Spirits[s].SpiritBonus.Concentration)) return 0;
                int ownBonus = !legendary && !alliedOrder && medium.IsActiveSpirit(marshal) && medium.Spirits.TryGetValue(marshal, out var entry)
                    ? caster.Progression.Features.GetRank(entry.SpiritBonus.SpiritBonusFeature.Get()) + entry.SpiritFocus : 0;
                return GetBonus() + ownBonus;
            }
        }
        public int GetBonus()
        {
            var caster = Context.MaybeCaster;
            return SpiritSurgeMath.Roll(ProwlerSpiritRules.SpiritClassLevel(caster),
                ProwlerSpiritRules.IsProwler(caster?.Descriptor),
                HardenedSoul.HasFeature(caster?.Descriptor),
                Context.SourceAbility == BlueprintTool.Get<BlueprintAbility>(Guids.MarshalLegendaryMarshalAbility),
                sides => rnd.Next(1, sides + 1));
        }

        private static readonly Random rnd = new Random();
    }
}



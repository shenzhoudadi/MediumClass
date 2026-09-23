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
    [TypeId("96306dd2-f947-44d4-a6d8-1eafe7c937dc")]
    class MediumSpiritSurgeComponent : UnitFactComponentDelegate, IConcentrationBonusProvider
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(MediumSpiritSurgeComponent));
        public override void OnTurnOn()
        {
            Logger.Log("I am in OnTurnOn of SpiritSurgeComponent");
            var caster = base.Context?.MaybeCaster;
            UnitPartMedium unitPartMedium = caster?.Get<UnitPartMedium>();
            if (unitPartMedium == null || unitPartMedium.PrimarySpirit == null
                || !unitPartMedium.Spirits.TryGetValue(unitPartMedium.PrimarySpirit, out var spiritEntry)) { return; }

            Stats = spiritEntry.SpiritBonus.Stats ?? Array.Empty<StatType>();
            Concentration = spiritEntry.SpiritBonus.Concentration;
            CharacterLevel = base.Context.MaybeCaster.Progression.GetClassLevel(BlueprintTool.Get<BlueprintCharacterClass>(Guids.Medium));
            MarshalBonus = 0;

            Logger.Log("Right before If statement of SpiritSurgeComponent");
            if (unitPartMedium.PrimarySpirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Marshal))
            {
                if (base.Context.SourceAbility != BlueprintTool.Get<BlueprintAbility>(Guids.MarshalLegendaryMarshalAbility))
                    MarshalBonus = caster.Progression.Features.GetRank(spiritEntry.SpiritBonus.SpiritBonusFeature.Get()) + spiritEntry.SpiritFocus;
            }
            foreach (StatType statType in Stats)
                base.Owner.Stats.GetStat(statType)?.AddModifier((GetBonus() + MarshalBonus), base.Runtime, ModifierDescriptor.UntypedStackable);
            // Legendary Marshal has no influence cost to refund. Free uses belong to the caster.
            if (base.Context.SourceAbility != BlueprintTool.Get<BlueprintAbility>(Guids.MarshalLegendaryMarshalAbility)
                && unitPartMedium.FreeSurgeAmount > 0)
            {
                var resource = caster.Resources.GetResource(BlueprintTool.Get<BlueprintAbilityResource>(Guids.MediumInfluenceResource));
                if (resource != null)
                {
                    resource.Amount += 1;
                    unitPartMedium.FreeSurgeAmount -= 1;
                }
            }
            
        }

        public override void OnTurnOff()
        {
            foreach (StatType statType in Stats ?? Array.Empty<StatType>())
                base.Owner.Stats.GetStat(statType)?.RemoveModifiersFrom(base.Runtime);
        }

        public int GetStaticConcentrationBonus(EntityFactComponent runtime)
        {
            if (!Concentration) 
                return 0;
            using (runtime.RequestEventContext())
                return GetBonus() + MarshalBonus;
        }

        public int GetBonus()
        {
            if(base.Context.SourceAbility == BlueprintTool.Get<BlueprintAbility>(Guids.MarshalLegendaryMarshalAbility)) { return rnd.Next(1, 7); }
            switch (CharacterLevel)
            {
                case < 10:
                    return rnd.Next(1, 7);
                case < 20:
                    return rnd.Next(1, 9);
                case >= 20:
                    return rnd.Next(1, 11);
            }
            return 0;
        }

        public StatType[] Stats;
        public bool Concentration = false;
        public Random rnd = new Random();
        public int CharacterLevel = 0;
        public int MarshalBonus = 0;
    }
}

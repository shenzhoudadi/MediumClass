using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.UnitLogic;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;
using MediumClass.Prowler;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace MediumClass.Medium.NewComponents.AbilitySpecific
{
    [TypeId("eccda80d-3f23-4a4d-98c2-a1590ab1d39d")]
    class MediumTranceOfThreeComponent : UnitFactComponentDelegate<MediumTranceOfThreeComponent.TranceData>
    {
        public override void OnInitialize() => Data.Version = 1;

        public override void OnPostLoad()
        {
            // Old saves contain an EmptyComponentData/null runtime. The former
            // blueprint field was shared by every caster and was never saved.
            if (MaybeData == null || Data.Version == 0)
            {
                Data.Version = 1;
                Data.LegacyPending = true;
                Data.Applied = true;
            }
        }

        public override void OnApplyPostLoadFixes() => AdoptLegacyRanks();

        public override void OnTurnOn()
        {
            AdoptLegacyRanks();
            if (Data.Applied) return;
            var blueprint = BP?.Get();
            if (blueprint == null) return;
            Data.Version = 1;
            Data.Applied = true;
            // Astral Beacon or a full channel already provides this power.
            // Borrowing it cannot stack an additional copy of the same ranks.
            if (MinimumChannelRanks() > 0) return;
            int before = Owner.Progression.Features.GetRank(blueprint);
            int desired = Owner.Progression.Features.GetRank(BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpiritBonus));
            for (int i = 0; i < desired; i++) Owner.AddFact(blueprint);
            Data.GrantedRanks = Math.Max(0, Owner.Progression.Features.GetRank(blueprint) - before);
        }

        public override void OnTurnOff()
        {
            AdoptLegacyRanks();
            var blueprint = BP?.Get();
            if (blueprint != null)
            {
                // Level 20 can introduce Astral Beacon while the trance is still
                // active. Its persistent ranks must survive the temporary buff.
                int removable = Math.Max(0, Owner.Progression.Features.GetRank(blueprint) - MinimumChannelRanks());
                int count = Math.Min(Data.GrantedRanks, removable);
                for (int i = 0; i < count; i++) Owner.RemoveFact(blueprint);
            }
            Data.GrantedRanks = 0;
            Data.Applied = false;
            Data.LegacyPending = false;
        }

        private void AdoptLegacyRanks()
        {
            if (!Data.LegacyPending || Owner.Get<UnitPartMedium>() == null) return;
            var blueprint = BP?.Get();
            if (blueprint == null) return;
            int maximum = Owner.Progression.Features.GetRank(BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpiritBonus));
            Data.GrantedRanks = Math.Min(maximum,
                Math.Max(0, Owner.Progression.Features.GetRank(blueprint) - MinimumChannelRanks()));
            Data.LegacyPending = false;
        }

        private int MinimumChannelRanks()
        {
            var medium = Owner.Get<UnitPartMedium>();
            var blueprint = BP?.Get();
            if (medium == null || blueprint == null || !medium.ActiveSpiritClasses.Any()) return 0;
            bool active = medium.ActiveSpiritClasses.Any(spirit =>
                medium.Spirits[spirit].SpiritIntermediatePower?.Get() == blueprint);
            bool granted = active
                ? Owner.Progression.Features.GetRank(BlueprintTool.Get<BlueprintFeature>(Guids.SpiritPower)) - medium.ForgonePowers >= 2
                : Owner.HasFact(BlueprintTool.Get<BlueprintFeature>(Guids.AstralBeacon));
            return granted ? Math.Min(blueprint.Ranks, Math.Max(1, ProwlerSpiritRules.SpiritClassLevel(Owner) / 3)) : 0;
        }

        public class TranceData
        {
            [JsonProperty] public int Version;
            [JsonProperty] public int GrantedRanks;
            [JsonProperty] public bool Applied;
            [JsonProperty] public bool LegacyPending;
        }

        public BlueprintFeatureReference BP;
    }
}

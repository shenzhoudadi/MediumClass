using System.Linq;
using System;
using System.Collections.Generic;
using Kingmaker.EntitySystem;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.QA;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Utility;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utils;
using Newtonsoft.Json;
using static UnityModManagerNet.UnityModManager.ModEntry;
using BlueprintCore.Utils;
using MediumClass.Utilities;

namespace MediumClass.Medium.NewComponents.AbilitySpecific
{
	// Token: 0x02001B4E RID: 6990
	[ComponentName("Add stat bonus")]
	[AllowedOn(typeof(BlueprintFeature), false)]
	[AllowedOn(typeof(BlueprintBuff), false)]
	[AllowMultipleComponents]
	[TypeId("47229d52-5129-4439-a657-5e3d10acc5c4")]
	public class MediumContextSharedSeanceComponent : UnitFactComponentDelegate<MediumContextSharedSeanceComponent.SeanceData>
	{
		private static readonly ModLogger Logger = Logging.GetLogger(nameof(MediumContextSharedSeanceComponent));

        public override void OnInitialize()
        {
            // Native new-fact initialization runs here; saved runtimes instead use
            // RestoreOwnerLink then OnPostLoad, without calling OnInitialize.
            Data.Version = 1;
        }

        public override void OnPostLoad()
        {
            if (MaybeData == null || Data.Version == 0)
            {
                // The old EmptyComponentData runtime was saved as null. The game
                // reconstructs it from the current blueprint, with no ownership data.
                Data.LegacyCleanupPending = true;
                Data.Version = 1;
            }
            CaptureLegacySpirit();
        }

        public override void OnApplyPostLoadFixes() => CaptureLegacySpirit();

		public override void OnTurnOn()
		{
			CaptureLegacySpirit();
			UnitPartMedium medium = base.Owner.Get<UnitPartMedium>();
			if (medium?.PrimarySpirit == null || !medium.Spirits.ContainsKey(medium.PrimarySpirit))
			{

				return;
			}
            foreach (var active in medium.ActiveSpiritClasses)
            {
                var boon = medium.Spirits[active].SpiritSeanceBoon?.Get();
                if (boon == null) continue;
                foreach (var target in Recipients())
                    if (!target.HasFact(boon))
                        TrackGrant(target, target.AddFact(boon));
            }
            Data.Version = 1;
		}

		// Token: 0x0600BC6A RID: 48234 RVA: 0x00313508 File Offset: 0x00311708
		public override void OnTurnOff()
		{
            AdoptLegacyGrants();
            foreach (var grant in Data.Grants.ToArray())
            {
                if (grant.Target == null || grant.Fact == null || grant.Fact.IsDisposed) continue;
                // Rank-one boon features can be shared by two Mediums. Hand the
                // existing fact to a remaining provider instead of erasing its boon.
                if (!TryTransferGrant(grant)) grant.Target.RemoveFact(grant.Fact);
            }
            Data.Grants.Clear();
        }

        private IEnumerable<UnitEntityData> Recipients() =>
            new[] { Owner }.Concat(Game.Instance.Player.ActiveCompanions).Where(unit => unit != null).Distinct();

        private void TrackGrant(UnitEntityData target, EntityFact fact)
        {
            if (fact != null && !Data.Grants.Any(grant => grant.Target == target && grant.Fact == fact))
                Data.Grants.Add(new Grant { Target = target, Fact = fact });
        }

        private void CaptureLegacySpirit()
        {
            if (!Data.LegacyCleanupPending || Data.LegacySpirit?.Get() != null) return;
            var primary = Owner.Get<UnitPartMedium>()?.PrimarySpirit;
            if (primary?.Get() != null) Data.LegacySpirit = primary;
        }

        private void AdoptLegacyGrants()
        {
            if (!Data.LegacyCleanupPending) return;
            CaptureLegacySpirit();
            var medium = Owner.Get<UnitPartMedium>();
            if (Data.LegacySpirit != null && medium != null
                && medium.Spirits.TryGetValue(Data.LegacySpirit, out var entry))
            {
                var boon = entry.SpiritSeanceBoon?.Get();
                if (boon != null)
                    foreach (var target in Recipients()) TrackGrant(target, target.Facts.Get(boon));
            }
            else
                Logger.Log("Legacy shared seance had no recoverable primary spirit; unrelated boons were retained.");
            Data.LegacyCleanupPending = false;
            Data.LegacySpirit = null;
        }

        private bool TryTransferGrant(Grant grant)
        {
            var shared = BlueprintTool.Get<BlueprintBuff>(Guids.MediumSharedSeanceBuff);
            foreach (var provider in Recipients())
            {
                var medium = provider.Get<UnitPartMedium>();
                if (medium == null || !medium.ActiveSpiritClasses.Any(spirit =>
                    medium.Spirits[spirit].SpiritSeanceBoon?.Get() == grant.Fact.Blueprint)) continue;
                foreach (var buff in provider.Buffs.Enumerable.Where(buff => buff != Fact
                    && buff.IsActive && buff.Blueprint == shared).ToArray())
                {
                    bool transferred = false;
                    buff.CallComponents<MediumContextSharedSeanceComponent>(component =>
                    {
                        component.TrackGrant(grant.Target, grant.Fact);
                        transferred = true;
                    });
                    if (transferred) return true;
                }
            }
            return false;
        }

        public class SeanceData
        {
            [JsonProperty] public int Version;
            [JsonProperty] public List<Grant> Grants = new List<Grant>();
            [JsonProperty] public bool LegacyCleanupPending;
            [JsonProperty] public BlueprintCharacterClassReference LegacySpirit;
        }
        public class Grant
        {
            [JsonProperty] public UnitEntityData Target;
            [JsonProperty] public EntityFact Fact;
        }
	}
}

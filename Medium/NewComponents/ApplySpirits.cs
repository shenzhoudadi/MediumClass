using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintCore.Utils;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Designers;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Medium.NewComponents.AbilitySpecific;
using MediumClass.Prowler;
using MediumClass.Utilities;
using MediumClass.Utils;
using Owlcat.QA.Validation;
using Owlcat.Runtime.Core.Utils;
using TabletopTweaks.Core.NewUnitParts;
using UnityEngine;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.NewComponents
{
	// Token: 0x02001BB6 RID: 7094
	[TypeId("72326d37-dfb6-42a3-bd9c-24ee2201539b")]
	public class ApplySpirits : UnitFactComponentDelegate, IUnitReapplyFeaturesOnLevelUpHandler
	{
		private static readonly ModLogger Logger = Logging.GetLogger(nameof(ApplySpirits));
		// Resolve the current runtime owner instead of caching character state on a blueprint component.
		private UnitPartMedium medium => base.Owner.Get<UnitPartMedium>();

		public override void OnActivate()
		{
            if (!HasPrimarySpirit())
            {
                Logger.Log("Cannot apply spirit: state is missing. Save reconstruction requires investigation.");
                return;
            }
            this.TryApplySpirit();
		}

        public void HandleUnitReapplyFeaturesOnLevelUp()
        {
            if (!HasPrimarySpirit()) return;
            RefreshActiveSpirits();
            base.Owner.Ensure<UnitPartMediumPreparedSpells>().Sync();
            MediumClass.Medium.MediumSpiritSpellbookRules.ClampRemainingSlots(base.Owner.Descriptor);
        }

		public override void OnDeactivate()
		{
            base.Owner.Get<UnitPartMediumPreparedSpells>()?.Clear();
            if (!HasPrimarySpirit())
            {
                MediumClass.Medium.MediumSpiritSpellbookRules.ClampRemainingSlots(base.Owner.Descriptor);
                Logger.Log("Cannot fully remove spirit: state is missing. No new UnitPart was created.");
                return;
            }
            foreach (var spirit in medium.Spirits.Keys.ToArray())
            {
				if (spirit.Get() != medium.PrimarySpirit.Get()) {
					RemoveSecondarySpirits(spirit); }
			}
			this.Revert();
			
		}

        private bool HasPrimarySpirit()
        {
            return medium != null && medium.PrimarySpirit != null
                && medium.Spirits.ContainsKey(medium.PrimarySpirit);
        }

        private void AddPower(BlueprintFeatureReference reference, int ranks = 1)
        {
            // Move/swift/overwrite powers are optional for several spirits.
            var blueprint = reference?.Get();
            if (blueprint == null) return;
            int current = Owner.Progression.Features.GetRank(blueprint);
            for (int i = current; i < ranks; i++) Owner.AddFact(blueprint);
        }

        private void RemovePower(BlueprintFeatureReference reference)
        {
            var blueprint = reference?.Get();
            if (blueprint == null) return;
            // FeatureCollection.RemoveFact removes a single rank. These are
            // spirit-specific powers, so end every granted rank on teardown.
            int ranks = Owner.Progression.Features.GetRank(blueprint);
            for (int i = 0; i < ranks; i++) Owner.RemoveFact(blueprint);
        }

		private void ApplySpiritSpellbook(BlueprintCharacterClassReference spirit)
        {
			int SpiritPowerRank = base.Owner.Progression.Features.GetRank(BlueprintTool.Get<BlueprintFeature>(Guids.SpiritPower)) - medium.ForgonePowers;
			if ((spirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Hierophant) || spirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Archmage)) && SpiritPowerRank > 0)
			{
				base.Owner.Progression.Features.RemoveFact(medium.Spirits[spirit].SpiritLesserPower.Get());
				base.Owner.Ensure<UnitPartMediumPreparedSpells>().Sync();
			}
		}
        private void CheckWeakerSpiritAndApply(BlueprintCharacterClassReference spirit)
        {
            int rank = Owner.Progression.Features.GetRank(BlueprintTool.Get<BlueprintFeature>(Guids.SpiritPower)) - medium.ForgonePowers;
            var entry = medium.Spirits[spirit];
            bool caster = spirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Archmage)
                || spirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Hierophant);
            // The caster lesser feature is a book prohibition, not a power to grant.
            if (rank >= 1 && !caster) AddPower(entry.SpiritLesserPower);
            else if (!caster) RemovePower(entry.SpiritLesserPower);
            if (rank >= 2)
            {
                int ranks = spirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Trickster)
                    ? Math.Max(1, ProwlerSpiritRules.SpiritClassLevel(Owner) / 3) : 1;
                AddPower(entry.SpiritIntermediatePower, ranks);
                AddPower(entry.SpiritIntermediatePowerMove);
                AddPower(entry.SpiritIntermediatePowerSwift);
            }
            else
            {
                RemovePower(entry.SpiritIntermediatePower);
                RemovePower(entry.SpiritIntermediatePowerMove);
                RemovePower(entry.SpiritIntermediatePowerSwift);
            }
            if (rank >= 3) AddPower(entry.SpiritGreaterPower);
            else RemovePower(entry.SpiritGreaterPower);
            if (rank >= 4) AddPower(entry.SpiritSupremePower);
            else RemovePower(entry.SpiritSupremePower);
        }

        private void TryApplySpirit() => RefreshActiveSpirits();

        public void RefreshActiveSpirits()
        {
            if (!HasPrimarySpirit()) return;
            foreach (var spirit in medium.ActiveSpiritClasses.ToArray())
            {
                // A fully channeled spirit replaces Astral Beacon's secondary variants.
                RemovePower(medium.Spirits[spirit].OverwriteIntermediatePower);
                RemovePower(medium.Spirits[spirit].OverwriteGreaterPower);
                CheckWeakerSpiritAndApply(spirit);
                ApplySpiritSpellbook(spirit);
            }
            if (Owner.Progression.Features.HasFact(BlueprintTool.Get<BlueprintFeature>(Guids.AstralBeacon)))
                foreach (var spirit in medium.Spirits.Keys.Where(s => !medium.IsActiveSpirit(s)))
                    ApplySecondarySpirits(spirit);
            RefreshSpiritModifiers();
        }

        private void RefreshSpiritModifiers()
        {
            // Updating a feature rank or recalculating a buff's context does not
            // replace modifiers created from a fixed integer in OnTurnOn. Refresh
            // only these two already-enabled buffs in their own runtime context;
            // never deactivate the channel or invoke the whole buff lifecycle.
            foreach (var buff in Owner.Buffs.Enumerable.Where(b => b.IsActive && b.IsTurnedOn).ToArray())
            {
                if (buff.Blueprint == BlueprintTool.Get<BlueprintBuff>(Guids.MediumSpiritBonusBuff))
                    buff.CallComponents<MediumContextSpiritBonusComponent>(component => component.RefreshModifiers());
                else if (buff.Blueprint == BlueprintTool.Get<BlueprintBuff>(Guids.MediumInfluenceDebuff))
                    buff.CallComponents<MediumInfluencePenaltyComponent>(component => component.RefreshModifiers());
            }
        }

		private void ApplySecondarySpirits(BlueprintCharacterClassReference spirit)
		{
            var entry = medium.Spirits[spirit];
            int ranks = spirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Trickster)
                ? Math.Max(1, ProwlerSpiritRules.SpiritClassLevel(Owner) / 3) : 1;
            if (entry.OverwriteIntermediatePower?.Get() != null)
            {
                RemovePower(entry.SpiritIntermediatePower);
                AddPower(entry.OverwriteIntermediatePower, ranks);
            }
            else AddPower(entry.SpiritIntermediatePower, ranks);
            AddPower(entry.SpiritIntermediatePowerMove);
            AddPower(entry.SpiritIntermediatePowerSwift);
            if (entry.OverwriteGreaterPower?.Get() != null)
            {
                RemovePower(entry.SpiritGreaterPower);
                AddPower(entry.OverwriteGreaterPower);
            }
            else AddPower(entry.SpiritGreaterPower);
            AddPower(entry.SpiritSupremePower);
		}

		private void RemoveSecondarySpirits(BlueprintCharacterClassReference spirit)
		{
			RemovePower(medium.Spirits[spirit].SpiritIntermediatePower);
			RemovePower(medium.Spirits[spirit].SpiritIntermediatePowerMove);
			RemovePower(medium.Spirits[spirit].SpiritIntermediatePowerSwift);
			RemovePower(medium.Spirits[spirit].OverwriteIntermediatePower);
			RemovePower(medium.Spirits[spirit].SpiritGreaterPower);
			RemovePower(medium.Spirits[spirit].OverwriteGreaterPower);
			RemovePower(medium.Spirits[spirit].SpiritSupremePower);
		}

		// Token: 0x0600BDCF RID: 48591 RVA: 0x00318090 File Offset: 0x00316290
		private void Revert()
		{
			List<Buff> list = base.Owner.Buffs.Enumerable.ToTempList<Buff>();
			foreach (Buff buff in list)
			{
				// Preserve the existing name-based cleanup for both shipped display languages.
				if (buff.Blueprint.Name.Contains("Trickster's Edge") || buff.Blueprint.Name.Contains("诡术师绝技")){
					base.Owner.Buffs.RemoveFact(buff);
				}
				if (buff.Blueprint.Name.Contains("Seance Boon") || buff.Blueprint.Name.Contains("降灵奖励"))
				{
					base.Owner.Buffs.RemoveFact(buff);
				}
			}

			RemovePower(medium.Spirits[medium.PrimarySpirit].SpiritLesserPower);
			RemovePower(medium.Spirits[medium.PrimarySpirit].SpiritIntermediatePower);
			RemovePower(medium.Spirits[medium.PrimarySpirit].SpiritIntermediatePowerMove);
			RemovePower(medium.Spirits[medium.PrimarySpirit].SpiritIntermediatePowerSwift);
			RemovePower(medium.Spirits[medium.PrimarySpirit].SpiritGreaterPower);
			RemovePower(medium.Spirits[medium.PrimarySpirit].SpiritSupremePower);

			foreach (var extra in medium.AdditionalSpirits ?? new List<BlueprintCharacterClassReference>())
                if (medium.Spirits.TryGetValue(extra, out var entry)) RemovePower(entry.SpiritLesserPower);
            AddPower(BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpellcasterFeatProhibitArchmage));
            AddPower(BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpellcasterFeatProhibitHierophant));
            medium.ClearChannelSelection();
            MediumClass.Medium.MediumInfluenceRules.Reset(Owner.Descriptor);
			MediumClass.Medium.MediumSpiritSpellbookRules.ClampRemainingSlots(base.Owner.Descriptor);
			base.ClearData();
		}
	}
}

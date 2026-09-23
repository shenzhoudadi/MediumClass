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
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using MediumClass.Medium.NewUnitParts;
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
	public class ApplySpirits : UnitFactComponentDelegate
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

		public override void OnDeactivate()
		{
            if (!HasPrimarySpirit())
            {
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

        private void AddPower(BlueprintFeatureReference reference)
        {
            // Move/swift/overwrite powers are optional for several spirits.
            var blueprint = reference?.Get();
            if (blueprint != null) base.Owner.AddFact(blueprint);
        }

        private void RemovePower(BlueprintFeatureReference reference)
        {
            var blueprint = reference?.Get();
            if (blueprint != null) base.Owner.RemoveFact(blueprint);
        }

		private void ApplySpiritSpellbook()
        {
			int SpiritPowerRank = base.Owner.Progression.Features.GetRank(BlueprintTool.Get<BlueprintFeature>(Guids.SpiritPower)) - medium.ForgonePowers;
			if ((medium.PrimarySpirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Hierophant) || medium.PrimarySpirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Archmage)) && SpiritPowerRank > 0)
			{
				base.Owner.Progression.Features.RemoveFact(medium.Spirits[medium.PrimarySpirit].SpiritLesserPower.Get());
			}
		}
		private void CheckWeakerSpiritAndApply()
        {			 
			int SpiritPowerRank = base.Owner.Progression.Features.GetRank(BlueprintTool.Get<BlueprintFeature>(Guids.SpiritPower)) - medium.ForgonePowers;
			if ((SpiritPowerRank >= 1))
				AddPower(medium.Spirits[medium.PrimarySpirit].SpiritLesserPower);
			if ((SpiritPowerRank >= 2))
            {
				if (medium.PrimarySpirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Trickster))
				{
					int val = base.Owner.Progression.GetClassLevel(BlueprintTool.Get<BlueprintCharacterClass>(Guids.Medium)) / 3;
					for (int i = 1; i < val; i++)
					{
						AddPower(medium.Spirits[medium.PrimarySpirit].SpiritIntermediatePower);
					}
				}
				AddPower(medium.Spirits[medium.PrimarySpirit].SpiritIntermediatePower);
			}
			if ((SpiritPowerRank >= 2))
				AddPower(medium.Spirits[medium.PrimarySpirit].SpiritIntermediatePowerMove);
			if ((SpiritPowerRank >= 2))
				AddPower(medium.Spirits[medium.PrimarySpirit].SpiritIntermediatePowerSwift);
			if ((SpiritPowerRank >= 3))
				AddPower(medium.Spirits[medium.PrimarySpirit].SpiritGreaterPower);
			if ((SpiritPowerRank >= 4))
				AddPower(medium.Spirits[medium.PrimarySpirit].SpiritSupremePower);
		}

		private void TryApplySpirit()
		{
			CheckWeakerSpiritAndApply();
			ApplySpiritSpellbook();


			if (base.Owner.Progression.Features.HasFact(BlueprintTool.Get<BlueprintFeature>(Guids.AstralBeacon)))
            {
				foreach (var spirit in medium.Spirits.Keys)
				{
					if (spirit.Get() != medium.PrimarySpirit.Get())
						ApplySecondarySpirits(spirit);
				}
			}
			
		}

		private void ApplySecondarySpirits(BlueprintCharacterClassReference spirit)
		{
			if(spirit.Get() == BlueprintTool.Get<BlueprintCharacterClass>(Guids.Trickster))
            {
				int val = base.Owner.Progression.GetClassLevel(BlueprintTool.Get<BlueprintCharacterClass>(Guids.Medium)) / 3;
				for(int i=1; i < val; i++)
                {
					AddPower(medium.Spirits[spirit].SpiritIntermediatePower);
				}
			}
			AddPower(medium.Spirits[spirit].SpiritIntermediatePower);
			AddPower(medium.Spirits[spirit].SpiritIntermediatePowerMove);
			AddPower(medium.Spirits[spirit].SpiritIntermediatePowerSwift);
			AddPower(medium.Spirits[spirit].OverwriteIntermediatePower);
			AddPower(medium.Spirits[spirit].SpiritGreaterPower);
			AddPower(medium.Spirits[spirit].OverwriteGreaterPower);
			AddPower(medium.Spirits[spirit].SpiritSupremePower);
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
				if (buff.Blueprint.Name.Contains("Trickster's Edge")){
					base.Owner.Buffs.RemoveFact(buff);
				}
				if (buff.Blueprint.Name.Contains("Seance Boon"))
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

			base.Owner.Progression.Features.AddFact(BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpellcasterFeatProhibitArchmage), Context);
			base.Owner.Progression.Features.AddFact(BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpellcasterFeatProhibitHierophant), Context);

			medium.PrimarySpirit = new BlueprintCharacterClassReference();
			base.ClearData();
		}
	}
}

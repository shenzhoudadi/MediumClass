using BlueprintCore.Utils;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Utility;
using MediumClass.Utilities;
using MediumClass.Medium.NewActions;
using Kingmaker.UnitLogic.Abilities.Components;
using MediumClass.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TurnBased.Controllers;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Medium.NewUnitParts
{
    public class UnitPartMedium : UnitPart
	{
		private static readonly ModLogger Logger = Logging.GetLogger(nameof(UnitPartMedium));
		public void AddSpiritEntry(BlueprintCharacterClassReference spiritClass, BlueprintBuffReference spiritInfluencePenalty, BlueprintAbilityResourceReference influence, BlueprintFeatureReference feature, BlueprintFeatureReference boon,
			EntityFact source, bool concentration, StatType[] stats, StatType[] penalty_stats, 
			BlueprintFeatureReference LesserPower, BlueprintFeatureReference IntermediatePower, BlueprintFeatureReference OverwriteIntermediate, BlueprintFeatureReference GreaterPower, BlueprintFeatureReference OverwriteGreater, BlueprintFeatureReference SupremePower, 
			BlueprintFeatureReference IntermediatePowerMove, BlueprintFeatureReference IntermediatePowerSwift)
        {
			if(Spirits.ContainsKey(spiritClass)) { return; }
			Spirits.Add(spiritClass, new SpiritEntry()
			{
				SpiritInfluencePenalty = spiritInfluencePenalty,
				InfluenceResource = influence,
				SpiritSeanceBoon = boon,
				SpiritLesserPower = LesserPower,
				SpiritIntermediatePower = IntermediatePower,
				SpiritIntermediatePowerMove = IntermediatePowerMove,
				SpiritIntermediatePowerSwift = IntermediatePowerSwift,
				OverwriteIntermediatePower = OverwriteIntermediate,
				SpiritGreaterPower = GreaterPower,
				OverwriteGreaterPower = OverwriteGreater,
				SpiritSupremePower = SupremePower,
				SpiritBonus = new SpiritStatEntry()
				{
					Stats = stats,
					Concentration = concentration,
					SpiritBonusFeature = feature,
				},
				SpiritPenalty = new SpiritStatEntry()
				{
					Stats = penalty_stats,
					Concentration = false
				},
				Source = source
			});
        }

		public void RemoveSpiritEntry(EntityFact source, BlueprintCharacterClassReference spiritClass)
		{
			// Six components share one channeling fact. Removing one must not erase the others.
			if (spiritClass == null || !Spirits.TryGetValue(spiritClass, out var entry)
				|| entry.Source != source) return;
			Spirits.Remove(spiritClass);
			TryRemove();
		}

		private void TryRemove()
		{
			if(!Spirits.Any()) { this.RemoveSelf(); }
		}

		public void AddWeakerSpiritChannel(BlueprintAbility SourceAbility)
        {
			if (SourceAbility == BlueprintTool.Get<BlueprintAbility>(Guids.WeakerSpiritChannelOneAbility))
			{
				ForgonePowers = 1;
				FreeSurgeAmount = 2;
			}
			else if (SourceAbility == BlueprintTool.Get<BlueprintAbility>(Guids.WeakerSpiritChannelTwoAbility))
			{
				ForgonePowers = 2;
				FreeSurgeAmount = 4;
			}
			else if (SourceAbility == BlueprintTool.Get<BlueprintAbility>(Guids.WeakerSpiritChannelThreeAbility))
			{
				ForgonePowers = 3;
				FreeSurgeAmount = 6;
			}
			else if (SourceAbility == BlueprintTool.Get<BlueprintAbility>(Guids.WeakerSpiritChannelFourAbility))
			{
				ForgonePowers = 4;
				FreeSurgeAmount = 8;
			}
			if(base.Owner.HasFact(BlueprintTool.Get<BlueprintUnitFact>(Guids.MediumSpiritMastery)))
            {
				FreeSurgeAmount *= 2;
				FreeSurgeAmount += 2;
			}
				
		}

		public void RemoveWeakerSpiritChannel()
		{
			FreeSurgeAmount = 0;
			ForgonePowers = 0;
			if (base.Owner.HasFact(BlueprintTool.Get<BlueprintUnitFact>(Guids.MediumSpiritMastery)))
				FreeSurgeAmount = 2;
		}

		public void HandleSpiritMastery()
        {
			FreeSurgeAmount = 2;
        }

		public void RemoveSpiritMastery()
        {
			FreeSurgeAmount = 0;
        }

		public bool IsInfluencePenalty(bool isSecondaryCheck = false)
        {

			if (base.Owner.Descriptor.Resources.GetResourceAmount(BlueprintTool.Get<BlueprintAbilityResource>(Guids.MediumInfluenceResource)) <= 2) { return true; }
			return false;
        }

		public void HandleInfluencePenalty()
		{
			// Loading or teardown can still leave no valid primary spirit. Channeling checks
			// influence only after ContextActionApplySpirit has selected the new spirit.
			if (PrimarySpirit == null || !Spirits.TryGetValue(PrimarySpirit, out var entry)) return;
			if (IsInfluencePenalty()) { base.Owner.Buffs.AddBuff(entry.SpiritInfluencePenalty.Get(), base.Owner, new TimeSpan(24, 0, 0)); }
		}

		public void AddSpiritFocus(BlueprintCharacterClassReference spirit)
        {
			Spirits[spirit].SpiritFocus = 1;
		}

		public void RemoveSpiritFocus(BlueprintCharacterClassReference spirit)
        {
			Spirits[spirit].SpiritFocus = 0;
        }

		public override void OnPostLoad()
		{
			base.OnPostLoad();
			var channelBuff = Owner.Buffs.Enumerable.FirstOrDefault(buff => buff.Blueprint ==
				BlueprintTool.Get<BlueprintBuff>(Guids.MediumChannelSpiritPrimarySpiritBuff));
			if (channelBuff == null)
			{
				// Buff restoration may not have completed yet. Absence here is not proof
				// that the saved selection is invalid; normal deactivation clears it.
				Logger.Log("Channel buff was not available during OnPostLoad. Existing primary spirit was retained; verify buff restoration order.");
				return;
			}

			// All six spirits use the same buff. Recover the choice from the originating
			// channel ability's action, not an English/localized display name.
			var actions = channelBuff.Context?.SourceAbility?.GetComponent<AbilityEffectRunAction>();
			var channelAction = actions?.Actions?.Actions?.OfType<ContextActionApplySpirit>().FirstOrDefault();
			if (channelAction?.Spirit?.Get() != null)
				PrimarySpirit = channelAction.Spirit;
			else
				Logger.Log("Primary spirit could not be reconstructed from the saved channel context. Existing state was retained; inspect the save before continuing.");
		}

		public class SpiritStatEntry
		{
			public StatType[] Stats;
			public bool Concentration = false;
			public BlueprintFeatureReference SpiritBonusFeature;
		}
		public class SpiritEntry
		{
			public BlueprintBuffReference SpiritInfluencePenalty;
			public BlueprintAbilityResourceReference InfluenceResource;
			public BlueprintFeatureReference SpiritSeanceBoon;
			public BlueprintFeatureReference SpiritLesserPower;
			public BlueprintFeatureReference SpiritIntermediatePower;
			public BlueprintFeatureReference SpiritIntermediatePowerMove;
			public BlueprintFeatureReference SpiritIntermediatePowerSwift;
			public BlueprintFeatureReference OverwriteIntermediatePower;
			public BlueprintFeatureReference SpiritGreaterPower;
			public BlueprintFeatureReference OverwriteGreaterPower;
			public BlueprintFeatureReference SpiritSupremePower;
			public SpiritStatEntry SpiritBonus;
			public SpiritStatEntry SpiritPenalty;
			public int SpiritFocus = 0;
			public EntityFact Source;
		}
		public BlueprintCharacterClassReference PrimarySpirit = new BlueprintCharacterClassReference();
		public BlueprintCharacterClassReference SecondarySpirit = new BlueprintCharacterClassReference();
		public IDictionary<BlueprintCharacterClassReference, SpiritEntry> Spirits = new Dictionary<BlueprintCharacterClassReference, SpiritEntry>();
		public int ForgonePowers = 0;
		public int FreeSurgeAmount = 0;

	}
}

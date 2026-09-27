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
			// This part also owns the saved channel choice and spent free surges.
			// An empty runtime catalogue must never delete that persistent state.
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
			ResetDailySurges();
        }

        public void ResetDailySurges()
        {
            bool mastery = Owner.HasFact(BlueprintTool.Get<BlueprintUnitFact>(Guids.MediumSpiritMastery));
            FreeSurgeAmount = InfluenceMath.DailyFreeSurges(ForgonePowers, mastery);
        }

		public void RemoveSpiritMastery()
        {
			FreeSurgeAmount = 0;
        }

		public bool IsInfluencePenalty(bool isSecondaryCheck = false)
        {

			return MediumInfluenceRules.Amount(Owner.Descriptor) >= 3;
        }

		public void HandleInfluencePenalty()
		{
			// Loading or teardown can still leave no valid primary spirit. Channeling checks
			// influence only after ContextActionApplySpirit has selected the new spirit.
			MediumInfluenceRules.RefreshPenalty(Owner.Descriptor);
		}

		public void AddSpiritFocus(BlueprintCharacterClassReference spirit)
        {
			if (spirit != null && Spirits.TryGetValue(spirit, out var entry)) entry.SpiritFocus = 1;
		}

		public void RemoveSpiritFocus(BlueprintCharacterClassReference spirit)
        {
			if (spirit != null && Spirits.TryGetValue(spirit, out var entry)) entry.SpiritFocus = 0;
        }

		public override void OnPostLoad()
		{
			base.OnPostLoad();
			RestoreSavedSelection();
		}

		public override void OnPreSave()
		{
			base.OnPreSave();
			ChannelSaveVersion = 1;
		}

		public override void OnTurnOn()
		{
			base.OnTurnOn();
			MediumChannelRestore.Restore(Owner.Descriptor);
		}

		internal void RestoreSavedSelection()
		{
			var channelBuff = Owner.Buffs.Enumerable.FirstOrDefault(buff => buff.Blueprint ==
				BlueprintTool.Get<BlueprintBuff>(Guids.MediumChannelSpiritPrimarySpiritBuff) && buff.IsActive);
			if (channelBuff == null)
			{
				// Buff restoration may not have completed yet. Absence here is not proof
				// that the saved selection is invalid; normal deactivation clears it.
				Logger.Log("Channel buff was not available during OnPostLoad. Existing primary spirit was retained; verify buff restoration order.");
				return;
			}

			// All six spirits use the same buff. Recover the choice from the originating
			// channel ability's action, not an English/localized display name.
			var actions = (channelBuff.Context?.SourceAbility ?? channelBuff.SourceAbility)?.GetComponent<AbilityEffectRunAction>();
			var channelAction = actions?.Actions?.Actions?.OfType<ContextActionApplySpirit>().FirstOrDefault();
			if (PrimarySpirit?.Get() == null && channelAction?.Spirit?.Get() != null)
				PrimarySpirit = channelAction.Spirit;
			if (PrimarySpirit?.Get() == null)
				Logger.Log("Primary spirit could not be reconstructed from the saved channel context. Existing state was retained; inspect the save before continuing.");

			if (ChannelSaveVersion == 0)
			{
				// Old saves omitted these fields. Recover the weaker-channel choice
				// from its own buff, but never invent unspent daily free surges.
				var weaker = Owner.Buffs.Enumerable.FirstOrDefault(buff => buff.IsActive && buff.Blueprint ==
					BlueprintTool.Get<BlueprintBuff>(Guids.WeakerSpiritChannelBuff));
				int remaining = FreeSurgeAmount;
				if (weaker?.Context?.SourceAbility != null) AddWeakerSpiritChannel(weaker.Context.SourceAbility);
				FreeSurgeAmount = remaining;
				ChannelSaveVersion = 1;
			}
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
		[JsonProperty] public int ChannelSaveVersion;
		[JsonProperty] public BlueprintCharacterClassReference PrimarySpirit = new BlueprintCharacterClassReference();
		[JsonProperty] public BlueprintCharacterClassReference SecondarySpirit = new BlueprintCharacterClassReference();
		// Preserve each formally added spirit; never infer extras from temporary powers.
		[JsonProperty] public List<BlueprintCharacterClassReference> AdditionalSpirits = new List<BlueprintCharacterClassReference>();

		[JsonIgnore]
		public IEnumerable<BlueprintCharacterClassReference> ActiveSpiritClasses =>
			new[] { PrimarySpirit }.Concat(AdditionalSpirits ?? Enumerable.Empty<BlueprintCharacterClassReference>())
			.Where(s => s?.Get() != null && Spirits.ContainsKey(s))
			.GroupBy(s => s.Get().AssetGuid).Select(g => g.First());

		public bool IsActiveSpirit(BlueprintCharacterClassReference spirit) =>
			spirit?.Get() != null && ActiveSpiritClasses.Any(s => s.Get() == spirit.Get());

		public void ClearChannelSelection()
		{
			PrimarySpirit = new BlueprintCharacterClassReference();
			AdditionalSpirits?.Clear();
		}
		[JsonIgnore] public IDictionary<BlueprintCharacterClassReference, SpiritEntry> Spirits = new Dictionary<BlueprintCharacterClassReference, SpiritEntry>();
		[JsonProperty] public int ForgonePowers = 0;
		[JsonProperty] public int FreeSurgeAmount = 0;

	}
}

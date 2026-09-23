using BlueprintCore.Utils;
using MediumClass.Utilities;
using System;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
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

namespace MediumClass.Medium.NewComponents.AbilitySpecific
{
	// Token: 0x02001B4E RID: 6990
	[ComponentName("Add stat bonus")]
	[AllowedOn(typeof(BlueprintFeature), false)]
	[AllowedOn(typeof(BlueprintBuff), false)]
	[AllowMultipleComponents]
	[TypeId("995fb9e0-f2f5-4dc2-a281-b7959ea95cda")]
	public class MediumContextSpiritBonusComponent : UnitFactComponentDelegate<AddContextStatBonus.ComponentData>
	{
		private static readonly ModLogger Logger = Logging.GetLogger(nameof(MediumContextSpiritBonusComponent));
		public override void OnTurnOn()
		{
			RemoveOwnModifiers();
			UnitPartMedium medium = base.Owner.Get<UnitPartMedium>();
			if (medium?.PrimarySpirit == null || !medium.Spirits.ContainsKey(medium.PrimarySpirit))
			{
				return;
			}
			int ranks = base.Owner.Progression.Features.GetRank(medium.Spirits[medium.PrimarySpirit].SpiritBonus.SpiritBonusFeature) + medium.Spirits[medium.PrimarySpirit].SpiritFocus;
			foreach (StatType stat in medium.Spirits[medium.PrimarySpirit].SpiritBonus.Stats ?? Array.Empty<StatType>())
			{
				base.Owner.Stats.GetStat(stat)?.AddModifier(ranks, base.Runtime, ModifierDescriptor.UntypedStackable);
			}
		}

		// Token: 0x0600BC6A RID: 48234 RVA: 0x00313508 File Offset: 0x00311708
		public override void OnTurnOff()
		{
			RemoveOwnModifiers();
		}

		private void RemoveOwnModifiers()
		{
			// Definitions survive load/teardown even when the per-unit spirit table does not.
			foreach (var component in BlueprintTool.Get<BlueprintFeature>(Guids.MediumChannelSpirit)
				.GetComponents<MediumSpiritComponent>())
				foreach (var stat in component.Stats ?? Array.Empty<StatType>())
					Owner.Stats.GetStat(stat)?.RemoveModifiersFrom(Runtime);
		}
	}
}

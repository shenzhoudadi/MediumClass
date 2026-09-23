using System;
using BlueprintCore.Blueprints.Configurators.Classes;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Designers.EventConditionActionSystem.Events;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.Utility;
using MediumClass.Utilities;
using MediumClass.Utils;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Medium.NewComponents.AbilitySpecific
{
	// Token: 0x020022E0 RID: 8928
	[AllowedOn(typeof(BlueprintUnitFact), false)]
	[TypeId("fa7cb9ed3a0d47769375739a2c1cfa1d")]
	public class AddTrickstersEdge : UnitFactComponentDelegate, ISubscriber, IInitiatorRulebookSubscriber, IUnitSubscriber
	{
		private static readonly ModLogger Logger = Logging.GetLogger(nameof(AddTrickstersEdge));

		public override void OnTurnOn()
		{
			// Check if we have a skill.
			if(!Skill.IsSkill()) { return; }
			ModifiableValue stat = base.Owner.Stats.GetStat(Skill);
			if (stat == null) { return; }
			// Reapplication must replace this component's modifier, not stack it.
			stat.RemoveModifiersFrom(base.Runtime);
			int mediumLevel = base.Owner.Progression.GetClassLevel(BlueprintTool.Get<BlueprintCharacterClass>(Guids.Medium));
			// Preserve the original mod's arithmetic and zero-rank class-skill workaround.
			// Whether BaseStatBonus represents actual ranks needs in-game verification.
			int originalValue = stat.BaseValue;
			int buffValue = mediumLevel - originalValue;
			if (originalValue == 0) buffValue += 3;
			stat.AddModifier(buffValue, base.Runtime, ModifierDescriptor.BaseStatBonus);
		}

		public override void OnTurnOff()
		{
			ModifiableValue stat = base.Owner.Stats.GetStat(Skill);
			if (stat == null)
			{
				return;
			}
			stat.RemoveModifiersFrom(base.Runtime);
		}

		public StatType Skill;
	}
}

using System;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Mechanics;
using MediumClass.Utils;
using UnityEngine;
using UnityEngine.Serialization;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Medium.NewComponents.AbilitySpecific
{

	[AllowedOn(typeof(BlueprintUnitFact), false)]
	[AllowMultipleComponents]
	[TypeId("963858fe0f384d0d90a852be8fb9628b")]
	public class AddSurpriseStrike : UnitFactComponentDelegate, IInitiatorRulebookHandler<RulePrepareDamage>, IRulebookHandler<RulePrepareDamage>, ISubscriber, IInitiatorRulebookSubscriber
	{
		private static readonly ModLogger Logger = Logging.GetLogger(nameof(AddSurpriseStrike));

		public void OnEventAboutToTrigger(RulePrepareDamage evt)
		{
			
			// Damage can originate from spells or effects without a weapon attack roll.
			if (evt.DamageBundle?.Weapon == null || evt.ParentRule?.AttackRoll == null
				|| !evt.ParentRule.AttackRoll.IsTargetFlatFooted)
			{
				return;
			}

			DamageDescription Damage = new DamageDescription
			{
				// Precision damage follows the attacking weapon's physical form.
				// Build a per-event description; do not mutate the shared blueprint component.
				TypeDescription = new DamageTypeDescription
				{
					Type = DamageType.Type,
					Common = DamageType.Common,
					Physical = { Form = evt.DamageBundle.Weapon.Blueprint.DamageType.Physical.Form }
				},
				Dice = new DiceFormula(Value.DiceCountValue.Calculate(base.Context), Value.DiceType),
				Bonus = Value.BonusValue.Calculate(base.Context),
				SourceFact = base.Fact
			};

			BaseDamage baseDamage = Damage.CreateDamage();
			evt.Add(baseDamage);
		}

		public void OnEventDidTrigger(RulePrepareDamage evt)
		{
		}

		public DamageTypeDescription DamageType;
		public ContextDiceValue Value;
	}
}

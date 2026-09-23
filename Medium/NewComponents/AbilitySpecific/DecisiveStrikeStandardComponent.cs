using System;
using System.Collections.Generic;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using MediumClass.Utils;
using UnityEngine;
using UnityEngine.Serialization;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Medium.NewComponents.AbilitySpecific
{
	// Token: 0x02001BF2 RID: 7154
	[AllowedOn(typeof(BlueprintUnitFact), false)]
	// The upstream ID belongs to the game's FreeActionSpell component.
	[TypeId("b61fefa0-ffda-416f-a405-50f5eaa0094a")]
	public class DecisiveStrikeStandardComponent : UnitFactComponentDelegate, IInitiatorRulebookHandler<RuleCastSpell>, IRulebookHandler<RuleCastSpell>, ISubscriber, IInitiatorRulebookSubscriber
	{
		private static readonly ModLogger Logger = Logging.GetLogger(nameof(DecisiveStrikeStandardComponent));
		public override void OnTurnOn()
		{
			// The modifier is granted to this buff's owner. Saved casting context may be absent.
			base.Owner.Get<UnitPartAbilityModifiers>()?.RemoveEntry(base.Fact);
			var abilitiesList = base.Owner.Descriptor.Abilities;
			foreach (var ability in abilitiesList)
            {
				if(ability.Blueprint.IsSpell && !ability.Blueprint.IsFullRoundAction)
                {
					base.Owner.Ensure<UnitPartAbilityModifiers>().AddEntry(new UnitPartAbilityModifiers.ActionEntry(base.Fact, UnitCommand.CommandType.Free, ability.Blueprint));
				}
            }
		}

		// Token: 0x0600BF1B RID: 48923 RVA: 0x0031D29B File Offset: 0x0031B49B
		public override void OnTurnOff()
		{
			base.Owner.Get<UnitPartAbilityModifiers>()?.RemoveEntry(base.Fact);
		}

        public void OnEventAboutToTrigger(RuleCastSpell evt) { }

        public void OnEventDidTrigger(RuleCastSpell evt)
        {
			OnTurnOff();
        }
    }
}

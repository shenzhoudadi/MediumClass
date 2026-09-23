using Kingmaker.EntitySystem;
using System.Runtime.CompilerServices;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using MediumClass.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Medium.NewComponents
{
    [TypeId("996f6104335842e08262f14bd153298e")]
    public class AddResourcelessSpell : UnitFactComponentDelegate, IInitiatorRulebookHandler<RuleCastSpell>, IRulebookHandler<RuleCastSpell>, ISubscriber, IInitiatorRulebookSubscriber
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(AddResourcelessSpell));
        public void OnEventAboutToTrigger(RuleCastSpell evt)
        {
            var ability = evt.Context?.Ability;
            if (ability == null) return;
            // Blueprint components are shared; costs belong to this runtime fact instance.
            var originalCosts = costsByRuntime.GetOrCreateValue(Runtime);
            if (!originalCosts.ContainsKey(ability))
                originalCosts.Add(ability, ability.ExtraSpellSlotCost);
            ability.ExtraSpellSlotCost = -1;
        }

        public void OnEventDidTrigger(RuleCastSpell evt) { }

        public override void OnTurnOff()
        {
            if (costsByRuntime.TryGetValue(Runtime, out var originalCosts))
            {
                foreach (var entry in originalCosts)
                {
                    // Do not overwrite a later change made by another effect.
                    if (entry.Key.ExtraSpellSlotCost == -1)
                        entry.Key.ExtraSpellSlotCost = entry.Value;
                }
                costsByRuntime.Remove(Runtime);
            }
            base.OnTurnOff();
        }
        private static readonly ConditionalWeakTable<EntityFactComponent, Dictionary<AbilityData, int>> costsByRuntime = new();
    }
}

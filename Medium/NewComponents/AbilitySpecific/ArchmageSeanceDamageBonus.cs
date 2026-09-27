using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;

namespace MediumClass.Medium.NewComponents.AbilitySpecific
{
    [TypeId("be4fb120fb1140aeafb908997b01e3f9")]
    internal sealed class ArchmageSeanceDamageBonus : UnitFactComponentDelegate,
        IInitiatorRulebookHandler<RuleCalculateDamage>, IRulebookHandler<RuleCalculateDamage>,
        ISubscriber, IInitiatorRulebookSubscriber
    {
        private static readonly RulebookEvent.CustomDataKey AppliedKey =
            new RulebookEvent.CustomDataKey("MediumClass.ArchmageSeanceDamageBonus");

        public void OnEventAboutToTrigger(RuleCalculateDamage evt)
        {
            if (evt?.ParentRule == null || !IsSpellDamage(evt)
                || evt.TryGetCustomData<bool>(AppliedKey, out var applied) && applied)
                return;

            foreach (var damage in evt.DamageBundle)
            {
                if (damage == null || damage.Precision || damage.IgnoreModifiers
                    || (damage.Dice.ModifiedValue.Rolls <= 0 && damage.TotalBonus <= 0
                        && damage.PreRolledValue.GetValueOrDefault() <= 0))
                    continue;

                // One fixed +2 for the entire bundle, carried by its first real
                // damage chunk. Do not multiply it by dice or elemental types.
                evt.SetCustomData(AppliedKey, true);

                // Hellfire Ray's unholy half and later AoE targets reuse the
                // original RollAndBonusValue. It already includes this bonus.
                // Native CalculateDamageValue skips Bonus for PreRolledValue;
                // adding TargetRelated here would apply the boon twice.
                if (!damage.PreRolledValue.HasValue)
                    damage.AddModifier(2, Fact);
                return;
            }
        }

        private static bool IsSpellDamage(RuleCalculateDamage evt)
        {
            // ContextActionDealDamage explicitly supplies SourceAbility, also
            // for rays/touch spells whose bundle has a synthetic ray weapon.
            var spell = evt.ParentRule.SourceAbility;
            if (spell != null) return spell.IsSpell;

            // A spell buff may be the reason for an ordinary weapon attack.
            // Do not turn those weapon hits into spell damage merely because
            // their inherited reason happens to contain a spell context.
            if (evt.DamageBundle.Weapon != null) return false;
            return (evt.Reason?.Context ?? evt.ParentRule.Reason?.Context)?.SourceAbility?.IsSpell == true;
        }

        public void OnEventDidTrigger(RuleCalculateDamage evt) { }
    }
}

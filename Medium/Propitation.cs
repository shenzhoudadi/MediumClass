using BlueprintCore.Actions.Builder;
using BlueprintCore.Blueprints.CustomConfigurators;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Mechanics.Actions;
using MediumClass.Utilities;

namespace MediumClass.Medium
{
    internal static class Propitation
    {
        internal const string ResourceGuid = "b9a4a702-b2d7-4d8a-883f-7a2c872de103";
        internal const string AbilityGuid = "d649cfa1-2eb7-4eae-a361-58ccfd72b132";
        public static void ConfigureEnabled()
        {
            var resource = AbilityResourceConfigurator.New("MediumPropitiationResource", ResourceGuid)
                .SetMaxAmount(new BlueprintAbilityResource.Amount { BaseValue = 1 }).Configure();
            var ability = AbilityConfigurator.New("MediumPropitiationAbility", AbilityGuid)
                .SetDisplayName("MediumPropitation.Name")
                .SetDescription("MediumPropitation.Description")
                .SetIcon("assets/icons/spiritsurge.png")
                .SetType(AbilityType.Supernatural)
                .SetRange(AbilityRange.Personal)
                .SetActionType(UnitCommand.CommandType.Standard)
                .AddComponent<PropitiationRequirement>()
                .AddAbilityResourceLogic(requiredResource: resource, amount: 1, isSpendResource: true)
                .AddAbilityEffectRunAction(ActionsBuilder.New().Add<ContextActionPropitiation>())
                .Configure();
            FeatureConfigurator.New("MediumPropitation", Guids.MediumPropitation)
                .SetDisplayName("MediumPropitation.Name")
                .SetDescription("MediumPropitation.Description")
                .SetIsClassFeature(true)
                .AddAbilityResources(resource: resource, restoreAmount: true, restoreOnLevelUp: false)
                .AddFacts(new() { ability })
                .Configure();
        }
    }

    [TypeId("e6bac4b442b14fbfa5ce644ae791c80e")]
    internal sealed class ContextActionPropitiation : ContextAction
    {
        public override string GetCaption() => "Reduce accumulated influence by one";
        public override void RunAction() => MediumInfluenceRules.Soothe(Context.MaybeCaster?.Descriptor);
    }

    [TypeId("0b0d595c016d4833ac3c190907207497")]
    internal sealed class PropitiationRequirement : BlueprintComponent, IAbilityRestriction
    {
        public bool IsAbilityRestrictionPassed(AbilityData ability) =>
            MediumInfluenceRules.HasSpirit(ability.Caster) && MediumInfluenceRules.Amount(ability.Caster) > 1;
        public string GetAbilityRestrictionUIText() => "Requires a channeled spirit and at least 2 accumulated influence / 需要已降灵且共鸣至少为 2";
    }
}

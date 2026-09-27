using System;
using System.Linq;
using BlueprintCore.Utils;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using MediumClass.Medium.NewActions;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;

namespace MediumClass.Medium
{
    internal static class MediumInfluenceRules
    {
        private static readonly string[] ResourceGuids = {
            Guids.MediumInfluenceResource, Guids.MediumInfluenceResourceArchmage,
            Guids.MediumInfluenceResourceChampion, Guids.MediumInfluenceResourceGuardian,
            Guids.MediumInfluenceResourceHierophant, Guids.MediumInfluenceResourceMarshal,
            Guids.MediumInfluenceResourceTrickster
        };
        internal static bool IsInfluence(BlueprintScriptableObject resource) => resource != null
            && ResourceGuids.Any(g => resource.AssetGuid == BlueprintGuid.Parse(g));
        internal static bool UsesInfluence(AbilityData ability) => ability != null
            && IsInfluence(ability.RequiredResource);
        internal static bool HasSpirit(UnitDescriptor owner) =>
            owner?.Unit.Get<UnitPartMedium>()?.ActiveSpiritClasses.Any() == true;

        private static UnitPartMediumInfluence State(UnitDescriptor owner)
        {
            var state = owner.Unit.Ensure<UnitPartMediumInfluence>();
            if (state.Version == 0)
            {
                var oldResource = owner.Resources.GetResource(BlueprintTool.Get<BlueprintAbilityResource>(Guids.MediumInfluenceResource));
                bool wasChannelled = HasSpirit(owner) || owner.HasFact(
                    BlueprintTool.Get<BlueprintBuff>(Guids.MediumChannelSpiritPrimarySpiritBuff));
                state.Accumulated = InfluenceMath.Migrate(oldResource?.Amount ?? InfluenceMath.BaseCap,
                    wasChannelled, owner.HasFact(BlueprintTool.Get<BlueprintFeature>(Guids.MediumPropitation)));
                state.Version = 1;
                Mirror(owner, state.Accumulated);
            }
            return state;
        }

        internal static int Amount(UnitDescriptor owner) => owner == null ? 0 : State(owner).Accumulated;
        internal static int Cap(UnitDescriptor owner) => InfluenceMath.BaseCap +
            (owner?.HasFact(BlueprintTool.Get<BlueprintFeature>(MythicInfluence.FeatureGuid)) == true
                ? InfluenceMath.MythicBonus(owner.Progression.MythicLevel) : 0) +
            (HardenedSoul.HasFeature(owner) ? 3 : 0);
        internal static bool CanAccept(UnitDescriptor owner, int cost) => owner != null
            && owner.Resources.GetResource(BlueprintTool.Get<BlueprintAbilityResource>(Guids.MediumInfluenceResource)) != null
            && InfluenceMath.CanAccept(Amount(owner), cost, Cap(owner));

        internal static bool TryAccept(UnitDescriptor owner, int cost)
        {
            if (!CanAccept(owner, cost)) return false;
            var state = State(owner);
            state.Accumulated += cost;
            Mirror(owner, state.Accumulated);
            RefreshPenalty(owner);
            return true;
        }

        internal static void Reset(UnitDescriptor owner)
        {
            if (owner == null) return;
            var state = owner.Unit.Ensure<UnitPartMediumInfluence>();
            state.Version = 1;
            state.Accumulated = 0;
            Mirror(owner, 0);
            owner.Buffs.RemoveFact(BlueprintTool.Get<BlueprintBuff>(Guids.MediumInfluenceDebuff));
        }

        internal static void Soothe(UnitDescriptor owner)
        {
            if (owner == null) return;
            var state = State(owner);
            state.Accumulated = InfluenceMath.Soothe(state.Accumulated, HasSpirit(owner));
            Mirror(owner, state.Accumulated);
            RefreshPenalty(owner);
        }

        internal static void RefreshPenalty(UnitDescriptor owner)
        {
            if (owner == null) return;
            var buff = BlueprintTool.Get<BlueprintBuff>(Guids.MediumInfluenceDebuff);
            // Rebuild only after actual state changes so added spirits' penalties join
            // the same buff, and dropping below 3 immediately clears old modifiers.
            owner.Buffs.RemoveFact(buff);
            if (HasSpirit(owner) && Amount(owner) >= InfluenceMath.PenaltyThreshold)
                owner.Buffs.AddBuff(buff, owner.Unit, new TimeSpan(24, 0, 0));
        }

        internal static void RestoreAfterLoad(UnitDescriptor owner)
        {
            if (owner?.Resources.GetResource(BlueprintTool.Get<BlueprintAbilityResource>(Guids.MediumInfluenceResource)) == null) return;
            Mirror(owner, Amount(owner));
            RefreshPenalty(owner);
        }

        internal static void SynchronizeExistingCounter(UnitDescriptor owner)
        {
            var state = owner?.Unit.Get<UnitPartMediumInfluence>();
            if (state?.Version == 1) Mirror(owner, state.Accumulated);
        }

        private static void Mirror(UnitDescriptor owner, int amount)
        {
            foreach (var blueprint in owner.Resources.Enumerable.Where(IsInfluence).ToArray())
            {
                var resource = owner.Resources.GetResource(blueprint);
                int old = resource.Amount;
                resource.Amount = amount;
                if (old != amount)
                    EventBus.RaiseEvent<IUnitAbilityResourceHandler>(h => h.HandleAbilityResourceChange(owner.Unit, resource, old));
            }
        }

        internal static bool IsFreeSurge(AbilityData ability) => ability != null
            && (ability.Blueprint.AssetGuid == BlueprintGuid.Parse(Guids.SpiritSurgeAbility)
                || ability.Blueprint.AssetGuid == BlueprintGuid.Parse(Guids.MarshalMarshalsOrdersAbility))
            && ability.Caster.Unit.Get<UnitPartMedium>()?.FreeSurgeAmount > 0;

        internal static int Cost(AbilityData ability, int original)
        {
            if (IsFreeSurge(ability)) return 0;
            if (ability.Blueprint.GetComponent<AbilityEffectRunAction>()?.Actions?.Actions
                ?.OfType<ContextActionApplySpirit>().Any() == true)
                return MultiSpiritRules.NextChannelCost(ability.Caster.Unit);
            return Math.Max(0, original);
        }
    }

    [HarmonyPatch(typeof(AbilityResourceLogic), nameof(AbilityResourceLogic.CalculateCost))]
    internal static class InfluenceAbilityCostPatch
    {
        private static void Postfix(AbilityResourceLogic __instance, AbilityData ability, ref int __result)
        {
            if (MediumInfluenceRules.IsInfluence(__instance.RequiredResource))
                __result = MediumInfluenceRules.Cost(ability, __result);
        }
    }

    [HarmonyPatch(typeof(AbilityResourceLogic), nameof(AbilityResourceLogic.Spend))]
    internal static class InfluenceAbilitySpendPatch
    {
        private static bool Prefix(AbilityResourceLogic __instance, AbilityData ability)
        {
            if (!MediumInfluenceRules.IsInfluence(__instance.RequiredResource)) return true;
            if (!__instance.IsSpendResource || ability?.Caster == null) return false;
            // Consume the free use here, before effects run, rather than first
            // charging influence and later refunding it from a buff callback.
            if (MediumInfluenceRules.IsFreeSurge(ability))
                ability.Caster.Unit.Get<UnitPartMedium>().FreeSurgeAmount--;
            else MediumInfluenceRules.TryAccept(ability.Caster, __instance.CalculateCost(ability));
            return false;
        }
    }

    [HarmonyPatch(typeof(UnitAbilityResourceCollection), nameof(UnitAbilityResourceCollection.HasEnoughResource))]
    internal static class InfluenceEnoughPatch
    {
        private static bool Prefix(BlueprintScriptableObject __0, int __1, UnitDescriptor ___m_Owner, ref bool __result)
        {
            if (!MediumInfluenceRules.IsInfluence(__0)) return true;
            __result = MediumInfluenceRules.CanAccept(___m_Owner, __1);
            return false;
        }
    }

    [HarmonyPatch(typeof(UnitAbilityResourceCollection), nameof(UnitAbilityResourceCollection.Add))]
    internal static class InfluenceGrantPatch
    {
        private static void Postfix(BlueprintScriptableObject __0, UnitDescriptor ___m_Owner)
        {
            // Granting an Astral Beacon alias must not refill/clear the shared
            // counter, including during a level-up with spirits still present.
            if (MediumInfluenceRules.IsInfluence(__0))
                MediumInfluenceRules.SynchronizeExistingCounter(___m_Owner);
        }
    }

    [HarmonyPatch(typeof(UnitAbilityResourceCollection), nameof(UnitAbilityResourceCollection.Spend))]
    internal static class InfluenceSpendPatch
    {
        private static bool Prefix(BlueprintScriptableObject __0, int __1, UnitDescriptor ___m_Owner)
        {
            if (!MediumInfluenceRules.IsInfluence(__0)) return true;
            MediumInfluenceRules.TryAccept(___m_Owner, __1);
            return false;
        }
    }

    [HarmonyPatch(typeof(UnitAbilityResourceCollection), nameof(UnitAbilityResourceCollection.Restore),
        new[] { typeof(BlueprintScriptableObject), typeof(int), typeof(bool) })]
    internal static class InfluenceRestorePatch
    {
        private static bool Prefix(BlueprintScriptableObject __0, int __1, bool __2, UnitDescriptor ___m_Owner)
        {
            if (!MediumInfluenceRules.IsInfluence(__0)) return true;
            if (__0.AssetGuid != BlueprintGuid.Parse(Guids.MediumInfluenceResource)) return false;
            if (__2)
            {
                MediumInfluenceRules.Reset(___m_Owner);
                ___m_Owner.Unit.Get<UnitPartMedium>()?.ResetDailySurges();
            }
            else for (int i = 0; i < __1; i++) MediumInfluenceRules.Soothe(___m_Owner);
            return false;
        }
    }

    [HarmonyPatch(typeof(UnitAbilityResourceCollection), nameof(UnitAbilityResourceCollection.HasMaxAmount))]
    internal static class InfluenceRestedPatch
    {
        private static bool Prefix(BlueprintScriptableObject __0, UnitDescriptor ___m_Owner, ref bool __result)
        {
            if (!MediumInfluenceRules.IsInfluence(__0)) return true;
            __result = MediumInfluenceRules.Amount(___m_Owner) == 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(BlueprintAbilityResource), nameof(BlueprintAbilityResource.GetMaxAmount))]
    internal static class InfluenceCapPatch
    {
        private static bool Prefix(BlueprintAbilityResource __instance, UnitDescriptor __0, ref int __result)
        {
            if (!MediumInfluenceRules.IsInfluence(__instance)) return true;
            __result = MediumInfluenceRules.Cap(__0);
            return false;
        }
    }

    [HarmonyPatch(typeof(AbilityCastRateUtils), "GetAvailableCastsCountFromResources")]
    internal static class InfluenceCastAvailabilityPatch
    {
        private static bool Prefix(AbilityData ability, ref int __result)
        {
            if (!MediumInfluenceRules.UsesInfluence(ability)) return true;
            int cost = ability.ResourceLogic?.CalculateCost(ability) ?? 1;
            __result = cost <= 0 ? -1 : Math.Max(0,
                (MediumInfluenceRules.Cap(ability.Caster) - MediumInfluenceRules.Amount(ability.Caster)) / cost);
            return false;
        }
    }

    [HarmonyPatch(typeof(MechanicActionBarSlotAbility), nameof(MechanicActionBarSlotAbility.GetResource))]
    internal static class InfluenceActionBarCounterPatch
    {
        private static bool Prefix(MechanicActionBarSlotAbility __instance, ref int __result)
        {
            if (!MediumInfluenceRules.UsesInfluence(__instance.Ability)) return true;
            __result = MediumInfluenceRules.Amount(__instance.Ability.Caster);
            return false;
        }
    }

    [HarmonyPatch(typeof(MechanicActionBarSlot), nameof(MechanicActionBarSlot.GetCountText))]
    internal static class InfluenceActionBarLabelPatch
    {
        private static bool Prefix(MechanicActionBarSlot __instance, ref string __result)
        {
            var ability = (__instance as MechanicActionBarSlotAbility)?.Ability
                ?? (__instance as MechanicActionBarSlotSpell)?.Spell;
            if (!MediumInfluenceRules.UsesInfluence(ability)) return true;
            __result = MediumInfluenceRules.Amount(ability.Caster) + "/" + MediumInfluenceRules.Cap(ability.Caster);
            return false;
        }
    }

    [HarmonyPatch(typeof(UnitDescriptor), nameof(UnitDescriptor.ApplyPostLoadFixes))]
    internal static class InfluenceLoadPatch
    {
        private static void Postfix(UnitDescriptor __instance) => MediumInfluenceRules.RestoreAfterLoad(__instance);
    }
}




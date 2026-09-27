using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.Localization;
using Kingmaker.ResourceLinks;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.Visual.Animation.Kingmaker.Actions;
using MediumClass.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TabletopTweaks.Core.Utilities;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Prowler
{
    /// <summary>
    /// 绝境巡行者需要的“手工蓝图”工具：必须手工组装的蓝图（化形用的克隆、隐藏包装特性、
    /// 由部件拼装的 buff/能力）走这里，创建后统一登记并 OnEnable。
    /// 移植自 alpha 的 Bp 辅助，只保留合并后仍然需要的部分。
    /// </summary>
    internal static class ProwlerBlueprints
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(ProwlerBlueprints));
        private static readonly List<SimpleBlueprint> Created = new List<SimpleBlueprint>();
        private static readonly HashSet<BlueprintGuid> CreatedIds = new HashSet<BlueprintGuid>();

        /// <summary>动态蓝图（血统延期包装等）沿用 alpha 的派生键，保证同一内容 GUID 不变。</summary>
        internal static BlueprintGuid DeriveGuid(string key)
        {
            using (var hash = SHA256.Create())
            {
                string hex = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("ProwlerWorldsEnd.v1/" + key)))
                    .Replace("-", string.Empty)
                    .Substring(0, 32)
                    .ToLowerInvariant();
                return BlueprintGuid.Parse(hex);
            }
        }

        internal static BlueprintGuid Guid(string key) => DeriveGuid(key);

        internal static T Get<T>(string guid) where T : BlueprintScriptableObject
        {
            var blueprint = ResourcesLibrary.TryGetBlueprint<T>(BlueprintGuid.Parse(guid));
            return blueprint ?? throw new InvalidOperationException($"Missing blueprint {typeof(T).Name} {guid}");
        }

        internal static LocalizedString Text(string key) => new LocalizedString { m_Key = key };

        private static void Register(SimpleBlueprint blueprint)
        {
            if (!CreatedIds.Add(blueprint.AssetGuid))
            {
                throw new InvalidOperationException("Duplicate Prowler blueprint: " + blueprint.name);
            }
            Created.Add(blueprint);
            ResourcesLibrary.BlueprintsCache.AddCachedBlueprint(blueprint.AssetGuid, blueprint);
        }

        internal static T New<T>(string name, string guid) where T : SimpleBlueprint, new()
        {
            var blueprint = new T { name = "Prowler_" + name, AssetGuid = BlueprintGuid.Parse(guid) };
            Register(blueprint);
            if (blueprint is BlueprintScriptableObject scriptable)
            {
                scriptable.ComponentsArray = Array.Empty<BlueprintComponent>();
            }
            return blueprint;
        }

        internal static T New<T>(string key) where T : SimpleBlueprint, new() => New<T>(key, DeriveGuid(key).ToString());

        internal static T Clone<T>(T source, string name, string guid) where T : SimpleBlueprint
        {
            var blueprint = Helpers.CreateCopy(source);
            blueprint.AssetGuid = BlueprintGuid.Parse(guid);
            blueprint.name = "Prowler_" + name;
            Register(blueprint);
            if (blueprint is BlueprintScriptableObject scriptable)
            {
                foreach (var component in scriptable.ComponentsArray) { component.OwnerBlueprint = scriptable; }
            }
            return blueprint;
        }

        internal static T Clone<T>(T source, string key) where T : SimpleBlueprint => Clone(source, key, DeriveGuid(key).ToString());

        internal static T Add<T>(BlueprintScriptableObject blueprint, T component) where T : BlueprintComponent
        {
            component.name = "$Prowler$" + component.GetType().Name + "$" + blueprint.ComponentsArray.Length;
            component.OwnerBlueprint = blueprint;
            blueprint.ComponentsArray = blueprint.ComponentsArray.Concat(new BlueprintComponent[] { component }).ToArray();
            return component;
        }

        internal static LevelEntry Level(int level, params BlueprintFeatureBase[] features) => new LevelEntry
        {
            Level = level,
            m_Features = features.Select(f => f.ToReference<BlueprintFeatureBaseReference>()).ToList()
        };

        internal static bool IsProwler(UnitDescriptor unit)
        {
            if (unit == null || ProwlerArchetypeBuilder.Archetype == null) return false;
            var bloodrager = BlueprintTool.Get<BlueprintCharacterClass>(ProwlerContent.Bloodrager);
            return unit.Progression.GetClassData(bloodrager)?.Archetypes.Contains(ProwlerArchetypeBuilder.Archetype) == true;
        }

        internal static BlueprintFeature CreateFeature(string name, string guid, string displayKey, string descriptionKey,
            bool hideInUi = false)
        {
            var feature = New<BlueprintFeature>(name, guid);
            feature.m_DisplayName = Text(displayKey);
            feature.m_Description = Text(descriptionKey);
            feature.IsClassFeature = true;
            feature.Ranks = 1;
            feature.Groups = Array.Empty<FeatureGroup>();
            feature.HideInUI = hideInUi;
            feature.HideInCharacterSheetAndLevelUp = hideInUi;
            return feature;
        }

        internal static BlueprintBuff CreateBuff(string name, string guid, string displayKey, string descriptionKey)
        {
            var buff = New<BlueprintBuff>(name, guid);
            buff.m_DisplayName = Text(displayKey);
            buff.m_Description = Text(descriptionKey);
            buff.Stacking = StackingType.Replace;
            buff.Frequency = DurationRate.Rounds;
            buff.FxOnStart = new PrefabLink();
            buff.FxOnRemove = new PrefabLink();
            buff.IsClassFeature = true;
            return buff;
        }

        internal static BlueprintBuff Buff(string key, string displayKey, string descriptionKey) =>
            CreateBuff(key, DeriveGuid(key).ToString(), displayKey, descriptionKey);

        internal static BlueprintAbility CreateAbility(string name, string guid, string displayKey, string descriptionKey,
            GameAction action, UnitCommand.CommandType command = UnitCommand.CommandType.Standard)
        {
            var ability = New<BlueprintAbility>(name, guid);
            ability.m_DisplayName = Text(displayKey);
            ability.m_Description = Text(descriptionKey);
            ability.LocalizedDuration = Text(ProwlerStrings.AbilityDuration);
            ability.LocalizedSavingThrow = Text(ProwlerStrings.AbilitySavingThrow);
            ability.Type = AbilityType.Supernatural;
            ability.ActionType = command;
            ability.Range = AbilityRange.Personal;
            ability.CanTargetSelf = true;
            ability.CanTargetFriends = true;
            ability.Animation = UnitAnimationActionCastSpell.CastAnimationStyle.Immediate;
            ability.m_Icon = Get<BlueprintAbility>(ProwlerContent.SharedActionIcon).Icon;
            if (action != null)
            {
                action.name = "$ProwlerAction$" + name;
                ability.AddToElementsList(action);
                Add(ability, new AbilityEffectRunAction { Actions = new ActionList { Actions = new[] { action } } });
            }
            return ability;
        }

        internal static void Give(BlueprintScriptableObject fact, params BlueprintUnitFact[] abilities)
        {
            Add(fact, new AddFacts
            {
                m_Facts = abilities.Select(f => f.ToReference<BlueprintUnitFactReference>()).ToArray()
            });
        }

        /// <summary>手工蓝图创建完成后统一初始化，与 alpha 一致。</summary>
        internal static void CompleteCreation()
        {
            foreach (var blueprint in Created) { blueprint.OnEnable(); }
            Logger.Log($"Initialized {Created.Count} manually built Prowler blueprints.");
        }
    }
}

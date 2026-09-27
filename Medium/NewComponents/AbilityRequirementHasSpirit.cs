using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Abilities.Components;
using MediumClass.Medium.NewActions;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Medium.Spirits;
using MediumClass.Utilities;
using MediumClass.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Medium.NewComponents
{
    [TypeId("60be564b13e54e3cb0ca08e50bbcf2e3")]
    public class AbilityRequirementHasSpirit : BlueprintComponent, IAbilityRestriction
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(AbilityRequirementHasSpirit));
        public string GetAbilityRestrictionUIText()
        {
            return !Not
                ? LocalizationText.Get("Requires a Spirit Channeled", "需要先降灵一位英灵。")
                : LocalizationText.Get("Requires an unchanneled spirit; additional spirits require Mythic Multiple Spirits.", "只能选择尚未降灵的英灵；追加英灵需要神话多重降灵。");

        }

        public bool IsAbilityRestrictionPassed(AbilityData ability)
        {
            if (!Not && MultiSpiritRules.IsTranceOfActiveSpirit(ability)) return false;
            var channel = ability.Blueprint.GetComponent<AbilityEffectRunAction>()?.Actions?.Actions?
                .OfType<ContextActionApplySpirit>().FirstOrDefault();
            if (Not && channel != null)
                return MultiSpiritRules.CanChannel(ability.Caster.Unit, channel.Spirit);
            return ((ability.Caster.HasFact(BlueprintTool.Get<BlueprintUnitFact>(Guids.MediumChannelSpiritPrimarySpiritBuff)) && !Not) ||
                (!ability.Caster.HasFact(BlueprintTool.Get<BlueprintUnitFact>(Guids.MediumChannelSpiritPrimarySpiritBuff)) && Not));
        }
        //[SerializeField]
        public bool Not = false;
    }
}

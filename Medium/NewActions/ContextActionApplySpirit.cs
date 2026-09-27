using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics.Actions;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;
using MediumClass.Utils;
using Owlcat.Runtime.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UnityModManagerNet.UnityModManager.ModEntry;
using MediumClass.NewComponents;

namespace MediumClass.Medium.NewActions
{
    [TypeId("4113da50-006a-40d5-b100-11450fe90159")]
    public class ContextActionApplySpirit : ContextAction
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(ContextActionApplySpirit));
        public override string GetCaption()
        {
            return string.Format("Sets the primary or secondary spirit.");
        }

        public override void RunAction() 
        {
            UnitEntityData maybeCaster = base.Context.MaybeCaster;
            if (maybeCaster == null)
            {
                LogChannel.Default.Error(this, "ContextActionApplySpirit: target is null", Array.Empty<object>());
                return;
            }
            UnitPartMedium unitPartMedium = maybeCaster.Get<UnitPartMedium>();
            if (unitPartMedium != null && MultiSpiritRules.CanChannel(maybeCaster, Spirit))
            {
                // Retire a temporary Trance grant before adding the permanent
                // channel grant, otherwise Trance expiry can remove its power.
                MultiSpiritRules.RemoveTrance(maybeCaster, Spirit);
                if (unitPartMedium.ActiveSpiritClasses.Any())
                {
                    var slots = MediumSpiritSpellbookRules.CaptureChannelSlots(maybeCaster.Descriptor);
                    if (unitPartMedium.AdditionalSpirits == null)
                        unitPartMedium.AdditionalSpirits = new List<BlueprintCharacterClassReference>();
                    unitPartMedium.AdditionalSpirits.Add(Spirit);
                    var channel = maybeCaster.Buffs.Enumerable.FirstOrDefault(b => b.Blueprint ==
                        BlueprintTool.Get<BlueprintBuff>(Guids.MediumChannelSpiritPrimarySpiritBuff));
                    channel?.CallComponents<ApplySpirits>(c => c.RefreshActiveSpirits());
                    MediumSpiritSpellbookRules.RefreshForAdditionalSpirit(maybeCaster.Descriptor, slots);
                    // Refresh these two dependent buffs without ending the channel session.
                    foreach (var dependent in maybeCaster.Buffs.Enumerable.Where(b => b.Blueprint ==
                        BlueprintTool.Get<BlueprintBuff>(Guids.MediumSpiritBonusBuff) || b.Blueprint ==
                        BlueprintTool.Get<BlueprintBuff>(Guids.MediumSharedSeanceBuff)).ToArray())
                        dependent.Reapply();
                    unitPartMedium.HandleInfluencePenalty();
                    return;
                }
                unitPartMedium.PrimarySpirit = Spirit;
                BlueprintBuff buff = BlueprintTool.Get<BlueprintBuff>(Guids.MediumChannelSpiritPrimarySpiritBuff);
                maybeCaster.Buffs.AddBuff(buff, base.Context, null);
                // The Medium's spontaneous book owns the daily casts. Recompute its slots
                // after the new spirit has been selected and its six-level table is active.
                MediumSpiritSpellbookRules.RefreshForChannel(maybeCaster.Descriptor);
            }
        }

        public BlueprintCharacterClassReference Spirit;
 
    }
}

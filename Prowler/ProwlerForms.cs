using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using System;
using System.Linq;

namespace MediumClass.Prowler
{
    /// <summary>
    /// 绝境巡行者 11 级“精魂之选”化形系统：
    /// 采用原版“野兽之状IV（剑齿虎）”作为唯一猫科形态，通过开关能力控制。
    /// 开启开关后，在进入血怒时自动化身为剑齿虎形态，并失去大血狂暴/强力血狂暴的瞬发法术 buff。
    /// 12 级获得“化形施法”，可在化形形态下正常施法。
    /// </summary>
    internal static class ProwlerForms
    {
        internal static BlueprintBuff Mode { get; private set; }
        internal static BlueprintBuff Controller { get; private set; }
        internal static BlueprintBuff SmilodonBuff { get; private set; }
        internal static BlueprintFeature ShapeFeature { get; private set; }
        internal static BlueprintFeature CastingFeature { get; private set; }

        internal static bool Shaped(UnitEntityData owner) =>
            owner != null && SmilodonBuff != null && owner.HasFact(SmilodonBuff);

        internal static void Build()
        {
            // 1. 化形开关 Buff 与控制器 Buff
            Mode = ProwlerBlueprints.Buff("ShapeMode", ProwlerStrings.ShapeModeName, ProwlerStrings.ShapeModeDescription);
            Controller = ProwlerBlueprints.Buff("RageFormController", ProwlerStrings.ChosenName, ProwlerStrings.ChosenDescription);
            Controller.m_Flags = BlueprintBuff.Flags.HiddenInUi;
            ProwlerBlueprints.Add(Controller, new RageFormLifecycle());

            // 2. 野兽之状IV（剑齿虎）化形 Buff：克隆自原版野兽之状IV剑齿虎
            SmilodonBuff = ProwlerBlueprints.Clone(
                ProwlerBlueprints.Get<BlueprintBuff>(ProwlerContent.PolymorphLargeCat),
                "FormSmilodon",
                ProwlerGuids.SmilodonForm);
            SmilodonBuff.m_DisplayName = ProwlerBlueprints.Text(ProwlerStrings.ChosenName);
            SmilodonBuff.m_Description = ProwlerBlueprints.Text(ProwlerStrings.FormDescription);
            SmilodonBuff.m_Flags &= ~BlueprintBuff.Flags.IsFromSpell;

            // 3. 11 级特性“精魂之选”本体：当进入血怒时触发控制器 Buff，并给予开关能力
            ShapeFeature = ProwlerBlueprints.CreateFeature(
                "Chosen",
                ProwlerGuids.Chosen,
                ProwlerStrings.ChosenName,
                ProwlerStrings.ChosenDescription);

            ProwlerBlueprints.Add(ShapeFeature, new BuffExtraEffects
            {
                m_CheckedBuff = ProwlerBlueprints.Get<BlueprintBuff>(ProwlerContent.Rage).ToReference<BlueprintBuffReference>(),
                m_ExtraEffectBuff = Controller.ToReference<BlueprintBuffReference>(),
                m_CheckedBuffList = Array.Empty<BlueprintBuffReference>()
            });

            var toggle = ProwlerBlueprints.CreateAbility(
                "ToggleShape",
                ProwlerGuids.ToggleShape,
                ProwlerStrings.ToggleShapeName,
                ProwlerStrings.ToggleShapeDescription,
                new ShapeModeAction(),
                UnitCommand.CommandType.Free);
            ProwlerBlueprints.Add(toggle, new ShapeModeRestriction());
            ProwlerBlueprints.Give(ShapeFeature, toggle);

            // 4. 12 级特性“化形施法”本体：赋予在野兽形态下正常施法的机制特性（自然施法）
            CastingFeature = ProwlerBlueprints.CreateFeature(
                "ShapeCasting",
                ProwlerGuids.ShapeCasting,
                ProwlerStrings.ShapeCastingName,
                ProwlerStrings.ShapeCastingDescription);
            var naturalSource = ProwlerBlueprints.Get<BlueprintFeature>(ProwlerContent.NaturalSpellFeature);
            foreach (var comp in naturalSource.GetComponents<AddMechanicsFeature>())
            {
                ProwlerBlueprints.Add(CastingFeature, new AddMechanicsFeature { m_Feature = comp.m_Feature });
            }
        }
    }

    [TypeId("b6c5bc84217e4df2a7ff8f11d61eefac")]
    public class ShapeModeRestriction : BlueprintComponent, IAbilityCasterRestriction
    {
        public bool IsCasterRestrictionPassed(UnitEntityData unit) =>
            unit != null && !unit.HasFact(ProwlerBlueprints.Get<BlueprintBuff>(ProwlerContent.Rage));

        public string GetAbilityCasterRestrictionUIText() => "血怒进行中不能更换模式。";
    }

    [TypeId("b687a0260e91405f9581e0c8b7103da9")]
    public class ShapeModeAction : ContextAction
    {
        public override string GetCaption() => "切换下次血怒的模式";

        public override void RunAction()
        {
            var unit = Context.MaybeCaster;
            if (unit == null) return;
            var current = unit.Buffs.Enumerable.FirstOrDefault(b => b.Blueprint == ProwlerForms.Mode);
            if (current != null)
            {
                current.Remove();
            }
            else
            {
                unit.Buffs.AddBuff(ProwlerForms.Mode, Context, null)?.MakePermanent();
            }
        }
    }

    [TypeId("fb6e3135eeb64bedbf2f341c5b5e6087")]
    public class RageFormLifecycle : UnitBuffComponentDelegate
    {
        public override void OnTurnOn()
        {
            if (Owner != null && Owner.HasFact(ProwlerForms.Mode) && !ProwlerForms.Shaped(Owner))
            {
                var child = Owner.Buffs.AddBuff(ProwlerForms.SmilodonBuff, Context, null);
                if (child != null)
                {
                    child.MakePermanent();
                    Buff.StoreFact(child);
                }
            }
        }

        public override void OnTurnOff()
        {
            if (Owner != null && ProwlerForms.SmilodonBuff != null)
            {
                Owner.Buffs.RemoveFact(ProwlerForms.SmilodonBuff);
            }
        }
    }

    /// <summary>
    /// 当处于剑齿虎形态时，压制大血狂暴（Greater Bloodrage）与强力血狂暴（Mighty Bloodrage）附带的自动瞬发法术。
    /// </summary>
    [HarmonyPatch(typeof(AutoMetamagic), nameof(AutoMetamagic.ShouldApplyTo))]
    internal static class ShapeQuickenPatch
    {
        private static bool Prefix(AutoMetamagic c, ref bool __result)
        {
            if (c == null || !ProwlerForms.Shaped(c.Owner)) return true;
            var guid = c.Fact.Blueprint.AssetGuid.ToString();
            if (guid != ProwlerContent.QuickenedRage && guid != ProwlerContent.QuickenedTirelessRage) return true;
            __result = false;
            return false;
        }
    }
}

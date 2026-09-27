extern alias CecilStandalone;
using Cecil = CecilStandalone::Mono.Cecil;
using Cil = CecilStandalone::Mono.Cecil.Cil;

// Conversion objects are not instantiated here because the real constructors
// require Unity entities. Inspect the actual native precedence and compiled
// production setter/call sites, then exercise that precedence in a narrow model.
internal static class LegendaryConversionChecks
{
    internal static void NativePrecedence(Cecil.AssemblyDefinition native, Action<bool,string> check)
    {
        var type=native.MainModule.GetType("Kingmaker.UnitLogic.Abilities.AbilityData");
        var getter=type.Methods.Single(m=>m.Name=="get_SpellLevel");
        var body=getter.Body.Instructions;
        int CallIndex(string name)=>body.ToList().FindIndex(i=>i.Operand is Cecil.MethodReference m&&m.Name==name);
        check(CallIndex("get_OverrideSpellLevel")>=0
            && CallIndex("get_OverrideSpellLevel")<CallIndex("get_ConvertedFrom")
            && CallIndex("get_ConvertedFrom")<CallIndex("get_SpellList"),
            "native spell-level resolution prioritizes explicit override and then converted menu");
        check(body.Any(i=>i.Operand is Cecil.MethodReference m&&m.Name=="get_CharacterLevel")
            && body.Any(i=>i.OpCode==Cil.OpCodes.Div),"untyped menu falls back to half character level");
        check(type.Methods.Any(m=>m.Name=="set_OverrideSpellLevel"&&m.Parameters.Count==1
            &&m.Parameters[0].ParameterType.FullName=="System.Nullable`1<System.Int32>"),
            "actual native OverrideSpellLevel setter signature");
    }

    internal static void ProductionWiring(string path,Action<bool,string> check)
    {
        using var mod=Cecil.AssemblyDefinition.ReadAssembly(path);
        foreach(var parentName in new[]{"MediumClass.Medium.NewUnitParts.UnitPartArchmage","MediumClass.Medium.NewComponents.UnitPartHierophant"})
        {
            var parent=mod.MainModule.GetType(parentName);
            var type=parent.NestedTypes.Single(t=>t.Name=="SpiritAbilityData");
            var ctor=type.Methods.Single(m=>m.IsConstructor&&m.Parameters.Count==3);
            check(ctor.Parameters[2].ParameterType.FullName=="System.Int32",parent.Name+" conversion takes actual list circle");
            var body=ctor.Body.Instructions;
            var setter=body.Single(i=>i.Operand is Cecil.MethodReference m&&m.Name=="set_OverrideSpellLevel");
            check(setter.Previous?.Operand is Cecil.MethodReference nullableCtor&&nullableCtor.Name==".ctor"
                &&nullableCtor.DeclaringType.FullName=="System.Nullable`1<System.Int32>"
                &&setter.Previous.Previous?.OpCode==Cil.OpCodes.Ldarg_3,
                parent.Name+" compiled conversion passes actual list circle to native override");
            var handler=parent.Methods.Single(m=>m.Name=="HandleGetConversions");
            var calls=handler.Body.Instructions.Where(i=>i.OpCode==Cil.OpCodes.Newobj
                &&i.Operand is Cecil.MethodReference m&&m.DeclaringType.FullName==type.FullName).ToArray();
            check(calls.Length==(parent.Name=="UnitPartArchmage"?12:1),parent.Name+" all plain and variant conversion paths covered");
            foreach(var call in calls)
            {
                var target=(Cecil.MethodReference)call.Operand;
                check(target.Parameters.Count==3&&target.Parameters[2].ParameterType.FullName=="System.Int32",
                    parent.Name+" conversion call cannot omit selected circle");
                var instruction=call.Previous;
                check(instruction.OpCode.Code is Cil.Code.Ldloc or Cil.Code.Ldloc_S
                    or Cil.Code.Ldloc_0 or Cil.Code.Ldloc_1 or Cil.Code.Ldloc_2 or Cil.Code.Ldloc_3,
                    parent.Name+" conversion gets circle from local loop/selected group");
            }
        }
    }

    internal static void LevelScenarios(Action<bool,string> check)
    {
        foreach(var characterLevel in new[]{13,16,17,18,19,20,40})
        foreach(var selectedCircle in Enumerable.Range(1,9))
        {
            var menu=new NativePrecedenceModel{CharacterLevel=characterLevel};
            var converted=new NativePrecedenceModel{CharacterLevel=characterLevel,ConvertedFrom=menu,OverrideSpellLevel=selectedCircle};
            check(converted.SpellLevel==selectedCircle,"conversion circle must be independent of character/menu level");
            check(10+converted.SpellLevel+5==15+selectedCircle,"converted spell DC retains selected circle");
            if(selectedCircle!=menu.SpellLevel)
            {
                converted.OverrideSpellLevel=null;
                check(converted.SpellLevel!=selectedCircle,"negative control reproduces missing-override bug");
            }
        }
    }

    private sealed class NativePrecedenceModel
    {
        internal int CharacterLevel;
        internal int? OverrideSpellLevel;
        internal NativePrecedenceModel ConvertedFrom;
        internal int SpellLevel=>OverrideSpellLevel??ConvertedFrom?.SpellLevel??Math.Min(9,CharacterLevel/2);
    }
}

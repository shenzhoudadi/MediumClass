extern alias NativeCecil;
using System.Reflection;
using BlueprintCore.Utils;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using MediumClass.Medium;
using MediumClass.Medium.NewActions;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;
using NativeCecil::Mono.Cecil;


int checks=0;
void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
void Equal<T>(T expected,T actual,string why)=>Check(Equals(expected,actual),$"{why}: expected {expected}, actual {actual}");
// Verify patch target arity, argument types/names and injected field against the installed game.
using(var game=AssemblyDefinition.ReadAssembly(args[0])) {
    foreach(var patch in typeof(MediumInfluenceRules).Assembly.GetTypes().Where(t=>t.GetCustomAttributes<HarmonyPatch>().Any())){
        var hp=patch.GetCustomAttributes<HarmonyPatch>().Select(p=>p.info).ToArray();
        var targetType=hp.Select(p=>p.declaringType).FirstOrDefault(t=>t!=null);
        var targetName=hp.Select(p=>p.methodName).FirstOrDefault(n=>n!=null);
        var targetArgs=hp.Select(p=>p.argumentTypes).FirstOrDefault(a=>a!=null);
        var native=game.MainModule.Types.Single(t=>t.FullName==targetType.FullName);
        var candidates=native.Methods.Where(m=>m.Name==targetName).ToArray();
        if(targetArgs!=null)candidates=candidates.Where(m=>m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(targetArgs.Select(t=>t.FullName))).ToArray();
        Equal(1,candidates.Length,patch.Name+" unique native target");
        var method=candidates.Single();
        foreach(var replacement in patch.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Where(m=>m.Name=="Prefix"||m.Name=="Postfix"))
        foreach(var p in replacement.GetParameters()){
            if(p.Name=="__instance"||p.Name=="__result")continue;
            if(p.Name.StartsWith("___")) {Check(native.Fields.Any(f=>f.Name==p.Name.Substring(3)&&f.FieldType.FullName==p.ParameterType.FullName),patch.Name+" field "+p.Name);continue;}
            int index;
            var q=p.Name.StartsWith("__")&&int.TryParse(p.Name.Substring(2),out index)?method.Parameters[index]:method.Parameters.Single(n=>n.Name==p.Name);
            Equal(p.ParameterType.FullName,q.ParameterType.FullName,patch.Name+" argument "+p.Name);
        }
    }
}
for(int rank=1;rank<=10;rank++)Equal(rank<3?1:rank<6?2:rank<9?3:4,InfluenceMath.MythicBonus(rank),"mythic rank "+rank);
for(int cap=5;cap<=9;cap++)for(int n=0;n<=cap+1;n++)for(int cost=0;cost<=6;cost++)
    Equal(cost==0||n+cost<=cap,InfluenceMath.CanAccept(n,cost,cap),"capacity boundary");
Equal(0,InfluenceMath.Migrate(0,false,true),"unchanneled old save");
Equal(3,InfluenceMath.Migrate(2,true,false),"legacy remaining 2");
Equal(3,InfluenceMath.Migrate(3,true,true),"legacy propitiation remaining 3");

foreach(var field in typeof(Guids).GetFields(BindingFlags.Public|BindingFlags.Static)){
    string id=(string)field.GetValue(null);
    if(field.Name.Contains("Resource"))BlueprintTool.Register(id,new BlueprintAbilityResource());
    else if(field.Name.EndsWith("Buff") || field.Name.EndsWith("Debuff"))BlueprintTool.Register(id,new BlueprintBuff());
    else if(field.Name.EndsWith("Ability"))BlueprintTool.Register(id,new BlueprintScriptableObject());
    else BlueprintTool.Register(id,new BlueprintFeature());
}
BlueprintTool.Register(MythicInfluence.FeatureGuid,new BlueprintFeature());
BlueprintTool.Register(HardenedSoul.FeatureGuid,new BlueprintFeature());
var common=BlueprintTool.Get<BlueprintAbilityResource>(Guids.MediumInfluenceResource);
var alias=BlueprintTool.Get<BlueprintAbilityResource>(Guids.MediumInfluenceResourceArchmage);
var penalty=BlueprintTool.Get<BlueprintBuff>(Guids.MediumInfluenceDebuff);
var surgeBp=BlueprintTool.Get<BlueprintScriptableObject>(Guids.SpiritSurgeAbility);
var harmony=new Harmony("MediumClass.tests.influence");
harmony.PatchAll(typeof(MediumInfluenceRules).Assembly);
Console.WriteLine("Installed production patches on explicit API doubles with Harmony "+typeof(Harmony).Assembly.GetName().Version);
UnitDescriptor Fresh(){var u=new UnitDescriptor();u.Resources.Add(common,false);u.Unit.Ensure<UnitPartMedium>();return u;}
AbilityData Ability(UnitDescriptor u,BlueprintScriptableObject bp=null,int cost=1)=>new(){Caster=u,Blueprint=bp??new(){AssetGuid=BlueprintGuid.Parse(Guid.NewGuid().ToString())},ResourceLogic=new(){RequiredResource=common,Amount=cost}};
var hero=Fresh();
Equal(0,MediumInfluenceRules.Amount(hero),"fresh starts zero");
var channelBp=new BlueprintScriptableObject{AssetGuid=BlueprintGuid.Parse(Guid.NewGuid().ToString())};
channelBp.Components[typeof(AbilityEffectRunAction)]=new AbilityEffectRunAction{Actions=new(){Actions=new object[]{new ContextActionApplySpirit()}}};
var channel=Ability(hero,channelBp);
var convertedSlot=new MechanicActionBarSlotSpell{Spell=channel};
Equal("0/5",convertedSlot.GetCountText(0),"converted spell counterlabel");
Equal(5,convertedSlot.GetResource(),"converted spell retains availability atzerocounter");
for(int nth=1;nth<=3;nth++){
    Equal(Math.Max(1,nth-1),channel.ResourceLogic.CalculateCost(channel),"channel dynamic cost");
    channel.ResourceLogic.Spend(channel);
    hero.Unit.Get<UnitPartMedium>().ActiveSpiritClasses.Add("spirit"+nth);
}
Equal(4,MediumInfluenceRules.Amount(hero),"first+second+third = 1+1+2");
Equal(3,channel.ResourceLogic.CalculateCost(channel),"fourth +3");
Equal(0,AbilityCastRateUtils.GetAvailableCastsCountFromResources(channel),"cannot fit fourth at cap5");
Check(hero.Buffs.Active.Contains(penalty),"penalty active at4");
var paid=Ability(hero);paid.ResourceLogic.Spend(paid);
Equal(5,MediumInfluenceRules.Amount(hero),"paid adds to cap");
Check(!hero.Resources.HasEnoughResource(common,1),"no paid casts at cap");
var surge=Ability(hero,surgeBp);hero.Unit.Get<UnitPartMedium>().FreeSurgeAmount=2;
Equal(0,surge.ResourceLogic.CalculateCost(surge),"free surge cost0");
Equal(-1,AbilityCastRateUtils.GetAvailableCastsCountFromResources(surge),"free surge available at cap");
surge.ResourceLogic.Spend(surge);
Equal(1,hero.Unit.Get<UnitPartMedium>().FreeSurgeAmount,"consume one free use");
Equal(5,MediumInfluenceRules.Amount(hero),"free surge never changes influence");
var orders=Ability(hero,BlueprintTool.Get<BlueprintScriptableObject>(Guids.MarshalMarshalsOrdersAbility));
Equal(0,orders.ResourceLogic.CalculateCost(orders),"Marshal Orders shares last free surge");
orders.ResourceLogic.Spend(orders);
Equal(0,hero.Unit.Get<UnitPartMedium>().FreeSurgeAmount,"Orders consumes shared free allowance");
Equal(5,MediumInfluenceRules.Amount(hero),"free Orders usable at influence cap");
Equal(1,orders.ResourceLogic.CalculateCost(orders),"Orders costs influence after allowance");
hero.Unit.Get<UnitPartMedium>().FreeSurgeAmount=2;
var legendary=Ability(hero,BlueprintTool.Get<BlueprintScriptableObject>(Guids.MarshalLegendaryMarshalAbility));
Check(!MediumInfluenceRules.IsFreeSurge(legendary),"Legendary Marshal never consumes daily free allowance");
var slot=new MechanicActionBarSlotAbility{Ability=paid};
Equal(5,slot.GetResource(),"action bar current points");Equal("5/5",slot.GetCountText(5),"action bar cap label");
hero.Facts.Add(BlueprintTool.Get<BlueprintFeature>(MythicInfluence.FeatureGuid));hero.Progression.MythicLevel=9;
Equal(9,common.GetMaxAmount(hero),"mythic cap native resource API");
Equal(5,MediumInfluenceRules.Amount(hero),"mythic cap doesn't refill counter");
hero.Resources.Add(alias,true);
Equal(5,hero.Resources.GetResource(alias).Amount,"aliasgrant mirrors without reset");
hero.Resources.Restore(alias,0,true);
Equal(5,MediumInfluenceRules.Amount(hero),"aliasrestore doesn't reset");
hero.Resources.Spend(alias,1);
Equal(6,MediumInfluenceRules.Amount(hero),"secondary cost uses shared counter");
for(int i=0;i<4;i++)MediumInfluenceRules.Soothe(hero);
Equal(2,MediumInfluenceRules.Amount(hero),"soothe decrement");Check(!hero.Buffs.Active.Contains(penalty),"penalty clears below3");
for(int i=0;i<5;i++)MediumInfluenceRules.Soothe(hero);
Equal(1,MediumInfluenceRules.Amount(hero),"soothe floor1 while active");
hero.Resources.Restore(common,0,true);
Equal(0,MediumInfluenceRules.Amount(hero),"rest resets zero");Check(hero.Resources.HasMaxAmount(common),"rested resource predicate uses zero");
var daily=hero.Unit.Get<UnitPartMedium>();
hero.Facts.Add(BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpiritMastery));
daily.FreeSurgeAmount=0;
hero.Resources.Restore(common,0,true);
Equal(2,daily.FreeSurgeAmount,"rest restores Spirit Mastery without weaker spirit");
orders.ResourceLogic.Spend(orders);
Equal(1,daily.FreeSurgeAmount,"spending after rest decrements daily pool");
hero.ApplyPostLoadFixes();
Equal(1,daily.FreeSurgeAmount,"loading does not refill daily pool");
hero.Resources.Restore(alias,0,true);
Equal(1,daily.FreeSurgeAmount,"alias restoration does not refill daily pool");
MediumInfluenceRules.Reset(hero);
Equal(1,daily.FreeSurgeAmount,"generic channel counter reset does not refill daily pool");
daily.ForgonePowers=2;daily.FreeSurgeAmount=0;
hero.Resources.Restore(common,0,true);
Equal(10,daily.FreeSurgeAmount,"full restoration with weaker spirit still active");
daily.ForgonePowers=0;daily.ResetDailySurges();
Equal(2,daily.FreeSurgeAmount,"weaker spirit rest cleanup leaves mastery allowance");
daily.FreeSurgeAmount=0;hero.Resources.Restore(common,0,true);
Equal(2,daily.FreeSurgeAmount,"cleanup before full restoration also leaves mastery allowance");
var legacy=Fresh();legacy.Unit.Get<UnitPartMedium>().ActiveSpiritClasses.Add("old");legacy.Resources.GetResource(common).Amount=2;
legacy.ApplyPostLoadFixes();Equal(3,MediumInfluenceRules.Amount(legacy),"runtime migration");
legacy.ApplyPostLoadFixes();Equal(3,MediumInfluenceRules.Amount(legacy),"migration idempotent");
var loading=Fresh();loading.Resources.GetResource(common).Amount=2;
loading.Buffs.Active.Add(BlueprintTool.Get<BlueprintBuff>(Guids.MediumChannelSpiritPrimarySpiritBuff));
loading.ApplyPostLoadFixes();Equal(3,MediumInfluenceRules.Amount(loading),"legacy channel buff preserves migration beforecatalog");
loading.Unit.Get<UnitPartMedium>().ActiveSpiritClasses.Add("restored");
loading.ApplyPostLoadFixes();Equal(3,MediumInfluenceRules.Amount(loading),"late catalog doesn't reinvert");
Check(loading.Buffs.Active.Contains(penalty),"late catalogue refreshes penalty");var other=Fresh();Equal(0,MediumInfluenceRules.Amount(other),"another hero isolated");
var unrelated=BlueprintTool.Register(Guid.NewGuid().ToString(),new BlueprintAbilityResource());other.Resources.Add(unrelated,true);other.Resources.Spend(unrelated,2);
Equal(3,other.Resources.GetResource(unrelated).Amount,"unrelated resources unchanged");
var hardened=Fresh();
Equal(0,MediumInfluenceRules.Amount(hardened),"hardened hero starts zero");
hardened.Unit.Get<UnitPartMedium>().ActiveSpiritClasses.Add("guardian");
MediumInfluenceRules.TryAccept(hardened,3);
Equal(3,MediumInfluenceRules.Amount(hardened),"pre-capstone points");
hardened.Facts.Add(BlueprintTool.Get<BlueprintFeature>(HardenedSoul.FeatureGuid));
Equal(8,MediumInfluenceRules.Cap(hardened),"hardened soul +3 basecap");
Equal(3,MediumInfluenceRules.Amount(hardened),"hardened soul doesn't reset points");
Check(hardened.Buffs.Active.Contains(penalty),"hardened soul retains penaltyat3");
hardened.Facts.Add(BlueprintTool.Get<BlueprintFeature>(MythicInfluence.FeatureGuid));
hardened.Progression.MythicLevel=9;
Equal(12,MediumInfluenceRules.Cap(hardened),"hardened+mythiccap12");
Equal(3,MediumInfluenceRules.Amount(hardened),"combinedcapdoesn'tresetpoints");
MediumInfluenceRules.TryAccept(hardened,9);
Equal(12,MediumInfluenceRules.Amount(hardened),"combinedcapacityusable");
Check(!MediumInfluenceRules.TryAccept(hardened,1),"combinedcapacityhardlimit");
// Enumerate every possible pair, checking two independent d8 calls and the
// triangular 2d8 distribution rather than merely testing a 2..16 range.
var frequencies=new Dictionary<int,int>();
for(int first=1;first<=8;first++)for(int second=1;second<=8;second++){
    var rolls=new Queue<int>(new[]{first,second});int calls=0;
    int sum=SpiritSurgeMath.Roll(20,false,true,false,sides=>{Equal(8,sides,"hardeneddie sides");calls++;return rolls.Dequeue();});
    Equal(2,calls,"hardenedrollstwice");Equal(first+second,sum,"hardened sum");
    frequencies[sum]=frequencies.TryGetValue(sum,out var freq)?freq+1:1;
}
Equal(1,frequencies[2],"2d8 minimum frequency");Equal(8,frequencies[9],"2d8 mode frequency");Equal(1,frequencies[16],"2d8 maximum frequency");
Equal(6,SpiritSurgeMath.Roll(20,false,true,true,s=>s),"legendarymarshal fixed d6 priority");
Equal(6,SpiritSurgeMath.Roll(20,true,false,false,s=>s),"prowler fixed d6");
Equal(16,SpiritSurgeMath.Roll(20,true,true,false,s=>s),"explicit capstone takes priority over prowler");
Equal(10,SpiritSurgeMath.Roll(20,false,false,false,s=>s),"ordinary lv20 d10");
Equal(8,SpiritSurgeMath.Roll(10,false,false,false,s=>s),"ordinary lv10 d8");
Equal(6,SpiritSurgeMath.Roll(1,false,false,false,s=>s),"ordinary lv1 d6");Console.WriteLine($"PASS: {checks} checks; production counter, capacity/migration, native patch contracts and real Harmony installation against explicit API doubles (no Unity gameplay).");







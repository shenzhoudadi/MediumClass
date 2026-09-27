using System.IO.Compression;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using MediumClass.Medium.NewComponents.AbilitySpecific;
using Mono.Cecil;
using Newtonsoft.Json.Linq;

try {
int checks=0;
void Check(bool condition,string message){checks++;if(!condition){Console.Error.WriteLine("FAIL: "+message);throw new Exception(message);}}
void Equal(int expected,int actual,string message)=>Check(expected==actual,$"{message}: expected {expected}, got {actual}");
if(args.Length!=2)throw new ArgumentException("Pass Assembly-CSharp.dll and game blueprints.zip paths.");
using(var game=AssemblyDefinition.ReadAssembly(args[0])){
    TypeDefinition Type(string name)=>game.MainModule.Types.Single(t=>t.FullName==name);
    Check(Type("Kingmaker.RuleSystem.RulebookEvent").Methods.Any(m=>m.Name=="SetCustomData"&&m.Parameters.Count==2),"native rule customdata setter");
    Check(Type("Kingmaker.RuleSystem.RulebookEvent").Methods.Any(m=>m.Name=="TryGetCustomData"&&m.Parameters.Count==2),"native rule customdata getter");
    Check(Type("Kingmaker.RuleSystem.Rules.Damage.BaseDamage").Methods.Any(m=>m.Name=="AddModifier"&&m.Parameters.Count==2&&m.Parameters[0].ParameterType.FullName=="System.Int32"&&m.Parameters[1].ParameterType.FullName=="Kingmaker.EntitySystem.EntityFact"),"native AddModifier(int,EntityFact)");
    Check(Type("Kingmaker.RuleSystem.Rules.Damage.BaseDamage").Methods.Any(m=>m.Name=="get_TotalBonus"),"native total flat bonus");
    Check(Type("Kingmaker.RuleSystem.Rules.Damage.IDamageBundleReadonly").Methods.Any(m=>m.Name=="get_Weapon"),"native bundle weapon");
    Check(Type("Kingmaker.RuleSystem.Rules.Damage.RuleDealDamage").Methods.Any(m=>m.Name=="get_SourceAbility"),"native explicit spell provenance");
    var calculate=Type("Kingmaker.RuleSystem.Rules.Damage.RuleCalculateDamage").Methods.Single(m=>m.Name=="CalculateDamageValue");
    Check(calculate.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="get_PreRolledValue"),"native pre-rolled branch exists");
    Check(calculate.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="get_Bonus"),"native regular bonus path exists");
}

// Read the shipped spell definitions rather than assume their damage structure.
using(var zip=ZipFile.OpenRead(args[1])){
    JObject Blueprint(string path){using var reader=new StreamReader(zip.GetEntry(path).Open());return JObject.Parse(reader.ReadToEnd());}
    var hell=Blueprint("Spells/Level6/HellfireRay.jbp");
    var actions=hell.SelectTokens("$..Actions").OfType<JArray>().SelectMany(a=>a.OfType<JObject>()).Where(a=>((string)a["$type"])?.EndsWith(", ContextActionDealDamage")==true).ToArray();
    Equal(2,actions.Length,"Hellfire two sequential damage actions per ray");
    Check((string)actions[0]["DamageType"]?["Energy"]=="Fire"&&(bool)actions[0]["Half"],"first half is fire");
    Check((bool)actions[0]["WriteRawResultToSharedValue"]&&!(bool)actions[0]["ReadPreRolledFromSharedValue"],"fire writes rolled raw result");
    Check((string)actions[1]["DamageType"]?["Energy"]=="Unholy"&&(bool)actions[1]["Half"]&&(bool)actions[1]["ReadPreRolledFromSharedValue"],"unholy half reuses raw result");
    Check(JToken.DeepEquals(actions[0]["ResultSharedValue"],actions[1]["PreRolledSharedValue"]),"Hellfire halves use same shared slot");
    var elemental=Blueprint("Spells/Level6/ElementalAssessor.jbp");
    var hit=elemental.SelectTokens("$..Actions").OfType<JArray>().SelectMany(a=>a.OfType<JObject>()).Single(a=>((string)a["$type"])?.EndsWith(", ContextActionDealDamage")==true);
    Equal(0,(int)hit["Value"]["DiceCountValue"]["Value"],"Elemental Assessor base has zero damage dice");
    Check((string)hit["Value"]["BonusValue"]["ValueType"]=="Shared","Elemental Assessor base damage is shared flat value");
    var riders=Blueprint("Spells/Level6/ElementalAssessorBuff.jbp")["Data"]["Components"].OfType<JObject>().Where(c=>((string)c["$type"])?.EndsWith(", AdditionalDiceOnAttack")==true).ToArray();
    Equal(3,riders.Length,"three additional elemental chunks");
    foreach(var rider in riders)Equal(0,(int)rider["Value"]["DiceCountValue"]["Value"],"elemental rider uses zero dice");
    foreach(string element in new[]{"Acid","Cold","Electricity","Fire"}){
        var tick=Blueprint("Spells/Level6/ElementalAssessor"+element+"Buff.jbp").SelectTokens("$..NewRound.Actions").OfType<JArray>().SelectMany(a=>a.OfType<JObject>()).Single();
        Equal(4,(int)tick["Value"]["DiceCountValue"]["Value"],"Elemental Assessor ongoing damage rolls 4d6");
    }
}

var component=new ArchmageSeanceDamageBonus();
BaseDamage Chunk(int rolls,int flat=0,bool precision=false)=>new(){Dice=new(){ModifiedValue=new(){Rolls=rolls}},Bonus=flat,Precision=precision};
RuleCalculateDamage Spell(params BaseDamage[] chunks){var parent=new RuleDealDamage{SourceAbility=new()};parent.DamageBundle.AddRange(chunks);return new(){ParentRule=parent};}
foreach(int dice in new[]{0,1,2,10,20}){
    var chunk=Chunk(dice,5);var evt=Spell(chunk);component.OnEventAboutToTrigger(evt);
    Equal(7,chunk.Bonus,"fixed +2 independent of dice count");
    Check(ReferenceEquals(component.Fact,chunk.Modifiers.Single().Source),"bonus records native fact source");
    component.OnEventAboutToTrigger(evt);component.OnEventDidTrigger(evt);
    Equal(7,chunk.Bonus,"duplicate event dispatch does not add again");
    new ArchmageSeanceDamageBonus().OnEventAboutToTrigger(evt);
    Equal(7,chunk.Bonus,"overlapping copies of seance boon do not stack within event");
}
var split=new[]{Chunk(0,7),Chunk(0,9),Chunk(0,6),Chunk(0,8)};
var splitEvent=Spell(split);splitEvent.ParentRule.DamageBundle.Weapon=new object();
component.OnEventAboutToTrigger(splitEvent);
Equal(32,split.Sum(d=>d.Bonus),"Elemental Assessor total +2, not +8 or +0");
Equal(9,split[0].Bonus,"only original first damage type carries bonus");
Equal(0,split.Skip(1).Sum(d=>d.Modifiers.Count),"other damage types unchanged");

// Native order: normal damage + Bonus, critical/empower, write raw; half;
// second action reads raw (ignoring regular Bonus), then halves that same raw.
int Raw(BaseDamage d,int rolled,int critical=1,double empower=1)=>d.PreRolledValue??(int)((rolled+d.Bonus)*critical*empower);
foreach(int critical in new[]{1,2})foreach(double empower in new[]{1.0,1.5})foreach(int rays in new[]{1,2,3}){
    int total=0,baseline=0;
    for(int ray=0;ray<rays;ray++){
        var fire=Chunk(15);var first=Spell(fire);first.ParentRule.DamageBundle.Weapon=new object();
        component.OnEventAboutToTrigger(first);
        int raw=Raw(fire,60,critical,empower);
        total+=raw/2;
        var unholy=Chunk(15);unholy.PreRolledValue=raw;var second=Spell(unholy);second.ParentRule.DamageBundle.Weapon=new object();
        component.OnEventAboutToTrigger(second);total+=Raw(unholy,999,critical,empower)/2;
        Equal(0,unholy.Modifiers.Count,"Hellfire copied raw receives no duplicate modifier");
        baseline+=((int)(60*critical*empower)/2)*2;
    }
    // For half damage each half rounds down independently, just like native.
    int expectedIncrease=((int)(62*critical*empower)/2*2-(int)(60*critical*empower)/2*2)*rays;
    Equal(expectedIncrease,total-baseline,"Hellfire split damage bonus survives exactly once per ray");
}
var areaFirst=Chunk(10);component.OnEventAboutToTrigger(Spell(areaFirst));int areaRaw=Raw(areaFirst,40);
for(int target=0;target<4;target++){var cached=Chunk(10);cached.PreRolledValue=areaRaw;component.OnEventAboutToTrigger(Spell(cached));Equal(42,Raw(cached,999),"later AoE target reuses already enhanced raw");Equal(0,cached.Modifiers.Count,"AoE cached raw not enhanced twice");}
for(int round=0;round<4;round++){var tick=Chunk(4);component.OnEventAboutToTrigger(Spell(tick));Equal(2,tick.Bonus,"each fresh ongoing damage tick gets fixed +2");}
var precision=Chunk(3,0,true);var ignored=Chunk(4);ignored.IgnoreModifiers=true;var zero=Chunk(0);var valid=Chunk(1);
component.OnEventAboutToTrigger(Spell(precision,ignored,zero,valid));
Equal(0,precision.Bonus,"no precision bonus");Equal(0,ignored.Bonus,"no forced modifier on ignored chunk");Equal(0,zero.Bonus,"empty chunk not made damaging");Equal(2,valid.Bonus,"first eligible damage chunk gets bonus");
var weapon=Spell(Chunk(1));weapon.ParentRule.SourceAbility=null;weapon.ParentRule.DamageBundle.Weapon=new object();weapon.Reason=new(){Context=new(){SourceAbility=new()}};
component.OnEventAboutToTrigger(weapon);Equal(0,weapon.DamageBundle[0].Bonus,"weapon attack with spell buff reason excluded");
var nonSpell=Spell(Chunk(1));nonSpell.ParentRule.SourceAbility.IsSpell=false;component.OnEventAboutToTrigger(nonSpell);Equal(0,nonSpell.DamageBundle[0].Bonus,"nonspell ability excluded");
var fallback=Spell(Chunk(1));fallback.ParentRule.SourceAbility=null;fallback.ParentRule.Reason=new(){Context=new(){SourceAbility=new()}};component.OnEventAboutToTrigger(fallback);Equal(2,fallback.DamageBundle[0].Bonus,"nonweapon spell-context fallback");
var noSource=Spell(Chunk(1));noSource.ParentRule.SourceAbility=null;component.OnEventAboutToTrigger(noSource);Equal(0,noSource.DamageBundle[0].Bonus,"sourceless damage excluded");
var otherBonus=Chunk(3);otherBonus.AddModifier(9,new object());component.OnEventAboutToTrigger(Spell(otherBonus));Equal(11,otherBonus.Bonus,"independent other source bonus preserved");
Console.WriteLine($"PASS: {checks} checks. Actual production component on explicit doubles; installed spell blueprints and native API verified. Not Unity gameplay.");
} catch(Exception error) {
    Console.Error.WriteLine(error.GetType().FullName);
    Console.Error.WriteLine(error.Message);
    Console.Error.WriteLine(error.StackTrace);
    Environment.ExitCode=1;
}

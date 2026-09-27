using System.Reflection;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using MediumClass.Medium.NewComponents.AbilitySpecific;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;

int checks=0;
void Equal(int want,int got,string why){checks++;if(want!=got){Console.Error.WriteLine($"FAIL: {why}: expected {want}, got {got}");throw new Exception(why);}}
var marshal=new BlueprintCharacterClassReference{Value=new()};
var champion=new BlueprintCharacterClassReference{Value=new()};
var trickster=new BlueprintCharacterClassReference{Value=new()};
BlueprintTool.Assets[Guids.Marshal+".ref"]=marshal;
var bonus=new BlueprintFeature();var bonusRef=new BlueprintFeatureReference{Value=bonus};
foreach(var id in new[]{Guids.MarshalMarshalsOrdersAbility,Guids.MarshalLegendaryMarshalAbility,Guids.SpiritSurgeAbility})BlueprintTool.Assets[id]=new BlueprintAbility();
BlueprintTool.Assets[Guids.MediumChannelSpirit]=new BlueprintFeature{Components=new object[]{new MediumSpiritComponent{Stats=(StatType[])Enum.GetValues(typeof(StatType))}}};
// Framework supports replacing this readonly field for tests. No random hooks are added to the mod.
typeof(MediumSpiritSurgeComponent).GetField("rnd",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new MaximumDie());
UnitEntityData Caster(bool prowler,bool multiple,bool hardened=false){
    var caster=new UnitEntityData();caster.Descriptor.Prowler=prowler;caster.Descriptor.Hardened=hardened;
    caster.Progression.Features.Ranks[bonus]=6;
    var part=caster.Ensure<UnitPartMedium>();
    part.Spirits[marshal]=new(){SpiritBonus=new(){Stats=new[]{StatType.AdditionalAttackBonus,StatType.SaveWill,StatType.SkillPersuasion},Concentration=true,SpiritBonusFeature=bonusRef}};
    part.Spirits[champion]=new(){SpiritBonus=new(){Stats=new[]{StatType.AdditionalAttackBonus,StatType.AdditionalDamage},SpiritBonusFeature=bonusRef}};
    part.Spirits[trickster]=new(){SpiritBonus=new(){Stats=new[]{StatType.Initiative,StatType.SkillPersuasion},SpiritBonusFeature=bonusRef}};
    part.ActiveSpiritClasses.Add(marshal);
    if(multiple){part.ActiveSpiritClasses.Add(champion);part.ActiveSpiritClasses.Add(trickster);}
    return caster;
}
MediumSpiritSurgeComponent Apply(UnitEntityData caster,UnitEntityData target,string ability){
    var component=new MediumSpiritSurgeComponent{Owner=target,Context=new(){MaybeCaster=caster,SourceAbility=BlueprintTool.Get<BlueprintAbility>(ability)}};
    component.OnTurnOn();return component;
}
foreach(bool prowler in new[]{false,true})foreach(bool multiple in new[]{false,true})foreach(bool hardened in new[]{false,true}){
    int die=hardened?16:prowler?6:10;
    var caster=Caster(prowler,multiple,hardened);
    var own=Apply(caster,caster,Guids.MarshalMarshalsOrdersAbility);
    Equal(die+6,caster.Stats.GetStat(StatType.AdditionalAttackBonus).Total,"self Orders includes Marshal bonus");
    Equal(die+6,own.GetStaticConcentrationBonus(new EntityFactComponent()),"self Orders concentration includes own bonus");
    Equal(multiple?die:0,caster.Stats.GetStat(StatType.AdditionalDamage).Total,"self Orders preserves Champion surge without Marshal bonus on damage");
    Equal(multiple?die:0,caster.Stats.GetStat(StatType.Initiative).Total,"self Orders preserves Trickster surge");
    own.OnTurnOff();foreach(StatType stat in Enum.GetValues(typeof(StatType)))Equal(0,caster.Stats.GetStat(stat).Total,"turnoff removes all own modifiers");
    var ally=new UnitEntityData();var allied=Apply(caster,ally,Guids.MarshalMarshalsOrdersAbility);
    Equal(die,ally.Stats.GetStat(StatType.AdditionalAttackBonus).Total,"ally Orders receives die only");
    Equal(die,allied.GetStaticConcentrationBonus(new EntityFactComponent()),"ally concentration receives die only");
    Equal(0,ally.Stats.GetStat(StatType.AdditionalDamage).Total,"ally never inherits Champion surge");
    Equal(0,ally.Stats.GetStat(StatType.Initiative).Total,"ally never inherits Trickster surge");
    allied.OnTurnOff();foreach(StatType stat in Enum.GetValues(typeof(StatType)))Equal(0,ally.Stats.GetStat(stat).Total,"turnoff removes ally modifiers");
    foreach(var target in new[]{caster,ally}){
        var legendary=Apply(caster,target,Guids.MarshalLegendaryMarshalAbility);
        Equal(6,target.Stats.GetStat(StatType.AdditionalAttackBonus).Total,"Legendary Marshal fixed d6 even with Hardened Soul");
        Equal(6,legendary.GetStaticConcentrationBonus(new EntityFactComponent()),"Legendary concentration fixed d6");
        Equal(0,target.Stats.GetStat(StatType.AdditionalDamage).Total,"Legendary does not inherit other spirits");
        legendary.OnTurnOff();
    }
    var ordinary=Apply(caster,caster,Guids.SpiritSurgeAbility);
    Equal(die+6,caster.Stats.GetStat(StatType.AdditionalAttackBonus).Total,"ordinary self surge unchanged");
    Equal(multiple?die:0,caster.Stats.GetStat(StatType.AdditionalDamage).Total,"ordinary multispirit surge union");
    ordinary.OnTurnOff();
}
Console.WriteLine($"PASS: {checks} real SpiritSurge component checks over explicit game API doubles; not Unity gameplay.");
sealed class MaximumDie:Random {public override int Next(int minValue,int maxValue)=>maxValue-1;}

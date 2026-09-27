// Deliberately narrow native API model. Executes actual linked production patches,
// but is not a Unity integration test or a rendering test.
using System.Runtime.CompilerServices;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
namespace Kingmaker.Blueprints { public readonly record struct BlueprintGuid(Guid Value) { public static BlueprintGuid Parse(string s)=>new(Guid.Parse(s)); } }
namespace Kingmaker.UnitLogic.Abilities.Blueprints { public class BlueprintAbility { public BlueprintGuid AssetGuid; } }
namespace Kingmaker.UnitLogic.Abilities
{
    public class AbilityData { public BlueprintAbility Blueprint; public Spellbook Spellbook; public int SpellLevel; public object MetamagicData; public bool Available; }
    public class Ability { public AbilityData Data; public bool Hidden; public void Hide()=>Hidden=true; }
    public class AbilityCollection { public readonly List<Ability> Entries=new(); public Ability GetAbility(BlueprintAbility bp)=>Entries.FirstOrDefault(a=>a.Data.Blueprint==bp); public IEnumerator<Ability> GetEnumerator()=>Entries.GetEnumerator(); }
}
namespace Kingmaker.UnitLogic
{
    public class Unit { public UnitDescriptor Descriptor; }
    public class UnitDescriptor
    {
        public Kingmaker.EntitySystem.Entities.UnitEntityData Unit; public AbilityCollection Abilities=new(); public List<Spellbook> Spellbooks=new();
        public UnitDescriptor() { Unit=new Kingmaker.EntitySystem.Entities.UnitEntityData{Descriptor=this}; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void ApplyPostLoadFixes() { }
    }
    public class SpellSlot { public AbilityData SpellShell; }
    public class Spellbook
    {
        public string Id; public List<AbilityData> Known=new(), Custom=new(), Special=new();
        public IEnumerable<AbilityData> GetKnownSpells(int l)=>Known.Where(s=>s.SpellLevel==l);
        public IEnumerable<AbilityData> GetCustomSpells(int l)=>Custom.Where(s=>s.SpellLevel==l);
        public IEnumerable<AbilityData> GetSpecialSpells(int l)=>Special.Where(s=>s.SpellLevel==l);
    }
}
namespace Kingmaker.EntitySystem.Entities { public class UnitEntityData : Kingmaker.UnitLogic.Unit { } }
namespace BlueprintCore.Utils
{
    public static class BlueprintTool { public static Dictionary<string,object> Assets=new(); public static T Get<T>(string id)=>(T)Assets[id]; }
}
namespace MediumClass.Utilities
{
    public static class Guids
    {
        public const string HierophantSupremeAbility2="b80ab52f-17fa-4c4c-874c-e7aa28b5903c";
        public const string HierophantSupremeAbility4="54aecd71-0160-4cfb-836f-555b0ab6ba7b";
    }
}
namespace MediumClass.Medium
{
    public static class MediumSpiritSpellbookRules
    {
        public static bool IsPreparationBook(Spellbook b)=>b?.Id is "archmage" or "hierophant";
        public static bool IsMediumBook(Spellbook b)=>b?.Id=="medium";
        public static Action<UnitDescriptor> BeforeActionBarFetch;
        public static void SyncForActionBar(UnitDescriptor owner)=>BeforeActionBarFetch?.Invoke(owner);
    }
}
namespace Kingmaker.UI.UnitSettings
{
    public class MechanicActionBarSlot { public Unit Unit; }
    public class MechanicActionBarSlotEmpty : MechanicActionBarSlot { }
    public class MechanicActionBarSlotSpell : MechanicActionBarSlot { public virtual AbilityData Spell {get;set;} }
    public class MechanicActionBarSlotSpontaneousSpell : MechanicActionBarSlotSpell { public MechanicActionBarSlotSpontaneousSpell(AbilityData s) {Spell=s;} }
    public class MechanicActionBarSlotMemorizedSpell : MechanicActionBarSlotSpell { public MechanicActionBarSlotMemorizedSpell(SpellSlot s) {Spell=s.SpellShell;} }
    public class MechanicActionBarSlotSpontaneusConvertedSpell : MechanicActionBarSlot { public AbilityData Spell; }
    public class MechanicActionBarSlotAbility : MechanicActionBarSlot { public AbilityData Ability; }
    public class UnitUISettings
    {
        public UnitDescriptor Owner; private MechanicActionBarSlot[] m_Slots=new MechanicActionBarSlot[10]; public MechanicActionBarSlot[] Raw=>m_Slots; public int Collected;
        [MethodImpl(MethodImplOptions.NoInlining)] public void SetSlot(MechanicActionBarSlot slot,int index)=>Raw[index]=slot;
        [MethodImpl(MethodImplOptions.NoInlining)] public MechanicActionBarSlot GetSlot(int index,Unit unit)
        {
            // Native cleanup discards hidden facts before returning the slot.
            // A prefix migration must therefore run before this point.
            if (IsBad(Raw[index]))
                Raw[index]=GetBadSlotReplacement(Raw[index],Owner)??new MechanicActionBarSlotEmpty();
            return Raw[index];
        }
        private bool IsBad(MechanicActionBarSlot slot)=>slot is MechanicActionBarSlotAbility a
            && Owner.Abilities.Entries.Any(x=>x.Data==a.Ability&&x.Hidden);
        [MethodImpl(MethodImplOptions.NoInlining)] private static MechanicActionBarSlot GetBadSlotReplacement(MechanicActionBarSlot slot,UnitDescriptor owner)=>null;
        [MethodImpl(MethodImplOptions.NoInlining)] public bool UpdateBadSlots()
        {
            var changed=false;
            for(var index=0;index<Raw.Length;index++)
                if(IsBad(Raw[index]))
                {
                    SetSlot(GetBadSlotReplacement(Raw[index],Owner)??new MechanicActionBarSlotEmpty(),index);
                    changed=true;
                }
            return changed;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public void CollectSpells(Spellbook book)=>Collected++;
        [MethodImpl(MethodImplOptions.NoInlining)] public void CollectSpells(Spellbook book,int level)=>Collected++;
    }
}
namespace Kingmaker.UI.MVVM._VM.ActionBar
{
    public static class ActionBarSpellbookHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public static List<AbilityData> Fetch(Kingmaker.EntitySystem.Entities.UnitEntityData unit)=>unit.Descriptor.Spellbooks.SelectMany(book=>book.Known).ToList();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void TryAddAbility(List<AbilityData> list,AbilityData item) {if (!list.Any(a=>a.Blueprint==item.Blueprint)) list.Add(item);}
        [MethodImpl(MethodImplOptions.NoInlining)] public static void TryAddSpell(List<SpellSlot> list,SpellSlot item) {list.Add(item);}
    }
}

using System.Collections.Generic;
using System.Linq;
using BlueprintCore.Utils;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Newtonsoft.Json;
using MediumClass.Utilities;

namespace MediumClass.Medium.NewUnitParts
{
    // The spirit books are preparation sheets. Only the Medium book casts their selected spells.
    public class UnitPartMediumPreparedSpells : UnitPart
    {
        [JsonProperty]
        private readonly List<AbilityData> grantedSpells = new List<AbilityData>();

        [JsonIgnore]
        private bool syncing;

        public void Sync() => Sync(true);

        // Fetch already rebuilds the spell menu. Its repair pass must not request
        // another refresh while that same menu is being collected.
        internal void Sync(bool notifyActionBar)
        {
            if (syncing) return;
            syncing = true;
            bool changed;
            try
            {
                changed = SyncPreparedSpells();
            }
            finally
            {
                syncing = false;
            }
            if (changed && notifyActionBar)
                MediumSpiritSpellbookRules.NotifyActionBar(Owner.Descriptor);
        }

        private bool SyncPreparedSpells()
        {
            var spiritBooks = MediumSpiritSpellbookRules.EnsurePreparationBooks(Owner.Descriptor);
            if (spiritBooks.Count == 0)
            {
                return ClearGrantedSpells();
            }
            var mediumBook = MediumSpiritSpellbookRules.MediumBook(Owner.Descriptor);
            int maxLevel = mediumBook.MaxSpellLevel;

            var prepared = spiritBooks.SelectMany(book => book.GetAllMemorizedSpells())
                .Where(slot => slot?.SpellShell?.Blueprint != null && slot.SpellLevel >= 0
                    && slot.SpellLevel <= maxLevel && slot.SpellLevel <= 6)
                .Select(slot => (Level: slot.SpellLevel, Spell: slot.SpellShell.Blueprint))
                .Distinct()
                .ToList();

            bool changed = false;
            foreach (var spell in grantedSpells.ToArray())
            {
                if (spell == null || !prepared.Any(p => p.Level == spell.SpellLevel && p.Spell == spell.Blueprint))
                {
                    changed |= Revoke(mediumBook, spell);
                    grantedSpells.Remove(spell);
                }
            }

            foreach (var selection in prepared)
            {
                if (mediumBook.IsKnownOnLevel(selection.Spell, selection.Level)) continue;
                var added = mediumBook.AddKnownTemporary(selection.Level, selection.Spell);
                if (added?.IsTemporary == true)
                {
                    grantedSpells.Add(added);
                    changed = true;
                }
            }
            return changed;
        }

        public void Clear()
        {
            if (syncing) return;
            syncing = true;
            try
            {
                // RemoveCustomSpell/SetDirty publish synchronous UI events. The
                // channel's primary spirit may still exist during OnDeactivate,
                // so a nested Fetch must not regrant the spells being dismissed.
                if (ClearGrantedSpells())
                    MediumSpiritSpellbookRules.NotifyActionBar(Owner.Descriptor);
            }
            finally
            {
                syncing = false;
            }
        }

        private bool ClearGrantedSpells()
        {
            bool changed = false;
            var mediumBook = Owner.Descriptor.GetSpellbook(BlueprintTool.Get<BlueprintSpellbook>(Guids.MediumSpellbook));
            if (mediumBook != null)
                foreach (var spell in grantedSpells)
                    changed |= Revoke(mediumBook, spell);
            grantedSpells.Clear();
            return changed;
        }

        private bool Revoke(Spellbook book, AbilityData spell)
        {
            if (spell?.IsTemporary != true) return false;
            // Snapshot before native removal, which may also remove an equal custom entry.
            var recipes = Enumerable.Range(0, 11).SelectMany(book.GetCustomSpells)
                .Where(recipe => recipe.MetamagicData != null
                    && (recipe.Blueprint == spell.Blueprint || recipe.Blueprint.Parent == spell.Blueprint)
                    && (recipe.SpellLevelInSpellbook ?? recipe.SpellLevel) == spell.SpellLevel).ToArray();
            book.RemoveTemporarySpell(spell);
            // Native removal only edits the known/custom lists; cached action-bar
            // entries retain their circle and would otherwise remain castable.
            Owner.UISettings?.RemoveSlot(spell);
            if (book.GetKnownSpells(spell.SpellLevel).Concat(book.GetSpecialSpells(spell.SpellLevel))
                .Any(known => known.Blueprint == spell.Blueprint
                    || spell.Blueprint.Parent != null && known.Blueprint == spell.Blueprint.Parent)) return true;
            // Native MetamagicBuilder does not copy IsTemporary/ConvertedFrom.
            // Revoke recipes made from this temporary base circle as well, marking
            // the cached objects before removal so the spending guard covers them.
            foreach (var recipe in recipes)
            {
                recipe.IsTemporary = true;
                book.RemoveCustomSpell(recipe);
                Owner.UISettings?.RemoveSlot(recipe);
            }
            return true;
        }

        // Loading synchronizes at UnitDescriptor.ApplyPostLoadFixes, after spellbooks,
        // spirit facts and unit parts have all been restored. Do not create books earlier.
    }
}

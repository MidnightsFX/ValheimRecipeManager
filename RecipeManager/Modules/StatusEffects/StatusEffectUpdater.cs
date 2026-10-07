using HarmonyLib;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;
using Object = UnityEngine.Object;

namespace RecipeManager
{
    /// <summary>
    /// Owns StatusEffects.yaml: adding new status effects, by copying one or from nothing, and changing ones that
    /// exist. The object DB holds one template of each effect, which the game copies whenever the effect is given
    /// to someone, so a change reaches every time the effect is given from then on.
    ///
    /// An effect this mod adds is made once per session and kept, so an item that points at it always points at a
    /// working object. Each new world's object DB starts from the game's own list, and the effects are put into it
    /// again. A copy starts from its source as it is at each apply, so it follows changes to the source. For an
    /// effect that exists, each field this mod changes is recorded before its first change and put back before each
    /// apply, so the config stays declarative while other mods' changes to the rest are left alone.
    /// </summary>
    internal static class StatusEffectUpdater
    {
        public static Dictionary<string, StatusEffectDefinition> Definitions = new Dictionary<string, StatusEffectDefinition>();

        /// <summary>The effects this mod made, by name, for the whole session.</summary>
        private static readonly Dictionary<string, StatusEffect> Created = new Dictionary<string, StatusEffect>();
        /// <summary>What each effect this mod made was copied from, empty for none.</summary>
        private static readonly Dictionary<string, string> CreatedFrom = new Dictionary<string, string>();
        /// <summary>The fields of each effect made from nothing as they were made, which every apply starts from.</summary>
        private static readonly Dictionary<string, FieldSnapshot> BlankBaselines = new Dictionary<string, FieldSnapshot>();
        /// <summary>The fields this mod has changed on each effect that exists, with what they were before, by name.</summary>
        private static readonly Dictionary<string, FieldSnapshot> Originals = new Dictionary<string, FieldSnapshot>();
        /// <summary>Icons loaded from the textures folder, read once per session.</summary>
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

        /// <summary>Health, stamina and eitr added while an effect lasts, by the effect's name hash, read by the stat bonus patch.</summary>
        internal static readonly Dictionary<int, Vector3> StatBonuses = new Dictionary<int, Vector3>();

        // Every field an entry can write. Fields a type does not have are skipped.
        private static readonly string[] WrittenFields =
        {
            "m_name", "m_tooltip", "m_category", "m_icon", "m_flashIcon", "m_cooldownIcon", "m_hidden", "m_attributes",
            "m_startMessageType", "m_startMessage", "m_stopMessageType", "m_stopMessage", "m_repeatMessageType", "m_repeatMessage",
            "m_repeatInterval", "m_ttl", "m_cooldown", "m_activationAnimation",
            "m_tickInterval", "m_healthPerTickMinHealthPercentage", "m_healthPerTick", "m_healthUpFront", "m_healthOverTime",
            "m_healthOverTimeDuration", "m_healthOverTimeInterval", "m_healthRegenMultiplier",
            "m_staminaUpFront", "m_staminaOverTime", "m_staminaOverTimeDuration", "m_staminaOverTimeIsFraction", "m_staminaDrainPerSec",
            "m_staminaRegenMultiplier", "m_runStaminaDrainModifier",
            "m_jumpStaminaUseModifier", "m_attackStaminaUseModifier", "m_blockStaminaUseModifier", "m_blockStaminaUseFlatValue",
            "m_dodgeStaminaUseModifier", "m_swimStaminaUseModifier", "m_homeItemStaminaUseModifier", "m_sneakStaminaUseModifier",
            "m_runStaminaUseModifier",
            "m_eitrUpFront", "m_eitrOverTime", "m_eitrOverTimeDuration", "m_eitrRegenMultiplier",
            "m_adrenalineUpFront", "m_adrenalineModifier", "m_staggerModifier", "m_timedBlockBonus",
            "m_addArmor", "m_armorMultiplier", "m_mods", "m_modifyAttackSkill", "m_damageModifier", "m_percentigeDamageModifiers",
            "m_raiseSkill", "m_raiseSkillModifier", "m_skillLevel", "m_skillLevelModifier", "m_skillLevel2", "m_skillLevelModifier2",
            "m_speedModifier", "m_swimSpeedModifier", "m_jumpModifier", "m_maxMaxFallSpeed", "m_fallDamageModifier",
            "m_windMovementModifier", "m_windRunStaminaModifier", "m_noiseModifier", "m_stealthModifier", "m_addMaxCarryWeight"
        };

        /// <summary>Hooked to every new object DB in a world, ahead of the items, which can point at these effects.</summary>
        public static void InitialSynchronization()
        {
            Reapply();
        }

        /// <summary>
        /// Puts back every effect this mod changed, takes out the ones it added, and applies the configuration. Safe to
        /// run again at any time, which also picks up effects other mods add late.
        /// </summary>
        public static void Reapply()
        {
            if (ObjectDB.instance == null) { return; }
            Revert();
            StatBonuses.Clear();
            Apply();
        }

        /// <summary>Puts every effect this mod changed back as it was, and takes the added ones out of the object DB.</summary>
        public static void Revert()
        {
            foreach (KeyValuePair<string, FieldSnapshot> original in Originals)
            {
                original.Value.WriteTo(FindEffect(original.Key));
            }
            if (ObjectDB.instance == null) { return; }
            foreach (KeyValuePair<string, StatusEffect> made in Created)
            {
                ObjectDB.instance.m_StatusEffects.Remove(made.Value);
            }
        }

        /// <summary>Whether this mod made the effect, rather than the game or another mod.</summary>
        internal static bool IsCreated(string name)
        {
            return !string.IsNullOrEmpty(name) && Created.ContainsKey(name);
        }

        /// <summary>
        /// A copy of an effect that exists, with the fields this mod changed put back as they were. Null when this mod has
        /// not changed it. The caller destroys the copy.
        /// </summary>
        internal static StatusEffect OriginalCopy(StatusEffect effect)
        {
            if (effect == null || !Originals.TryGetValue(effect.name, out FieldSnapshot original)) { return null; }
            StatusEffect copy = Object.Instantiate(effect);
            copy.name = effect.name;
            original.WriteTo(copy);
            return copy;
        }

        private static void Apply()
        {
            // Several entries for one effect are applied in order onto it, like items
            List<(string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries)> modifies = GroupByPrefab(StatusEffectAction.Modify);
            List<(string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries)> adds = GroupByPrefab(StatusEffectAction.Add);
            HashSet<string> added = new HashSet<string>(adds.Select(group => group.prefab));

            // Changes to effects that exist go first, so a copy of one starts from it as it is configured
            foreach ((string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries) in modifies)
            {
                TryApply(prefab, entries, () => ModifyEffect(prefab, entries, added.Contains(prefab)));
            }

            // An added effect can copy another added effect, so they are made in rounds until none is left that can be
            List<(string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries)> waiting = adds;
            bool progress = true;
            while (waiting.Count > 0 && progress)
            {
                progress = false;
                foreach ((string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries) in waiting.ToList())
                {
                    string clonedFrom = ClonedFrom(prefab, entries);
                    if (!string.IsNullOrEmpty(clonedFrom) && FindEffect(clonedFrom) == null) { continue; }
                    waiting.RemoveAll(group => group.prefab == prefab);
                    progress = true;
                    TryApply(prefab, entries, () => AddEffect(prefab, entries, clonedFrom));
                }
            }
            foreach ((string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries) in waiting)
            {
                Logger.LogWarning($"Status effect '{entries[0].Key}' copies {ClonedFrom(prefab, entries)}, which does not exist. {prefab} will not be added.");
            }
        }

        /// <summary>The valid entries of one action, grouped by the effect they name, in the order they were loaded.</summary>
        private static List<(string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries)> GroupByPrefab(StatusEffectAction action)
        {
            List<(string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries)> groups = new List<(string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries)>();
            foreach (KeyValuePair<string, StatusEffectDefinition> entry in Definitions)
            {
                if (entry.Value == null || entry.Value.action != action) { continue; }
                if (string.IsNullOrEmpty(entry.Value.prefab))
                {
                    Logger.LogError($"Status effect '{entry.Key}' has no prefab set, which is the name of the effect {(action == StatusEffectAction.Add ? "to add" : "to change")}. Skipping.");
                    continue;
                }
                int index = groups.FindIndex(group => group.prefab == entry.Value.prefab);
                if (index < 0) { groups.Add((entry.Value.prefab, new List<KeyValuePair<string, StatusEffectDefinition>> { entry })); }
                else { groups[index].entries.Add(entry); }
            }
            foreach ((string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries) in groups)
            {
                if (entries.Count > 1)
                {
                    Logger.LogDebug($"{prefab} is set by {entries.Count} entries, applied in this order: {string.Join(", ", entries.Select(entry => entry.Key))}.");
                }
            }
            return groups;
        }

        /// <summary>What the entries adding an effect copy. The last entry that names one decides, and disagreeing entries are reported.</summary>
        private static string ClonedFrom(string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries)
        {
            List<string> sources = entries.Select(entry => entry.Value.clonedFrom).Where(source => !string.IsNullOrEmpty(source)).Distinct().ToList();
            if (sources.Count > 1)
            {
                Logger.LogWarning($"The entries adding {prefab} copy different effects ({string.Join(", ", sources)}). It copies {sources[sources.Count - 1]}, the last of them.");
            }
            return sources.Count > 0 ? sources[sources.Count - 1] : null;
        }

        private static void TryApply(string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries, System.Action apply)
        {
            try { apply(); }
            catch (Exception ex) { Logger.LogError($"Status effect {prefab} ({string.Join(", ", entries.Select(entry => entry.Key))}) could not be applied. Error: {ex}"); }
        }

        private static void AddEffect(string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries, string clonedFrom)
        {
            string key = entries[0].Key;
            if (Created.TryGetValue(prefab, out StatusEffect effect) && effect != null && CreatedFrom[prefab] != (clonedFrom ?? string.Empty))
            {
                // Copying something else now. Items find effects by name, so they move to the new one on their next apply.
                Logger.LogInfo($"{prefab} now copies {(string.IsNullOrEmpty(clonedFrom) ? "nothing" : clonedFrom)}, making it again.");
                Created.Remove(prefab);
                BlankBaselines.Remove(prefab);
                effect = null;
            }
            if (effect == null)
            {
                if (FindEffect(prefab) != null)
                {
                    Logger.LogError($"Status effect '{key}' wants to add {prefab}, but an effect with that name already exists. Change it with 'action: Modify' instead. Skipping.");
                    return;
                }
                effect = Create(prefab, clonedFrom);
                Created[prefab] = effect;
                CreatedFrom[prefab] = clonedFrom ?? string.Empty;
            }
            ObjectDB.instance.m_StatusEffects.Add(effect);

            // Starts from what it copies as that is now, or from how it was made
            StatusEffect source = string.IsNullOrEmpty(clonedFrom) ? null : FindEffect(clonedFrom);
            FieldSnapshot baseline = source != null ? FieldSnapshot.Of(source, WrittenFields) : BlankBaselines[prefab];
            baseline.WriteTo(effect);
            effect.m_tooltip = StatusEffectAttributes.WithoutBonusLines(effect.m_tooltip);
            WriteEntries(prefab, effect, entries);
        }

        private static StatusEffect Create(string prefab, string clonedFrom)
        {
            StatusEffect source = string.IsNullOrEmpty(clonedFrom) ? null : FindEffect(clonedFrom);
            StatusEffect effect = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<SE_Stats>();
            effect.name = prefab;
            // The copy carries the source's cached name hash, which is what the game finds effects by
            AccessTools.Field(typeof(StatusEffect), "m_nameHash").SetValue(effect, 0);
            // Not unloaded between worlds, where nothing in the scene refers to it
            effect.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (source == null)
            {
                effect.m_name = prefab;
                BlankBaselines[prefab] = FieldSnapshot.Of(effect, WrittenFields);
            }
            Logger.LogInfo($"Created status effect {prefab}{(source != null ? $" from {source.name}" : string.Empty)}.");
            return effect;
        }

        private static void ModifyEffect(string prefab, List<KeyValuePair<string, StatusEffectDefinition>> entries, bool addedHere)
        {
            string key = entries[0].Key;
            if (addedHere)
            {
                Logger.LogError($"Status effect '{key}' modifies {prefab}, which is an effect this mod adds. Put the settings on its 'action: Add' entry instead. Skipping.");
                return;
            }
            StatusEffect effect = FindEffect(prefab);
            if (effect == null)
            {
                Logger.LogWarning($"No status effect named {prefab} exists, '{key}' will be skipped. Names are case sensitive, RM_StatusEffects_PrintAll lists the ones that exist.");
                return;
            }
            // Puts back what this mod changed before, then records whatever this apply changes for the first time
            if (!Originals.TryGetValue(prefab, out FieldSnapshot original))
            {
                original = new FieldSnapshot();
                Originals.Add(prefab, original);
            }
            original.WriteTo(effect);
            FieldSnapshot before = FieldSnapshot.Of(effect, WrittenFields);
            WriteEntries(prefab, effect, entries);
            original.RecordChanges(before, effect);
        }

        /// <summary>Writes each entry over the effect in order, then the health, stamina and eitr they add between them.</summary>
        private static void WriteEntries(string prefab, StatusEffect effect, List<KeyValuePair<string, StatusEffectDefinition>> entries)
        {
            Sprite icon = LoadIcon(prefab);
            float? health = null, stamina = null, eitr = null;
            foreach (KeyValuePair<string, StatusEffectDefinition> entry in entries)
            {
                StatusEffectAttributes.Apply(effect, entry.Value, icon);
                health = entry.Value.maxHealth ?? health;
                stamina = entry.Value.maxStamina ?? stamina;
                eitr = entry.Value.maxEitr ?? eitr;
            }
            if (health == null && stamina == null && eitr == null) { return; }
            StatBonuses[prefab.GetStableHashCode()] = new Vector3(health ?? 0f, stamina ?? 0f, eitr ?? 0f);
            StatusEffectAttributes.AddBonusLines(effect, health, stamina, eitr);
        }

        private static Sprite LoadIcon(string name)
        {
            if (Icons.TryGetValue(name, out Sprite icon)) { return icon; }
            Sprite[] loaded = TextureTools.LoadIcons(name, null);
            icon = loaded != null && loaded.Length > 0 ? loaded[0] : null;
            Icons[name] = icon;
            return icon;
        }

        /// <summary>An effect in the object DB by its name, case sensitive like the game.</summary>
        internal static StatusEffect FindEffect(string name)
        {
            if (ObjectDB.instance == null || ObjectDB.instance.m_StatusEffects == null || string.IsNullOrEmpty(name)) { return null; }
            return ObjectDB.instance.m_StatusEffects.Find(effect => effect != null && effect.name == name);
        }
    }
}

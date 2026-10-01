using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    /// <summary>
    /// Owns everything in Items.yaml: creating brand new items by cloning existing prefabs, and
    /// changing the stats of items that already exist.
    ///
    /// The two halves run at different points. Adding is not freely reversible, because a prefab's name
    /// is its network identity and items have to be registered before ObjectDB and ZNetScene are built,
    /// so adding or removing a definition needs a restart. Modifying an item that already exists only
    /// writes to its shared data and can be applied, re-applied and reverted at any time.
    /// </summary>
    internal class ItemUpdater
    {
        /// <summary>Every entry in the item config files, additions and modifications together.</summary>
        public static Dictionary<String, ItemAddition> ItemDefinitions = new Dictionary<String, ItemAddition>();
        public static List<TrackedItem> TrackedItems = new List<TrackedItem>();
        public static List<TrackedItem> ModifiedItems = new List<TrackedItem>();

        /// <summary>Prefab names this process has already registered, so re-runs refresh instead of re-clone.</summary>
        private static readonly HashSet<string> CreatedPrefabs = new HashSet<string>();

        /// <summary>Existing prefabs a modification is currently applied to, so they can be put back.</summary>
        private static readonly HashSet<string> ModifiedPrefabs = new HashSet<string>();

        private const string TokenPrefix = "rm_item_";

        /// <summary>
        /// Hooked to ItemManager.OnVanillaItemsAvailable. Safe to run repeatedly - it re-fires on every
        /// ObjectDB copy, and prefabs already created in this process are refreshed rather than cloned again.
        /// </summary>
        public static void CreateCustomItems()
        {
            if (ItemDefinitions.Count == 0) { return; }

            TrackedItems.Clear();
            foreach (KeyValuePair<String, ItemAddition> entry in ItemDefinitions)
            {
                if (entry.Value == null)
                {
                    Logger.LogError($"Item entry '{entry.Key}' is empty, it needs at least an action and a prefab. Skipping.");
                    continue;
                }
                // Modifications target items that exist already, they are handled once the object DB is built
                if (entry.Value.action == ItemAction.Modify) { continue; }
                try
                {
                    CreateCustomItem(entry.Key, entry.Value);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Item addition '{entry.Key}' failed and will be skipped. Error: {ex}");
                }
            }
            if (TrackedItems.Count > 0) { Logger.LogInfo($"Registered {TrackedItems.Count} custom item(s)."); }
        }

        private static void CreateCustomItem(string modificationName, ItemAddition def)
        {
            if (!ValidateDefinition(modificationName, def)) { return; }

            if (def.action == ItemAction.Remove)
            {
                if (CreatedPrefabs.Contains(def.prefab))
                {
                    Logger.LogWarning($"Item addition '{modificationName}' is set to Remove, but {def.prefab} was already registered this session. Restart the game to fully remove it.");
                }
                return;
            }

            TrackedItem tracked = new TrackedItem
            {
                action = def.action,
                prefab = def.prefab,
                clonedFrom = def.clonedFrom,
                definition = def
            };

            // Already created earlier in this process - this event re-fires on every ObjectDB copy.
            // The prefab itself lives in Jotunn's DontDestroyOnLoad container and still holds everything
            // we wrote to it, so only the tracking needs rebuilding. Re-applying here would allocate a
            // fresh set of materials and textures on every trip through the main menu.
            if (CreatedPrefabs.Contains(def.prefab))
            {
                GameObject existing = PrefabManager.Instance.GetPrefab(def.prefab);
                if (existing == null)
                {
                    Logger.LogWarning($"{def.prefab} was registered earlier but can no longer be resolved, skipping.");
                    return;
                }
                tracked.itemPrefab = existing;
                TrackedItems.Add(tracked);
                return;
            }

            if (PrefabManager.Instance.GetPrefab(def.prefab) != null)
            {
                Logger.LogError($"Item addition '{modificationName}' wants to create the prefab '{def.prefab}', but a prefab with that name already exists. Prefab names must be globally unique, and changing an item that already exists is done with 'action: Modify'. This addition will be skipped.");
                return;
            }

            GameObject clone = PrefabManager.Instance.CreateClonedPrefab(def.prefab, def.clonedFrom);
            if (clone == null)
            {
                Logger.LogError($"Item addition '{modificationName}' could not clone '{def.clonedFrom}'. Check that the prefab name is spelled correctly. This addition will be skipped.");
                return;
            }

            if (clone.GetComponent<ItemDrop>() == null)
            {
                Logger.LogError($"'{def.clonedFrom}' has no ItemDrop component, so it cannot be used as the source for an item. Item addition '{modificationName}' will be skipped.");
                return;
            }

            ApplyDefinition(clone, def);

            CustomItem customItem = new CustomItem(clone, fixReference: false);
            if (!customItem.IsValid())
            {
                Logger.LogError($"The item created by '{modificationName}' is not valid and will be skipped.");
                return;
            }

            if (!ItemManager.Instance.AddItem(customItem))
            {
                Logger.LogError($"Jotunn rejected the item created by '{modificationName}'. This addition will be skipped.");
                return;
            }

            CreatedPrefabs.Add(def.prefab);
            tracked.itemPrefab = clone;
            tracked.customItem = customItem;

            if (def.recipe != null)
            {
                CustomRecipe recipe = BuildRecipe(modificationName, def);
                if (recipe != null && !ItemManager.Instance.AddRecipe(recipe))
                {
                    Logger.LogWarning($"Could not register the recipe for '{modificationName}'. The item exists but will not be craftable.");
                }
            }

            TrackedItems.Add(tracked);
            Logger.LogInfo($"Created item {def.prefab} from {def.clonedFrom}.");
        }

        private static bool ValidateDefinition(string modificationName, ItemAddition def)
        {
            if (string.IsNullOrEmpty(def.prefab))
            {
                Logger.LogError($"Item addition '{modificationName}' has no 'prefab' set. This is the name of the new item and is required. Skipping.");
                return false;
            }
            if (def.action != ItemAction.Remove && string.IsNullOrEmpty(def.clonedFrom))
            {
                Logger.LogError($"Item addition '{modificationName}' has no 'clonedFrom' set. This is the existing prefab to copy and is required. If you meant to change an item that already exists, use 'action: Modify' instead. Skipping.");
                return false;
            }
            if (string.Equals(def.prefab, def.clonedFrom, StringComparison.Ordinal))
            {
                Logger.LogError($"Item addition '{modificationName}' has 'prefab' and 'clonedFrom' set to the same value ({def.prefab}). The new item needs its own unique name, and changing the original in place is done with 'action: Modify'. Skipping.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Writes every configurable field onto the prefab. Split out from creation so it can also be
        /// used to refresh an already registered item on reload.
        /// </summary>
        public static void ApplyDefinition(GameObject prefab, ItemAddition def)
        {
            ItemDrop.ItemData.SharedData shared = GetSharedData(prefab);
            if (shared == null)
            {
                Logger.LogWarning($"{def.prefab} has no usable ItemDrop data, nothing to apply.");
                return;
            }

            // Start from the values the clone was born with, so a field removed from the yaml goes back
            // to what the source item had instead of keeping whatever was applied last.
            ItemAttributes.CaptureOriginal(def.prefab, shared);
            ItemAttributes.RestoreOriginal(def.prefab, shared);

            ApplyStats(shared, def, statusEffectsAvailable: false);
            ApplyArt(prefab, def.prefab, warnWhenIconless: true);
        }

        /// <summary>
        /// Names, descriptions and stats. Kept apart from the art so that several definitions can be
        /// stacked onto one prefab without reloading its textures once for each of them.
        /// </summary>
        private static void ApplyStats(ItemDrop.ItemData.SharedData shared, ItemAddition def, bool statusEffectsAvailable)
        {
            if (def.action == ItemAction.Modify)
            {
                // An item that already exists keeps its own name and description unless one is given
                if (!string.IsNullOrEmpty(def.name)) { shared.m_name = ResolveToken(def.name, def.prefab, string.Empty); }
            }
            else
            {
                shared.m_name = ResolveNameToken(def);
            }

            string description = ResolveToken(def.description, def.prefab, "_desc");
            if (description != null) { shared.m_description = description; }

            ItemAttributes.Apply(shared, def, statusEffectsAvailable);
        }

        /// <summary>
        /// Icons and model textures, both read from the item's texture folder. Nothing happens when the
        /// folder does not exist, so this is a no-op for items with no custom art.
        /// </summary>
        private static void ApplyArt(GameObject prefab, string prefabName, bool warnWhenIconless)
        {
            ItemDrop.ItemData.SharedData shared = GetSharedData(prefab);
            if (shared == null) { return; }

            // Icons fall back to the cloned original when the user has not supplied any. An item with
            // no icons cannot be picked up by the player, so this must never end up empty.
            Sprite[] icons = TextureTools.LoadIcons(prefabName, shared.m_icons);
            if (icons != null && icons.Length > 0)
            {
                shared.m_icons = icons;
            }
            else if (warnWhenIconless)
            {
                Logger.LogWarning($"{prefabName} ended up with no icons and would not be pickupable. Check that the prefab it was cloned from has an icon.");
            }

            int textures = TextureTools.ApplyTextureOverrides(prefab, prefabName);
            if (textures > 0) { Logger.LogInfo($"Applied {textures} texture override(s) to {prefabName}."); }
        }

        /// <summary>
        /// Applies every 'Modify' entry to the items it targets, and puts back any item that was being
        /// modified but no longer is.
        ///
        /// Runs once the object DB exists, which is both when status effects can be looked up and when
        /// the items themselves can be found. Re-running is safe and expected: each item is restored to
        /// its original values before the configuration is written over the top.
        /// </summary>
        public static void ApplyItemModifications()
        {
            // Several entries, potentially from different config files, are allowed to target the same
            // item. They are grouped so the item is only reset once and then has each of them applied.
            Dictionary<string, List<ItemAddition>> byPrefab = GroupModificationsByPrefab();

            RevertPrefabs(Except(ModifiedPrefabs, byPrefab.Keys));
            ModifiedItems.Clear();

            int modified = 0;
            foreach (KeyValuePair<string, List<ItemAddition>> entry in byPrefab)
            {
                try
                {
                    if (ApplyModification(entry.Key, entry.Value)) { modified++; }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Could not modify {entry.Key}. Error: {ex}");
                }
            }

            if (modified > 0) { Logger.LogInfo($"Applied modifications to {modified} existing item(s)."); }
        }

        private static bool ApplyModification(string prefabName, List<ItemAddition> definitions)
        {
            GameObject prefab = ResolveItemPrefab(prefabName);
            if (prefab == null)
            {
                Logger.LogWarning($"No item named '{prefabName}' could be found, the modification targeting it will be skipped. Prefab names are case sensitive, run RM_Items_PrintAll to see the ones that exist.");
                return false;
            }

            ItemDrop.ItemData.SharedData shared = GetSharedData(prefab);
            if (shared == null)
            {
                Logger.LogWarning($"'{prefabName}' has no usable ItemDrop data, the modification targeting it will be skipped.");
                return false;
            }

            ItemAttributes.CaptureOriginal(prefabName, shared);
            ItemAttributes.RestoreOriginal(prefabName, shared);

            foreach (ItemAddition def in definitions)
            {
                ApplyStats(shared, def, statusEffectsAvailable: true);
                // Tracked per definition rather than per item, so that a status effect which is not
                // registered yet gets retried no matter which entry asked for it.
                ModifiedItems.Add(new TrackedItem
                {
                    action = ItemAction.Modify,
                    prefab = prefabName,
                    itemPrefab = prefab,
                    definition = def
                });
            }
            ApplyArt(prefab, prefabName, warnWhenIconless: false);

            ModifiedPrefabs.Add(prefabName);
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Modified {prefabName}."); }
            return true;
        }

        private static Dictionary<string, List<ItemAddition>> GroupModificationsByPrefab()
        {
            Dictionary<string, List<ItemAddition>> byPrefab = new Dictionary<string, List<ItemAddition>>();
            foreach (KeyValuePair<String, ItemAddition> entry in ItemDefinitions)
            {
                ItemAddition def = entry.Value;
                if (def == null || def.action != ItemAction.Modify) { continue; }
                if (string.IsNullOrEmpty(def.prefab))
                {
                    Logger.LogError($"Item modification '{entry.Key}' has no 'prefab' set. This is the item to change and is required. Skipping.");
                    continue;
                }
                // Modifying an item this mod created would reset it to the item it was cloned from
                // before applying, quietly throwing away the rest of its own definition.
                if (CreatedPrefabs.Contains(def.prefab))
                {
                    Logger.LogError($"Item modification '{entry.Key}' targets {def.prefab}, which is an item this mod adds. Put the stats on that 'action: Add' entry instead of modifying it separately. Skipping.");
                    continue;
                }

                List<ItemAddition> definitions;
                if (!byPrefab.TryGetValue(def.prefab, out definitions))
                {
                    definitions = new List<ItemAddition>();
                    byPrefab.Add(def.prefab, definitions);
                }
                definitions.Add(def);
            }
            return byPrefab;
        }

        /// <summary>
        /// Puts every item this mod has modified back to its original stats. Custom icons and textures
        /// are not undone, those are only released on a restart.
        /// </summary>
        public static void RevertItemModifications()
        {
            RevertPrefabs(new List<string>(ModifiedPrefabs));
            ModifiedItems.Clear();
        }

        private static void RevertPrefabs(List<string> prefabNames)
        {
            foreach (string prefabName in prefabNames)
            {
                ItemDrop.ItemData.SharedData shared = GetSharedData(ResolveItemPrefab(prefabName));
                if (shared == null)
                {
                    Logger.LogWarning($"{prefabName} can no longer be resolved, its original stats could not be restored.");
                }
                else if (ItemAttributes.RestoreOriginal(prefabName, shared) && ValConfig.EnableDebugMode.Value)
                {
                    Logger.LogInfo($"Restored the original stats of {prefabName}.");
                }
                ModifiedPrefabs.Remove(prefabName);
            }
        }

        private static List<string> Except(IEnumerable<string> source, ICollection<string> exclude)
        {
            List<string> remaining = new List<string>();
            foreach (string value in source)
            {
                if (!exclude.Contains(value)) { remaining.Add(value); }
            }
            return remaining;
        }

        /// <summary>
        /// Items live in the object DB, but a modification can also target something another mod
        /// registered through Jotunn, so both lookups are tried.
        /// </summary>
        private static GameObject ResolveItemPrefab(string prefabName)
        {
            GameObject prefab = null;
            if (ObjectDB.instance != null) { prefab = ObjectDB.instance.GetItemPrefab(prefabName); }
            if (prefab == null) { prefab = PrefabManager.Instance.GetPrefab(prefabName); }
            return prefab;
        }

        private static ItemDrop.ItemData.SharedData GetSharedData(GameObject prefab)
        {
            if (prefab == null) { return null; }
            return prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
        }

        /// <summary>
        /// Second pass for status effects that only become resolvable once the object DB is fully built.
        /// Hooked to ItemManager.OnItemsRegistered. Deliberately does no allocation, so it is safe to
        /// run on every object DB rebuild.
        /// </summary>
        public static void ResolveDeferredReferences()
        {
            ResolveDeferredReferences(TrackedItems);
            ResolveDeferredReferences(ModifiedItems);
        }

        private static void ResolveDeferredReferences(List<TrackedItem> tracked)
        {
            foreach (TrackedItem item in tracked)
            {
                if (item.definition == null || item.itemPrefab == null) { continue; }

                ItemDrop.ItemData.SharedData shared = GetSharedData(item.itemPrefab);
                if (shared == null) { continue; }

                ItemAttributes.ApplyStatusEffects(shared, item.definition, warnOnMissing: true);
            }
        }

        /// <summary>
        /// Resolves the item's display name to a localization token.
        ///
        /// This matters more than it looks: m_name is what the game keys food slots, known recipes and
        /// auto-stacking on. A clone that keeps the source item's token shares its food slot and merges
        /// with its dropped stacks, so the token always has to be unique.
        /// </summary>
        private static string ResolveNameToken(ItemAddition def)
        {
            if (string.IsNullOrEmpty(def.name))
            {
                Logger.LogWarning($"Item addition for {def.prefab} has no 'name' set. Falling back to the prefab name - set 'name' to give it a proper display name.");
                return ResolveToken(def.prefab, def.prefab, string.Empty);
            }
            return ResolveToken(def.name, def.prefab, string.Empty);
        }

        /// <summary>
        /// A value starting with '$' is treated as a token the user manages themselves. Anything else is
        /// registered as the English translation of a generated, per-prefab token.
        /// </summary>
        private static string ResolveToken(string configured, string prefab, string suffix)
        {
            if (string.IsNullOrEmpty(configured)) { return null; }
            if (configured.StartsWith("$", StringComparison.Ordinal)) { return configured; }

            string token = $"${TokenPrefix}{prefab.ToLowerInvariant()}{suffix}";
            LocalizationManager.Instance.GetLocalization().AddTranslation(token, configured);
            return token;
        }

        private static CustomRecipe BuildRecipe(string modificationName, ItemAddition def)
        {
            ItemRecipe source = def.recipe;
            List<RequirementConfig> requirements = new List<RequirementConfig>();
            // Kept in step with requirements, the requirements built from them come back in the same order
            List<Ingrediant> requirementSources = new List<Ingrediant>();
            foreach (Ingrediant ingredient in source.ingredients)
            {
                if (ingredient == null || string.IsNullOrEmpty(ingredient.prefab))
                {
                    Logger.LogWarning($"Recipe for '{modificationName}' has an ingredient with no prefab set, it will be ignored.");
                    continue;
                }
                RequirementConfig requirement = new RequirementConfig
                {
                    Item = ingredient.prefab,
                    Amount = ingredient.craftCost,
                    AmountPerLevel = ingredient.upgradeCost
                };
                // RecipeConfig drops a requirement that costs nothing, which would put the two lists out of step
                if (!requirement.IsValid()) { continue; }
                requirements.Add(requirement);
                requirementSources.Add(ingredient);
            }

            RecipeConfig config = new RecipeConfig
            {
                Name = $"Recipe_{def.prefab}",
                Item = def.prefab,
                Amount = source.craftAmount,
                CraftingStation = source.craftedAt,
                RepairStation = string.IsNullOrEmpty(source.repairAt) ? source.craftedAt : source.repairAt,
                MinStationLevel = source.minStationLevel,
                RequireOnlyOneIngredient = source.anyOneResource,
                Enabled = true,
                Requirements = requirements.ToArray()
            };

            CustomRecipe recipe = new CustomRecipe(config);
            if (recipe.Recipe != null)
            {
                // A new item has no recipe of its own to copy from, so unset fields take the game's defaults
                recipe.Recipe.m_resources = RecipeFields.ApplyRequirements(recipe.Recipe.m_resources, requirementSources, null, null);
            }
            return recipe;
        }

        /// <summary>
        /// Re-applies everything that can change while the game is running: the stats, names, icons and
        /// textures of items this mod added, and every modification of an item that already exists.
        /// Definitions that added or removed an item since startup cannot be honoured without a restart
        /// and are reported instead.
        /// </summary>
        public static void ReapplyExistingItems()
        {
            HashSet<string> stillDefined = new HashSet<string>();

            foreach (KeyValuePair<String, ItemAddition> entry in ItemDefinitions)
            {
                ItemAddition def = entry.Value;
                if (def == null || string.IsNullOrEmpty(def.prefab)) { continue; }
                if (def.action == ItemAction.Remove || def.action == ItemAction.Modify) { continue; }
                stillDefined.Add(def.prefab);

                if (!CreatedPrefabs.Contains(def.prefab))
                {
                    Logger.LogWarning($"'{entry.Key}' defines a new item ({def.prefab}) which cannot be added while the game is running. Restart the game or return to the main menu to create it.");
                    continue;
                }

                GameObject prefab = PrefabManager.Instance.GetPrefab(def.prefab);
                if (prefab == null)
                {
                    Logger.LogWarning($"{def.prefab} was registered but can no longer be resolved, skipping.");
                    continue;
                }

                try
                {
                    ApplyDefinition(prefab, def);
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Refreshed {def.prefab}."); }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Could not refresh {def.prefab}. Error: {ex}");
                }
            }

            foreach (string created in CreatedPrefabs)
            {
                if (!stillDefined.Contains(created))
                {
                    Logger.LogWarning($"{created} was removed from the configuration but is already registered. Restart the game to remove it.");
                }
            }

            ApplyItemModifications();
            // Items added this run resolved their status effects with no object DB to look in
            ResolveDeferredReferences(TrackedItems);
        }

        public static void UpdateItemAdditions(ItemAdditionCollection itemAdditions)
        {
            ItemDefinitions.Clear();
            // A file whose entries are all commented out still parses, but leaves the dictionary null
            if (itemAdditions?.ItemAdditions == null) { return; }
            foreach (KeyValuePair<String, ItemAddition> entry in itemAdditions.ItemAdditions)
            {
                ItemDefinitions.Add(entry.Key, entry.Value);
            }
        }

        public static void UpdateItemAdditionsFromList(List<ItemAdditionCollection> collections)
        {
            ItemDefinitions.Clear();
            foreach (ItemAdditionCollection collection in collections)
            {
                if (collection?.ItemAdditions == null) { continue; }
                foreach (KeyValuePair<String, ItemAddition> entry in collection.ItemAdditions)
                {
                    if (ItemDefinitions.ContainsKey(entry.Key))
                    {
                        Logger.LogWarning($"Item entry '{entry.Key}' is defined more than once, only the first definition will be used.");
                        continue;
                    }
                    ItemDefinitions.Add(entry.Key, entry.Value);
                }
            }
        }

        public static void UpdateItemAdditionsFromRPC(string yaml)
        {
            ItemAdditionCollection collection = ValConfig.yamldeserializer.Deserialize<ItemAdditionCollection>(yaml);
            UpdateItemAdditions(collection);
        }
    }
}

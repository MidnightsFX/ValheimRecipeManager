using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using Logger = Jotunn.Logger;
using static RecipeManager.Common.DataObjects;
using System.Linq;

namespace RecipeManager {
    internal class RecipeUpdater {
        public static Dictionary<String,RecipeModification> RecipesToModify = new Dictionary<String, RecipeModification>();
        public static List<TrackedRecipe> TrackedRecipes = new List<TrackedRecipe>();
        private static readonly Dictionary<Recipe, bool> EnabledWhenFirstSeen = new Dictionary<Recipe, bool>();

        /// <summary>
        /// Every recipe this mod built for a Modify, with the recipe it replaces. Recipe objects outlive a world,
        /// and one another mod registers through Jotunn can come back in the next world as the one built here,
        /// so a built recipe found in the list is followed back to the real original.
        /// </summary>
        private static readonly Dictionary<Recipe, Recipe> BuiltFrom = new Dictionary<Recipe, Recipe>();

        /// <summary>The Jotunn entry a recipe from another mod was registered under, captured the first time it is seen.</summary>
        private static readonly Dictionary<Recipe, CustomRecipe> JotunnEntries = new Dictionary<Recipe, CustomRecipe>();

        public static void SecondaryRecipeSync() {
            // Guard against doing any recipe modifications if there are no recipes to modify
            if (TrackedRecipes.Count == 0) return;

            Logger.LogInfo($"Validating {TrackedRecipes.Count} Recipe modifications");
            foreach (TrackedRecipe tracked_recipe in TrackedRecipes) {
                if(CheckIfRecipeWasModified(tracked_recipe) == true) { continue; }

                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Tracked {tracked_recipe.prefab} recipe still has its original recipe in the db. Modifying."); }
                ApplyRecipeModifcations(tracked_recipe);
            }
        }

        public static void RecipeRevert()
        {
            foreach (TrackedRecipe tracked_recipe in TrackedRecipes)
            {
                ReverseRecipeModifications(tracked_recipe);
            }
        }

        /// <summary>Hooked to every new object DB in a world.</summary>
        public static void InitialRecipesAndSynchronize()
        {
            RevertOutsideObjectDB();
            BuildRecipesForTracking();
            RecipeUpdateRunner();
        }

        /// <summary>
        /// Puts back what the previous world changed outside its object DB: switching a recipe on or off writes to
        /// the recipe itself, and Jotunn's entries carry into every object DB after. The new object DB's list is
        /// left alone, it starts fresh. Some mods also build new recipe objects for each world, so re-adding a
        /// recipe a Delete took out of the old list would bring back a stale copy.
        /// </summary>
        private static void RevertOutsideObjectDB()
        {
            foreach (TrackedRecipe tracked_recipe in TrackedRecipes)
            {
                if (tracked_recipe == null) { continue; }
                if ((tracked_recipe.action == DataObjects.Action.Disable || tracked_recipe.action == DataObjects.Action.Enable) && tracked_recipe.listedRecipe != null)
                {
                    tracked_recipe.listedRecipe.m_enabled = tracked_recipe.originalEnabled;
                }
                if (tracked_recipe.action == DataObjects.Action.Modify)
                {
                    RestoreJotunnEntry(tracked_recipe.updatedCustomRecipe, tracked_recipe.originalRecipe);
                }
            }
        }

        /// <summary>Undoes the modifications applied, then builds and applies the configured ones.</summary>
        public static void Reapply()
        {
            RecipeRevert();
            BuildRecipesForTracking();
            SecondaryRecipeSync();
        }

        public static void RecipeUpdateRunner()
        {
            // Perform the first recipe modification
            Logger.LogDebug($"Applying {TrackedRecipes.Count} Recipe modifications");
            foreach (TrackedRecipe tracked_recipe  in TrackedRecipes) {
                ApplyRecipeModifcations(tracked_recipe);
            }
        }

        /// <summary>
        /// Every action needs either the recipe that already exists in the object DB or the replacement
        /// built from the config. A tracked entry can be missing one of them when the prefab or recipe
        /// name in the config did not resolve, so check before acting rather than silently doing nothing.
        /// </summary>
        private static bool HasTargetsForAction(TrackedRecipe tracked_recipe, string verb)
        {
            if (tracked_recipe == null) {
                Logger.LogWarning($"A tracked recipe was null and cannot be {verb}, skipping it.");
                return false;
            }

            bool needsListed = tracked_recipe.action == DataObjects.Action.Disable
                || tracked_recipe.action == DataObjects.Action.Enable
                || tracked_recipe.action == DataObjects.Action.Delete
                || tracked_recipe.action == DataObjects.Action.Modify;
            if (needsListed && tracked_recipe.listedRecipe == null) {
                Logger.LogWarning($"No existing recipe could be found for {tracked_recipe.recipeName ?? tracked_recipe.prefab}, so it cannot be {verb}. Check the prefab or recipeName in the config, RM_Recipes_PrintAll lists the recipes that exist.");
                return false;
            }

            bool needsUpdated = tracked_recipe.action == DataObjects.Action.Add
                || tracked_recipe.action == DataObjects.Action.Modify;
            if (needsUpdated && tracked_recipe.updatedRecipe == null) {
                Logger.LogWarning($"No replacement recipe could be built for {tracked_recipe.prefab}, so it cannot be {verb}.");
                return false;
            }
            return true;
        }

        public static void ApplyRecipeModifcations(TrackedRecipe tracked_recipe)
        {
            if (!HasTargetsForAction(tracked_recipe, "applied")) { return; }
            if (ObjectDB.instance == null) {
                Logger.LogWarning($"The object database is not available yet, the modification for {tracked_recipe.prefab} will be skipped.");
                return;
            }

            if (ValConfig.EnableDebugMode.Value)
            {
                if (tracked_recipe.updatedRecipe != null) { LogRecipe("Applying Updated Recipe", tracked_recipe.updatedRecipe); }
                if (tracked_recipe.listedRecipe != null) { LogRecipe("Targeting Original Recipe", tracked_recipe.listedRecipe); }
            }

            bool update_applied = false;
            // Disable Action
            if (tracked_recipe.action == DataObjects.Action.Disable)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Disable Action called for {tracked_recipe.prefab} recipe"); }
                update_applied = DisableRecipe(tracked_recipe.listedRecipe);
            }

            // Delete Action
            if (tracked_recipe.action == DataObjects.Action.Delete)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Delete Action called for {tracked_recipe.prefab} recipe"); }
                update_applied = DeleteRecipe(tracked_recipe.listedRecipe);
            }

            // Modify Action
            if (tracked_recipe.action == DataObjects.Action.Modify)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Modify Action called for {tracked_recipe.prefab} recipe"); }
                if (ObjectDB.instance.m_recipes.Contains(tracked_recipe.updatedRecipe)) {
                    DeleteRecipe(tracked_recipe.listedRecipe); // Ensure the original recipe is removed if it still exists, because the modified one has been added
                    update_applied = true;
                } else if (ObjectDB.instance.m_recipes.Contains(tracked_recipe.listedRecipe)) {
                    update_applied = ModifyRecipeInODB(tracked_recipe.listedRecipe, tracked_recipe.updatedRecipe);
                    SwapJotunnEntry(tracked_recipe.originalRecipe, tracked_recipe.updatedCustomRecipe);
                }
            }

            // Add Action
            if (tracked_recipe.action == DataObjects.Action.Add)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Add Action called for {tracked_recipe.prefab} recipe"); }
                update_applied = AddRecipe(tracked_recipe.updatedRecipe);
            }

            // Enable Action
            if (tracked_recipe.action == DataObjects.Action.Enable)
            {
                if (ValConfig.EnableDebugMode.Value) {
                    Logger.LogWarning($"Enable Action called for {tracked_recipe.prefab} recipe. Are you sure you wanted that? Most recipes are already enabled.");
                }
                update_applied = EnableRecipe(tracked_recipe.listedRecipe);
            }

            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"{tracked_recipe.prefab} recipe update applied? {update_applied}"); }
        }

        private static void LogRecipe(string heading, Recipe recipe)
        {
            String resources = "";
            foreach (Piece.Requirement res in recipe.m_resources ?? new Piece.Requirement[0])
            {
                resources += $" {res.m_resItem},{res.m_amount},{res.m_amountPerLevel}{(res.m_upgraderResource ? ",upgrader" : "")}";
            }
            Logger.LogInfo($"{heading}: {recipe.name}\n" +
                $"amount:{recipe.m_amount}\n" +
                $"enabled:{recipe.m_enabled}\n" +
                $"craftingStation:{recipe.m_craftingStation}\n" +
                $"repairStation:{recipe.m_repairStation}\n" +
                $"reqStationLevel:{recipe.m_minStationLevel}\n" +
                $"reqOneIngrediant:{recipe.m_requireOnlyOneIngredient}\n" +
                $"upgradeOnly:{recipe.m_noCraftOnlyUpgrade}\n" +
                $"listSortWeight:{recipe.m_listSortWeight}\n" +
                $"resources:{resources}");
        }

        public static void ReverseRecipeModifications(TrackedRecipe tracked_recipe)
        {
            if (!HasTargetsForAction(tracked_recipe, "reverted")) { return; }

            // Back to how the recipe started, which is not always on. These write to the recipe itself, so they
            // work with or without an object DB to find it in.
            if (tracked_recipe.action == DataObjects.Action.Disable || tracked_recipe.action == DataObjects.Action.Enable)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Reverting {tracked_recipe.action} for {tracked_recipe.prefab} recipe"); }
                tracked_recipe.listedRecipe.m_enabled = tracked_recipe.originalEnabled;
                return;
            }

            // Modify Action. Jotunn's entry is swapped back even with no object DB, the next one is built from it.
            if (tracked_recipe.action == DataObjects.Action.Modify)
            {
                RestoreJotunnEntry(tracked_recipe.updatedCustomRecipe, tracked_recipe.originalRecipe);
            }

            if (ObjectDB.instance == null) {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"The object database is not available, nothing in it to revert for {tracked_recipe.prefab}."); }
                return;
            }

            bool update_applied = false;
            // Delete Action
            if (tracked_recipe.action == DataObjects.Action.Delete)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Reverting delete for {tracked_recipe.prefab} recipe"); }
                update_applied = AddRecipe(tracked_recipe.listedRecipe);
            }

            // Modify Action
            if (tracked_recipe.action == DataObjects.Action.Modify)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Reversing modify for {tracked_recipe.prefab} recipe"); }
                // The original, not whatever was listed when this was applied, which can be a recipe built earlier
                update_applied = ModifyRecipeInODB(tracked_recipe.updatedRecipe, tracked_recipe.originalRecipe);
            }

            // Add Action
            if (tracked_recipe.action == DataObjects.Action.Add)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Reverting add for {tracked_recipe.prefab} recipe"); }
                update_applied = DeleteRecipe(tracked_recipe.updatedRecipe);
            }

            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"{tracked_recipe.prefab} recipe modification reverted? {update_applied}"); }
        }

        public static bool CheckIfRecipeWasModified(TrackedRecipe trackedRecipe)
        {
            if (trackedRecipe == null || ObjectDB.instance == null) { return true; }
            bool modified = true;
            if (trackedRecipe.action == DataObjects.Action.Modify || trackedRecipe.action == DataObjects.Action.Delete) {
                int index_of_original_recipe = ObjectDB.instance.m_recipes.IndexOf(trackedRecipe.listedRecipe);
                if (index_of_original_recipe > -1)
                {
                    modified = false;
                }
            }
            if (trackedRecipe.action == DataObjects.Action.Add) {
                int index_of_new_recipe = ObjectDB.instance.m_recipes.IndexOf(trackedRecipe.updatedRecipe);
                if (index_of_new_recipe < 0)
                {
                    modified = false;
                }
            }
            // Just always reapply disables or enables since they are quick and harmless
            if (trackedRecipe.action == DataObjects.Action.Enable || trackedRecipe.action == DataObjects.Action.Disable) {
                modified = false;
            }
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"recipe {trackedRecipe.recipeName ?? trackedRecipe.prefab} already modified? {modified}"); }
            return modified;
        }

        public static void BuildRecipesForTracking()
        {
            TrackedRecipes.Clear();
            Patches.RecipeUpgradeCosts.FromRecipes.Clear();
            Patches.RecipeStationLevelCap.FromRecipes.Clear();
            if (RecipesToModify.Count == 0 || ObjectDB.instance == null) { return; }
            foreach (KeyValuePair<string, RecipeModification>  recipeMod in RecipesToModify)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Constructing modification details for {recipeMod.Key}"); }
                RecipeModification mod = recipeMod.Value;
                TrackedRecipe tRecipeDetails = new TrackedRecipe {
                    key = recipeMod.Key,
                    action = mod.action,
                    prefab = mod.prefab,
                    recipeName = mod.recipeName
                };

                // An Add makes a recipe of its own, every other action works on one that is already listed
                if (mod.action != DataObjects.Action.Add)
                {
                    Recipe listed = FindListedRecipe(mod);
                    if (listed == null) {
                        Logger.LogWarning($"Recipe modification '{recipeMod.Key}' will be skipped, no recipe for {mod.recipeName ?? mod.prefab} could be found. Check the prefab or recipeName in the config, RM_Recipes_PrintAll lists the recipes that exist.");
                        continue;
                    }
                    tRecipeDetails.listedRecipe = listed;
                    tRecipeDetails.originalRecipe = OriginalOf(listed);
                    tRecipeDetails.originalCustomRecipe = JotunnEntryFor(tRecipeDetails.originalRecipe);
                    // Disable and Enable write to the recipe asset itself, which outlives the object DB. Read the state
                    // the first time the recipe is seen, a later world load would read back our own change.
                    if (!EnabledWhenFirstSeen.ContainsKey(listed)) {
                        EnabledWhenFirstSeen[listed] = listed.m_enabled;
                    }
                    tRecipeDetails.originalEnabled = EnabledWhenFirstSeen[listed];
                }

                if (mod.action == DataObjects.Action.Modify || mod.action == DataObjects.Action.Add)
                {
                    Recipe built = RecipeBuilder.Build(recipeMod.Key, mod, tRecipeDetails.originalRecipe, out string problem);
                    if (built == null) {
                        Logger.LogWarning($"Recipe modification '{recipeMod.Key}' will be skipped, {problem}.");
                        continue;
                    }
                    if (mod.action == DataObjects.Action.Modify) { BuiltFrom[built] = tRecipeDetails.originalRecipe; }
                    // Left out of a Modify, the cap is kept like any other value
                    short? cap = mod.maxStationLevel ?? (mod.action == DataObjects.Action.Modify ? Patches.RecipeStationLevelCap.CapOf(tRecipeDetails.originalRecipe) : null);
                    Patches.RecipeStationLevelCap.Register(Patches.RecipeStationLevelCap.FromRecipes, built, cap, $"Recipe modification '{recipeMod.Key}'");
                    tRecipeDetails.updatedRecipe = built;
                    tRecipeDetails.updatedCustomRecipe = new CustomRecipe(built, false, false);
                }
                else if (mod.maxStationLevel.HasValue || (mod.recipe?.ingredients?.Exists(i => i?.upgradeCosts != null) ?? false))
                {
                    Logger.LogWarning($"Recipe modification '{recipeMod.Key}' sets maxStationLevel or upgradeCosts on a {mod.action}, only a Modify or an Add changes those.");
                }
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Adding tracked Recipe"); }
                TrackedRecipes.Add(tRecipeDetails);
            }
        }

        /// <summary>The recipe an entry acts on, by its recipe name when it gives one, otherwise by the item it makes.</summary>
        private static Recipe FindListedRecipe(RecipeModification mod)
        {
            int index = -1;
            // Matching the recipe name allows multiple modifications to the same item
            if (!string.IsNullOrEmpty(mod.recipeName)) { index = RecipeIndexForRecipeName(mod.recipeName); }
            if (index == -1 && !string.IsNullOrEmpty(mod.prefab)) { index = RecipeIndexForPrefab(mod.prefab); }
            return index > -1 ? ObjectDB.instance.m_recipes[index] : null;
        }

        private static Recipe OriginalOf(Recipe recipe)
        {
            Recipe original = recipe;
            while (BuiltFrom.TryGetValue(original, out Recipe from)) { original = from; }
            return original;
        }

        public static int RecipeIndexForPrefab(string prefab)
        {
            if (ObjectDB.instance == null || ObjectDB.instance.m_recipes == null) { return -1; }
            return ObjectDB.instance.m_recipes.FindIndex(m => m != null && m.m_item != null && m.m_item.name == prefab);
        }

        public static int RecipeIndexForRecipeName(string recipe_name)
        {
            if (ObjectDB.instance == null || ObjectDB.instance.m_recipes == null) { return -1; }
            return ObjectDB.instance.m_recipes.FindIndex(m => m != null && m.name != null && m.name == recipe_name);
        }

        private static HashSet<CustomRecipe> JotunnRecipes()
        {
            return AccessTools.Field(typeof(ItemManager), "Recipes").GetValue(ItemManager.Instance) as HashSet<CustomRecipe>;
        }

        private static CustomRecipe JotunnEntryFor(Recipe original)
        {
            if (original == null) { return null; }
            if (JotunnEntries.TryGetValue(original, out CustomRecipe entry)) { return entry; }
            entry = JotunnRecipes()?.FirstOrDefault(custom => custom != null && custom.Recipe == original);
            if (entry != null) { JotunnEntries[original] = entry; }
            return entry;
        }

        /// <summary>
        /// Jotunn adds the recipes other mods register through it to every new object DB. When the modified recipe
        /// is one of those, ours takes its place there too. Recipes that did not come through Jotunn are left alone.
        /// </summary>
        private static void SwapJotunnEntry(Recipe original, CustomRecipe updated)
        {
            HashSet<CustomRecipe> registered = JotunnRecipes();
            if (registered == null || updated == null || registered.Contains(updated)) { return; }
            // The original's own entry, or one this mod swapped in for it earlier
            int removed = registered.RemoveWhere(custom => custom != null && custom.Recipe != null && OriginalOf(custom.Recipe) == original);
            if (removed > 0) { registered.Add(updated); }
        }

        private static void RestoreJotunnEntry(CustomRecipe updated, Recipe original)
        {
            HashSet<CustomRecipe> registered = JotunnRecipes();
            if (registered == null || updated == null || !registered.Remove(updated)) { return; }
            if (original != null && JotunnEntries.TryGetValue(original, out CustomRecipe entry)) { registered.Add(entry); }
        }

        /// <summary>True when the object DB is built and the recipe is something we can act on.</summary>
        private static bool RecipeDBReady(Recipe recipe, string operation)
        {
            if (ObjectDB.instance == null || ObjectDB.instance.m_recipes == null) {
                Logger.LogWarning($"The object database is not available, cannot {operation} a recipe.");
                return false;
            }
            if (recipe == null) {
                Logger.LogWarning($"Cannot {operation} a recipe that could not be resolved, skipping it.");
                return false;
            }
            return true;
        }

        public static bool ModifyRecipeInODB(Recipe recipe, Recipe newRecipe)
        {
            if (!RecipeDBReady(recipe, "modify") || newRecipe == null) { return false; }
            int index = ObjectDB.instance.m_recipes.IndexOf(recipe);
            if (index > -1)
            {
                ObjectDB.instance.m_recipes[index] = newRecipe;
                return true;
            }
            return false;
        }

        public static bool DisableRecipe(Recipe recipe)
        {
            if (!RecipeDBReady(recipe, "disable")) { return false; }
            int index = ObjectDB.instance.m_recipes.IndexOf(recipe);
            if (index > -1)
            {
                ObjectDB.instance.m_recipes[index].m_enabled = false;
                return true;
            }
            return false;
        }

        public static bool EnableRecipe(Recipe recipe)
        {
            if (!RecipeDBReady(recipe, "enable")) { return false; }
            int index = ObjectDB.instance.m_recipes.IndexOf(recipe);
            if (index > -1)
            {
                ObjectDB.instance.m_recipes[index].m_enabled = true;
                return true;
            }
            return false;
        }

        public static bool DeleteRecipe(Recipe recipe)
        {
            if (!RecipeDBReady(recipe, "delete")) { return false; }
            return ObjectDB.instance.m_recipes.Remove(recipe);
        }

        public static bool AddRecipe(Recipe recipe)
        {
            // Guarded hard: a null in ObjectDB.m_recipes breaks the game's own recipe iteration,
            // not just ours, and the failure would surface far away from here.
            if (!RecipeDBReady(recipe, "add")) { return false; }
            if(!ObjectDB.instance.m_recipes.Contains(recipe))
            {
                ObjectDB.instance.m_recipes.Add(recipe);
            }
            return true;
        }
    }
}

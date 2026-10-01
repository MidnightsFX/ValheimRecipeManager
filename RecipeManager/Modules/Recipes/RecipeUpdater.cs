using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using Logger = Jotunn.Logger;
using static RecipeManager.Common.DataObjects;
using System.Linq;
using Jotunn.Configs;
using UnityEngine;

namespace RecipeManager {
    internal class RecipeUpdater {
        public static Dictionary<String,RecipeModification> RecipesToModify = new Dictionary<String, RecipeModification>();
        public static List<TrackedRecipe> TrackedRecipes = new List<TrackedRecipe>();
        private static readonly Dictionary<Recipe, bool> EnabledWhenFirstSeen = new Dictionary<Recipe, bool>();

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

        public static void InitialRecipesAndSynchronize()
        {
            BuildRecipesForTracking();
            RecipeUpdateRunner();
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

            bool needsOriginal = tracked_recipe.action == DataObjects.Action.Disable
                || tracked_recipe.action == DataObjects.Action.Enable
                || tracked_recipe.action == DataObjects.Action.Delete
                || tracked_recipe.action == DataObjects.Action.Modify;
            if (needsOriginal && tracked_recipe.originalRecipe == null) {
                Logger.LogWarning($"No existing recipe could be found for {tracked_recipe.prefab}, so it cannot be {verb}. Check the prefab or recipeName in the config, RM_Recipes_PrintAll lists the recipes that exist.");
                return false;
            }

            bool needsUpdated = tracked_recipe.action == DataObjects.Action.Add
                || tracked_recipe.action == DataObjects.Action.Modify;
            if (needsUpdated && tracked_recipe.updatedRecipe == null) {
                Logger.LogWarning($"No replacement recipe could be built for {tracked_recipe.prefab}, so it cannot be {verb}. Check that the entry has a valid recipe block.");
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
                if (tracked_recipe.updatedRecipe != null)
                {
                    String resources = "";
                    foreach (Piece.Requirement res in tracked_recipe.updatedRecipe.m_resources ?? new Piece.Requirement[0])
                    {
                        resources += $" {res.m_resItem},{res.m_amount},{res.m_amountPerLevel}{(res.m_upgraderResource ? ",upgrader" : "")}";
                    }
                    Logger.LogInfo($"Applying Updated Recipe: {tracked_recipe.updatedRecipe.name}\n" +
                    $"amount:{tracked_recipe.updatedRecipe.m_amount}\n" +
                    $"enabled:{tracked_recipe.updatedRecipe.m_enabled}\n" +
                    $"craftingStation:{tracked_recipe.updatedRecipe.m_craftingStation}\n" +
                    $"reqStationLevel:{tracked_recipe.updatedRecipe.m_minStationLevel}\n" +
                    $"reqOneIngrediant:{tracked_recipe.updatedRecipe.m_requireOnlyOneIngredient}\n" +
                    $"upgradeOnly:{tracked_recipe.updatedRecipe.m_noCraftOnlyUpgrade}\n" +
                    $"listSortWeight:{tracked_recipe.updatedRecipe.m_listSortWeight}\n" +
                    $"resources:{resources}");
                }
                if (tracked_recipe.originalRecipe != null)
                {
                    String resources = "";
                    foreach (Piece.Requirement res in tracked_recipe.originalRecipe.m_resources ?? new Piece.Requirement[0])
                    {
                        resources += $" {res.m_resItem},{res.m_amount},{res.m_amountPerLevel}{(res.m_upgraderResource ? ",upgrader" : "")}";
                    }
                    Logger.LogInfo($"Targeting Original Recipe: {tracked_recipe.originalRecipe.name}\n" +
                    $"amount:{tracked_recipe.originalRecipe.m_amount}\n" +
                    $"enabled:{tracked_recipe.originalRecipe.m_enabled}\n" +
                    $"amount:{tracked_recipe.originalRecipe.m_amount}\n" +
                    $"craftingStation:{tracked_recipe.originalRecipe.m_craftingStation}\n" +
                    $"reqStationLevel:{tracked_recipe.originalRecipe.m_minStationLevel}\n" +
                    $"reqOneIngrediant:{tracked_recipe.originalRecipe.m_requireOnlyOneIngredient}\n" +
                    $"upgradeOnly:{tracked_recipe.originalRecipe.m_noCraftOnlyUpgrade}\n" +
                    $"listSortWeight:{tracked_recipe.originalRecipe.m_listSortWeight}\n" +
                    $"resources:{resources}");
                }
            }

            bool update_applied = false;
            // Disable Action
            if (tracked_recipe.action == DataObjects.Action.Disable)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Disable Action called for {tracked_recipe.prefab} recipe"); }
                update_applied = DisableRecipe(tracked_recipe.originalRecipe);
            }

            // Delete Action
            if (tracked_recipe.action == DataObjects.Action.Delete)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Delete Action called for {tracked_recipe.prefab} recipe"); }
                update_applied = DeleteRecipe(tracked_recipe.originalRecipe);
            }

            // Modify Action
            if (tracked_recipe.action == DataObjects.Action.Modify)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Modify Action called for {tracked_recipe.prefab} recipe"); }
                if (ObjectDB.instance.m_recipes.Contains(tracked_recipe.updatedRecipe)) {
                    DeleteRecipe(tracked_recipe.originalRecipe); // Ensure the original recipe is removed if it still exists, because the modified one has been added
                    update_applied = true;
                } else {
                    if (ObjectDB.instance.m_recipes.Contains(tracked_recipe.originalRecipe)) {
                        update_applied = ModifyRecipeInODB(tracked_recipe.originalRecipe, tracked_recipe.updatedRecipe);
                        ModifyRecipeInJotunnManager(tracked_recipe.originalCustomRecipe, tracked_recipe.updatedCustomRecipe);
                    }
                    
                    
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
                update_applied = EnableRecipe(tracked_recipe.originalRecipe);
            }

            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"{tracked_recipe.prefab} recipe update applied? {update_applied}"); }
        }

        public static void ReverseRecipeModifications(TrackedRecipe tracked_recipe)
        {
            if (!HasTargetsForAction(tracked_recipe, "reverted")) { return; }
            if (ObjectDB.instance == null) {
                Logger.LogWarning($"The object database is not available yet, the revert for {tracked_recipe.prefab} will be skipped.");
                return;
            }

            bool update_applied = false;
            // Disable Action
            if (tracked_recipe.action == DataObjects.Action.Disable)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Reverting disable for {tracked_recipe.prefab} recipe"); }
                // Back to how the recipe started, which is not always on. RM_Recipes_PrintAll writes the recipes the game ships off as Disable.
                update_applied = tracked_recipe.originalEnabled ? EnableRecipe(tracked_recipe.originalRecipe) : DisableRecipe(tracked_recipe.originalRecipe);
            }

            // Delete Action
            if (tracked_recipe.action == DataObjects.Action.Delete)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Reverting delete for {tracked_recipe.prefab} recipe"); }
                update_applied = AddRecipe(tracked_recipe.originalRecipe);
            }

            // Modify Action
            if (tracked_recipe.action == DataObjects.Action.Modify)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Reversing modify for {tracked_recipe.prefab} recipe"); }
                update_applied = ModifyRecipeInODB(tracked_recipe.updatedRecipe, tracked_recipe.originalRecipe);
                ModifyRecipeInJotunnManager(tracked_recipe.updatedCustomRecipe, tracked_recipe.originalCustomRecipe);
            }

            // Add Action
            if (tracked_recipe.action == DataObjects.Action.Add)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Reverting add for {tracked_recipe.prefab} recipe"); }
                update_applied = DeleteRecipe(tracked_recipe.updatedRecipe);
            }

            // Enable Action
            if (tracked_recipe.action == DataObjects.Action.Enable)
            {
                update_applied = tracked_recipe.originalEnabled ? EnableRecipe(tracked_recipe.originalRecipe) : DisableRecipe(tracked_recipe.originalRecipe);
            }

            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"{tracked_recipe.prefab} recipe modification reverted? {update_applied}"); }
        }

        public static bool CheckIfRecipeWasModified(TrackedRecipe trackedRecipe)
        {
            if (trackedRecipe == null || ObjectDB.instance == null) { return true; }
            bool modified = true;
            if (trackedRecipe.action == DataObjects.Action.Modify || trackedRecipe.action == DataObjects.Action.Delete) {
                int index_of_original_recipe = ObjectDB.instance.m_recipes.IndexOf(trackedRecipe.originalRecipe);
                if (index_of_original_recipe > 0)
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
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"recipe {trackedRecipe.recipeName} already modified? {modified}"); }
            return modified;
        }

        public static void BuildRecipesForTracking()
        {
            if (RecipesToModify.Count == 0) { return; }
            TrackedRecipes.Clear();
            foreach (KeyValuePair<string, RecipeModification>  recipeMod in RecipesToModify)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Constructing modification details for {recipeMod.Key}"); }
                // if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Data: {recipeMod.Value}"); }
                TrackedRecipe tRecipeDetails = new TrackedRecipe();
                tRecipeDetails.action = recipeMod.Value.action;
                tRecipeDetails.prefab = recipeMod.Value.prefab;
                int index = -1;
                // Attempt to resolve the recipe by its internal name, if it is provided. This allows multiple modifications to the same recipe or item.
                if (recipeMod.Value.recipeName != null)
                {
                    index = RecipeIndexForRecipeName(recipeMod.Value.recipeName);
                    tRecipeDetails.recipeName = recipeMod.Value.recipeName;
                }
                // fallback to finding the recipe by its prefab if the recipe name is not provided
                if (index == -1)
                {
                    index = RecipeIndexForPrefab(recipeMod.Value.prefab);
                }
                
                if (index > -1)
                {
                    tRecipeDetails.originalRecipe = ObjectDB.instance.m_recipes[index];
                    // Disable and Enable write to the recipe asset itself, which outlives the object DB. Read the state
                    // the first time the recipe is seen, a later world load would read back our own change.
                    if (!EnabledWhenFirstSeen.ContainsKey(tRecipeDetails.originalRecipe)) {
                        EnabledWhenFirstSeen[tRecipeDetails.originalRecipe] = tRecipeDetails.originalRecipe.m_enabled;
                    }
                    tRecipeDetails.originalEnabled = EnabledWhenFirstSeen[tRecipeDetails.originalRecipe];
                } else
                {
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogWarning($"Could not find recipe for: {recipeMod.Value.prefab}"); }
                }
                // Tracked recipe has a recipe definition so we build the new recipe/custom recipe
                if (recipeMod.Value.recipe != null)
                {
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Found custom recipe modifications, building out definition."); }
                    
                    List<RequirementConfig> requirementConfigs = new List<RequirementConfig>();
                    // Kept in step with requirementConfigs, the requirements built from them come back in the same order
                    List<Ingrediant> requirementSources = new List<Ingrediant>();
                    if (recipeMod.Value.recipe.noRecipeCost == false) {
                        if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Setting recipe cost requirements."); }

                        if (recipeMod.Value.recipe.ingredients == null) {
                            Logger.LogWarning($"{recipeMod.Key} has a recipe with no ingredients. Set noRecipeCost to true if the recipe is meant to be free, otherwise add ingredients. This modification will be skipped.");
                            continue;
                        }
                        foreach (Ingrediant ingr in recipeMod.Value.recipe.ingredients)
                        {
                            RequirementConfig requirement = new RequirementConfig { Item = ingr.prefab, Amount = ingr.craftCost, AmountPerLevel = ingr.upgradeCost };
                            // RecipeConfig drops a requirement that costs nothing, which would put the two lists out of step
                            if (!requirement.IsValid()) { continue; }
                            requirementConfigs.Add(requirement);
                            requirementSources.Add(ingr);
                        }
                    }
                    if (recipeMod.Value.repairAt == null) {
                        recipeMod.Value.repairAt = recipeMod.Value.craftedAt;
                    }

                    // Only a Modify has a recipe to copy the values it leaves out from, an added recipe is new
                    Recipe modifiedRecipe = recipeMod.Value.action == DataObjects.Action.Modify ? tRecipeDetails.originalRecipe : null;
                    RecipeFields.RecipeSettings? modifiedSettings = modifiedRecipe != null ? RecipeFields.RecipeSettings.From(modifiedRecipe) : (RecipeFields.RecipeSettings?)null;

                    CustomRecipe updatedCustomRecipe = new CustomRecipe(new RecipeConfig()
                    {
                        Name = tRecipeDetails.recipeName != null ? tRecipeDetails.recipeName : $"Recipe_{recipeMod.Value.prefab}",
                        Amount = recipeMod.Value.craftAmount,
                        CraftingStation = recipeMod.Value.craftedAt,
                        RepairStation = recipeMod.Value.repairAt,
                        MinStationLevel = recipeMod.Value.minStationLevel,
                        RequireOnlyOneIngredient = recipeMod.Value.recipe.anyOneResource,
                        Requirements = requirementConfigs.ToArray()
                    });
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Built new custom recipe."); }
                    Recipe nRecipe = updatedCustomRecipe.Recipe;
                    // Enabled, upgrade only, sort weight and the quality multiplier, which RecipeConfig cannot all carry
                    RecipeFields.Resolve(recipeMod.Value, modifiedSettings).ApplyTo(nRecipe);
                    CustomItem targetPrefab = ItemManager.Instance.GetItem(recipeMod.Value.prefab);
                    if (targetPrefab != null)
                    {
                        if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Found existing custom item, storing for comparision."); }
                        tRecipeDetails.originalCustomRecipe = targetPrefab.Recipe;
                    }
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Resolving references on custom recipe."); }
                    if (recipeMod.Value.craftedAt != null)
                    {
                        try {
                            CraftingStation craftStation = PrefabManager.Instance.GetPrefab(recipeMod.Value.craftedAt)?.GetComponent<CraftingStation>();
                            CraftingStation repairStation;
                            if (recipeMod.Value.craftedAt != recipeMod.Value.repairAt)
                            {
                                repairStation = PrefabManager.Instance.GetPrefab(recipeMod.Value.repairAt)?.GetComponent<CraftingStation>();
                            } else {
                                repairStation = craftStation;
                            }
                            
                            nRecipe.m_repairStation = craftStation;
                            nRecipe.m_craftingStation = craftStation;
                        } catch {
                            Logger.LogWarning($"Crafting station ({recipeMod.Value.craftedAt}) or repair station ({recipeMod.Value.repairAt}) could not be resolved or did not have a craftingStation component");
                        }
                    }
                    if (recipeMod.Value.repairAt != null)
                    {
                        try {
                            CraftingStation repairStation = PrefabManager.Instance.GetPrefab(recipeMod.Value.repairAt)?.GetComponent<CraftingStation>();
                            nRecipe.m_repairStation = repairStation;
                        }
                        catch
                        {
                            Logger.LogWarning($"Repair station ({recipeMod.Value.repairAt}) could not be resolved or did not have a craftingStation component");
                        }
                    }

                    try {
                        GameObject tgo = PrefabManager.Instance.GetPrefab(recipeMod.Value.prefab);
                        ItemDrop tid = tgo.GetComponent<ItemDrop>();
                        if (tid != null) { 
                            nRecipe.m_item = tid;
                        } else
                        {
                            Logger.LogWarning($"Could not find a prefab ({recipeMod.Value.prefab}) GO ({tgo}) with an ItemDrop ({tid}) component to reference. This recipe will not have a target and will be skipped.");
                            continue;
                        }
                    } catch {
                        Logger.LogWarning($"Could not find a prefab ({recipeMod.Value.prefab}) with an ItemDrop component to reference. This recipe will not have a target and will be skipped.");
                        continue;
                    }
                    
                    nRecipe.name = "Recipe_" + recipeMod.Key;

                    if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Resolving resource requirement references."); }
                    bool valid = true;
                    foreach (Piece.Requirement res in nRecipe.m_resources ?? new Piece.Requirement[0])
                    {
                        if (res.m_resItem == null) {
                            valid = false;
                            Logger.LogError($"A resource requirement on {recipeMod.Key} has no item attached, most likely because its prefab name does not exist. This Recipe modification will be skipped.");
                            continue;
                        }
                        var res_prefab = ObjectDB.instance.GetItemPrefab(res.m_resItem.name.Replace("JVLmock_", ""));
                        if (res_prefab != null) {
                            res.m_resItem = res_prefab.GetComponent<ItemDrop>(); 
                        } else {
                            valid = false;
                            Logger.LogError($"Could not resolve itemdrop reference for: {res.m_resItem.name.Replace("JVLmock_", "")}. This Recipe modification will be skipped.");
                        }
                    }
                    if (valid == false) { continue;  }
                    // Reject all entries that do not have resolved references
                    // nRecipe.m_resources.Where(val => val.m_resItem.GetType() == typeof(ItemDrop)).ToArray();

                    int builtRequirements = nRecipe.m_resources.Length;
                    nRecipe.m_resources = RecipeFields.ApplyRequirements(nRecipe.m_resources, requirementSources, modifiedRecipe?.m_resources, RequirementItemName);
                    if (ValConfig.EnableDebugMode.Value && nRecipe.m_resources.Length > builtRequirements) {
                        Logger.LogInfo($"{recipeMod.Key} lists no upgrader resource, keeping the Forge of Potential cost of the recipe it modifies.");
                    }

                    tRecipeDetails.updatedRecipe = nRecipe;
                    tRecipeDetails.updatedCustomRecipe = updatedCustomRecipe;
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Set updated recipe and updatedCustomRecipe."); }
                }
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Adding tracked Recipe"); }
                TrackedRecipes.Add(tRecipeDetails);
            }
        }

        private static string RequirementItemName(Piece.Requirement requirement)
        {
            return requirement.m_resItem != null ? requirement.m_resItem.name : null;
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

        public static bool ModifyRecipeInJotunnManager(CustomRecipe recipe, CustomRecipe newRecipe)
        {

            HashSet<CustomRecipe> hashsetRecipes = AccessTools.Field(typeof(ItemManager), "Recipes").GetValue(ItemManager.Instance) as HashSet<CustomRecipe>;
            if (hashsetRecipes != null)
            {
                // Skip the change if the recipe list already contains the requireed entry
                if (hashsetRecipes.Contains(newRecipe)) {
                    return true;
                }
                // Don't modify anything in jotun if this wasn't a jotun recipe
                if (hashsetRecipes.Contains(recipe)) {
                    hashsetRecipes.Remove(recipe);
                    hashsetRecipes.Add(newRecipe);
                }
                return true;
            }
            return false;
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

        public static void UpdateRecipeModifications(RecipeModificationCollection recipeMods)
        {
            RecipesToModify.Clear();
            foreach (KeyValuePair<String, RecipeModification> entry in recipeMods.RecipeModifications)
            {
                RecipesToModify.Add(entry.Key, entry.Value);
            }
        }

        public static void UpdateRecipeModificationsFromList(List<RecipeModificationCollection> lRecipeMods)
        {
            RecipesToModify.Clear();
            foreach(RecipeModificationCollection rcol in lRecipeMods)
            {
                foreach (KeyValuePair<String, RecipeModification> entry in rcol.RecipeModifications)
                {
                    RecipesToModify.Add(entry.Key, entry.Value);
                }
            }
        }

    }
}

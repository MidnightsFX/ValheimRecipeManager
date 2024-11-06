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

namespace RecipeManager
{
    internal class RecipeUpdater
    {
        public static Dictionary<String,RecipeModification> RecipesToModify = new Dictionary<String, RecipeModification>();
        public static List<TrackedRecipe> TrackedRecipes = new List<TrackedRecipe>();

        public static void SecondaryRecipeSync()
        {
            foreach (TrackedRecipe tracked_recipe in TrackedRecipes)
            {
                if(CheckIfRecipeWasModified(tracked_recipe) == true) { continue; }

                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Tracked {tracked_recipe.prefab} recipe still has its original recipe in the db. Modifying."); }
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
            foreach (TrackedRecipe tracked_recipe  in TrackedRecipes)
            {
                ApplyRecipeModifcations(tracked_recipe);
            }
        }

        public static void ApplyRecipeModifcations(TrackedRecipe tracked_recipe)
        {
            if (Config.EnableDebugMode.Value) 
            {
                if (tracked_recipe.updatedRecipe != null)
                {
                    String resources = "";
                    foreach (Piece.Requirement res in tracked_recipe.updatedRecipe.m_resources)
                    {
                        resources += $" {res.m_resItem},{res.m_amount},{res.m_amountPerLevel}";
                    }
                    Logger.LogInfo($"Applying Updated Recipe: {tracked_recipe.updatedRecipe.name}\n" +
                    $"amount:{tracked_recipe.updatedRecipe.m_amount}\n" +
                    $"enabled:{tracked_recipe.updatedRecipe.m_enabled}\n" +
                    $"amount:{tracked_recipe.updatedRecipe.m_amount}\n" +
                    $"craftingStation:{tracked_recipe.updatedRecipe.m_craftingStation}\n" +
                    $"reqStationLevel:{tracked_recipe.updatedRecipe.m_minStationLevel}\n" +
                    $"reqOneIngrediant:{tracked_recipe.updatedRecipe.m_requireOnlyOneIngredient}\n" +
                    $"resources:{resources}");
                }
                if (tracked_recipe.originalRecipe != null)
                {
                    String resources = "";
                    foreach (Piece.Requirement res in tracked_recipe.originalRecipe.m_resources)
                    {
                        resources += $" {res.m_resItem},{res.m_amount},{res.m_amountPerLevel}";
                    }
                    Logger.LogInfo($"Targeting Original Recipe: {tracked_recipe.originalRecipe.name}\n" +
                    $"amount:{tracked_recipe.originalRecipe.m_amount}\n" +
                    $"enabled:{tracked_recipe.originalRecipe.m_enabled}\n" +
                    $"amount:{tracked_recipe.originalRecipe.m_amount}\n" +
                    $"craftingStation:{tracked_recipe.originalRecipe.m_craftingStation}\n" +
                    $"reqStationLevel:{tracked_recipe.originalRecipe.m_minStationLevel}\n" +
                    $"reqOneIngrediant:{tracked_recipe.originalRecipe.m_requireOnlyOneIngredient}\n" +
                    $"resources:{resources}");
                }
            }

            bool update_applied = false;
            // Disable Action
            if (tracked_recipe.action == DataObjects.Action.Disable)
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Disable Action called for {tracked_recipe.prefab} recipe"); }
                update_applied = DisableRecipe(tracked_recipe.originalRecipe);
            }

            // Delete Action
            if (tracked_recipe.action == DataObjects.Action.Delete)
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Delete Action called for {tracked_recipe.prefab} recipe"); }
                update_applied = DeleteRecipe(tracked_recipe.originalRecipe);
            }

            // Modify Action
            if (tracked_recipe.action == DataObjects.Action.Modify)
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Modify Action called for {tracked_recipe.prefab} recipe"); }
                if (ObjectDB.instance.m_recipes.Contains(tracked_recipe.updatedRecipe)) {
                    update_applied = true;
                } else {
                    update_applied = ModifyRecipeInODB(tracked_recipe.originalRecipe, tracked_recipe.updatedRecipe);
                    ModifyRecipeInJotunnManager(tracked_recipe.originalCustomRecipe, tracked_recipe.updatedCustomRecipe);
                }
            }

            // Add Action
            if (tracked_recipe.action == DataObjects.Action.Add)
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Add Action called for {tracked_recipe.prefab} recipe"); }
                update_applied = AddRecipe(tracked_recipe.updatedRecipe);
                if (ItemManager.Instance.GetRecipe(tracked_recipe.recipeName) != null)
                {
                    ItemManager.Instance.AddRecipe(tracked_recipe.updatedCustomRecipe);
                }
            }

            // Enable Action
            if (tracked_recipe.action == DataObjects.Action.Enable)
            {
                if (Config.EnableDebugMode.Value) {
                    Logger.LogWarning($"Enable Action called for {tracked_recipe.prefab} recipe. Are you sure you wanted that? Most recipes are already enabled.");
                }
                update_applied = EnableRecipe(tracked_recipe.originalRecipe);
            }

            if (Config.EnableDebugMode.Value) { Logger.LogInfo($"{tracked_recipe.prefab} recipe update applied? {update_applied}"); }
        }

        public static void ReverseRecipeModifications(TrackedRecipe tracked_recipe)
        {
            bool update_applied = false;
            // Disable Action
            if (tracked_recipe.action == DataObjects.Action.Disable)
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Reverting disable for {tracked_recipe.prefab} recipe"); }
                update_applied = EnableRecipe(tracked_recipe.originalRecipe);
            }

            // Delete Action
            if (tracked_recipe.action == DataObjects.Action.Delete)
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Reverting delete for {tracked_recipe.prefab} recipe"); }
                update_applied = AddRecipe(tracked_recipe.originalRecipe);
            }

            // Modify Action
            if (tracked_recipe.action == DataObjects.Action.Modify)
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Reversing modify for {tracked_recipe.prefab} recipe"); }
                update_applied = ModifyRecipeInODB(tracked_recipe.updatedRecipe, tracked_recipe.originalRecipe);
                ModifyRecipeInJotunnManager(tracked_recipe.updatedCustomRecipe, tracked_recipe.originalCustomRecipe);
            }

            // Add Action
            if (tracked_recipe.action == DataObjects.Action.Add)
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Reverting add for {tracked_recipe.prefab} recipe"); }
                update_applied = DeleteRecipe(tracked_recipe.updatedRecipe);
            }

            // Enable Action
            if (tracked_recipe.action == DataObjects.Action.Enable)
            {
                update_applied = DisableRecipe(tracked_recipe.originalRecipe);
            }

            if (Config.EnableDebugMode.Value) { Logger.LogInfo($"{tracked_recipe.prefab} recipe modification reverted? {update_applied}"); }
        }

        public static bool CheckIfRecipeWasModified(TrackedRecipe trackedRecipe)
        {
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
            if (Config.EnableDebugMode.Value) { Logger.LogInfo($"recipe {trackedRecipe.recipeName} already modified? {modified}"); }
            return modified;
        }

        public static void BuildRecipesForTracking()
        {
            TrackedRecipes.Clear();
            foreach (KeyValuePair<string, RecipeModification>  recipeMod in RecipesToModify)
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Constructing modification details for {recipeMod.Key}"); }
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
                } else
                {
                    if (Config.EnableDebugMode.Value) { Logger.LogWarning($"Could not find recipe for: {recipeMod.Value.prefab}"); }
                }
                // Tracked recipe has a recipe definition so we build the new recipe/custom recipe
                if (recipeMod.Value.recipe != null)
                {
                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Found custom recipe modifications, building out definition."); }
                    RequirementConfig[] cing_recipe = new RequirementConfig[recipeMod.Value.recipe.ingredients.Count];
                    int i_index = 0;
                    foreach (Ingrediant ingr in recipeMod.Value.recipe.ingredients)
                    {
                        cing_recipe[i_index] = new RequirementConfig { Item = ingr.prefab, Amount = ingr.craftCost, AmountPerLevel = ingr.upgradeCost, Recover = ingr.refund };
                        i_index++;
                    }
                    CustomRecipe updatedCustomRecipe = new CustomRecipe(new RecipeConfig()
                    {
                        Name = tRecipeDetails.recipeName != null ? tRecipeDetails.recipeName : $"Recipe_{recipeMod.Value.prefab}",
                        Amount = recipeMod.Value.craftAmount,
                        CraftingStation = recipeMod.Value.craftedAt,
                        MinStationLevel = recipeMod.Value.minStationLevel,
                        Enabled = recipeMod.Value.action == DataObjects.Action.Disable ? false : true,
                        Requirements = cing_recipe
                    });
                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Built new custom recipe."); }
                    Recipe nRecipe = updatedCustomRecipe.Recipe;
                    CustomItem targetPrefab = ItemManager.Instance.GetItem(recipeMod.Value.prefab);
                    if (targetPrefab != null)
                    {
                        if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Found existing custom item, storing for comparision."); }
                        tRecipeDetails.originalCustomRecipe = targetPrefab.Recipe;
                    }
                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Resolving references on custom recipe."); }
                    if (recipeMod.Value.craftedAt != null)
                    {
                        try {
                            CraftingStation craftStation = PrefabManager.Instance.GetPrefab(recipeMod.Value.craftedAt)?.GetComponent<CraftingStation>();
                            nRecipe.m_repairStation = craftStation;
                            nRecipe.m_craftingStation = craftStation;
                        } catch {
                            Logger.LogWarning($"Crafting station ({recipeMod.Value.craftedAt}) could not be resolved or did not have a craftingStation component");
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

                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Resolving resource requirement references."); }
                    foreach (Piece.Requirement res in nRecipe.m_resources)
                    {
                        var res_prefab = ObjectDB.instance.GetItemPrefab(res.m_resItem.name.Replace("JVLmock_", ""));
                        if (res_prefab != null) {
                            res.m_resItem = res_prefab.GetComponent<ItemDrop>(); 
                        } else {
                            Logger.LogWarning($"Could not resolve itemdrop reference for: {res.m_resItem.name}. This requirement will be deleted.");
                        }
                    }
                    // Reject all entries that do not have resolved references
                    nRecipe.m_resources.Where(val => val.m_resItem.GetType() == typeof(ItemDrop)).ToArray();

                    tRecipeDetails.updatedRecipe = nRecipe;
                    tRecipeDetails.updatedCustomRecipe = updatedCustomRecipe;
                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Set updated recipe and updatedCustomRecipe."); }
                }
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Adding tracked Recipe"); }
                TrackedRecipes.Add(tRecipeDetails);
            }
        }

        public static int RecipeIndexForPrefab(string prefab)
        {
            return ObjectDB.instance.m_recipes.FindIndex(m => m.m_item != null && m.m_item.name == prefab);
        }

        public static int RecipeIndexForRecipeName(string recipe_name)
        {
            return ObjectDB.instance.m_recipes.FindIndex(m => m.name != null && m.name == recipe_name);
        }

        public static bool ModifyRecipeInJotunnManager(CustomRecipe recipe, CustomRecipe newRecipe)
        {
            HashSet<CustomRecipe> hashsetRecipes = AccessTools.Field(typeof(ItemManager), "Recipes").GetValue(ItemManager.Instance) as HashSet<CustomRecipe>;
            if (hashsetRecipes != null)
            {
                hashsetRecipes.Remove(recipe);
                hashsetRecipes.Add(newRecipe);
                return true;
            }
            return false;
        }

        public static bool ModifyRecipeInODB(Recipe recipe, Recipe newRecipe)
        {
            int index = ObjectDB.instance.m_recipes.IndexOf(recipe);
            if (index > -1)
            {
                ObjectDB.instance.m_recipes[index] = newRecipe;
                //ObjectDB.instance.m_recipes[index].m_enabled = newRecipe.m_enabled;
                //ObjectDB.instance.m_recipes[index].m_amount = newRecipe.m_amount;
                //ObjectDB.instance.m_recipes[index].m_resources = newRecipe.m_resources;
                //ObjectDB.instance.m_recipes[index].m_craftingStation = newRecipe.m_craftingStation;
                //ObjectDB.instance.m_recipes[index].m_repairStation = newRecipe.m_repairStation;
                //ObjectDB.instance.m_recipes[index].m_minStationLevel = newRecipe.m_minStationLevel;
                //ObjectDB.instance.m_recipes[index].m_requireOnlyOneIngredient = newRecipe.m_requireOnlyOneIngredient;
                return true;
            }
            return false;
        }

        public static bool DisableRecipe(Recipe recipe)
        {
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
            return ObjectDB.instance.m_recipes.Remove(recipe);
        }

        public static bool AddRecipe(Recipe recipe)
        {
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

    }
}

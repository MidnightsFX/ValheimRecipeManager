using Jotunn.Entities;
using RecipeManager.Common;
using System.IO;
using System;
using System.Linq;
using Logger = Jotunn.Logger;
using BepInEx;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager
{
    internal class RecipeReloadCommand : ConsoleCommand
    {
        public override string Name => "RM_Recipes_Reload";

        public override string Help => "Resynchronizes recipes.";

        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            RecipeUpdater.RecipeRevert();
            // read out the file
            ValConfig.ReloadRecipeFiles();
            RecipeUpdater.BuildRecipesForTracking();
            RecipeUpdater.SecondaryRecipeSync();
            ValConfig.SendUpdatedRecipeConfigs();
        }
    }

    internal class RecipeUnapplyCommand : ConsoleCommand
    {
        public override string Name => "RM_Recipes_Unapply";

        public override string Help => "Removes recipe modifications.";

        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            RecipeUpdater.RecipeRevert();
        }
    }

    internal class RecipePrintCommand : ConsoleCommand
    {
        public override string Name => "RM_Recipes_PrintAll";

        public override string Help => "Prints all the recipes stored in the Object DB";

        public override void Run(string[] args)
        {
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Starting to dump recipes"); }
            String ODBRecipes_path = Path.Combine(Paths.ConfigPath, "RecipeManager", "ObjectDBRecipes.yaml");
            RecipeModificationCollection recipeCollection = new RecipeModificationCollection();
            using (StreamWriter writetext = new StreamWriter(ODBRecipes_path))
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Loading recipes from ODB"); }
                foreach (Recipe recipe in ObjectDB.instance.m_recipes.ToList())
                {
                    // Skip invalid recipes
                    if (recipe == null) { continue; }
                    if (recipe.name == null) { continue; }

                    if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Building Recipe {recipe.name}"); }
                    RecipeModification recipe_as_mod = new RecipeModification();
                    
                    recipe_as_mod.recipeName = recipe.name;
                    recipe_as_mod.minStationLevel = (short)recipe.m_minStationLevel;
                    recipe_as_mod.craftAmount = (short)recipe.m_amount;
                    recipe_as_mod.action = DataObjects.Action.Enable;

                    // null checks needed on all child object accessors due to some oddly formed recipes
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Checking for empty referenced objects"); }
                    if (recipe.m_craftingStation != null) {
                        if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Adding crafting station"); }
                        recipe_as_mod.craftedAt = recipe.m_craftingStation.name;
                    }
                    if (recipe.m_repairStation != null)
                    {
                        if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Adding repair station"); }
                        recipe_as_mod.repairAt = recipe.m_repairStation.name;
                    }
                    if (recipe.m_item != null)
                    {
                        if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Adding prefab"); }
                        recipe_as_mod.prefab = recipe.m_item.name;
                    }
                    SimpleRecipe temp_srecipe = new SimpleRecipe();
                    if (recipe.m_resources != null && recipe.m_resources.Length > 0)
                    {
                        temp_srecipe.anyOneResource = recipe.m_requireOnlyOneIngredient;
                        if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Building resource requirements"); }
                        foreach (Piece.Requirement req in recipe.m_resources)
                        {
                            try
                            {
                                Ingrediant res_req = new Ingrediant();
                                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Setting crafting cost"); }
                                res_req.craftCost = (short)req.m_amount;
                                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Setting upgrade cost"); }
                                res_req.upgradeCost = (short)req.m_amountPerLevel;
                                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Setting prefab name"); }
                                if (req.m_resItem != null) { res_req.prefab = req.m_resItem.name; }
                                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Adding to ingrediants list"); }
                                temp_srecipe.ingredients.Add(res_req);
                            } catch (Exception e)
                            {
                                if (ValConfig.EnableDebugMode.Value) { Logger.LogWarning($"Requirement did not contain all of the details required to set an ingrediant {req} \n{e}"); }
                            }
                        }
                    }
                    if (temp_srecipe.ingredients != null && temp_srecipe.ingredients.Count > 0) {
                        recipe_as_mod.recipe = temp_srecipe;
                    }
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Adding {recipe.name} to collection."); }
                    if (recipeCollection.RecipeModifications.ContainsKey(recipe.name))
                    {
                        try {
                            int randtag = UnityEngine.Random.Range(0, 1000);
                            recipeCollection.RecipeModifications.Add(recipe.name + $"_{randtag}", recipe_as_mod);
                            Logger.LogWarning($"{recipe.name} was already added to the list of recipes and will be renamed {recipe.name}_{randtag}, please use unique recipe names.");
                        } catch {
                            Logger.LogWarning($"{recipe.name} was already added to the list of recipes and will be skipped, please use unique recipe names.");
                        }
                    } else
                    {
                        recipeCollection.RecipeModifications.Add(recipe.name, recipe_as_mod);
                    }
                    
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Recipe {recipe.name} Added."); }
                }
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Serializing and printing recipes."); }
                var yaml = ValConfig.yamlserializer.Serialize(recipeCollection);
                writetext.WriteLine(yaml);
                Logger.LogInfo($"Recipes dumped to file {ODBRecipes_path}");
            }
        }
    }
}

using Jotunn;
using Jotunn.Entities;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = Jotunn.Logger;

namespace RecipeManager
{
    internal class PieceReloadCommand : ConsoleCommand
    {
        public override string Name => "RM_Pieces_Reload";

        public override string Help => "Resynchronizes piece modifications.";

        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            RecipeUpdater.RecipeRevert();
            // read out the file
            string pieceConfigData = File.ReadAllText(Config.pieceConfigFilePath);
            try
            {
                var pieceDeserializedData = Config.yamldeserializer.Deserialize<RecipeModificationCollection>(pieceConfigData);
                RecipeUpdater.UpdateRecipeModifications(pieceDeserializedData);
            }
            catch
            {
                Logger.LogWarning($"Could not reload the pieces file from disk: {Config.recipeConfigFilePath}");
            }
            PieceUpdater.BuildPieceTracker();
            PieceUpdater.PieceUpdateRunner();
        }
    }

    // Need to actually fill this out
    internal class PiecePrintCommand : ConsoleCommand
    {
        public override string Name => "RM_Pieces_PrintAll";

        public override string Help => "Prints all the Pieces found.";

        public override void Run(string[] args)
        {
            if (Config.EnableDebugMode.Value) { Logger.LogInfo("Starting to dump recipes"); }
            String ODBRecipes_path = Path.Combine(BepInEx.Paths.ConfigPath, "RecipeManager", "AllPiecesFound.yaml");
            RecipeModificationCollection recipeCollection = new RecipeModificationCollection();
            using (StreamWriter writetext = new StreamWriter(ODBRecipes_path))
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo("Gathering Pieces"); }
                List<Piece> pieces = Resources.FindObjectsOfTypeAll<Piece>().ToList<Piece>();

                foreach (Piece pc in pieces)
                {
                    if (pc == null) { continue; }
                    if (pc.name == null) { continue; }
                    // This list of just broken recipes
                    // if (recipe.name == "Recipe_Adze") { continue; }
                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Building Recipe {pc.m_name}"); }
                    PieceModification piece_as_mod = new PieceModification();

                    recipe_as_mod.recipeName = recipe.name;
                    recipe_as_mod.minStationLevel = (short)recipe.m_minStationLevel;
                    recipe_as_mod.craftAmount = (short)recipe.m_amount;
                    recipe_as_mod.action = DataObjects.Action.Enable;

                    // null checks needed on all child object accessors due to some oddly formed recipes
                    if (Config.EnableDebugMode.Value) { Logger.LogInfo("Checking for empty referenced objects"); }
                    if (recipe.m_craftingStation != null)
                    {
                        if (Config.EnableDebugMode.Value) { Logger.LogInfo("Adding crafting station"); }
                        recipe_as_mod.craftedAt = recipe.m_craftingStation.name;
                    }
                    if (recipe.m_item != null)
                    {
                        if (Config.EnableDebugMode.Value) { Logger.LogInfo("Adding prefab"); }
                        recipe_as_mod.prefab = recipe.m_item.name;
                    }
                    SimpleRecipe temp_srecipe = new SimpleRecipe();
                    if (recipe.m_resources != null && recipe.m_resources.Length > 0)
                    {
                        temp_srecipe.anyOneResource = recipe.m_requireOnlyOneIngredient;
                        if (Config.EnableDebugMode.Value) { Logger.LogInfo("Building resource requirements"); }
                        foreach (Piece.Requirement req in recipe.m_resources)
                        {
                            try
                            {
                                Ingrediant res_req = new Ingrediant();
                                if (Config.EnableDebugMode.Value) { Logger.LogInfo("Setting crafting cost"); }
                                res_req.craftCost = (short)req.m_amount;
                                if (Config.EnableDebugMode.Value) { Logger.LogInfo("Setting upgrade cost"); }
                                res_req.upgradeCost = (short)req.m_amountPerLevel;
                                if (Config.EnableDebugMode.Value) { Logger.LogInfo("Setting refund bool"); }
                                res_req.refund = req.m_recover;
                                if (Config.EnableDebugMode.Value) { Logger.LogInfo("Setting prefab name"); }
                                if (req.m_resItem != null) { res_req.prefab = req.m_resItem.name; }
                                if (Config.EnableDebugMode.Value) { Logger.LogInfo("Adding to ingrediants list"); }
                                temp_srecipe.ingredients.Add(res_req);
                            }
                            catch (Exception e)
                            {
                                if (Config.EnableDebugMode.Value) { Logger.LogWarning($"Requirement did not contain all of the details required to set an ingrediant {req} \n{e}"); }
                            }
                        }
                    }
                    if (temp_srecipe.ingredients != null && temp_srecipe.ingredients.Count > 0)
                    {
                        recipe_as_mod.recipe = temp_srecipe;
                    }
                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Adding {recipe.name} to collection."); }
                    if (recipeCollection.RecipeModifications.ContainsKey(recipe.name))
                    {
                        try
                        {
                            int randtag = UnityEngine.Random.Range(0, 1000);
                            recipeCollection.RecipeModifications.Add(recipe.name + $"_{randtag}", recipe_as_mod);
                            Logger.LogWarning($"{recipe.name} was already added to the list of recipes and will be renamed {recipe.name}_{randtag}, please use unique recipe names.");
                        }
                        catch
                        {
                            Logger.LogWarning($"{recipe.name} was already added to the list of recipes and will be skipped, please use unique recipe names.");
                        }
                    }
                    else
                    {
                        recipeCollection.RecipeModifications.Add(recipe.name, recipe_as_mod);
                    }

                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Recipe {recipe.name} Added."); }
                }
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Serializing and printing recipes."); }
                var yaml = Config.yamlserializer.Serialize(recipeCollection);
                writetext.WriteLine(yaml);
                Logger.LogInfo($"Recipes dumped to file {ODBRecipes_path}");
            }
        }
    }

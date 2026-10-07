using Jotunn.Entities;
using RecipeManager.Common;
using System.IO;
using System;
using System.Collections.Generic;
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

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            ValConfig.Recipes.ReloadFromDisk();
        }
    }

    internal class RecipeUnapplyCommand : ConsoleCommand
    {
        public override string Name => "RM_Recipes_Unapply";

        public override string Help => "Removes recipe modifications until the next reload.";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            RecipeUpdater.RecipeRevert();
            // Nothing left tracked, so the delayed validation run does not put them straight back
            RecipeUpdater.TrackedRecipes.Clear();
            ItemCommandUtils.Respond("Recipe modifications removed, RM_Recipes_Reload applies them again.");
        }
    }

    internal class RecipePrintCommand : ConsoleCommand
    {
        internal const string DumpFileName = "ObjectDBRecipes.yaml";

        public override string Name => "RM_Recipes_PrintAll";

        public override string Help => "Prints all the recipes stored in the Object DB";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            if (ObjectDB.instance == null)
            {
                ItemCommandUtils.Respond("The object DB is not available yet, load into a world first.");
                return;
            }
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Starting to dump recipes"); }
            String ODBRecipes_path = Path.Combine(Paths.ConfigPath, "RecipeManager", DumpFileName);
            RecipeModificationCollection recipeCollection = new RecipeModificationCollection();
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Loading recipes from ODB"); }
            foreach (Recipe recipe in ObjectDB.instance.m_recipes.ToList())
            {
                // Skip invalid recipes
                if (recipe == null) { continue; }
                if (recipe.name == null) { continue; }

                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Building Recipe {recipe.name}"); }
                RecipeModification recipe_as_mod = DescribeRecipe(recipe);
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
            }
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Serializing and printing recipes."); }
            using (StreamWriter writetext = new StreamWriter(ODBRecipes_path))
            {
                writetext.WriteLine("# Reference dump of the recipes in the object DB, as they currently are.");
                writetext.WriteLine("# This file is NOT loaded by the mod, it is here so you can see what the recipes are.");
                writetext.WriteLine("#");
                writetext.WriteLine("# To change one of these recipes, copy its entry into your own Recipes.yaml and delete");
                writetext.WriteLine("# every line you do not want to change - anything left out keeps its current value.");
                writetext.WriteLine("# Copied out unchanged, an entry leaves its recipe exactly as it is.");
                writetext.WriteLine("# To switch a recipe off instead, keep its recipeName and use 'action: Disable'.");
                writetext.WriteLine();
                writetext.WriteLine(ValConfig.yamlserializer.Serialize(recipeCollection));
            }
            ItemCommandUtils.Respond($"Dumped {recipeCollection.RecipeModifications.Count} recipe(s) to {ODBRecipes_path}");
        }

        /// <summary>
        /// The Modify that reproduces a recipe. Every field is written out, so the entry copied unchanged gives the
        /// same recipe, apart from the optional ones that match what leaving them out would give.
        /// </summary>
        internal static RecipeModification DescribeRecipe(Recipe recipe)
        {
            RecipeModification recipe_as_mod = new RecipeModification
            {
                action = DataObjects.Action.Modify,
                recipeName = recipe.name,
                prefab = recipe.m_item != null ? recipe.m_item.name : null,
                minStationLevel = (short)recipe.m_minStationLevel,
                craftAmount = (short)recipe.m_amount,
                craftedAt = recipe.m_craftingStation != null ? StationRef.NameOf(recipe.m_craftingStation) : StationRef.None,
                repairAt = recipe.m_repairStation != null ? StationRef.NameOf(recipe.m_repairStation) : null,
                maxStationLevel = Patches.RecipeStationLevelCap.CapOf(recipe)
            };
            RecipeFields.Capture(RecipeFields.RecipeSettings.From(recipe), recipe_as_mod);

            SimpleRecipe temp_srecipe = new SimpleRecipe
            {
                anyOneResource = recipe.m_requireOnlyOneIngredient ? true : (bool?)null,
                ingredients = new List<Ingrediant>()
            };
            // null checks needed on all child object accessors due to some oddly formed recipes
            foreach (Piece.Requirement req in recipe.m_resources ?? new Piece.Requirement[0])
            {
                if (req == null || req.m_resItem == null)
                {
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogWarning($"{recipe.name} has a requirement with no item, it is left out."); }
                    continue;
                }
                // Costs set per level replace the per level cost
                Dictionary<int, int> costsPerLevel = Patches.RecipeUpgradeCosts.CostsOf(req);
                Ingrediant res_req = new Ingrediant
                {
                    prefab = req.m_resItem.name,
                    craftCost = (short)req.m_amount,
                    upgradeCost = costsPerLevel == null ? (short)req.m_amountPerLevel : (short?)null,
                    upgradeCosts = costsPerLevel
                };
                // Without these the Forge of Potential idol reads as an ordinary ingredient
                RecipeFields.Capture(req, res_req);
                temp_srecipe.ingredients.Add(res_req);
            }
            // A recipe with no ingredients needs to say so, a Modify with none listed keeps what is there
            if (temp_srecipe.ingredients.Count == 0)
            {
                temp_srecipe.ingredients = null;
                temp_srecipe.noRecipeCost = true;
            }
            recipe_as_mod.recipe = temp_srecipe;
            return recipe_as_mod;
        }
    }
}

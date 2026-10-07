using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager.Importer
{
    /// <summary>A WackysDatabase Recipe_ file as a Recipes.yaml entry.</summary>
    internal static class RecipeImporter
    {
        private static readonly Dictionary<string, string> Reasons = new Dictionary<string, string>();

        internal static void Import(WackyImporter ctx, WackyFile file)
        {
            ImportReport.FileResult result = ctx.Report.Add(file);
            WackyNode node = ctx.Parse(file, result);
            if (node == null) { return; }

            string name = node.Str("name");
            if (string.IsNullOrEmpty(name))
            {
                result.Skipped = "it has no name";
                return;
            }

            RecipeModification recipe = new RecipeModification();
            string clone = node.Str("clonePrefabName");
            if (!string.IsNullOrEmpty(clone) && clone != "NO")
            {
                // A second recipe for an item, under a name of its own
                recipe.action = Action.Add;
                recipe.prefab = clone;
                recipe.recipeName = name;
                node.Converted.Add($"clonePrefabName -> action: Add, prefab: {clone}, recipeName: {name}");
                if (!ctx.ItemKnown(clone)) { result.Warnings.Add($"no item named {clone} exists in this game for the recipe to make"); }
            }
            else if (clone == "NO")
            {
                // One of the game's recipes, named by the recipe rather than the item it makes
                recipe.action = Action.Modify;
                recipe.recipeName = name;
                node.Converted.Add($"name -> recipeName: {name}");
                if (RecipeUpdater.RecipeIndexForRecipeName(name) < 0) { result.Warnings.Add($"no recipe named {name} exists in this game"); }
            }
            else if (ctx.ClonedItems.Contains(name))
            {
                // A brand new item this import adds has no recipe yet to modify
                recipe.action = Action.Add;
                recipe.prefab = name;
                node.Converted.Add($"name -> action: Add, prefab: {name}, the item is one this import adds");
            }
            else if (WackyImporter.FindItem(name) != null)
            {
                recipe.action = Action.Modify;
                recipe.prefab = name;
                node.Converted.Add($"name -> prefab: {name}");
                if (RecipeUpdater.RecipeIndexForPrefab(name) < 0) { result.Warnings.Add($"{name} has no recipe in this game to modify"); }
            }
            else if (RecipeUpdater.RecipeIndexForRecipeName(name) > -1)
            {
                recipe.action = Action.Modify;
                recipe.recipeName = name;
                node.Converted.Add($"name -> recipeName: {name}");
            }
            else
            {
                recipe.action = Action.Modify;
                recipe.prefab = name;
                node.Converted.Add($"name -> prefab: {name}");
                result.Warnings.Add($"no item or recipe named {name} exists in this game, the entry was written anyway");
            }

            recipe.craftedAt = ctx.Station(node, "craftingStation", "craftedAt", result);
            recipe.repairAt = ctx.Station(node, "repairStation", "repairAt", result);
            recipe.minStationLevel = (short?)node.Int("minStationLevel", "minStationLevel");
            recipe.craftAmount = (short?)node.Int("amount", "craftAmount");
            recipe.listSortWeight = node.Int("m_listSortWeight", "listSortWeight");
            recipe.upgradeOnly = node.Bool("m_noCraftOnlyUpgrade", "upgradeOnly");
            bool? anyOne = node.Bool("requireOnlyOneIngredient", "recipe.anyOneResource");

            bool? disabled = node.Bool("disabled");
            if (disabled == true)
            {
                recipe.enabled = false;
                node.Converted.Add("disabled -> enabled: false");
            }
            else if (disabled == false)
            {
                node.Converted.Add("disabled: false, nothing to do, RecipeManager switches a recipe it changes on");
            }

            SimpleRecipe ingredients = Ingredients(ctx, node, MaxQualityOf(recipe.prefab ?? ItemOfRecipe(recipe.recipeName)), result);
            if (ingredients != null || anyOne != null)
            {
                recipe.recipe = ingredients ?? new SimpleRecipe();
                recipe.recipe.anyOneResource = anyOne;
            }
            if (recipe.action == Action.Add && (recipe.recipe == null || recipe.recipe.ingredients == null))
            {
                result.Skipped = "it adds a recipe but lists no reqs";
                ImportReport.Collect(result, node, key => WackyImporter.ReasonFor(Reasons, key), null);
                return;
            }

            // -1 is WackysDatabase's "no cap"
            float? cap = node.Peek("maxStationLevelCap");
            if (cap.HasValue && cap.Value >= 0)
            {
                recipe.maxStationLevel = (short)node.Int("maxStationLevelCap", "maxStationLevel");
                // WackysDatabase upgrades a recipe with upgrade_reqs through a second recipe, which its cap never reached
                if (node.Raw("upgrade_reqs") is IList<object> upgrades && upgrades.Count > 0)
                {
                    result.Warnings.Add("maxStationLevelCap: WackysDatabase only capped crafting this recipe, not its upgrade_reqs upgrades. RecipeManager caps the upgrades too");
                }
                if (recipe.minStationLevel.HasValue && recipe.maxStationLevel < recipe.minStationLevel)
                {
                    result.Warnings.Add($"maxStationLevelCap: {recipe.maxStationLevel} is below minStationLevel {recipe.minStationLevel}. RecipeManager never asks for less than minStationLevel");
                }
            }
            else { node.Use("maxStationLevelCap"); }
            // WackysDatabase stops the item being upgraded at all when this is true, with or without upgrade_reqs.
            // That is the item's own max quality, so it goes on the item's entry.
            if (string.Equals(node.Raw("disabledUpgrade")?.ToString().Trim(), "true", System.StringComparison.OrdinalIgnoreCase))
            {
                string item = recipe.prefab ?? ItemOfRecipe(recipe.recipeName);
                if (item == null)
                {
                    node.Drop("disabledUpgrade", "could not tell which item the recipe makes, so its upgrades could not be switched off");
                }
                else
                {
                    ItemAddition entry = ctx.ItemEntryFor(item, result);
                    if (entry.quality == null) { entry.quality = new ItemQuality(); }
                    entry.quality.maxLevel = 1;
                    node.Use("disabledUpgrade", $"-> quality.maxLevel: 1 on {item} in Wacky_Items.yaml");
                }
            }
            else { node.Use("disabledUpgrade"); }

            // A Modify that only switches the recipe off is a Disable
            if (recipe.action == Action.Modify && recipe.enabled == false && recipe.recipe == null && recipe.craftedAt == null && recipe.repairAt == null
                && recipe.minStationLevel == null && recipe.maxStationLevel == null && recipe.craftAmount == null && recipe.listSortWeight == null && recipe.upgradeOnly == null)
            {
                recipe.action = Action.Disable;
                recipe.enabled = null;
            }

            ImportReport.Collect(result, node, key => WackyImporter.ReasonFor(Reasons, key), null);
            string entryKey = ctx.NewKey("recipe", name, RecipeUpdater.RecipesToModify.Keys);
            ctx.Recipes.Add(entryKey, recipe);
            result.Targets.Add($"{entryKey} ({recipe.action}) in Wacky_Recipes.yaml");
        }

        /// <summary>The item a recipe in the game makes, by the recipe's name.</summary>
        private static string ItemOfRecipe(string recipeName)
        {
            if (string.IsNullOrEmpty(recipeName)) { return null; }
            int index = RecipeUpdater.RecipeIndexForRecipeName(recipeName);
            Recipe recipe = index > -1 ? ObjectDB.instance.m_recipes[index] : null;
            return recipe != null && recipe.m_item != null ? recipe.m_item.name : null;
        }

        /// <summary>
        /// reqs, and upgrade_reqs when there are any. With upgrade_reqs WackysDatabase only uses reqs to craft and only
        /// upgrade_reqs to upgrade, which is exactly an ingredient's craftCost and upgradeCost. An upgrade_reqs entry
        /// with a quality costs its amount only when upgrading to that level, which is an entry of upgradeCosts.
        /// </summary>
        /// <param name="maxQuality">The highest level the item can reach, for spelling out a plain cost level by level.</param>
        private static SimpleRecipe Ingredients(WackyImporter ctx, WackyNode node, int maxQuality, ImportReport.FileResult result)
        {
            List<string> reqs = node.StrList("reqs");
            List<string> upgradeReqs = node.StrList("upgrade_reqs");
            if (reqs == null) { return null; }

            List<Ingrediant> ingredients = new List<Ingrediant>();
            for (int i = 0; i < reqs.Count; i++)
            {
                Ingrediant ingredient = ctx.RecipeRequirement(reqs[i], $"reqs[{i}]", result);
                if (ingredient != null) { ingredients.Add(ingredient); }
            }
            node.Converted.Add($"reqs -> recipe.ingredients ({ingredients.Count})");

            if (upgradeReqs != null && upgradeReqs.Count > 0)
            {
                foreach (Ingrediant ingredient in ingredients) { ingredient.upgradeCost = 0; }
                // Costs for one level only, by item
                Dictionary<string, Dictionary<int, int>> levelCosts = new Dictionary<string, Dictionary<int, int>>();
                for (int i = 0; i < upgradeReqs.Count; i++)
                {
                    string text = upgradeReqs[i];
                    if (string.IsNullOrWhiteSpace(text)) { continue; }
                    string where = $"upgrade_reqs[{i}]";
                    string[] parts = text.Split(':');
                    string item = parts[0].Trim();
                    if (!ctx.ItemKnown(item))
                    {
                        result.Warnings.Add($"{where}: no item named {item} exists in this game, WackysDatabase leaves it out too");
                        continue;
                    }
                    int perLevel = parts.Length > 1 ? WackyImporter.ParseInt(parts[1], 1, where, result) : 1;
                    int quality = parts.Length > 2 ? WackyImporter.ParseInt(parts[2], 1, where, result) : 1;
                    Ingrediant same = IngredientFor(ingredients, item);
                    if (quality > 1)
                    {
                        if (!levelCosts.TryGetValue(item, out Dictionary<int, int> costs))
                        {
                            costs = new Dictionary<int, int>();
                            levelCosts.Add(item, costs);
                        }
                        costs[quality] = (costs.TryGetValue(quality, out int already) ? already : 0) + perLevel;
                        continue;
                    }
                    same.upgradeCost = (short)perLevel;
                }
                // An item with costs for single levels lists every level, its plain cost added to each as the game works it out
                foreach (KeyValuePair<string, Dictionary<int, int>> entry in levelCosts)
                {
                    Ingrediant ingredient = IngredientFor(ingredients, entry.Key);
                    int plain = ingredient.upgradeCost ?? 0;
                    if (plain > 0)
                    {
                        Piece.Requirement asGame = new Piece.Requirement { m_amountPerLevel = plain };
                        for (int level = 2; level <= System.Math.Max(maxQuality, entry.Value.Keys.Max()); level++)
                        {
                            entry.Value[level] = (entry.Value.TryGetValue(level, out int levelCost) ? levelCost : 0) + asGame.GetAmount(level);
                        }
                    }
                    ingredient.upgradeCost = null;
                    ingredient.upgradeCosts = entry.Value;
                }
                node.Converted.Add(levelCosts.Count > 0
                    ? "upgrade_reqs -> upgradeCost and upgradeCosts on the ingredients, reqs only pay for crafting"
                    : "upgrade_reqs -> upgradeCost on the ingredients, reqs only pay for crafting");
            }
            else if (upgradeReqs != null)
            {
                node.Use("upgrade_reqs");
            }

            ingredients.RemoveAll(ingredient => ingredient.craftCost == 0 && (ingredient.upgradeCost ?? 0) == 0 && ingredient.upgradeCosts == null);
            // An empty list rather than noRecipeCost: WackysDatabase makes a recipe with no reqs free but keeps its
            // Forge of Potential idol, and so does an empty ingredient list on a Modify
            return new SimpleRecipe { ingredients = ingredients };
        }

        /// <summary>The ingredient for an item, added with no crafting cost when reqs did not list it.</summary>
        private static Ingrediant IngredientFor(List<Ingrediant> ingredients, string item)
        {
            Ingrediant same = ingredients.FirstOrDefault(ingredient => ingredient.prefab == item);
            if (same != null) { return same; }
            same = new Ingrediant
            {
                prefab = item,
                craftCost = 0,
                upgradeCost = 0,
                upgraderResource = WackyImporter.IsUpgrader(item) ? true : (bool?)null
            };
            ingredients.Add(same);
            return same;
        }

        /// <summary>The highest level an item can be upgraded to, from the game, or the game's usual 4.</summary>
        private static int MaxQualityOf(string item)
        {
            GameObject prefab = WackyImporter.FindItem(item);
            int? quality = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_maxQuality : null;
            return quality.HasValue && quality.Value > 1 ? quality.Value : 4;
        }
    }
}

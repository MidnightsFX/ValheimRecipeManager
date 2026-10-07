using HarmonyLib;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager.Patches
{
    /// <summary>
    /// Upgrade costs set for each level an item is upgraded to. The game charges an ingredient's upgrade cost times
    /// the number of levels, so a cost that differs from level to level needs the amount looked up instead.
    /// </summary>
    internal static class RecipeUpgradeCosts
    {
        /// <summary>Requirements built for Recipes.yaml, rebuilt with the recipes.</summary>
        internal static readonly Dictionary<Piece.Requirement, Dictionary<int, int>> FromRecipes = new Dictionary<Piece.Requirement, Dictionary<int, int>>();
        /// <summary>Requirements of the recipes of items this mod adds, which last the whole session.</summary>
        internal static readonly Dictionary<Piece.Requirement, Dictionary<int, int>> FromItems = new Dictionary<Piece.Requirement, Dictionary<int, int>>();
        /// <summary>The entries of the items in <see cref="FromItems"/>. Their recipes stay in the game all session, whatever config is loaded.</summary>
        internal static readonly HashSet<string> ItemUsers = new HashSet<string>();

        internal static readonly PatchFeature Feature = new PatchFeature(
            "RecipeUpgradeCosts",
            "Upgrade costs for each level, set with an ingredient's upgradeCosts.",
            true,
            Users,
            (AccessTools.Method(typeof(Piece.Requirement), nameof(Piece.Requirement.GetAmount)), new HarmonyMethod(typeof(RecipeUpgradeCosts), nameof(GetAmountPrefix)), null));

        private static IEnumerable<string> Users()
        {
            foreach (KeyValuePair<string, RecipeModification> entry in RecipeUpdater.RecipesToModify)
            {
                if (RecipePatchUsers.Builds(entry.Value) && HasCosts(entry.Value.recipe?.ingredients)) { yield return entry.Key; }
            }
            foreach (KeyValuePair<string, ItemAddition> entry in ItemUpdater.ItemDefinitions)
            {
                if (RecipePatchUsers.Builds(entry.Value) && HasCosts(entry.Value.recipe?.ingredients)) { yield return entry.Key; }
            }
            foreach (string entry in ItemUsers) { yield return entry; }
        }

        private static bool HasCosts(List<Ingrediant> ingredients)
        {
            return ingredients != null && ingredients.Any(ingredient => ingredient?.upgradeCosts != null && ingredient.upgradeCosts.Count > 0);
        }

        /// <summary>The cost of upgrading to a level, for a requirement with costs set per level.</summary>
        private static bool GetAmountPrefix(Piece.Requirement __instance, int qualityLevel, ref int __result)
        {
            // Crafting, as opposed to upgrading, is left to the game
            if (qualityLevel <= 1) { return true; }
            if (!FromRecipes.TryGetValue(__instance, out Dictionary<int, int> costs) && !FromItems.TryGetValue(__instance, out costs)) { return true; }
            if (!costs.TryGetValue(qualityLevel, out int amount))
            {
                // A Forge of Potential idol costs its craft cost for every upgrade, a level with none set keeps that
                if (__instance.m_upgraderResource) { return true; }
                amount = 0;
            }
            __result = amount;
            return false;
        }

        /// <summary>
        /// Records the costs an ingredient sets for the requirement built from it, once the requirement knows whether it
        /// is an idol. Checked against what the patch can do: a level below 2 is crafting, not upgrading.
        /// </summary>
        /// <returns>Whether there were costs to record.</returns>
        internal static bool Register(Dictionary<Piece.Requirement, Dictionary<int, int>> registry, Piece.Requirement requirement, Ingrediant ingredient, string entry)
        {
            if (requirement == null || ingredient?.upgradeCosts == null || ingredient.upgradeCosts.Count == 0) { return false; }
            Dictionary<int, int> costs = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> cost in ingredient.upgradeCosts)
            {
                if (cost.Key < 2)
                {
                    Logger.LogWarning($"{entry} gives {ingredient.prefab} an upgrade cost for level {cost.Key}, but upgrades start at level 2, it is ignored. Use craftCost for crafting.");
                    continue;
                }
                if (requirement.m_upgraderResource && cost.Value <= 0)
                {
                    Logger.LogWarning($"{entry} makes upgrading to level {cost.Key} at the Forge of Potential cost no {ingredient.prefab}, so that upgrade is free.");
                }
                costs[cost.Key] = Math.Max(0, cost.Value);
            }
            if (ingredient.upgradeCost.HasValue && ingredient.upgradeCost.Value != 0)
            {
                Logger.LogWarning($"{entry} gives {ingredient.prefab} both an upgradeCost and upgradeCosts. upgradeCosts decides what each upgrade costs, upgradeCost is ignored.");
            }
            registry[requirement] = costs;
            return true;
        }

        /// <summary>The costs set for a requirement, for the recipe dump.</summary>
        internal static Dictionary<int, int> CostsOf(Piece.Requirement requirement)
        {
            if (FromRecipes.TryGetValue(requirement, out Dictionary<int, int> costs) || FromItems.TryGetValue(requirement, out costs))
            {
                return new Dictionary<int, int>(costs);
            }
            return null;
        }
    }

    /// <summary>
    /// A cap on the station level a recipe asks for. The game asks for one more station level for each upgrade, so
    /// an item with more upgrade levels than the station has can never be fully upgraded without it.
    /// </summary>
    internal static class RecipeStationLevelCap
    {
        internal static readonly Dictionary<Recipe, int> FromRecipes = new Dictionary<Recipe, int>();
        internal static readonly Dictionary<Recipe, int> FromItems = new Dictionary<Recipe, int>();
        /// <summary>The entries of the items in <see cref="FromItems"/>. Their recipes stay in the game all session, whatever config is loaded.</summary>
        internal static readonly HashSet<string> ItemUsers = new HashSet<string>();

        // GetRequiredStationLevel is small enough to be copied into the code that calls it when that code is compiled.
        // Patching it stops that for code compiled later, and patching its two callers as well compiles them again,
        // for when the patch goes on after they have already run.
        private static readonly HarmonyMethod Recompile = new HarmonyMethod(typeof(RecipeStationLevelCap), nameof(RecompilePostfix));

        internal static readonly PatchFeature Feature = new PatchFeature(
            "RecipeStationLevelCap",
            "The highest station level a recipe needs, set with a recipe's maxStationLevel.",
            true,
            Users,
            (AccessTools.Method(typeof(Recipe), nameof(Recipe.GetRequiredStationLevel)), null, new HarmonyMethod(typeof(RecipeStationLevelCap), nameof(GetRequiredStationLevelPostfix))),
            (AccessTools.Method(typeof(Player), nameof(Player.RequiredCraftingStation)), null, Recompile),
            (AccessTools.Method(typeof(InventoryGui), "UpdateRecipe"), null, Recompile));

        private static IEnumerable<string> Users()
        {
            foreach (KeyValuePair<string, RecipeModification> entry in RecipeUpdater.RecipesToModify)
            {
                if (RecipePatchUsers.Builds(entry.Value) && entry.Value.maxStationLevel != null) { yield return entry.Key; }
            }
            foreach (KeyValuePair<string, ItemAddition> entry in ItemUpdater.ItemDefinitions)
            {
                if (RecipePatchUsers.Builds(entry.Value) && entry.Value.recipe?.maxStationLevel != null) { yield return entry.Key; }
            }
            foreach (string entry in ItemUsers) { yield return entry; }
        }

        private static void RecompilePostfix() { }

        private static void GetRequiredStationLevelPostfix(Recipe __instance, ref int __result)
        {
            if (!FromRecipes.TryGetValue(__instance, out int cap) && !FromItems.TryGetValue(__instance, out cap)) { return; }
            // Never below what crafting the item needs
            int floor = Math.Max(1, __instance.m_minStationLevel);
            __result = Math.Min(__result, Math.Max(cap, floor));
        }

        /// <summary>Records a recipe's cap, checked against the level crafting it needs.</summary>
        /// <returns>Whether there was a cap to record.</returns>
        internal static bool Register(Dictionary<Recipe, int> registry, Recipe recipe, short? cap, string entry)
        {
            if (recipe == null || !cap.HasValue) { return false; }
            if (cap.Value < Math.Max(1, recipe.m_minStationLevel))
            {
                Logger.LogWarning($"{entry} caps the station level at {cap.Value}, below the level of {recipe.m_minStationLevel} crafting it needs. Upgrades will need level {recipe.m_minStationLevel}.");
            }
            registry[recipe] = cap.Value;
            return true;
        }

        internal static short? CapOf(Recipe recipe)
        {
            if (recipe == null) { return null; }
            if (FromRecipes.TryGetValue(recipe, out int cap) || FromItems.TryGetValue(recipe, out cap)) { return (short)cap; }
            return null;
        }
    }

    /// <summary>Which entries build a recipe, the only ones whose recipe settings can need a patch.</summary>
    internal static class RecipePatchUsers
    {
        internal static bool Builds(RecipeModification mod)
        {
            return mod != null && (mod.action == DataObjects.Action.Modify || mod.action == DataObjects.Action.Add);
        }

        /// <summary>Only an added item gets a recipe from its entry, a Modify leaves the item's recipe to Recipes.yaml.</summary>
        internal static bool Builds(ItemAddition item)
        {
            return item != null && item.action == ItemAction.Add;
        }
    }
}

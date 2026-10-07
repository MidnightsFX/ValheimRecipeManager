using Jotunn.Managers;
using RecipeManager.Common;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    /// <summary>
    /// Builds the recipe a Modify or an Add entry describes. A Modify starts from a copy of the recipe it replaces
    /// and only overwrites what the entry sets, so everything it leaves out keeps that recipe's value. An Add starts
    /// from a new recipe with the game's defaults.
    /// </summary>
    internal static class RecipeBuilder
    {
        /// <param name="key">The entry's name, for messages.</param>
        /// <param name="original">The recipe a Modify replaces, null for an Add.</param>
        /// <param name="problem">Why the recipe could not be built, when it returns null.</param>
        internal static Recipe Build(string key, RecipeModification mod, Recipe original, out string problem)
        {
            problem = null;
            bool modify = mod.action == DataObjects.Action.Modify;
            if (modify && original == null)
            {
                problem = $"no recipe for {mod.recipeName ?? mod.prefab} exists to modify. RM_Recipes_PrintAll lists the recipes that do";
                return null;
            }
            if (!modify && mod.recipe == null)
            {
                problem = "an Add needs a recipe block with its ingredients, or noRecipeCost: true for a free recipe";
                return null;
            }

            ItemDrop item = modify ? original.m_item : null;
            if (!string.IsNullOrEmpty(mod.prefab))
            {
                item = ResolveItem(mod.prefab);
                if (item == null)
                {
                    problem = $"there is no item named {mod.prefab} for it to craft. Prefab names are case sensitive";
                    return null;
                }
            }
            if (item == null)
            {
                problem = "it names no item to craft, set its prefab";
                return null;
            }

            if (!BuildRequirements(key, mod.recipe, modify, modify ? original.m_resources : null, out Piece.Requirement[] resources, out problem))
            {
                return null;
            }

            // Instantiating copies every field, the requirements included, so the original is never written to
            Recipe recipe = modify ? Object.Instantiate(original) : ScriptableObject.CreateInstance<Recipe>();
            recipe.name = modify ? original.name : (string.IsNullOrEmpty(mod.recipeName) ? $"Recipe_{key}" : mod.recipeName);
            recipe.m_item = item;
            recipe.m_amount = mod.craftAmount ?? (modify ? original.m_amount : 1);
            recipe.m_minStationLevel = mod.minStationLevel ?? (modify ? original.m_minStationLevel : 1);
            recipe.m_requireOnlyOneIngredient = mod.recipe?.anyOneResource ?? (modify && original.m_requireOnlyOneIngredient);
            recipe.m_craftingStation = Station(key, "craftedAt", mod.craftedAt, modify ? original.m_craftingStation : null, modify);
            recipe.m_repairStation = Station(key, "repairAt", mod.repairAt, modify ? original.m_repairStation : null, modify);
            RecipeFields.Resolve(mod, modify ? RecipeFields.RecipeSettings.From(original) : (RecipeFields.RecipeSettings?)null).ApplyTo(recipe);
            if (resources != null) { recipe.m_resources = resources; }
            else if (!modify) { recipe.m_resources = new Piece.Requirement[0]; }
            else if (recipe.m_resources != null && original.m_resources != null && recipe.m_resources.Length == original.m_resources.Length)
            {
                // The copy has its own requirement objects, in the same order
                for (int i = 0; i < recipe.m_resources.Length; i++) { CarryCosts(original.m_resources[i], recipe.m_resources[i]); }
            }
            return recipe;
        }

        /// <summary>
        /// Costs per level the modified recipe has for a requirement, kept on the one built in its place. An item this
        /// mod adds can set them on its recipe, and a Modify of that recipe keeps them like any other value.
        /// </summary>
        private static void CarryCosts(Piece.Requirement from, Piece.Requirement to)
        {
            if (from == null || to == null) { return; }
            Dictionary<int, int> costs = Patches.RecipeUpgradeCosts.CostsOf(from);
            if (costs != null) { Patches.RecipeUpgradeCosts.FromRecipes[to] = costs; }
        }

        /// <summary>
        /// The requirements an entry lists. <paramref name="resources"/> is left null when the entry keeps the
        /// requirements of the recipe it modifies.
        /// </summary>
        private static bool BuildRequirements(string key, SimpleRecipe recipe, bool modify, Piece.Requirement[] original, out Piece.Requirement[] resources, out string problem)
        {
            resources = null;
            problem = null;
            if (recipe == null) { return true; }
            if (recipe.noRecipeCost)
            {
                resources = new Piece.Requirement[0];
                return true;
            }
            if (recipe.ingredients == null)
            {
                if (modify) { return true; }
                problem = "its recipe lists no ingredients. Set noRecipeCost: true if it is meant to be free";
                return false;
            }

            List<Piece.Requirement> built = new List<Piece.Requirement>();
            // Kept in step with built, ApplyRequirements reads them in the same order
            List<Ingrediant> sources = new List<Ingrediant>();
            foreach (Ingrediant ingredient in recipe.ingredients)
            {
                if (ingredient == null || string.IsNullOrEmpty(ingredient.prefab))
                {
                    Logger.LogWarning($"Recipe modification '{key}' has an ingredient with no prefab set, it will be ignored.");
                    continue;
                }
                ItemDrop item = ResolveItem(ingredient.prefab);
                if (item == null)
                {
                    problem = $"its ingredient {ingredient.prefab} is not an item that exists. Prefab names are case sensitive";
                    return false;
                }
                // An amount left out is the one the recipe already asks for this item
                Piece.Requirement same = RecipeFields.FindRequirement(original, ingredient.prefab, RequirementItemName);
                int amount = ingredient.craftCost ?? same?.m_amount ?? 0;
                int perLevel = ingredient.upgradeCost ?? same?.m_amountPerLevel ?? 0;
                bool costsPerLevel = ingredient.upgradeCosts != null && ingredient.upgradeCosts.Count > 0;
                if (amount == 0 && perLevel == 0 && !costsPerLevel)
                {
                    Logger.LogWarning($"Recipe modification '{key}' asks for no {ingredient.prefab} to craft or to upgrade, so that ingredient is left out.");
                    continue;
                }
                Piece.Requirement requirement = new Piece.Requirement { m_resItem = item, m_amount = amount, m_amountPerLevel = perLevel };
                built.Add(requirement);
                sources.Add(ingredient);
            }
            resources = RecipeFields.ApplyRequirements(built.ToArray(), sources, original, RequirementItemName);
            // After ApplyRequirements, which decides which of them are Forge of Potential idols
            for (int i = 0; i < built.Count; i++)
            {
                Ingrediant ingredient = sources[i];
                if (ingredient.upgradeCosts != null && ingredient.upgradeCosts.Count > 0)
                {
                    Patches.RecipeUpgradeCosts.Register(Patches.RecipeUpgradeCosts.FromRecipes, resources[i], ingredient, $"Recipe modification '{key}'");
                }
                else if (ingredient.upgradeCosts == null && ingredient.upgradeCost == null)
                {
                    CarryCosts(RecipeFields.FindRequirement(original, ingredient.prefab, RequirementItemName), resources[i]);
                }
            }
            // Idols kept from the recipe being modified are copies of its own
            for (int i = built.Count; i < resources.Length; i++)
            {
                CarryCosts(RecipeFields.FindRequirement(original, RequirementItemName(resources[i]), RequirementItemName), resources[i]);
            }
            if (ValConfig.EnableDebugMode.Value && resources.Length > built.Count)
            {
                Logger.LogInfo($"{key} lists no upgrader resource, keeping the Forge of Potential cost of the recipe it modifies.");
            }
            return true;
        }

        /// <summary>
        /// The station a craftedAt or repairAt value names. Left out keeps <paramref name="current"/>, which is
        /// null for an Add, and 'none' or an empty value clears it, as an empty value always has.
        /// </summary>
        private static CraftingStation Station(string key, string field, string configured, CraftingStation current, bool modify)
        {
            if (configured == null) { return current; }
            if (configured.Trim().Length == 0 || StationRef.IsNone(configured)) { return null; }
            CraftingStation station = StationRef.Resolve(configured);
            if (station == null)
            {
                Logger.LogWarning($"Recipe modification '{key}' has {field}: {configured}, which is not a crafting station. {(modify ? "The recipe keeps the station it has" : "The recipe will be handcrafted")}. Use the station's prefab name, such as piece_workbench or forge.");
                return current;
            }
            return station;
        }

        /// <summary>An item from the object DB, or one another mod registered through Jotunn that is not in it yet.</summary>
        internal static ItemDrop ResolveItem(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) { return null; }
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            if (go == null) { go = PrefabManager.Instance.GetPrefab(prefabName); }
            return go != null ? go.GetComponent<ItemDrop>() : null;
        }

        internal static string RequirementItemName(Piece.Requirement requirement)
        {
            return requirement.m_resItem != null ? requirement.m_resItem.name : null;
        }
    }
}

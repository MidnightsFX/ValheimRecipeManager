using RecipeManager.Common;
using System;
using System.Collections.Generic;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager
{
    /// <summary>
    /// The Recipe and Piece.Requirement fields that Jotunn's RecipeConfig and RequirementConfig cannot carry.
    /// A recipe rebuilt through those configs gets the game's defaults for all of them. That is how a modified
    /// armor or weapon lost its Forge of Potential idol, and how the Deep North gold gear became craftable.
    ///
    /// Reading and writing a Recipe's own fields is all this does with the game. Item names are passed in, so it
    /// never needs the Unity runtime and can be run outside the game.
    /// </summary>
    internal static class RecipeFields
    {
        // Recipe's own field initialisers, which is what a recipe built from a RecipeConfig starts with
        internal const int DefaultListSortWeight = 100;
        internal const float DefaultQualityResultAmountMultiplier = 1f;

        /// <summary>The recipe-wide values, as read off a recipe or resolved for one being built.</summary>
        internal struct RecipeSettings
        {
            public bool Enabled;
            public bool UpgradeOnly;
            public int ListSortWeight;
            public float QualityResultAmountMultiplier;

            public static RecipeSettings From(Recipe recipe)
            {
                return new RecipeSettings {
                    Enabled = recipe.m_enabled,
                    UpgradeOnly = recipe.m_noCraftOnlyUpgrade,
                    ListSortWeight = recipe.m_listSortWeight,
                    QualityResultAmountMultiplier = recipe.m_qualityResultAmountMultiplier
                };
            }

            public void ApplyTo(Recipe recipe)
            {
                recipe.m_enabled = Enabled;
                recipe.m_noCraftOnlyUpgrade = UpgradeOnly;
                recipe.m_listSortWeight = ListSortWeight;
                recipe.m_qualityResultAmountMultiplier = QualityResultAmountMultiplier;
            }
        }

        /// <summary>
        /// What a recipe built from <paramref name="mod"/> should have. A value the entry leaves out comes from
        /// <paramref name="modified"/>, the recipe a Modify replaces, so entries written before these fields existed
        /// keep that recipe's behaviour. Pass null when adding a recipe, which starts from the game's defaults.
        /// </summary>
        internal static RecipeSettings Resolve(RecipeModification mod, RecipeSettings? modified)
        {
            return new RecipeSettings {
                // Not copied like the rest: a modified recipe has always been switched on unless the entry disables
                // it, and configs written for earlier versions use that to bring back recipes the game ships off.
                Enabled = mod.enabled ?? mod.action != DataObjects.Action.Disable,
                UpgradeOnly = mod.upgradeOnly ?? modified?.UpgradeOnly ?? false,
                ListSortWeight = mod.listSortWeight ?? modified?.ListSortWeight ?? DefaultListSortWeight,
                QualityResultAmountMultiplier = mod.qualityResultAmountMultiplier ?? modified?.QualityResultAmountMultiplier ?? DefaultQualityResultAmountMultiplier
            };
        }

        /// <summary>
        /// Writes the recipe-wide values onto a dumped entry. Values that match what leaving them out would give are
        /// left out, which keeps the dump readable: a Modify copies them from the recipe either way.
        /// </summary>
        internal static void Capture(RecipeSettings settings, RecipeModification into)
        {
            into.enabled = settings.Enabled ? (bool?)null : false;
            into.upgradeOnly = settings.UpgradeOnly ? true : (bool?)null;
            into.listSortWeight = settings.ListSortWeight != DefaultListSortWeight ? settings.ListSortWeight : (int?)null;
            into.qualityResultAmountMultiplier = settings.QualityResultAmountMultiplier != DefaultQualityResultAmountMultiplier ? settings.QualityResultAmountMultiplier : (float?)null;
        }

        /// <summary>Writes a requirement's fields onto a dumped ingredient, leaving out the ones that are the default.</summary>
        internal static void Capture(Piece.Requirement requirement, Ingrediant into)
        {
            into.upgraderResource = requirement.m_upgraderResource ? true : (bool?)null;
            into.recover = requirement.m_recover != DefaultRecover(requirement.m_upgraderResource) ? requirement.m_recover : (bool?)null;
            into.anyOneExtraAmount = requirement.m_extraAmountOnlyOneIngredient != 0 ? requirement.m_extraAmountOnlyOneIngredient : (int?)null;
        }

        /// <summary>
        /// Sets the fields RequirementConfig cannot on each built requirement, then makes sure a modified recipe that
        /// could be upgraded at the Forge of Potential still can be.
        /// </summary>
        /// <param name="built">Built from <paramref name="ingredients"/>, one requirement per ingredient and in the same order.</param>
        /// <param name="modified">The requirements of the recipe a Modify replaces, null when adding a recipe.</param>
        /// <param name="itemName">The prefab name of a requirement's item.</param>
        /// <returns>The requirements to use. This can be longer than <paramref name="built"/>.</returns>
        internal static Piece.Requirement[] ApplyRequirements(Piece.Requirement[] built, IList<Ingrediant> ingredients, Piece.Requirement[] modified, Func<Piece.Requirement, string> itemName)
        {
            bool hasUpgrader = false;
            for (int i = 0; i < built.Length; i++)
            {
                Ingrediant ingredient = ingredients[i];
                // An unset field is copied from the modified recipe's requirement for the same item. Older dumps
                // list the idol as an ordinary ingredient, and this is what keeps it an idol.
                Piece.Requirement same = FindRequirement(modified, ingredient.prefab, itemName);
                bool upgrader = ingredient.upgraderResource ?? same?.m_upgraderResource ?? false;
                built[i].m_upgraderResource = upgrader;
                built[i].m_recover = ingredient.recover ?? same?.m_recover ?? DefaultRecover(upgrader);
                built[i].m_extraAmountOnlyOneIngredient = ingredient.anyOneExtraAmount ?? same?.m_extraAmountOnlyOneIngredient ?? 0;
                hasUpgrader |= upgrader;
            }
            if (hasUpgrader || modified == null) { return built; }

            // The forge only lists items whose recipe has an upgrader resource. A modification that leaves the idol
            // out keeps the one the recipe already had, rather than taking the item off the forge.
            List<Piece.Requirement> requirements = new List<Piece.Requirement>(built);
            foreach (Piece.Requirement requirement in modified)
            {
                if (requirement != null && requirement.m_upgraderResource) { requirements.Add(Copy(requirement)); }
            }
            return requirements.ToArray();
        }

        // The game's own recipes refund every ordinary ingredient when the forge breaks an item, and never the idol
        private static bool DefaultRecover(bool upgraderResource)
        {
            return !upgraderResource;
        }

        private static Piece.Requirement FindRequirement(Piece.Requirement[] requirements, string prefab, Func<Piece.Requirement, string> itemName)
        {
            if (requirements == null || prefab == null) { return null; }
            foreach (Piece.Requirement requirement in requirements)
            {
                if (requirement != null && itemName(requirement) == prefab) { return requirement; }
            }
            return null;
        }

        // A copy, not the modified recipe's own object. Reforged Potential rewrites the idol requirement of whatever
        // is selected at the forge, and that should not carry into the recipe a revert puts back.
        private static Piece.Requirement Copy(Piece.Requirement requirement)
        {
            return new Piece.Requirement {
                m_resItem = requirement.m_resItem,
                m_amount = requirement.m_amount,
                m_extraAmountOnlyOneIngredient = requirement.m_extraAmountOnlyOneIngredient,
                m_amountPerLevel = requirement.m_amountPerLevel,
                m_upgraderResource = requirement.m_upgraderResource,
                m_recover = requirement.m_recover
            };
        }
    }
}

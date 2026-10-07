using HarmonyLib;
using RecipeManager.Common;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager.Patches
{
    /// <summary>
    /// Maximum health, stamina and eitr from a status effect. The game works these out from the player's base values
    /// and food every second, and status effects have no setting of their own for them.
    /// </summary>
    internal static class StatusEffectStatBonus
    {
        internal static readonly PatchFeature Feature = new PatchFeature(
            "StatusEffectStatBonus",
            "Maximum health, stamina and eitr from a status effect's maxHealth, maxStamina and maxEitr.",
            true,
            Users,
            (AccessTools.Method(typeof(Player), "GetTotalFoodValue"), null, new HarmonyMethod(typeof(StatusEffectStatBonus), nameof(GetTotalFoodValuePostfix))));

        private static IEnumerable<string> Users()
        {
            foreach (KeyValuePair<string, StatusEffectDefinition> entry in StatusEffectUpdater.Definitions)
            {
                StatusEffectDefinition def = entry.Value;
                if (def != null && (def.maxHealth != null || def.maxStamina != null || def.maxEitr != null)) { yield return entry.Key; }
            }
        }

        private static void GetTotalFoodValuePostfix(Player __instance, ref float hp, ref float stamina, ref float eitr)
        {
            if (StatusEffectUpdater.StatBonuses.Count == 0) { return; }
            List<StatusEffect> active = __instance.GetSEMan()?.GetStatusEffects();
            if (active == null) { return; }
            foreach (StatusEffect effect in active)
            {
                if (effect == null || !StatusEffectUpdater.StatBonuses.TryGetValue(effect.NameHash(), out Vector3 bonus)) { continue; }
                hp += bonus.x;
                stamina += bonus.y;
                eitr += bonus.z;
            }
            // A negative bonus can never take the player below one health, or below nothing
            hp = Mathf.Max(1f, hp);
            stamina = Mathf.Max(0f, stamina);
            eitr = Mathf.Max(0f, eitr);
        }
    }
}

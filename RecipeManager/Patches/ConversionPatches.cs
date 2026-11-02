using HarmonyLib;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager.Patches
{
    /// <summary>
    /// How many items a smelter type station makes from one input. The game's conversions for these have no amount and
    /// make one, so what comes out is scaled at the one method all of it passes through. The station's queues keep counting
    /// inputs, so nothing about this reaches a save. Not client only: whoever owns a station runs it, which can be a
    /// dedicated server.
    /// </summary>
    internal static class SmelterOutputAmount
    {
        internal static readonly PatchFeature Feature = new PatchFeature(
            "SmelterOutputAmount",
            "How many items a smelter type station makes from one input, set with a conversion's amount.",
            false,
            Users,
            (AccessTools.Method(typeof(Smelter), nameof(Smelter.Spawn)), new HarmonyMethod(typeof(SmelterOutputAmount), nameof(SpawnPrefix)), null),
            (AccessTools.Method(typeof(Smelter), nameof(Smelter.GetProcessedQueueSize)), null, new HarmonyMethod(typeof(SmelterOutputAmount), nameof(GetProcessedQueueSizePostfix))));

        private static bool spawningFullStack;

        // Read from the tracked entries rather than the loaded configs: fermenters use amount as well, and which kind of
        // station a config names is only known once the prefabs exist
        private static IEnumerable<string> Users()
        {
            foreach (TrackedConversion conversion in ConversionUpdater.TrackedConversions)
            {
                if (conversion.convertType == ConversionType.Smelter && conversion.action != ConversionAction.Remove
                    && conversion.conversions != null && conversion.conversions.Exists(c => c.amount > 1))
                {
                    yield return conversion.key;
                }
            }
        }

        /// <summary>
        /// The game does not limit the stack it is given here, and an inventory takes a dropped stack into one slot whatever
        /// its size. So everything over a full stack comes out as more calls to this method, and the original makes the rest.
        /// </summary>
        private static void SpawnPrefix(Smelter __instance, string ore, ref int stack)
        {
            if (spawningFullStack) { return; }
            Smelter.ItemConversion conversion = __instance.GetItemConversion(ore);
            int amount = ConversionUpdater.SmelterAmount(__instance, conversion);
            if (amount <= 1 || conversion.m_to == null) { return; }
            int maxStack = Math.Max(1, conversion.m_to.m_itemData.m_shared.m_maxStackSize);
            long remaining = (long)stack * amount;
            spawningFullStack = true;
            try
            {
                for (; remaining > maxStack; remaining -= maxStack) { __instance.Spawn(ore, maxStack); }
            }
            finally
            {
                spawningFullStack = false;
            }
            stack = (int)remaining;
        }

        /// <summary>What a spawnStack station holds is counted in inputs, the player reads this as how many items are ready.</summary>
        private static void GetProcessedQueueSizePostfix(Smelter __instance, ref int __result)
        {
            if (__result <= 0) { return; }
            string ore = __instance.m_nview.GetZDO().GetString(ZDOVars.s_spawnOre);
            __result *= ConversionUpdater.SmelterAmount(__instance, __instance.GetItemConversion(ore));
        }
    }
}

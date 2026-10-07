using HarmonyLib;
using RecipeManager.Common;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager.Patches
{
    /// <summary>
    /// A crafting station level needed to place a piece. The game only asks for the station itself, so the level is
    /// checked after it: a piece is unknown until a station of that level has been seen, and can only be placed near one.
    /// </summary>
    internal static class PieceStationLevel
    {
        /// <summary>The level each piece needs, by the piece prefab's own Piece component.</summary>
        internal static readonly Dictionary<Piece, int> Levels = new Dictionary<Piece, int>();

        internal static readonly PatchFeature Feature = new PatchFeature(
            "PieceStationLevel",
            "The station level needed to place a piece, set with a piece's minStationLevel.",
            true,
            Users,
            (AccessTools.Method(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) }), null,
                new HarmonyMethod(typeof(PieceStationLevel), nameof(HaveRequirementsPostfix))));

        private static IEnumerable<string> Users()
        {
            foreach (KeyValuePair<string, PieceModification> entry in PieceUpdater.PiecesToModify)
            {
                PieceModification def = entry.Value;
                if (def != null && (def.action == PieceAction.Modify || def.action == PieceAction.Add) && def.minStationLevel != null)
                {
                    yield return entry.Key;
                }
            }
        }

        private static void HaveRequirementsPostfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            if (!__result || piece == null || piece.m_craftingStation == null || Levels.Count == 0) { return; }
            if (!Levels.TryGetValue(piece, out int level) || level <= 1) { return; }
            string station = piece.m_craftingStation.m_name;
            if (mode == Player.RequirementMode.IsKnown || mode == Player.RequirementMode.CanAlmostBuild)
            {
                // The highest level of the station the player has seen
                __result = __instance.m_knownStations.TryGetValue(station, out int known) && known >= level;
                return;
            }
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench)) { return; }
            __result = StationInRange(station, __instance.transform.position, level);
        }

        /// <summary>Whether a station of that name and at least that level is close enough to build next to.</summary>
        private static bool StationInRange(string name, Vector3 point, int level)
        {
            foreach (CraftingStation station in CraftingStation.m_allStations)
            {
                if (station == null || station.m_name != name) { continue; }
                Vector3 at = point;
                at.y = station.transform.position.y;
                if (Vector3.Distance(station.transform.position, at) < station.GetStationBuildRange() && station.GetLevel() >= level) { return true; }
            }
            return false;
        }
    }
}

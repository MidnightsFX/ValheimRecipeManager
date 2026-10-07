using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecipeManager.Common
{
    /// <summary>
    /// Turns a crafting station as written in a config into the station itself. A station can be given by its
    /// prefab name (piece_workbench), by the name the game shows for it ($piece_workbench, the form WackysDatabase
    /// writes), or loosely ("Workbench", which earlier versions of the README used).
    /// </summary>
    internal static class StationRef
    {
        /// <summary>The value that removes a station: handcrafted for a recipe, no station needed for a piece.</summary>
        internal const string None = "none";

        private static Dictionary<string, CraftingStation> byToken;
        private static int indexedPrefabCount = -1;

        /// <summary>Whether a configured value asks for no station at all.</summary>
        internal static bool IsNone(string value)
        {
            return value != null && string.Equals(value.Trim(), None, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The station a value names. Null and logs nothing when it names none, so the caller can say what it does
        /// about it. Never call this for an empty value or for <see cref="None"/>.
        /// </summary>
        internal static CraftingStation Resolve(string value)
        {
            if (string.IsNullOrEmpty(value)) { return null; }
            string name = value.Trim();

            CraftingStation station = FromPrefab(name);
            if (station != null) { return station; }

            Dictionary<string, CraftingStation> tokens = TokenIndex();
            if (tokens.TryGetValue(name, out station)) { return station; }
            if (!name.StartsWith("$", StringComparison.Ordinal))
            {
                if (tokens.TryGetValue("$" + name, out station)) { return station; }
                if (tokens.TryGetValue("$piece_" + name, out station)) { return station; }
                station = FromPrefab("piece_" + name);
                if (station != null) { return station; }
            }
            return null;
        }

        /// <summary>The name a config should use for a station, its prefab name.</summary>
        internal static string NameOf(CraftingStation station)
        {
            return station == null ? null : PrefabNameOf(station.gameObject);
        }

        /// <summary>
        /// The name of the prefab an object is, or was placed from. Only the "(Clone)" Unity adds to a copy is taken
        /// off. The game's own Utils.GetPrefabName also cuts at the first space, which would shorten a mod's prefab
        /// named "My Kiln" to "My".
        /// </summary>
        internal static string PrefabNameOf(GameObject go)
        {
            const string clone = "(Clone)";
            string name = go.name;
            return name.EndsWith(clone, StringComparison.Ordinal) ? name.Substring(0, name.Length - clone.Length) : name;
        }

        private static CraftingStation FromPrefab(string name)
        {
            GameObject go = PrefabManager.Instance.GetPrefab(name);
            if (go == null && ZNetScene.instance != null)
            {
                // Prefab names are case sensitive everywhere else, a station named with the wrong case is still unambiguous
                foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
                {
                    if (prefab != null && string.Equals(prefab.name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        go = prefab;
                        break;
                    }
                }
            }
            return go != null ? go.GetComponent<CraftingStation>() : null;
        }

        /// <summary>Every station by its $ name, case insensitive. Rebuilt when another mod has added prefabs since.</summary>
        private static Dictionary<string, CraftingStation> TokenIndex()
        {
            int prefabCount = ZNetScene.instance != null ? ZNetScene.instance.m_prefabs.Count : -1;
            if (byToken != null && prefabCount == indexedPrefabCount && prefabCount >= 0) { return byToken; }

            byToken = new Dictionary<string, CraftingStation>(StringComparer.OrdinalIgnoreCase);
            indexedPrefabCount = prefabCount;
            IEnumerable<CraftingStation> stations;
            if (ZNetScene.instance != null)
            {
                List<CraftingStation> found = new List<CraftingStation>();
                foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
                {
                    CraftingStation station = prefab != null ? prefab.GetComponent<CraftingStation>() : null;
                    if (station != null) { found.Add(station); }
                }
                stations = found;
            }
            else
            {
                // No world loaded yet. This also finds stations placed in the world, which carry the same names.
                stations = Resources.FindObjectsOfTypeAll<CraftingStation>();
            }
            foreach (CraftingStation station in stations)
            {
                if (station == null || string.IsNullOrEmpty(station.m_name)) { continue; }
                if (station.gameObject.name.EndsWith("(Clone)", StringComparison.Ordinal)) { continue; }
                // The first prefab wins, several mods copy the game's stations and keep their $ name
                if (!byToken.ContainsKey(station.m_name)) { byToken.Add(station.m_name, station); }
            }
            return byToken;
        }
    }
}

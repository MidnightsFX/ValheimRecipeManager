using Jotunn.Managers;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    internal class ConversionUpdater
    {
        // The game's own initialisers for a conversion that sets nothing
        internal const int DefaultFermenterAmount = 4;
        internal const float DefaultCookTime = 10f;

        public static Dictionary<String, ConversionModification> ConversionsToModify = new Dictionary<String, ConversionModification>();
        public static List<TrackedConversion> TrackedConversions = new List<TrackedConversion>();

        /// <summary>
        /// What each station looked like before this mod first wrote to it, keyed by GameObject instance
        /// id. A config entry applies to the prefab and to every copy of it placed in the world, so this is
        /// per object rather than per entry. Kept for the life of the process so a re-apply never records
        /// an already modified station as the original.
        /// </summary>
        private static readonly Dictionary<int, ConversionSnapshot> OriginalConversions = new Dictionary<int, ConversionSnapshot>();

        /// <summary>
        /// The first snapshot of each prefab itself, by prefab name. A station placed in the world after the prefab
        /// was modified is a copy of the modified prefab, so its original is the prefab's, not what it holds.
        /// </summary>
        private static readonly Dictionary<string, ConversionSnapshot> OriginalByPrefab = new Dictionary<string, ConversionSnapshot>();

        private static bool warnedOverCookedTime;

        /// <summary>Hooked to every new object DB in a world.</summary>
        public static void InitialSychronization() {
            // Station prefabs outlive the world, put back what the previous one's configuration changed first
            RevertConversionModifications();
            BuildConversionTracker();
            ConversionUpdateRunner();
        }

        /// <summary>Undoes the modifications applied, then tracks and applies the configured ones.</summary>
        public static void Reapply() {
            RevertConversionModifications();
            BuildConversionTracker();
            ConversionUpdateRunner();
        }

        public static void ConversionUpdateRunner() {
            Logger.LogInfo($"Applying {TrackedConversions.Count} conversion modifications");
            if (TrackedConversions.Count == 0) { return; }
            Dictionary<string, List<GameObject>> stations = IndexStations();
            foreach (TrackedConversion conv in TrackedConversions) {
                ApplyConversionModifications(conv, stations);
            }
        }

        private static void ApplyConversionModifications(TrackedConversion conv, Dictionary<string, List<GameObject>> stations) {
            if (conv == null || conv.prefab == null) {
                Logger.LogWarning("A tracked conversion has no target prefab, this modification will be skipped.");
                return;
            }
            Logger.LogDebug($"Applying conversion ({conv.prefab.name}) modification action: {conv.action}");

            if (!stations.TryGetValue(conv.prefab.name, out List<GameObject> objects)) {
                Logger.LogWarning($"No stations found for conversion {conv.prefab.name}, skipping modification.");
                return;
            }
            foreach (GameObject obj in objects) {
                if (obj == null) { continue; }
                switch (conv.action)
                {
                    case ConversionAction.Modify:
                        ModifyConversion(conv, obj);
                        break;
                    case ConversionAction.Add:
                        AddConversion(conv, obj);
                        break;
                    case ConversionAction.Remove:
                        RemoveConversion(conv, obj);
                        break;
                }
            }
        }

        /// <summary>
        /// Every object with a station on it by the prefab it comes from: the prefab itself, and each copy placed in
        /// the world, so a change reaches stations that are already built. Matched on the exact prefab name, so a
        /// change to piece_cookingstation leaves piece_cookingstation_iron alone.
        /// </summary>
        internal static Dictionary<string, List<GameObject>> IndexStations() {
            Dictionary<string, List<GameObject>> index = new Dictionary<string, List<GameObject>>();
            HashSet<int> seen = new HashSet<int>();
            AddStations<Smelter>(index, seen);
            AddStations<Fermenter>(index, seen);
            AddStations<CookingStation>(index, seen);
            AddStations<Incinerator>(index, seen);
            return index;
        }

        private static void AddStations<T>(Dictionary<string, List<GameObject>> index, HashSet<int> seen) where T : Component {
            foreach (T station in Resources.FindObjectsOfTypeAll<T>()) {
                if (station == null) { continue; }
                GameObject root = StationRoot(station);
                if (!seen.Add(root.GetInstanceID())) { continue; }
                string name = StationRef.PrefabNameOf(root);
                if (!index.TryGetValue(name, out List<GameObject> objects)) {
                    objects = new List<GameObject>();
                    index.Add(name, objects);
                }
                // The prefab ahead of its placed copies, so its original is recorded before theirs is needed
                if (IsPlaced(root)) { objects.Add(root); } else { objects.Insert(0, root); }
            }
        }

        private static bool IsPlaced(GameObject go) {
            return go.name.EndsWith("(Clone)", StringComparison.Ordinal);
        }

        /// <summary>
        /// The object a station belongs to, which is what a conversion config has to name. The battering ram keeps
        /// its Smelter on a child object ("kiln engine"), so the component's own object is not always it.
        /// </summary>
        internal static GameObject StationRoot(Component station) {
            ZNetView nview = station.GetComponentInParent<ZNetView>(true);
            return nview != null ? nview.gameObject : station.gameObject;
        }

        /// <summary>
        /// Puts every station this mod has touched back to the values it shipped with.
        ///
        /// Driven from the snapshots rather than the tracking list, so a station that has since been
        /// dropped from the configuration is still restored. Callers reload and re-apply straight after
        /// this, which is what lets a conversion removed from the yaml actually disappear.
        /// </summary>
        public static void RevertConversionModifications() {
            if (OriginalConversions.Count == 0) { return; }
            Logger.LogInfo($"Reverting {OriginalConversions.Count} conversion modifications");

            List<int> gone = new List<int>();
            int restored = 0;
            foreach (KeyValuePair<int, ConversionSnapshot> original in OriginalConversions) {
                // Stations that were destroyed with their scene have nothing left to restore
                if (original.Value.WriteBack()) { restored++; } else { gone.Add(original.Key); }
            }
            foreach (int key in gone) { OriginalConversions.Remove(key); }
            Logger.LogDebug($"Restored {restored} conversion station(s), {gone.Count} no longer exist.");
        }

        /// <summary>
        /// The station component on an object or any of its children. Most stations sit on the prefab's
        /// root, but the battering ram keeps its Smelter on a child object ("kiln engine"). Inactive objects
        /// are included since Jotunn keeps cloned prefabs under an inactive container.
        /// </summary>
        internal static T FindStation<T>(GameObject go) where T : Component {
            return go.GetComponentInChildren<T>(true);
        }

        /// <summary>
        /// Records what a station looked like before it is written to. Only the first call for a given
        /// object does anything, so this is safe to put in front of every modification.
        /// </summary>
        private static void CaptureOriginal(GameObject go) {
            if (go == null) { return; }
            int key = go.GetInstanceID();
            if (OriginalConversions.ContainsKey(key)) { return; }
            string prefabName = StationRef.PrefabNameOf(go);
            bool placed = IsPlaced(go);
            ConversionSnapshot snapshot = placed && OriginalByPrefab.TryGetValue(prefabName, out ConversionSnapshot prefabOriginal)
                ? prefabOriginal.For(go)
                : ConversionSnapshot.From(go);
            if (snapshot.IsEmpty) { return; }
            OriginalConversions.Add(key, snapshot);
            if (!placed && !OriginalByPrefab.ContainsKey(prefabName)) { OriginalByPrefab.Add(prefabName, snapshot); }
        }

        private static void ModifyConversion(TrackedConversion conversion, GameObject go) {
            CaptureOriginal(go);
            switch(conversion.convertType) {
                case ConversionType.Smelter:
                    Smelter smelt = FindStation<Smelter>(go);
                    if (smelt == null) {
                        Logger.LogInfo($"Smelter component not found on {go.name}, cannot apply conversion modifications.");
                        break;
                    }
                    if (conversion.conversions != null) {
                        smelt.m_conversion = conversion.conversions.Select(c => new Smelter.ItemConversion { m_from = c.from, m_to = c.to }).ToList();
                    }
                    if (conversion.fuelItem != null) { smelt.m_fuelItem = conversion.fuelItem; }
                    // Values were validated when the tracker was built, zero is a real setting here
                    if (conversion.fuelPerProduct.HasValue) { smelt.m_fuelPerProduct = conversion.fuelPerProduct.Value; }
                    if (conversion.conversionTime.HasValue) { smelt.m_secPerProduct = conversion.conversionTime.Value; }
                    if (conversion.maxOres.HasValue) { smelt.m_maxOre = conversion.maxOres.Value; }
                    if (conversion.maxFuel.HasValue) { smelt.m_maxFuel = conversion.maxFuel.Value; }
                    if (conversion.spawnStack.HasValue) { smelt.m_spawnStack = conversion.spawnStack.Value; }
                    if (conversion.requiresRoof.HasValue) { smelt.m_requiresRoof = conversion.requiresRoof.Value; }
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = FindStation<Fermenter>(go);
                    if (ferment == null) {
                        Logger.LogInfo($"Fermenter component not found on {go.name}, cannot apply conversion modifications.");
                        break;
                    }
                    if (conversion.conversions != null) {
                        List<Fermenter.ItemConversion> current = ferment.m_conversion ?? new List<Fermenter.ItemConversion>();
                        // An amount left out is the one the station already makes from the same input
                        ferment.m_conversion = conversion.conversions.Select(c => new Fermenter.ItemConversion {
                            m_from = c.from,
                            m_to = c.to,
                            m_producedItems = c.amount ?? current.Find(e => e != null && SameItem(e.m_from, c.from))?.m_producedItems ?? DefaultFermenterAmount
                        }).ToList();
                    }
                    if (conversion.conversionTime.HasValue) { ferment.m_fermentationDuration = conversion.conversionTime.Value; }
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = FindStation<CookingStation>(go);
                    if (cook == null) {
                        Logger.LogInfo($"CookingStation component not found on {go.name}, cannot apply conversion modifications.");
                        break;
                    }
                    if (conversion.conversions != null) {
                        List<CookingStation.ItemConversion> current = cook.m_conversion ?? new List<CookingStation.ItemConversion>();
                        cook.m_conversion = conversion.conversions.Select(c => new CookingStation.ItemConversion {
                            m_from = c.from,
                            m_to = c.to,
                            m_cookTime = c.cookTime ?? current.Find(e => e != null && SameItem(e.m_from, c.from))?.m_cookTime ?? DefaultCookTime
                        }).ToList();
                    }
                    if (conversion.maxFuel.HasValue) { cook.m_maxFuel = conversion.maxFuel.Value; }
                    if (conversion.fuelItem != null) { cook.m_fuelItem = conversion.fuelItem; }
                    if (conversion.overCookedItem != null) { cook.m_overCookedItem = conversion.overCookedItem; }
                    if (conversion.requiresFire.HasValue) { cook.m_requireFire = conversion.requiresFire.Value; }
                    // m_useFuel is not configurable, so it is left as the station has it
                    if (conversion.secPerFuel.HasValue) { cook.m_secPerFuel = (int)conversion.secPerFuel.Value; }
                    break;
                case ConversionType.Incinerator:
                    Incinerator incinerator = FindStation<Incinerator>(go);
                    if (incinerator == null) {
                        Logger.LogInfo($"Incinerator component not found on {go.name}, cannot apply conversion modifications.");
                        break;
                    }
                    if (conversion.incineratorConversions != null) {
                        incinerator.m_conversions = ByPriority(go, conversion.incineratorConversions.Select(c => NewIncineratorConversion(c.built, null)).ToList());
                    }
                    if (conversion.defaultResult != null) { incinerator.m_defaultResult = conversion.defaultResult; }
                    if (conversion.defaultCost.HasValue) { incinerator.m_defaultCost = conversion.defaultCost.Value; }
                    break;
            }
        }

        /// <summary>
        /// An obliterator sorts its conversions by priority once, when it is placed. One already placed is given its list
        /// sorted the same way, the prefab keeps the order it was given.
        /// </summary>
        private static List<Incinerator.IncineratorConversion> ByPriority(GameObject go, List<Incinerator.IncineratorConversion> conversions) {
            if (conversions != null && go != null && IsPlaced(go)) {
                conversions.Sort((a, b) => (b?.m_priority ?? 0).CompareTo(a?.m_priority ?? 0));
            }
            return conversions;
        }

        /// <summary>
        /// A conversion for an incinerator, new each time so the station never shares one with the snapshot. Values its
        /// entry left out come from <paramref name="existing"/>, the one it updates, or the game's defaults.
        /// </summary>
        private static Incinerator.IncineratorConversion NewIncineratorConversion(Incinerator.IncineratorConversion built, Incinerator.IncineratorConversion existing)
        {
            return new Incinerator.IncineratorConversion {
                m_result = built.m_result,
                m_resultAmount = built.m_resultAmount,
                m_priority = built.m_priority,
                m_requireOnlyOneIngredient = built.m_requireOnlyOneIngredient,
                m_requirements = built.m_requirements ?? (existing?.m_requirements != null ? new List<Incinerator.Requirement>(existing.m_requirements) : new List<Incinerator.Requirement>())
            };
        }

        // A conversion is identified by what it takes. A smelter can leave that side empty, and an empty side
        // only matches another empty side.
        private static bool SameItem(ItemDrop a, ItemDrop b) {
            if (a == null || b == null) { return a == null && b == null; }
            return a.gameObject.name == b.gameObject.name;
        }

        /// <summary>
        /// Updates the conversion the station already has for each input listed, or adds one when it has none.
        /// Every station only ever uses the first conversion for an input, so a second one would never run.
        /// Builds a new list of new entries rather than changing the station's, which the snapshot shares.
        /// </summary>
        public static void AddConversion(TrackedConversion conversion, GameObject go) {
            if (conversion.convertType == ConversionType.Incinerator) {
                if (conversion.incineratorConversions == null) { return; }
                CaptureOriginal(go);
                AddIncineratorConversions(conversion, go);
                return;
            }
            if (conversion.conversions == null) { return; }
            CaptureOriginal(go);
            switch (conversion.convertType) {
                case ConversionType.Smelter:
                    Smelter smelt = FindStation<Smelter>(go);
                    if (smelt == null) {
                        Logger.LogWarning($"Smelter component not found on {go.name}, no conversions can be added to it.");
                        break;
                    }
                    smelt.m_conversion = Upsert(smelt.m_conversion, conversion.conversions, true, e => e.m_from,
                        (e, c) => new Smelter.ItemConversion { m_from = e.m_from, m_to = c.toGiven ? c.to : e.m_to },
                        c => new Smelter.ItemConversion { m_from = c.from, m_to = c.to });
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = FindStation<Fermenter>(go);
                    if (ferment == null) {
                        Logger.LogWarning($"Fermenter component not found on {go.name}, no conversions can be added to it.");
                        break;
                    }
                    ferment.m_conversion = Upsert(ferment.m_conversion, conversion.conversions, false, e => e.m_from,
                        (e, c) => new Fermenter.ItemConversion { m_from = e.m_from, m_to = c.toGiven ? c.to : e.m_to, m_producedItems = c.amount ?? e.m_producedItems },
                        c => new Fermenter.ItemConversion { m_from = c.from, m_to = c.to, m_producedItems = c.amount ?? DefaultFermenterAmount });
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = FindStation<CookingStation>(go);
                    if (cook == null) {
                        Logger.LogWarning($"CookingStation component not found on {go.name}, no conversions can be added to it.");
                        break;
                    }
                    cook.m_conversion = Upsert(cook.m_conversion, conversion.conversions, false, e => e.m_from,
                        (e, c) => new CookingStation.ItemConversion { m_from = e.m_from, m_to = c.toGiven ? c.to : e.m_to, m_cookTime = c.cookTime ?? e.m_cookTime },
                        c => new CookingStation.ItemConversion { m_from = c.from, m_to = c.to, m_cookTime = c.cookTime ?? DefaultCookTime });
                    break;
            }
        }

        /// <summary>Updates the incinerator's conversion for each result listed, or adds one when it has none.</summary>
        private static void AddIncineratorConversions(TrackedConversion conversion, GameObject go) {
            Incinerator incinerator = FindStation<Incinerator>(go);
            if (incinerator == null) {
                Logger.LogWarning($"Incinerator component not found on {go.name}, no conversions can be added to it.");
                return;
            }
            List<Incinerator.IncineratorConversion> result = incinerator.m_conversions == null ? new List<Incinerator.IncineratorConversion>() : new List<Incinerator.IncineratorConversion>(incinerator.m_conversions);
            foreach ((Incinerator.IncineratorConversion built, IncineratorConversionDef def) in conversion.incineratorConversions) {
                int index = result.FindIndex(e => e != null && SameItem(e.m_result, built.m_result));
                if (index < 0) {
                    result.Add(NewIncineratorConversion(built, null));
                    continue;
                }
                Incinerator.IncineratorConversion existing = result[index];
                Incinerator.IncineratorConversion updated = NewIncineratorConversion(built, existing);
                // Left out of an Add, these keep what the station already has for the same result
                if (def.resultAmount == null) { updated.m_resultAmount = existing.m_resultAmount; }
                if (def.priority == null) { updated.m_priority = existing.m_priority; }
                if (def.anyOneIngredient == null) { updated.m_requireOnlyOneIngredient = existing.m_requireOnlyOneIngredient; }
                result[index] = updated;
            }
            incinerator.m_conversions = ByPriority(go, result);
        }

        /// <param name="emptyOutputAllowed">Smelters only, which can make nothing from an input.</param>
        private static List<T> Upsert<T>(List<T> existing, List<ResolvedConversion> entries, bool emptyOutputAllowed, Func<T, ItemDrop> fromOf, Func<T, ResolvedConversion, T> update, Func<ResolvedConversion, T> create) where T : class {
            List<T> result = existing == null ? new List<T>() : new List<T>(existing);
            foreach (ResolvedConversion entry in entries) {
                int index = result.FindIndex(e => e != null && SameItem(fromOf(e), entry.from));
                if (index > -1) {
                    result[index] = update(result[index], entry);
                } else if (entry.to != null || emptyOutputAllowed) {
                    result.Add(create(entry));
                }
            }
            return result;
        }

        /// <summary>Takes out the conversions for each input listed, only the ones making the listed output when one is given.</summary>
        public static void RemoveConversion(TrackedConversion conversion, GameObject go)
        {
            if (conversion.convertType == ConversionType.Incinerator) {
                if (conversion.incineratorConversions == null) { return; }
                CaptureOriginal(go);
                Incinerator incinerator = FindStation<Incinerator>(go);
                if (incinerator == null || incinerator.m_conversions == null) {
                    Logger.LogWarning($"Incinerator component not found on {go.name}, no conversions can be removed from it.");
                    return;
                }
                incinerator.m_conversions = ByPriority(go, incinerator.m_conversions.Where(e => e == null || !conversion.incineratorConversions.Exists(c => SameItem(e.m_result, c.built.m_result))).ToList());
                return;
            }
            if (conversion.conversions == null) { return; }
            CaptureOriginal(go);
            switch (conversion.convertType)
            {
                case ConversionType.Smelter:
                    Smelter smelt = FindStation<Smelter>(go);
                    if (smelt == null || smelt.m_conversion == null) {
                        Logger.LogWarning($"Smelter component not found on {go.name}, no conversions can be removed from it.");
                        break;
                    }
                    smelt.m_conversion = Without(smelt.m_conversion, conversion.conversions, e => e.m_from, e => e.m_to);
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = FindStation<Fermenter>(go);
                    if (ferment == null || ferment.m_conversion == null) {
                        Logger.LogWarning($"Fermenter component not found on {go.name}, no conversions can be removed from it.");
                        break;
                    }
                    ferment.m_conversion = Without(ferment.m_conversion, conversion.conversions, e => e.m_from, e => e.m_to);
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = FindStation<CookingStation>(go);
                    if (cook == null || cook.m_conversion == null) {
                        Logger.LogWarning($"CookingStation component not found on {go.name}, no conversions can be removed from it.");
                        break;
                    }
                    cook.m_conversion = Without(cook.m_conversion, conversion.conversions, e => e.m_from, e => e.m_to);
                    break;
            }
        }

        private static List<T> Without<T>(List<T> existing, List<ResolvedConversion> entries, Func<T, ItemDrop> fromOf, Func<T, ItemDrop> toOf) {
            List<T> result = new List<T>(existing);
            foreach (ResolvedConversion entry in entries) {
                result.RemoveAll(e => e != null && SameItem(fromOf(e), entry.from) && (!entry.toGiven || SameItem(toOf(e), entry.to)));
            }
            return result;
        }

        public static void BuildConversionTracker()
        {
            TrackedConversions.Clear();
            foreach (KeyValuePair<string, ConversionModification> cov in ConversionsToModify)
            {
                if (cov.Key == null || cov.Value == null) { continue; }
                Logger.LogDebug($"Constructing Conversion modifications for {cov.Key}");
                if (string.IsNullOrEmpty(cov.Value.prefab)) {
                    Logger.LogWarning($"Conversion modification '{cov.Key}' has no prefab set, it will be skipped. Define a prefab to modify to fix this.");
                    continue;
                }

                TrackedConversion tconv = new TrackedConversion { key = cov.Key, action = cov.Value.action };
                GameObject tgo = PrefabManager.Instance.GetPrefab(cov.Value.prefab);
                if (tgo == null) {
                    Logger.LogWarning($"Could not find piece with name: {cov.Value.prefab}, this modification will be skipped.");
                    continue;
                }
                tconv.prefab = tgo;
                // Looked up the same way the modifications are applied, so a station found here is one
                // that can actually be written to
                Smelter tsmelt = FindStation<Smelter>(tgo);
                if (tsmelt != null) {
                    tconv.smelterComponent = tsmelt;
                    tconv.convertType = ConversionType.Smelter;
                }
                Fermenter tferm = FindStation<Fermenter>(tgo);
                if (tferm != null) {
                    tconv.fermenterComponent = tferm;
                    tconv.convertType = ConversionType.Fermenter;
                }
                CookingStation tcook = FindStation<CookingStation>(tgo);
                if (tcook != null) {
                    tconv.cookingStationComponent = tcook;
                    tconv.convertType = ConversionType.CookingStation;
                }
                Incinerator tincinerator = FindStation<Incinerator>(tgo);
                if (tincinerator != null) {
                    tconv.incineratorComponent = tincinerator;
                    tconv.convertType = ConversionType.Incinerator;
                }
                if (tconv.cookingStationComponent == null && tconv.fermenterComponent == null && tconv.smelterComponent == null && tconv.incineratorComponent == null) {
                    Logger.LogWarning($"{cov.Value.prefab} did not have an attached Fermenter, Smelter, CookingStation or Incinerator. This modification will be skipped.");
                    continue;
                }
                bool incinerates = tconv.convertType == ConversionType.Incinerator;
                if (incinerates && cov.Value.conversions != null) {
                    Logger.LogWarning($"Conversion modification '{cov.Key}' lists conversions for {cov.Value.prefab}, an incinerator. Use incineratorConversions instead, they are skipped.");
                }
                if (!incinerates && (cov.Value.incineratorConversions != null || cov.Value.defaultResult != null || cov.Value.defaultCost != null)) {
                    Logger.LogWarning($"Conversion modification '{cov.Key}' sets incinerator settings on {cov.Value.prefab}, which is not an incinerator. They are skipped.");
                }
                if (cov.Value.overCookedTime > 0 && !warnedOverCookedTime) {
                    warnedOverCookedTime = true;
                    Logger.LogWarning($"Conversion modification '{cov.Key}' sets overCookedTime, which the game has no setting for. It is ignored, remove it from your conversion files.");
                }

                // The conversion list is left null when the entry has none, so a Modify that only changes timings
                // or fuel keeps the station's own conversions
                if (cov.Value.conversions != null && tconv.convertType != ConversionType.Incinerator) {
                    tconv.conversions = new List<ResolvedConversion>();
                    foreach (ConversionDef conv in cov.Value.conversions) {
                        if (ResolveConversion(cov.Key, conv, tconv, out ResolvedConversion resolved)) { tconv.conversions.Add(resolved); }
                    }
                }

                switch (tconv.convertType) {
                    case ConversionType.Fermenter:
                        Logger.LogDebug("Setting Fermenter");
                        if (cov.Value.conversionTime.HasValue) {
                            if (cov.Value.conversionTime.Value > 0) {
                                tconv.conversionTime = cov.Value.conversionTime;
                            } else {
                                Logger.LogWarning($"{cov.Key} has a conversionTime of {cov.Value.conversionTime.Value}, it must be above 0. The fermentation time will not be changed.");
                            }
                        }
                        break;
                    case ConversionType.CookingStation:
                        Logger.LogDebug("Setting CookingStation");
                        tconv.maxFuel = NonNegative(cov.Key, "maxFuel", cov.Value.maxFuel);
                        if (cov.Value.secPerFuel.HasValue) {
                            if (cov.Value.secPerFuel.Value > 0) {
                                tconv.secPerFuel = cov.Value.secPerFuel;
                            } else {
                                Logger.LogWarning($"{cov.Key} has a secPerFuel of {cov.Value.secPerFuel.Value}, it must be above 0 and will be ignored.");
                            }
                        }
                        tconv.fuelItem = ResolveNamedItem(cov.Key, "fuel item", cov.Value.fuelItem);
                        tconv.overCookedItem = ResolveNamedItem(cov.Key, "over cooked item", cov.Value.overCookedItem);
                        tconv.requiresFire = cov.Value.requiresFire;
                        break;
                    case ConversionType.Incinerator:
                        tconv.defaultResult = ResolveNamedItem(cov.Key, "default result", cov.Value.defaultResult);
                        if (cov.Value.defaultCost.HasValue) {
                            if (cov.Value.defaultCost.Value > 0) {
                                tconv.defaultCost = cov.Value.defaultCost;
                            } else {
                                Logger.LogWarning($"{cov.Key} has a defaultCost of {cov.Value.defaultCost.Value}, it must be above 0 and will be ignored.");
                            }
                        }
                        if (cov.Value.incineratorConversions != null) {
                            tconv.incineratorConversions = new List<(Incinerator.IncineratorConversion built, IncineratorConversionDef def)>();
                            foreach (IncineratorConversionDef def in cov.Value.incineratorConversions) {
                                Incinerator.IncineratorConversion built = ResolveIncineratorConversion(cov.Key, def, tconv.action, tincinerator);
                                if (built != null) { tconv.incineratorConversions.Add((built, def)); }
                            }
                        }
                        break;
                    case ConversionType.Smelter:
                        Logger.LogDebug("Setting Smelter");
                        tconv.conversionTime = SmelterConversionTime(cov.Key, cov.Value.conversionTime);
                        // Zero is allowed for these: the game treats maxFuel 0 as needing no fuel, and
                        // fuelPerProduct 0 as never burning the fuel that is loaded
                        tconv.fuelPerProduct = NonNegative(cov.Key, "fuelPerProduct", cov.Value.fuelPerProduct);
                        tconv.maxFuel = NonNegative(cov.Key, "maxFuel", cov.Value.maxFuel);
                        tconv.maxOres = NonNegative(cov.Key, "maxOres", cov.Value.maxOres);
                        tconv.fuelItem = ResolveNamedItem(cov.Key, "fuel item", cov.Value.fuelItem);
                        tconv.spawnStack = cov.Value.spawnStack;
                        tconv.requiresRoof = cov.Value.requiresRoof;
                        // The game checks for a roof from a point on the station, which not every station has
                        if (tconv.requiresRoof == true && tsmelt.m_roofCheckPoint == null) {
                            Logger.LogWarning($"{cov.Key} makes {cov.Value.prefab} need a roof, but it has no point to check for one from. That setting is ignored.");
                            tconv.requiresRoof = null;
                        }
                        if (tconv.action != ConversionAction.Remove) { CheckSmelterSetup(cov.Key, tconv); }
                        break;
                }
                TrackedConversions.Add(tconv);
            }

            // Smelters are the one station that works with a side left out: the battering ram burns wood and
            // makes nothing, and the frost kiln makes frozen fuel from its fuel alone. Fermenters and cooking
            // stations read both sides without checking, so for those a missing side skips the conversion,
            // unless it is an Add updating a conversion the station already has, which then keeps its output.
            static bool ResolveConversion(string entry, ConversionDef conv, TrackedConversion tconv, out ResolvedConversion resolved)
            {
                resolved = null;
                if (conv == null) { return false; }
                Logger.LogDebug($"Creating Conversion {conv.fromPrefab} to {conv.toPrefab}");
                bool smelter = tconv.convertType == ConversionType.Smelter;
                bool noFrom = string.IsNullOrEmpty(conv.fromPrefab);
                bool noTo = string.IsNullOrEmpty(conv.toPrefab);
                if (noFrom && noTo) {
                    Logger.LogWarning($"{entry} has a conversion with neither a fromPrefab nor a toPrefab, it will be skipped.");
                    return false;
                }
                if (!smelter && noFrom) {
                    Logger.LogWarning($"{entry} has a conversion with no fromPrefab, only smelter type stations can leave it out. This conversion will be skipped.");
                    return false;
                }
                if (!ResolveItem(entry, conv.fromPrefab, out ItemDrop from) || !ResolveItem(entry, conv.toPrefab, out ItemDrop to)) { return false; }

                if (!smelter && noTo) {
                    bool updatesExisting = tconv.action == ConversionAction.Remove
                        || (tconv.action == ConversionAction.Add && StationHasInput(tconv, from));
                    if (!updatesExisting) {
                        Logger.LogWarning($"{entry} has a conversion from {conv.fromPrefab} with no toPrefab, only smelter type stations can leave it out. This conversion will be skipped.");
                        return false;
                    }
                }
                resolved = new ResolvedConversion {
                    from = from,
                    to = to,
                    // A smelter conversion that names no output on a Modify makes nothing, on an Add or Remove it is left as it is
                    toGiven = !noTo || (smelter && tconv.action == ConversionAction.Modify),
                    amount = conv.amount,
                    cookTime = conv.cookTime
                };
                return true;
            }

            static bool StationHasInput(TrackedConversion tconv, ItemDrop from)
            {
                if (tconv.fermenterComponent != null && tconv.fermenterComponent.m_conversion != null) {
                    return tconv.fermenterComponent.m_conversion.Exists(e => e != null && SameItem(e.m_from, from));
                }
                if (tconv.cookingStationComponent != null && tconv.cookingStationComponent.m_conversion != null) {
                    return tconv.cookingStationComponent.m_conversion.Exists(e => e != null && SameItem(e.m_from, from));
                }
                return false;
            }

            // A name left out resolves to no item, which is a valid smelter conversion side. A name that does
            // not resolve to an item is a mistake, and skips the conversion rather than silently becoming an
            // empty side. PrefabManager.GetPrefab throws on a null name, so it is never asked for one.
            static bool ResolveItem(string entry, string prefabName, out ItemDrop item)
            {
                item = null;
                if (string.IsNullOrEmpty(prefabName)) { return true; }
                GameObject go = PrefabManager.Instance.GetPrefab(prefabName);
                item = go != null ? go.GetComponent<ItemDrop>() : null;
                if (item == null) {
                    Logger.LogWarning($"{entry} has a conversion using {prefabName}, which {(go == null ? "could not be found" : "is not an item")}. This conversion will be skipped.");
                    return false;
                }
                return true;
            }

            // A smelter only takes items while maxOres is above 0, and only needs fuel while maxFuel is. The
            // frost kiln has nowhere to load items and the battering ram nowhere to load fuel, so turning
            // either on leaves them waiting forever. A conversion with no fromPrefab is only used while the
            // station takes no items, it is then made from fuel alone (Smelter.GetItemConversion).
            static void CheckSmelterSetup(string entry, TrackedConversion tconv)
            {
                Smelter station = tconv.smelterComponent;
                string name = tconv.prefab.name;
                // Add only changes the conversion list, the station keeps its own limits
                bool modify = tconv.action == ConversionAction.Modify;
                int maxOres = (modify ? tconv.maxOres : null) ?? station.m_maxOre;
                int maxFuel = (modify ? tconv.maxFuel : null) ?? station.m_maxFuel;
                if (maxOres > 0 && station.m_addOreSwitch == null) {
                    Logger.LogWarning($"{entry} leaves {name} with maxOres {maxOres}, but it has nowhere to load items. It will never produce anything unless maxOres is 0.");
                }
                if (maxFuel > 0 && station.m_addWoodSwitch == null) {
                    Logger.LogWarning($"{entry} leaves {name} with maxFuel {maxFuel}, but it has nowhere to load fuel. It will never produce anything unless maxFuel is 0.");
                }

                List<ResolvedConversion> supplied = tconv.conversions;
                if (maxOres > 0) {
                    // The game's item menu reads the fromPrefab of every conversion and throws on a missing one
                    int skipped = supplied == null ? 0 : supplied.RemoveAll(c => c.from == null);
                    if (skipped > 0) {
                        Logger.LogWarning($"{entry} gives {name} {skipped} conversion(s) with no fromPrefab, but it takes items (maxOres {maxOres}). Those only work with maxOres 0 and have been skipped.");
                    }
                    return;
                }

                // The inputs the station ends up with, an Add keeps the station's own conversions in front
                List<ItemDrop> inputs;
                if (modify && supplied != null) {
                    inputs = supplied.Select(c => c.from).ToList();
                } else {
                    inputs = (station.m_conversion ?? new List<Smelter.ItemConversion>()).Where(c => c != null).Select(c => c.m_from).ToList();
                    foreach (ResolvedConversion c in supplied ?? new List<ResolvedConversion>()) {
                        if (!inputs.Exists(i => SameItem(i, c.from))) { inputs.Add(c.from); }
                    }
                }
                if (inputs.Any(i => i != null)) {
                    Logger.LogWarning($"{entry} leaves {name} with maxOres 0, so it takes no items and its conversions with a fromPrefab will never run.");
                }
                if (inputs.Count(i => i == null) > 1) {
                    Logger.LogWarning($"{entry} leaves {name} with more than one conversion without a fromPrefab, only the first of them is ever produced.");
                }
            }

            // An incinerator conversion with its items looked up. Ingredients are left null when an Add leaves them out,
            // so the conversion it updates keeps its own. A Remove only needs the result. A conversion with no ingredients
            // is never kept: the obliterator would try to make an endless number of them and stop the game.
            static Incinerator.IncineratorConversion ResolveIncineratorConversion(string entry, IncineratorConversionDef def, ConversionAction action, Incinerator station)
            {
                if (def == null) { return null; }
                ItemDrop result = ResolveNamedItem(entry, "incinerator result", def.result);
                if (result == null) {
                    if (string.IsNullOrEmpty(def.result)) { Logger.LogWarning($"{entry} has an incinerator conversion with no result, it will be skipped."); }
                    return null;
                }
                List<Incinerator.Requirement> requirements = null;
                if (def.ingredients != null) {
                    requirements = new List<Incinerator.Requirement>();
                    foreach (IncineratorIngredientDef ingredient in def.ingredients) {
                        if (ingredient == null) { continue; }
                        ItemDrop item = ResolveNamedItem(entry, "incinerator ingredient", ingredient.prefab);
                        if (item == null) { continue; }
                        requirements.Add(new Incinerator.Requirement { m_resItem = item, m_amount = Math.Max(1, ingredient.amount ?? 1) });
                    }
                    if (requirements.Count == 0 && action != ConversionAction.Remove) {
                        Logger.LogWarning($"{entry} has an incinerator conversion making {def.result} with no ingredients that exist. It needs at least one, it will be skipped.");
                        return null;
                    }
                } else if (action == ConversionAction.Modify
                    || (action == ConversionAction.Add && !(station?.m_conversions?.Exists(e => e != null && SameItem(e.m_result, result)) ?? false))) {
                    Logger.LogWarning($"{entry} has an incinerator conversion making {def.result} with no ingredients. It needs at least one, it will be skipped.");
                    return null;
                }
                return new Incinerator.IncineratorConversion {
                    m_result = result,
                    m_resultAmount = Math.Max(1, def.resultAmount ?? 1),
                    m_priority = def.priority ?? 0,
                    m_requireOnlyOneIngredient = def.anyOneIngredient ?? false,
                    m_requirements = requirements
                };
            }

            static ItemDrop ResolveNamedItem(string entry, string field, string itemName)
            {
                if (string.IsNullOrEmpty(itemName)) { return null; }
                GameObject itemGo = PrefabManager.Instance.GetPrefab(itemName);
                ItemDrop item = itemGo != null ? itemGo.GetComponent<ItemDrop>() : null;
                if (item == null) {
                    Logger.LogWarning($"{entry} could not find an item named {itemName}, the {field} will not be changed.");
                }
                return item;
            }

            static int? NonNegative(string entry, string field, int? value)
            {
                if (value.HasValue && value.Value < 0) {
                    Logger.LogWarning($"{entry} has a {field} of {value.Value}, it can not be negative and will be ignored.");
                    return null;
                }
                return value;
            }

            // Smelter.UpdateSmelter advances in one second steps, making at most one product per step and
            // burning fuelPerProduct / secPerProduct fuel each step. A time below one second is therefore no
            // faster, it only burns more fuel per product, and a time of zero never produces anything.
            static float? SmelterConversionTime(string entry, float? conversionTime)
            {
                if (conversionTime.HasValue == false) { return null; }
                if (conversionTime.Value <= 0) {
                    Logger.LogWarning($"{entry} has a conversionTime of {conversionTime.Value}, a smelter with no conversion time never produces anything. The conversion time will not be changed.");
                    return null;
                }
                if (conversionTime.Value < 1) {
                    Logger.LogWarning($"{entry} has a conversionTime of {conversionTime.Value}, smelters can not produce more than one item per second and a shorter time only burns more fuel. Using 1 instead.");
                    return 1f;
                }
                return conversionTime;
            }
        }

        /// <summary>
        /// A copy of every field the conversion actions can write, for whichever of the three station
        /// components an object actually has. The conversion lists are copied rather than referenced, and
        /// the actions always hand a station a new list, so nothing written later reaches back in here.
        /// </summary>
        private class ConversionSnapshot
        {
            private GameObject gameObject;

            private bool hasSmelter;
            private List<Smelter.ItemConversion> smelterConversions;
            private ItemDrop smelterFuelItem;
            private int smelterFuelPerProduct;
            private float smelterSecPerProduct;
            private int smelterMaxOre;
            private int smelterMaxFuel;
            private bool smelterSpawnStack;
            private bool smelterRequiresRoof;

            private bool hasFermenter;
            private List<Fermenter.ItemConversion> fermenterConversions;
            private float fermentationDuration;

            private bool hasCookingStation;
            private List<CookingStation.ItemConversion> cookingConversions;
            private ItemDrop cookingFuelItem;
            private ItemDrop cookingOverCookedItem;
            private int cookingMaxFuel;
            private bool cookingUseFuel;
            private bool cookingRequireFire;
            private int cookingSecPerFuel;

            private bool hasIncinerator;
            private List<Incinerator.IncineratorConversion> incineratorConversions;
            private ItemDrop incineratorDefaultResult;
            private int incineratorDefaultCost;

            public static ConversionSnapshot From(GameObject go)
            {
                ConversionSnapshot snapshot = new ConversionSnapshot { gameObject = go };

                Smelter smelt = FindStation<Smelter>(go);
                if (smelt != null) {
                    snapshot.hasSmelter = true;
                    snapshot.smelterConversions = Copy(smelt.m_conversion);
                    snapshot.smelterFuelItem = smelt.m_fuelItem;
                    snapshot.smelterFuelPerProduct = smelt.m_fuelPerProduct;
                    snapshot.smelterSecPerProduct = smelt.m_secPerProduct;
                    snapshot.smelterMaxOre = smelt.m_maxOre;
                    snapshot.smelterMaxFuel = smelt.m_maxFuel;
                    snapshot.smelterSpawnStack = smelt.m_spawnStack;
                    snapshot.smelterRequiresRoof = smelt.m_requiresRoof;
                }

                Fermenter ferment = FindStation<Fermenter>(go);
                if (ferment != null) {
                    snapshot.hasFermenter = true;
                    snapshot.fermenterConversions = Copy(ferment.m_conversion);
                    snapshot.fermentationDuration = ferment.m_fermentationDuration;
                }

                CookingStation cook = FindStation<CookingStation>(go);
                if (cook != null) {
                    snapshot.hasCookingStation = true;
                    snapshot.cookingConversions = Copy(cook.m_conversion);
                    snapshot.cookingFuelItem = cook.m_fuelItem;
                    snapshot.cookingOverCookedItem = cook.m_overCookedItem;
                    snapshot.cookingMaxFuel = cook.m_maxFuel;
                    snapshot.cookingUseFuel = cook.m_useFuel;
                    snapshot.cookingRequireFire = cook.m_requireFire;
                    snapshot.cookingSecPerFuel = cook.m_secPerFuel;
                }

                Incinerator incinerator = FindStation<Incinerator>(go);
                if (incinerator != null) {
                    snapshot.hasIncinerator = true;
                    snapshot.incineratorConversions = Copy(incinerator.m_conversions);
                    snapshot.incineratorDefaultResult = incinerator.m_defaultResult;
                    snapshot.incineratorDefaultCost = incinerator.m_defaultCost;
                }

                return snapshot;
            }

            /// <summary>The same values, to be restored onto another object.</summary>
            public ConversionSnapshot For(GameObject go)
            {
                ConversionSnapshot copy = (ConversionSnapshot)MemberwiseClone();
                copy.gameObject = go;
                copy.smelterConversions = Copy(smelterConversions);
                copy.fermenterConversions = Copy(fermenterConversions);
                copy.cookingConversions = Copy(cookingConversions);
                copy.incineratorConversions = Copy(incineratorConversions);
                return copy;
            }

            /// <summary>True when the object had none of the three station components.</summary>
            public bool IsEmpty
            {
                get { return !hasSmelter && !hasFermenter && !hasCookingStation && !hasIncinerator; }
            }

            /// <summary>Restores the station. False when the object it was taken from no longer exists.</summary>
            public bool WriteBack()
            {
                if (gameObject == null) { return false; }

                if (hasSmelter) {
                    Smelter smelt = FindStation<Smelter>(gameObject);
                    if (smelt != null) {
                        smelt.m_conversion = Copy(smelterConversions);
                        smelt.m_fuelItem = smelterFuelItem;
                        smelt.m_fuelPerProduct = smelterFuelPerProduct;
                        smelt.m_secPerProduct = smelterSecPerProduct;
                        smelt.m_maxOre = smelterMaxOre;
                        smelt.m_maxFuel = smelterMaxFuel;
                        smelt.m_spawnStack = smelterSpawnStack;
                        smelt.m_requiresRoof = smelterRequiresRoof;
                    }
                }

                if (hasFermenter) {
                    Fermenter ferment = FindStation<Fermenter>(gameObject);
                    if (ferment != null) {
                        ferment.m_conversion = Copy(fermenterConversions);
                        ferment.m_fermentationDuration = fermentationDuration;
                    }
                }

                if (hasCookingStation) {
                    CookingStation cook = FindStation<CookingStation>(gameObject);
                    if (cook != null) {
                        cook.m_conversion = Copy(cookingConversions);
                        cook.m_fuelItem = cookingFuelItem;
                        cook.m_overCookedItem = cookingOverCookedItem;
                        cook.m_maxFuel = cookingMaxFuel;
                        cook.m_useFuel = cookingUseFuel;
                        cook.m_requireFire = cookingRequireFire;
                        cook.m_secPerFuel = cookingSecPerFuel;
                    }
                }

                if (hasIncinerator) {
                    Incinerator incinerator = FindStation<Incinerator>(gameObject);
                    if (incinerator != null) {
                        incinerator.m_conversions = ByPriority(gameObject, Copy(incineratorConversions));
                        incinerator.m_defaultResult = incineratorDefaultResult;
                        incinerator.m_defaultCost = incineratorDefaultCost;
                    }
                }

                return true;
            }

            /// <summary>
            /// A fresh list holding the same conversion entries. Handed out on every restore as well as
            /// taken on capture, so a later Add or Remove cannot reach back into the snapshot.
            /// </summary>
            private static List<T> Copy<T>(List<T> source)
            {
                return source == null ? null : new List<T>(source);
            }
        }
    }
}

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
        public static Dictionary<String, ConversionModification> ConversionsToModify = new Dictionary<String, ConversionModification>();
        public static List<TrackedConversion> TrackedConversions = new List<TrackedConversion>();

        /// <summary>
        /// What each station looked like before this mod first wrote to it, keyed by GameObject instance
        /// id. One config entry matches every object whose name starts with the prefab name, so this is
        /// per object rather than per entry. Kept for the life of the process so a re-apply never
        /// records an already modified station as the original.
        /// </summary>
        private static readonly Dictionary<int, ConversionSnapshot> OriginalConversions = new Dictionary<int, ConversionSnapshot>();

        public static void InitialSychronization() {
            BuildConversionTracker();
            ConversionUpdateRunner();
        }

        public static void ConversionUpdateRunner() {
            Logger.LogInfo($"Applying {TrackedConversions.Count} conversion modifications");
            foreach (TrackedConversion piece in TrackedConversions) {
                ApplyConversionModifications(piece);
            }
        }

        public static void ApplyConversionModifications(TrackedConversion conv) {
            if (conv == null || conv.prefab == null) {
                Logger.LogWarning("A tracked conversion has no target prefab, this modification will be skipped.");
                return;
            }
            Logger.LogDebug($"Applying conversion ({conv.prefab.name}) modification action: {conv.action}");

            IEnumerable<GameObject> objects = Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj != null && obj.name.StartsWith(conv.prefab.name));
            if (objects.Any() == false) {
                Logger.LogWarning($"No Prefabs found for conversion {conv.prefab.name}, skipping modification.");
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
            ConversionSnapshot snapshot = ConversionSnapshot.From(go);
            // The prefab name search matches plenty of objects with no station on them
            if (snapshot.IsEmpty) { return; }
            OriginalConversions.Add(key, snapshot);
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
                    if (conversion.updatedSmelterConversions != null) { smelt.m_conversion = conversion.updatedSmelterConversions; }
                    if (conversion.fuelItem != null) { smelt.m_fuelItem = conversion.fuelItem; }
                    // Values were validated when the tracker was built, zero is a real setting here
                    if (conversion.fuelPerProduct.HasValue) { smelt.m_fuelPerProduct = conversion.fuelPerProduct.Value; }
                    if (conversion.conversionTime.HasValue) { smelt.m_secPerProduct = conversion.conversionTime.Value; }
                    if (conversion.maxOres.HasValue) { smelt.m_maxOre = conversion.maxOres.Value; }
                    if (conversion.maxFuel.HasValue) { smelt.m_maxFuel = conversion.maxFuel.Value; }
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = FindStation<Fermenter>(go);
                    if (ferment == null) {
                        Logger.LogInfo($"Fermenter component not found on {go.name}, cannot apply conversion modifications.");
                        break;
                    }
                    if (conversion.updatedFermenterConversions != null) { ferment.m_conversion = conversion.updatedFermenterConversions; }
                    if (conversion.conversionTime.HasValue) { ferment.m_fermentationDuration = conversion.conversionTime.Value; }
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = FindStation<CookingStation>(go);
                    if (cook == null) {
                        Logger.LogInfo($"CookingStation component not found on {go.name}, cannot apply conversion modifications.");
                        break;
                    }
                    if (conversion.updatedCookingConversions != null) { cook.m_conversion = conversion.updatedCookingConversions; }
                    if (conversion.maxFuel.HasValue) { cook.m_maxFuel = conversion.maxFuel.Value; }
                    if (conversion.fuelItem != null) { cook.m_fuelItem = conversion.fuelItem; }
                    // m_useFuel and m_requireFire are not configurable, so they are left as the station has them
                    if (conversion.secPerFuel.HasValue) { cook.m_secPerFuel = (int)conversion.secPerFuel.Value; }
                    break;
            }
        }

        /// <summary>
        /// Adds every entry the station does not already have. Tolerates a config that defined no conversions.
        /// Entries are built fresh from the config, so they never share an instance with what the station
        /// already holds and are compared with <paramref name="same"/> instead.
        /// </summary>
        private static void AddEntries<T>(List<T> target, List<T> entries, Func<T, T, bool> same) {
            if (entries == null) { return; }
            foreach (T entry in entries) {
                if (target.Exists(existing => same(existing, entry)) == false) { target.Add(entry); }
            }
        }

        private static void RemoveEntries<T>(List<T> target, List<T> entries, Func<T, T, bool> same) {
            if (entries == null) { return; }
            foreach (T entry in entries) {
                target.RemoveAll(existing => same(existing, entry));
            }
        }

        // A conversion is identified by what it takes and what it makes. A smelter can leave either side
        // empty, and an empty side only matches another empty side.
        private static bool SameItem(ItemDrop a, ItemDrop b) {
            if (a == null || b == null) { return a == null && b == null; }
            return a.gameObject.name == b.gameObject.name;
        }

        private static bool SameConversion(Smelter.ItemConversion a, Smelter.ItemConversion b) {
            return a != null && b != null && SameItem(a.m_from, b.m_from) && SameItem(a.m_to, b.m_to);
        }

        private static bool SameConversion(Fermenter.ItemConversion a, Fermenter.ItemConversion b) {
            return a != null && b != null && SameItem(a.m_from, b.m_from) && SameItem(a.m_to, b.m_to);
        }

        private static bool SameConversion(CookingStation.ItemConversion a, CookingStation.ItemConversion b) {
            return a != null && b != null && SameItem(a.m_from, b.m_from) && SameItem(a.m_to, b.m_to);
        }

        public static void AddConversion(TrackedConversion conversion, GameObject go) {
            CaptureOriginal(go);
            switch (conversion.convertType) {
                case ConversionType.Smelter:
                    Smelter smelt = FindStation<Smelter>(go);
                    if (smelt == null) {
                        Logger.LogWarning($"Smelter component not found on {go.name}, no conversions can be added to it.");
                        break;
                    }
                    // A station that shipped with no conversions at all has a null list, give it one to fill
                    if (smelt.m_conversion == null) { smelt.m_conversion = new List<Smelter.ItemConversion>(); }
                    AddEntries(smelt.m_conversion, conversion.updatedSmelterConversions, SameConversion);
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = FindStation<Fermenter>(go);
                    if (ferment == null) {
                        Logger.LogWarning($"Fermenter component not found on {go.name}, no conversions can be added to it.");
                        break;
                    }
                    if (ferment.m_conversion == null) { ferment.m_conversion = new List<Fermenter.ItemConversion>(); }
                    AddEntries(ferment.m_conversion, conversion.updatedFermenterConversions, SameConversion);
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = FindStation<CookingStation>(go);
                    if (cook == null) {
                        Logger.LogWarning($"CookingStation component not found on {go.name}, no conversions can be added to it.");
                        break;
                    }
                    if (cook.m_conversion == null) { cook.m_conversion = new List<CookingStation.ItemConversion>(); }
                    AddEntries(cook.m_conversion, conversion.updatedCookingConversions, SameConversion);
                    break;
            }
        }

        public static void RemoveConversion(TrackedConversion conversion, GameObject go)
        {
            CaptureOriginal(go);
            switch (conversion.convertType)
            {
                case ConversionType.Smelter:
                    Smelter smelt = FindStation<Smelter>(go);
                    if (smelt == null || smelt.m_conversion == null) {
                        Logger.LogWarning($"Smelter component not found on {go.name}, no conversions can be removed from it.");
                        break;
                    }
                    RemoveEntries(smelt.m_conversion, conversion.updatedSmelterConversions, SameConversion);
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = FindStation<Fermenter>(go);
                    if (ferment == null || ferment.m_conversion == null) {
                        Logger.LogWarning($"Fermenter component not found on {go.name}, no conversions can be removed from it.");
                        break;
                    }
                    RemoveEntries(ferment.m_conversion, conversion.updatedFermenterConversions, SameConversion);
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = FindStation<CookingStation>(go);
                    if (cook == null || cook.m_conversion == null) {
                        Logger.LogWarning($"CookingStation component not found on {go.name}, no conversions can be removed from it.");
                        break;
                    }
                    RemoveEntries(cook.m_conversion, conversion.updatedCookingConversions, SameConversion);
                    break;
            }
        }

        public static void BuildConversionTracker()
        {
            TrackedConversions.Clear();
            foreach (KeyValuePair<string, ConversionModification> cov in ConversionsToModify)
            {
                if (cov.Key == null) { continue; }
                Logger.LogDebug($"Constructing Conversion modifications for {cov.Key}");

                bool valid = true;
                TrackedConversion tconv = new TrackedConversion();
                tconv.action = cov.Value.action;

                try {
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
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Could not resolve the prefab ({cov.Value.prefab}) referenced by {cov.Key}, this modification will be skipped. Define a prefab to modify to fix this. Error: {ex.Message}");
                    continue;
                }
                if (tconv.cookingStationComponent == null && tconv.fermenterComponent == null && tconv.smelterComponent == null) {
                    Logger.LogWarning($"{cov.Value.prefab} did not have an attached Fermenter, Smelter, or CookingStation. This modification will be skipped.");
                    continue;
                }
                // Each station's conversion list is left null when the entry has no conversions, so a Modify
                // that only changes timings or fuel keeps the station's own conversions
                switch (tconv.convertType) {
                    case ConversionType.Fermenter:
                        Logger.LogDebug("Setting Fermenter");
                        if (cov.Value.conversions != null) {
                            List<Fermenter.ItemConversion> fermConvs = new List<Fermenter.ItemConversion> ();
                            foreach (var conv in cov.Value.conversions) {
                                if (ResolveConversion(cov.Key, conv, true, out ItemDrop from, out ItemDrop to) == false) { continue; }
                                Fermenter.ItemConversion fconv = new Fermenter.ItemConversion();
                                fconv.m_to = to;
                                fconv.m_from = from;
                                fconv.m_producedItems = conv.amount;
                                fermConvs.Add(fconv);
                            }
                            tconv.updatedFermenterConversions = fermConvs;
                        }
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
                        if (cov.Value.conversions != null) {
                            List<CookingStation.ItemConversion> cookConvs = new List<CookingStation.ItemConversion>();
                            foreach (var conv in cov.Value.conversions) {
                                if (ResolveConversion(cov.Key, conv, true, out ItemDrop from, out ItemDrop to) == false) { continue; }
                                CookingStation.ItemConversion fconv = new CookingStation.ItemConversion();
                                fconv.m_to = to;
                                fconv.m_from = from;
                                fconv.m_cookTime = conv.cookTime;
                                cookConvs.Add(fconv);
                            }
                            tconv.updatedCookingConversions = cookConvs;
                        }
                        if (cov.Value.overCookedTime > 0) { tconv.overCookedTime = cov.Value.overCookedTime; }
                        tconv.maxFuel = NonNegative(cov.Key, "maxFuel", cov.Value.maxFuel);
                        if (cov.Value.secPerFuel.HasValue) {
                            if (cov.Value.secPerFuel.Value > 0) {
                                tconv.secPerFuel = cov.Value.secPerFuel;
                            } else {
                                Logger.LogWarning($"{cov.Key} has a secPerFuel of {cov.Value.secPerFuel.Value}, it must be above 0 and will be ignored.");
                            }
                        }
                        tconv.fuelItem = ResolveFuelItem(cov.Key, cov.Value.fuelItem);
                        break;
                    case ConversionType.Smelter:
                        Logger.LogDebug("Setting Smelter");
                        if (cov.Value.conversions != null) {
                            List<Smelter.ItemConversion> smeltConvs = new List<Smelter.ItemConversion>();
                            foreach (var conv in cov.Value.conversions) {
                                if (ResolveConversion(cov.Key, conv, false, out ItemDrop from, out ItemDrop to) == false) { continue; }
                                Smelter.ItemConversion fconv = new Smelter.ItemConversion();
                                fconv.m_to = to;
                                fconv.m_from = from;
                                smeltConvs.Add(fconv);
                            }
                            tconv.updatedSmelterConversions = smeltConvs;
                        }
                        tconv.conversionTime = SmelterConversionTime(cov.Key, cov.Value.conversionTime);
                        // Zero is allowed for these: the game treats maxFuel 0 as needing no fuel, and
                        // fuelPerProduct 0 as never burning the fuel that is loaded
                        tconv.fuelPerProduct = NonNegative(cov.Key, "fuelPerProduct", cov.Value.fuelPerProduct);
                        tconv.maxFuel = NonNegative(cov.Key, "maxFuel", cov.Value.maxFuel);
                        tconv.maxOres = NonNegative(cov.Key, "maxOres", cov.Value.maxOres);
                        tconv.fuelItem = ResolveFuelItem(cov.Key, cov.Value.fuelItem);
                        if (tconv.action != ConversionAction.Remove) { CheckSmelterSetup(cov.Key, tconv); }
                        break;
                }
                if (valid == false) {
                    Logger.LogWarning($"Conversion request was not valid, skipping.");
                    continue;
                }
                TrackedConversions.Add(tconv);
            }

            // Smelters are the one station that works with a side left out: the battering ram burns wood and
            // makes nothing, and the frost kiln makes frozen fuel from its fuel alone. Fermenters and cooking
            // stations read both sides without checking, so for those a missing side skips the conversion.
            static bool ResolveConversion(string entry, ConversionDef conv, bool needsBothSides, out ItemDrop from, out ItemDrop to)
            {
                from = null;
                to = null;
                if (conv == null) { return false; }
                Logger.LogDebug($"Creating Conversion {conv.fromPrefab} to {conv.toPrefab}");
                bool noFrom = string.IsNullOrEmpty(conv.fromPrefab);
                bool noTo = string.IsNullOrEmpty(conv.toPrefab);
                if (noFrom && noTo) {
                    Logger.LogWarning($"{entry} has a conversion with neither a fromPrefab nor a toPrefab, it will be skipped.");
                    return false;
                }
                if (needsBothSides && (noFrom || noTo)) {
                    Logger.LogWarning($"{entry} has a conversion with no {(noFrom ? "fromPrefab" : "toPrefab")}, only smelter type stations can leave one out. This conversion will be skipped.");
                    return false;
                }
                bool fromFound = ResolveItem(entry, conv.fromPrefab, out from);
                bool toFound = ResolveItem(entry, conv.toPrefab, out to);
                return fromFound && toFound;
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

                List<Smelter.ItemConversion> supplied = tconv.updatedSmelterConversions;
                if (maxOres > 0) {
                    // The game's item menu reads the fromPrefab of every conversion and throws on a missing one
                    int skipped = supplied == null ? 0 : supplied.RemoveAll(c => c.m_from == null);
                    if (skipped > 0) {
                        Logger.LogWarning($"{entry} gives {name} {skipped} conversion(s) with no fromPrefab, but it takes items (maxOres {maxOres}). Those only work with maxOres 0 and have been skipped.");
                    }
                    return;
                }

                // What the station ends up with, an Add keeps the station's own conversions in front
                List<Smelter.ItemConversion> result = modify && supplied != null ? supplied : new List<Smelter.ItemConversion>(station.m_conversion ?? new List<Smelter.ItemConversion>());
                if (modify == false) { AddEntries(result, supplied, SameConversion); }
                if (result.Any(c => c != null && c.m_from != null)) {
                    Logger.LogWarning($"{entry} leaves {name} with maxOres 0, so it takes no items and its conversions with a fromPrefab will never run.");
                }
                if (result.Count(c => c != null && c.m_from == null) > 1) {
                    Logger.LogWarning($"{entry} leaves {name} with more than one conversion without a fromPrefab, only the first of them is ever produced.");
                }
            }

            static ItemDrop ResolveFuelItem(string entry, string fuelItem)
            {
                if (string.IsNullOrEmpty(fuelItem)) { return null; }
                GameObject fuelGo = PrefabManager.Instance.GetPrefab(fuelItem);
                ItemDrop fuel = fuelGo != null ? fuelGo.GetComponent<ItemDrop>() : null;
                if (fuel == null) {
                    Logger.LogWarning($"{entry} could not find an item named {fuelItem}, the fuel item will not be changed.");
                }
                return fuel;
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
        /// components an object actually has. The conversion lists are copied rather than referenced:
        /// AddConversion and RemoveConversion mutate the live list in place, so holding the same list
        /// would mean the snapshot changed along with the station.
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

            private bool hasFermenter;
            private List<Fermenter.ItemConversion> fermenterConversions;
            private float fermentationDuration;

            private bool hasCookingStation;
            private List<CookingStation.ItemConversion> cookingConversions;
            private ItemDrop cookingFuelItem;
            private int cookingMaxFuel;
            private bool cookingUseFuel;
            private bool cookingRequireFire;
            private int cookingSecPerFuel;

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
                    snapshot.cookingMaxFuel = cook.m_maxFuel;
                    snapshot.cookingUseFuel = cook.m_useFuel;
                    snapshot.cookingRequireFire = cook.m_requireFire;
                    snapshot.cookingSecPerFuel = cook.m_secPerFuel;
                }

                return snapshot;
            }

            /// <summary>True when the object had none of the three station components.</summary>
            public bool IsEmpty
            {
                get { return !hasSmelter && !hasFermenter && !hasCookingStation; }
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
                        cook.m_maxFuel = cookingMaxFuel;
                        cook.m_useFuel = cookingUseFuel;
                        cook.m_requireFire = cookingRequireFire;
                        cook.m_secPerFuel = cookingSecPerFuel;
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

        public static void UpdateConversionModifications(ConversionModificationCollection convMods)
        {
            ConversionsToModify.Clear();
            foreach (KeyValuePair<String, ConversionModification> entry in convMods.ConversionModifications)
            {
                ConversionsToModify.Add(entry.Key, entry.Value);
            }
        }

        public static void UpdateConversionModificationsFromRPC(string rpcRecieved)
        {
            Logger.LogInfo($"RPC Recieved: {rpcRecieved}");
            Logger.LogInfo($"RPC Data: {rpcRecieved.Length}");
            var convData = ValConfig.yamldeserializer.Deserialize<ConversionModificationCollection>(rpcRecieved);
            UpdateConversionModifications(convData);
        }

        public static void UpdateConversionModificationsFromList(List<ConversionModificationCollection> convMods)
        {
            ConversionsToModify.Clear();
            foreach (ConversionModificationCollection rcol in convMods) {
                foreach (KeyValuePair<String, ConversionModification> entry in rcol.ConversionModifications) {
                    ConversionsToModify.Add(entry.Key, entry.Value);
                }
            }
        }
    }
}

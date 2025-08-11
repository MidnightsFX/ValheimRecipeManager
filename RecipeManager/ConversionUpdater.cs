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
            Logger.LogDebug($"Applying conversion ({conv.prefab.name}) modification action: {conv.action}");

            IEnumerable<GameObject> objects = Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.name.StartsWith(conv.prefab.name));
            if (objects == null) {
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

        public static void RevertConversionModifications() {
            Logger.LogInfo($"Reverting {TrackedConversions.Count} conversion modifications");
            foreach (TrackedConversion conv in TrackedConversions) {
                Logger.LogDebug($"Applying conversion ({conv.prefab.name}) modification action: {conv.action}");
                IEnumerable<GameObject> objects = Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.name.StartsWith(conv.prefab.name));
                foreach (GameObject obj in objects) {
                    if (obj == null) { continue; }
                    switch (conv.action) {
                        case ConversionAction.Modify:
                            ModifyConversion(conv, obj);
                            break;
                        case ConversionAction.Add:
                            RemoveConversion(conv, obj);
                            break;
                        case ConversionAction.Remove:
                            AddConversion(conv, obj);
                            break;
                    }
                }

            }
        }

        private static void ModifyConversion(TrackedConversion conversion, GameObject go) {
            switch(conversion.convertType) {
                case ConversionType.Smelter:
                    Smelter smelt = go.GetComponent<Smelter>();
                    if (smelt == null) {
                        Logger.LogInfo($"Smelter component not found on {go.name}, cannot apply conversion modifications.");
                        break;
                    }
                    if (conversion.updatedSmelterConversions != null) { smelt.m_conversion = conversion.updatedSmelterConversions; }
                    if (conversion.fuelItem != null) { smelt.m_fuelItem = conversion.fuelItem; }
                    if (conversion.fuelPerProduct > 0) { smelt.m_fuelPerProduct = conversion.fuelPerProduct; }
                    if (conversion.conversionTime > 0) { smelt.m_secPerProduct = conversion.conversionTime; }
                    if (conversion.maxOres > 0) { smelt.m_maxOre = conversion.maxOres; }
                    if (conversion.maxFuel > 0) { smelt.m_maxFuel = conversion.maxFuel; }
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = go.GetComponent<Fermenter>();
                    if (ferment == null) {
                        Logger.LogInfo($"Fermenter component not found on {go.name}, cannot apply conversion modifications.");
                        break;
                    }
                    if (conversion.updatedFermenterConversions != null) { ferment.m_conversion = conversion.updatedFermenterConversions; }
                    if (conversion.conversionTime > 0) { ferment.m_fermentationDuration = conversion.conversionTime; }
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = go.GetComponent<CookingStation>();
                    if (cook == null) {
                        Logger.LogInfo($"CookingStation component not found on {go.name}, cannot apply conversion modifications.");
                        break;
                    }
                    if (conversion.updatedCookingConversions != null) { cook.m_conversion = conversion.updatedCookingConversions; }
                    if (conversion.maxFuel > 0) { cook.m_maxFuel = conversion.maxFuel; }
                    if (conversion.fuelItem != null) { cook.m_fuelItem = conversion.fuelItem; }
                    cook.m_useFuel = conversion.requiresFuel;
                    cook.m_requireFire = conversion.requiresFire;
                    if (conversion.secPerFuel > 0) { cook.m_secPerFuel = (int)conversion.secPerFuel; }
                    break;
            }
        }

        public static void AddConversion(TrackedConversion conversion, GameObject go) {
            switch (conversion.convertType) {
                case ConversionType.Smelter:
                    Smelter smelt = go.GetComponent<Smelter>();
                    foreach(var conversion_entry in conversion.updatedSmelterConversions) {
                        if (smelt.m_conversion.Contains(conversion_entry) == false) { smelt.m_conversion.Add(conversion_entry); }
                    }
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = go.GetComponent<Fermenter>();
                    foreach (var conversion_entry in conversion.updatedFermenterConversions) {
                        if (ferment.m_conversion.Contains(conversion_entry) == false) { ferment.m_conversion.Add(conversion_entry); }
                    }
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = go.GetComponent<CookingStation>();
                    foreach (var conversion_entry in conversion.updatedCookingConversions) {
                        if (cook.m_conversion.Contains(conversion_entry) == false) { cook.m_conversion.Add(conversion_entry); }
                    }
                    break;
            }
        }

        public static void RemoveConversion(TrackedConversion conversion, GameObject go)
        {
            switch (conversion.convertType)
            {
                case ConversionType.Smelter:
                    Smelter smelt = go.GetComponent<Smelter>();
                    foreach (var conversion_entry in conversion.updatedSmelterConversions) {
                        if (smelt.m_conversion.Contains(conversion_entry)) { smelt.m_conversion.Remove(conversion_entry); }
                    }
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = go.GetComponent<Fermenter>();
                    foreach (var conversion_entry in conversion.updatedFermenterConversions) {
                        if (ferment.m_conversion.Contains(conversion_entry)) { ferment.m_conversion.Remove(conversion_entry); }
                    }
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = go.GetComponent<CookingStation>();
                    foreach (var conversion_entry in conversion.updatedCookingConversions) {
                        if (cook.m_conversion.Contains(conversion_entry)) { cook.m_conversion.Remove(conversion_entry); }
                    }
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
                if (cov.Value.conversionTime > 0) { tconv.conversionTime = cov.Value.conversionTime; }

                try {
                    GameObject tgo = PrefabManager.Instance.GetPrefab(cov.Value.prefab);
                    if (tgo == null) {
                        Logger.LogWarning($"Could not find piece with name: {cov.Value.prefab}, this modification will be skipped.");
                        continue;
                    }
                    tconv.prefab = tgo;
                    Smelter tsmelt = (tgo.GetComponent<Smelter>() ?? tgo.GetComponentInChildren<Smelter>()) ?? tgo.GetComponentInParent<Smelter>();
                    if (tsmelt != null) {
                        tconv.originalSmelter = tsmelt;
                        tconv.convertType = ConversionType.Smelter;
                    }
                    Fermenter tferm = (tgo.GetComponent<Fermenter>() ?? tgo.GetComponentInChildren<Fermenter>()) ?? tgo.GetComponentInParent<Fermenter>();
                    if (tferm != null) {
                        tconv.originalFermenter = tferm;
                        tconv.convertType = ConversionType.Fermenter;
                    }
                    CookingStation tcook = (tgo.GetComponent<CookingStation>() ?? tgo.GetComponentInChildren<CookingStation>()) ?? tgo.GetComponentInParent<CookingStation>();
                    if (tcook != null) {
                        tconv.originalCookingStation = tcook;
                        tconv.convertType = ConversionType.CookingStation;
                    }
                }
                catch (Exception)
                {
                    Logger.LogWarning($"Could not find entries referenced prefab, this modification will be skipped. Define a prefab to modify to fix this.");
                }
                if (tconv.originalCookingStation == null && tconv.originalFermenter == null && tconv.originalSmelter == null) {
                    Logger.LogWarning($"{tconv.prefab.name} did not have an attached Fermenter, Smelter, or CookingStation. This modification will be skipped.");
                    continue;
                }
                switch (tconv.convertType) {
                    case ConversionType.Fermenter:
                        Logger.LogDebug("Setting Fermenter");
                        List<Fermenter.ItemConversion> fermConvs = new List<Fermenter.ItemConversion> ();
                        if (cov.Value.conversions != null && cov.Value.conversions.Count > 0) {
                            foreach (var conv in cov.Value.conversions) {
                                Logger.LogDebug($"Creating Conversion {conv.fromPrefab} to {conv.toPrefab}");
                                GetConversionGOs(conv, out GameObject togo, out GameObject fromgo);
                                if (togo == null || fromgo == null) { continue; }
                                Fermenter.ItemConversion fconv = new Fermenter.ItemConversion();
                                fconv.m_to = togo.GetComponent<ItemDrop>();
                                fconv.m_from = fromgo.GetComponent<ItemDrop>();
                                fconv.m_producedItems = conv.amount;
                                fermConvs.Add(fconv);
                            }
                        }
                        tconv.updatedFermenterConversions = fermConvs;
                        break;
                    case ConversionType.CookingStation:
                        Logger.LogDebug("Setting CookingStation");
                        List<CookingStation.ItemConversion> cookConvs = new List<CookingStation.ItemConversion>();
                        if (cov.Value.conversions != null && cov.Value.conversions.Count > 0) {
                            foreach (var conv in cov.Value.conversions) {
                                Logger.LogDebug($"Creating Conversion {conv.fromPrefab} to {conv.toPrefab}");
                                GetConversionGOs(conv, out GameObject togo, out GameObject fromgo);
                                if (togo == null || fromgo == null) { continue; }
                                CookingStation.ItemConversion fconv = new CookingStation.ItemConversion();
                                fconv.m_to = togo.GetComponent<ItemDrop>();
                                fconv.m_from = fromgo.GetComponent<ItemDrop>();
                                fconv.m_cookTime = conv.cookTime;
                                cookConvs.Add(fconv);
                            }
                        }
                        tconv.updatedCookingConversions = cookConvs;
                        tconv.requiresFuel = cov.Value.requiresFuel;
                        tconv.requiresFire = cov.Value.requiresFire;
                        if (cov.Value.overCookedTime > 0) { tconv.overCookedTime = cov.Value.overCookedTime; }
                        break;
                    case ConversionType.Smelter:
                        Logger.LogDebug("Setting Smelter");
                        List<Smelter.ItemConversion> smeltConvs = new List<Smelter.ItemConversion>();
                        if (cov.Value.conversions != null && cov.Value.conversions.Count > 0) {
                            foreach (var conv in cov.Value.conversions) {
                                Logger.LogDebug($"Creating Conversion {conv.fromPrefab} to {conv.toPrefab}");
                                GetConversionGOs(conv, out GameObject togo, out GameObject fromgo);
                                if (togo == null || fromgo == null) { continue; }
                                Smelter.ItemConversion fconv = new Smelter.ItemConversion();
                                fconv.m_to = togo.GetComponent<ItemDrop>();
                                fconv.m_from = fromgo.GetComponent<ItemDrop>();
                                smeltConvs.Add(fconv);
                            }
                        }
                        tconv.updatedSmelterConversions = smeltConvs;
                        tconv.fuelPerProduct = cov.Value.fuelPerProduct;
                        tconv.requiresFuel = cov.Value.requiresFuel;
                        if (cov.Value.maxOres > 0) { tconv.maxOres = cov.Value.maxOres; }
                        if (cov.Value.maxFuel > 0) { tconv.maxFuel = cov.Value.maxFuel; }
                        if (cov.Value.fuelItem != null && cov.Value.fuelItem != "") {
                            GameObject fuelGo = PrefabManager.Instance.GetPrefab(cov.Value.fuelItem);
                            if (fuelGo != null) {
                                ItemDrop idfuel = fuelGo.GetComponent<ItemDrop>();
                                if (idfuel) { tconv.requiresFuel = true; }
                            }
                        }
                        break;
                }
                if (valid == false) {
                    Logger.LogWarning($"Conversion request was not valid, skipping.");
                    continue;
                }
                TrackedConversions.Add(tconv);
            }

            static void GetConversionGOs(ConversionDef conv, out GameObject togo, out GameObject fromgo)
            {
                fromgo = PrefabManager.Instance.GetPrefab(conv.fromPrefab);
                togo = PrefabManager.Instance.GetPrefab(conv.toPrefab);
                if (fromgo == null)
                {
                    Logger.LogWarning($"Conversion could not find {conv.fromPrefab}, this conversion will be skipped.");
                }
                if (togo == null)
                {
                    Logger.LogWarning($"Conversion could not find {conv.toPrefab}, this conversion will be skipped.");
                }
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

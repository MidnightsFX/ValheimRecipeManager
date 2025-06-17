using Jotunn.Managers;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    internal class ConversionUpdater
    {
        public static Dictionary<String, ConversionModification> ConversionsToModify = new Dictionary<String, ConversionModification>();
        public static List<TrackedConversion> TrackedConversions = new List<TrackedConversion>();

        public static void InitialSychronization()
        {
            BuildConversionTracker();
            ConversionUpdateRunner();
        }

        public static void ConversionUpdateRunner()
        {
            Logger.LogInfo($"Applying {TrackedConversions.Count} conversion modifications");
            foreach (TrackedConversion piece in TrackedConversions) {
                ApplyPieceModifications(piece);
            }
        }

        public static void ApplyPieceModifications(TrackedConversion conv)
        {
            Logger.LogDebug($"Applying piece ({conv.prefab.name}) modification action: {conv.action}");
            switch (conv.action)
            {
                case ConversionAction.Modify:
                    ModifyConversion(conv);
                    break;
                case ConversionAction.Add:
                    AddConversion(conv);
                    break;
                case ConversionAction.Remove:
                    RemoveConversion(conv);
                    break;
            }
        }

        private static void ModifyConversion(TrackedConversion conversion)
        {
            switch(conversion.convertType)
            {
                case ConversionType.Smelter:
                    Smelter smelt = conversion.prefab.GetComponent<Smelter>();
                    smelt.m_conversion = conversion.updatedSmelterConversions;
                    if (conversion.fuelItem != null) { smelt.m_fuelItem = conversion.fuelItem; }
                    if (conversion.fuelPerProduct > 0) { smelt.m_fuelPerProduct = conversion.fuelPerProduct; }
                    if (conversion.conversionTime > 0) { smelt.m_secPerProduct = conversion.conversionTime; }
                    if (conversion.maxOres > 0) { smelt.m_maxOre = conversion.maxOres; }
                    if (conversion.maxFuel > 0) { smelt.m_maxFuel = conversion.maxFuel; }
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = conversion.prefab.GetComponent<Fermenter>();
                    ferment.m_conversion = conversion.updatedFermenterConversions;
                    if (conversion.conversionTime > 0) { ferment.m_fermentationDuration = conversion.conversionTime; }
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = conversion.prefab.GetComponent<CookingStation>();
                    cook.m_conversion = conversion.updatedCookingConversions;
                    if (conversion.maxFuel > 0) { cook.m_maxFuel = conversion.maxFuel; }
                    if (conversion.fuelItem != null) { cook.m_fuelItem = conversion.fuelItem; }
                    break;
            }
        }

        public static void AddConversion(TrackedConversion conversion)
        {
            switch (conversion.convertType)
            {
                case ConversionType.Smelter:
                    Smelter smelt = conversion.prefab.GetComponent<Smelter>();
                    foreach(var conversion_entry in conversion.updatedSmelterConversions) {
                        if (smelt.m_conversion.Contains(conversion_entry) == false) { smelt.m_conversion.Add(conversion_entry); }
                    }
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = conversion.prefab.GetComponent<Fermenter>();
                    foreach (var conversion_entry in conversion.updatedFermenterConversions) {
                        if (ferment.m_conversion.Contains(conversion_entry) == false) { ferment.m_conversion.Add(conversion_entry); }
                    }
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = conversion.prefab.GetComponent<CookingStation>();
                    foreach (var conversion_entry in conversion.updatedCookingConversions) {
                        if (cook.m_conversion.Contains(conversion_entry) == false) { cook.m_conversion.Add(conversion_entry); }
                    }
                    break;
            }
        }

        public static void RemoveConversion(TrackedConversion conversion)
        {
            switch (conversion.convertType)
            {
                case ConversionType.Smelter:
                    Smelter smelt = conversion.prefab.GetComponent<Smelter>();
                    foreach (var conversion_entry in conversion.updatedSmelterConversions) {
                        if (smelt.m_conversion.Contains(conversion_entry)) { smelt.m_conversion.Remove(conversion_entry); }
                    }
                    break;
                case ConversionType.Fermenter:
                    Fermenter ferment = conversion.prefab.GetComponent<Fermenter>();
                    foreach (var conversion_entry in conversion.updatedFermenterConversions) {
                        if (ferment.m_conversion.Contains(conversion_entry)) { ferment.m_conversion.Remove(conversion_entry); }
                    }
                    break;
                case ConversionType.CookingStation:
                    CookingStation cook = conversion.prefab.GetComponent<CookingStation>();
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
                Logger.LogDebug($"Construction Conversion modifications for {cov.Key}");

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
                    Smelter tsmelt = tgo.GetComponent<Smelter>();
                    if (tsmelt != null) {
                        tconv.originalSmelter = tsmelt;
                        tconv.convertType = ConversionType.Smelter;
                    }
                    Fermenter tferm = tgo.GetComponent<Fermenter>();
                    if (tferm != null) {
                        tconv.originalFermenter = tferm;
                        tconv.convertType = ConversionType.Fermenter;
                    }
                    CookingStation tcook = tgo.GetComponent<CookingStation>();
                    if (tcook != null) {
                        tconv.originalCookingStation = tcook;
                        tconv.convertType = ConversionType.CookingStation;
                    }
                }
                catch (Exception)
                {
                    Logger.LogWarning($"Could not find entries referenced prefab, this modification will be skipped. Define a prefab to modify to fix this.");
                }
                if (tconv.originalCookingStation == null && tconv.originalFermenter == null && tconv.originalSmelter) {
                    Logger.LogWarning($"{tconv.prefab.name} did not have an attached Fermenter, Smelter, or CookingStation. This modification will be skipped.");
                    continue;
                }
                switch (tconv.convertType) {
                    case ConversionType.Fermenter:
                        List<Fermenter.ItemConversion> fermConvs = new List<Fermenter.ItemConversion> ();
                        foreach(var conv in cov.Value.conversions) {
                            GetConversionGOs(conv, out GameObject togo, out GameObject fromgo);
                            if (togo == null || fromgo == null) { continue; }
                            Fermenter.ItemConversion fconv = new Fermenter.ItemConversion();
                            fconv.m_to = togo.GetComponent<ItemDrop>();
                            fconv.m_from = fromgo.GetComponent<ItemDrop>();
                            fconv.m_producedItems = conv.amount;
                            fermConvs.Add(fconv);
                        }
                        if (fermConvs.Count == 0) { valid = false; }
                        tconv.updatedFermenterConversions = fermConvs;
                        break;
                    case ConversionType.CookingStation:
                        List<CookingStation.ItemConversion> cookConvs = new List<CookingStation.ItemConversion>();
                        foreach (var conv in cov.Value.conversions)
                        {
                            GetConversionGOs(conv, out GameObject togo, out GameObject fromgo);
                            if (togo == null || fromgo == null) { continue; }
                            CookingStation.ItemConversion fconv = new CookingStation.ItemConversion();
                            fconv.m_to = togo.GetComponent<ItemDrop>();
                            fconv.m_from = fromgo.GetComponent<ItemDrop>();
                            fconv.m_cookTime = conv.cookTime;
                            cookConvs.Add(fconv);
                        }
                        if (cookConvs.Count == 0) { valid = false; }
                        tconv.updatedCookingConversions = cookConvs;
                        tconv.requiresFuel = cov.Value.requiresFuel;
                        if (cov.Value.overCookedTime > 0) { tconv.overCookedTime = cov.Value.overCookedTime; }
                        break;
                    case ConversionType.Smelter:
                        List<Smelter.ItemConversion> smeltConvs = new List<Smelter.ItemConversion>();
                        foreach (var conv in cov.Value.conversions)
                        {
                            GetConversionGOs(conv, out GameObject togo, out GameObject fromgo);
                            if (togo == null || fromgo == null) { continue; }
                            Smelter.ItemConversion fconv = new Smelter.ItemConversion();
                            fconv.m_to = togo.GetComponent<ItemDrop>();
                            fconv.m_from = fromgo.GetComponent<ItemDrop>();
                            smeltConvs.Add(fconv);
                        }
                        if (smeltConvs.Count == 0) { valid = false; }
                        tconv.updatedSmelterConversions = smeltConvs;
                        tconv.fuelPerProduct = cov.Value.fuelPerProduct;
                        tconv.requiresFuel = cov.Value.requiresFuel;
                        if (cov.Value.maxOres > 0) { tconv.maxOres = cov.Value.maxOres; }
                        if (cov.Value.maxFuel > 0) { tconv.maxFuel = cov.Value.maxFuel; }
                        if (cov.Value.fuelItem != null && cov.Value.fuelItem != "") {
                            GameObject fuelGo = PrefabManager.Instance.GetPrefab(cov.Value.fuelItem);
                            if (fuelGo != null) {
                                ItemDrop idfuel = fuelGo.GetComponent<ItemDrop>();
                                if (idfuel) { tconv.requiresFuel = idfuel; }
                            }
                        }
                        break;
                }
                if (valid == false) {
                    Logger.LogWarning($"Conversion request was not valid, skipping.");
                }
            }

            static void GetConversionGOs(ConversionDef conv, out GameObject togo, out GameObject fromgo)
            {
                fromgo = PrefabManager.Instance.GetPrefab(conv.fromPrefab);
                togo = PrefabManager.Instance.GetPrefab(conv.toPrefab);
                if (fromgo == null)
                {
                    Logger.LogWarning($"Converstion could not find {conv.fromPrefab}, this conversion will be skipped.");
                }
                if (togo == null)
                {
                    Logger.LogWarning($"Converstion could not find {conv.toPrefab}, this conversion will be skipped.");
                }
            }
        }
    }
}

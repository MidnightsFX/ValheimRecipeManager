using Jotunn.Configs;
using Jotunn.Entities;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    internal class ConversionPrintCommand : ConsoleCommand
    {
        public override string Name => "RM_Conversion_PrintAll";

        public override string Help => "Prints all conversions.";

        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Starting to dump conversion list"); }
            String ExistingConversions = Path.Combine(BepInEx.Paths.ConfigPath, "RecipeManager", "AllConversionsDebug.yaml");
            ConversionModificationCollection ConversionCollection = new ConversionModificationCollection();

            List<Smelter> smelterConversions = Resources.FindObjectsOfTypeAll<Smelter>().Where(pc => pc.name.EndsWith("(Clone)") != true && Regex.IsMatch(pc.name.Trim(), @"\(\d+\)") != true).ToList<Smelter>();
            List<Fermenter> fermenterConversions = Resources.FindObjectsOfTypeAll<Fermenter>().Where(pc => pc.name.EndsWith("(Clone)") != true && Regex.IsMatch(pc.name.Trim(), @"\(\d+\)") != true).ToList<Fermenter>();
            List<CookingStation> cookingConversions = Resources.FindObjectsOfTypeAll<CookingStation>().Where(pc => pc.name.EndsWith("(Clone)") != true && Regex.IsMatch(pc.name.Trim(), @"\(\d+\)") != true).ToList<CookingStation>();

            foreach (var smelter in smelterConversions) {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Building Smelter definition {smelter.gameObject.name}"); }
                ConversionModification convMod = new ConversionModification();
                Logger.LogInfo($"Set name {smelter.gameObject.name}");
                convMod.prefab = smelter.gameObject.name;
                convMod.action = ConversionAction.Modify;
                Logger.LogInfo($"Set fuelPerProduct {smelter.m_fuelPerProduct}");
                convMod.fuelPerProduct = smelter.m_fuelPerProduct;
                Logger.LogInfo($"Set maxFuel {smelter.m_maxFuel}");
                convMod.maxFuel = smelter.m_maxFuel;
                Logger.LogInfo($"Set maxOres {smelter.m_maxOre}");
                convMod.maxOres = smelter.m_maxOre;
                if (smelter.m_fuelItem != null) {
                    Logger.LogInfo($"Set fuelItem {smelter.m_fuelItem.gameObject.name}");
                    convMod.fuelItem = smelter.m_fuelItem.gameObject.name;
                }
                Logger.LogInfo($"Set requiresFuel {smelter.m_fuelItem != null}");
                //convMod.requiresFuel = smelter.m_fuelItem != null;
                Logger.LogInfo($"Set conversionTime {smelter.m_secPerProduct}");
                convMod.conversionTime = smelter.m_secPerProduct;
                List <ConversionDef> conversions = new List<ConversionDef>();
                if (smelter.m_conversion != null && smelter.m_conversion.Count > 0) {
                    foreach (Smelter.ItemConversion conversion in smelter.m_conversion) {
                        if (conversion == null) { continue; }
                        ConversionDef convDef = new ConversionDef();
                        if (conversion.m_from != null) {
                            convDef.fromPrefab = conversion.m_from.gameObject.name;
                            Logger.LogInfo($"Convert From: {conversion.m_from.gameObject.name}");
                        }
                        if (conversion.m_to != null) {
                            convDef.toPrefab = conversion.m_to.gameObject.name;
                            Logger.LogInfo($"Convert To: {conversion.m_to.gameObject.name}");
                        }
                        conversions.Add(convDef);
                    }
                }
                
                convMod.conversions = conversions;
                Logger.LogInfo($"Added.");
                ConversionCollection.ConversionModifications.Add(smelter.gameObject.name, convMod);
            }

            foreach (var fermenter in fermenterConversions) {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Building Fermenter definition {fermenter.m_name}"); }
                ConversionModification convMod = new ConversionModification();
                convMod.prefab = fermenter.gameObject.name;
                convMod.action = ConversionAction.Modify;
                convMod.conversionTime = fermenter.m_fermentationDuration;
                //convMod.requiresFuel = false;
                List<ConversionDef> conversions = new List<ConversionDef>();
                foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion) {
                    if (conversion == null) { continue; }
                    ConversionDef convDef = new ConversionDef();
                    convDef.fromPrefab = conversion.m_from.name;
                    convDef.toPrefab = conversion.m_to.name;
                    convDef.amount = conversion.m_producedItems;
                    conversions.Add(convDef);
                }
                convMod.conversions = conversions;
                ConversionCollection.ConversionModifications.Add(fermenter.gameObject.name, convMod);
            }

            foreach (var cookingStation in cookingConversions) {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Building Cooking Station definition {cookingStation.m_name}"); }
                ConversionModification convMod = new ConversionModification();
                convMod.prefab = cookingStation.gameObject.name;
                convMod.action = ConversionAction.Modify;
                //convMod.requiresFuel = cookingStation.m_useFuel;
                if (cookingStation.m_fuelItem != null) {
                    convMod.fuelItem = cookingStation.m_fuelItem.name;
                }
                if (convMod.overCookedItem != null) {
                    convMod.overCookedItem = cookingStation.m_overCookedItem.name;
                }
                convMod.secPerFuel = cookingStation.m_secPerFuel;
                convMod.maxFuel = cookingStation.m_maxFuel;
                //convMod.requiresFuel = cookingStation.m_useFuel;
                //convMod.requiresFire = cookingStation.m_requireFire;
                List<ConversionDef> conversions = new List<ConversionDef>();
                foreach (CookingStation.ItemConversion conversion in cookingStation.m_conversion) {
                    if (conversion == null) { continue; }
                    ConversionDef convDef = new ConversionDef();
                    if (conversion.m_from != null) { convDef.fromPrefab = conversion.m_from.name; }
                    if (conversion.m_to != null) { convDef.toPrefab = conversion.m_to.name; }
                    convDef.cookTime = conversion.m_cookTime;
                    conversions.Add(convDef);
                }
                convMod.conversions = conversions;
                ConversionCollection.ConversionModifications.Add(cookingStation.gameObject.name, convMod);
            }

            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Serializing and printing conversions."); }
            var yaml = ValConfig.yamlserializer.Serialize(ConversionCollection);
            using (StreamWriter writetext = new StreamWriter(ExistingConversions)) {
                writetext.WriteLine(yaml);
            }   
        }
    }

    internal class ConversionReloadCommand : ConsoleCommand
    {
        public override string Name => "RM_Conversion_Reload";

        public override string Help => "Resynchronizes conversion modifications.";

        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            ConversionUpdater.RevertConversionModifications();
            ValConfig.ReloadConversionFiles();
            ConversionUpdater.BuildConversionTracker();
            ConversionUpdater.ConversionUpdateRunner();
            // ValConfig.SendUpdatedPieceConfigs();
        }
    }
}

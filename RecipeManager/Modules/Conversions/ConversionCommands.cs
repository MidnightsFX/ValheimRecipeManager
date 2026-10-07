using Jotunn.Entities;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    internal class ConversionPrintCommand : ConsoleCommand
    {
        internal const string DumpFileName = "AllConversionsDebug.yaml";

        public override string Name => "RM_Conversion_PrintAll";

        public override string Help => "Prints all conversions.";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Starting to dump conversion list"); }
            String ExistingConversions = Path.Combine(BepInEx.Paths.ConfigPath, "RecipeManager", DumpFileName);
            ConversionModificationCollection ConversionCollection = new ConversionModificationCollection();

            List<Smelter> smelterConversions = Resources.FindObjectsOfTypeAll<Smelter>().Where(pc => IsPrefab(pc)).ToList<Smelter>();
            List<Fermenter> fermenterConversions = Resources.FindObjectsOfTypeAll<Fermenter>().Where(pc => IsPrefab(pc)).ToList<Fermenter>();
            List<CookingStation> cookingConversions = Resources.FindObjectsOfTypeAll<CookingStation>().Where(pc => IsPrefab(pc)).ToList<CookingStation>();
            List<Incinerator> incinerators = Resources.FindObjectsOfTypeAll<Incinerator>().Where(pc => IsPrefab(pc)).ToList<Incinerator>();

            foreach (var smelter in smelterConversions) {
                string prefabName = PrefabName(smelter);
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Building Smelter definition {prefabName}"); }
                ConversionModification convMod = new ConversionModification
                {
                    prefab = prefabName,
                    action = ConversionAction.Modify,
                    fuelPerProduct = smelter.m_fuelPerProduct,
                    maxFuel = smelter.m_maxFuel,
                    maxOres = smelter.m_maxOre,
                    fuelItem = ItemName(smelter.m_fuelItem),
                    conversionTime = smelter.m_secPerProduct,
                    spawnStack = smelter.m_spawnStack,
                    requiresRoof = smelter.m_requiresRoof,
                    conversions = new List<ConversionDef>()
                };
                foreach (Smelter.ItemConversion conversion in smelter.m_conversion ?? new List<Smelter.ItemConversion>()) {
                    if (conversion == null) { continue; }
                    convMod.conversions.Add(new ConversionDef { fromPrefab = ItemName(conversion.m_from), toPrefab = ItemName(conversion.m_to) });
                }
                AddDefinition(ConversionCollection, convMod);
            }

            foreach (var fermenter in fermenterConversions) {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Building Fermenter definition {fermenter.m_name}"); }
                ConversionModification convMod = new ConversionModification
                {
                    prefab = PrefabName(fermenter),
                    action = ConversionAction.Modify,
                    conversionTime = fermenter.m_fermentationDuration,
                    conversions = new List<ConversionDef>()
                };
                foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion ?? new List<Fermenter.ItemConversion>()) {
                    if (conversion == null) { continue; }
                    convMod.conversions.Add(new ConversionDef { fromPrefab = ItemName(conversion.m_from), toPrefab = ItemName(conversion.m_to), amount = conversion.m_producedItems });
                }
                AddDefinition(ConversionCollection, convMod);
            }

            foreach (var cookingStation in cookingConversions) {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Building Cooking Station definition {cookingStation.m_name}"); }
                ConversionModification convMod = new ConversionModification
                {
                    prefab = PrefabName(cookingStation),
                    action = ConversionAction.Modify,
                    fuelItem = ItemName(cookingStation.m_fuelItem),
                    overCookedItem = ItemName(cookingStation.m_overCookedItem),
                    secPerFuel = cookingStation.m_secPerFuel,
                    maxFuel = cookingStation.m_maxFuel,
                    requiresFire = cookingStation.m_requireFire,
                    conversions = new List<ConversionDef>()
                };
                foreach (CookingStation.ItemConversion conversion in cookingStation.m_conversion ?? new List<CookingStation.ItemConversion>()) {
                    if (conversion == null) { continue; }
                    convMod.conversions.Add(new ConversionDef { fromPrefab = ItemName(conversion.m_from), toPrefab = ItemName(conversion.m_to), cookTime = conversion.m_cookTime });
                }
                AddDefinition(ConversionCollection, convMod);
            }

            foreach (Incinerator incinerator in incinerators) {
                ConversionModification convMod = new ConversionModification
                {
                    prefab = PrefabName(incinerator),
                    action = ConversionAction.Modify,
                    defaultResult = ItemName(incinerator.m_defaultResult),
                    defaultCost = incinerator.m_defaultCost,
                    incineratorConversions = new List<IncineratorConversionDef>()
                };
                foreach (Incinerator.IncineratorConversion conversion in incinerator.m_conversions ?? new List<Incinerator.IncineratorConversion>()) {
                    if (conversion == null) { continue; }
                    convMod.incineratorConversions.Add(new IncineratorConversionDef {
                        result = ItemName(conversion.m_result),
                        resultAmount = conversion.m_resultAmount,
                        priority = conversion.m_priority,
                        anyOneIngredient = conversion.m_requireOnlyOneIngredient,
                        ingredients = (conversion.m_requirements ?? new List<Incinerator.Requirement>())
                            .Where(requirement => requirement != null && requirement.m_resItem != null)
                            .Select(requirement => new IncineratorIngredientDef { prefab = ItemName(requirement.m_resItem), amount = requirement.m_amount })
                            .ToList()
                    });
                }
                AddDefinition(ConversionCollection, convMod);
            }

            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Serializing and printing conversions."); }
            var yaml = ValConfig.yamlserializer.Serialize(ConversionCollection);
            using (StreamWriter writetext = new StreamWriter(ExistingConversions)) {
                writetext.WriteLine("# Reference dump of every conversion station, as it currently is.");
                writetext.WriteLine("# This file is NOT loaded by the mod, copy the entries you want to change into your own Conversions.yaml.");
                writetext.WriteLine();
                writetext.WriteLine(yaml);
            }
            ItemCommandUtils.Respond($"Dumped {ConversionCollection.ConversionModifications.Count} station(s) to {ExistingConversions}");
        }

        private static void AddDefinition(ConversionModificationCollection collection, ConversionModification convMod)
        {
            // A prefab with two kinds of station, or a mod's copy sharing the name, is listed once
            if (collection.ConversionModifications.ContainsKey(convMod.prefab)) {
                Logger.LogDebug($"{convMod.prefab} was already dumped, skipping its second station.");
                return;
            }
            collection.ConversionModifications.Add(convMod.prefab, convMod);
        }

        private static string ItemName(ItemDrop item)
        {
            return item != null ? item.gameObject.name : null;
        }

        private static string PrefabName(Component station)
        {
            return ConversionUpdater.StationRoot(station).name;
        }

        /// <summary>Skips the copies placed in the world, only the prefabs themselves are printed.</summary>
        private static bool IsPrefab(Component station)
        {
            string name = PrefabName(station);
            return name.EndsWith("(Clone)") != true && Regex.IsMatch(name.Trim(), @"\(\d+\)") != true;
        }
    }

    internal class ConversionReloadCommand : ConsoleCommand
    {
        public override string Name => "RM_Conversion_Reload";

        public override string Help => "Resynchronizes conversion modifications.";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            ValConfig.Conversions.ReloadFromDisk();
        }
    }
}

﻿using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;

namespace RecipeManager
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    internal class RecipeManager : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.RecipeManager";
        public const string PluginName = "RecipeManager";
        public const string PluginVersion = "0.3.1";

        public Common.Config cfg;

        private void Awake()
        {
            cfg = new Common.Config(Config);
            // Update the list of Recipes to track, update etc each time the object db is re-init'd
            ItemManager.OnItemsRegistered += RecipeUpdater.InitialRecipesAndSynchronize;
            MinimapManager.OnVanillaMapDataLoaded += RecipeUpdater.SecondaryRecipeSync;
            // Jotunn.Logger.LogInfo("Recipe Updater Loaded.");

            CommandManager.Instance.AddConsoleCommand(new RecipeReloadCommand());
            CommandManager.Instance.AddConsoleCommand(new RecipePrintCommand());
        }

    }
}
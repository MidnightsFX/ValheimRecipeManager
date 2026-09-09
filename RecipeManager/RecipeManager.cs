using BepInEx;
using BepInEx.Logging;
using Jotunn.Managers;
using Jotunn.Utils;
using RecipeManager.Common;
using System.Collections;
using UnityEngine;

namespace RecipeManager
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.VersionCheckOnly, VersionStrictness.Patch)]
    internal class RecipeManager : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.RecipeManager";
        public const string PluginName = "RecipeManager";
        public const string PluginVersion = "0.6.1";

        public Common.ValConfig cfg;
        public static ManualLogSource Log;

        public void Awake()
        {
            Log = this.Logger;
            cfg = new Common.ValConfig(Config);
            // Custom items have to exist before anything can reference them, and before the object db is
            // built, so they are created as soon as the vanilla items are available to clone from.
            PrefabManager.OnVanillaPrefabsAvailable += ItemUpdater.CreateCustomItems;
            // Update the list of Recipes to track, update etc each time the object db is re-init'd
            ItemManager.OnItemsRegistered += RecipeUpdater.InitialRecipesAndSynchronize;
            ItemManager.OnItemsRegistered += PieceUpdater.InitialSychronization;
            ItemManager.OnItemsRegistered += ConversionUpdater.InitialSychronization;
            // Items that already exist can only be found, and their status effects only looked up, once
            // the object db is complete - which is after the cloned items have been registered into it
            ItemManager.OnItemsRegistered += ItemUpdater.ApplyItemModifications;
            // Status effects only exist once the object db is complete, which is after the items are cloned
            ItemManager.OnItemsRegistered += ItemUpdater.ResolveDeferredReferences;
            MinimapManager.OnVanillaMapDataLoaded += Common.ValConfig.ReloadPieceFiles;
            MinimapManager.OnVanillaMapDataLoaded += RecipeUpdater.SecondaryRecipeSync;
            MinimapManager.OnVanillaMapDataLoaded += PieceUpdater.BuildPieceTracker;
            MinimapManager.OnVanillaMapDataLoaded += PieceUpdater.PieceUpdateRunner;
            MinimapManager.OnVanillaMapDataLoaded += ConversionUpdater.ConversionUpdateRunner;
            MinimapManager.OnVanillaMapDataLoaded += ItemUpdater.ApplyItemModifications;
            // Jotunn.Logger.LogInfo("Recipe Updater Loaded.");
            MinimapManager.OnVanillaMapDataLoaded += SecondarySync;

            CommandManager.Instance.AddConsoleCommand(new RecipeReloadCommand());
            CommandManager.Instance.AddConsoleCommand(new RecipePrintCommand());
            CommandManager.Instance.AddConsoleCommand(new RecipeUnapplyCommand());

            CommandManager.Instance.AddConsoleCommand(new PiecePrintCommand());
            CommandManager.Instance.AddConsoleCommand(new PieceReloadCommand());

            CommandManager.Instance.AddConsoleCommand(new ConversionPrintCommand());
            CommandManager.Instance.AddConsoleCommand(new ConversionReloadCommand());

            CommandManager.Instance.AddConsoleCommand(new ItemPrintCommand());
            CommandManager.Instance.AddConsoleCommand(new ItemReloadCommand());
            CommandManager.Instance.AddConsoleCommand(new TextureDumpCommand());
        }

        internal static void SecondarySync() {
            if (ZNet.instance != null && ZNet.instance.isActiveAndEnabled) {
                ZNet.instance.StartCoroutine(RunDelayedSync());
            }
        }

        internal static IEnumerator RunDelayedSync() {
            yield return new WaitForSeconds(ValConfig.ValidationRecheckDelay.Value);
            if (ValConfig.EnableDelayedValidationRun.Value == false) { yield break; }
            RecipeUpdater.SecondaryRecipeSync();
            PieceUpdater.PieceUpdateRunner();
            ConversionUpdater.ConversionUpdateRunner();
            ItemUpdater.ApplyItemModifications();
        }

    }
}
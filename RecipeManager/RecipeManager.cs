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
        public const string PluginVersion = "0.11.0";

        public Common.ValConfig cfg;
        public static ManualLogSource Log;

        public void Awake()
        {
            Log = this.Logger;
            // Registered before any config loads. Loading this machine's files decides which are needed, and
            // patching now, before the game first runs the code they change, is what makes them take.
            PatchManager.Register(Patches.RecipeUpgradeCosts.Feature);
            PatchManager.Register(Patches.RecipeStationLevelCap.Feature);
            PatchManager.Register(Patches.StatusEffectStatBonus.Feature);
            PatchManager.Register(Patches.PieceStationLevel.Feature);
            cfg = new Common.ValConfig(Config);
            // Fires on every main menu. After playing on a server, this machine's own files apply again.
            PrefabManager.OnVanillaPrefabsAvailable += Common.ValConfig.ReloadLocalConfigs;
            // Custom items have to exist before anything can reference them, and before the object db is
            // built, so they are created as soon as the vanilla items are available to clone from.
            PrefabManager.OnVanillaPrefabsAvailable += ItemUpdater.CreateCustomItems;
            // Added pieces likewise, Jotunn puts them in their tools when the object db is built
            PrefabManager.OnVanillaPrefabsAvailable += PieceCreator.CreateCustomPieces;
            // Status effects go first, the items that point at them are resolved after
            ItemManager.OnItemsRegistered += StatusEffectUpdater.InitialSynchronization;
            // Update the list of Recipes to track, update etc each time the object db is re-init'd
            ItemManager.OnItemsRegistered += RecipeUpdater.InitialRecipesAndSynchronize;
            ItemManager.OnItemsRegistered += PieceUpdater.InitialSychronization;
            ItemManager.OnItemsRegistered += ConversionUpdater.InitialSychronization;
            // Items that already exist can only be found, and their status effects only looked up, once
            // the object db is complete - which is after the cloned items have been registered into it
            ItemManager.OnItemsRegistered += ItemUpdater.ApplyItemModifications;
            // Status effects only exist once the object db is complete, which is after the items are cloned
            ItemManager.OnItemsRegistered += ItemUpdater.ResolveDeferredReferences;
            // Re-applied once the map is up, for mods that add or change things late. The configuration in use
            // is the one already loaded, which on a client is the server's.
            // Status effects again first, for effects other mods add late, then the items that point at them
            MinimapManager.OnVanillaMapDataLoaded += StatusEffectUpdater.Reapply;
            MinimapManager.OnVanillaMapDataLoaded += RecipeUpdater.SecondaryRecipeSync;
            MinimapManager.OnVanillaMapDataLoaded += PieceUpdater.BuildPieceTracker;
            MinimapManager.OnVanillaMapDataLoaded += PieceUpdater.PieceUpdateRunner;
            MinimapManager.OnVanillaMapDataLoaded += ConversionUpdater.ConversionUpdateRunner;
            MinimapManager.OnVanillaMapDataLoaded += ItemUpdater.ApplyItemModifications;
            MinimapManager.OnVanillaMapDataLoaded += ItemUpdater.ResolveDeferredReferences;
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

            CommandManager.Instance.AddConsoleCommand(new StatusEffectPrintCommand());
            CommandManager.Instance.AddConsoleCommand(new StatusEffectReloadCommand());

            CommandManager.Instance.AddConsoleCommand(new WackyImportCommand());
            CommandManager.Instance.AddConsoleCommand(new Patches.PatchesCommand());
        }

        internal static void SecondarySync() {
            if (ZNet.instance != null && ZNet.instance.isActiveAndEnabled) {
                ZNet.instance.StartCoroutine(RunDelayedSync());
            }
        }

        internal static IEnumerator RunDelayedSync() {
            yield return new WaitForSeconds(ValConfig.ValidationRecheckDelay.Value);
            if (ValConfig.EnableDelayedValidationRun.Value == false) { yield break; }
            StatusEffectUpdater.Reapply();
            RecipeUpdater.SecondaryRecipeSync();
            PieceUpdater.PieceUpdateRunner();
            ConversionUpdater.ConversionUpdateRunner();
            ItemUpdater.ApplyItemModifications();
            ItemUpdater.ResolveDeferredReferences();
        }

    }
}
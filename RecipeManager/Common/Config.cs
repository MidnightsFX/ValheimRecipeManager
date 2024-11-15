using BepInEx;
using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections;
using System.IO;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using Logger = Jotunn.Logger;
using static RecipeManager.Common.DataObjects;
using System.Collections.Generic;

namespace RecipeManager.Common
{
    class Config
    {
        public static ConfigFile cfg;
        public static ConfigEntry<bool> EnableDebugMode;

        public static String recipeConfigFilePath = Path.Combine(Paths.ConfigPath, "RecipeManager", "Recipes.yaml");
        public static List<string> RecipeConfigFilePaths = new List<string>();
        public static String pieceConfigFilePath = Path.Combine(Paths.ConfigPath, "RecipeManager", "Pieces.yaml");
        public static List<string> PieceConfigFilePaths = new List<string>();
        public static IDeserializer yamldeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        public static ISerializer yamlserializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).DisableAliases().Build();

        private static CustomRPC RecipeConfigRPC;
        private static CustomRPC PiecesConfigRPC;

        public Config(ConfigFile cfgref)
        {
            // Init with the default plugin config file
            cfg = cfgref;
            cfg.SaveOnConfigSet = true;
            CreateConfigValues(cfgref);
            var mainfilepath = Paths.ConfigPath;

            FileSystemWatcher maincfgFSWatcher = new FileSystemWatcher();
            maincfgFSWatcher.Path = mainfilepath;
            maincfgFSWatcher.NotifyFilter = NotifyFilters.LastWrite;
            maincfgFSWatcher.Filter = $"{RecipeManager.PluginGUID}.cfg";
            maincfgFSWatcher.Changed += new FileSystemEventHandler(UpdateMainConfigFile);
            maincfgFSWatcher.Created += new FileSystemEventHandler(UpdateMainConfigFile);
            maincfgFSWatcher.Renamed += new RenamedEventHandler(UpdateMainConfigFile);
            maincfgFSWatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            maincfgFSWatcher.EnableRaisingEvents = true;

            Logger.LogInfo("Config filewatcher initialized.");
            SetupSecondaryConfigFile();
            SetupConfigRPCs();
        }

        private void CreateConfigValues(ConfigFile Config)
        {
            // Debugmode
            EnableDebugMode = Config.Bind("Client config", "EnableDebugMode", false,
                new ConfigDescription("Enables Debug logging for Recipe Manager. This is client side and is not syncd with the server.",
                null,
                new ConfigurationManagerAttributes { IsAdminOnly = false, IsAdvanced = true }));
        }

        private static void UpdateMainConfigFile(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(Paths.ConfigPath)) { return; }
            try
            {
                cfg.SaveOnConfigSet = false;
                cfg.Reload();
                cfg.SaveOnConfigSet = true;
            }
            catch
            {
                Logger.LogError($"There was an issue reloading {RecipeManager.PluginGUID}.cfg.");
            }
        }

        private static void SetupSecondaryConfigFile()
        {
            string externalConfigFolder = GetSecondaryConfigDirectoryPath();
            bool hasRecipeConfig = false;
            bool hasPieceConfig = false;

            string[] presentFiles = Directory.GetFiles(externalConfigFolder);

            foreach (string configFile in presentFiles)
            {
                if (configFile.Contains("Recipes.yaml"))
                {
                    if (EnableDebugMode.Value) { Logger.LogInfo($"Found recipe configuration yaml: {configFile}"); }
                    RecipeConfigFilePaths.Add(configFile);
                    hasRecipeConfig = true;
                }
                if (configFile.Contains("Pieces.yaml"))
                {
                    if (EnableDebugMode.Value) { Logger.LogInfo($"Found pieces configuration yaml: {configFile}"); }
                    PieceConfigFilePaths.Add(configFile);
                    hasPieceConfig = true;
                }
            }

            // write out the recipe config defaults if they do not exist
            if (hasRecipeConfig == false)
            {
                Logger.LogInfo("Recipe file missing, recreating.");
                using (StreamWriter writetext = new StreamWriter(recipeConfigFilePath))
                {
                    String header = @"#################################################
# Recipe Manipulation Config
#################################################
# recipeModifications:                     # <- This is the top level key, all modifications live under this, it is required.
#   DisableWoodArrow:                      # <- This is the modification name, its primarily for you to understand what this modification does SHOULD BE UNIQUE
#     action: Disable                      # <- This is the action it should be one of [Disable, Delete, Modify, Add, Enable]
#     prefab: ArrowWood                    # <- This is the prefab that the modification will target
#   AddNewWoodArrowRecipe:
#     action: Add
#     prefab: ArrowWood
#     recipeName: Recipe_ArrowWood         # <- optional, specifying the recipe name allows multiple mutating multiple recipes targeting the same prefab
#     craftedAt: Workbench                 # <- The crafting station that should craft this recipe, leave it empty or invalid for handcrafting
#     minStationLevel: 2                   # <- This is the required crafting station level for discovery AND crafting
#     recipe:                              # <- When performing [Modify] or [Add] you should define a recipe
#       anyOneResource: false              # <- This makes the recipe only require one ingrediant, first from the top will be used.
#       ingredients:                       # <- Ingrediants in the recipe, is an array
#         - prefab: Wood                   # <- Prefab that this ingrediant requires
#           craftCost: 2                   # <- The amount of this ingrediant it takes to craft the recipe  
#           upgradeCost: 0                 # <- The amount of this ingrediant it takes to upgrade the item 
#           refund: false                  # <- Whether or not this recipe refunds  
#         - prefab: Feathers
#           craftCost: 2
#           upgradeCost: 0
#           refund: true
#   DeleteTrollHideArmorRecipe:
#     action: Delete
#     prefab: CapeTrollHide
#   ModifyTrollHideChestRecipe:
#     action: Modify
#     prefab: ArmorTrollLeatherChest
#     craftedAt: Workbench
#     minStationLevel: 1
#     recipe:
#       anyOneResource: false
#       ingredients:
#         - prefab: TrollHide
#           craftCost: 4
#           upgradeCost: 2
#           refund: false
";

                    writetext.WriteLine(header);
                    writetext.WriteLine(YamlRecipeConfigDefinition());
                }
            }

            // write out the piece config defaults if they do not exist
            if (hasPieceConfig == false)
            {
                Logger.LogInfo("Pieces file missing, recreating.");
                using (StreamWriter writetext = new StreamWriter(pieceConfigFilePath))
                {
                    // Write the header here too
                    writetext.WriteLine(YamlPieceConfigDefinition());
                }
            }

            List<RecipeModificationCollection> allRecipeData = new List<RecipeModificationCollection>();

            foreach (var secondaryRecipeFile in RecipeConfigFilePaths)
            {
                // read out the file
                string recipeConfigData = File.ReadAllText(secondaryRecipeFile);
                try
                {
                    var recipeFileData = yamldeserializer.Deserialize<RecipeModificationCollection>(recipeConfigData);
                    allRecipeData.Add(recipeFileData);
                    //RecipeUpdater.UpdateRecipeModifications(recipeFileData);
                }
                catch (Exception e) { Logger.LogError($"There was an error reading recipe data from {secondaryRecipeFile}, it will not be used. Error: {e}"); }

                // File watcher for the recipe
                FileSystemWatcher recipeFW = new FileSystemWatcher();
                recipeFW.Path = externalConfigFolder;
                recipeFW.NotifyFilter = NotifyFilters.LastWrite;
                recipeFW.Filter = $"{DetermineFileName(secondaryRecipeFile)}";
                recipeFW.Changed += new FileSystemEventHandler(UpdateRecipeConfigFilesOnChange);
                recipeFW.Created += new FileSystemEventHandler(UpdateRecipeConfigFilesOnChange);
                recipeFW.Renamed += new RenamedEventHandler(UpdateRecipeConfigFilesOnChange);
                recipeFW.SynchronizingObject = ThreadingHelper.SynchronizingObject;
                recipeFW.EnableRaisingEvents = true;
            }
            RecipeUpdater.UpdateRecipeModificationsFromList(allRecipeData);

            List<PieceModificationCollection> allPieceData = new List<PieceModificationCollection>();

            foreach (var secondaryPieceFile in PieceConfigFilePaths)
            {
                // read out the file
                string recipeConfigData = File.ReadAllText(secondaryPieceFile);
                try
                {
                    var piecefiledata = yamldeserializer.Deserialize<PieceModificationCollection>(recipeConfigData);
                    allPieceData.Add(piecefiledata);
                    //RecipeUpdater.UpdateRecipeModifications(recipeFileData);
                }
                catch (Exception e) { Logger.LogError($"There was an error reading piece data from {secondaryPieceFile}, it will not be used. Error: {e}"); }
                // File watcher for the recipe
                FileSystemWatcher recipeFW = new FileSystemWatcher();
                recipeFW.Path = externalConfigFolder;
                recipeFW.NotifyFilter = NotifyFilters.LastWrite;
                recipeFW.Filter = $"{DetermineFileName(secondaryPieceFile)}";
                recipeFW.Changed += new FileSystemEventHandler(UpdatePieceConfigFilesOnChange);
                recipeFW.Created += new FileSystemEventHandler(UpdatePieceConfigFilesOnChange);
                recipeFW.Renamed += new RenamedEventHandler(UpdatePieceConfigFilesOnChange);
                recipeFW.SynchronizingObject = ThreadingHelper.SynchronizingObject;
                recipeFW.EnableRaisingEvents = true;
            }
            PieceUpdater.UpdateRecipeModificationsFromList(allPieceData);
        }

        internal static void ReloadPieceFiles()
        {
            List<PieceModificationCollection> allPieceData = new List<PieceModificationCollection>();

            foreach (var secondaryPieceFile in PieceConfigFilePaths)
            {
                // read out the file
                string recipeConfigData = File.ReadAllText(secondaryPieceFile);
                try
                {
                    var piecefiledata = yamldeserializer.Deserialize<PieceModificationCollection>(recipeConfigData);
                    allPieceData.Add(piecefiledata);
                }
                catch (Exception e) { Logger.LogError($"There was an error reading piece data from {secondaryPieceFile}, it will not be used. Error: {e}"); }
            }
            PieceUpdater.UpdateRecipeModificationsFromList(allPieceData);
        }

        internal static void ReloadRecipeFiles()
        {
            List<RecipeModificationCollection> allRecipeData = new List<RecipeModificationCollection>();

            foreach (var secondaryRecipeFile in RecipeConfigFilePaths)
            {
                // read out the file
                string recipeConfigData = File.ReadAllText(secondaryRecipeFile);
                try
                {
                    var recipeFileData = yamldeserializer.Deserialize<RecipeModificationCollection>(recipeConfigData);
                    allRecipeData.Add(recipeFileData);
                    //RecipeUpdater.UpdateRecipeModifications(recipeFileData);
                }
                catch (Exception e) { Logger.LogError($"There was an error reading recipe data from {secondaryRecipeFile}, it will not be used. Error: {e}"); }
            }
            RecipeUpdater.UpdateRecipeModificationsFromList(allRecipeData);
        }

        private static string DetermineFileName(string fullfilepath)
        {
            string filename = "";
            string[] split_filepath = fullfilepath.Split('\\');
            // zero based and the last item
            filename = split_filepath[split_filepath.Length - 2];
            return filename;
        }

        private static void UpdateRecipeConfigFilesOnChange(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(recipeConfigFilePath)) { return; }
            if (EnableDebugMode.Value) { Logger.LogInfo($"{e} Recipe filewatcher called, updating recipe Modification values."); }
            RecipeUpdater.RecipeRevert();
            RecipeUpdater.UpdateRecipeModifications(ReadAllRecipeConfigs());
            RecipeUpdater.BuildRecipesForTracking();
            RecipeUpdater.SecondaryRecipeSync();
            if (EnableDebugMode.Value) { Logger.LogInfo("Updated RecipeModifications in-memory values."); }
            if (GUIManager.IsHeadless()) {
                try {
                    RecipeConfigRPC.SendPackage(ZNet.instance.m_peers, SendRecipeConfigs());
                    if (EnableDebugMode.Value) { Logger.LogInfo("Sent recipe configs to clients."); }
                } catch (Exception ex) {
                    Logger.LogError($"Error while server syncing recipeModification configs: {ex}");
                }
            } else {
                if (EnableDebugMode.Value) {
                    Logger.LogDebug("Instance is not a server, and will not send znet recipeModification updates.");
                }
            }
        }

        private static void UpdatePieceConfigFilesOnChange(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(pieceConfigFilePath)) { return; }
            if (EnableDebugMode.Value) { Logger.LogInfo($"{e} Piece filewatcher called, updating piece Modification values."); }
            PieceUpdater.RevertPieceModifications();
            PieceUpdater.UpdateRecipeModifications(ReadAllPieceConfigs());
            PieceUpdater.BuildPieceTracker();
            PieceUpdater.PieceUpdateRunner();
            if (EnableDebugMode.Value) { Logger.LogInfo("Updated RecipeModifications in-memory values."); }
            if (GUIManager.IsHeadless()) {
                try {
                    RecipeConfigRPC.SendPackage(ZNet.instance.m_peers, SendPieceConfigs());
                    if (EnableDebugMode.Value) { Logger.LogInfo("Sent levels configs to clients."); }
                } catch (Exception ex) {
                    Logger.LogError($"Error while server syncing recipeModification configs: {ex}");
                }
            } else {
                if (EnableDebugMode.Value) {
                    Logger.LogDebug("Instance is not a server, and will not send znet recipeModification updates.");
                }
            }

        }


        public static string GetSecondaryConfigDirectoryPath()
        {
            var patchesFolderPath = Path.Combine(Paths.ConfigPath, "RecipeManager");
            var dirInfo = Directory.CreateDirectory(patchesFolderPath);

            return dirInfo.FullName;
        }

        public void SetupConfigRPCs()
        {
            RecipeConfigRPC = NetworkManager.Instance.AddRPC("recipeManager_recipes_rpc", OnServerRecieveConfigs, OnClientReceiveRecipeConfigs);
            SynchronizationManager.Instance.AddInitialSynchronization(RecipeConfigRPC, SendRecipeConfigs);
            PiecesConfigRPC = NetworkManager.Instance.AddRPC("recipeManager_pieces_rpc", OnServerRecieveConfigs, OnClientReceivePieceConfigs);
            SynchronizationManager.Instance.AddInitialSynchronization(PiecesConfigRPC, SendPieceConfigs);
        }

        public static IEnumerator OnServerRecieveConfigs(long sender, ZPackage package)
        {
            if (EnableDebugMode.Value) { Logger.LogInfo("Server recieved config from client, rejecting due to being the server."); }
            yield return null;
        }

        private static ZPackage SendRecipeConfigs()
        {
            ZPackage package = new ZPackage();
            package.Write(ReadAllRecipeConfigs().ToString());
            return package;
        }

        private static ZPackage SendPieceConfigs()
        {
            ZPackage package = new ZPackage();
            package.Write(ReadAllPieceConfigs().ToString());
            return package;
        }

        private static RecipeModificationCollection ReadAllRecipeConfigs()
        {
            List<RecipeModificationCollection> allRecipeData = new List<RecipeModificationCollection>();

            foreach (var secondaryRecipeFile in RecipeConfigFilePaths)
            {
                if (EnableDebugMode.Value) { Logger.LogDebug($"Loading values from {secondaryRecipeFile}."); }
                // read out the file
                string recipeConfigData = File.ReadAllText(secondaryRecipeFile);
                try
                {
                    var recipeFileData = yamldeserializer.Deserialize<RecipeModificationCollection>(recipeConfigData);
                    allRecipeData.Add(recipeFileData);
                    //RecipeUpdater.UpdateRecipeModifications(recipeFileData);
                }
                catch (Exception ex) { Logger.LogError($"There was an error reading recipe data from {secondaryRecipeFile}, it will not be used. Error: {ex}"); }
            }
            RecipeModificationCollection allRecipeModifications = new RecipeModificationCollection();

            foreach (RecipeModificationCollection rcol in allRecipeData)
            {
                foreach (KeyValuePair<String, RecipeModification> entry in rcol.RecipeModifications)
                {
                    allRecipeModifications.RecipeModifications.Add(entry.Key, entry.Value);
                }
            }
            return allRecipeModifications;
        }

        private static PieceModificationCollection ReadAllPieceConfigs()
        {
            List<PieceModificationCollection> allPieceData = new List<PieceModificationCollection>();

            foreach (var pieceFile in PieceConfigFilePaths)
            {
                if (EnableDebugMode.Value) { Logger.LogDebug($"Loading values from {pieceFile}."); }
                // read out the file
                string pieceConfigData = File.ReadAllText(pieceFile);
                try
                {
                    var recipeFileData = yamldeserializer.Deserialize<PieceModificationCollection>(pieceConfigData);
                    allPieceData.Add(recipeFileData);
                    //RecipeUpdater.UpdateRecipeModifications(recipeFileData);
                }
                catch (Exception ex) { Logger.LogError($"There was an error reading piece data from {pieceFile}, it will not be used. Error: {ex}"); }
            }
            PieceModificationCollection allRecipeModifications = new PieceModificationCollection();

            foreach (PieceModificationCollection rcol in allPieceData)
            {
                foreach (KeyValuePair<String, PieceModification> entry in rcol.PieceModifications)
                {
                    allRecipeModifications.PieceModifications.Add(entry.Key, entry.Value);
                }
            }
            return allRecipeModifications;
        }

        private static IEnumerator OnClientReceiveRecipeConfigs(long sender, ZPackage package)
        {
            var yaml = package.ReadString();
            RecipeUpdater.RecipeRevert();
            try {
                var recipeFileData = Config.yamldeserializer.Deserialize<RecipeModificationCollection>(yaml);
                RecipeUpdater.UpdateRecipeModifications(recipeFileData);
            } catch {
                Logger.LogWarning($"Recieved invalid configuration, all recipes reverted.");
            }
            RecipeUpdater.BuildRecipesForTracking();
            RecipeUpdater.SecondaryRecipeSync();
            yield return null;
        }

        private static IEnumerator OnClientReceivePieceConfigs(long sender, ZPackage package)
        {
            var yaml = package.ReadString();
            PieceUpdater.RevertPieceModifications();
            try {
                var recipeFileData = Config.yamldeserializer.Deserialize<PieceModificationCollection>(yaml);
                PieceUpdater.UpdateRecipeModifications(recipeFileData);
            } catch {
                Logger.LogWarning($"Recieved invalid configuration, all pieces reverted.");
            }
            PieceUpdater.BuildPieceTracker();
            PieceUpdater.PieceUpdateRunner();
            yield return null;
        }

        public static string YamlRecipeConfigDefinition()
        {
            var recipeCollection = new RecipeModificationCollection();
            recipeCollection.RecipeModifications = RecipeUpdater.RecipesToModify;
            var yaml = yamlserializer.Serialize(recipeCollection);
            return yaml;
        }

        public static string YamlPieceConfigDefinition()
        {
            var pieceCollection = new PieceModificationCollection();
            pieceCollection.PieceModifications = PieceUpdater.PiecesToModify;
            var yaml = yamlserializer.Serialize(pieceCollection);
            return yaml;
        }

        /// <summary>
        ///  Helper to bind configs for bool types
        /// </summary>
        /// <param name="config_file"></param>
        /// <param name="catagory"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        /// <param name="advanced"></param>
        /// <returns></returns>
        public ConfigEntry<bool> BindServerConfig(string catagory, string key, bool value, string description, bool advanced = false)
        {
            return cfg.Bind(catagory, key, value,
                new ConfigDescription(description,
                null,
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Helper to bind configs for strings
        /// </summary>
        /// <param name="config_file"></param>
        /// <param name="catagory"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        /// <param name="advanced"></param>
        /// <returns></returns>
        public ConfigEntry<string> BindServerConfig(string catagory, string key, string value, string description, bool advanced = false)
        {
            return cfg.Bind(catagory, key, value,
                new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Helper to bind configs for Shorts
        /// </summary>
        /// <param name="config_file"></param>
        /// <param name="catagory"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        /// <param name="advanced"></param>
        /// <returns></returns>
        public ConfigEntry<short> BindServerConfig(string catagory, string key, short value, string description, bool advanced = false, short valmin = 0, short valmax = 150)
        {
            return cfg.Bind(catagory, key, value,
                new ConfigDescription(description,
                new AcceptableValueRange<short>(valmin, valmax),
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Helper to bind configs for float types
        /// </summary>
        /// <param name="config_file"></param>
        /// <param name="catagory"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        /// <param name="advanced"></param>
        /// <param name="valmin"></param>
        /// <param name="valmax"></param>
        /// <returns></returns>
        public ConfigEntry<float> BindServerConfig(string catagory, string key, float value, string description, bool advanced = false, float valmin = 0, float valmax = 150)
        {
            return cfg.Bind(catagory, key, value,
                new ConfigDescription(description,
                new AcceptableValueRange<float>(valmin, valmax),
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }
    }
}

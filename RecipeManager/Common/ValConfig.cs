using BepInEx;
using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections;
using System.IO;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using static RecipeManager.Common.DataObjects;
using System.Collections.Generic;

namespace RecipeManager.Common
{
    class ValConfig
    {
        public static ConfigFile cfg;
        public static ConfigEntry<bool> EnableDebugMode;
        public static ConfigEntry<bool> EnableDelayedValidationRun;
        public static ConfigEntry<float> ValidationRecheckDelay;

        public static String recipeConfigFilePath = Path.Combine(Paths.ConfigPath, "RecipeManager", "Recipes.yaml");
        public static List<string> RecipeConfigFilePaths = new List<string>();
        public static String pieceConfigFilePath = Path.Combine(Paths.ConfigPath, "RecipeManager", "Pieces.yaml");
        public static List<string> PieceConfigFilePaths = new List<string>();
        public static String ConversionConfigFilePath = Path.Combine(Paths.ConfigPath, "RecipeManager", "Conversions.yaml");
        public static List<string> ConversionConfigFilePaths = new List<string>();
        public static String ItemConfigFilePath = Path.Combine(Paths.ConfigPath, "RecipeManager", "Items.yaml");
        public static List<string> ItemConfigFilePaths = new List<string>();
        public static IDeserializer yamldeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        public static ISerializer yamlserializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults).Build();

        private static CustomRPC RecipeConfigRPC;
        private static CustomRPC PiecesConfigRPC;
        private static CustomRPC ConversionConfigRPC;
        private static CustomRPC ItemsConfigRPC;

        public ValConfig(ConfigFile cfgref)
        {
            // Init with the default plugin config file
            cfg = cfgref;
            cfg.SaveOnConfigSet = true;
            CreateConfigValues(cfgref);
            SetupSecondaryConfigFile();
            SetupConfigRPCs();
        }

        private void CreateConfigValues(ConfigFile Config) {
            // Debugmode
            EnableDebugMode = Config.Bind("Client config", "EnableDebugMode", false,
                new ConfigDescription("Enables Debug logging for Recipe Manager. This is client side and is not syncd with the server.",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = true }));
            EnableDebugMode.SettingChanged += Logger.enableDebugLogging;
            Logger.CheckEnableDebugLogging();

            EnableDelayedValidationRun = BindServerConfig("Sync", "EnableDelayedValidationRun", false, "When enabled, all modifications are verified and re-ran if needed after a set time. This occurs once for each player and is limited to that player. This can be used to fix issues with extremely late running mod additions.", true);
            ValidationRecheckDelay = BindServerConfig("Sync", "ValidationRecheckDelay", 60f, "The duration, in seconds that the secondary validation run will wait to run", true);
        }

        private static void SetupSecondaryConfigFile()
        {
            string externalConfigFolder = GetSecondaryConfigDirectoryPath();
            bool hasRecipeConfig = false;
            bool hasPieceConfig = false;
            bool hasConversionConfig = false;
            bool hasItemConfig = false;

            string[] presentFiles = Directory.GetFiles(externalConfigFolder);

            foreach (string configFile in presentFiles)
            {
                if (configFile.Contains("Recipes.yaml") && !configFile.Contains("ObjectDBRecipes.yaml"))
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
                if (configFile.Contains("Conversions.yaml"))
                {
                    if (EnableDebugMode.Value) { Logger.LogInfo($"Found Conversion configuration yaml: {configFile}"); }
                    ConversionConfigFilePaths.Add(configFile);
                    hasConversionConfig = true;
                }
                if (configFile.Contains("Items.yaml") && !configFile.Contains("ObjectDBItems.yaml"))
                {
                    if (EnableDebugMode.Value) { Logger.LogInfo($"Found Item configuration yaml: {configFile}"); }
                    ItemConfigFilePaths.Add(configFile);
                    hasItemConfig = true;
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
#     recipeName: Recipe_ArrowWood         # <- optional, specifying the recipe name allows matching and mutating multiple recipes targeting the same prefab
#     craftedAt: Workbench                 # <- The crafting station that should craft this recipe, leave it empty or invalid for handcrafting
#     minStationLevel: 2                   # <- This is the required crafting station level for discovery AND crafting
#     recipe:                              # <- When performing [Modify] or [Add] you should define a recipe
#       anyOneResource: false              # <- This makes the recipe only require one ingrediant, first from the top will be used.
#       noRecipeCost: false                # <- This makes the recipe not require any resources to craft, if this is used the ingredients list will be ignored
#       ingredients:                       # <- Ingrediants in the recipe, is an array
#         - prefab: Wood                   # <- Prefab that this ingrediant requires
#           craftCost: 2                   # <- The amount of this ingrediant it takes to craft the recipe  
#           upgradeCost: 0                 # <- The amount of this ingrediant it takes to upgrade the item 
#         - prefab: Feathers
#           craftCost: 2
#           upgradeCost: 0
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
                    String header = @"############################################################################
# Piece Manipulation Config
############################################################################
# pieceModifications:                                      # <- This is the top level key, it is required
#  modify_the_bed:                                         # <- REQUIRED The name of this modification, it should be unique but is for your information
#    action: Enable                                        # <- REQUIRED This is the action applied can be [Enable, Disable, Modify]
#    prefab: bed                                           # <- REQUIRED This is the prefab that the action will be applied to
#    requirements:                                         # <- If the piece is being modified, you can set requirements which will be the cost to build this
#    - prefab: Wood                                        # <- Each requirement entry requires a prefab name, you can find item prefabs on the valheim wiki
#      amount: 8                                           # <- The amount of the prefab required
#    requiredToPlaceCraftingStation: piece_workbench       # <- The crafting station used to place this item, if it is set to 'none' it will remove the station requirement
#    allowedInDungeon: false                               # <- If you can build this inside dungeons
#    canBeDeconstructed: true                              # <- If this can be broken with middle mouse
#    pieceCategory: Furniture                              # <- The category tab this will be placed in. This can be any of the categories across any available tools
#    comfortAmount: 1                                      # <- If above 0 this item will provide comfort
#    comfortGroup: Bed                                     # <- The comfort group this is a part of
#    isUpgradeForStation: false                            # <- If this piece is considered an upgrade for a crafting station
#    craftingStationConnectionRadius: 0                    # <- The radius that this will connect to a crafting station
#    mustBeAvobeConnectedStation: false                    # <- If this upgrade must be placed ABOVE its crafting station- this is normally only used for hanging upgrades for the cooking station
#    spaceRequired: 0                                      # <- How much space is required for this item
#    pieceName: $piece_bed                                 # <- The localizable name for this, setting ""My Bed"" will make this piece called ""My Bed""
#    pieceDescription: ''                                  # <- The description for this piece, many pieces do not have this
#    enablePiece: true                                     # <- Whether or not this piece is enabled
#    onlyInSelectBiome: None                               # <- Whether or not this can be placed in only one biome, in vanilla this is used for plants
#    cultivatedGroundOnly: false                           # <- Whether this piece needs to be placed on cultivated ground
#    groundPlacement: false                                # <- Whether this piece needs to be placed on ground (stone is also considered ground)";
                    // Write the header here too
                    writetext.WriteLine(header);
                    writetext.WriteLine(YamlPieceConfigDefinition());
                }
            }

            // write out the piece config defaults if they do not exist
            if (hasConversionConfig == false)
            {
                Logger.LogInfo("Conversion file missing, recreating.");
                using (StreamWriter writetext = new StreamWriter(ConversionConfigFilePath))
                {
                    String header = @"############################################################################
# Conversion Manipulation Config
############################################################################
";
                    // Write the header here too
                    writetext.WriteLine(header);
                    writetext.WriteLine(YamlConversionConfigDefinition());
                }
            }

            // write out the item config defaults if they do not exist
            if (hasItemConfig == false)
            {
                Logger.LogInfo("Items file missing, recreating.");
                using (StreamWriter writetext = new StreamWriter(ItemConfigFilePath))
                {
                    String header = @"############################################################################
# Item Config
############################################################################
# Two different things live in this file.
#
# 'action: Add' creates a brand new item by cloning an existing prefab. The clone keeps the model and
# every stat of the item it was copied from, and anything you set here is applied on top.
#
# 'action: Modify' changes an item that already exists, vanilla ones included. Only the fields you set
# are changed and everything else is left alone, so taking a field back out of the yaml puts that
# item's original value back.
#
# NOTE: adding or removing an item needs a game restart (or a trip back to the main menu). Everything
# else - stats, names, icons, textures, and every modification - applies live with RM_Items_Reload.
#
# Run RM_Items_PrintAll to write every item in the game, with the stats it currently has, out to
# ObjectDBItems.yaml. That file is never loaded, it is there for you to copy entries out of.
# RM_Items_PrintAll <text> only dumps the items whose prefab name contains <text>.
#
# itemAdditions:                             # <- This is the top level key, all entries live under this, it is required.
#   HoneyBread:                              # <- The name of this entry, it is for your information but SHOULD BE UNIQUE
#     action: Add                            # <- REQUIRED This is the action applied, can be [Add, Modify, Remove]
#     prefab: HoneyBread                     # <- REQUIRED For Add this is the NEW item and must be globally unique, for Modify it is the item to change
#     clonedFrom: Bread                      # <- REQUIRED for Add, the existing prefab to copy. Leave it out when modifying
#     name: Honey Bread                      # <- The display name. Start it with '$' to use your own localization token
#     description: Sweet, sticky and filling. # <- The tooltip description
#     weight: 0.5                            # <- Leave unset to keep the current weight
#     maxStackSize: 20                       # <- Leave unset to keep the current stack size
#     value: 12                              # <- The trader value of this item
#     teleportable: true                     # <- Whether this item can go through a portal
#     food:                                  # <- Only set this for food and drinks, leave it out otherwise
#       health: 50                           # <- The max health this food grants
#       stamina: 60                          # <- The max stamina this food grants
#       eitr: 0                              # <- The max eitr this food grants
#       duration: 1800                       # <- How long the food lasts, in seconds
#       regen: 2                             # <- Health regenerated per tick while this food is active
#       eatAnimTime: 1                       # <- How long the eating animation takes
#       isDrink: false                       # <- Whether this is drunk rather than eaten
#       consumeStatusEffect: ''              # <- The name of an EXISTING status effect to apply when consumed
#     recipe:                                # <- Optional, leave it out for an item that is not crafted
#       craftedAt: piece_cauldron            # <- The crafting station, leave it empty for handcrafting
#       repairAt: ''                         # <- Defaults to craftedAt
#       minStationLevel: 1                   # <- The required crafting station level
#       craftAmount: 1                       # <- How many of the item one craft produces
#       anyOneResource: false                # <- Only require one of the ingredients below
#       ingredients:
#       - prefab: Bread                      # <- Prefab that this ingredient requires
#         craftCost: 1                       # <- The amount of this ingredient it takes to craft
#         upgradeCost: 0                     # <- The amount of this ingredient it takes to upgrade
#       - prefab: Honey
#         craftCost: 2
#         upgradeCost: 0
#
# ---------------------------------------------------------------------------
# Weapon, armor and set stats. These work the same on an added item and on one you are modifying.
# ---------------------------------------------------------------------------
#
#   BurningIronSword:
#     action: Modify
#     prefab: SwordIron                      # <- The existing item being changed
#     weapon:                                # <- Weapon and tool stats
#       damage:                              # <- Base damage, only the damage types you list are touched
#         slash: 60
#         fire: 20
#       damagePerLevel:                      # <- Damage added by each upgrade level
#         slash: 5
#       attackForce: 60                      # <- Knockback
#       backstabBonus: 3                     # <- Sneak attack multiplier
#       toolTier: 2                          # <- What a tool can harvest, 0 is wood and stone, 2 is black metal
#     armor:                                 # <- Armor and shield stats
#       armor: 24
#       armorPerLevel: 2
#       blockPower: 62                       # <- Shields, how much damage a block absorbs
#       blockPowerPerLevel: 4
#       deflectionForce: 30
#       deflectionForcePerLevel: 5
#       movementModifier: -0.05              # <- Speed change while equipped, -0.05 is 5% slower
#       eitrRegenModifier: 0.1
#     resistances:                           # <- Replaces the whole resistance list, use [] to clear it
#     - type: Fire                           # <- Blunt, Slash, Pierce, Chop, Pickaxe, Fire, Frost, Lightning, Poison or Spirit
#       modifier: VeryResistant              # <- Normal, SlightlyResistant, Resistant, VeryResistant, SlightlyWeak, Weak, VeryWeak, Immune or Ignore
#     statusEffects:
#       equipped: SetEffect_TrollArmor       # <- Applied for as long as the item is equipped
#       onHit: Burning                       # <- Applied to whatever this weapon hits
#       onHitChance: 0.5                     # <- Chance for onHit to land, 0 to 1
#     setBonus:
#       name: SwordSet                       # <- Grouping key, every piece of the set must use the same one
#       size: 2                              # <- How many pieces have to be worn, has to be more than 1
#       statusEffect: SetEffect_TrollArmor   # <- The effect wearing the full set grants
#
# Status effects have to already exist, this mod cannot create new ones. Use the value None to empty a
# status effect slot instead of naming one.
#
# Custom icons and textures are loaded from BepInEx\config\RecipeManager\Textures\<prefab>\
# Use 'RM_Textures_Dump <prefab>' in the console to dump an existing prefab's images, then copy that
# folder, rename it to your new item's prefab name, and repaint the files inside it.";
                    writetext.WriteLine(header);
                    writetext.WriteLine(YamlItemConfigDefinition());
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
            PieceUpdater.UpdatePieceModificationsFromList(allPieceData);

            List<ConversionModificationCollection> allConversionData = new List<ConversionModificationCollection>();

            foreach (var secondaryConversionPath in ConversionConfigFilePaths)
            {
                // read out the file
                string convData = File.ReadAllText(secondaryConversionPath);
                try
                {
                    var convDataSerialized = yamldeserializer.Deserialize<ConversionModificationCollection>(convData);
                    allConversionData.Add(convDataSerialized);
                    //RecipeUpdater.UpdateRecipeModifications(recipeFileData);
                }
                catch (Exception e) { Logger.LogError($"There was an error reading conversion data from {secondaryConversionPath}, it will not be used. Error: {e}"); }
                // File watcher for the recipe
                FileSystemWatcher recipeFW = new FileSystemWatcher();
                recipeFW.Path = externalConfigFolder;
                recipeFW.NotifyFilter = NotifyFilters.LastWrite;
                recipeFW.Filter = $"{DetermineFileName(secondaryConversionPath)}";
                recipeFW.Changed += new FileSystemEventHandler(UpdateConversionConfigFilesOnChange);
                recipeFW.Created += new FileSystemEventHandler(UpdateConversionConfigFilesOnChange);
                recipeFW.Renamed += new RenamedEventHandler(UpdateConversionConfigFilesOnChange);
                recipeFW.SynchronizingObject = ThreadingHelper.SynchronizingObject;
                recipeFW.EnableRaisingEvents = true;
            }
            ConversionUpdater.UpdateConversionModificationsFromList(allConversionData);

            List<ItemAdditionCollection> allItemData = new List<ItemAdditionCollection>();

            foreach (var secondaryItemFile in ItemConfigFilePaths)
            {
                // read out the file
                string itemConfigData = File.ReadAllText(secondaryItemFile);
                try
                {
                    var itemFileData = yamldeserializer.Deserialize<ItemAdditionCollection>(itemConfigData);
                    if (itemFileData != null) { allItemData.Add(itemFileData); }
                }
                catch (Exception e) { Logger.LogError($"There was an error reading item data from {secondaryItemFile}, it will not be used. Error: {e}"); }
                // File watcher for the item additions
                FileSystemWatcher itemFW = new FileSystemWatcher();
                itemFW.Path = externalConfigFolder;
                itemFW.NotifyFilter = NotifyFilters.LastWrite;
                itemFW.Filter = $"{DetermineFileName(secondaryItemFile)}";
                itemFW.Changed += new FileSystemEventHandler(UpdateItemConfigFilesOnChange);
                itemFW.Created += new FileSystemEventHandler(UpdateItemConfigFilesOnChange);
                itemFW.Renamed += new RenamedEventHandler(UpdateItemConfigFilesOnChange);
                itemFW.SynchronizingObject = ThreadingHelper.SynchronizingObject;
                itemFW.EnableRaisingEvents = true;
            }
            ItemUpdater.UpdateItemAdditionsFromList(allItemData);
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
            PieceUpdater.UpdatePieceModificationsFromList(allPieceData);
        }

        internal static void ReloadConversionFiles()
        {
            List<ConversionModificationCollection> allPieceData = new List<ConversionModificationCollection>();

            foreach (var secondaryPieceFile in ConversionConfigFilePaths)
            {
                // read out the file
                string recipeConfigData = File.ReadAllText(secondaryPieceFile);
                try
                {
                    var piecefiledata = yamldeserializer.Deserialize<ConversionModificationCollection>(recipeConfigData);
                    allPieceData.Add(piecefiledata);
                }
                catch (Exception e) { Logger.LogError($"There was an error reading piece data from {secondaryPieceFile}, it will not be used. Error: {e}"); }
            }
            ConversionUpdater.UpdateConversionModificationsFromList(allPieceData);
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

        internal static void ReloadItemFiles()
        {
            List<ItemAdditionCollection> allItemData = new List<ItemAdditionCollection>();

            foreach (var itemFile in ItemConfigFilePaths)
            {
                // read out the file
                string itemConfigData = File.ReadAllText(itemFile);
                try
                {
                    var itemFileData = yamldeserializer.Deserialize<ItemAdditionCollection>(itemConfigData);
                    if (itemFileData != null) { allItemData.Add(itemFileData); }
                }
                catch (Exception e) { Logger.LogError($"There was an error reading item data from {itemFile}, it will not be used. Error: {e}"); }
            }
            ItemUpdater.UpdateItemAdditionsFromList(allItemData);
        }

        private static string DetermineFileName(string fullfilepath) {
            string filename = Path.GetFileName(fullfilepath);
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
            PieceUpdater.UpdatePieceModifications(ReadAllPieceConfigs());
            PieceUpdater.BuildPieceTracker();
            PieceUpdater.PieceUpdateRunner();
            if (EnableDebugMode.Value) { Logger.LogInfo("Updated RecipeModifications in-memory values."); }
            if (GUIManager.IsHeadless()) {
                try {
                    PiecesConfigRPC.SendPackage(ZNet.instance.m_peers, SendPieceConfigs());
                    if (EnableDebugMode.Value) { Logger.LogInfo("Sent piece configs to clients."); }
                } catch (Exception ex) {
                    Logger.LogError($"Error while server syncing recipeModification configs: {ex}");
                }
            } else {
                if (EnableDebugMode.Value) {
                    Logger.LogDebug("Instance is not a server, and will not send znet recipeModification updates.");
                }
            }
        }

        private static void UpdateConversionConfigFilesOnChange(object sender, FileSystemEventArgs e)
        {
            if (EnableDebugMode.Value) { Logger.LogInfo($"{e} Conversion filewatcher called, updating conversion Modification values."); }
            ConversionUpdater.RevertConversionModifications();
            ConversionUpdater.UpdateConversionModifications(ReadAllConversionConfigs());
            ConversionUpdater.BuildConversionTracker();
            ConversionUpdater.ConversionUpdateRunner();
            if (EnableDebugMode.Value) { Logger.LogInfo("Updated ConversionModifications in-memory values."); }
            if (GUIManager.IsHeadless()) {
                try
                {
                    ConversionConfigRPC.SendPackage(ZNet.instance.m_peers, SendConversionConfigs());
                    if (EnableDebugMode.Value) { Logger.LogInfo("Sent conversion configs to clients."); }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error while server syncing conversionModification configs: {ex}");
                }
            }
            else
            {
                if (EnableDebugMode.Value)
                {
                    Logger.LogDebug("Instance is not a server, and will not send znet recipeModification updates.");
                }
            }
        }

        private static void UpdateItemConfigFilesOnChange(object sender, FileSystemEventArgs e)
        {
            if (EnableDebugMode.Value) { Logger.LogInfo($"{e} Item filewatcher called, updating item addition values."); }
            ReloadItemFiles();
            ItemUpdater.ReapplyExistingItems();
            if (EnableDebugMode.Value) { Logger.LogInfo("Updated ItemAdditions in-memory values."); }
            if (GUIManager.IsHeadless()) {
                try
                {
                    ItemsConfigRPC.SendPackage(ZNet.instance.m_peers, SendItemConfigs());
                    if (EnableDebugMode.Value) { Logger.LogInfo("Sent item configs to clients."); }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error while server syncing itemAddition configs: {ex}");
                }
            }
            else
            {
                if (EnableDebugMode.Value)
                {
                    Logger.LogDebug("Instance is not a server, and will not send znet itemAddition updates.");
                }
            }
        }


        public static string GetSecondaryConfigDirectoryPath() {
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
            ConversionConfigRPC = NetworkManager.Instance.AddRPC("recipeManager_conversion_rpc", OnServerRecieveConfigs, OnClientReceiveConversionConfigs);
            SynchronizationManager.Instance.AddInitialSynchronization(ConversionConfigRPC, SendConversionConfigs);
            ItemsConfigRPC = NetworkManager.Instance.AddRPC("recipeManager_items_rpc", OnServerRecieveConfigs, OnClientReceiveItemConfigs);
            SynchronizationManager.Instance.AddInitialSynchronization(ItemsConfigRPC, SendItemConfigs);
        }

        public static IEnumerator OnServerRecieveConfigs(long sender, ZPackage package)
        {
            if (EnableDebugMode.Value) { Logger.LogInfo("Server recieved config from client, rejecting due to being the server."); }
            yield return null;
        }

        private static ZPackage SendRecipeConfigs()
        {
            ZPackage package = new ZPackage();
            package.Write(yamlserializer.Serialize(ReadAllRecipeConfigs()));
            return package;
        }

        private static ZPackage SendPieceConfigs()
        {
            ZPackage package = new ZPackage();
            package.Write(yamlserializer.Serialize(ReadAllPieceConfigs()));
            return package;
        }

        private static ZPackage SendConversionConfigs()
        {
            ZPackage package = new ZPackage();
            package.Write(yamlserializer.Serialize(ReadAllConversionConfigs()));
            return package;
        }

        private static ZPackage SendItemConfigs()
        {
            ZPackage package = new ZPackage();
            package.Write(yamlserializer.Serialize(ReadAllItemConfigs()));
            return package;
        }

        public static void SendUpdatedItemConfigs() {
            ItemsConfigRPC.SendPackage(ZNet.instance.m_peers, SendItemConfigs());
        }

        public static void SendUpdatedPieceConfigs() {
            PiecesConfigRPC.SendPackage(ZNet.instance.m_peers, SendPieceConfigs());
        }

        public static void SendUpdatedRecipeConfigs() {
            RecipeConfigRPC.SendPackage(ZNet.instance.m_peers, SendRecipeConfigs());
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

        private static ConversionModificationCollection ReadAllConversionConfigs()
        {
            List<ConversionModificationCollection> allPieceData = new List<ConversionModificationCollection>();

            foreach (var convFile in ConversionConfigFilePaths)
            {
                if (EnableDebugMode.Value) { Logger.LogDebug($"Loading values from {convFile}."); }
                // read out the file
                string convConfigData = File.ReadAllText(convFile);
                try
                {
                    var convFileData = yamldeserializer.Deserialize<ConversionModificationCollection>(convConfigData);
                    allPieceData.Add(convFileData);
                    //RecipeUpdater.UpdateRecipeModifications(recipeFileData);
                }
                catch (Exception ex) { Logger.LogError($"There was an error reading conversion data from {convFile}, it will not be used. Error: {ex}"); }
            }
            ConversionModificationCollection allRecipeModifications = new ConversionModificationCollection();

            foreach (ConversionModificationCollection rcol in allPieceData)
            {
                foreach (KeyValuePair<String, ConversionModification> entry in rcol.ConversionModifications)
                {
                    allRecipeModifications.ConversionModifications.Add(entry.Key, entry.Value);
                }
            }
            return allRecipeModifications;
        }

        private static ItemAdditionCollection ReadAllItemConfigs()
        {
            List<ItemAdditionCollection> allItemData = new List<ItemAdditionCollection>();

            foreach (var itemFile in ItemConfigFilePaths)
            {
                if (EnableDebugMode.Value) { Logger.LogDebug($"Loading values from {itemFile}."); }
                // read out the file
                string itemConfigData = File.ReadAllText(itemFile);
                try
                {
                    var itemFileData = yamldeserializer.Deserialize<ItemAdditionCollection>(itemConfigData);
                    if (itemFileData != null) { allItemData.Add(itemFileData); }
                }
                catch (Exception ex) { Logger.LogError($"There was an error reading item data from {itemFile}, it will not be used. Error: {ex}"); }
            }
            ItemAdditionCollection allItemAdditions = new ItemAdditionCollection();

            foreach (ItemAdditionCollection icol in allItemData)
            {
                // A file whose entries are all commented out still parses, but leaves the dictionary null
                if (icol?.ItemAdditions == null) { continue; }
                foreach (KeyValuePair<String, ItemAddition> entry in icol.ItemAdditions)
                {
                    if (allItemAdditions.ItemAdditions.ContainsKey(entry.Key)) { continue; }
                    allItemAdditions.ItemAdditions.Add(entry.Key, entry.Value);
                }
            }
            return allItemAdditions;
        }

        private static IEnumerator OnClientReceiveItemConfigs(long sender, ZPackage package)
        {
            var yaml = package.ReadString();
            try {
                ItemUpdater.UpdateItemAdditionsFromRPC(yaml);
            } catch {
                Logger.LogWarning($"Recieved invalid configuration, item additions ignored.");
            }
            // Items cannot be registered once the object DB exists, so an item the server has but this
            // client does not needs a restart. Everything else - the stats of items that are already
            // here, and every modification of an existing item - is brought in line with the server.
            ItemUpdater.ReapplyExistingItems();
            yield return null;
        }

        private static IEnumerator OnClientReceiveRecipeConfigs(long sender, ZPackage package)
        {
            var yaml = package.ReadString();
            RecipeUpdater.RecipeRevert();
            try {
                var recipeFileData = ValConfig.yamldeserializer.Deserialize<RecipeModificationCollection>(yaml);
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
                PieceUpdater.UpdatePieceModificationsFromRPC(yaml);
                var recipeFileData = ValConfig.yamldeserializer.Deserialize<PieceModificationCollection>(yaml);
                PieceUpdater.UpdatePieceModifications(recipeFileData);
            } catch {
                Logger.LogWarning($"Recieved invalid configuration, pieces changes ignored.");
            }
            PieceUpdater.BuildPieceTracker();
            PieceUpdater.PieceUpdateRunner();
            yield return null;
        }

        private static IEnumerator OnClientReceiveConversionConfigs(long sender, ZPackage package)
        {
            var yaml = package.ReadString();
            ConversionUpdater.RevertConversionModifications();
            try {
                ConversionUpdater.UpdateConversionModificationsFromRPC(yaml);
            } catch {
                Logger.LogWarning($"Recieved invalid configuration, conversions changes ignored.");
            }
            ConversionUpdater.BuildConversionTracker();
            ConversionUpdater.ConversionUpdateRunner();
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

        public static string YamlConversionConfigDefinition()
        {
            var pieceCollection = new ConversionModificationCollection();
            pieceCollection.ConversionModifications = ConversionUpdater.ConversionsToModify;
            var yaml = yamlserializer.Serialize(pieceCollection);
            return yaml;
        }

        public static string YamlItemConfigDefinition()
        {
            var itemCollection = new ItemAdditionCollection();
            itemCollection.ItemAdditions = ItemUpdater.ItemDefinitions;
            var yaml = yamlserializer.Serialize(itemCollection);
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

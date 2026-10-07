using BepInEx;
using BepInEx.Configuration;
using System;
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

        public static IDeserializer yamldeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        /// <summary>For the files people read: the reference dumps, the default files and imports. Leaves out every default value.</summary>
        public static ISerializer yamlserializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults).Build();
        /// <summary>
        /// For what is sent to clients. Only leaves out nulls: a value left out comes back as whatever the field
        /// starts as, which for a non-nullable field with an initializer is not the value that was sent.
        /// </summary>
        public static ISerializer syncSerializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build();

        public static ConfigChannel<RecipeModificationCollection, RecipeModification> Recipes;
        public static ConfigChannel<PieceModificationCollection, PieceModification> Pieces;
        public static ConfigChannel<ConversionModificationCollection, ConversionModification> Conversions;
        public static ConfigChannel<ItemAdditionCollection, ItemAddition> Items;
        public static ConfigChannel<StatusEffectCollection, StatusEffectDefinition> StatusEffects;
        private static readonly List<ConfigChannel> Channels = new List<ConfigChannel>();

        // Held so it is not collected, which would silently stop the reloads
        private static FileSystemWatcher configWatcher;

        public ValConfig(ConfigFile cfgref)
        {
            // Init with the default plugin config file
            cfg = cfgref;
            cfg.SaveOnConfigSet = true;
            CreateConfigValues(cfgref);
            CreateChannels();
            SetupSecondaryConfigFiles();
            foreach (ConfigChannel channel in Channels) { channel.LoadFromDisk(); }
            WatchConfigFolder();
            Recipes.RegisterRpc();
            Pieces.RegisterRpc();
            Conversions.RegisterRpc();
            // Ahead of the items, which can point at the effects it adds
            StatusEffects.RegisterRpc();
            Items.RegisterRpc();
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

        private static void CreateChannels()
        {
            Recipes = new ConfigChannel<RecipeModificationCollection, RecipeModification>(
                "recipe", "Recipes.yaml", new[] { RecipePrintCommand.DumpFileName }, "recipeManager_recipes_rpc",
                collection => collection.RecipeModifications,
                entries => new RecipeModificationCollection { RecipeModifications = entries },
                () => RecipeUpdater.RecipesToModify,
                entries => RecipeUpdater.RecipesToModify = entries,
                RecipeUpdater.Reapply);
            Pieces = new ConfigChannel<PieceModificationCollection, PieceModification>(
                "piece", "Pieces.yaml", null, "recipeManager_pieces_rpc",
                collection => collection.PieceModifications,
                entries => new PieceModificationCollection { PieceModifications = entries },
                () => PieceUpdater.PiecesToModify,
                entries => PieceUpdater.PiecesToModify = entries,
                PieceUpdater.Reapply);
            Conversions = new ConfigChannel<ConversionModificationCollection, ConversionModification>(
                "conversion", "Conversions.yaml", null, "recipeManager_conversion_rpc",
                collection => collection.ConversionModifications,
                entries => new ConversionModificationCollection { ConversionModifications = entries },
                () => ConversionUpdater.ConversionsToModify,
                entries => ConversionUpdater.ConversionsToModify = entries,
                ConversionUpdater.Reapply);
            Items = new ConfigChannel<ItemAdditionCollection, ItemAddition>(
                "item", "Items.yaml", new[] { ItemPrintCommand.DumpFileName }, "recipeManager_items_rpc",
                collection => collection.ItemAdditions,
                entries => new ItemAdditionCollection { ItemAdditions = entries },
                () => ItemUpdater.ItemDefinitions,
                entries => ItemUpdater.ItemDefinitions = entries,
                // Items cannot be registered once the object DB exists, so an item the server has but this
                // client does not needs a restart. Everything else - the stats of items that are already
                // here, and every modification of an existing item - is brought in line.
                ItemUpdater.ReapplyExistingItems);
            StatusEffects = new ConfigChannel<StatusEffectCollection, StatusEffectDefinition>(
                "statuseffect", "StatusEffects.yaml", new[] { StatusEffectPrintCommand.DumpFileName }, "recipeManager_statuseffects_rpc",
                collection => collection.statusEffects,
                entries => new StatusEffectCollection { statusEffects = entries },
                () => StatusEffectUpdater.Definitions,
                entries => StatusEffectUpdater.Definitions = entries,
                ReapplyStatusEffects);
            Channels.Add(Recipes);
            Channels.Add(Pieces);
            Channels.Add(Conversions);
            Channels.Add(StatusEffects);
            Channels.Add(Items);
        }

        /// <summary>Items point straight at the effect objects, so they are brought in line after the effects change.</summary>
        private static void ReapplyStatusEffects()
        {
            StatusEffectUpdater.Reapply();
            ItemUpdater.RefreshStatusEffectReferences();
        }

        /// <summary>
        /// Re-reads every local config file. Hooked to the main menu, so that after playing on a server the
        /// game goes back to this machine's own configuration.
        /// </summary>
        internal static void ReloadLocalConfigs()
        {
            foreach (ConfigChannel channel in Channels) { channel.LoadFromDisk(); }
        }

        /// <summary>
        /// One watcher for the whole folder, so a file added while the game runs is picked up too. Events are
        /// handed to the main thread, and each kind of file waits for its changes to settle before reloading.
        /// Subfolders are not watched, they are not loaded either.
        /// </summary>
        private static void WatchConfigFolder()
        {
            configWatcher = new FileSystemWatcher(GetSecondaryConfigDirectoryPath(), "*.yaml");
            configWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            configWatcher.IncludeSubdirectories = false;
            configWatcher.Changed += OnConfigFileChanged;
            configWatcher.Created += OnConfigFileChanged;
            configWatcher.Deleted += OnConfigFileChanged;
            configWatcher.Renamed += OnConfigFileRenamed;
            configWatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            configWatcher.EnableRaisingEvents = true;
        }

        private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            DispatchChange(e.Name);
        }

        private static void OnConfigFileRenamed(object sender, RenamedEventArgs e)
        {
            // Renaming a file can take it out of one kind and into another
            DispatchChange(e.OldName);
            DispatchChange(e.Name);
        }

        private static void DispatchChange(string fileName)
        {
            foreach (ConfigChannel channel in Channels)
            {
                if (channel.Matches(fileName)) { channel.ScheduleReload(); }
            }
        }

        private static void SetupSecondaryConfigFiles()
        {
            WriteDefaultFile(Recipes, Recipes.FindFiles().Count, @"#################################################
# Recipe Manipulation Config
#################################################
# recipeModifications:                     # <- This is the top level key, all modifications live under this, it is required.
#   DisableWoodArrow:                      # <- This is the modification name, its primarily for you to understand what this modification does SHOULD BE UNIQUE
#     action: Disable                      # <- This is the action it should be one of [Disable, Delete, Modify, Add, Enable]
#     prefab: ArrowWood                    # <- This is the prefab that the modification will target
#   AddNewWoodArrowRecipe:
#     action: Add
#     prefab: ArrowWood
#     recipeName: Recipe_ArrowWood_Cheap   # <- optional. On an Add it names the new recipe, otherwise it picks which recipe to change when an item has several
#     craftedAt: piece_workbench           # <- The crafting station's prefab name, its $piece_ name works too. Left out of an Add, empty or 'none' means handcrafted
#     minStationLevel: 2                   # <- This is the required crafting station level for discovery AND crafting
#     recipe:                              # <- An Add needs a recipe
#       anyOneResource: false              # <- This makes the recipe only require one ingrediant, first from the top will be used.
#       noRecipeCost: false                # <- This makes the recipe not require any resources to craft, if this is used the ingredients list will be ignored
#       ingredients:                       # <- Ingrediants in the recipe, is an array
#         - prefab: Wood                   # <- Prefab that this ingrediant requires
#           craftCost: 2                   # <- The amount of this ingrediant it takes to craft the recipe
#           upgradeCost: 0                 # <- The amount of this ingrediant it takes to upgrade the item, per level
#         - prefab: Feathers
#           craftCost: 2
#           upgradeCost: 0
#   DeleteTrollHideArmorRecipe:
#     action: Delete
#     prefab: CapeTrollHide
#   CheaperTrollHideChest:
#     action: Modify                       # <- A Modify only changes what it sets, everything left out keeps the recipe's current value
#     prefab: ArmorTrollLeatherChest
#     minStationLevel: 2                   # <- Only the station level changes here, the station and ingredients stay as they are
#   TrollHideLegsCost:
#     action: Modify
#     prefab: ArmorTrollLeatherLegs
#     recipe:
#       ingredients:                       # <- Listing ingredients replaces them all. Leave the list out to keep them, [] makes the recipe free
#         - prefab: TrollHide
#           craftCost: 4                   # <- Left out, an ingredient keeps the amount the recipe already had for it
#           upgradeCost: 2
#         - prefab: Upgrader1Armor         # <- The idol the Forge of Potential takes to upgrade this armor
#           craftCost: 1                   # <- For an upgrader resource this is what every upgrade costs
#           upgraderResource: true         # <- Only paid at the Forge of Potential, every other station ignores it.
#                                          #    Leave the idol out of a Modify and the recipe keeps the one it had.
#
# Every field of a Modify keeps the recipe's current value when it is left out, an Add starts from the game's
# defaults. The exception is enabled: left out, Modify and Add switch the recipe on.
# RM_Recipes_PrintAll writes the recipes in the game out to ObjectDBRecipes.yaml, to copy entries from.
#     craftAmount: 1                       # <- How many items one craft makes
#     repairAt: piece_workbench            # <- A second station that can repair the item, besides the one it is crafted at
#     maxStationLevel: 3                   # <- The highest station level upgrading needs, uses a game patch
#     enabled: false                       # <- Keeps the recipe switched off
#     upgradeOnly: true                    # <- Only listed in a station's upgrade tab, never the craft tab. The Deep North gold gear uses this
#     listSortWeight: 100                  # <- Position in the crafting list, lower comes first. The game uses 100, fishing bait 500 and up
#     qualityResultAmountMultiplier: 3     # <- With anyOneResource, how much each quality level of the ingredient used adds
#     recipe:
#       ingredients:
#         - prefab: TrollHide
#           recover: true                  # <- Whether part of this is refunded when the Forge of Potential breaks the item
#           anyOneExtraAmount: 1           # <- With anyOneResource, extra items crafted when this is the ingredient used
#           upgradeCosts: {2: 5, 3: 10}    # <- What upgrading to each level costs, by the level reached. Uses a game patch
");

            WriteDefaultFile(Pieces, Pieces.FindFiles().Count, @"############################################################################
# Piece Manipulation Config
############################################################################
# A Modify only changes what it sets. Every field left out keeps the piece's current value.
# RM_Pieces_PrintAll writes every piece in the game out to AllPiecesDebug.yaml, to copy entries from.
#
# pieceModifications:                                      # <- This is the top level key, it is required
#  modify_the_bed:                                         # <- REQUIRED The name of this modification, it should be unique but is for your information
#    action: Modify                                        # <- REQUIRED This is the action applied can be [Enable, Disable, Modify, Add]
#    prefab: bed                                           # <- REQUIRED This is the prefab that the action will be applied to
#    requirements:                                         # <- The cost to build this. Left out keeps the cost, [] makes the piece free
#    - prefab: Wood                                        # <- Each requirement entry requires a prefab name, you can find item prefabs on the valheim wiki
#      amount: 8                                           # <- The amount of the prefab required
#      recover: true                                       # <- Whether this is refunded when the piece is torn down
#    requiredToPlaceCraftingStation: piece_workbench       # <- The crafting station used to place this item, if it is set to 'none' it will remove the station requirement
#    allowedInDungeon: false                               # <- If you can build this inside dungeons
#    canBeDeconstructed: true                              # <- If this can be broken with middle mouse
#    pieceCategory: Furniture                              # <- The build tab this is listed in, such as Misc, Crafting, BuildingWorkbench or Furniture. Any other name makes a tab of that name
#    comfortAmount: 1                                      # <- If above 0 this item will provide comfort
#    comfortGroup: Bed                                     # <- The comfort group this is a part of
#    isUpgradeForStation: false                            # <- If this piece is considered an upgrade for a crafting station
#    craftingStationConnectionRadius: 0                    # <- The radius that this will connect to a crafting station
#    mustBeAvobeConnectedStation: false                    # <- If this upgrade must be placed ABOVE its crafting station- this is normally only used for hanging upgrades for the cooking station
#    spaceRequired: 0                                      # <- How much space is required for this item
#    pieceName: $piece_bed                                 # <- The localizable name for this, setting ""My Bed"" will make this piece called ""My Bed""
#    pieceDescription: ''                                  # <- The description for this piece, many pieces do not have this
#    enablePiece: true                                     # <- Whether or not this piece is enabled
#    onlyInSelectBiome: Meadows, BlackForest               # <- The biomes this can be placed in, None or All for anywhere. In vanilla this is used for plants
#    cultivatedGroundOnly: false                           # <- Whether this piece needs to be placed on cultivated ground
#    groundPlacement: false                                # <- Whether this piece needs to be placed on ground (stone is also considered ground)
#
# 'action: Add' makes a new piece by copying clonedFrom. Like items, adding one needs a restart. A few of the
# other settings, every one optional, the README lists them all:
#  big_chest:
#    action: Add
#    prefab: piece_chest_big                               # <- The new piece's prefab name, it has to be unique
#    clonedFrom: piece_chest_wood
#    pieceName: Big chest
#    pieceTable: Hammer                                    # <- The tool it is built with. On a Modify this moves the piece
#    usage: Storage                                        # <- The build menu tags it is listed under
#    minStationLevel: 2                                    # <- The station level needed to place it, uses a game patch
#    groundOnly: true                                      # <- One of the placement rules
#    wearNTear:                                            # <- How it wears and breaks
#      health: 800
#    container:                                            # <- Also station, stationExtension and fireplace, for pieces that are one
#      width: 8
#      height: 4
#
# An icon at BepInEx\config\RecipeManager\Textures\<prefab>\icon.png replaces the piece's icon.");

            WriteDefaultFile(Conversions, Conversions.FindFiles().Count, @"############################################################################
# Conversion Manipulation Config
############################################################################
# conversionModifications:
#   BetterKiln:
#     action: Modify                 # <- Modify changes the station and replaces its conversion list when one is given,
#                                    #    Add adds conversions or updates the ones the station already has for the same input,
#                                    #    Remove takes out the conversions for the inputs listed
#     prefab: charcoal_kiln
#     maxOres: 30                    # <- Smelters, how many items it holds
#     maxFuel: 0                     # <- Smelters and cooking stations, how much fuel it holds. 0 makes a smelter need no fuel
#     fuelPerProduct: 1              # <- Smelters, fuel used for each item made
#     conversionTime: 10             # <- Smelters, seconds per item. Fermenters, seconds per batch
#     fuelItem: Wood                 # <- Smelters and cooking stations
#     secPerFuel: 5000               # <- Cooking stations, seconds one fuel lasts
#     overCookedItem: Coal           # <- Cooking stations, what food turns into when it is left on too long
#     conversions:
#       - fromPrefab: Wood
#         toPrefab: Coal
#         amount: 4                  # <- Fermenters, items made. Left out keeps the station's own, or 4
#         cookTime: 10               # <- Cooking stations, seconds to cook. Left out keeps the station's own, or 10
#     spawnStack: false              # <- Smelters, whether what it makes comes out as one stack
#     requiresRoof: true             # <- Smelters, whether it only works under a roof
#     requiresFire: true             # <- Cooking stations, whether it needs a fire under it
# The obliterator (incinerator) takes defaultResult, defaultCost and incineratorConversions instead, see the README.
# RM_Conversion_PrintAll writes every station in the game out to AllConversionsDebug.yaml, to copy entries from.
");

            WriteDefaultFile(Items, Items.FindFiles().Count, @"############################################################################
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
#       consumeStatusEffect: ''              # <- A status effect to apply when consumed, one StatusEffects.yaml adds works too
#     recipe:                                # <- Optional and only for Add, leave it out for an item that is not crafted.
#                                            #    An existing item's recipe is changed in Recipes.yaml
#       craftedAt: piece_cauldron            # <- The crafting station, leave it empty for handcrafting
#       repairAt: ''                         # <- A second station that can repair the item
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
#       # Ingredients also take upgraderResource, recover, anyOneExtraAmount and upgradeCosts, and the recipe takes
#       # maxStationLevel, see Recipes.yaml. A cloned weapon or armor needs an upgraderResource idol
#       # (Upgrader0Weapon..Upgrader7Armor) to be upgradeable at the Forge of Potential.
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
#     - type: Fire                           # <- Blunt, Slash, Pierce, Chop, Pickaxe, Fire, Frost, Lightning, Poison, Spirit or NonPlayer
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
# ---------------------------------------------------------------------------
# More stats. RM_Items_PrintAll writes every one of these an item uses, copy what you need from there.
# ---------------------------------------------------------------------------
#
#   QuickCrossbow:
#     action: Modify
#     prefab: CrossbowArbalest
#     equipDuration: 0.5                     # <- Seconds to put it on
#     quality:
#       maxLevel: 4                          # <- Highest upgrade level, 1 means it cannot be upgraded
#     durability:
#       max: 300
#       perLevel: 100                        # <- Durability added by each upgrade level
#       drainPerUse: 1
#     weapon:
#       skill: Crossbows                     # <- The skill it trains and uses, a mod's skill works too
#       primaryAttack:                       # <- Also secondaryAttack. Only the numbers change, never the animation
#         stamina: 10
#         speedFactor: 0.5                   # <- Movement speed while attacking
#         projectile:
#           velocity: 250
#           count: 1                         # <- Projectiles fired at once
#         reload:
#           time: 2                          # <- Seconds a reload takes
#     staminaModifiers:                      # <- Stamina cost changes while equipped, -0.1 is 10% cheaper
#       run: -0.1
#       jump: -0.1
#
# Prefabs an item points at, like a projectile or weapon.spawnOnHit, use the prefab's name. Use the value
# None to empty such a slot.
#
# Status effects are named by their prefab name, an effect added in StatusEffects.yaml works too. Use the
# value None to empty a status effect slot instead of naming one.
#
# Custom icons and textures are loaded from BepInEx\config\RecipeManager\Textures\<prefab>\
# Use 'RM_Textures_Dump <prefab>' in the console to dump an existing prefab's images, then copy that
# folder, rename it to your new item's prefab name, and repaint the files inside it.");

            WriteDefaultFile(StatusEffects, StatusEffects.FindFiles().Count, @"############################################################################
# Status Effect Config
############################################################################
# 'action: Add' makes a new status effect, a copy of 'clonedFrom' or, without it, one with no stats at all.
# 'action: Modify' changes an effect that already exists. Anything left out keeps the effect's current value.
# Items point at these by their prefab name, in Items.yaml.
#
# A change reaches an effect the next time it is given, one a player already has keeps its old values.
# RM_StatusEffects_PrintAll writes every effect in the game out to ObjectDBStatusEffects.yaml, to copy entries from.
#
# statusEffects:                             # <- This is the top level key, it is required
#   TrollSetStrength:                        # <- The name of this entry, it is for your information but SHOULD BE UNIQUE
#     action: Add
#     prefab: SetEffect_TrollStrength        # <- For Add, the NEW effect's name, which must be unique. For Modify, the effect to change
#     clonedFrom: SetEffect_TrollArmor       # <- Optional for Add, the effect to copy
#     name: Troll strength                   # <- The name shown in the game. Start it with '$' to use your own localization token
#     tooltip: Hits harder and carries more.
#     duration: 0                            # <- Seconds it lasts, 0 lasts for as long as something keeps it on, like a set bonus
#     carryWeight: 50                        # <- Weight added to what the player can carry
#     maxHealth: 25                          # <- Added to maximum health while it lasts, also maxStamina and maxEitr. Uses a game patch
#     damage:
#       skill: All                           # <- The weapon skill whose attacks it boosts, All for every attack
#       multiplier: 1.1                      # <- 1.1 is 10% more damage
#     movement:
#       speed: 0.05                          # <- 5% faster
#     resistances:                           # <- Replaces the whole resistance list, use [] to clear it
#     - type: Frost
#       modifier: Resistant
#   LongerRested:
#     action: Modify
#     prefab: Rested
#     health:
#       regenMultiplier: 1.6                 # <- Health regeneration, 1 is normal
#     stamina:
#       regenMultiplier: 1.6
#
# More settings: category, cooldown, hidden, attributes, messages, staminaUse, eitr, adrenaline, armor, stagger,
# skills, noise and stealth. Every one an effect uses is in the dump, and the README lists them all.
# The icon is loaded from BepInEx\config\RecipeManager\Textures\<prefab>\icon.png");
        }

        /// <summary>
        /// Writes an example file for a kind of config that has none. It is all comments, which loads as no entries.
        /// It used to end in an empty top level key, which silently emptied the file again when the example's own
        /// top level key was uncommented above it.
        /// </summary>
        private static void WriteDefaultFile(ConfigChannel channel, int existingFiles, string header)
        {
            if (existingFiles > 0) { return; }
            Logger.LogInfo($"No {channel.Label} file found, creating {channel.Suffix}.");
            string path = Path.Combine(GetSecondaryConfigDirectoryPath(), channel.Suffix);
            using (StreamWriter writetext = new StreamWriter(path))
            {
                writetext.WriteLine(header);
            }
        }

        public static string GetSecondaryConfigDirectoryPath() {
            var patchesFolderPath = Path.Combine(Paths.ConfigPath, "RecipeManager");
            var dirInfo = Directory.CreateDirectory(patchesFolderPath);

            return dirInfo.FullName;
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

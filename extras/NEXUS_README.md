RecipeManager

This is a lightweight recipe modification tool. You can define recipes to Add, Modify, Delete etc. It does not run constantly, and it only patches the game for the few settings that need it, while a config uses one of them, see Game patches below.

Coming from [url=https://thunderstore.io/c/valheim/p/WackyMole/WackysDatabase/]WackysDB[/url]? RM_Import_WackyDB converts its files, see Importing from WackysDatabase below.

In Valheim a recipe is something that is crafting at a crafting station, or cooking station etc. For example, to make wood arrows you use a crafting recipe from the workbench.
Building pieces (built with the hammer) are not recipes, furnace conversions (1 ore for 1 ingot etc) are not recipes.

Pieces are almost anything that is placed. Everything built with the hammer, plants placed with the cultivator, placable food etc.


[size=5][b]Features[/b][/size]
[list]
[*]Add recipes[/*]
[*]Modify recipes[/*]
[*]Delete recipes[/*]
[*]Disable Pieces[/*]
[*]Enable Pieces[/*]
[*]Modify Pieces[/*]
[*]Add pieces by copying one, move pieces between tools and tabs, and change their placement rules, health, containers, fires and stations[/*]
[*]Build pieces out of items, such as trophies you hang on the wall, that give the item back when removed[/*]
[*]Add new items by cloning an existing item, with their own name, stats, icon and textures[/*]
[*]Modify the stats of items that already exist, including vanilla weapons and armor, down to their attacks, durability and upgrade levels[/*]
[*]Add new status effects and change the ones that exist[/*]
[*]Dump any prefab's icons and textures to disk so you can repaint them[/*]
[*]Convert WackysDatabase recipe, item, piece and status effect files into RecipeManager files[/*]
[/list]


[size=4][b]Upgrading to 0.11[/b][/size]

A Modify now only changes what it sets, for recipes and pieces as it already did for items. Everything it leaves out keeps the value the recipe or piece already has. Two things behave differently because of that:

[list]
[*]A recipe Modify without craftedAt keeps the recipe's station. It used to become handcrafted, so write craftedAt: none if that is what you want. The same goes for minStationLevel, craftAmount, anyOneResource and the ingredients, which are all kept when left out. A craftedAt naming no station that exists also used to mean handcrafted, it now keeps the station and says so in the log.[/*]
[*]A piece Modify without requirements keeps the piece's cost. It used to make the piece free, so write requirements: [] for a free piece. Every other piece setting left out is kept as well, where it used to be reset, which could lift a sapling's biome restriction or make a piece deconstructable.[/*]
[/list]

Entries copied out of the PrintAll dumps list every value, so they work the same as before.

Conversions changed too: an Add for an input the station already converts now updates that conversion instead of adding a second one the station would never use. An entry also only changes the station it names, so piece_cookingstation no longer changes piece_cookingstation_iron as well.


[size=4][b]Console commands[/b][/size]
Each part of the mod has commands to write what is in the game out to a file, and to apply your changes without restarting. They are described in the section they belong to:

[list]
[*]Recipes: RM_Recipes_PrintAll, RM_Recipes_Reload, RM_Recipes_Unapply[/*]
[*]Pieces: RM_Pieces_PrintAll, RM_Pieces_Reload[/*]
[*]Conversions: RM_Conversion_PrintAll, RM_Conversion_Reload[/*]
[*]Items: RM_Items_PrintAll, RM_Items_Reload, RM_Textures_Dump[/*]
[*]Status effects: RM_StatusEffects_PrintAll, RM_StatusEffects_Reload[/*]
[*]WackysDatabase: RM_Import_WackyDB[/*]
[*]Game patches: RM_Patches[/*]
[/list]

The PrintAll commands write to \BepInEx\config\RecipeManager\. Those files are never loaded, they are there for you to copy entries out of.

The Reload commands only work where the configuration comes from this machine's own files: a solo game or the machine hosting. A client connected to a server always uses the server's configuration.

Commands require [url=https://valheim.fandom.com/wiki/Developer_console]enabling the in-game console[/url] and running devcommands first. Valheim only treats devcommands as enabled on the instance that hosts the world, so run them in a solo game or on the machine hosting, not from a client connected to someone else's server.

If you are using Thunderstore Mod Manager or R2 Modmanager, you can enabled the console by selecting your valheim profile, going to settings (on the far left), debugging tab, set launch parameters and add --console

It is not recommended to use Vortex mod manager, however using vortex hardlink or copy deployments you can launch from steam using BepinEx with a --console launch parameter. To do so right click on the game in your library, manage, general, at the bottom add --console to the launch options.


[size=4][b]Game patches[/b][/size]

Almost everything this mod does is data written onto the game's own items, recipes and pieces. A few settings have nothing in the game to write to, and need a small patch instead. Each patch is only applied while a loaded config uses one of its settings, and taken off again when none does. Leave those settings out and the game is not patched at all. A dedicated server never applies them, the game only reads these settings on the players' machines.

[list]
[*]a recipe ingredient's upgradeCosts, in Recipes.yaml or an item's recipe in Items.yaml: RecipeUpgradeCosts[/*]
[*]a recipe's maxStationLevel, in either file: RecipeStationLevelCap[/*]
[*]a status effect's maxHealth, maxStamina or maxEitr: StatusEffectStatBonus[/*]
[*]a piece's minStationLevel: PieceStationLevel[/*]
[/list]

RM_Patches lists every patch, whether it is on, and which config entries use it. It works without devcommands.


[size=4][b]Config files[/b][/size]

Every config file lives in \BepInEx\config\RecipeManager\. Saving, adding or deleting one while the game is running applies it straight away, and the Reload commands do the same on demand. Files in subfolders are not loaded.

Its recommended to use a syntax highlighting yaml editor when modifying recipes to help ensure that your yaml is valid. [url=https://yamlchecker.com/]yamlChecker[/url] is a free online linter you can use to validate also.

A file's name decides what it holds, by how it ends (upper or lower case does not matter):

[list]
[*]Files ending in Recipes.yaml are recipe files, except ObjectDBRecipes.yaml. Forge_Recipes.yaml is loaded, SoupStationStuff.yaml and Recipes.yaml.bak are not.[/*]
[*]Files ending in Pieces.yaml are piece files, such as CultivatorPieces.yaml.[/*]
[*]Files ending in Conversions.yaml are conversion files.[/*]
[*]Files ending in Items.yaml are item files, except ObjectDBItems.yaml. Food_Items.yaml is loaded, Food.yaml is not.[/*]
[*]Files ending in StatusEffects.yaml are status effect files, except ObjectDBStatusEffects.yaml.[/*]
[/list]

Files of one kind are read in name order. Every entry name has to be unique across all files of its kind: a name used twice keeps its first definition and the log says where the second one is. A file that does not parse is skipped with an error in the log, and the other files still load.


[size=4][b]Recipes[/b][/size]

Recipes can be manipulated and added through yaml. All of the existing recipes can also be dumped to a file, in the same format to help you find and understand existing recipes.

Recipe modifications are stored at \BepInEx\config\RecipeManager\Recipes.yaml. This file will be generated on the first run if it does not already exist.
R2modman or Thunderstore mod manager will also detect this file as an editable config file and it can be edited or opened directly from the "Edit Config" menu, search for Recipes.yaml.

[b]Yaml configuration example:[/b]
[code]
#################################################
# Recipe Manipulation Config
#################################################
recipeModifications:                     # <- This is the top level key, all modifications live under this, it is required.
  DisableWoodArrow:                      # <- This is the modification name, its primarily for you to understand what this modification does SHOULD BE UNIQUE
    action: Disable                      # <- This is the action it should be one of [Disable, Delete, Modify, Add, Enable]
    prefab: ArrowWood                    # <- This is the prefab that the modification will target
  AddNewWoodArrowRecipe:
    action: Add
    prefab: ArrowWood
    recipeName: Recipe_ArrowWood_Cheap   # <- optional. On an Add it names the new recipe, otherwise it picks which recipe to change when an item has several
    craftedAt: piece_workbench           # <- The crafting station's prefab name, its $piece_ name works too. Left out of an Add, empty or 'none' means handcrafted
    minStationLevel: 2                   # <- This is the required crafting station level for discovery AND crafting
    recipe:                              # <- An Add needs a recipe
      anyOneResource: false              # <- This makes the recipe only require one ingrediant, first from the top will be used.
      ingredients:                       # <- Ingrediants in the recipe, is an array
        - prefab: Wood                   # <- Prefab that this ingrediant requires
          craftCost: 2                   # <- The amount of this ingrediant it takes to craft the recipe
          upgradeCost: 0                 # <- The amount of this ingrediant it takes to upgrade the item, per level
        - prefab: Feathers
          craftCost: 2
          upgradeCost: 0
  DeleteTrollHideArmorRecipe:
    action: Delete
    prefab: CapeTrollHide
  CheaperTrollHideChest:
    action: Modify                       # <- A Modify only changes what it sets, everything left out keeps the recipe's current value
    prefab: ArmorTrollLeatherChest
    minStationLevel: 2                   # <- Only the station level changes here, the station and ingredients stay as they are
  TrollHideLegsCost:
    action: Modify
    prefab: ArmorTrollLeatherLegs
    recipe:
      ingredients:                       # <- Listing ingredients replaces them all. Leave the list out to keep them, [] makes the recipe free
        - prefab: TrollHide
          craftCost: 4                   # <- Left out, an ingredient keeps the amount the recipe already had for it
          upgradeCost: 2
        - prefab: Upgrader1Armor         # <- The idol the Forge of Potential takes to upgrade this armor
          craftCost: 1                   # <- For an upgrader resource this is what every upgrade costs
          upgraderResource: true         # <- Only paid at the Forge of Potential, every other station ignores it.
                                         #    Leave the idol out of a Modify and the recipe keeps the one it had.
[/code]

A Modify changes only what it sets: every field it leaves out, the ingredients included, keeps the recipe's current value. An Add starts from the game's defaults: handcrafted, station level 1, one item per craft. craftedAt and repairAt take a station's prefab name (piece_workbench, forge, piece_cauldron...) or its $piece_ name, and none makes a recipe handcrafted. A station name that matches nothing is reported in the log, and the recipe keeps its station.

Items can always be repaired at the station they are crafted at. repairAt adds a second station that can repair them.

To have an item's upgrades cost something different from crafting it, give an ingredient a craftCost of 0 and an upgradeCost. It is then only paid when upgrading, and an ingredient with an upgradeCost of 0 is only paid when crafting.

The game charges an ingredient's upgradeCost for each level gained, so the second upgrade costs twice the first. For costs that are different at each level, give the ingredient upgradeCosts, with the level reached and what it costs. A level left out costs none of that ingredient, except the Forge of Potential idol, which costs its craftCost as usual. This uses a game patch.

The game also asks for one more station level for each upgrade. maxStationLevel caps that, so an item with more upgrade levels than its station has can still be fully upgraded. It never goes below minStationLevel, and also uses a game patch. Both work on an item's recipe in Items.yaml too.

[b]Yaml example:[/b]
[code]
recipeModifications:
  IronSwordUpgrades:
    action: Modify
    prefab: SwordIron
    maxStationLevel: 3                   # <- Upgrading never needs more than a level 3 forge
    recipe:
      ingredients:
        - prefab: Iron
          upgradeCosts:                  # <- Level reached: what it costs
            2: 5
            3: 10
            4: 20
        - prefab: Silver
          craftCost: 0                   # <- Only for the last upgrade
          upgradeCosts:
            4: 5
[/code]

[b]Optional recipe fields:[/b]
Left out of a Modify, each of these keeps the value the recipe already had, and an Add starts from the game's defaults. The exception is enabled: left out, Modify and Add switch the recipe on. RM_Recipes_PrintAll writes them out wherever a recipe differs from the defaults, so copying an entry out of the dump keeps them.
[code]
    craftAmount: 1                       # <- How many items one craft makes
    repairAt: piece_workbench            # <- A second station that can repair the item
    maxStationLevel: 3                   # <- The highest station level upgrading needs, a game patch
    enabled: false                       # <- Keeps the recipe switched off
    upgradeOnly: true                    # <- Only listed in a station's upgrade tab, never the craft tab. The Deep North gold gear uses this
    listSortWeight: 100                  # <- Position in the crafting list, lower comes first. The game uses 100, fishing bait 500 and up
    qualityResultAmountMultiplier: 3     # <- With anyOneResource, how much each quality level of the ingredient used adds
    recipe:
      noRecipeCost: true                 # <- Free to craft, the ingredients are ignored. The same as ingredients: []
      ingredients:
        - prefab: TrollHide
          upgraderResource: false        # <- true for the Forge of Potential idol, see the example above
          recover: true                  # <- Whether part of this is refunded when the Forge of Potential breaks the item
          anyOneExtraAmount: 1           # <- With anyOneResource, extra items crafted when this is the ingredient used
          upgradeCosts: {2: 5, 3: 10}    # <- What upgrading to each level costs, a game patch
[/code]


[size=3][b]Recipe commands[/b][/size]

RM_Recipes_PrintAll - Writes every recipe in the object DB, including your modifications, to ObjectDBRecipes.yaml, each one as a Modify with every value written out. To edit a recipe, copy its entry into Recipes.yaml and delete the lines you do not want to change.

RM_Recipes_Reload - Puts every recipe back the way it was, reads the recipe files again and applies them. The result is sent to connected players.

RM_Recipes_Unapply - Puts every modified recipe back the way it was without reading the files again, which is handy for comparing against the originals. It only changes the instance it is run on, and lasts until the next RM_Recipes_Reload, recipe file change or world load.


[size=3][b]Recipe examples[/b][/size]

The mod ships with a default example recipe file which will provide you similar explanations to what is listed above. You can find the recipe file after starting up your game with the mod installed in your BepInEx\config\RecipeManager folder.

Add a craftable chain (replace [url=https://thunderstore.io/c/valheim/p/Digitalroot/Chain_Manager/]Chain-Manager[/url])
[code]
recipeModifications:
  CraftableChainRecipe:
    action: Add
    prefab: Chain
    craftedAt: forge
    minStationLevel: 4
    craftAmount: 1
    recipe:
      anyOneResource: false
      ingredients:
      - prefab: Iron
        craftCost: 2
        upgradeCost: 0
[/code]

Disable recipes you don't want from vanilla or other mods (disable recipes from [url=https://thunderstore.io/c/valheim/p/southsil/SouthsilArmor/]Southsil Armors[/url])
[code]
recipeModifications:
  DisableNeckHelm:
    action: Disable
    prefab: neckhelm
  DisableNeckChest:
    action: Disable
    prefab: neckchest
  DisableNeckLegs:
    action: Disable
    prefab: necklegs
[/code]

Increase the bronze yield (replace [url=https://thunderstore.io/c/valheim/p/Digitalroot/Triple_Bronze_JVL/]triple bronze[/url]). Only the amount changes, the station and ingredients stay as they are.
[code]
recipeModifications:
  Recipe_Bronze:
    action: Modify
    prefab: Bronze
    recipeName: Recipe_Bronze
    craftAmount: 3
  Recipe_Bronze5:
    action: Modify
    prefab: Bronze
    recipeName: Recipe_Bronze5
    craftAmount: 15
[/code]

Move a recipe from one crafting station to another (Custom mead from [url=https://thunderstore.io/c/valheim/p/OhhLoz/HoneyPlus/]Honey+[/url])
[code]
recipeModifications:
  custom_item_meadbase_damage:
    action: Modify
    prefab: HoneyMeadBaseDamage
    recipeName: $custom_item_meadbase_damage
    craftedAt: piece_cauldron
[/code]


[size=4][b]Pieces[/b][/size]

Piece modifications are stored at \BepInEx\config\RecipeManager\Pieces.yaml. This file will be generated on the first run, with an explanation of the most used fields, if it does not already exist.

A Modify changes only what it sets, every field it leaves out keeps the piece's current value. requirements replaces the whole build cost when it is given, and requirements: [] makes a piece free. A requirement without recover keeps the refund setting the piece already has for that item. requiredToPlaceCraftingStation: none removes the need for a station. pieceCategory takes a build tab, one of the game's or one another mod has added, and any other name makes a tab of that name. onlyInSelectBiome takes one or more biomes separated by commas, and None or All lets the piece be placed anywhere.

[size=3][b]Adding a piece[/b][/size]

action: Add makes a new piece by copying clonedFrom. The copy keeps the model and every setting of the piece it was copied from, and everything else the entry sets is applied on top. pieceTable picks the tool it is built with (Hammer, Hoe, Cultivator, or a tool another mod adds), and left out it is built with the same tool as the original. Set pieceName, or it shares the original's name. A copy of a crafting station becomes a station of its own, named after the piece, so the recipes and station upgrades meant for it have to name it by its prefab name.

[quote]Like items, adding or removing a piece needs a game restart (or a trip back to the main menu). Everything else applies live with RM_Pieces_Reload.[/quote]


[size=3][b]Pieces made from an item[/b][/size]

Instead of clonedFrom, an Add can set fromItem to an item that has a display model, the one an item stand shows: trophies, weapons, shields, helmets and the like. The piece is that model, posed the way it would be on an item stand but without the stand. mount: Wall (the default) copies the wall item stand, mount: Floor the one that stands on the ground or a table. Everything else is an ordinary piece, so it can be damaged, repaired, removed with the hammer and protected by a ward, it needs something to rest on, and every setting above and below works on it.

Building it uses up the item, and removing or destroying it gives the item back. That cost is always there: requirements adds to it, and an entry that leaves the item out of its list, or gives it no refund, gets it put back. Left out, the name, description and icon are the item's, and the station, health and sounds are the item stand's.

Like an item stand, it gives 1 comfort in the Display group, where only one piece counts. Set comfortGroup: None to have each different item count on its own. The game counts pieces with the same name once, so a second copy of the same trophy adds nothing.

[list]
[*]An item with upgrade levels or durability comes back as a new one at level 1.[/*]
[*]An item another mod adds is only found if that mod has added it by the time the main menu loads.[/*]
[*]An item whose model carries a glow or sparkle effect may not show in the build preview. The placed piece shows it.[/*]
[*]Looking at it shows no text, as with most pieces.[/*]
[/list]

[b]Yaml example:[/b]
[code]
pieceModifications:
  trophy_eikthyr:
    action: Add
    prefab: piece_trophy_eikthyr           # <- The new piece's prefab name, it has to be unique
    fromItem: TrophyEikthyr                # <- The item it is made from, and costs
    mount: Wall                            # <- Wall or Floor
    pieceTable: Hammer
    pieceCategory: Furniture
    comfortAmount: 1
    comfortGroup: None                     # <- Each different trophy counts on its own
  trophy_elder:
    action: Add
    prefab: piece_trophy_elder
    fromItem: TrophyTheElder
    mount: Floor
    comfortAmount: 1
    comfortGroup: None
    requirements:                          # <- Added to the trophy, which is always part of the cost
    - prefab: FineWood
      amount: 2
[/code]


[size=3][b]Tools, tabs and placement[/b][/size]

pieceTable on a Modify moves a piece to another tool. usage sets the build menu tags the game sorts pieces by (Building, Furniture, Storage and so on, several separated by commas).

minStationLevel asks for an upgraded station: the piece stays unknown until a player has seen the station at that level, and can only be placed next to one. It uses a game patch, and the build menu does not show the level.

The placement rules are all true or false: groundOnly, allowAltGroundPlacement, waterPiece, noInWater, notOnWood, notOnTiltingSurface, inCeilingOnly, notOnFloor, noClipping, clipGround, clipEverything, onlyInTeleportArea, allowedInDeepSnow, requireDeepSnow, vegetationGroundOnly, canRotate, allowRotatedOverlap, canRockJade and noVines. extraPlacementDistance lets a piece be placed further away, and blockingPieces lists pieces that cannot be within blockRadius of it.

An icon at \BepInEx\config\RecipeManager\Textures\&lt;prefab&gt;\icon.png replaces the piece's icon.


[size=3][b]Piece parts[/b][/size]

Besides how it is built, a piece can be a crafting station, a station upgrade, a container or a fire, and most pieces wear and break. Each of those parts has a block of its own, and a block for a part the piece does not have is skipped with a warning. A change to them reaches pieces already built the next time their area loads. RM_Pieces_PrintAll writes out every part a piece has.

A container made smaller can lose what is stored past its new size the next time it loads.

[b]Yaml example:[/b]
[code]
pieceModifications:
  big_chest:
    action: Add
    prefab: piece_chest_big                # <- The new piece's prefab name, it has to be unique
    clonedFrom: piece_chest_wood
    pieceName: Big chest
    pieceTable: Hammer
    pieceCategory: Storage                 # <- A new tab, named Storage
    minStationLevel: 2                     # <- Needs an upgraded workbench nearby, a game patch
    requirements:
    - prefab: FineWood
      amount: 20
    container:
      width: 8
      height: 4
    wearNTear:
      health: 800
      resistances:                         # <- The damage types left out keep the piece's own
      - type: Fire
        modifier: Resistant
  forge_reach:
    action: Modify
    prefab: forge
    station:
      buildRange: 30                       # <- How far from it pieces that need it can be built
      craftRequiresRoof: false
  bonfire_forever:
    action: Modify
    prefab: bonfire
    fireplace:
      infiniteFuel: true
[/code]

[b]Every part's fields:[/b]
[code]
    wearNTear:
      health: 400
      materialType: Wood                   # <- Wood, Stone, Iron, HardWood, Marble, Ashstone, Ancient, Ice or Timberwood, decides how much it can carry
      noRoofWear: true                     # <- Spared the wear of standing in the rain
      noSupportWear: true                  # <- Despite the name, true means it needs support and breaks without it
      supports: true                       # <- Whether other pieces can rest on it
      triggerPrivateArea: true             # <- Whether damaging it alerts a nearby ward
      burnable: true
      minToolTier: 0                       # <- The lowest tool tier that can damage it
      ashDamageImmune: false
      ashDamageResist: false
      snowDamageImmune: false
      resistances: [{type: Fire, modifier: Resistant}]  # <- Only the damage types listed change
    station:
      name: $piece_forge                   # <- What recipes and upgrades know the station by
      discoveryRange: 4                    # <- How close a player has to come to learn it
      buildRange: 20
      extraRangePerLevel: 4                # <- Build range added by each upgrade
      useDistance: 2
      craftRequiresRoof: true
      craftRequiresFire: true
      showBasicRecipes: false              # <- Also lists the recipes that need no station
      canRepair: true
    stationExtension:
      station: piece_workbench             # <- The station it upgrades
      maxStationDistance: 5
      stack: false                         # <- Whether several of it count as several upgrades
      continuousConnection: false
    container:
      width: 5
      height: 2
      checkWard: true                      # <- Whether a ward keeps others out
      autoDestroyIfEmpty: false
    fireplace:
      startFuel: 3
      maxFuel: 10
      secPerFuel: 3                        # <- Seconds one fuel lasts
      infiniteFuel: false
      fuelItem: Wood
      canTurnOff: false
      canRefill: true
      igniteInterval: 0                    # <- Seconds between tries to set fire to what is around it, 0 for never
      igniteChance: 0
      igniteSpread: 4
[/code]

[size=3][b]Piece commands[/b][/size]

RM_Pieces_PrintAll - Writes every piece in the game, including your modifications, to AllPiecesDebug.yaml, each one as a Modify with every value written out. Copied unchanged, an entry leaves its piece as it is.

RM_Pieces_Reload - Puts every piece back the way it was, reads the piece files again and applies them. The result is sent to connected players.

[size=3][b]Piece examples[/b][/size]

Enables all of the holiday building pieces
[code]
pieceModifications:
  enable_mistletoe:
    action: Enable
    prefab: piece_mistletoe
  enable_jackoturnip:
    action: Enable
    prefab: piece_jackoturnip
  enable_yuletree:
    action: Enable
    prefab: piece_xmastree
  enable_yulecrown:
    action: Enable
    prefab: piece_xmascrown
  enable_yulegarland:
    action: Enable
    prefab: piece_xmasgarland
  enable_gift1:
    action: Enable
    prefab: piece_gift1
  enable_gift2:
    action: Enable
    prefab: piece_gift2
  enable_maypole:
    action: Enable
    prefab: piece_maypole
[/code]

Makes the kiln cost 100 stone, but only 1 surtling core
[code]
pieceModifications:
  expensive_kiln:
    action: Modify
    prefab: charcoal_kiln
    requirements:
    - prefab: Stone
      amount: 100
    - prefab: SurtlingCore
      amount: 1
[/code]

Removes the stonecutter as a buildable piece
[code]
pieceModifications:
  remove_stonecutter:
    action: Disable
    prefab: piece_stonecutter
[/code]


[size=4][b]Conversions[/b][/size]

Conversion modifications are stored at \BepInEx\config\RecipeManager\Conversions.yaml. This file will be generated on the first run if it does not already exist.

[size=3][b]Conversion commands[/b][/size]

RM_Conversion_PrintAll - Writes the conversions of every smelter type station, fermenter, cooking station and obliterator, including your modifications, to AllConversionsDebug.yaml, in the same format as Conversions.yaml.

RM_Conversion_Reload - Puts every station back the way it was, reads the conversion files again and applies them. The result is sent to connected players.

[size=3][b]Conversion examples[/b][/size]

Any field left out of a conversion modification keeps the station's own value, conversions included. A few values need care:
[list]
[*][b]maxFuel: 0[/b] makes a smelter type station need no fuel at all.[/*]
[*][b]fuelPerProduct: 0[/b] means fuel still has to be loaded, but it is never used up.[/*]
[*][b]maxOres: 0[/b] makes the station accept no items to convert.[/*]
[*]Smelter type stations produce at most one item per second, and burn fuelPerProduct / conversionTime fuel each second. A conversionTime below 1 would only burn more fuel per item, so it is raised to 1. A conversionTime of 0 is ignored.[/*]
[*][b]amount[/b] is for fermenters and [b]cookTime[/b] is for cooking stations, smelter type stations always make one item. Left out, a conversion keeps the amount or cook time the station already has for that input, or uses the game's own 4 items and 10 seconds for a new one.[/*]
[*][b]overCookedItem[/b] sets what a cooking station turns food into when it is left on too long.[/*]
[*][b]spawnStack[/b] and [b]requiresRoof[/b] are for smelter type stations: whether what it makes comes out as one stack, and whether it only works under a roof. [b]requiresFire[/b] is for cooking stations, whether it needs a fire under it.[/*]
[/list]

[b]action: Modify[/b] sets the station's own values and, when conversions is listed, replaces its conversions with the ones listed. [b]action: Add[/b] updates the conversion the station already has for each input listed, or adds one when it has none: a station only ever uses the first conversion for an input, so there is no point having two. [b]action: Remove[/b] takes out the conversions for each input listed, or only the ones making toPrefab when it is given.

A modification applies to the station it names and to every copy of it already built, but not to other stations whose names start the same way, so piece_cookingstation leaves piece_cookingstation_iron alone.

Smelter type stations can leave out one side of a conversion:
[list]
[*]Leave out [b]toPrefab[/b] and the item is used up without making anything. This is how the battering ram burns wood.[/*]
[*]Leave out [b]fromPrefab[/b] and the item is made from fuel alone. This is how the frost kiln makes frozen fuel from ice. It only works on a station with maxOres: 0, and only the first conversion without a fromPrefab is ever made.[/*]
[*]The battering ram has nowhere to load fuel and the frost kiln has nowhere to load items, so keep the ram at maxFuel: 0 and the kiln at maxOres: 0 or they never run.[/*]
[/list]

Fermenters and cooking stations need both sides, except in an Add or Remove for an input the station already converts, where a left out toPrefab keeps or matches whatever it makes.

Makes the charcoal kiln take wood, finewood and corewood
[code]
conversionModifications:
  charcoal_kiln:
    action: modify
    prefab: charcoal_kiln
    conversions:
    - fromPrefab: Wood
      toPrefab: Coal
    - fromPrefab: FineWood
      toPrefab: Coal
    - fromPrefab: RoundLog
      toPrefab: Coal
    maxOres: 50
    conversionTime: 3
[/code]

Makes the blast furnance take all ores, increases its holding size, and changes the time it takes to smelt an item.
[code]
conversionModifications:
  blastfurnace:
    prefab: blastfurnace
    action: modify
    conversions:
    - fromPrefab: FlametalOreNew
      toPrefab: FlametalNew
    - fromPrefab: BlackMetalScrap
      toPrefab: BlackMetal
    - fromPrefab: CopperOre
      toPrefab: Copper
    - fromPrefab: IronOre
      toPrefab: Iron
    - fromPrefab: IronScrap
      toPrefab: Iron
    - fromPrefab: BronzeScrap
      toPrefab: Bronze
    - fromPrefab: TinOre
      toPrefab: Tin
    - fromPrefab: SilverOre
      toPrefab: Silver
    - fromPrefab: CopperScrap
      toPrefab: Copper
    maxOres: 20
    maxFuel: 50
    fuelPerProduct: 2
    conversionTime: 15
[/code]

Makes the fermenter brew very quickly, and makes every mead base produce 10 potions instead of less.
[code]
conversionModifications:
  fermenter:
    prefab: fermenter
    action: modify
    conversions:
    - fromPrefab: MeadBaseHealthMinor
      toPrefab: MeadHealthMinor
      amount: 10
    - fromPrefab: MeadBaseHealthMedium
      toPrefab: MeadHealthMedium
      amount: 10
    - fromPrefab: MeadBaseStaminaMinor
      toPrefab: MeadStaminaMinor
      amount: 10
    - fromPrefab: MeadBaseStaminaMedium
      toPrefab: MeadStaminaMedium
      amount: 10
    - fromPrefab: MeadBasePoisonResist
      toPrefab: MeadPoisonResist
      amount: 10
    - fromPrefab: MeadBaseFrostResist
      toPrefab: MeadFrostResist
      amount: 10
    - fromPrefab: BarleyWineBase
      toPrefab: BarleyWine
      amount: 10
    - fromPrefab: MeadBaseTasty
      toPrefab: MeadTasty
      amount: 10
    - fromPrefab: MeadBaseHealthMajor
      toPrefab: MeadHealthMajor
      amount: 10
    - fromPrefab: MeadBaseStaminaLingering
      toPrefab: MeadStaminaLingering
      amount: 10
    - fromPrefab: MeadBaseEitrMinor
      toPrefab: MeadEitrMinor
      amount: 10
    - fromPrefab: MeadBaseEitrLingering
      toPrefab: MeadEitrLingering
      amount: 10
    - fromPrefab: MeadBaseHealthLingering
      toPrefab: MeadHealthLingering
      amount: 10
    - fromPrefab: MeadBaseBzerker
      toPrefab: MeadBzerker
      amount: 10
    - fromPrefab: MeadBaseStrength
      toPrefab: MeadStrength
      amount: 10
    - fromPrefab: MeadBaseHasty
      toPrefab: MeadHasty
      amount: 10
    - fromPrefab: MeadBaseLightFoot
      toPrefab: MeadLightfoot
      amount: 10
    - fromPrefab: MeadBaseSwimmer
      toPrefab: MeadSwimmer
      amount: 10
    - fromPrefab: MeadBaseTamer
      toPrefab: MeadTamer
      amount: 10
    - fromPrefab: MeadBaseBugRepellent
      toPrefab: MeadBugRepellent
      amount: 10
    conversionTime: 10
[/code]

Makes the cooking station cook faster.
[code]
conversionModifications:
  piece_cookingstation:
    action: modify
    prefab: piece_cookingstation
    conversions:
    - fromPrefab: RawMeat
      toPrefab: CookedMeat
      cookTime: 10
    - fromPrefab: NeckTail
      toPrefab: NeckTailGrilled
      cookTime: 10
    - fromPrefab: FishRaw
      toPrefab: FishCooked
      cookTime: 10
    - fromPrefab: DeerMeat
      toPrefab: CookedDeerMeat
      cookTime: 10
    - fromPrefab: WolfMeat
      toPrefab: CookedWolfMeat
      cookTime: 10
    - fromPrefab: ChickenMeat
      toPrefab: CookedChickenMeat
      cookTime: 10
    - fromPrefab: HareMeat
      toPrefab: CookedHareMeat
      cookTime: 10
[/code]

Makes the oven cook faster, and fuel last longer.
[code]
conversionModifications:
  piece_oven:
    action: modify
    prefab: piece_oven
    conversions:
    - fromPrefab: LoxPieUncooked
      toPrefab: LoxPie
      cookTime: 10
    - fromPrefab: BreadDough
      toPrefab: Bread
      cookTime: 10
    - fromPrefab: FishAndBreadUncooked
      toPrefab: FishAndBread
      cookTime: 10
    - fromPrefab: MeatPlatterUncooked
      toPrefab: MeatPlatter
      cookTime: 10
    - fromPrefab: HoneyGlazedChickenUncooked
      toPrefab: HoneyGlazedChicken
      cookTime: 10
    - fromPrefab: MisthareSupremeUncooked
      toPrefab: MisthareSupreme
      cookTime: 10
    - fromPrefab: MagicallyStuffedShroomUncooked
      toPrefab: MagicallyStuffedShroom
      cookTime: 10
    - fromPrefab: RoastedCrustPieUncooked
      toPrefab: RoastedCrustPie
      cookTime: 10
    - fromPrefab: PiquantPieUncooked
      toPrefab: PiquantPie
      cookTime: 10
    maxFuel: 10
    fuelItem: Wood
    secPerFuel: 2000
[/code]

Makes the spinning wheel work faster and take up to 50 flax.
[code]
conversionModifications:
  piece_spinningwheel:
    action: Modify
    prefab: piece_spinningwheel
    conversions:
    - fromPrefab: Flax
      toPrefab: LinenThread
    maxOres: 50
    conversionTime: 3
[/code]

Lets the battering ram run on coal as well as wood, and hold twice as much.
[code]
conversionModifications:
  BatteringRam:
    action: Modify
    prefab: BatteringRam
    conversions:
    - fromPrefab: Wood
    - fromPrefab: FineWood
    - fromPrefab: RoundLog
    - fromPrefab: Blackwood
    - fromPrefab: Coal
    maxOres: 50
[/code]

Stops the battering ram taking blackwood.
[code]
conversionModifications:
  ram_no_blackwood:
    action: Remove
    prefab: BatteringRam
    conversions:
    - fromPrefab: Blackwood
[/code]

Makes the frost kiln produce frozen fuel twice as fast, from 2 ice instead of 5.
[code]
conversionModifications:
  piece_FrostKiln:
    action: Modify
    prefab: piece_FrostKiln
    conversions:
    - toPrefab: FrozenFuel
    fuelPerProduct: 2
    conversionTime: 15
[/code]


[size=3][b]The obliterator[/b][/size]

The obliterator (incinerator) has a list of its own, incineratorConversions. Each one makes result from its ingredients, and needs at least one. Everything without a conversion turns into defaultResult, one for every defaultCost items. action: Modify replaces the list, Add updates the conversion making the same result or adds one, and Remove takes out the ones making each result listed.

[b]Yaml example:[/b]
[code]
conversionModifications:
  ObliteratorScrap:
    action: Add
    prefab: incinerator
    incineratorConversions:
    - result: Iron
      resultAmount: 1
      priority: 10                         # <- Made before the conversions with a lower priority
      anyOneIngredient: false              # <- true when any one of the ingredients is enough
      ingredients:
      - prefab: IronScrap
        amount: 5
[/code]


[size=4][b]Items[/b][/size]

\BepInEx\config\RecipeManager\Items.yaml does two things:

[list]
[*]action: Add creates a brand new item by cloning an existing one. The clone keeps the model and every stat of the item it was copied from, and anything you set in the config is applied on top.[/*]
[*]action: Modify changes an item that already exists, vanilla weapons and armor included. Only the fields you set are changed, and taking a field back out of the yaml puts the original value back.[/*]
[/list]

Adding or removing an item needs a game restart (or a trip back to the main menu). This is a hard requirement of how Valheim identifies items on the network, not something the mod can work around. Everything else, including every modification of an existing item, works live with RM_Items_Reload.

[size=3][b]Item commands[/b][/size]

RM_Items_PrintAll - Writes the items in the object DB, with the stats they currently have, to ObjectDBItems.yaml. Copy an entry out of it to see what the numbers should look like. It is a big file, so RM_Items_PrintAll sword only writes the items whose prefab name contains "sword" (upper or lower case).

RM_Items_Reload - Reads the item files again. Stats, names, icons and textures are re-applied straight away, and the item definitions are sent to connected players. Adding or removing an item needs a restart.

RM_Textures_Dump &lt;prefabName&gt; - Writes a prefab's icons and material textures to \BepInEx\config\RecipeManager\Textures\&lt;prefabName&gt;\ as PNGs, along with a _dump_info.txt describing each file. Prefab names are case sensitive. See Custom icons and textures below for what to do with them.

[size=3][b]Adding a new item[/b][/size]

Add a "Honey bread" food, made from bread and honey at the cauldron
[code]
itemAdditions:
  HoneyBread:
    action: Add
    prefab: HoneyBread
    clonedFrom: Bread
    name: Honey Bread
    description: Sweet, sticky and filling.
    weight: 0.5
    maxStackSize: 20
    value: 12
    teleportable: true
    food:
      health: 50
      stamina: 60
      eitr: 0
      duration: 1800
      regen: 2
      eatAnimTime: 1
      isDrink: false
      consumeStatusEffect: ''
    recipe:
      craftedAt: piece_cauldron
      repairAt: ''
      minStationLevel: 1
      craftAmount: 1
      anyOneResource: false
      ingredients:
      - prefab: Bread
        craftCost: 1
        upgradeCost: 0
      - prefab: Honey
        craftCost: 2
        upgradeCost: 0
[/code]

Item recipe ingredients also take upgraderResource, recover, anyOneExtraAmount and upgradeCosts, and the recipe takes maxStationLevel, see the optional recipe fields. A cloned weapon or armor needs an upgraderResource idol (Upgrader0Weapon..Upgrader7Armor) to be upgradeable at the Forge of Potential.

New items are visible to the rest of the mod by their prefab name, so you can also refer to them from Recipes.yaml or Conversions.yaml.

[size=3][b]Modifying an existing item[/b][/size]

Set action: Modify and point prefab at the item you want to change. There is no clonedFrom, nothing new is created, and the change reaches every copy of that item that already exists, including the ones sitting in chests and inventories.

Anything you leave out keeps its current value, so a modification only has to list what actually differs. The reverse is true too: deleting a line and running RM_Items_Reload restores that item's original value rather than leaving the last number you set.

A recipe: block only applies to an item you add. An existing item's recipe is changed in Recipes.yaml.

Make the iron sword hit harder and set things on fire
[code]
itemAdditions:
  BurningIronSword:
    action: Modify
    prefab: SwordIron
    weapon:
      damage:
        slash: 60
        fire: 20
      damagePerLevel:
        slash: 5
      backstabBonus: 4
    statusEffects:
      onHit: Burning
      onHitChance: 0.5
[/code]

Give the troll armor set fire resistance and a real set bonus
[code]
itemAdditions:
  FireproofTrollChest:
    action: Modify
    prefab: ArmorTrollLeatherChest
    armor:
      armor: 20
      armorPerLevel: 4
      movementModifier: 0.05
    resistances:
    - type: Fire
      modifier: VeryResistant
    - type: Frost
      modifier: Resistant
    setBonus:
      name: TrollLeather
      size: 3
      statusEffect: SetEffect_TrollArmor
[/code]

[size=3][b]Weapon, armor and set stats[/b][/size]

These blocks work exactly the same on an item you added and on one you are modifying, so a cloned sword can be given its own damage profile the same way a vanilla one can.

weapon: damage and tool stats.
[list]
[*]damage - base damage. Takes any of damage, blunt, slash, pierce, chop, pickaxe, fire, frost, lightning, poison, spirit, nonPlayer (the game's NonPlayer damage type). Types you leave out are not touched.[/*]
[*]damagePerLevel - damage added by each upgrade level, same fields as damage.[/*]
[*]attackForce - knockback.[/*]
[*]backstabBonus - sneak attack damage multiplier.[/*]
[*]toolTier - what a tool is able to harvest. 0 is wood and stone, 2 is black metal.[/*]
[*]skill - the skill the weapon trains and uses, such as Swords, Bows or Crossbows. A skill another mod adds works too.[/*]
[*]ammoType - the kind of ammo a bow or crossbow fires, or the kind an ammo item is: $ammo_arrows or $ammo_bolts for the game's own.[/*]
[*]dodgeable / blockable - whether its hits can be dodged, and blocked.[/*]
[*]spawnOnHit / spawnOnHitTerrain - a prefab spawned where it hits something, and where it hits the ground. None for nothing.[/*]
[*]primaryAttack / secondaryAttack - the weapon's two attacks, see More item stats below.[/*]
[/list]

armor: armor, shield and equip stats.
[list]
[*]armor / armorPerLevel - armor value, and how much each upgrade level adds.[/*]
[*]blockPower / blockPowerPerLevel - how much damage a block absorbs.[/*]
[*]deflectionForce / deflectionForcePerLevel - how far a blocked attacker is pushed back.[/*]
[*]movementModifier - speed change while equipped, as a fraction. -0.05 is 5% slower.[/*]
[*]eitrRegenModifier - eitr regeneration change while equipped.[/*]
[*]heatResistanceModifier - protection from the Ashlands heat while equipped, as a fraction.[/*]
[*]timedBlockBonus - how much more a parry absorbs than a plain block, as a multiplier.[/*]
[*]perfectBlockStaminaRegen - stamina given back by a parry.[/*]
[*]buildBlockCharges / maxBlockCharges / blockChargeDecayTime - whether blocking builds up charges like the bucklers do, how many, and how many seconds one lasts.[/*]
[/list]

resistances: a list of type and modifier pairs. This replaces the item's whole resistance list, so include every resistance you want it to end up with, and use resistances: [] to remove them all.
[list]
[*]type is one of Blunt, Slash, Pierce, Chop, Pickaxe, Fire, Frost, Lightning, Poison, Spirit, NonPlayer.[/*]
[*]modifier is one of Normal, SlightlyResistant, Resistant, VeryResistant, SlightlyWeak, Weak, VeryWeak, Immune, Ignore.[/*]
[/list]

statusEffects: effects the item provides.
[list]
[*]equipped - applied for as long as the item is equipped.[/*]
[*]onHit - applied to whatever the weapon hits.[/*]
[*]onHitChance - chance for onHit to land, from 0 to 1.[/*]
[*]perfectBlock - applied to the wearer when they parry.[/*]
[*]fullAdrenaline - applied when the wearer's adrenaline fills up.[/*]
[/list]

setBonus: the armor set the item belongs to.
[list]
[*]name - grouping key. Every piece of the same set has to use the same value.[/*]
[*]size - how many pieces have to be worn. The game ignores set bonuses with a size of 1 or less.[/*]
[*]statusEffect - the effect that wearing the full set grants.[/*]
[/list]

Status effects are referenced by name, an effect added in StatusEffects.yaml included. SetEffect_TrollArmor, Burning and Frost are examples of names that work. To empty a status effect slot rather than set one, use the value None.

[size=3][b]More item stats[/b][/size]

RM_Items_PrintAll writes every one of these that an item uses, so the dump of an item shows what its numbers are. Like everything else on an item, a field left out keeps the item's own value.

Top level:
[list]
[*]equipDuration - seconds it takes to put the item on.[/*]
[*]questItem - kept on death and cannot be moved into a chest, like the game's keys.[/*]
[*]appendToolTip - another item whose tooltip is shown below this one's, the way the feasts show their food. None removes it.[/*]
[/list]

quality: upgrade levels, and the Forge of Potential.
[list]
[*]maxLevel - the highest upgrade level. 1 means the item cannot be upgraded at all.[/*]
[*]weightPerLevel - weight added by each upgrade level, as a fraction of the weight.[/*]
[*]upgradeChance / breakChance - at the Forge of Potential, the chance an upgrade works, and the chance a failed one breaks the item, 0 to 1.[/*]
[*]upgradeSteps - levels a successful upgrade at the Forge of Potential adds.[/*]
[*]breakRefund - how much of the ingredients a broken item gives back, 0 to 1.[/*]
[/list]

durability:
[list]
[*]enabled - whether the item wears out at all.[/*]
[*]max / perLevel - durability, and durability added by each upgrade level.[/*]
[*]drainPerUse / drainPerSecond - durability lost each use, and each second while in use like a torch.[/*]
[*]repairable - whether it can be repaired.[/*]
[*]destroyWhenBroken - for an item that wears out while equipped, like a torch, whether it is destroyed when it runs out. Weapons and armor that run out just stop working.[/*]
[/list]

staminaModifiers: stamina cost changes while equipped, as fractions. -0.1 is 10% cheaper. Takes homeItems (tools and building), jump, attack, block, dodge, swim, sneak and run.

adrenaline: max is how much adrenaline the wearer can build up, block and perfectBlock how much a block and a parry give.

weapon.primaryAttack and weapon.secondaryAttack: a weapon's two attacks. Only numbers and switches that do not depend on the attack's animation can be changed, so a change can never leave a weapon stuck mid-swing. A change reaches the next swing.
[list]
[*]stamina / eitr / health / healthPercentage - what the attack costs. healthPercentage is a percentage of the current health.[/*]
[*]adrenaline / useAdrenaline - adrenaline gained for each hit, and each time the attack is used whether it hits or not.[/*]
[*]speedFactor / rotationSpeedFactor - movement and turning speed while attacking, as a fraction of normal.[/*]
[*]damageMultiplier / forceMultiplier / staggerMultiplier - multipliers on the weapon's damage, knockback and stagger for this attack.[/*]
[*]lastChainDamageMultiplier - damage multiplier on the last hit of a combo.[/*]
[*]damageMultiplierPerMissingHealth / damageMultiplierByHealthMissing / staminaReturnPerMissingHealth - extra damage, and stamina back, as the attacker's health drops, like the berserker axes.[/*]
[*]range / height / angle / rayWidth - the reach and shape of a swing. angle is in degrees.[/*]
[*]multiHit / lowerDamagePerHit - whether a swing can hit several targets, and whether it shares its damage out between them.[/*]
[*]hitTerrain / hitFriendly / hitThroughWalls - what a swing can hit.[/*]
[*]recoilPushback / selfDamage - how far the attacker is pushed back, and damage the attacker takes.[/*]
[*]consumeItem - whether the attack uses up the item, like throwing a bomb.[/*]
[*]raiseSkillAmount - how much skill a hit gives.[/*]
[*]startNoise / hitNoise - noise creatures can hear when the attack starts and when it hits.[/*]
[*]spawnOnTrigger / spawnOnHit / spawnOnHitChance - a prefab spawned when the attack is used, one spawned where it hits, and the chance of the second, which starts at 0 so set it too. None for nothing.[/*]
[*]projectile - prefab, velocity, velocityMin, accuracy, accuracyMin, skillAccuracy, count, bursts, burstInterval and launchAngle. Velocity and accuracy go from the Min value to the full one as a bow is drawn, accuracy is a spread in degrees. On a bow or crossbow the ammo's numbers are added to these, and the ammo's projectile is fired.[/*]
[*]reload - time, staminaDrain and eitrDrain of a crossbow's reload. The game only reads the primary attack's.[/*]
[*]draw - durationMin (seconds to fully draw with no skill), staminaDrain and eitrDrain of a bow's draw. The game only reads the primary attack's.[/*]
[/list]

Prefabs, like a projectile or spawnOnHit, are named by their prefab name, and None empties the slot. A prefab another mod adds can be used, it is looked up once the world has loaded.

Make the arbalest reload and swap faster, and make running and jumping cheaper while it is held
[code]
itemAdditions:
  QuickArbalest:
    action: Modify
    prefab: CrossbowArbalest
    equipDuration: 0.5
    weapon:
      primaryAttack:
        reload:
          time: 2
    staminaModifiers:
      run: -0.1
      jump: -0.1
[/code]

Stop the iron sword being upgraded, and make it never wear out
[code]
itemAdditions:
  PlainIronSword:
    action: Modify
    prefab: SwordIron
    quality:
      maxLevel: 1
    durability:
      enabled: false
[/code]

[size=3][b]Custom icons and textures[/b][/size]

Icons and textures are read from \BepInEx\config\RecipeManager\Textures\&lt;prefab&gt;\ where &lt;prefab&gt; is the prefab value of your new item. Any file that is present replaces that texture, and anything missing keeps whatever the cloned item had.

The workflow is:
[list]
[*]Run RM_Textures_Dump Bread in the console. This writes Bread's icon and material textures to \BepInEx\config\RecipeManager\Textures\Bread\ along with a _dump_info.txt describing what each file is.[/*]
[*]Copy that whole folder and rename the copy to your new item's prefab name, for example Textures\HoneyBread\[/*]
[*]Repaint the PNGs you care about and delete the ones you did not change.[/*]
[*]Restart the game, or run RM_Items_Reload.[/*]
[/list]

The file names are what tie a PNG to a texture slot, so do not rename them:
[list]
[*]icon_0.png is the inventory icon (icon.png also works if there is only one). This is the one most people want.[/*]
[*]&lt;renderer&gt;__&lt;material&gt;__&lt;slot&gt;.png is a model texture, for example attach__0___MainTex.png. _MainTex is the colour texture, _BumpMap is the normal map, _MetallicGlossMap and _EmissionMap are the metallic and glow maps.[/*]
[/list]

Textures are only ever applied to a copy of the material, so repainting your new item never changes the vanilla item it was cloned from.

On a server, item definitions are synced to clients but the PNGs are not. A client that does not have the image files still gets the item with the correct stats and recipe, it just shows the original artwork.


[size=4][b]Status effects[/b][/size]

\BepInEx\config\RecipeManager\StatusEffects.yaml adds new status effects and changes the ones that exist. Items use them by their prefab name, like any other status effect.

[list]
[*]action: Add makes a new effect. With clonedFrom it is a copy of that effect, without it the effect starts with no stats at all.[/*]
[*]action: Modify changes an effect that already exists. Everything it leaves out keeps the effect's own value.[/*]
[/list]

A change reaches an effect the next time it is given: a set bonus the next time the set is put on, a food effect the next time it is eaten. An effect a player already has keeps its old values until it runs out.

maxHealth, maxStamina and maxEitr add to the player's maximum while the effect lasts, and are listed under the effect's tooltip. They use a game patch.

Only the stats the game gives its ordinary effects can be set. Shield, poison and frost effects keep their own settings, though an entry can still change their name, texts, timings and icon. An icon at \BepInEx\config\RecipeManager\Textures\&lt;prefab&gt;\icon.png replaces the effect's icon.


[size=3][b]Status effect commands[/b][/size]

RM_StatusEffects_PrintAll - Writes every status effect in the game, with the stats it uses, to ObjectDBStatusEffects.yaml. RM_StatusEffects_PrintAll troll only writes the effects whose name contains "troll".

RM_StatusEffects_Reload - Reads the status effect files again and applies them, then brings the items that use them in line. The result is sent to connected players.

[b]Yaml example:[/b]
[code]
statusEffects:
  TrollSetStrength:
    action: Add
    prefab: SetEffect_TrollStrength        # <- The new effect's name, which has to be unique
    clonedFrom: SetEffect_TrollArmor
    name: Troll strength
    tooltip: Hits harder and carries more.
    carryWeight: 50
    maxHealth: 25                          # <- A game patch
    damage:
      skill: All                           # <- The weapon skill whose attacks it boosts, All for every attack
      multiplier: 1.1
    resistances:
    - type: Frost
      modifier: Resistant
  LongerRested:
    action: Modify
    prefab: Rested
    health:
      regenMultiplier: 1.6
[/code]

To give the troll armor set the new effect, point the set bonus at it in Items.yaml:

[code]
itemAdditions:
  TrollChest:
    action: Modify
    prefab: ArmorTrollLeatherChest
    setBonus:
      statusEffect: SetEffect_TrollStrength
[/code]

[b]Every field:[/b]

Most numbers are fractions, where 0.1 is 10% more and -0.1 is 10% less. The multipliers are the exception, where 1 is normal.

[code]
    name: Troll strength                   # <- Start it with '$' to use your own localization token
    tooltip: Hits harder.
    category: strength                     # <- Effects with the same category replace each other
    duration: 300                          # <- Seconds, 0 lasts for as long as something keeps it on
    cooldown: 0                            # <- Guardian powers, seconds before it can be used again
    hidden: false                          # <- Kept off the list of effects on screen
    flashIcon: false
    cooldownIcon: false                    # <- Shows the time left on the icon
    attributes: ColdResistance             # <- ColdResistance, DoubleImpactDamage, SailingPower or TamingBoost
    activationAnimation: gpower            # <- Guardian powers, the animation played
    messages:
      start: You feel strong
      startType: Center                    # <- TopLeft or Center, also stopType and repeatType
      stop: ''
      repeat: ''
      repeatInterval: 0
    health: {upFront: 0, perTick: 0, tickInterval: 0, minHealthPercentage: 0, overTime: 0, overTimeDuration: 0, overTimeInterval: 5, regenMultiplier: 1}
    stamina: {upFront: 0, overTime: 0, overTimeDuration: 0, overTimeIsFraction: false, drainPerSecond: 0, regenMultiplier: 1, runDrain: 0}
    staminaUse: {homeItems: 0, jump: 0, attack: 0, block: 0, blockFlat: 0, dodge: 0, swim: 0, sneak: 0, run: 0}
    eitr: {upFront: 0, overTime: 0, overTimeDuration: 0, regenMultiplier: 1}
    adrenaline: {upFront: 0, modifier: 0}
    armor: {add: 0, multiplier: 0}
    stagger: {modifier: 0, timedBlockBonus: 0}
    damage: {skill: All, multiplier: 1, percentByType: {fire: 0.1}}
    resistances: []
    skills: {raise: Swords, raiseModifier: 0, level: Swords, levelModifier: 0, level2: Bows, level2Modifier: 0}
    movement: {speed: 0, swimSpeed: 0, jump: {x: 0, y: 0, z: 0}, maxFallSpeed: 0, fallDamage: 0, wind: 0, windRunStamina: 0}
    noise: 0
    stealth: 0
    carryWeight: 0
    maxHealth: 0
    maxStamina: 0
    maxEitr: 0
[/code]


[size=4][b]Importing from WackysDatabase[/b][/size]

RM_Import_WackyDB converts WackysDatabase's recipe, item, piece and status effect files into RecipeManager files. Run it in a world, since it looks stations and items up in the game:
[list=1]
[*]Load into a world with RecipeManager installed. Disabling WackysDatabase first gives the most accurate report, since its own changes are otherwise part of what the import compares against.[/*]
[*]Run RM_Import_WackyDB. It reads \BepInEx\config\wackysDatabase\ and every folder in it. To read somewhere else, give the folder: RM_Import_WackyDB wackyDatabase-BulkYML, relative to \BepInEx\config\, or a full path.[/*]
[*]Everything is written to \BepInEx\config\RecipeManager\WackyImport\, which is not loaded: Wacky_Recipes.yaml, Wacky_Items.yaml, Wacky_Pieces.yaml, Wacky_Conversions.yaml, Wacky_StatusEffects.yaml, a Textures folder for icons, and ImportReport.txt.[/*]
[*]Read ImportReport.txt. For every WackysDatabase file it lists what the file became, every setting that was carried across, every setting that was left out and why, and anything to check, such as an item that does not exist in this game.[/*]
[*]When it looks right, move the Wacky_*.yaml files into \BepInEx\config\RecipeManager\, and the contents of its Textures folder into \BepInEx\config\RecipeManager\Textures\. Remove WackysDatabase's files, or WackysDatabase itself, so both mods do not change the same things.[/*]
[/list]

What carries across today:
[list]
[*]Recipes: station, repair station, station level, amount, sort order, upgrade-only, any-one ingredient, disabling, ingredients, WackysDatabase's separate upgrade costs, including costs for single quality levels, and station level caps. Extra recipes WackysDatabase adds for an item become an Add, and switching off upgrades sets the item's highest upgrade level to 1.[/*]
[*]Items: added (cloned) items, name, description, weight, stack size, value, teleporting, equip time, quest items, upgrade levels and the Forge of Potential, durability, damage, skill, ammo, armor, shield and parry stats, stamina and other stat modifiers, adrenaline, food, status effects, set bonus, resistances, icons, and the numbers of both attacks, including their projectiles, reload and draw.[/*]
[*]Pieces: added (copied) pieces, moving pieces between tools, cost, station, station level, build tab and tags, comfort, name, description, disabling, the placement rules, blocking pieces, icons, health and resistances, and crafting station, station upgrade, container and fire settings. Smelter, cooking station, fermenter and obliterator settings become conversion entries.[/*]
[*]Status effects: added (copied) and changed effects, their name, texts, timings, icon and stats, and the health, stamina and eitr they add.[/*]
[/list]

What does not: attack speed, a status effect for one attack, extra set bonuses, the Water damage type, status effect reapply cooldowns and effects that start another when they end need game patches RecipeManager does not have. Shield, poison and frost effect settings, an attack's type, animations and effects, parts a piece does not have (such as turning a piece into a crafting station), and beehives, sap collectors, plants, portals, ships, shield generators and battering rams are left alone. Creatures, pickables, trees, projectiles, area effects, materials and 3D model items are not part of RecipeManager. The report names every one of these settings in every file, so nothing is dropped silently.


[size=4][b]FAQ[/b][/size]
[list]
[*][b]Q:[/b] Why is my recipe not showing up?
[b]A:[/b] Ensure that the recipe uses the action 'Modify' or 'Add'. An entry with 'Enable' only switches a recipe on, and does nothing to one that is already on.[/*]
[*][b]Q:[/b] Since updating to 0.11 my modified recipe is crafted at a station, or my modified piece costs something
[b]A:[/b] A Modify now keeps everything it leaves out. Write craftedAt: none for a handcrafted recipe, and requirements: [] for a free piece. See Upgrading to 0.11 above.[/*]
[*][b]Q:[/b] I edited an armor or weapon recipe and the idol became part of the crafting cost, or the item disappeared from the Forge of Potential
[b]A:[/b] Update to 0.6.5 or later. The idol (Upgrader1Armor and the like) is an upgrader resource, which only the Forge of Potential charges and which is what puts the item on the Forge. Entries written by older versions are recognised automatically, and a fresh RM_Recipes_PrintAll marks the idol with upgraderResource: true. The same update keeps the Deep North gold gear upgrade-only and keeps fishing bait in its place in the crafting list.[/*]
[*][b]Q:[/b] My yaml has an error about the refund key
[b]A:[/b] The refund key was removed in 0.4.1. If you need to remove the key from an existing yaml you can use the following regex ( +refund: \w+\n?) and replace with nothing. This can be done from most text editors find and replace.[/*]
[*][b]Q:[/b] My new item has the same food bar as the item I cloned it from
[b]A:[/b] Set name on the item addition. The display name is what the game uses to tell foods apart, so without one your item shares a food slot with its original.[/*]
[*][b]Q:[/b] I changed a status effect and nothing happened
[b]A:[/b] A change reaches an effect the next time it is given. Take the item off and put it on again, or wait for the effect to run out.[/*]
[*][b]Q:[/b] Why does adding an item need a restart?
[b]A:[/b] An item's prefab name is its network identity, and Valheim builds that lookup once when the world loads. Items therefore have to exist before then. Editing an item you already have does not need a restart, only adding or removing one does.[/*]
[/list]


[size=4][b]Planned Features[/b][/size]
[list]
[*]More recipe validation[/*]
[*][s]More item stats: durability, max quality, attack costs and the like[/s] (added in 0.11.0)[/*]
[*][s]Upgrade costs for each quality level, and capping the station level a recipe needs[/s] (added in 0.11.0)[/*]
[*][s]Custom status effects[/s] (added in 0.11.0)[/*]
[*][s]Adding pieces, moving them between tools, and changing their health, containers, fires and stations[/s] (added in 0.11.0)[/*]
[/list]


[size=5][b]Installation (manual)[/b][/size]
Ensure the downloaded .dll is placed inside your /bepinex/plugins folder.

Please note this mod does nothing until configured.

[line]

If you enjoy my mods donations are appreciated. My mods will never be paid and donations are not required.

[url=https://ko-fi.com/midnightsfx][img]https://raster.shields.io/badge/Ko--fi-Support%20me-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white[/img][/url]

RecipeManager

This is a lightweight recipe modification tool. You can define recipes to Add, Modify, Delete etc. It does not use any patches, and does not run constantly.

If you want to do more than modify recipes and pieces check out [url=https://thunderstore.io/c/valheim/p/WackyMole/WackysDatabase/]WackysDB[/url]

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
[/list]

Recipes can be manipulated and added through yaml. All of the existing recipes can also be dumped to a file, in the same format to help you find and understand existing recipes.

Its recommended to use a syntax highlighting yaml editor when modifying recipes to help ensure that your yaml is valid. [url=https://yamlchecker.com/]yamlChecker[/url] is a free online linter you can use to validate also.

Recipe modifications are stored at \BepInEx\config\RecipeManager\Recipes.yaml. This file will be generated on the first run if it does not already exist.

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
    recipeName: Recipe_ArrowWood         # <- optional, specifying the recipe name allows matching and mutating multiple recipes targeting the same prefab
    craftedAt: Workbench                 # <- The crafting station that should craft this recipe, leave it empty or invalid for handcrafting
    minStationLevel: 2                   # <- This is the required crafting station level for discovery AND crafting
    recipe:                              # <- When performing [Modify] or [Add] you should define a recipe
      anyOneResource: false              # <- This makes the recipe only require one ingrediant, first from the top will be used.
      ingredients:                       # <- Ingrediants in the recipe, is an array
        - prefab: Wood                   # <- Prefab that this ingrediant requires
          craftCost: 2                   # <- The amount of this ingrediant it takes to craft the recipe
          upgradeCost: 0                 # <- The amount of this ingrediant it takes to upgrade the item
        - prefab: Feathers
          craftCost: 2
          upgradeCost: 0
  DeleteTrollHideArmorRecipe:
    action: Delete
    prefab: CapeTrollHide
  ModifyTrollHideChestRecipe:
    action: Modify
    prefab: ArmorTrollLeatherChest
    craftedAt: Workbench
    minStationLevel: 1
    recipe:
      anyOneResource: false
      ingredients:
        - prefab: TrollHide
          craftCost: 4
          upgradeCost: 2
[/code]


[size=4][b]Commands[/b][/size]
This mod adds two new commands which can be used to speed up recipe modification.

RM_Recipes_Reload - This reloads all recipe modifications that are found.

RM_Recipes_PrintAll - This prints all recipes currently stored in the object DB (including modifications), this file is ignored.

RM_Pieces_Reload - This reloads all piece modifications that are found.

RM_Pieces_PrintAll - This prints all pieces to a debug file (including modifications), this file is ignored.

Commands require [url=https://valheim.fandom.com/wiki/Developer_console]enabling the in-game console[/url].

If you are using Thunderstore Mod Manager or R2 Modmanager, you can enabled the console by selecting your valheim profile, going to settings (on the far left), debugging tab, set launch parameters and add --console

It is not recommended to use Vortex mod manager, however using vortex hardlink or copy deployments you can launch from steam using BepinEx with a --console launch parameter. To do so right click on the game in your library, manage, general, at the bottom add --console to the launch options.


[size=4][b]Multiple Config files[/b][/size]

All files which contain Recipes.yaml in \BepInEx\config\RecipeManager\ will be considered recipe files (except ObjectDBRecipes.yaml).
For example this means that:

[list]
[*]Forge_Recipes.yaml is valid[/*]
[*]SoupStationStuff.yaml is not valid and will not be loaded but SoupStationStuffRecipes.yaml is.[/*]
[/list]

All files which contain Pieces.yaml in \BepInEx\config\RecipeManager\ will be considered piece modifications and loaded.
For example this means that:

[list]
[*]CultivatorPieces.yaml is valid[/*]
[*]Cultivator.yaml is not valid and will not be loaded[/*]
[/list]


[size=4][b]Recipe Examples[/b][/size]

The mod ships with a default example recipe file which will provide you similar explanations to what is listed above. You can find the recipe file after starting up your game with the mod installed in you BepInEx\config\RecipeManager folder.

R2modman or Thunderstore mod manager will also detect this file as an editable config file and it can be edited or opened directly from the "Edit Config" menu, search for Recipes.yaml.

The only file that is will be used to load recipes is Recipes.yaml other yaml files in that directory will not be loaded.

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

Increase the bronze yield (replace [url=https://thunderstore.io/c/valheim/p/Digitalroot/Triple_Bronze_JVL/]triple bronze[/url])
[code]
recipeModifications:
  Recipe_Bronze:
    action: Modify
    prefab: Bronze
    recipeName: Recipe_Bronze
    craftedAt: forge
    minStationLevel: 1
    craftAmount: 3
    recipe:
      anyOneResource: false
      ingredients:
      - prefab: Copper
        craftCost: 2
        upgradeCost: 1
      - prefab: Tin
        craftCost: 1
        upgradeCost: 1
  Recipe_Bronze5:
    action: Modify
    prefab: Bronze
    recipeName: Recipe_Bronze5
    craftedAt: forge
    minStationLevel: 1
    craftAmount: 15
    recipe:
      anyOneResource: false
      ingredients:
      - prefab: Copper
        craftCost: 10
        upgradeCost: 1
      - prefab: Tin
        craftCost: 5
        upgradeCost: 1
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


[size=4][b]Piece Examples[/b][/size]

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


[size=4][b]Conversion Examples[/b][/size]

Makes the charcoal kiln take wood, finewood and corewood
[code]
conversionModifications:
  charcoal_kiln:
    action: modify
    prefab: charcoal_kiln
    conversions:
    - fromPrefab: Wood
      toPrefab: Coal
      amount: 2
    - fromPrefab: FineWood
      toPrefab: Coal
      amount: 2
    - fromPrefab: RoundLog
      toPrefab: Coal
      amount: 2
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
      amount: 1
    - fromPrefab: BlackMetalScrap
      toPrefab: BlackMetal
      amount: 1
    - fromPrefab: CopperOre
      toPrefab: Copper
      amount: 1
    - fromPrefab: IronOre
      toPrefab: Iron
      amount: 1
    - fromPrefab: IronScrap
      toPrefab: Iron
      amount: 1
    - fromPrefab: BronzeScrap
      toPrefab: Bronze
      amount: 1
    - fromPrefab: TinOre
      toPrefab: Tin
      amount: 1
    - fromPrefab: SilverOre
      toPrefab: Silver
      amount: 1
    - fromPrefab: CopperScrap
      toPrefab: Copper
      amount: 1
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


[size=4][b]FAQ[/b][/size]
[list]
[*][b]Q:[/b] Why is my recipe not showing up?
[b]A:[/b] Ensure that the recipe uses the action 'Modify' or 'Add', by default recipes dumped from the object DB will not, they will use Enable. Which if the recipe already exists and is not disabled, does nothing.[/*]
[*][b]Q:[/b] My yaml has an error about the refund key
[b]A:[/b] The refund key was removed in 0.4.1. If you need to remove the key from an existing yaml you can use the following regex ( +refund: \w+\n?) and replace with nothing. This can be done from most text editors find and replace.[/*]
[/list]


[size=4][b]Planned Features[/b][/size]
[list]
[*]More recipe validation[/*]
[*]Conversion modifications (like ores for ingots)[/*]
[*]Export to WackyDB[/*]
[/list]


[size=5][b]Installation (manual)[/b][/size]
Ensure the downloaded .dll is placed inside your /bepinex/plugins folder.

Please note this mod does nothing until configured.

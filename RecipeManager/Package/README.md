# RecipeManager
This is a lightweight recipe modification tool. You can define recipes to Add, Modify, Delete etc. It does not use any patches, and does not run constantly. 

If you want to do more than modify recipes and pieces check out [WackysDB](https://thunderstore.io/c/valheim/p/WackyMole/WackysDatabase/)

In Valheim a recipe is something that is crafting at a crafting station, or cooking station etc. For example, to make wood arrows you use a crafting recipe from the workbench.
Building pieces (built with the hammer) are not recipes, furnace conversions (1 ore for 1 ingot etc) are not recipes.

Pieces are almost anything that is placed. Everything built with the hammer, plants placed with the cultivator, placable food etc.

## Features
- Add recipes
- Modify recipes
- Delete recipes
- Disable Pieces
- Enable Pieces
- Modify Pieces
- Add new items by cloning an existing item, with their own name, stats, icon and textures
- Modify the stats of items that already exist, including vanilla weapons and armor
- Dump any prefab's icons and textures to disk so you can repaint them

Recipes can be manipulated and added through yaml. All of the existing recipes can also be dumped to a file, in the same format to help you find and understand existing recipes.

Its recommended to use a syntax highlighting yaml editor when modifying recipes to help ensure that your yaml is valid. [yamlChecker](https://yamlchecker.com/) is a free online linter you can use to validate also.

Recipe modifications are stored at `\BepInEx\config\RecipeManager\Recipes.yaml`. This file will be generated on the first run if it does not already exist.

<details>
  <summary>Yaml configuration example</summary>

```yaml
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
```
</details>

### Commands
This mod adds two new commands which can be used to speed up recipe modification.

`RM_Recipes_Reload` - This reloads all recipe modifications that are found.

`RM_Recipes_PrintAll` - This prints all recipes currently stored in the object DB (including modifications), this file is ignored.

`RM_Pieces_Reload` - This reloads all piece modifications that are found.

`RM_Pieces_PrintAll` - This prints all pieces to a debug file (including modifications), this file is ignored.

`RM_Conversion_Reload` - This reloads all conversion modifications that are found.

`RM_Conversion_PrintAll` - This prints all conversions to a debug file, this file is ignored.

`RM_Items_Reload` - This reloads item additions and modifications. Stats, names, icons and textures are re-applied straight away; adding or removing an item needs a restart.

`RM_Items_PrintAll` - This prints the items in the object DB, with the stats they currently have, to a reference file, this file is ignored. `RM_Items_PrintAll <text>` only prints the items whose prefab name contains `<text>`.

`RM_Textures_Dump <prefabName>` - This dumps a prefab's icons and material textures to `\BepInEx\config\RecipeManager\Textures\<prefabName>\` as PNGs.

Commands require [enabling the in-game console](https://valheim.fandom.com/wiki/Developer_console).

If you are using Thunderstore Mod Manager or R2 Modmanager, you can enabled the console by selecting your valheim profile, going to settings (on the far left), debugging tab, set launch parameters and add `--console`

It is not recommended to use Vortex mod manager, however using vortex hardlink or copy deployments you can launch from steam using BepinEx with a `--console` launch parameter. To do so right click on the game in your library, manage, general, at the bottom add `--console` to the launch options.

### Multiple Config files

All files which contain `Recipes.yaml` in `\BepInEx\config\RecipeManager\` will be considered recipe files (except `ObjectDBRecipes.yaml`).
For example this means that:

- `Forge_Recipes.yaml` is valid
- `SoupStationStuff.yaml` is not valid and will not be loaded but `SoupStationStuffRecipes.yaml` is.

All files which contain `Pieces.yaml` in `\BepInEx\config\RecipeManager\` will be considered piece modifications and loaded.
For example this means that:

- `CultivatorPieces.yaml` is valid
- `Cultivator.yaml` is not valid and will not be loaded

All files which contain `Items.yaml` in `\BepInEx\config\RecipeManager\` will be considered item additions and loaded (except `ObjectDBItems.yaml`).
For example this means that:

- `Food_Items.yaml` is valid
- `Food.yaml` is not valid and will not be loaded

### Recipe Examples

The mod ships with a default example recipe file which will provide you similar explanations to what is listed above. You can find the recipe file after starting up your game with the mod installed in you `BepInEx\config\RecipeManager` folder.

R2modman or Thunderstore mod manager will also detect this file as an editable config file and it can be edited or opened directly from the "Edit Config" menu, search for `Recipes.yaml`.

The only file that is will be used to load recipes is `Recipes.yaml` other yaml files in that directory will not be loaded.

Add a craftable chain (replace [Chain-Manager](https://thunderstore.io/c/valheim/p/Digitalroot/Chain_Manager/))
<details>
  <summary>Yaml example</summary>

```yaml
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
```
</details>

Disable recipes you don't want from vanilla or other mods (disable recipes from [Southsil Armors](https://thunderstore.io/c/valheim/p/southsil/SouthsilArmor/))
<details>
  <summary>Yaml example</summary>

```yaml
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
```
</details>

Increase the bronze yield (replace [triple bronze](https://thunderstore.io/c/valheim/p/Digitalroot/Triple_Bronze_JVL/))
<details>
  <summary>Yaml example</summary>

```yaml
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
```
</details>

Move a recipe from one crafting station to another (Custom mead from [Honey+](https://thunderstore.io/c/valheim/p/OhhLoz/HoneyPlus/))
<details>
  <summary>Yaml example</summary>

```yaml
recipeModifications:
  custom_item_meadbase_damage:
    action: Modify
    prefab: HoneyMeadBaseDamage
    recipeName: $custom_item_meadbase_damage
    craftedAt: piece_cauldron
```
</details>

### Piece Examples

Enables all of the holiday building pieces
<details>
  <summary>Yaml example</summary>

```yaml
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
```
</details>

Makes the kiln cost 100 stone, but only 1 surtling core
<details>
  <summary>Yaml example</summary>

```yaml
pieceModifications:
  expensive_kiln:
    action: Modify
    prefab: charcoal_kiln
    requirements:
    - prefab: Stone
      amount: 100
    - prefab: SurtlingCore
      amount: 1
```
</details>

Removes the stonecutter as a buildable piece
<details>
  <summary>Yaml example</summary>

```yaml
pieceModifications:
  remove_stonecutter:
    action: Disable
    prefab: piece_stonecutter
```
</details>

### Conversion Examples
Any field left out of a conversion modification keeps the station's own value. A few values need care:
- `maxFuel: 0` makes a smelter type station need no fuel at all.
- `fuelPerProduct: 0` means fuel still has to be loaded, but it is never used up.
- `maxOres: 0` makes the station accept no items to convert.
- Smelter type stations produce at most one item per second, and burn `fuelPerProduct / conversionTime` fuel each second. A `conversionTime` below 1 would only burn more fuel per item, so it is raised to 1. A `conversionTime` of 0 is ignored.

Makes the charcoal kiln take wood, finewood and corewood
<details>
  <summary>Yaml example</summary>

```yaml
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
```
</details>

Makes the blast furnance take all ores, increases its holding size, and changes the time it takes to smelt an item.
<details>
  <summary>Yaml example</summary>

```yaml
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
```
</details>

Makes the fermenter brew very quickly, and makes every mead base produce 10 potions instead of less.
<details>
  <summary>Yaml example</summary>

```yaml
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
```
</details>

Makes the cooking station cook faster.
<details>
  <summary>Yaml example</summary>

```yaml
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
```
</details>

Makes the oven cook faster, and fuel last longer.
<details>
  <summary>Yaml example</summary>

```yaml
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
```
</details>

Makes the spinning wheel work faster and take up to 50 flax.
<details>
  <summary>Yaml example</summary>

```yaml
conversionModifications:
  piece_spinningwheel:
    action: Modify
    prefab: piece_spinningwheel
    conversions:
    - fromPrefab: Flax
      toPrefab: LinenThread
    maxOres: 50
    conversionTime: 3
```
</details>

### Items

`\BepInEx\config\RecipeManager\Items.yaml` does two things:

- `action: Add` creates a brand new item by cloning an existing one. The clone keeps the model and
  every stat of the item it was copied from, and anything you set in the config is applied on top.
- `action: Modify` changes an item that already exists, vanilla weapons and armor included. Only the
  fields you set are changed, and taking a field back out of the yaml puts the original value back.

> Adding or removing an item needs a game restart (or a trip back to the main menu). This is a hard
> requirement of how Valheim identifies items on the network, not something the mod can work around.
> Everything else, including every modification of an existing item, works live with `RM_Items_Reload`.

#### Adding a new item

Add a "Honey bread" food, made from bread and honey at the cauldron
<details>
  <summary>Yaml example</summary>

```yaml
itemAdditions:
  HoneyBread:                              # <- The name of this addition, it is for your information but SHOULD BE UNIQUE
    action: Add                            # <- REQUIRED This is the action applied, can be [Add, Remove]
    prefab: HoneyBread                     # <- REQUIRED The prefab name of the NEW item, must be globally unique
    clonedFrom: Bread                      # <- REQUIRED The existing prefab to copy
    name: Honey Bread                      # <- The display name. Start it with '$' to use your own localization token
    description: Sweet, sticky and filling. # <- The tooltip description
    weight: 0.5                            # <- Leave unset to keep the cloned item's weight
    maxStackSize: 20                       # <- Leave unset to keep the cloned item's stack size
    value: 12                              # <- The trader value of this item
    teleportable: true                     # <- Whether this item can go through a portal
    food:                                  # <- Only set this for food and drinks, leave it out otherwise
      health: 50                           # <- The max health this food grants
      stamina: 60                          # <- The max stamina this food grants
      eitr: 0                              # <- The max eitr this food grants
      duration: 1800                       # <- How long the food lasts, in seconds
      regen: 2                             # <- Health regenerated per tick while this food is active
      eatAnimTime: 1                       # <- How long the eating animation takes
      isDrink: false                       # <- Whether this is drunk rather than eaten
      consumeStatusEffect: ''              # <- The name of an EXISTING status effect to apply when consumed
    recipe:                                # <- Optional, leave it out for an item that is not crafted
      craftedAt: piece_cauldron            # <- The crafting station, leave it empty for handcrafting
      repairAt: ''                         # <- Defaults to craftedAt
      minStationLevel: 1                   # <- The required crafting station level
      craftAmount: 1                       # <- How many of the item one craft produces
      anyOneResource: false                # <- Only require one of the ingredients below
      ingredients:
      - prefab: Bread                      # <- Prefab that this ingredient requires
        craftCost: 1                       # <- The amount of this ingredient it takes to craft
        upgradeCost: 0                     # <- The amount of this ingredient it takes to upgrade
      - prefab: Honey
        craftCost: 2
        upgradeCost: 0
```
</details>

Run `RM_Items_PrintAll` to dump the items in the game with their real stat values. That file is never
loaded, it is there so you can copy an entry and see what the numbers should look like. It is a big
file, so `RM_Items_PrintAll sword` will only dump the items whose prefab name contains "sword".

New items are visible to the rest of the mod by their prefab name, so you can also refer to them from
`Recipes.yaml` or `Conversions.yaml`. For example, to cook your new item in the oven instead of crafting
it at the cauldron, leave out the `recipe:` block and add a conversion from an uncooked item to it.

#### Modifying an existing item

Set `action: Modify` and point `prefab` at the item you want to change. There is no `clonedFrom`,
nothing new is created, and the change reaches every copy of that item that already exists, including
the ones sitting in chests and inventories.

Anything you leave out keeps its current value, so a modification only has to list what actually
differs. The reverse is true too: deleting a line and running `RM_Items_Reload` restores that item's
original value rather than leaving the last number you set.

Make the iron sword hit harder and set things on fire
<details>
  <summary>Yaml example</summary>

```yaml
itemAdditions:
  BurningIronSword:
    action: Modify
    prefab: SwordIron                        # <- The existing item to change
    weapon:
      damage:                                # <- Only the damage types listed here are touched
        slash: 60
        fire: 20
      damagePerLevel:
        slash: 5
      backstabBonus: 4
    statusEffects:
      onHit: Burning                         # <- Applied to whatever the sword hits
      onHitChance: 0.5
```
</details>

Give the troll armor set fire resistance and a real set bonus
<details>
  <summary>Yaml example</summary>

```yaml
itemAdditions:
  FireproofTrollChest:
    action: Modify
    prefab: ArmorTrollLeatherChest
    armor:
      armor: 20
      armorPerLevel: 4
      movementModifier: 0.05                 # <- 5% faster while worn
    resistances:                             # <- Replaces the item's whole resistance list
    - type: Fire
      modifier: VeryResistant
    - type: Frost
      modifier: Resistant
    setBonus:
      name: TrollLeather                     # <- Every piece of the set has to use the same name
      size: 3
      statusEffect: SetEffect_TrollArmor
```
</details>

#### Weapon, armor and set stats

These blocks work exactly the same on an item you added and on one you are modifying, so a cloned
sword can be given its own damage profile the same way a vanilla one can.

`weapon:` - damage and tool stats.

| Field | Meaning |
| --- | --- |
| `damage` | Base damage. Takes any of `damage`, `blunt`, `slash`, `pierce`, `chop`, `pickaxe`, `fire`, `frost`, `lightning`, `poison`, `spirit`. Types you leave out are not touched. |
| `damagePerLevel` | Damage added by each upgrade level, same fields as `damage`. |
| `attackForce` | Knockback. |
| `backstabBonus` | Sneak attack damage multiplier. |
| `toolTier` | What a tool is able to harvest. 0 is wood and stone, 2 is black metal. |

`armor:` - armor, shield and equip stats.

| Field | Meaning |
| --- | --- |
| `armor` / `armorPerLevel` | Armor value, and how much each upgrade level adds. |
| `blockPower` / `blockPowerPerLevel` | How much damage a block absorbs, shields and weapons that block. |
| `deflectionForce` / `deflectionForcePerLevel` | How far a blocked attacker is pushed back. |
| `movementModifier` | Speed change while equipped, as a fraction. `-0.05` is 5% slower. |
| `eitrRegenModifier` | Eitr regeneration change while equipped. |

`resistances:` - a list of `type` and `modifier` pairs. This **replaces** the item's whole resistance
list, so include every resistance you want it to end up with, and use `resistances: []` to remove them
all.

- `type` is one of `Blunt`, `Slash`, `Pierce`, `Chop`, `Pickaxe`, `Fire`, `Frost`, `Lightning`,
  `Poison`, `Spirit`.
- `modifier` is one of `Normal`, `SlightlyResistant`, `Resistant`, `VeryResistant`, `SlightlyWeak`,
  `Weak`, `VeryWeak`, `Immune`, `Ignore`.

`statusEffects:` - effects the item provides.

| Field | Meaning |
| --- | --- |
| `equipped` | Applied for as long as the item is equipped. |
| `onHit` | Applied to whatever the weapon hits. |
| `onHitChance` | Chance for `onHit` to land, from 0 to 1. |

`setBonus:` - the armor set the item belongs to.

| Field | Meaning |
| --- | --- |
| `name` | Grouping key. Every piece of the same set has to use the same value. |
| `size` | How many pieces have to be worn. The game ignores set bonuses with a size of 1 or less. |
| `statusEffect` | The effect that wearing the full set grants. |

Status effects are referenced by name and have to already exist, this mod cannot create new ones.
`SetEffect_TrollArmor`, `Burning` and `Frost` are examples of names that work. To empty a status effect
slot rather than set one, use the value `None`.

#### Custom icons and textures

Icons and textures are read from `\BepInEx\config\RecipeManager\Textures\<prefab>\`, where `<prefab>` is
the `prefab:` value of your new item. Any file that is present replaces that texture, and anything
missing keeps whatever the cloned item had.

The workflow is:

1. Run `RM_Textures_Dump Bread` in the console. This writes Bread's icon and material textures to
   `\BepInEx\config\RecipeManager\Textures\Bread\`, along with a `_dump_info.txt` describing what each
   file is.
2. Copy that whole folder and rename the copy to your new item's prefab name, for example
   `Textures\HoneyBread\`.
3. Repaint the PNGs you care about and delete the ones you did not change.
4. Restart the game, or run `RM_Items_Reload`.

The file names are what tie a PNG to a texture slot, so do not rename them:

- `icon_0.png` is the inventory icon (`icon.png` also works if there is only one). This is the one most
  people want.
- `<renderer>__<material>__<slot>.png` is a model texture, for example `attach__0___MainTex.png`.
  `_MainTex` is the colour texture, `_BumpMap` is the normal map, `_MetallicGlossMap` and
  `_EmissionMap` are the metallic and glow maps.

Textures are only ever applied to a copy of the material, so repainting an item you added never changes
the vanilla item it was cloned from.

This works for `action: Modify` too, where `<prefab>` is the existing item you are changing. Retexturing
an item that already exists does change how that item looks everywhere, and unlike its stats it is not
undone by `RM_Items_Reload` - remove the folder and restart to get the original artwork back.

On a server, item definitions are synced to clients but the PNGs are not. A client that does not have
the image files still gets the item with the correct stats and recipe, it just shows the original
artwork.

### FAQ
- Q: Why is my recipe not showing up?
  A. Ensure that the recipe uses the action 'Modify' or 'Add', by default recipes dumped from the object DB will not, they will use Enable. Which if the recipe already exists and is not disabled, does nothing.
- Q: My yaml has an error about the `refund` key
  A. The refund key was removed in 0.4.1. If you need to remove the key from an existing yaml you can use the following regex `( +refund: \w+\n?)` and replace with nothing. This can be done from most text editors find and replace.

- Q: My new item has the same food bar as the item I cloned it from
  A. Set `name` on the item addition. The display name is what the game uses to tell foods apart, so
  without one your item shares a food slot with its original.
- Q: Why does adding an item need a restart?
  A. An item's prefab name is its network identity, and Valheim builds that lookup once when the world
  loads. Items therefore have to exist before then. Editing an item you already have does not need a
  restart, only adding or removing one does.

- Q: I changed a vanilla item and now I want it back to normal
  A. Delete the modification and run `RM_Items_Reload`. The mod keeps the original values of anything it
  touches and puts them back. Custom icons and textures are the exception, those need a restart.

- Q: My status effect is not being applied
  A. Status effects have to already exist, this mod cannot create them. Check the log, an effect that
  could not be found is reported by name. `RM_Items_PrintAll` shows the names used by items that already
  have one.

- Q: I set a resistance and the item lost the resistances it already had
  A. `resistances` replaces the whole list rather than adding to it. List every resistance you want the
  item to end up with.

### Planned Features
- More recipe validation
- ~~Conversion modifications (like ores for ingots)~~ (added in 0.5.0)
- ~~Stat editing for weapons, armor and tools~~ (added in 0.6.0)
- Custom status effects for added items
- Export to WackyDB


## Installation (manual)
Ensure the downloaded .dll is placed inside your /bepinex/plugins folder.

Please note this mod does nothing until configured.


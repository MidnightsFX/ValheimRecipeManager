# RecipeManager
This is a lightweight recipe modification tool. You can define recipes to Add, Modify, Delete etc. It does not use any patches, and does not run constantly. 


## Features
- Disable recipes
- Modify recipes
- Delete recipes
- Disable recipes

Recipes can be manipulated and added through yaml. All of the existing recipes can also be dumped to a file, in the same format to help you find and understand existing recipes.

```yaml
#################################################
# Recipe Manipulation Config
#################################################
RecipeModifications:                     # <- This is the top level key, all modifications live under this, it is required.
  DisableWoodArrow:                      # <- This is the modification name, its primarily for you to understand what this modification does SHOULD BE UNIQUE
    action: Disable                      # <- This is the action it should be one of [Disable, Delete, Modify, Add, Enable]
    prefab: ArrowWood                    # <- This is the prefab that the modification will target
  AddNewWoodArrowRecipe:
    action: Add
    prefab: ArrowWood
    craftedAt: Workbench                 # <- The crafting station that should craft this recipe, leave it empty or invalid for handcrafting
    minStationLevel: 2                   # <- This is the required crafting station level for discovery AND crafting
    recipe:                              # <- When performing [Modify] or [Add] you should define a recipe
      anyOneResource: false              # <- This makes the recipe only require one ingrediant, first from the top will be used.
      ingredients:                       # <- Ingrediants in the recipe, is an array
        - prefab: Wood                   # <- Prefab that this ingrediant requires
          craftCost: 2                   # <- The amount of this ingrediant it takes to craft the recipe  
          upgradeCost: 0                 # <- The amount of this ingrediant it takes to upgrade the item 
          refund: false                  # <- Whether or not this recipe refunds  
        - prefab: Feathers
          craftCost: 2
          upgradeCost: 0
          refund: true
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
          refund: false
```

### Commands
This mod adds two new commands which can be used to speed up recipe modification.

`RecipeManager_Reload` - This reloads all recipe modifications that are listed in the recipe config file.

`RecipeManager_PrintAllRecipes` - This prints all recipes currently stored in the object DB (including modifications).

### Planned Features
- Server sync recipes (re-enable, disable etc)
- More recipe validation
- Recipe name referencing
- automatic rollback for recipes that are no longer edited


## Installation (manual)
Ensure the downloaded .dll is placed inside your /bepinex/plugins folder.

Please note this mod does nothing until configured.


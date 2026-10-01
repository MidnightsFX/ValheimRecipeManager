using Jotunn.Entities;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using YamlDotNet.Serialization;
using static Piece;

namespace RecipeManager.Common
{
    internal class DataObjects
    {
        public enum Action
        {
            Disable,
            Delete,
            Modify,
            Add,
            Enable
        }

        public enum PieceAction
        {
            Disable,
            Modify,
            Enable
        }

        public enum ConversionAction
        {
            Add,
            Modify,
            Remove
        }

        public enum ConversionType
        {
            Fermenter,
            Smelter,
            CookingStation
        }

        [DataContract]
        public class ConversionModificationCollection
        {
            public Dictionary<String, ConversionModification> ConversionModifications { get; set; } = new Dictionary<string, ConversionModification>();
        }

        public class ConversionModification
        {
            public ConversionAction action { get; set; }
            public string prefab { get; set; }
            public List<ConversionDef> conversions { get; set; }
            // public ConversionAttributes propertyChanges { get; set; }
            // Nullable so a field left out of the yaml leaves the station alone, while an explicit 0 is
            // still applied. 0 is meaningful to the game: a smelter with maxFuel 0 needs no fuel at all.
            public int? maxOres { get; set; }
            public int? maxFuel { get; set; }
            public int? fuelPerProduct { get; set; }
            //public bool requiresFuel { get; set; }
            //public bool requiresFire { get; set; }
            public float? conversionTime { get; set; }
            public string fuelItem { get; set; }
            public string overCookedItem { get; set; }
            public float overCookedTime { get; set; }
            public float? secPerFuel { get; set; }
        }

        public class ConversionDef
        {
            public string fromPrefab { get; set; }
            public string toPrefab { get; set; }
            public int amount { get; set; }
            public float cookTime { get; set; } = 0f;
        }

        public class TrackedConversion
        {
            public ConversionAction action { get; set; }
            public GameObject prefab { get; set; }
            public ConversionType convertType { get; set; }
            // Whichever conversion component was found on the prefab, used to decide convertType.
            // These alias the live components, the values they shipped with are snapshotted in
            // ConversionUpdater instead.
            public Smelter smelterComponent { get; set; }
            public Fermenter fermenterComponent { get; set; }
            public CookingStation cookingStationComponent { get; set; }
            public List<Smelter.ItemConversion> updatedSmelterConversions { get; set; }
            public List<Fermenter.ItemConversion> updatedFermenterConversions { get; set; }
            public List<CookingStation.ItemConversion> updatedCookingConversions { get; set; }
            // Null means the field was not configured and the station keeps its own value
            public int? maxOres { get; set; }
            public int? maxFuel { get; set; }
            public int? fuelPerProduct { get; set; }
            public float? conversionTime { get; set; }
            public float overCookedTime { get; set; }
            public float? secPerFuel { get; set; }
            public ItemDrop fuelItem { get; set; }
        }

        public class TrackedPiece
        {
            public PieceAction action { get; set; }
            public string prefab {  get; set; }
            // The values a piece shipped with are snapshotted in PieceUpdater, a component reference here
            // would just alias the object being modified.
            public Requirement[] updatedRequirements { get; set; }
            public CraftingStation RequiredToPlaceCraftingStation { get; set; }
            public bool AllowedInDungeon { get; set; } = false;
            public bool CanBeDeconstructed { get; set; } = true;
            public PieceCategory PieceCategory { get; set; } = PieceCategory.All;
            public int ComfortAmount { get; set; } = -1;
            public ComfortGroup ComfortGroup { get; set; } = ComfortGroup.None;
            public bool IsUpgradeForStation {  get; set; }
            public float CraftingStationConnectionRadius { get; set; } = -1f;
            public bool MustBeAvobeConnectedStation { get; set; } = false;
            public float SpaceRequired { get; set; } = -1;
            public string PieceName { get; set; }
            public string PieceDescription { get; set; }
            public bool EnablePiece { get; set; } = true;
            public Heightmap.Biome OnlyInBiome { get; set; } = Heightmap.Biome.All;
            public bool CultivatedGroundOnly { get; set; } = false;
            public bool GroundPlacement { get; set; } = true;
        }

        [DataContract]
        public class PieceModificationCollection
        {
            public Dictionary<String, PieceModification> PieceModifications { get; set; } = new Dictionary<string, PieceModification>();
        }

        [DataContract]
        public class PieceModification
        {
            public PieceAction action { get; set; }
            public string prefab { get; set; }
            public List<SimpleRequirement> requirements { get; set; } = new List<SimpleRequirement>();
            public String RequiredToPlaceCraftingStation { get; set;}
            public bool AllowedInDungeon { get; set; } = false;
            public bool CanBeDeconstructed { get; set; } = true;
            public PieceCategory PieceCategory { get; set; } = PieceCategory.All;
            public int ComfortAmount { get; set; } = -1;
            public ComfortGroup ComfortGroup { get; set; } = ComfortGroup.None;
            public bool IsUpgradeForStation { get; set; }
            public float CraftingStationConnectionRadius { get; set; } = -1f;
            public bool MustBeAvobeConnectedStation { get; set; } = false;
            public float SpaceRequired { get; set; } = -1;
            public string PieceName { get; set; }
            public string PieceDescription { get; set; }
            public bool EnablePiece { get; set; } = true;
            public Heightmap.Biome OnlyInSelectBiome { get; set; } = Heightmap.Biome.All;
            public bool CultivatedGroundOnly { get; set; } = false;
            public bool GroundPlacement {  get; set; } = false;
        }

        [DataContract]
        public class SimpleRequirement
        {
            public string Prefab { get; set; }
            public int amount { get; set; }
            public bool recover { get; set; } = true;
        }

        public class TrackedRecipe
        {
            public Action action { get; set; }
            public string prefab { get; set; }
            public string recipeName { get; set; }
            public Recipe originalRecipe { get; set; }
            // Whether originalRecipe was switched on before this mod touched it, the game ships some recipes off
            public bool originalEnabled { get; set; }
            public Recipe updatedRecipe { get; set; }
            public CustomRecipe updatedCustomRecipe { get; set; }
            public CustomRecipe originalCustomRecipe { get; set; }
        }

        [DataContract]
        public class RecipeModificationCollection
        {
            public Dictionary<String,RecipeModification> RecipeModifications { get; set; } = new Dictionary<String,RecipeModification>();
        }

        [DataContract]
        public class RecipeModification
        {
            // Always written. Disable is the enum's default, so it would otherwise be left out of the dump
            [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
            public Action action { get; set; }
            public string prefab { get; set; }
            public string recipeName { get; set; }
            public string craftedAt { get; set; }
            public string repairAt { get; set; }
            public short minStationLevel { get; set; } = 1;
            public short craftAmount { get; set; } = 1;
            // The fields below are nullable so that leaving one out keeps whatever the recipe being modified
            // already has, and an added recipe starts from the game's defaults. Entries written before these
            // fields existed rely on that. See RecipeFields for how each one is resolved.
            /// <summary>False keeps the recipe switched off. Left out, Modify and Add switch the recipe on.</summary>
            public bool? enabled { get; set; }
            /// <summary>Only listed in a station's upgrade tab, never its craft tab. The game's Deep North gold gear uses this.</summary>
            public bool? upgradeOnly { get; set; }
            /// <summary>Lower sorts first in the crafting list. The game uses 100, and 500 and up for fishing bait.</summary>
            public int? listSortWeight { get; set; }
            /// <summary>With anyOneResource, how much each quality level of the ingredient used adds, as a multiple of craftAmount.</summary>
            public float? qualityResultAmountMultiplier { get; set; }
            public SimpleRecipe recipe { get; set; }
        }

        [DataContract]
        public class SimpleRecipe
        {
            public bool anyOneResource { get; set; } = false;

            public bool noRecipeCost { get; set; } = false;
            public List<Ingrediant> ingredients { get; set; } = new List<Ingrediant>();
        }

        [DataContract]
        public class Ingrediant
        {
            public string prefab { get; set; }
            public short craftCost { get; set; }
            public short upgradeCost { get; set; }
            // Nullable like RecipeModification's optional fields. Left out, a Modify copies the value from the
            // modified recipe's requirement for the same prefab.
            /// <summary>
            /// Only paid at an upgrader station (the Forge of Potential), every other station ignores it. This is
            /// how the idols are attached to armor and weapons. craftCost is what each upgrade costs.
            /// </summary>
            public bool? upgraderResource { get; set; }
            /// <summary>Whether part of this ingredient is refunded when the Forge of Potential breaks the item.</summary>
            public bool? recover { get; set; }
            /// <summary>With anyOneResource, how many extra items are crafted when this is the ingredient used.</summary>
            public int? anyOneExtraAmount { get; set; }
        }

        public enum ItemAction
        {
            Add,
            Remove,
            Modify
        }

        [DataContract]
        public class ItemAdditionCollection
        {
            public Dictionary<String, ItemAddition> ItemAdditions { get; set; } = new Dictionary<String, ItemAddition>();
        }

        [DataContract]
        public class ItemAddition
        {
            public ItemAction action { get; set; }
            public string prefab { get; set; }
            public string clonedFrom { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            // These are nullable so that an unset value ("keep what the cloned prefab had") stays
            // distinguishable from an explicit 0. A sentinel like -1 would not survive the server sync,
            // because the serializer omits any value equal to the type default.
            public float? weight { get; set; }
            public int? maxStackSize { get; set; }
            public int? value { get; set; }
            public bool? teleportable { get; set; }
            public FoodStats food { get; set; }
            public WeaponStats weapon { get; set; }
            public ArmorStats armor { get; set; }
            // A null list keeps whatever resistances the item already has, an empty list clears them all.
            public List<DamageResistance> resistances { get; set; }
            public ItemStatusEffects statusEffects { get; set; }
            public SetBonus setBonus { get; set; }
            public ItemRecipe recipe { get; set; }
        }

        [DataContract]
        public class FoodStats
        {
            public float? health { get; set; }
            public float? stamina { get; set; }
            public float? eitr { get; set; }
            public float? duration { get; set; }
            public float? regen { get; set; }
            public float? eatAnimTime { get; set; }
            public bool? isDrink { get; set; }
            public string consumeStatusEffect { get; set; }
        }

        /// <summary>
        /// One damage value per damage type. Every field is optional, an unset one leaves that damage
        /// type exactly as it is, so a modification can raise a sword's slash damage without having to
        /// restate the rest of its damage profile.
        /// </summary>
        [DataContract]
        public class DamageValues
        {
            /// <summary>The untyped "damage" line in the tooltip, not the total of the other types.</summary>
            public float? damage { get; set; }
            public float? blunt { get; set; }
            public float? slash { get; set; }
            public float? pierce { get; set; }
            public float? chop { get; set; }
            public float? pickaxe { get; set; }
            public float? fire { get; set; }
            public float? frost { get; set; }
            public float? lightning { get; set; }
            public float? poison { get; set; }
            public float? spirit { get; set; }
        }

        [DataContract]
        public class WeaponStats
        {
            public DamageValues damage { get; set; }
            /// <summary>Damage added for each upgrade level above the first.</summary>
            public DamageValues damagePerLevel { get; set; }
            public float? attackForce { get; set; }
            public float? backstabBonus { get; set; }
            /// <summary>Which materials the tool can harvest, 0 is wood and stone, 2 is black metal.</summary>
            public int? toolTier { get; set; }
        }

        [DataContract]
        public class ArmorStats
        {
            public float? armor { get; set; }
            public float? armorPerLevel { get; set; }
            public float? blockPower { get; set; }
            public float? blockPowerPerLevel { get; set; }
            public float? deflectionForce { get; set; }
            public float? deflectionForcePerLevel { get; set; }
            /// <summary>Movement speed change while equipped, as a fraction. -0.05 is 5% slower.</summary>
            public float? movementModifier { get; set; }
            public float? eitrRegenModifier { get; set; }
        }

        [DataContract]
        public class DamageResistance
        {
            public HitData.DamageType type { get; set; }
            public HitData.DamageModifier modifier { get; set; }
        }

        [DataContract]
        public class ItemStatusEffects
        {
            /// <summary>Applied for as long as the item is equipped.</summary>
            public string equipped { get; set; }
            /// <summary>Applied to whatever this weapon hits.</summary>
            public string onHit { get; set; }
            /// <summary>Chance for the onHit effect to land, 0 to 1.</summary>
            public float? onHitChance { get; set; }
        }

        /// <summary>
        /// Armor set bonus. The game only grants it when the set has a status effect, a size above one,
        /// and enough equipped pieces sharing the same set name.
        /// </summary>
        [DataContract]
        public class SetBonus
        {
            /// <summary>Grouping key, every piece of the same set has to use the same value.</summary>
            public string name { get; set; }
            /// <summary>How many pieces of the set have to be worn.</summary>
            public int? size { get; set; }
            public string statusEffect { get; set; }
        }

        [DataContract]
        public class ItemRecipe
        {
            public string craftedAt { get; set; }
            public string repairAt { get; set; }
            public short minStationLevel { get; set; } = 1;
            public short craftAmount { get; set; } = 1;
            public bool anyOneResource { get; set; } = false;
            public List<Ingrediant> ingredients { get; set; } = new List<Ingrediant>();
        }

        public class TrackedItem
        {
            public ItemAction action { get; set; }
            public string prefab { get; set; }
            public string clonedFrom { get; set; }
            public GameObject itemPrefab { get; set; }
            public CustomItem customItem { get; set; }
            public ItemAddition definition { get; set; }
        }
    }
}

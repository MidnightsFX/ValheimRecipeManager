using Jotunn.Entities;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using YamlDotNet.Serialization;
using static Piece;

namespace RecipeManager.Common
{
    // Rule for every config class below: a field that is nullable or a reference type has no initializer. A
    // left-out value then reads back as null, which is what "keep the current value" is built on, and a value
    // can never change on its way to a client because the serializer left it out.
    internal partial class DataObjects
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
            Enable,
            Add
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
            CookingStation,
            Incinerator
        }

        [DataContract]
        public class ConversionModificationCollection
        {
            public Dictionary<String, ConversionModification> ConversionModifications { get; set; } = new Dictionary<string, ConversionModification>();
        }

        public class ConversionModification
        {
            // Always written, Add is the enum's default and would otherwise be left out of dumps and imports
            [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
            public ConversionAction action { get; set; }
            public string prefab { get; set; }
            public List<ConversionDef> conversions { get; set; }
            // Nullable so a field left out of the yaml leaves the station alone, while an explicit 0 is
            // still applied. 0 is meaningful to the game: a smelter with maxFuel 0 needs no fuel at all.
            public int? maxOres { get; set; }
            public int? maxFuel { get; set; }
            public int? fuelPerProduct { get; set; }
            public float? conversionTime { get; set; }
            public string fuelItem { get; set; }
            /// <summary>What a cooking station turns food into when it is left on too long.</summary>
            public string overCookedItem { get; set; }
            /// <summary>Matches no setting in the game. Kept so files that set it still load, it is ignored.</summary>
            public float overCookedTime { get; set; }
            public float? secPerFuel { get; set; }
            /// <summary>Smelters, whether what it makes comes out as one stack instead of one item at a time.</summary>
            public bool? spawnStack { get; set; }
            /// <summary>Smelters, whether it only works under a roof.</summary>
            public bool? requiresRoof { get; set; }
            /// <summary>Cooking stations, whether it needs a fire under it.</summary>
            public bool? requiresFire { get; set; }
            /// <summary>Incinerators, what everything without a conversion of its own turns into.</summary>
            public string defaultResult { get; set; }
            /// <summary>Incinerators, how many items make one defaultResult.</summary>
            public int? defaultCost { get; set; }
            /// <summary>Incinerators, what particular items turn into.</summary>
            public List<IncineratorConversionDef> incineratorConversions { get; set; }
        }

        /// <summary>One thing an incinerator makes, from one or more items.</summary>
        public class IncineratorConversionDef
        {
            /// <summary>What it makes, which also names the conversion for Add and Remove.</summary>
            public string result { get; set; }
            /// <summary>How many it makes each time. Left out, it keeps the station's own, or 1.</summary>
            public int? resultAmount { get; set; }
            /// <summary>Which conversion goes first when several could be made, highest first.</summary>
            public int? priority { get; set; }
            /// <summary>Whether any one of the ingredients is enough, rather than all of them.</summary>
            public bool? anyOneIngredient { get; set; }
            /// <summary>Left out of an Add that updates a conversion, it keeps its ingredients.</summary>
            public List<IncineratorIngredientDef> ingredients { get; set; }
        }

        public class IncineratorIngredientDef
        {
            public string prefab { get; set; }
            /// <summary>Left out, 1.</summary>
            public int? amount { get; set; }
        }

        public class ConversionDef
        {
            public string fromPrefab { get; set; }
            public string toPrefab { get; set; }
            /// <summary>Fermenters only, how many items one input makes. Left out, it keeps what the station has for the same input, or the game's 4.</summary>
            public int? amount { get; set; }
            /// <summary>Cooking stations only, seconds to cook. Left out, it keeps what the station has for the same input, or the game's 10.</summary>
            public float? cookTime { get; set; }
        }

        /// <summary>A configured conversion with its items looked up. A null side is either a smelter conversion with that side empty, or one left out of an Add that updates a conversion the station already has.</summary>
        public class ResolvedConversion
        {
            public ItemDrop from { get; set; }
            public ItemDrop to { get; set; }
            public bool toGiven { get; set; }
            public int? amount { get; set; }
            public float? cookTime { get; set; }
        }

        public class TrackedConversion
        {
            public string key { get; set; }
            public ConversionAction action { get; set; }
            public GameObject prefab { get; set; }
            public ConversionType convertType { get; set; }
            // Whichever conversion component was found on the prefab, used to decide convertType.
            // These alias the live components, the values they shipped with are snapshotted in
            // ConversionUpdater instead.
            public Smelter smelterComponent { get; set; }
            public Fermenter fermenterComponent { get; set; }
            public CookingStation cookingStationComponent { get; set; }
            // Null when the entry lists no conversions, the station then keeps its own
            public List<ResolvedConversion> conversions { get; set; }
            // Null means the field was not configured and the station keeps its own value
            public int? maxOres { get; set; }
            public int? maxFuel { get; set; }
            public int? fuelPerProduct { get; set; }
            public float? conversionTime { get; set; }
            public float? secPerFuel { get; set; }
            public ItemDrop fuelItem { get; set; }
            public ItemDrop overCookedItem { get; set; }
            public Incinerator incineratorComponent { get; set; }
            public bool? spawnStack { get; set; }
            public bool? requiresRoof { get; set; }
            public bool? requiresFire { get; set; }
            public ItemDrop defaultResult { get; set; }
            public int? defaultCost { get; set; }
            /// <summary>Null when the entry lists none. Each keeps the def it came from, for the values an Add leaves out.</summary>
            public List<(Incinerator.IncineratorConversion built, IncineratorConversionDef def)> incineratorConversions { get; set; }
        }

        public class TrackedPiece
        {
            /// <summary>The entry's name, for messages.</summary>
            public string key { get; set; }
            public PieceAction action { get; set; }
            public string prefab {  get; set; }
            public PieceModification definition { get; set; }
            // Resolved while tracking. The values a piece shipped with are snapshotted in PieceUpdater, a
            // component reference here would just alias the object being modified.
            /// <summary>Null keeps the piece's build cost.</summary>
            public Requirement[] updatedRequirements { get; set; }
            /// <summary>False keeps the piece's station, true sets it to <see cref="RequiredToPlaceCraftingStation"/>, which can be null.</summary>
            public bool stationSet { get; set; }
            public CraftingStation RequiredToPlaceCraftingStation { get; set; }
            public PieceCategory? category { get; set; }
            /// <summary>Null keeps the piece's blocking pieces.</summary>
            public List<Piece> blockingPieces { get; set; }
            /// <summary>Null keeps the station an extension belongs to.</summary>
            public CraftingStation extensionStation { get; set; }
            /// <summary>Null keeps a fireplace's fuel.</summary>
            public ItemDrop fireplaceFuel { get; set; }
        }

        [DataContract]
        public class PieceModificationCollection
        {
            public Dictionary<String, PieceModification> PieceModifications { get; set; } = new Dictionary<string, PieceModification>();
        }

        /// <summary>
        /// A Modify only changes what it sets, everything left out keeps the piece's current value. The YAML key
        /// is the camelCase of each property name, so the misspelled mustBeAvobeConnectedStation stays as it is.
        /// </summary>
        [DataContract]
        public partial class PieceModification
        {
            // Always written, Disable is the enum's default and would otherwise be left out of dumps and imports
            [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
            public PieceAction action { get; set; }
            public string prefab { get; set; }
            /// <summary>Null keeps the build cost, an empty list makes the piece free.</summary>
            public List<SimpleRequirement> requirements { get; set; }
            /// <summary>Left out keeps the station, 'none' removes the need for one.</summary>
            public String RequiredToPlaceCraftingStation { get; set;}
            public bool? AllowedInDungeon { get; set; }
            public bool? CanBeDeconstructed { get; set; }
            /// <summary>A build tab, one of the game's piece categories.</summary>
            public string PieceCategory { get; set; }
            public int? ComfortAmount { get; set; }
            public ComfortGroup? ComfortGroup { get; set; }
            public bool? IsUpgradeForStation { get; set; }
            public float? CraftingStationConnectionRadius { get; set; }
            public bool? MustBeAvobeConnectedStation { get; set; }
            public float? SpaceRequired { get; set; }
            public string PieceName { get; set; }
            public string PieceDescription { get; set; }
            public bool? EnablePiece { get; set; }
            public Heightmap.Biome? OnlyInSelectBiome { get; set; }
            public bool? CultivatedGroundOnly { get; set; }
            public bool? GroundPlacement {  get; set; }
        }

        [DataContract]
        public class SimpleRequirement
        {
            public string Prefab { get; set; }
            public int amount { get; set; }
            /// <summary>Whether this is refunded when the piece is torn down. Left out, it keeps what the piece has for the same item, or true.</summary>
            public bool? recover { get; set; }
        }

        public class TrackedRecipe
        {
            public string key { get; set; }
            public Action action { get; set; }
            public string prefab { get; set; }
            public string recipeName { get; set; }
            /// <summary>
            /// The recipe as the game or the mod that adds it made it, never one this mod built. A Modify takes the
            /// values it leaves out from here and a revert puts this back.
            /// </summary>
            public Recipe originalRecipe { get; set; }
            /// <summary>
            /// The object in the recipe list that a Modify replaces. Usually <see cref="originalRecipe"/>, but a
            /// recipe another mod registers through Jotunn can come back as the one this mod built last time.
            /// </summary>
            public Recipe listedRecipe { get; set; }
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
            // Everything from here on is nullable so that leaving it out of a Modify keeps whatever the recipe
            // already has, and an added recipe starts from the game's defaults. See RecipeBuilder and
            // RecipeFields for how each one is resolved.
            /// <summary>A crafting station's prefab name or its $piece_ name. 'none', or an empty value, makes the recipe handcrafted.</summary>
            public string craftedAt { get; set; }
            /// <summary>Like craftedAt. Items can always be repaired where they are crafted, this adds a second station.</summary>
            public string repairAt { get; set; }
            public short? minStationLevel { get; set; }
            /// <summary>
            /// The highest station level upgrading ever needs. The game asks for one more level per upgrade, this
            /// caps it so the last upgrades can be done at a lower level. Uses a game patch.
            /// </summary>
            public short? maxStationLevel { get; set; }
            public short? craftAmount { get; set; }
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
            public bool? anyOneResource { get; set; }

            public bool noRecipeCost { get; set; }
            /// <summary>Left out, a Modify keeps the recipe's ingredients. An empty list makes the recipe free.</summary>
            public List<Ingrediant> ingredients { get; set; }
        }

        [DataContract]
        public class Ingrediant
        {
            public string prefab { get; set; }
            // Nullable like RecipeModification's optional fields. Left out, a Modify copies the value from the
            // modified recipe's requirement for the same prefab, or uses 0 for an item the recipe did not have.
            public short? craftCost { get; set; }
            public short? upgradeCost { get; set; }
            /// <summary>
            /// What upgrading to each level costs, by the level reached: {2: 5, 3: 10}. A level not listed costs none
            /// of this ingredient. Replaces upgradeCost, and uses a game patch.
            /// </summary>
            public Dictionary<int, int> upgradeCosts { get; set; }
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
            // Always written, Add is the enum's default and would otherwise be left out of dumps and imports
            [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
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
            /// <summary>Seconds it takes to put the item on.</summary>
            public float? equipDuration { get; set; }
            /// <summary>Kept on death and cannot be moved into a chest, like the game's keys.</summary>
            public bool? questItem { get; set; }
            /// <summary>An item whose tooltip is shown below this one's, the way the feasts show their food. 'none' removes it.</summary>
            public string appendToolTip { get; set; }
            public ItemQuality quality { get; set; }
            public ItemDurability durability { get; set; }
            public FoodStats food { get; set; }
            public WeaponStats weapon { get; set; }
            public ArmorStats armor { get; set; }
            public StaminaModifiers staminaModifiers { get; set; }
            public AdrenalineStats adrenaline { get; set; }
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
            /// <summary>Damage only dealt to creatures, never to players.</summary>
            public float? nonPlayer { get; set; }
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
            /// <summary>The skill the weapon trains and uses: one of the game's, such as Swords or Bows, or one a mod adds.</summary>
            public string skill { get; set; }
            /// <summary>The kind of ammo a bow or crossbow fires, or the kind an ammo item is. The game's are $ammo_arrows and $ammo_bolts.</summary>
            public string ammoType { get; set; }
            /// <summary>Whether its hits can be dodged.</summary>
            public bool? dodgeable { get; set; }
            /// <summary>Whether its hits can be blocked.</summary>
            public bool? blockable { get; set; }
            /// <summary>
            /// Which look of fire and lightning its hits set off. Fire is 0 red, 1 blue, 2 green, lightning is 0 the usual
            /// and 1 red. -1 plays every look at once. Unset, an item at -1 with fire or lightning damage gets 0.
            /// </summary>
            public int? hitVariant { get; set; }
            /// <summary>A prefab spawned where it hits something, 'none' for nothing.</summary>
            public string spawnOnHit { get; set; }
            /// <summary>A prefab spawned where it hits the ground, 'none' for nothing.</summary>
            public string spawnOnHitTerrain { get; set; }
            public AttackStats primaryAttack { get; set; }
            public AttackStats secondaryAttack { get; set; }
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
            /// <summary>Protection from the Ashlands heat while equipped, as a fraction.</summary>
            public float? heatResistanceModifier { get; set; }
            /// <summary>How much more a block absorbs when it is timed as a parry, as a multiplier.</summary>
            public float? timedBlockBonus { get; set; }
            /// <summary>Stamina given back by a parry.</summary>
            public float? perfectBlockStaminaRegen { get; set; }
            /// <summary>Whether blocking builds up charges, like the game's bucklers.</summary>
            public bool? buildBlockCharges { get; set; }
            public int? maxBlockCharges { get; set; }
            /// <summary>Seconds before a block charge wears off.</summary>
            public float? blockChargeDecayTime { get; set; }
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
            /// <summary>Applied to the wearer when they parry.</summary>
            public string perfectBlock { get; set; }
            /// <summary>Applied when the wearer's adrenaline fills up.</summary>
            public string fullAdrenaline { get; set; }
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
            /// <summary>Left out, 1.</summary>
            public short? minStationLevel { get; set; }
            /// <summary>Like a recipe's maxStationLevel.</summary>
            public short? maxStationLevel { get; set; }
            /// <summary>Left out, 1.</summary>
            public short? craftAmount { get; set; }
            public bool? anyOneResource { get; set; }
            public List<Ingrediant> ingredients { get; set; }
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

using Jotunn.Entities;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
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

        public class ConversionModification
        {
            public ConversionAction action { get; set; }
            public string prefab { get; set; }
            public List<ConversionDef> conversions { get; set; }
            // public ConversionAttributes propertyChanges { get; set; }
            public int maxOres { get; set; } = 0;
            public int maxFuel { get; set; } = 0;
            public int fuelPerProduct { get; set; } = 0;
            public bool requiresFuel { get; set; } = true;
            public float conversionTime { get; set; }
            public string fuelItem { get; set; }
            public string overCookedItem { get; set; }
            public float overCookedTime { get; set; }
            public int secPerFuel { get; set; } 
        }

        public class ConversionDef
        {
            public string fromPrefab { get; set; }
            public string toPrefab { get; set; }
            public int amount { get; set; } = 1;
            public float cookTime { get; set; } = 0f;
        }

        public class ConversionAttributes
        {
            public bool requiresRoof { get; set; } = false;
            public bool requiresFuel { get; set; } = false;
        }

        public class TrackedConversion
        {
            public ConversionAction action { get; set; }
            public GameObject prefab { get; set; }
            public ConversionType convertType { get; set; }
            public Smelter originalSmelter { get; set; }
            public Fermenter originalFermenter { get; set; }
            public CookingStation originalCookingStation { get; set; }
            public List<Smelter.ItemConversion> updatedSmelterConversions { get; set; }
            public List<Fermenter.ItemConversion> updatedFermenterConversions { get; set; }
            public List<CookingStation.ItemConversion> updatedCookingConversions { get; set; }
            public int maxOres { get; set; }
            public int maxFuel { get; set; }
            public int fuelPerProduct { get; set; }
            public float conversionTime { get; set; }
            public bool requiresFuel { get; set; }
            public float overCookedTime { get; set; }
            public ItemDrop fuelItem { get; set; }
        }

        public class TrackedPiece
        {
            public PieceAction action { get; set; }
            public string prefab {  get; set; }
            public Piece originalPiece { get; set; }
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
        }

        public class TrackedRecipe
        {
            public Action action { get; set; }
            public string prefab { get; set; }
            public string recipeName { get; set; }
            public Recipe originalRecipe { get; set; }
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
            public Action action { get; set; }
            public string prefab { get; set; }
            public string recipeName { get; set; }
            public string craftedAt { get; set; }
            public string repairAt { get; set; }
            public short minStationLevel { get; set; } = 1;
            public short craftAmount { get; set; } = 1;
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
        }
    }
}

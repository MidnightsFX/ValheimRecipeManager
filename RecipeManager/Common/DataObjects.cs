using Jotunn.Entities;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using static Piece;

namespace RecipeManager.Common
{
    public class DataObjects
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

        public enum PrefabSourceType {
            ExistingPrefab,
            LoadFromAsset
        }

        public class SourcePrefab
        {
            public PrefabSourceType source { get; set; }
            public string name { get; set; }
            public string objectSubPath { get; set; }
        }

        public class Vect {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }

        public class PrefabMod
        {
            public SourcePrefab sourcePrefab { get; set; }
            public string targetPrefab { get; set; }
            public string targetPrefabPath { get; set; }
            public string sourcePrefabPath { get; set; }
            public Quaternion rotation { get; set; }
            public Vector3 local_position { get; set; }
            public Vect size { get; set; }

        }

        public class PrefabModifierCollection {
            public Dictionary<string, PrefabMod> PrefabModifications { get; set; } = new Dictionary<string, PrefabMod>();
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
            public int maxOres { get; set; } = 0;
            public int maxFuel { get; set; } = 0;
            public int fuelPerProduct { get; set; } = 0;
            public bool requiresFuel { get; set; } = false;
            public bool requiresFire { get; set; } = false;
            public float conversionTime { get; set; }
            public string fuelItem { get; set; }
            public string overCookedItem { get; set; }
            public float overCookedTime { get; set; }
            public float secPerFuel { get; set; } 
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
            public bool requiresFire { get; set; }
            public float overCookedTime { get; set; }
            public float secPerFuel { get; set; }
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

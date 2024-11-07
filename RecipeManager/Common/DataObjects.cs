using Jotunn.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
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
            public Piece UpgradeFor {  get; set; }
            public float CraftingStationConnectionRadius { get; set; } = -1f;
            public bool MustBeAvobeConnectedStation { get; set; } = false;
            public int ExtraPlacementSpaceRequired { get; set; } = -1;
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
            public string UpgradeFor { get; set; }
            public float CraftingStationConnectionRadius { get; set; } = -1f;
            public bool MustBeAvobeConnectedStation { get; set; } = false;
            public int ExtraPlacementSpaceRequired { get; set; } = -1;
            public string PieceName { get; set; }
            public string PieceDescription { get; set; }
            public bool EnablePiece { get; set; } = true;
            public Heightmap.Biome OnlyInSelectBiome { get; set; } = Heightmap.Biome.All;
            public bool CultivatedGroundOnly { get; set; } = false;
            public bool GroundPlacement {  get; set; } = true;
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
            public short minStationLevel { get; set; } = 1;
            public short craftAmount { get; set; } = 1;
            public SimpleRecipe recipe { get; set; }
        }

        [DataContract]
        public class SimpleRecipe
        {
            public bool anyOneResource { get; set; } = false;
            public List<Ingrediant> ingredients { get; set; } = new List<Ingrediant>();
        }

        [DataContract]
        public class Ingrediant
        {
            public string prefab { get; set; }
            public short craftCost { get; set; }
            public short upgradeCost { get; set; }
            public bool refund { get; set; } = false;
        }
    }
}

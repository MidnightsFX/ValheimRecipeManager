using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RecipeManager.Common
{
    // The piece fields added in 0.11. Every one is optional and left out keeps the piece's own value, or the value
    // of the piece it is cloned from.
    internal partial class DataObjects
    {
        public partial class PieceModification
        {
            /// <summary>For an Add, the existing piece to copy. The new piece keeps its model and every setting.</summary>
            public string clonedFrom { get; set; }
            /// <summary>
            /// The tool the piece is built with, by the tool's prefab name (Hammer, Hoe, Cultivator) or its piece
            /// table's. A Modify moves the piece out of the tools it is in now.
            /// </summary>
            public string pieceTable { get; set; }
            /// <summary>The build menu tags the piece is listed under, such as Furniture or Storage. Several can be listed with commas.</summary>
            public Piece.UsageTagFlags? usage { get; set; }
            /// <summary>The level the crafting station needs to place the piece. Uses a game patch.</summary>
            public int? minStationLevel { get; set; }

            // Placement rules
            public bool? groundOnly { get; set; }
            public bool? allowAltGroundPlacement { get; set; }
            public bool? waterPiece { get; set; }
            public bool? noInWater { get; set; }
            public bool? notOnWood { get; set; }
            public bool? notOnTiltingSurface { get; set; }
            public bool? inCeilingOnly { get; set; }
            public bool? notOnFloor { get; set; }
            public bool? noClipping { get; set; }
            public bool? clipGround { get; set; }
            public bool? clipEverything { get; set; }
            public bool? onlyInTeleportArea { get; set; }
            public bool? allowedInDeepSnow { get; set; }
            public bool? requireDeepSnow { get; set; }
            public bool? vegetationGroundOnly { get; set; }
            public bool? canRotate { get; set; }
            public bool? allowRotatedOverlap { get; set; }
            public bool? canRockJade { get; set; }
            public bool? noVines { get; set; }
            /// <summary>Extra distance the piece can be placed from, on top of the tool's.</summary>
            public int? extraPlacementDistance { get; set; }
            /// <summary>How far away the pieces in blockingPieces stop this one being placed.</summary>
            public float? blockRadius { get; set; }
            /// <summary>Pieces that cannot be near this one, by prefab name. Replaces the whole list, [] clears it.</summary>
            public List<string> blockingPieces { get; set; }

            public WearNTearSettings wearNTear { get; set; }
            public CraftingStationSettings station { get; set; }
            public StationExtensionSettings stationExtension { get; set; }
            public ContainerSettings container { get; set; }
            public FireplaceSettings fireplace { get; set; }
        }

        /// <summary>How the piece wears and breaks.</summary>
        [DataContract]
        public class WearNTearSettings
        {
            public float? health { get; set; }
            /// <summary>Decides how well the piece carries weight: Wood, Stone, Iron, HardWood, Marble, Ashstone, Ancient, Ice or Timberwood.</summary>
            public WearNTear.MaterialType? materialType { get; set; }
            /// <summary>Whether it is spared the wear of standing in the rain.</summary>
            public bool? noRoofWear { get; set; }
            /// <summary>Whether it is spared the wear of having little support.</summary>
            public bool? noSupportWear { get; set; }
            /// <summary>Whether other pieces can rest on it.</summary>
            public bool? supports { get; set; }
            /// <summary>Whether damaging it alerts a nearby ward.</summary>
            public bool? triggerPrivateArea { get; set; }
            public bool? burnable { get; set; }
            /// <summary>The lowest tool tier that can damage it.</summary>
            public int? minToolTier { get; set; }
            public bool? ashDamageImmune { get; set; }
            public bool? ashDamageResist { get; set; }
            public bool? snowDamageImmune { get; set; }
            /// <summary>Resistances by damage type. The types left out keep what the piece has.</summary>
            public List<DamageResistance> resistances { get; set; }
        }

        /// <summary>A crafting station's own settings, for a piece that is one.</summary>
        [DataContract]
        public class CraftingStationSettings
        {
            /// <summary>
            /// The station's name, which is also what recipes and upgrades know it by. A copy of a station gets the piece's
            /// name, so it is a station of its own. Start it with '$' to use your own localization token.
            /// </summary>
            public string name { get; set; }
            /// <summary>How close a player has to come to learn the station.</summary>
            public float? discoveryRange { get; set; }
            /// <summary>How far from it pieces that need it can be built.</summary>
            public float? buildRange { get; set; }
            /// <summary>Build range added by each upgrade of the station.</summary>
            public float? extraRangePerLevel { get; set; }
            public float? useDistance { get; set; }
            public bool? craftRequiresRoof { get; set; }
            public bool? craftRequiresFire { get; set; }
            /// <summary>Whether it also lists the recipes that need no station.</summary>
            public bool? showBasicRecipes { get; set; }
            public bool? canRepair { get; set; }
        }

        /// <summary>A station upgrade's settings, for a piece that is one.</summary>
        [DataContract]
        public class StationExtensionSettings
        {
            /// <summary>The station it upgrades, by prefab name.</summary>
            public string station { get; set; }
            /// <summary>How far from the station it can be.</summary>
            public float? maxStationDistance { get; set; }
            /// <summary>Whether several of it count as several upgrades.</summary>
            public bool? stack { get; set; }
            public bool? continuousConnection { get; set; }
        }

        /// <summary>A container's settings, for a piece that is one.</summary>
        [DataContract]
        public class ContainerSettings
        {
            public int? width { get; set; }
            public int? height { get; set; }
            /// <summary>Whether a ward keeps others out of it.</summary>
            public bool? checkWard { get; set; }
            /// <summary>Whether it breaks once it is empty.</summary>
            public bool? autoDestroyIfEmpty { get; set; }
        }

        /// <summary>A fire's settings, for a piece that is one.</summary>
        [DataContract]
        public class FireplaceSettings
        {
            public float? startFuel { get; set; }
            public float? maxFuel { get; set; }
            /// <summary>Seconds one fuel lasts.</summary>
            public float? secPerFuel { get; set; }
            public bool? infiniteFuel { get; set; }
            /// <summary>The item it burns, by prefab name.</summary>
            public string fuelItem { get; set; }
            public bool? canTurnOff { get; set; }
            public bool? canRefill { get; set; }
            /// <summary>Seconds between tries to set fire to what is around it, 0 for never.</summary>
            public float? igniteInterval { get; set; }
            public float? igniteChance { get; set; }
            public int? igniteSpread { get; set; }
        }
    }
}

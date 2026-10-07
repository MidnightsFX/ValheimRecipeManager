using Jotunn.Entities;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = Jotunn.Logger;

namespace RecipeManager
{
    internal class PieceReloadCommand : ConsoleCommand
    {
        public override string Name => "RM_Pieces_Reload";

        public override string Help => "Resynchronizes piece modifications.";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            ValConfig.Pieces.ReloadFromDisk();
        }
    }

    internal class PiecePrintCommand : ConsoleCommand
    {
        internal const string DumpFileName = "AllPiecesDebug.yaml";

        public override string Name => "RM_Pieces_PrintAll";

        public override string Help => "Prints all the Pieces found.";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Starting to dump piece list"); }
            String ODBRecipes_path = Path.Combine(BepInEx.Paths.ConfigPath, "RecipeManager", DumpFileName);
            PieceModificationCollection pieceCollection = new PieceModificationCollection();
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Gathering Pieces"); }
            List<Piece> pieces = Resources.FindObjectsOfTypeAll<Piece>().Where(pc => pc.name.EndsWith("(Clone)") != true && Regex.IsMatch(pc.name.Trim(), @"\(\d+\)") != true ).ToList<Piece>();

            Dictionary<GameObject, string> tools = PieceTables.ToolsByPiece();
            foreach (Piece pc in pieces)
            {
                if (pc == null) { continue; }
                if (pc.name == null) { continue; }
                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Building Piece definition {pc.m_name}"); }
                PieceModification piece_as_mod = DescribePiece(pc, tools);
                if (!pieceCollection.PieceModifications.ContainsKey(piece_as_mod.prefab)) {
                    pieceCollection.PieceModifications.Add(piece_as_mod.prefab, piece_as_mod);
                }
            }
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Serializing and printing pieces."); }
            using (StreamWriter writetext = new StreamWriter(ODBRecipes_path))
            {
                writetext.WriteLine("# Reference dump of every piece, as it currently is.");
                writetext.WriteLine("# This file is NOT loaded by the mod, it is here so you can see what the pieces are.");
                writetext.WriteLine("#");
                writetext.WriteLine("# To change one of these pieces, copy its entry into your own Pieces.yaml and delete");
                writetext.WriteLine("# every line you do not want to change - anything left out keeps its current value.");
                writetext.WriteLine("# Copied out unchanged, an entry leaves its piece exactly as it is.");
                writetext.WriteLine();
                writetext.WriteLine(ValConfig.yamlserializer.Serialize(pieceCollection));
            }
            ItemCommandUtils.Respond($"Dumped {pieceCollection.PieceModifications.Count} piece(s) to {ODBRecipes_path}");
        }

        /// <summary>The Modify that reproduces a piece, with every field written out.</summary>
        /// <param name="tools">Each piece's tool, from <see cref="PieceTables.ToolsByPiece"/>.</param>
        internal static PieceModification DescribePiece(Piece pc, Dictionary<GameObject, string> tools)
        {
            PieceModification piece_as_mod = new PieceModification
            {
                action = PieceAction.Modify,
                prefab = pc.gameObject.name,
                EnablePiece = pc.m_enabled,
                CanBeDeconstructed = pc.m_canBeRemoved,
                CultivatedGroundOnly = pc.m_cultivatedGroundOnly,
                ComfortGroup = pc.m_comfortGroup,
                ComfortAmount = pc.m_comfort,
                GroundPlacement = pc.m_groundPiece,
                SpaceRequired = pc.m_spaceRequirement,
                AllowedInDungeon = pc.m_allowedInDungeons,
                CraftingStationConnectionRadius = pc.m_connectRadius,
                MustBeAvobeConnectedStation = pc.m_mustBeAboveConnected,
                OnlyInSelectBiome = pc.m_onlyInBiome,
                PieceCategory = CategoryName(pc.m_category),
                PieceDescription = pc.m_description,
                PieceName = pc.m_name,
                IsUpgradeForStation = pc.m_isUpgrade,
                RequiredToPlaceCraftingStation = pc.m_craftingStation != null ? StationRef.NameOf(pc.m_craftingStation) : StationRef.None,
                requirements = new List<SimpleRequirement>()
            };
            foreach (Piece.Requirement req in pc.m_resources ?? new Piece.Requirement[0])
            {
                if (req == null || req.m_resItem == null)
                {
                    if (ValConfig.EnableDebugMode.Value) { Logger.LogWarning($"{pc.name} has a requirement with no item, it is left out."); }
                    continue;
                }
                piece_as_mod.requirements.Add(new SimpleRequirement() { amount = req.m_amount, Prefab = req.m_resItem.name, recover = req.m_recover });
            }
            DescribeMore(pc, piece_as_mod, tools);
            return piece_as_mod;
        }

        /// <summary>
        /// The settings added in 0.11. The placement rules are only written where they differ from the game's default,
        /// most pieces use none of them.
        /// </summary>
        private static void DescribeMore(Piece pc, PieceModification def, Dictionary<GameObject, string> tools)
        {
            def.pieceTable = tools.TryGetValue(pc.gameObject, out string tool) ? tool : null;
            def.usage = pc.m_usage != 0 ? pc.m_usage : (Piece.UsageTagFlags?)null;
            if (Patches.PieceStationLevel.Levels.TryGetValue(pc, out int level)) { def.minStationLevel = level; }
            def.groundOnly = OnlyTrue(pc.m_groundOnly);
            def.allowAltGroundPlacement = OnlyTrue(pc.m_allowAltGroundPlacement);
            def.waterPiece = OnlyTrue(pc.m_waterPiece);
            def.noInWater = OnlyTrue(pc.m_noInWater);
            def.notOnWood = OnlyTrue(pc.m_notOnWood);
            def.notOnTiltingSurface = OnlyTrue(pc.m_notOnTiltingSurface);
            def.inCeilingOnly = OnlyTrue(pc.m_inCeilingOnly);
            def.notOnFloor = OnlyTrue(pc.m_notOnFloor);
            def.noClipping = OnlyTrue(pc.m_noClipping);
            def.clipGround = OnlyTrue(pc.m_clipGround);
            def.clipEverything = OnlyTrue(pc.m_clipEverything);
            def.onlyInTeleportArea = OnlyTrue(pc.m_onlyInTeleportArea);
            def.allowedInDeepSnow = OnlyTrue(pc.m_allowedInDeepSnow);
            def.requireDeepSnow = OnlyTrue(pc.m_requireDeepSnow);
            def.vegetationGroundOnly = OnlyTrue(pc.m_vegetationGroundOnly);
            def.canRotate = pc.m_canRotate ? (bool?)null : false;
            def.allowRotatedOverlap = OnlyTrue(pc.m_allowRotatedOverlap);
            def.canRockJade = OnlyTrue(pc.m_canRockJade);
            def.noVines = OnlyTrue(pc.m_noVines);
            def.extraPlacementDistance = pc.m_extraPlacementDistance != 0 ? pc.m_extraPlacementDistance : (int?)null;
            if (pc.m_blockingPieces != null && pc.m_blockingPieces.Count > 0)
            {
                def.blockRadius = pc.m_blockRadius;
                def.blockingPieces = pc.m_blockingPieces.Where(blocking => blocking != null).Select(blocking => blocking.gameObject.name).ToList();
            }

            WearNTear wear = pc.GetComponent<WearNTear>();
            if (wear != null)
            {
                def.wearNTear = new WearNTearSettings
                {
                    health = wear.m_health,
                    materialType = wear.m_materialType,
                    noRoofWear = wear.m_noRoofWear,
                    noSupportWear = wear.m_noSupportWear,
                    supports = wear.m_supports,
                    triggerPrivateArea = wear.m_triggerPrivateArea,
                    burnable = wear.m_burnable,
                    minToolTier = wear.m_minToolTier,
                    ashDamageImmune = OnlyTrue(wear.m_ashDamageImmune),
                    ashDamageResist = OnlyTrue(wear.m_ashDamageResist),
                    snowDamageImmune = OnlyTrue(wear.m_snowDamageImmune),
                    resistances = Resistances(wear.m_damages)
                };
            }
            CraftingStation station = pc.GetComponent<CraftingStation>();
            if (station != null)
            {
                def.station = new CraftingStationSettings
                {
                    name = station.m_name,
                    discoveryRange = station.m_discoverRange,
                    buildRange = station.m_rangeBuild,
                    extraRangePerLevel = station.m_extraRangePerLevel,
                    useDistance = station.m_useDistance,
                    craftRequiresRoof = station.m_craftRequireRoof,
                    craftRequiresFire = station.m_craftRequireFire,
                    showBasicRecipes = station.m_showBasicRecipies,
                    canRepair = station.m_canRepair
                };
            }
            StationExtension extension = pc.GetComponent<StationExtension>();
            if (extension != null)
            {
                def.stationExtension = new StationExtensionSettings
                {
                    station = extension.m_craftingStation != null ? StationRef.NameOf(extension.m_craftingStation) : null,
                    maxStationDistance = extension.m_maxStationDistance,
                    stack = extension.m_stack,
                    continuousConnection = extension.m_continousConnection
                };
            }
            Container container = pc.GetComponent<Container>();
            if (container != null)
            {
                def.container = new ContainerSettings
                {
                    width = container.m_width,
                    height = container.m_height,
                    checkWard = container.m_checkGuardStone,
                    autoDestroyIfEmpty = container.m_autoDestroyEmpty
                };
            }
            Fireplace fire = pc.GetComponent<Fireplace>();
            if (fire != null)
            {
                def.fireplace = new FireplaceSettings
                {
                    startFuel = fire.m_startFuel,
                    maxFuel = fire.m_maxFuel,
                    secPerFuel = fire.m_secPerFuel,
                    infiniteFuel = fire.m_infiniteFuel,
                    fuelItem = fire.m_fuelItem != null ? fire.m_fuelItem.name : null,
                    canTurnOff = fire.m_canTurnOff,
                    canRefill = fire.m_canRefill,
                    igniteInterval = fire.m_igniteInterval,
                    igniteChance = fire.m_igniteChance,
                    igniteSpread = fire.m_igniteSpread
                };
            }
        }

        private static bool? OnlyTrue(bool value) { return value ? true : (bool?)null; }

        /// <summary>The damage types a piece is not normal against.</summary>
        private static List<DamageResistance> Resistances(HitData.DamageModifiers damages)
        {
            List<DamageResistance> resistances = new List<DamageResistance>();
            foreach (HitData.DamageType type in Common.ItemAttributes.ResistableTypes)
            {
                HitData.DamageModifier modifier = damages.GetModifier(type);
                if (modifier != HitData.DamageModifier.Normal) { resistances.Add(new DamageResistance { type = type, modifier = modifier }); }
            }
            return resistances.Count > 0 ? resistances : null;
        }

        /// <summary>A build tab's name, the game's own or one a mod added, which the enum does not know.</summary>
        private static string CategoryName(Piece.PieceCategory category)
        {
            try
            {
                if (Jotunn.Managers.PieceManager.Instance.GetPieceCategoriesMap().TryGetValue(category, out string name)) { return name; }
            }
            catch (Exception) { }
            return category.ToString();
        }
    }
}

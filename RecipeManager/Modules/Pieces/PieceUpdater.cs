using Jotunn.Managers;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    internal class PieceUpdater
    {
        public static Dictionary<String, PieceModification> PiecesToModify = new Dictionary<String, PieceModification>();
        public static List<TrackedPiece> TrackedPieces = new List<TrackedPiece>();

        /// <summary>
        /// What each piece looked like before this mod first wrote to it, keyed by prefab name. Kept for
        /// the life of the process: the tracking list is rebuilt on every reload, but the values a piece
        /// shipped with never change, so re-capturing after a modification would record our own changes
        /// as the original.
        /// </summary>
        private static readonly Dictionary<String, PieceSnapshot> OriginalPieces = new Dictionary<String, PieceSnapshot>();
        /// <summary>The rest of what a piece can have written to it, recorded with <see cref="OriginalPieces"/>.</summary>
        private static readonly Dictionary<String, FieldSnapshot> OriginalPieceFields = new Dictionary<String, FieldSnapshot>();
        /// <summary>Icons loaded from the textures folder, read once per session. Null for a piece that has none.</summary>
        private static readonly Dictionary<String, Sprite> Icons = new Dictionary<String, Sprite>();

        // The Piece fields written besides the ones PieceSnapshot holds. Fields a game version does not have are skipped.
        private static readonly string[] PieceFields =
        {
            "m_icon", "m_usage", "m_groundOnly", "m_allowAltGroundPlacement", "m_waterPiece", "m_noInWater", "m_notOnWood",
            "m_notOnTiltingSurface", "m_inCeilingOnly", "m_notOnFloor", "m_noClipping", "m_clipGround", "m_clipEverything",
            "m_onlyInTeleportArea", "m_allowedInDeepSnow", "m_requireDeepSnow", "m_vegetationGroundOnly", "m_canRotate",
            "m_allowRotatedOverlap", "m_canRockJade", "m_noVines", "m_extraPlacementDistance", "m_blockRadius", "m_blockingPieces"
        };

        /// <summary>Hooked to every new object DB in a world.</summary>
        public static void InitialSychronization() {
            // Pieces outlive the world, put back what the previous one's configuration changed first
            RevertPieceModifications();
            BuildPieceTracker();
            PieceUpdateRunner();
        }

        /// <summary>Undoes the modifications applied, then tracks and applies the configured ones.</summary>
        public static void Reapply() {
            RevertPieceModifications();
            BuildPieceTracker();
            PieceUpdateRunner();
            RefreshBuildMenu();
        }

        /// <summary>The build menu only reads which pieces are known and in a tool when it is opened, or a station is found.</summary>
        private static void RefreshBuildMenu() {
            Player player = Player.m_localPlayer;
            if (player == null) { return; }
            try {
                player.UpdateKnownRecipesList();
                player.UpdateAvailablePiecesList();
            } catch (Exception ex) {
                Logger.LogDebug($"Could not refresh the build menu. Error: {ex.Message}");
            }
        }

        public static void PieceUpdateRunner()
        {
            Logger.LogInfo($"Applying {TrackedPieces.Count} piece modifications");
            foreach (TrackedPiece piece in TrackedPieces) {
                ApplyPieceModifications(piece);
            }
        }

        public static void ApplyPieceModifications(TrackedPiece piece)
        {
            if (piece == null) {
                Logger.LogWarning("A tracked piece was null, this modification will be skipped.");
                return;
            }
            Logger.LogDebug($"Applying piece ({piece.prefab}) modification action: {piece.action}");
            switch (piece.action) {
                case PieceAction.Disable:
                    DisablePiece(piece);
                    break;
                case PieceAction.Modify:
                case PieceAction.Add:
                    ModifyPiece(piece);
                    break;
                case PieceAction.Enable:
                    EnablePiece(piece);
                    break;
            }
        }

        /// <summary>
        /// Puts every piece this mod has touched back to the values it shipped with.
        ///
        /// Driven from the snapshots rather than the tracking list, so a piece that has since been
        /// dropped from the configuration is still restored. Callers reload and re-apply straight
        /// after this, which is what lets a setting removed from the yaml go back to its original
        /// value instead of keeping whatever was applied last.
        /// </summary>
        public static void RevertPieceModifications()
        {
            Patches.PieceStationLevel.Levels.Clear();
            PieceTables.RevertAll();
            PieceComponents.RevertAll();
            if (OriginalPieces.Count == 0) { return; }
            Logger.LogInfo($"Reverting {OriginalPieces.Count} piece modifications");
            foreach (KeyValuePair<String, PieceSnapshot> original in OriginalPieces)
            {
                RevertPiece(original.Key, original.Value);
            }
        }

        private static void RevertPiece(String prefabName, PieceSnapshot original)
        {
            Piece pcomp = ResolvePiece(prefabName);
            // ResolvePiece has already explained why it could not be found
            if (pcomp == null) { return; }
            original.WriteTo(pcomp);
            if (OriginalPieceFields.TryGetValue(prefabName, out FieldSnapshot fields)) { fields.WriteTo(pcomp); }
            Logger.LogDebug($"Restored the original settings of {prefabName}.");
        }

        /// <summary>
        /// Records what a piece looked like before it is written to. Only the first call for a given
        /// prefab does anything, so this is safe to put in front of every modification.
        /// </summary>
        private static void CaptureOriginal(String prefabName, Piece pcomp)
        {
            if (String.IsNullOrEmpty(prefabName) || pcomp == null) { return; }
            if (OriginalPieces.ContainsKey(prefabName)) { return; }
            OriginalPieces.Add(prefabName, PieceSnapshot.From(pcomp));
            OriginalPieceFields.Add(prefabName, FieldSnapshot.Of(pcomp, PieceFields));
        }

        /// <summary>
        /// Looks up the Piece component for a configured prefab name. Returns null and logs a warning
        /// when the prefab is not registered or carries no Piece component, so callers can skip that
        /// entry instead of throwing and aborting every remaining piece. Used on the revert path as
        /// well as the apply path, so the warnings do not assume either one.
        /// </summary>
        private static Piece ResolvePiece(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                Logger.LogWarning("A piece entry has no prefab configured, it will be skipped. Define a prefab to modify to fix this.");
                return null;
            }

            GameObject go = PrefabManager.Instance.GetPrefab(prefabName);
            if (go == null)
            {
                Logger.LogWarning($"Could not find a prefab named {prefabName}, it will be skipped. Check that whatever provides this piece is installed.");
                return null;
            }

            Piece pcomp = go.GetComponent<Piece>();
            if (pcomp == null)
            {
                Logger.LogWarning($"Prefab {prefabName} has no Piece component, it will be skipped.");
            }
            return pcomp;
        }

        public static void EnablePiece(TrackedPiece piece)
        {
            Logger.LogDebug($"Enabling {piece.prefab}");
            Piece pcomp = ResolvePiece(piece.prefab);
            if (pcomp == null) { return; }
            CaptureOriginal(piece.prefab, pcomp);
            pcomp.m_enabled = true;
        }

        /// <summary>Writes only what the entry sets, every field it leaves out keeps the piece's current value.</summary>
        private static void ModifyPiece(TrackedPiece piece)
        {
            Logger.LogDebug($"Modifying {piece.prefab}");
            Piece pcomp = ResolvePiece(piece.prefab);
            if (pcomp == null) { return; }
            CaptureOriginal(piece.prefab, pcomp);
            PieceModification def = piece.definition;

            if (piece.updatedRequirements != null) { pcomp.m_resources = piece.updatedRequirements; }
            if (piece.stationSet) { pcomp.m_craftingStation = piece.RequiredToPlaceCraftingStation; }
            if (piece.category.HasValue) { pcomp.m_category = piece.category.Value; }
            Set(ref pcomp.m_allowedInDungeons, def.AllowedInDungeon);
            Set(ref pcomp.m_canBeRemoved, def.CanBeDeconstructed);
            Set(ref pcomp.m_comfort, def.ComfortAmount);
            Set(ref pcomp.m_comfortGroup, def.ComfortGroup);
            Set(ref pcomp.m_connectRadius, def.CraftingStationConnectionRadius);
            Set(ref pcomp.m_enabled, def.EnablePiece);
            Set(ref pcomp.m_onlyInBiome, def.OnlyInSelectBiome);
            Set(ref pcomp.m_isUpgrade, def.IsUpgradeForStation);
            Set(ref pcomp.m_cultivatedGroundOnly, def.CultivatedGroundOnly);
            Set(ref pcomp.m_mustBeAboveConnected, def.MustBeAvobeConnectedStation);
            Set(ref pcomp.m_spaceRequirement, def.SpaceRequired);
            Set(ref pcomp.m_groundPiece, def.GroundPlacement);
            if (def.PieceName != null) { pcomp.m_name = def.PieceName; }
            if (def.PieceDescription != null) { pcomp.m_description = def.PieceDescription; }

            Set(ref pcomp.m_usage, def.usage);
            Set(ref pcomp.m_groundOnly, def.groundOnly);
            Set(ref pcomp.m_allowAltGroundPlacement, def.allowAltGroundPlacement);
            Set(ref pcomp.m_waterPiece, def.waterPiece);
            Set(ref pcomp.m_noInWater, def.noInWater);
            Set(ref pcomp.m_notOnWood, def.notOnWood);
            Set(ref pcomp.m_notOnTiltingSurface, def.notOnTiltingSurface);
            Set(ref pcomp.m_inCeilingOnly, def.inCeilingOnly);
            Set(ref pcomp.m_notOnFloor, def.notOnFloor);
            Set(ref pcomp.m_noClipping, def.noClipping);
            Set(ref pcomp.m_clipGround, def.clipGround);
            Set(ref pcomp.m_clipEverything, def.clipEverything);
            Set(ref pcomp.m_onlyInTeleportArea, def.onlyInTeleportArea);
            Set(ref pcomp.m_allowedInDeepSnow, def.allowedInDeepSnow);
            Set(ref pcomp.m_requireDeepSnow, def.requireDeepSnow);
            Set(ref pcomp.m_vegetationGroundOnly, def.vegetationGroundOnly);
            Set(ref pcomp.m_canRotate, def.canRotate);
            Set(ref pcomp.m_allowRotatedOverlap, def.allowRotatedOverlap);
            Set(ref pcomp.m_canRockJade, def.canRockJade);
            Set(ref pcomp.m_noVines, def.noVines);
            Set(ref pcomp.m_extraPlacementDistance, def.extraPlacementDistance);
            Set(ref pcomp.m_blockRadius, def.blockRadius);
            if (piece.blockingPieces != null) { pcomp.m_blockingPieces = new List<Piece>(piece.blockingPieces); }
            Sprite icon = LoadIcon(piece.prefab);
            if (icon != null) { pcomp.m_icon = icon; }
            if (def.minStationLevel.HasValue) { Patches.PieceStationLevel.Levels[pcomp] = def.minStationLevel.Value; }

            if (!string.IsNullOrEmpty(def.pieceTable))
            {
                PieceTable table = PieceTables.Find(def.pieceTable);
                if (table == null) {
                    Logger.LogWarning($"Piece modification '{piece.key}' sets pieceTable to {def.pieceTable}, which is not a tool that builds pieces. The piece stays where it is. Use the tool's prefab name, such as Hammer, Hoe or Cultivator.");
                } else {
                    PieceTables.MoveTo(piece.key, pcomp.gameObject, table);
                }
            }
            PieceComponents.Apply(piece.key, pcomp.gameObject, def, piece);
        }

        /// <summary>A piece's icon from the textures folder, null when it has none.</summary>
        internal static Sprite LoadIcon(string prefabName)
        {
            if (Icons.TryGetValue(prefabName, out Sprite icon)) { return icon; }
            Sprite[] loaded = TextureTools.LoadIcons(prefabName, null);
            icon = loaded != null && loaded.Length > 0 ? loaded[0] : null;
            Icons[prefabName] = icon;
            return icon;
        }

        private static void Set<T>(ref T field, T? configured) where T : struct
        {
            if (configured.HasValue) { field = configured.Value; }
        }

        private static void DisablePiece(TrackedPiece piece)
        {
            Logger.LogDebug($"Disabling {piece.prefab}");
            Piece pcomp = ResolvePiece(piece.prefab);
            if (pcomp == null) { return; }
            CaptureOriginal(piece.prefab, pcomp);
            pcomp.m_enabled = false;
        }

        public static void BuildPieceTracker()
        {
            TrackedPieces.Clear();
            foreach (KeyValuePair<string, PieceModification> piece in PiecesToModify)
            {
                if (piece.Key == null || piece.Value == null) {  continue; }
                Logger.LogDebug($"Constructing piece modification for {piece.Key}");
                PieceModification def = piece.Value;
                TrackedPiece tpiece = new TrackedPiece { key = piece.Key, action = def.action, prefab = def.prefab, definition = def };

                if (def.action == PieceAction.Add && !string.IsNullOrEmpty(def.prefab) && !PieceCreator.Created.Contains(def.prefab)) {
                    // A piece that already exists was reported when it could not be added
                    if (PrefabManager.Instance.GetPrefab(def.prefab) == null) {
                        Logger.LogWarning($"Piece modification '{piece.Key}' adds {def.prefab}, which this game does not have. Added pieces are made at the main menu from this machine's own files, so it needs the entry there too, and a restart or a trip to the main menu.");
                    }
                    continue;
                }

                Piece tpc = ResolvePiece(def.prefab);
                if (tpc == null) {
                    // ResolvePiece already explained why, just move on to the next entry
                    continue;
                }

                if (def.action == PieceAction.Enable || def.action == PieceAction.Disable) {
                    // We don't need to add any recipes or lookup details about things if we are just enabling or disabling the piece
                    TrackedPieces.Add(tpiece);
                    continue;
                }

                // Left out keeps the cost, an empty list makes the piece free
                if (def.requirements != null) {
                    tpiece.updatedRequirements = BuildRequirements(piece.Key, def.requirements, OriginalResources(def.prefab, tpc));
                    if (def.action == PieceAction.Add && !string.IsNullOrEmpty(def.fromItem)) {
                        tpiece.updatedRequirements = KeepItemRefunded(piece.Key, def.fromItem, tpiece.updatedRequirements);
                    }
                }

                string station = def.RequiredToPlaceCraftingStation;
                if (StationRef.IsNone(station)) {
                    tpiece.stationSet = true;
                    tpiece.RequiredToPlaceCraftingStation = null;
                } else if (!string.IsNullOrEmpty(station)) {
                    CraftingStation craftStation = StationRef.Resolve(station);
                    if (craftStation != null) {
                        tpiece.stationSet = true;
                        tpiece.RequiredToPlaceCraftingStation = craftStation;
                    } else {
                        Logger.LogWarning($"Piece modification '{piece.Key}' sets requiredToPlaceCraftingStation to {station}, which is not a crafting station. The piece keeps the station it has. Use the station's prefab name, such as piece_workbench, or none.");
                    }
                }

                tpiece.category = ResolveCategory(piece.Key, def.PieceCategory);
                CraftingStation placedWith = tpiece.stationSet ? tpiece.RequiredToPlaceCraftingStation : tpc.m_craftingStation;
                if (def.minStationLevel.HasValue && placedWith == null) {
                    Logger.LogWarning($"Piece modification '{piece.Key}' sets minStationLevel, but {def.prefab} needs no crafting station to be placed, so it has no effect. Set requiredToPlaceCraftingStation as well.");
                }
                if (def.blockingPieces != null) { tpiece.blockingPieces = ResolveBlockingPieces(piece.Key, def.blockingPieces); }
                string extensionStation = def.stationExtension?.station;
                if (!string.IsNullOrEmpty(extensionStation)) {
                    tpiece.extensionStation = StationRef.IsNone(extensionStation) ? null : StationRef.Resolve(extensionStation);
                    if (tpiece.extensionStation == null) {
                        Logger.LogWarning($"Piece modification '{piece.Key}' sets stationExtension station to {extensionStation}, which is not a crafting station. A station upgrade always belongs to one, so it keeps its station.");
                    }
                }
                string fuel = def.fireplace?.fuelItem;
                if (!string.IsNullOrEmpty(fuel)) {
                    GameObject fuelPrefab = PrefabManager.Instance.GetPrefab(fuel);
                    tpiece.fireplaceFuel = fuelPrefab != null ? fuelPrefab.GetComponent<ItemDrop>() : null;
                    if (tpiece.fireplaceFuel == null) {
                        Logger.LogWarning($"Piece modification '{piece.Key}' sets fireplace fuelItem to {fuel}, which is not an item. The fire keeps its fuel.");
                    }
                }
                if (def.action == PieceAction.Modify && !string.IsNullOrEmpty(def.clonedFrom)) {
                    Logger.LogWarning($"Piece modification '{piece.Key}' sets clonedFrom on a Modify, only an Add copies a piece. It is ignored.");
                }
                if (def.action == PieceAction.Modify && (!string.IsNullOrEmpty(def.fromItem) || def.mount.HasValue)) {
                    Logger.LogWarning($"Piece modification '{piece.Key}' sets fromItem or mount on a Modify, only an Add makes a piece from an item. They are ignored.");
                }
                // Finally add the tracked piece modifications
                TrackedPieces.Add(tpiece);
            }
            SwitchOffUnconfiguredPieces();
        }

        /// <summary>
        /// A piece added this session that the configuration in use no longer adds, such as one this machine adds and a
        /// server does not, is switched off. Nothing else would know what it is.
        /// </summary>
        private static void SwitchOffUnconfiguredPieces() {
            foreach (string created in PieceCreator.Created) {
                bool configured = false;
                foreach (PieceModification def in PiecesToModify.Values) {
                    if (def != null && def.action == PieceAction.Add && def.prefab == created) { configured = true; break; }
                }
                if (configured) { continue; }
                Piece pcomp = ResolvePiece(created);
                if (pcomp == null) { continue; }
                CaptureOriginal(created, pcomp);
                pcomp.m_enabled = false;
                Logger.LogWarning($"{created} was added this session but the configuration in use does not add it, so it is switched off. A restart removes it.");
            }
        }

        /// <summary>The pieces named, by prefab name. One that is not a piece is left out with a warning.</summary>
        private static List<Piece> ResolveBlockingPieces(string key, List<string> names)
        {
            List<Piece> pieces = new List<Piece>();
            foreach (string name in names) {
                if (string.IsNullOrEmpty(name)) { continue; }
                GameObject go = PrefabManager.Instance.GetPrefab(name);
                Piece blocking = go != null ? go.GetComponent<Piece>() : null;
                if (blocking == null) {
                    Logger.LogWarning($"Piece modification '{key}' lists {name} in blockingPieces, which is not a piece. It is left out.");
                    continue;
                }
                pieces.Add(blocking);
            }
            return pieces;
        }

        private static Piece.Requirement[] BuildRequirements(string key, List<SimpleRequirement> requirements, Piece.Requirement[] original)
        {
            List<Piece.Requirement> updatedreq = new List<Piece.Requirement>();
            foreach (SimpleRequirement req in requirements)
            {
                if (req == null || string.IsNullOrEmpty(req.Prefab)) {
                    Logger.LogWarning($"A requirement on {key} has no prefab set, it will be ignored.");
                    continue;
                }
                GameObject rgo = PrefabManager.Instance.GetPrefab(req.Prefab);
                ItemDrop tid = rgo == null ? null : rgo.GetComponent<ItemDrop>();
                if (tid == null) {
                    Logger.LogWarning($"Could not find an itemDrop for resource with name: {req.Prefab}, this requirement will be ignored.");
                    continue;
                }
                // Left out, the refund setting is the one the piece already has for this item
                Piece.Requirement same = RecipeFields.FindRequirement(original, req.Prefab, RecipeBuilder.RequirementItemName);
                bool recover = req.recover ?? same?.m_recover ?? true;
                updatedreq.Add(new Piece.Requirement() { m_amount = req.amount, m_resItem = tid, m_recover = recover });
                Logger.LogDebug($"Building requirement with res:{tid.name} amount:{req.amount}");
            }
            return updatedreq.ToArray();
        }

        /// <summary>
        /// A piece made from an item always costs at least one of it and gives it back, whatever else the entry lists,
        /// so removing or destroying the piece never loses the item.
        /// </summary>
        private static Piece.Requirement[] KeepItemRefunded(string key, string itemName, Piece.Requirement[] requirements)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(itemName);
            ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            // Reported when the piece was made, and a piece that could not be made is not tracked
            if (item == null) { return requirements; }
            Piece.Requirement listed = Array.Find(requirements, req => req.m_resItem != null && req.m_resItem.name == item.name);
            if (listed == null) {
                Logger.LogInfo($"Piece modification '{key}' is made from {itemName}, which is always part of its cost so the piece can give it back. One is added to its requirements.");
                List<Piece.Requirement> withItem = new List<Piece.Requirement> { ItemPieces.Refunded(item) };
                withItem.AddRange(requirements);
                return withItem.ToArray();
            }
            if (!listed.m_recover || listed.m_amount < 1) {
                Logger.LogInfo($"Piece modification '{key}' is made from {itemName}, which the piece always gives back, so its requirement is set to at least one and refunded.");
                listed.m_recover = true;
                listed.m_amount = Math.Max(1, listed.m_amount);
            }
            return requirements;
        }

        /// <summary>
        /// A build tab by name: one of the game's, one another mod added through Jotunn, or a new one by that name. Null
        /// keeps the piece's own.
        /// </summary>
        private static Piece.PieceCategory? ResolveCategory(string key, string configured)
        {
            if (string.IsNullOrEmpty(configured)) { return null; }
            // Earlier versions used All to mean "leave it alone"
            if (string.Equals(configured, nameof(Piece.PieceCategory.All), StringComparison.OrdinalIgnoreCase)) { return null; }
            if (string.Equals(configured, nameof(Piece.PieceCategory.Max), StringComparison.OrdinalIgnoreCase)) {
                Logger.LogWarning($"Piece modification '{key}' sets pieceCategory to {configured}, which is not a build tab. The piece stays in the tab it is in.");
                return null;
            }
            // The game reads a tab number it does not know past the end of its list of tabs
            if (int.TryParse(configured.Trim(), out _)) {
                Logger.LogWarning($"Piece modification '{key}' sets pieceCategory to {configured}, a number. Build tabs are named, the piece stays in the tab it is in.");
                return null;
            }
            Piece.PieceCategory? category = PieceManager.Instance.GetPieceCategory(configured);
            if (category == null) {
                // A tab of its own, which the build menu lists next to the game's
                category = PieceManager.Instance.AddPieceCategory(configured);
                Logger.LogInfo($"Piece modification '{key}' made the build tab {configured}.");
            }
            return category;
        }

        /// <summary>The requirements the piece shipped with, which is what a requirement left partly unset is completed from.</summary>
        private static Piece.Requirement[] OriginalResources(string prefabName, Piece current)
        {
            return OriginalPieces.TryGetValue(prefabName, out PieceSnapshot snapshot) ? snapshot.Resources : current.m_resources;
        }

        /// <summary>
        /// A copy of every field ModifyPiece, EnablePiece and DisablePiece can write. Holding the Piece
        /// component itself would be useless: it is the same object that gets modified, so it changes
        /// along with the piece.
        /// </summary>
        private class PieceSnapshot
        {
            private Piece.Requirement[] resources;
            private CraftingStation craftingStation;
            private bool allowedInDungeons;
            private bool canBeRemoved;
            private Piece.PieceCategory category;
            private int comfort;
            private Piece.ComfortGroup comfortGroup;
            private float connectRadius;
            private string description;
            private bool enabled;
            private string name;
            private Heightmap.Biome onlyInBiome;
            private bool isUpgrade;
            private bool cultivatedGroundOnly;
            private bool mustBeAboveConnected;
            private float spaceRequirement;
            private bool groundPiece;

            public Piece.Requirement[] Resources => resources;

            public static PieceSnapshot From(Piece pcomp)
            {
                return new PieceSnapshot
                {
                    resources = pcomp.m_resources,
                    craftingStation = pcomp.m_craftingStation,
                    allowedInDungeons = pcomp.m_allowedInDungeons,
                    canBeRemoved = pcomp.m_canBeRemoved,
                    category = pcomp.m_category,
                    comfort = pcomp.m_comfort,
                    comfortGroup = pcomp.m_comfortGroup,
                    connectRadius = pcomp.m_connectRadius,
                    description = pcomp.m_description,
                    enabled = pcomp.m_enabled,
                    name = pcomp.m_name,
                    onlyInBiome = pcomp.m_onlyInBiome,
                    isUpgrade = pcomp.m_isUpgrade,
                    cultivatedGroundOnly = pcomp.m_cultivatedGroundOnly,
                    mustBeAboveConnected = pcomp.m_mustBeAboveConnected,
                    spaceRequirement = pcomp.m_spaceRequirement,
                    groundPiece = pcomp.m_groundPiece
                };
            }

            public void WriteTo(Piece pcomp)
            {
                pcomp.m_resources = resources;
                pcomp.m_craftingStation = craftingStation;
                pcomp.m_allowedInDungeons = allowedInDungeons;
                pcomp.m_canBeRemoved = canBeRemoved;
                pcomp.m_category = category;
                pcomp.m_comfort = comfort;
                pcomp.m_comfortGroup = comfortGroup;
                pcomp.m_connectRadius = connectRadius;
                pcomp.m_description = description;
                pcomp.m_enabled = enabled;
                pcomp.m_name = name;
                pcomp.m_onlyInBiome = onlyInBiome;
                pcomp.m_isUpgrade = isUpgrade;
                pcomp.m_cultivatedGroundOnly = cultivatedGroundOnly;
                pcomp.m_mustBeAboveConnected = mustBeAboveConnected;
                pcomp.m_spaceRequirement = spaceRequirement;
                pcomp.m_groundPiece = groundPiece;
            }
        }
    }
}

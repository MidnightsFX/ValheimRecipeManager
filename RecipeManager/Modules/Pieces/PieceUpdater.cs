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

        public static void InitialSychronization() {
            BuildPieceTracker();
            PieceUpdateRunner();
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
            // Might need to do this for all pieces with the full name
            Piece pcomp = ResolvePiece(piece.prefab);
            if (pcomp == null) { return; }
            CaptureOriginal(piece.prefab, pcomp);
            pcomp.m_enabled = true;
        }

        private static void ModifyPiece(TrackedPiece piece)
        {
            Logger.LogDebug($"Modifying {piece.prefab}");
            // Might need to do this for all pieces with the full name, including clones
            Piece pcomp = ResolvePiece(piece.prefab);
            CaptureOriginal(piece.prefab, pcomp);
            if (pcomp != null) 
            {
                if (piece.updatedRequirements != null)
                {
                    pcomp.m_resources = piece.updatedRequirements;
                }
                pcomp.m_craftingStation = piece.RequiredToPlaceCraftingStation;
                pcomp.m_allowedInDungeons = piece.AllowedInDungeon;
                pcomp.m_canBeRemoved = piece.CanBeDeconstructed;
                pcomp.m_category = piece.PieceCategory;
                if (piece.ComfortAmount != -1)
                {
                    pcomp.m_comfort = piece.ComfortAmount;
                }
                if (piece.ComfortGroup != Piece.ComfortGroup.None)
                {
                    pcomp.m_comfortGroup = piece.ComfortGroup;
                }
                if (piece.CraftingStationConnectionRadius != -1)
                {
                    pcomp.m_connectRadius = piece.CraftingStationConnectionRadius;
                }
                if (piece.PieceDescription != null)
                {
                    pcomp.m_description = piece.PieceDescription;
                }
                pcomp.m_enabled = piece.EnablePiece;

                if (piece.PieceName != null) {
                    pcomp.m_name = piece.PieceName;
                }
                pcomp.m_onlyInBiome = piece.OnlyInBiome;
                pcomp.m_isUpgrade = piece.IsUpgradeForStation;
                pcomp.m_cultivatedGroundOnly = piece.CultivatedGroundOnly;
                pcomp.m_mustBeAboveConnected = piece.MustBeAvobeConnectedStation;
                if (piece.SpaceRequired != -1)
                {
                    pcomp.m_spaceRequirement = piece.SpaceRequired;
                }
                pcomp.m_groundPiece = piece.GroundPlacement;
                // pcomp
            }
        }

        private static void DisablePiece(TrackedPiece piece)
        {
            Logger.LogDebug($"Disabling {piece.prefab}");
            // Might need to do this for all pieces with the full name
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
                if (piece.Key == null) {  continue; }
                Logger.LogInfo($"Constructing piece modification for {piece.Key}");
                TrackedPiece tpiece = new TrackedPiece();
                tpiece.action = piece.Value.action;
                tpiece.prefab = piece.Value.prefab;

                Piece tpc = ResolvePiece(piece.Value.prefab);
                if (tpc == null) {
                    // ResolvePiece already explained why, just move on to the next entry
                    continue;
                }

                if (piece.Value.action == PieceAction.Enable || piece.Value.action == PieceAction.Disable) {
                    // We don't need to add any recipes or lookup details about things if we are just enabling or disabling the piece
                    TrackedPieces.Add(tpiece);
                    continue;
                }

                // No requirements block means the build cost is not being changed, leave it alone
                if (piece.Value.requirements == null) {
                    tpiece.updatedRequirements = null;
                } else {
                    List<Piece.Requirement> updatedreq = new List<Piece.Requirement>();
                    foreach(var req in piece.Value.requirements)
                    {
                        if (req == null || string.IsNullOrEmpty(req.Prefab)) {
                            Logger.LogWarning($"A requirement on {piece.Key} has no prefab set, it will be ignored.");
                            continue;
                        }
                        GameObject rgo = PrefabManager.Instance.GetPrefab(req.Prefab);
                        ItemDrop tid = rgo == null ? null : rgo.GetComponent<ItemDrop>();
                        if (tid == null) {
                            Logger.LogWarning($"Could not find an itemDrop for resource with name: {req.Prefab}, this requirement will be ignored.");
                            continue;
                        }
                        updatedreq.Add(new Piece.Requirement() { m_amount = req.amount, m_resItem = tid, m_recover = req.recover });
                        Logger.LogDebug($"Building requirement with res:{tid.name} amount:{req.amount}");
                    }
                    tpiece.updatedRequirements = updatedreq.ToArray();
                }

                if (piece.Value.RequiredToPlaceCraftingStation != null && piece.Value.RequiredToPlaceCraftingStation != "")
                {
                    if (piece.Value.RequiredToPlaceCraftingStation.ToLower() == "none") {
                        tpiece.RequiredToPlaceCraftingStation = null;
                    } else {
                        CraftingStation craftStation = PrefabManager.Instance.GetPrefab(piece.Value.RequiredToPlaceCraftingStation)?.GetComponent<CraftingStation>();
                        if (craftStation != null) {
                            tpiece.RequiredToPlaceCraftingStation = craftStation;
                        }  else {
                            Logger.LogWarning($"Could not link required crafting station, are you sure a crafting station exists with piecename: {piece.Value.RequiredToPlaceCraftingStation}");
                        }
                    }
                } else {
                    // If we do not have the crafting station set, it should be set to its current- this ensures it is not modified but that we support nullifying it
                    tpiece.RequiredToPlaceCraftingStation = tpc.m_craftingStation;
                }

                tpiece.IsUpgradeForStation = piece.Value.IsUpgradeForStation;
                tpiece.AllowedInDungeon = piece.Value.AllowedInDungeon;
                tpiece.CanBeDeconstructed = piece.Value.CanBeDeconstructed;
                tpiece.ComfortAmount = piece.Value.ComfortAmount;
                tpiece.ComfortGroup = piece.Value.ComfortGroup;
                tpiece.CraftingStationConnectionRadius = piece.Value.CraftingStationConnectionRadius;
                tpiece.PieceDescription = piece.Value.PieceDescription;
                tpiece.PieceName = piece.Value.PieceName;
                tpiece.EnablePiece = piece.Value.EnablePiece;
                tpiece.CultivatedGroundOnly = piece.Value.CultivatedGroundOnly;
                tpiece.MustBeAvobeConnectedStation = piece.Value.MustBeAvobeConnectedStation;
                tpiece.SpaceRequired = piece.Value.SpaceRequired;
                tpiece.GroundPlacement = piece.Value.GroundPlacement;
                if (piece.Value.PieceCategory != Piece.PieceCategory.All) {
                    tpiece.PieceCategory = piece.Value.PieceCategory;
                } else {
                    tpiece.PieceCategory = tpc.m_category;
                }
                // Finally add the tracked piece modifications
                TrackedPieces.Add(tpiece);
            }
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

        public static void UpdatePieceModificationsFromList(List<PieceModificationCollection> lPieceMods)
        {
            PiecesToModify.Clear();
            foreach (PieceModificationCollection rcol in lPieceMods)
            {
                foreach (KeyValuePair<String, PieceModification> entry in rcol.PieceModifications)
                {
                    PiecesToModify.Add(entry.Key, entry.Value);
                }
            }
        }

        public static void UpdatePieceModificationsFromRPC(string rpcRecieved)
        {
            Logger.LogInfo($"RPC Recieved: {rpcRecieved}");
            Logger.LogInfo($"RPC Data: {rpcRecieved.Length}");
            var pieceData = ValConfig.yamldeserializer.Deserialize<PieceModificationCollection>(rpcRecieved);
            UpdatePieceModifications(pieceData);
        }

        public static void UpdatePieceModifications(PieceModificationCollection PieceMods)
        {
            PiecesToModify.Clear();
            foreach (KeyValuePair<String, PieceModification> entry in PieceMods.PieceModifications)
            {
                PiecesToModify.Add(entry.Key, entry.Value);
            }
        }
    }
}

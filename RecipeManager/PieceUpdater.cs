using Jotunn;
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

        public static void RevertPieceModifications()
        {
            foreach (TrackedPiece piece in TrackedPieces)
            {
                RevertPiece(piece);
            }
        }

        public static void EnablePiece(TrackedPiece piece)
        {
            Logger.LogDebug($"Enabling {piece.prefab}");
            // Might need to do this for all pieces with the full name
            GameObject go = PrefabManager.Instance.GetPrefab(piece.prefab);
            go.GetComponent<Piece>().m_enabled = true;
        }

        private static void RevertPiece(TrackedPiece tpiece)
        {
            GameObject go = PrefabManager.Instance.GetPrefab(tpiece.prefab);
            Piece pcomp = go.GetComponent<Piece>();
            pcomp = tpiece.originalPiece;
        }

        private static void ModifyPiece(TrackedPiece piece)
        {
            Logger.LogDebug($"Modifying {piece.prefab}");
            // Might need to do this for all pieces with the full name, including clones
            GameObject go = PrefabManager.Instance.GetPrefab(piece.prefab);
            Piece pcomp = go.GetComponent<Piece>();
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
            Logger.LogDebug($"Modifying {piece.prefab}");
            // Might need to do this for all pieces with the full name
            GameObject go = PrefabManager.Instance.GetPrefab(piece.prefab);
            go.GetComponent<Piece>().m_enabled = false;
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

                try {
                    GameObject tgo = PrefabManager.Instance.GetPrefab(piece.Value.prefab);
                    Piece tpc = tgo.GetComponent<Piece>();
                    tpiece.originalPiece = tpc;
                } catch (Exception) {
                    Logger.LogWarning($"Could not find entries referenced piece, this modification will be skipped. Define a prefab to modify to fix this.");
                }

                if (piece.Value.action == PieceAction.Enable || piece.Value.action == PieceAction.Disable) {
                    // We don't need to add any recipes or lookup details about things if we are just enabling or disabling the piece
                    TrackedPieces.Add(tpiece);
                    continue;
                }

                List<Piece.Requirement> updatedreq = new List<Piece.Requirement>();
                foreach(var req in piece.Value.requirements)
                {
                    
                    try {
                        GameObject rgo = PrefabManager.Instance.GetPrefab(req.Prefab);
                        ItemDrop tid = rgo.GetComponent<ItemDrop>();
                        updatedreq.Add(new Piece.Requirement() { m_amount = req.amount, m_resItem = tid });
                        Logger.LogInfo($"Building requirement with res:{tid.name} amount:{req.amount}");
                    } catch {
                        Logger.LogWarning($"Could not find an itemDrop for resource with name: {req.Prefab}");
                    }
                }
                tpiece.updatedRequirements = updatedreq.ToArray();

                if (piece.Value.RequiredToPlaceCraftingStation != null && piece.Value.RequiredToPlaceCraftingStation != "")
                {
                    if (piece.Value.RequiredToPlaceCraftingStation.ToLower() == "none") {
                        tpiece.RequiredToPlaceCraftingStation = null;
                    } else {
                        CraftingStation craftStation = PrefabManager.Instance.GetPrefab(piece.Value.RequiredToPlaceCraftingStation)?.GetComponent<CraftingStation>();
                        if (craftStation != null) {
                            tpiece.RequiredToPlaceCraftingStation = craftStation;
                        }  else {
                            Logger.LogWarning($"Could not link required crafting station, are you sure a crafting station exists with piecename: {craftStation}");
                        }
                    }
                } else {
                    // If we do not have the crafting station set, it should be set to its current- this ensures it is not modified but that we support nullifying it
                    tpiece.RequiredToPlaceCraftingStation = PrefabManager.Instance.GetPrefab(piece.Value.prefab).GetComponent<Piece>().m_craftingStation; ;
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
                    tpiece.PieceCategory = tpiece.originalPiece.m_category;
                }
                // Finally add the tracked piece modifications
                TrackedPieces.Add(tpiece);
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
            //var pieceData = Config.yamldeserializer.Deserialize<PieceModificationCollection>(rpcRecieved);
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

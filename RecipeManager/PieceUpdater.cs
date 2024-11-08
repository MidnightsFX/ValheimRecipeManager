using Jotunn;
using Jotunn.Managers;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = Jotunn.Logger;

namespace RecipeManager
{
    internal class PieceUpdater
    {
        public static Dictionary<String, PieceModification> PiecesToModify = new Dictionary<String, PieceModification>();
        public static List<TrackedPiece> TrackedPieces = new List<TrackedPiece>();

        public static void InitialSychronization()
        {
            BuildPieceTracker();
            PieceUpdateRunner();
        }

        public static void PieceUpdateRunner()
        {
            foreach (TrackedPiece piece in TrackedPieces) 
            {
                ApplyPieceModifications(piece);   
            }
        }

        public static void ApplyPieceModifications(TrackedPiece piece)
        {
            switch (piece.action) {
                case PieceAction.Disable:
                    DisablePiece(piece.prefab);
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
            // Might need to do this for all pieces with the full name, including clones
            GameObject go = PrefabManager.Instance.GetPrefab(piece.prefab);
            Piece pcomp = go.GetComponent<Piece>();
            if (pcomp != null) 
            {
                if (piece.updatedRequirements != null)
                {
                    pcomp.m_resources = piece.updatedRequirements;
                }
                if (piece.RequiredToPlaceCraftingStation != null)
                {
                    pcomp.m_craftingStation = piece.RequiredToPlaceCraftingStation;
                }
                pcomp.m_allowedInDungeons = piece.AllowedInDungeon;
                pcomp.m_canBeRemoved = piece.CanBeDeconstructed;
                if (piece.PieceCategory != Piece.PieceCategory.All)
                {
                    pcomp.m_category = piece.PieceCategory;
                }
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

        private static void DisablePiece(string piece)
        {
            // Might need to do this for all pieces with the full name
            GameObject go = PrefabManager.Instance.GetPrefab(piece);
            go.GetComponent<Piece>().m_enabled = false;
        }

        public static void BuildPieceTracker()
        {
            TrackedPieces.Clear();
            foreach (KeyValuePair<string, PieceModification> piece in PiecesToModify)
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Constructing piece requirements for {piece.Key}"); }
                TrackedPiece tpiece = new TrackedPiece();
                tpiece.action = piece.Value.action;
                tpiece.prefab = piece.Value.prefab;

                GameObject tgo = PrefabManager.Instance.GetPrefab(piece.Value.prefab);
                Piece tpc = tgo.GetComponent<Piece>();
                tpiece.originalPiece = tpc;

                Piece.Requirement[] updatedreq = new Piece.Requirement[piece.Value.requirements.Count()];
                int index = 0;
                foreach(var req in piece.Value.requirements)
                {
                    try {
                        GameObject rgo = PrefabManager.Instance.GetPrefab(req.Prefab);
                        ItemDrop tid = rgo.GetComponent<ItemDrop>();
                        updatedreq[index] = new Piece.Requirement() { m_amount = req.amount, m_resItem = tid };
                    } catch {
                        Logger.LogWarning($"Could not find an itemDrop for resource with name: {req.Prefab}");
                    }
                }
                tpiece.updatedRequirements = updatedreq;

                if (piece.Value.RequiredToPlaceCraftingStation != null && piece.Value.RequiredToPlaceCraftingStation != "")
                {
                    CraftingStation craftStation = PrefabManager.Instance.GetPrefab(piece.Value.RequiredToPlaceCraftingStation)?.GetComponent<CraftingStation>();
                    if (craftStation != null)
                    {
                        tpiece.RequiredToPlaceCraftingStation = craftStation;
                    }
                    else
                    {
                        Logger.LogWarning($"Could not link required crafting station, are you sure a crafting station exists with piecename: {craftStation}");
                    }
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
            }
        }

        public static void UpdateRecipeModificationsFromList(List<PieceModificationCollection> lPieceMods)
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

        public static void UpdateRecipeModifications(PieceModificationCollection PieceMods)
        {
            PiecesToModify.Clear();
            foreach (KeyValuePair<String, PieceModification> entry in PieceMods.PieceModifications)
            {
                PiecesToModify.Add(entry.Key, entry.Value);
            }
        }
    }
}

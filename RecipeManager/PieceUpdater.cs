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
            }
        }

        private static void ModifyPiece(TrackedPiece piece)
        {
            // Might need to do this for all pieces with the full name
            GameObject go = PrefabManager.Instance.GetPrefab(piece.prefab);
            Piece pcomp = go.GetComponent<Piece>();
            if (pcomp != null) 
            {
                pcomp.m_resources = piece.updatedRequirements;
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
            }
        }
    }
}

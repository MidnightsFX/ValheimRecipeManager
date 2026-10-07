using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    /// <summary>
    /// Which tool each piece is built with. A tool's piece table is a list of piece prefabs that outlives the world, so
    /// where a piece was is recorded before it is first moved and put back on every revert.
    /// </summary>
    internal static class PieceTables
    {
        /// <summary>The tables each moved piece was in, and where in them, before it was first moved.</summary>
        private static readonly Dictionary<string, List<(PieceTable table, int index)>> OriginalMembership = new Dictionary<string, List<(PieceTable table, int index)>>();
        /// <summary>The moved pieces in the order they were first moved, which reverting goes back through.</summary>
        private static readonly List<string> MoveOrder = new List<string>();

        /// <summary>A table by its tool's prefab name (Hammer) or its own name (_HammerPieceTable), in any case.</summary>
        internal static PieceTable Find(string name)
        {
            if (string.IsNullOrEmpty(name)) { return null; }
            try { return PieceManager.Instance.GetPieceTable(CanonicalName(name) ?? name); }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// A tool's prefab name or a table's name as the game spells it, matched in any case, or null for a name that is
        /// neither. Looked up among everything loaded, so it works at the main menu too.
        /// </summary>
        internal static string CanonicalName(string configured)
        {
            if (string.IsNullOrEmpty(configured)) { return null; }
            foreach (UnityEngine.Object found in PrefabManager.Cache.GetPrefabs(typeof(PieceTable)).Values)
            {
                if (found is PieceTable table && string.Equals(table.name, configured, StringComparison.OrdinalIgnoreCase)) { return table.name; }
            }
            foreach (UnityEngine.Object found in PrefabManager.Cache.GetPrefabs(typeof(ItemDrop)).Values)
            {
                if (found is ItemDrop item && string.Equals(item.name, configured, StringComparison.OrdinalIgnoreCase)
                    && item.m_itemData?.m_shared?.m_buildPieces != null)
                {
                    return item.name;
                }
            }
            return null;
        }

        /// <summary>The name of the first tool whose table holds the piece, null when no tool has it.</summary>
        internal static string ToolOf(GameObject piece)
        {
            if (piece == null || ObjectDB.instance == null) { return null; }
            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                PieceTable table = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
                if (table != null && table.m_pieces.Contains(piece)) { return item.name; }
            }
            return null;
        }

        /// <summary>Each piece's tool, the first one that builds it, for looking many up at once.</summary>
        internal static Dictionary<GameObject, string> ToolsByPiece()
        {
            Dictionary<GameObject, string> tools = new Dictionary<GameObject, string>();
            if (ObjectDB.instance == null) { return tools; }
            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                PieceTable table = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
                if (table == null || table.m_pieces == null) { continue; }
                foreach (GameObject piece in table.m_pieces)
                {
                    if (piece != null && !tools.ContainsKey(piece)) { tools.Add(piece, item.name); }
                }
            }
            return tools;
        }

        /// <summary>The name of a table that holds the piece, found among everything loaded, so it works at the main menu too.</summary>
        internal static string TableNameOf(GameObject piece)
        {
            if (piece == null) { return null; }
            foreach (UnityEngine.Object found in PrefabManager.Cache.GetPrefabs(typeof(PieceTable)).Values)
            {
                if (found is PieceTable table && table.m_pieces != null && table.m_pieces.Contains(piece)) { return table.name; }
            }
            return null;
        }

        /// <summary>Puts the piece in the table and takes it out of every other one.</summary>
        internal static void MoveTo(string key, GameObject piece, PieceTable target)
        {
            if (piece == null || target == null) { return; }
            List<PieceTable> holding = Holding(piece);
            if (holding.Count == 1 && holding[0] == target) { return; }
            Record(piece, holding);
            foreach (PieceTable table in holding) { table.m_pieces.RemoveAll(entry => entry == piece); }
            target.m_pieces.Add(piece);
            Logger.LogDebug($"'{key}' moved {piece.name} to {target.name}.");
        }

        /// <summary>
        /// Puts every moved piece back in the tables it was in. Each was recorded with the pieces moved before it already
        /// gone, so they go back in the reverse order.
        /// </summary>
        internal static void RevertAll()
        {
            List<GameObject> pieces = MoveOrder.Select(name => PrefabManager.Instance.GetPrefab(name)).ToList();
            foreach (GameObject piece in pieces)
            {
                if (piece == null) { continue; }
                foreach (PieceTable table in Holding(piece)) { table.m_pieces.RemoveAll(entry => entry == piece); }
            }
            for (int i = MoveOrder.Count - 1; i >= 0; i--)
            {
                if (pieces[i] == null) { continue; }
                foreach ((PieceTable table, int index) in OriginalMembership[MoveOrder[i]])
                {
                    if (table == null) { continue; }
                    table.m_pieces.Insert(Mathf.Clamp(index, 0, table.m_pieces.Count), pieces[i]);
                }
            }
        }

        private static void Record(GameObject piece, List<PieceTable> holding)
        {
            if (OriginalMembership.ContainsKey(piece.name)) { return; }
            List<(PieceTable table, int index)> membership = new List<(PieceTable table, int index)>();
            foreach (PieceTable table in holding) { membership.Add((table, table.m_pieces.IndexOf(piece))); }
            OriginalMembership.Add(piece.name, membership);
            MoveOrder.Add(piece.name);
        }

        private static List<PieceTable> Holding(GameObject piece)
        {
            List<PieceTable> holding = new List<PieceTable>();
            foreach (PieceTable table in PieceManager.Instance.GetPieceTables())
            {
                if (table != null && table.m_pieces != null && table.m_pieces.Contains(piece) && !holding.Contains(table)) { holding.Add(table); }
            }
            return holding;
        }
    }
}

using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;
using Object = UnityEngine.Object;

namespace RecipeManager
{
    /// <summary>
    /// Makes the pieces Pieces.yaml adds, by copying an existing piece. A piece has to be known to the game before a world
    /// loads, so they are made at the main menu, once for the whole session, and everything else an Add entry sets is
    /// applied with the modifications. Adding or removing one needs a restart.
    /// </summary>
    internal static class PieceCreator
    {
        /// <summary>Pieces made this session, by prefab name.</summary>
        internal static readonly HashSet<string> Created = new HashSet<string>();

        /// <summary>Hooked to the main menu, after the local configs are loaded.</summary>
        internal static void CreateCustomPieces()
        {
            foreach (KeyValuePair<string, PieceModification> entry in PieceUpdater.PiecesToModify)
            {
                PieceModification def = entry.Value;
                if (def == null || def.action != PieceAction.Add) { continue; }
                if (string.IsNullOrEmpty(def.prefab) || Created.Contains(def.prefab)) { continue; }
                try { Create(entry.Key, def); }
                catch (System.Exception ex) { Logger.LogError($"Piece '{entry.Key}' could not be added. Error: {ex}"); }
            }
        }

        private static void Create(string key, PieceModification def)
        {
            if (string.IsNullOrEmpty(def.clonedFrom))
            {
                Logger.LogError($"Piece '{key}' adds {def.prefab} but sets no clonedFrom, the piece to copy. Skipping.");
                return;
            }
            if (PrefabManager.Instance.GetPrefab(def.prefab) != null)
            {
                Logger.LogError($"Piece '{key}' wants to add {def.prefab}, but a prefab with that name already exists. Pick a new name, or change it with 'action: Modify'. Skipping.");
                return;
            }
            GameObject source = PrefabManager.Instance.GetPrefab(def.clonedFrom);
            Piece sourcePiece = source != null ? source.GetComponent<Piece>() : null;
            if (sourcePiece == null)
            {
                Logger.LogError($"Piece '{key}' copies {def.clonedFrom}, which {(source == null ? "does not exist" : "is not a piece")}. Skipping.");
                return;
            }
            // Jotunn looks the table up by its exact name when the world loads, and drops the piece for good if it cannot
            string table = PieceTables.CanonicalName(def.pieceTable);
            if (table == null && def.pieceTable != null)
            {
                Logger.LogWarning($"Piece '{key}' sets pieceTable to {def.pieceTable}, which is not a tool that builds pieces. Use the tool's prefab name, such as Hammer, Hoe or Cultivator.");
            }
            table = table ?? PieceTables.ToolOf(source) ?? PieceTables.TableNameOf(source);
            if (table == null)
            {
                table = "Hammer";
                Logger.LogWarning($"{def.clonedFrom} is in no tool, so {def.prefab} is built with the Hammer.");
            }
            // The game only lists a piece with an icon
            Sprite icon = PieceUpdater.LoadIcon(def.prefab) ?? sourcePiece.m_icon;
            if (icon == null)
            {
                Logger.LogError($"Piece '{key}' copies {def.clonedFrom}, which has no icon, and a piece needs one to be built. Put one at Textures/{def.prefab}/icon.png. Skipping.");
                return;
            }

            GameObject clone = PrefabManager.Instance.CreateClonedPrefab(def.prefab, source);
            clone.GetComponent<Piece>().m_icon = icon;
            // The config's own Enabled and AllowedInDungeons would be written over the copy's
            PieceConfig config = new PieceConfig
            {
                PieceTable = table,
                Enabled = sourcePiece.m_enabled,
                AllowedInDungeons = sourcePiece.m_allowedInDungeons
            };
            if (!PieceManager.Instance.AddPiece(new CustomPiece(clone, false, config)))
            {
                Logger.LogError($"Piece '{key}' could not register {def.prefab}.");
                // Left behind, it would be found as a prefab that already exists the next time
                Object.Destroy(clone);
                return;
            }
            Created.Add(def.prefab);
            Logger.LogInfo($"Created piece {def.prefab} from {def.clonedFrom}, built with {table}.");
        }
    }
}

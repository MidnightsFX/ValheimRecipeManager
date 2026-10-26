using Jotunn.Managers;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;
using Object = UnityEngine.Object;

namespace RecipeManager
{
    /// <summary>
    /// Makes a piece out of an item's display model, the one an item stand shows. The piece is a copy of an item stand
    /// with the stand's own model taken away and the item's put where the stand would hold it, so it is posed the same
    /// way and keeps the stand's health, sounds and the small collider that rests it against a wall or the ground, which
    /// is what it is supported by. Its cost is the item, refunded, which is what gives the item back when the piece is
    /// removed or destroyed.
    /// </summary>
    internal static class ItemPieces
    {
        private const string WallStand = "itemstand";
        private const string FloorStand = "itemstandh";

        /// <summary>
        /// The prepared copy, not yet registered, and the stand it was copied from, which decides the tool it is built
        /// with when the entry sets none. Null when it cannot be made, which is logged.
        /// </summary>
        internal static GameObject Build(string key, PieceModification def, out GameObject source)
        {
            string standName = def.mount == ItemMount.Floor ? FloorStand : WallStand;
            source = PrefabManager.Instance.GetPrefab(standName);
            ItemStand sourceStand = source != null ? source.GetComponent<ItemStand>() : null;
            if (sourceStand == null || sourceStand.m_attachOther == null || source.GetComponent<Piece>() == null)
            {
                Logger.LogError($"Piece '{key}' is made from an item, which needs the game's {standName} to copy, and it could not be found. Skipping.");
                return null;
            }
            GameObject itemPrefab = PrefabManager.Instance.GetPrefab(def.fromItem);
            ItemDrop item = itemPrefab != null ? itemPrefab.GetComponent<ItemDrop>() : null;
            if (item == null)
            {
                Logger.LogError($"Piece '{key}' is made from {def.fromItem}, which {(itemPrefab == null ? "does not exist" : "is not an item")}. Skipping.");
                return null;
            }
            GameObject attachPrefab = ItemStand.GetAttachPrefab(itemPrefab);
            if (attachPrefab == null)
            {
                Logger.LogError($"Piece '{key}' is made from {def.fromItem}, which has no display model, the one an item stand shows. Skipping.");
                return null;
            }
            ItemDrop.ItemData data = item.m_itemData;
            Sprite[] icons = data.m_shared.m_icons;
            Sprite icon = PieceUpdater.LoadIcon(def.prefab) ?? (icons != null && icons.Length > 0 ? icons[0] : null);
            if (icon == null)
            {
                Logger.LogError($"Piece '{key}' is made from {def.fromItem}, which has no icon, and a piece needs one to be built. Put one at Textures/{def.prefab}/icon.png. Skipping.");
                return null;
            }
            if (data.m_shared.m_maxQuality > 1 || data.m_shared.m_useDurability)
            {
                Logger.LogWarning($"Piece '{key}' is made from {def.fromItem}, which has upgrade levels or durability. Removing the piece gives back a new one at level 1.");
            }

            GameObject clone = PrefabManager.Instance.CreateClonedPrefab(def.prefab, source);
            ItemStand stand = clone.GetComponent<ItemStand>();
            Transform holder = stand.m_attachOther;
            // The pose is the first one the stand would show the item in, read before the stand is taken off
            List<ItemStand.OrientationSettings> poses = new List<ItemStand.OrientationSettings>();
            stand.GetOrientationSettings(data, ref poses);
            ItemStand.OrientationSettings pose = poses[0];

            // The stand's model goes, but not the objects holding it, the wear states switch them and none can be missing
            foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>(true)) { Object.DestroyImmediate(renderer); }
            foreach (MeshFilter filter in clone.GetComponentsInChildren<MeshFilter>(true)) { Object.DestroyImmediate(filter); }
            LODGroup lod = clone.GetComponent<LODGroup>();
            if (lod != null) { Object.DestroyImmediate(lod); }
            Object.DestroyImmediate(stand);

            // Placed the way ItemStand.SetVisualItem places it
            GameObject model = Object.Instantiate(ItemStand.GetAttachGameObject(attachPrefab), holder);
            model.name = itemPrefab.name;
            model.transform.localPosition = attachPrefab.transform.localPosition + pose.m_position;
            model.transform.localRotation = attachPrefab.transform.localRotation * Quaternion.Euler(pose.m_rotation);
            model.transform.localScale = Vector3.Scale(Vector3.Scale(attachPrefab.transform.localScale, data.GetScale(1f)), pose.m_scale);
            // An item's colliders are on the item layer, which the hammer cannot aim at. Not solid, so big trophies do not
            // get in the way, and the stand's own collider still does the supporting.
            int nonSolid = LayerMask.NameToLayer("piece_nonsolid");
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) { collider.gameObject.layer = nonSolid; }

            // The entry's own settings are applied over these with the modifications, and a revert comes back to them
            Piece piece = clone.GetComponent<Piece>();
            piece.m_name = data.m_shared.m_name;
            piece.m_description = data.m_shared.m_description;
            piece.m_icon = icon;
            piece.m_resources = new[] { Refunded(item) };
            return clone;
        }

        /// <summary>One of the item, given back when the piece is removed or destroyed.</summary>
        internal static Piece.Requirement Refunded(ItemDrop item)
        {
            return new Piece.Requirement { m_resItem = item, m_amount = 1, m_recover = true };
        }
    }
}

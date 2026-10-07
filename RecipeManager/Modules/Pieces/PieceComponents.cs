using RecipeManager.Common;
using System;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    /// <summary>
    /// The settings of the parts a piece is built from besides its Piece: how it wears, and what it does as a station,
    /// station upgrade, container or fire. Each part is recorded before its first change and put back on every revert.
    /// A change reaches pieces already built the next time their area loads.
    /// </summary>
    internal static class PieceComponents
    {
        private static readonly string[] WearFields =
        {
            "m_health", "m_materialType", "m_noRoofWear", "m_noSupportWear", "m_supports", "m_triggerPrivateArea", "m_burnable",
            "m_minToolTier", "m_ashDamageImmune", "m_ashDamageResist", "m_snowDamageImmune", "m_damages"
        };
        private static readonly string[] StationFields =
        {
            "m_name", "m_discoverRange", "m_rangeBuild", "m_extraRangePerLevel", "m_useDistance", "m_craftRequireRoof", "m_craftRequireFire",
            "m_showBasicRecipies", "m_canRepair"
        };
        private static readonly string[] ExtensionFields = { "m_craftingStation", "m_maxStationDistance", "m_stack", "m_continousConnection" };
        private static readonly string[] ContainerFields = { "m_width", "m_height", "m_checkGuardStone", "m_autoDestroyEmpty" };
        private static readonly string[] FireplaceFields =
        {
            "m_startFuel", "m_maxFuel", "m_secPerFuel", "m_infiniteFuel", "m_fuelItem", "m_canTurnOff", "m_canRefill",
            "m_igniteInterval", "m_igniteChance", "m_igniteSpread"
        };

        /// <summary>Each part this mod has written to, by the piece's prefab name, with what it held before.</summary>
        private static readonly Dictionary<string, List<(Component part, FieldSnapshot original)>> Originals = new Dictionary<string, List<(Component part, FieldSnapshot original)>>();

        internal static void RevertAll()
        {
            foreach (KeyValuePair<string, List<(Component part, FieldSnapshot original)>> piece in Originals)
            {
                foreach ((Component part, FieldSnapshot original) in piece.Value)
                {
                    if (part != null) { original.WriteTo(part); }
                }
            }
        }

        internal static void Apply(string key, GameObject prefab, PieceModification def, TrackedPiece tracked)
        {
            if (def.wearNTear != null) { ApplyWear(key, prefab, def.wearNTear); }
            // A copy of a station would otherwise be the same station as its original, recipes and upgrades go by the name
            bool ownStation = def.action == PieceAction.Add && prefab.GetComponent<CraftingStation>() != null;
            if (def.station != null || ownStation) { ApplyStation(key, prefab, def, ownStation); }
            if (def.stationExtension != null) { ApplyExtension(key, prefab, def.stationExtension, tracked); }
            if (def.container != null) { ApplyContainer(key, prefab, def.container); }
            if (def.fireplace != null) { ApplyFireplace(key, prefab, def.fireplace, tracked); }
        }

        private static T Part<T>(string key, GameObject prefab, string block, string[] fields) where T : Component
        {
            T part = prefab.GetComponent<T>();
            if (part == null)
            {
                Logger.LogWarning($"Piece modification '{key}' sets {block}, but {prefab.name} has no {typeof(T).Name}. Those settings are skipped.");
                return null;
            }
            if (!Originals.TryGetValue(prefab.name, out List<(Component part, FieldSnapshot original)> parts))
            {
                parts = new List<(Component part, FieldSnapshot original)>();
                Originals.Add(prefab.name, parts);
            }
            if (!parts.Exists(entry => entry.part == part)) { parts.Add((part, FieldSnapshot.Of(part, fields))); }
            return part;
        }

        private static void ApplyWear(string key, GameObject prefab, WearNTearSettings s)
        {
            WearNTear wear = Part<WearNTear>(key, prefab, "wearNTear", WearFields);
            if (wear == null) { return; }
            Set(ref wear.m_health, s.health);
            Set(ref wear.m_materialType, s.materialType);
            Set(ref wear.m_noRoofWear, s.noRoofWear);
            Set(ref wear.m_noSupportWear, s.noSupportWear);
            Set(ref wear.m_supports, s.supports);
            Set(ref wear.m_triggerPrivateArea, s.triggerPrivateArea);
            Set(ref wear.m_burnable, s.burnable);
            Set(ref wear.m_minToolTier, s.minToolTier);
            Set(ref wear.m_ashDamageImmune, s.ashDamageImmune);
            Set(ref wear.m_ashDamageResist, s.ashDamageResist);
            Set(ref wear.m_snowDamageImmune, s.snowDamageImmune);
            if (s.resistances != null)
            {
                HitData.DamageModifiers damages = wear.m_damages;
                foreach (DamageResistance resistance in s.resistances)
                {
                    if (resistance == null) { continue; }
                    if (!SetResistance(ref damages, resistance.type, resistance.modifier))
                    {
                        Logger.LogWarning($"Piece modification '{key}' gives {prefab.name} a resistance to {resistance.type} damage, which pieces have none of. It is skipped.");
                    }
                }
                wear.m_damages = damages;
            }
        }

        /// <summary>Sets one damage type's modifier, unlike the game's own Apply, which only ever makes it better.</summary>
        internal static bool SetResistance(ref HitData.DamageModifiers damages, HitData.DamageType type, HitData.DamageModifier modifier)
        {
            switch (type)
            {
                case HitData.DamageType.Blunt: damages.m_blunt = modifier; return true;
                case HitData.DamageType.Slash: damages.m_slash = modifier; return true;
                case HitData.DamageType.Pierce: damages.m_pierce = modifier; return true;
                case HitData.DamageType.Chop: damages.m_chop = modifier; return true;
                case HitData.DamageType.Pickaxe: damages.m_pickaxe = modifier; return true;
                case HitData.DamageType.Fire: damages.m_fire = modifier; return true;
                case HitData.DamageType.Frost: damages.m_frost = modifier; return true;
                case HitData.DamageType.Lightning: damages.m_lightning = modifier; return true;
                case HitData.DamageType.Poison: damages.m_poison = modifier; return true;
                case HitData.DamageType.Spirit: damages.m_spirit = modifier; return true;
                case HitData.DamageType.NonPlayer: damages.m_nonPlayer = modifier; return true;
                default: return false;
            }
        }

        private static void ApplyStation(string key, GameObject prefab, PieceModification def, bool ownStation)
        {
            CraftingStation station = Part<CraftingStation>(key, prefab, "station", StationFields);
            if (station == null) { return; }
            CraftingStationSettings s = def.station;
            string name = Tokens.Resolve(s?.name, Tokens.PiecePrefix, prefab.name, "_station");
            if (name == null && ownStation)
            {
                // The piece's own name, or its prefab name when it still has the original's
                Piece piece = prefab.GetComponent<Piece>();
                name = def.PieceName != null && piece != null ? piece.m_name : prefab.name;
            }
            if (name != null) { station.m_name = name; }
            if (s == null) { return; }
            Set(ref station.m_discoverRange, s.discoveryRange);
            Set(ref station.m_rangeBuild, s.buildRange);
            Set(ref station.m_extraRangePerLevel, s.extraRangePerLevel);
            Set(ref station.m_useDistance, s.useDistance);
            if (s.craftRequiresRoof == true && station.m_roofCheckPoint == null)
            {
                // The game checks for a roof from a point on the station, which not every station has
                Logger.LogWarning($"Piece modification '{key}' makes {prefab.name} need a roof, but it has no point to check for one from. That setting is ignored.");
            }
            else { Set(ref station.m_craftRequireRoof, s.craftRequiresRoof); }
            Set(ref station.m_craftRequireFire, s.craftRequiresFire);
            Set(ref station.m_showBasicRecipies, s.showBasicRecipes);
            Set(ref station.m_canRepair, s.canRepair);
        }

        private static void ApplyExtension(string key, GameObject prefab, StationExtensionSettings s, TrackedPiece tracked)
        {
            StationExtension extension = Part<StationExtension>(key, prefab, "stationExtension", ExtensionFields);
            if (extension == null) { return; }
            if (tracked.extensionStation != null) { extension.m_craftingStation = tracked.extensionStation; }
            Set(ref extension.m_maxStationDistance, s.maxStationDistance);
            Set(ref extension.m_stack, s.stack);
            Set(ref extension.m_continousConnection, s.continuousConnection);
        }

        private static void ApplyContainer(string key, GameObject prefab, ContainerSettings s)
        {
            Container container = Part<Container>(key, prefab, "container", ContainerFields);
            if (container == null) { return; }
            if ((s.width.HasValue && s.width.Value < container.m_width) || (s.height.HasValue && s.height.Value < container.m_height))
            {
                Logger.LogWarning($"Piece modification '{key}' makes {prefab.name} smaller. Items already stored past its new size can be lost when it next loads.");
            }
            if (s.width.HasValue) { container.m_width = Math.Max(1, s.width.Value); }
            if (s.height.HasValue) { container.m_height = Math.Max(1, s.height.Value); }
            Set(ref container.m_checkGuardStone, s.checkWard);
            Set(ref container.m_autoDestroyEmpty, s.autoDestroyIfEmpty);
        }

        private static void ApplyFireplace(string key, GameObject prefab, FireplaceSettings s, TrackedPiece tracked)
        {
            Fireplace fire = Part<Fireplace>(key, prefab, "fireplace", FireplaceFields);
            if (fire == null) { return; }
            Set(ref fire.m_startFuel, s.startFuel);
            Set(ref fire.m_maxFuel, s.maxFuel);
            Set(ref fire.m_secPerFuel, s.secPerFuel);
            Set(ref fire.m_infiniteFuel, s.infiniteFuel);
            if (tracked.fireplaceFuel != null) { fire.m_fuelItem = tracked.fireplaceFuel; }
            Set(ref fire.m_canTurnOff, s.canTurnOff);
            Set(ref fire.m_canRefill, s.canRefill);
            Set(ref fire.m_igniteInterval, s.igniteInterval);
            Set(ref fire.m_igniteChance, s.igniteChance);
            Set(ref fire.m_igniteSpread, s.igniteSpread);
        }

        private static void Set<T>(ref T field, T? configured) where T : struct
        {
            if (configured.HasValue) { field = configured.Value; }
        }
    }
}

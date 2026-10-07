using Jotunn.Managers;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager.Importer
{
    /// <summary>A WackysDatabase Piece_ file as a Pieces.yaml entry, and its station blocks as Conversions.yaml entries.</summary>
    internal static class PieceImporter
    {
        private const string NeverApplied = "WackysDatabase reads this but never applies it";
        private const string NewPart = "giving a piece a part it does not have is not part of RecipeManager, only changing the parts it has";

        private static readonly Dictionary<string, string> Reasons = new Dictionary<string, string>
        {
            ["repairPiece"] = WackyImporter.NoEquivalent,
            ["wearNTearData.minToolTier"] = NeverApplied,
            ["wearNTearData.snowDamageImmune"] = NeverApplied,
            ["wearNTearData.outsideRequiredBiomeDamage"] = NeverApplied,
            ["craftingStationData.cStationCustomIcon"] = WackyImporter.Visual,
            ["craftingStationData.useAnimation"] = "changing how a station is used can leave players stuck in its animation",
            ["craftingStationData.upgrader"] = "turning a station into a Forge of Potential is not part of RecipeManager",
            ["comfort.comfortObjectName"] = WackyImporter.NoEquivalent,
            ["material"] = WackyImporter.Visual,
            ["damagedMaterial"] = WackyImporter.Visual,
            ["sizeMultiplier"] = WackyImporter.Visual,
            ["smelterData.addOreAnimationLength"] = WackyImporter.Visual,
            ["smelterData.addOreTooltip"] = WackyImporter.Visual,
            ["smelterData.emptyOreTooltip"] = WackyImporter.Visual,
            ["cookingStationData.addItemTooltip"] = WackyImporter.Visual
        };

        // The damage types a piece can resist, by the names WackysDatabase writes them with
        private static readonly (string key, HitData.DamageType type)[] WearDamageKeys =
        {
            ("m_blunt", HitData.DamageType.Blunt), ("m_slash", HitData.DamageType.Slash), ("m_pierce", HitData.DamageType.Pierce),
            ("m_chop", HitData.DamageType.Chop), ("m_pickaxe", HitData.DamageType.Pickaxe), ("m_fire", HitData.DamageType.Fire),
            ("m_frost", HitData.DamageType.Frost), ("m_lightning", HitData.DamageType.Lightning), ("m_poison", HitData.DamageType.Poison),
            ("m_spirit", HitData.DamageType.Spirit), ("m_nonPlayer", HitData.DamageType.NonPlayer)
        };

        internal static void Import(WackyImporter ctx, WackyFile file)
        {
            ImportReport.FileResult result = ctx.Report.Add(file);
            WackyNode node = ctx.Parse(file, result);
            if (node == null) { return; }

            string name = node.Str("name");
            if (string.IsNullOrEmpty(name))
            {
                result.Skipped = "it has no name";
                return;
            }
            node.Converted.Add($"name -> prefab: {name}");
            string clone = node.Str("clonePrefabName");
            bool add = !string.IsNullOrEmpty(clone);
            GameObject prefab = PrefabManager.Instance.GetPrefab(add ? clone : name);
            Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
            if (add)
            {
                node.Converted.Add($"clonePrefabName -> action: Add, clonedFrom: {clone}");
                if (piece == null) { result.Warnings.Add($"no piece named {clone} exists in this game to copy"); }
                if (!ctx.WackyDBLoaded && PrefabManager.Instance.GetPrefab(name) != null)
                {
                    result.Warnings.Add($"a prefab named {name} already exists, so RecipeManager will not add another one. Use a new name, or change it as a Modify instead");
                }
            }
            else if (piece == null) { result.Warnings.Add($"no piece named {name} exists in this game, the entry was written anyway"); }

            PieceModification mod = new PieceModification
            {
                action = add ? PieceAction.Add : PieceAction.Modify,
                prefab = name,
                clonedFrom = add ? clone : null,
                PieceName = node.Str("m_name", "pieceName"),
                PieceDescription = node.Str("m_description", "pieceDescription"),
                RequiredToPlaceCraftingStation = ctx.Station(node, "craftingStation", "requiredToPlaceCraftingStation", result),
                GroundPlacement = node.Bool("groundPiece", "groundPlacement"),
                AllowedInDungeon = node.Bool("allowedInDungeons", "allowedInDungeon"),
                CanBeDeconstructed = node.Bool("canBeRemoved", "canBeDeconstructed"),
                SpaceRequired = node.Float("spaceRequirement", "spaceRequired"),
                IsUpgradeForStation = node.Bool("isUpgrade", "isUpgradeForStation"),
                requirements = Requirements(ctx, node, result),
                groundOnly = node.Bool("ground", "groundOnly"),
                waterPiece = node.Bool("waterPiece", "waterPiece"),
                noInWater = node.Bool("noInWater", "noInWater"),
                notOnFloor = node.Bool("notOnFloor", "notOnFloor"),
                notOnWood = node.Bool("notOnWood", "notOnWood"),
                onlyInTeleportArea = node.Bool("onlyinTeleportArea", "onlyInTeleportArea"),
                allowedInDeepSnow = node.Bool("allowedInDeepSnow", "allowedInDeepSnow"),
                requireDeepSnow = node.Bool("requireDeepSnow", "requireDeepSnow"),
                canRockJade = node.Bool("canRockJade", "canRockJade"),
                blockingPieces = BlockingPieces(ctx, node, result)
            };
            // WackysDatabase names a copy after its prefab until it is given a name of its own
            if (add && mod.PieceName == null)
            {
                mod.PieceName = name;
                result.Warnings.Add($"it sets no m_name, so like WackysDatabase the new piece is called {name}");
            }

            WackyNode comfort = node.Child("comfort");
            if (comfort != null)
            {
                mod.ComfortAmount = comfort.Int("comfort", "comfortAmount");
                mod.ComfortGroup = comfort.Enum<Piece.ComfortGroup>("comfortGroup", "comfortGroup");
            }

            Category(node, mod, result);
            Tool(node, mod, prefab, add, name, result);

            float? level = node.Peek("minStationLevel");
            if (level.HasValue && level.Value > 1) { mod.minStationLevel = node.Int("minStationLevel", "minStationLevel"); }
            else { node.Use("minStationLevel", "1 or less, the game's own"); }

            bool? disabled = node.Bool("disabled");
            if (disabled == true)
            {
                mod.EnablePiece = false;
                node.Converted.Add("disabled -> enablePiece: false");
            }
            if (string.Equals(node.Raw("adminonly")?.ToString().Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                node.Drop("adminonly", "RecipeManager cannot make a piece buildable by admins only, it stays buildable for everyone");
            }
            else { node.Use("adminonly"); }

            ctx.QueueIcon(node, "customIcon", name, result);
            Parts(ctx, node, mod, prefab, result);
            Conversions(ctx, node, name, result);

            // A Modify that only switches the piece off is a Disable
            if (mod.action == PieceAction.Modify && mod.EnablePiece == false && OnlySwitchesOff(mod))
            {
                mod.action = PieceAction.Disable;
                mod.EnablePiece = null;
            }

            ImportReport.Collect(result, node, key => WackyImporter.ReasonFor(Reasons, key), (key, value) => ctx.SameAsGame(piece, key, value));
            string entryKey = ctx.NewKey("piece", name, PieceUpdater.PiecesToModify.Keys);
            ctx.Pieces.Add(entryKey, mod);
            result.Targets.Add($"{entryKey} ({mod.action}) in Wacky_Pieces.yaml");
        }

        private static bool OnlySwitchesOff(PieceModification mod)
        {
            foreach (PropertyInfo property in typeof(PieceModification).GetProperties())
            {
                if (property.Name == nameof(PieceModification.action) || property.Name == nameof(PieceModification.prefab) || property.Name == nameof(PieceModification.EnablePiece)) { continue; }
                if (property.GetValue(mod) != null) { return false; }
            }
            return true;
        }

        /// <summary>
        /// WackysDatabase puts a piece under the game's build menu tag of the same name, or a tab of its own for any other
        /// name, unless usage names the tags. A name that is also one of the game's own build tabs, which the Hoe and the
        /// Cultivator still sort by, sets that too.
        /// </summary>
        private static void Category(WackyNode node, PieceModification mod, ImportReport.FileResult result)
        {
            mod.usage = node.Enum<Piece.UsageTagFlags>("usage", "usage");
            string category = node.Str("piecehammerCategory")?.Trim();
            if (string.IsNullOrEmpty(category)) { return; }
            bool gameTab = Enum.TryParse(category, true, out Piece.PieceCategory tab) && Enum.IsDefined(typeof(Piece.PieceCategory), tab)
                && tab != Piece.PieceCategory.Max && tab != Piece.PieceCategory.All;
            if (gameTab) { mod.PieceCategory = tab.ToString(); }
            if (Enum.TryParse(category, true, out Piece.UsageTagFlags tag) && Enum.IsDefined(typeof(Piece.UsageTagFlags), tag))
            {
                if (mod.usage == null) { mod.usage = tag; }
                node.Converted.Add($"piecehammerCategory -> usage: {tag}{(gameTab ? $", pieceCategory: {tab}" : string.Empty)}");
                return;
            }
            if (tab == Piece.PieceCategory.BuildingWorkbench || tab == Piece.PieceCategory.BuildingStonecutter)
            {
                if (mod.usage == null) { mod.usage = Piece.UsageTagFlags.Building; }
                node.Converted.Add($"piecehammerCategory -> usage: Building, pieceCategory: {tab}");
                return;
            }
            if (gameTab)
            {
                node.Converted.Add($"piecehammerCategory -> pieceCategory: {tab}");
                return;
            }
            mod.PieceCategory = category;
            node.Converted.Add($"piecehammerCategory -> pieceCategory: {category}, a build tab of its own");
            if (mod.usage == null)
            {
                result.Warnings.Add($"WackysDatabase also takes {category} pieces off the game's own build menu tags. RecipeManager leaves their tags alone, set usage to change them");
            }
        }

        /// <summary>
        /// The tool a piece is built with. A copy is put in it. A piece that exists is only moved when piecehammerCategory
        /// is set too, as WackysDatabase does. Written with the tool's own spelling, which is how the game finds it.
        /// </summary>
        private static void Tool(WackyNode node, PieceModification mod, GameObject prefab, bool add, string pieceName, ImportReport.FileResult result)
        {
            string tool = node.Str("piecehammer")?.Trim();
            if (string.IsNullOrEmpty(tool))
            {
                if (add)
                {
                    mod.pieceTable = "Hammer";
                    result.Warnings.Add("it names no piecehammer, so like WackysDatabase the new piece is built with the Hammer");
                }
                return;
            }
            (PieceTable table, string name) = FindTable(tool);
            if (table == null)
            {
                if (add)
                {
                    mod.pieceTable = "Hammer";
                    result.Warnings.Add($"piecehammer: no tool or build table named {tool} was found, so like WackysDatabase the new piece is built with the Hammer");
                }
                else
                {
                    node.Drop("piecehammer", $"no tool or build table named {tool} was found, so the piece stays where it is");
                }
                return;
            }
            if (add)
            {
                mod.pieceTable = name;
                node.Converted.Add($"piecehammer -> pieceTable: {name}");
                return;
            }
            if (prefab != null && table.m_pieces.Contains(prefab))
            {
                node.Converted.Add($"piecehammer: {tool}, already built with it");
                return;
            }
            if (!node.Has("piecehammerCategory"))
            {
                node.Use("piecehammer", $"not moved, WackysDatabase only moves a piece to the {tool} when piecehammerCategory is set too");
                return;
            }
            mod.pieceTable = name;
            node.Converted.Add($"piecehammer -> pieceTable: {name}, moving {pieceName} to it");
            if (string.Equals(name, "Hoe", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Cultivator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "_CultivatorPieceTable", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add($"WackysDatabase adds {pieceName} to the {name} and leaves it in its other tools, RecipeManager moves it, so it is only built with the {name}");
            }
        }

        /// <summary>A tool or build table in any case, with the name it is written with: the tool's prefab name, or the table's.</summary>
        private static (PieceTable table, string name) FindTable(string tool)
        {
            GameObject item = WackyImporter.FindItem(tool);
            if (item == null && ObjectDB.instance != null)
            {
                item = ObjectDB.instance.m_items.FirstOrDefault(i => i != null && string.Equals(i.name, tool, StringComparison.OrdinalIgnoreCase));
            }
            PieceTable table = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
            if (table != null) { return (table, item.name); }
            table = Resources.FindObjectsOfTypeAll<PieceTable>().FirstOrDefault(t => t != null && string.Equals(t.name, tool, StringComparison.OrdinalIgnoreCase));
            return (table, table != null ? table.name : null);
        }

        private static List<string> BlockingPieces(WackyImporter ctx, WackyNode node, ImportReport.FileResult result)
        {
            List<string> names = node.StrList("blockingPieces");
            if (names == null) { return null; }
            List<string> pieces = new List<string>();
            foreach (string entry in names)
            {
                string pieceName = entry?.Trim();
                if (string.IsNullOrEmpty(pieceName)) { continue; }
                GameObject found = PrefabManager.Instance.GetPrefab(pieceName);
                if (found == null || found.GetComponent<Piece>() == null)
                {
                    // WackysDatabase leaves out the names it cannot find
                    result.Warnings.Add($"blockingPieces: no piece named {pieceName} exists in this game, it was left out");
                    continue;
                }
                pieces.Add(pieceName);
            }
            node.Converted.Add("blockingPieces -> blockingPieces");
            return pieces;
        }

        /// <summary>
        /// "Item:amount:amountPerLevel:recover" entries. One 'delete' entry makes the piece free, an empty list keeps
        /// its cost, as WackysDatabase does.
        /// </summary>
        private static List<SimpleRequirement> Requirements(WackyImporter ctx, WackyNode node, ImportReport.FileResult result)
        {
            List<string> build = node.StrList("build");
            if (build == null) { return null; }
            if (build.Any(entry => entry != null && string.Equals(entry.Trim(), "delete", StringComparison.OrdinalIgnoreCase)))
            {
                node.Converted.Add("build: delete -> requirements: [] (free)");
                return new List<SimpleRequirement>();
            }
            List<SimpleRequirement> requirements = new List<SimpleRequirement>();
            for (int i = 0; i < build.Count; i++)
            {
                string entry = build[i];
                if (string.IsNullOrWhiteSpace(entry)) { continue; }
                string where = $"build[{i}]";
                string[] parts = entry.Split(':');
                string item = parts[0].Trim();
                if (parts.Length < 2)
                {
                    result.Warnings.Add($"{where}: '{entry}' has no amount, it was left out");
                    continue;
                }
                if (!ctx.ItemKnown(item))
                {
                    result.Warnings.Add($"{where}: no item named {item} exists in this game, it was left out");
                    continue;
                }
                bool recover = parts.Length < 4 || string.Equals(parts[3].Trim(), "true", StringComparison.OrdinalIgnoreCase);
                requirements.Add(new SimpleRequirement { Prefab = item, amount = WackyImporter.ParseInt(parts[1], 1, where, result), recover = recover });
            }
            if (requirements.Count == 0)
            {
                // WackysDatabase keeps the cost when no requirement could be read
                node.Use("build", "no usable requirements, the piece keeps its cost");
                return null;
            }
            node.Converted.Add($"build -> requirements ({requirements.Count})");
            return requirements;
        }

        /// <summary>How the piece wears, and what it does as a station, station upgrade, container or fire.</summary>
        private static void Parts(WackyImporter ctx, WackyNode node, PieceModification mod, GameObject prefab, ImportReport.FileResult result)
        {
            WackyNode wear = Part<WearNTear>(node, "wearNTearData", prefab);
            if (wear != null)
            {
                mod.wearNTear = new WearNTearSettings
                {
                    health = wear.Float("health", "wearNTear.health"),
                    noRoofWear = wear.Bool("noRoofWear", "wearNTear.noRoofWear"),
                    noSupportWear = wear.Bool("noSupportWear", "wearNTear.noSupportWear"),
                    supports = wear.Bool("supports", "wearNTear.supports"),
                    triggerPrivateArea = wear.Bool("triggerPrivateArea", "wearNTear.triggerPrivateArea"),
                    materialType = wear.Enum<WearNTear.MaterialType>("materialType", "wearNTear.materialType"),
                    burnable = wear.Bool("burnable", "wearNTear.burnable"),
                    resistances = WearResistances(wear, prefab, ctx.WackyDBLoaded)
                };
            }

            WackyNode station = Part<CraftingStation>(node, "craftingStationData", prefab);
            if (station != null)
            {
                mod.station = new CraftingStationSettings
                {
                    discoveryRange = station.Float("discoveryRange", "station.discoveryRange"),
                    buildRange = station.Float("buildRange", "station.buildRange"),
                    craftRequiresRoof = station.Bool("craftRequiresRoof", "station.craftRequiresRoof"),
                    craftRequiresFire = station.Bool("craftRequiresFire", "station.craftRequiresFire"),
                    showBasicRecipes = station.Bool("showBasicRecipes", "station.showBasicRecipes"),
                    useDistance = station.Float("useDistance", "station.useDistance")
                };
            }

            // The single block from older versions comes first, as WackysDatabase reads it
            List<WackyNode> extensions = new List<WackyNode>();
            if (prefab != null && prefab.GetComponent<StationExtension>() == null)
            {
                node.Drop("cSExtensionData", NewPart);
                node.Drop("cSExtensionDataList", NewPart);
            }
            else
            {
                WackyNode single = node.Child("cSExtensionData");
                if (single != null) { extensions.Add(single); }
                extensions.AddRange(node.ChildList("cSExtensionDataList") ?? new List<WackyNode>());
            }
            if (extensions.Count > 0)
            {
                WackyNode extension = extensions[0];
                mod.stationExtension = new StationExtensionSettings
                {
                    station = ctx.Station(extension, "MainCraftingStationName", "stationExtension.station", result),
                    maxStationDistance = extension.Float("maxStationDistance", "stationExtension.maxStationDistance"),
                    continuousConnection = extension.Bool("continousConnection", "stationExtension.continuousConnection"),
                    stack = extension.Bool("stack", "stationExtension.stack")
                };
                if (mod.stationExtension.station == StationRef.None) { mod.stationExtension.station = null; }
                for (int i = 1; i < extensions.Count; i++)
                {
                    foreach (string key in extensions[i].Keys().ToList()) { extensions[i].Drop(key, "a piece can only upgrade one station in RecipeManager, the first one is used"); }
                }
            }

            WackyNode container = Part<Container>(node, "contData", prefab);
            if (container != null)
            {
                mod.container = new ContainerSettings
                {
                    width = container.Int("Width", "container.width"),
                    height = container.Int("Height", "container.height"),
                    checkWard = container.Bool("CheckWard", "container.checkWard"),
                    autoDestroyIfEmpty = container.Bool("AutoDestoryIfEmpty", "container.autoDestroyIfEmpty")
                };
            }

            WackyNode fire = Part<Fireplace>(node, "fireplaceData", prefab);
            if (fire != null)
            {
                mod.fireplace = new FireplaceSettings
                {
                    startFuel = fire.Float("StartFuel", "fireplace.startFuel"),
                    maxFuel = fire.Float("MaxFuel", "fireplace.maxFuel"),
                    secPerFuel = fire.Float("SecPerFuel", "fireplace.secPerFuel"),
                    infiniteFuel = fire.Bool("InfiniteFuel", "fireplace.infiniteFuel"),
                    fuelItem = fire.Str("FuelType", "fireplace.fuelItem"),
                    igniteInterval = fire.Float("IgniteInterval", "fireplace.igniteInterval"),
                    igniteChance = fire.Float("IgniteChance", "fireplace.igniteChance"),
                    igniteSpread = fire.Int("IgniteSpread", "fireplace.igniteSpread")
                };
                CheckItem(ctx, mod.fireplace.fuelItem, "fireplaceData.FuelType", result);
            }
        }

        /// <summary>A block for a part the piece has. One for a part it does not have is dropped, WackysDatabase would add the part.</summary>
        private static WackyNode Part<T>(WackyNode node, string key, GameObject prefab) where T : Component
        {
            if (!node.Has(key)) { return null; }
            if (prefab != null && prefab.GetComponent<T>() == null)
            {
                node.Drop(key, NewPart);
                return null;
            }
            return node.Child(key);
        }

        /// <summary>
        /// WackysDatabase sets every resistance of a piece whenever it has a wearNTearData block, and a type the block
        /// leaves out becomes Normal. Every type is written out to do the same, unless they are what the piece has.
        /// </summary>
        private static List<DamageResistance> WearResistances(WackyNode wear, GameObject prefab, bool wackyDBLoaded)
        {
            WackyNode damages = wear.Child("damageModifiers");
            List<DamageResistance> resistances = new List<DamageResistance>();
            foreach ((string key, HitData.DamageType type) in WearDamageKeys)
            {
                HitData.DamageModifier modifier = damages?.Enum<HitData.DamageModifier>(key) ?? HitData.DamageModifier.Normal;
                resistances.Add(new DamageResistance { type = type, modifier = modifier });
            }
            WearNTear current = prefab != null ? prefab.GetComponent<WearNTear>() : null;
            if (current != null && !wackyDBLoaded && resistances.All(r => current.m_damages.GetModifier(r.type) == r.modifier))
            {
                wear.Converted.Add($"{wear.Name}.damageModifiers: the same as the piece already has");
                return null;
            }
            wear.Converted.Add($"{wear.Name}.damageModifiers -> wearNTear.resistances, every damage type, Normal where it lists none");
            return resistances;
        }

        /// <summary>
        /// Smelter, cooking station, fermenter and incinerator blocks. WackysDatabase updates the conversion for each input
        /// listed, adds the ones a station does not have, and takes out the ones marked Remove, which is an Add and a Remove
        /// in RecipeManager. An incinerator's list replaces its own. The station's own settings go in a Modify of their own.
        /// </summary>
        private static void Conversions(WackyImporter ctx, WackyNode node, string prefab, ImportReport.FileResult result)
        {
            WackyNode smelter = node.Child("smelterData");
            if (smelter != null)
            {
                ConversionModification settings = new ConversionModification
                {
                    action = ConversionAction.Modify,
                    prefab = prefab,
                    fuelItem = smelter.Child("fuelItem")?.Str("name", "fuelItem"),
                    maxOres = smelter.Int("maxOre", "maxOres"),
                    maxFuel = smelter.Int("maxFuel", "maxFuel"),
                    fuelPerProduct = smelter.Int("fuelPerProduct", "fuelPerProduct"),
                    conversionTime = smelter.Float("secPerProduct", "conversionTime"),
                    spawnStack = smelter.Bool("spawnStack", "spawnStack"),
                    requiresRoof = smelter.Bool("requiresRoof", "requiresRoof")
                };
                CheckItem(ctx, settings.fuelItem, "smelterData.fuelItem", result);
                AddStation(ctx, prefab, settings, Lists(ctx, smelter, "smelterConversion", null, null, result), result);
            }

            WackyNode cooking = node.Child("cookingStationData");
            if (cooking != null)
            {
                ConversionModification settings = new ConversionModification
                {
                    action = ConversionAction.Modify,
                    prefab = prefab,
                    fuelItem = cooking.Str("fuelItem", "fuelItem"),
                    overCookedItem = cooking.Str("overcookedItem", "overCookedItem"),
                    maxFuel = cooking.Int("maxFuel", "maxFuel"),
                    secPerFuel = cooking.Float("secPerFuel", "secPerFuel"),
                    requiresFire = cooking.Bool("requireFire", "requiresFire")
                };
                CheckItem(ctx, settings.fuelItem, "cookingStationData.fuelItem", result);
                CheckItem(ctx, settings.overCookedItem, "cookingStationData.overcookedItem", result);
                // WackysDatabase sets a cook time of 10 on every conversion it lists without one, existing ones included
                AddStation(ctx, prefab, settings, Lists(ctx, cooking, "cookConversion", null, entry => entry.Float("CookTime") ?? 10f, result), result);
            }

            WackyNode fermenter = node.Child("fermStationData");
            if (fermenter != null)
            {
                ConversionModification settings = new ConversionModification
                {
                    action = ConversionAction.Modify,
                    prefab = prefab,
                    conversionTime = fermenter.Float("fermDuration", "conversionTime")
                };
                AddStation(ctx, prefab, settings, Lists(ctx, fermenter, "fermConversion", entry => entry.Int("Amount"), null, result), result);
            }

            WackyNode incinerator = node.Child("incineratorData");
            if (incinerator != null)
            {
                ConversionModification settings = new ConversionModification
                {
                    action = ConversionAction.Modify,
                    prefab = prefab,
                    defaultResult = incinerator.Str("defaultDrop", "defaultResult"),
                    defaultCost = incinerator.Int("defaultCostPerDrop", "defaultCost"),
                    incineratorConversions = IncineratorConversions(ctx, incinerator, result)
                };
                CheckItem(ctx, settings.defaultResult, "incineratorData.defaultDrop", result);
                AddStation(ctx, prefab, settings, (new List<ConversionDef>(), new List<ConversionDef>()), result);
            }
        }

        /// <summary>WackysDatabase replaces the whole list, filling in what each entry leaves out with its own defaults, which are written out.</summary>
        private static List<IncineratorConversionDef> IncineratorConversions(WackyImporter ctx, WackyNode block, ImportReport.FileResult result)
        {
            List<WackyNode> entries = block.ChildList("incineratorConversion");
            if (entries == null) { return null; }
            List<IncineratorConversionDef> conversions = new List<IncineratorConversionDef>();
            foreach (WackyNode entry in entries)
            {
                string made = entry.Str("Result") ?? "Coal";
                CheckItem(ctx, made, entry.Where("Result"), result);
                IncineratorConversionDef conversion = new IncineratorConversionDef
                {
                    result = made,
                    resultAmount = entry.Int("ResultAmount") ?? 1,
                    priority = entry.Int("Priority") ?? 0,
                    anyOneIngredient = entry.Bool("RequireOnlyOne") ?? true,
                    ingredients = new List<IncineratorIngredientDef>()
                };
                foreach (WackyNode requirement in entry.ChildList("Requirements") ?? new List<WackyNode>())
                {
                    string item = requirement.Str("Name");
                    int amount = requirement.Int("Amount") ?? 1;
                    if (string.IsNullOrEmpty(item)) { continue; }
                    CheckItem(ctx, item, requirement.Where("Name"), result);
                    conversion.ingredients.Add(new IncineratorIngredientDef { prefab = item, amount = amount });
                }
                entry.Converted.Add($"{entry.Name} -> incineratorConversions, making {made}");
                conversions.Add(conversion);
            }
            return conversions;
        }

        private static (List<ConversionDef> add, List<ConversionDef> remove) Lists(WackyImporter ctx, WackyNode block, string key,
            Func<WackyNode, int?> amount, Func<WackyNode, float?> cookTime, ImportReport.FileResult result)
        {
            List<ConversionDef> add = new List<ConversionDef>();
            List<ConversionDef> remove = new List<ConversionDef>();
            List<WackyNode> entries = block.ChildList(key);
            if (entries == null) { return (add, remove); }
            foreach (WackyNode entry in entries)
            {
                string from = entry.Str("FromName");
                string to = entry.Str("ToName");
                bool removed = entry.Bool("Remove") == true;
                entry.Converted.Add($"{entry.Name} -> {(removed ? "removed" : "added or updated")} conversion from {from ?? "nothing"}");
                CheckItem(ctx, from, entry.Where("FromName"), result);
                CheckItem(ctx, to, entry.Where("ToName"), result);
                if (removed)
                {
                    entry.Use("CookTime");
                    entry.Use("Amount");
                    remove.Add(new ConversionDef { fromPrefab = from });
                    continue;
                }
                add.Add(new ConversionDef { fromPrefab = from, toPrefab = to, amount = amount?.Invoke(entry), cookTime = cookTime?.Invoke(entry) });
            }
            return (add, remove);
        }

        private static void AddStation(WackyImporter ctx, string prefab, ConversionModification settings, (List<ConversionDef> add, List<ConversionDef> remove) lists, ImportReport.FileResult result)
        {
            bool hasSettings = settings.fuelItem != null || settings.overCookedItem != null || settings.maxOres != null || settings.maxFuel != null
                || settings.fuelPerProduct != null || settings.conversionTime != null || settings.secPerFuel != null || settings.spawnStack != null
                || settings.requiresRoof != null || settings.requiresFire != null || settings.defaultResult != null || settings.defaultCost != null
                || settings.incineratorConversions != null;
            ICollection<string> loaded = ConversionUpdater.ConversionsToModify.Keys;
            if (hasSettings)
            {
                string key = ctx.NewKey("conversion", prefab, loaded);
                ctx.Conversions.Add(key, settings);
                result.Targets.Add($"{key} (Modify) in Wacky_Conversions.yaml");
            }
            if (lists.add.Count > 0)
            {
                string key = ctx.NewKey("conversion", prefab + "_add", loaded);
                ctx.Conversions.Add(key, new ConversionModification { action = ConversionAction.Add, prefab = prefab, conversions = lists.add });
                result.Targets.Add($"{key} (Add) in Wacky_Conversions.yaml");
            }
            if (lists.remove.Count > 0)
            {
                string key = ctx.NewKey("conversion", prefab + "_remove", loaded);
                ctx.Conversions.Add(key, new ConversionModification { action = ConversionAction.Remove, prefab = prefab, conversions = lists.remove });
                result.Targets.Add($"{key} (Remove) in Wacky_Conversions.yaml");
            }
        }

        private static void CheckItem(WackyImporter ctx, string item, string where, ImportReport.FileResult result)
        {
            if (!string.IsNullOrEmpty(item) && !ctx.ItemKnown(item))
            {
                result.Warnings.Add($"{where}: no item named {item} exists in this game");
            }
        }
    }
}

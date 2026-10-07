using BepInEx;
using BepInEx.Bootstrap;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager.Importer
{
    /// <summary>
    /// Converts a folder of WackysDatabase files into RecipeManager files. Runs in the game, since WackysDatabase
    /// names stations by the name the game shows for them and a converted entry is checked against what exists.
    /// Everything is written to a staging folder that RecipeManager does not load, with a report of what each file
    /// became and what could not be carried across.
    /// </summary>
    internal class WackyImporter
    {
        internal const string OutputFolderName = "WackyImport";
        internal const string WackyDBGuid = "WackyMole.WackysDatabase";

        internal const string Visual = "visual changes are not part of RecipeManager, a texture override in its Textures folder can do some of this";
        internal const string NeedsPatch = "needs a game patch that RecipeManager does not have";
        internal const string NoEquivalent = "RecipeManager has no setting for this";

        private static readonly Regex UpgraderItem = new Regex(@"^Upgrader\d+(Weapon|Armor)$");

        internal readonly string Root;
        internal readonly string Output;
        internal readonly ImportReport Report = new ImportReport();
        internal readonly Dictionary<string, RecipeModification> Recipes = new Dictionary<string, RecipeModification>();
        internal readonly Dictionary<string, PieceModification> Pieces = new Dictionary<string, PieceModification>();
        internal readonly Dictionary<string, ConversionModification> Conversions = new Dictionary<string, ConversionModification>();
        internal readonly Dictionary<string, ItemAddition> Items = new Dictionary<string, ItemAddition>();
        internal readonly Dictionary<string, StatusEffectDefinition> StatusEffects = new Dictionary<string, StatusEffectDefinition>();

        /// <summary>Items this import adds by cloning, by prefab name.</summary>
        internal readonly HashSet<string> ClonedItems = new HashSet<string>();
        /// <summary>Status effects this import adds, by name.</summary>
        internal readonly HashSet<string> CreatedEffects = new HashSet<string>();
        /// <summary>
        /// Whether WackysDatabase is running in this game. Its changes are then in what the import reads from the
        /// game, so it cannot tell which values a file leaves the same.
        /// </summary>
        internal readonly bool WackyDBLoaded;

        private readonly HashSet<string> usedKeys = new HashSet<string>();
        private readonly List<(string source, string prefab)> icons = new List<(string source, string prefab)>();

        internal WackyImporter(string root)
        {
            Root = Path.GetFullPath(root);
            Output = Path.Combine(ValConfig.GetSecondaryConfigDirectoryPath(), OutputFolderName);
            WackyDBLoaded = Chainloader.PluginInfos.ContainsKey(WackyDBGuid);
            Report.Source = Root;
            Report.Output = Output;
        }

        internal void Run()
        {
            if (WackyDBLoaded)
            {
                Report.General.Add("WackysDatabase is running in this game, so its own changes are part of what the import compared against. Run the import with WackysDatabase disabled for a more accurate report.");
            }
            List<WackyFile> files = WackyFileScanner.Scan(Root);
            // Status effects first, so the items that use one this import adds know it will exist
            foreach (WackyFile file in files.Where(f => f.Type == WackyType.StatusEffect)) { StatusEffectImporter.Import(this, file); }
            // Items before recipes, so a recipe for an item this import clones knows it is new
            foreach (WackyFile file in files.Where(f => f.Type == WackyType.Item)) { ItemImporter.Import(this, file); }
            foreach (WackyFile file in files.Where(f => f.Type == WackyType.Recipe)) { RecipeImporter.Import(this, file); }
            foreach (WackyFile file in files.Where(f => f.Type == WackyType.Piece)) { PieceImporter.Import(this, file); }
            foreach (WackyFile file in files.Where(f => f.Type != WackyType.StatusEffect && f.Type != WackyType.Item && f.Type != WackyType.Recipe && f.Type != WackyType.Piece))
            {
                Report.Add(file).Skipped = UnsupportedReason(file.Type);
            }
            Write();
        }

        private static string UnsupportedReason(WackyType type)
        {
            switch (type)
            {
                case WackyType.Creature: return "RecipeManager does not change creatures";
                case WackyType.Pickable: return "RecipeManager does not change pickables";
                case WackyType.Treebase: return "RecipeManager does not change trees";
                case WackyType.Projectile: return "RecipeManager does not change projectiles";
                case WackyType.Aoe: return "RecipeManager does not change area effects";
                case WackyType.Material: return Visual;
                default: return "not a WackysDatabase data file";
            }
        }

        /// <summary>Parses a file, or reports why it could not be and returns null.</summary>
        internal WackyNode Parse(WackyFile file, ImportReport.FileResult result)
        {
            try
            {
                object parsed = ValConfig.yamldeserializer.Deserialize<object>(file.Text);
                if (!(parsed is IDictionary<object, object>))
                {
                    result.Skipped = "it holds no settings";
                    return null;
                }
                return new WackyNode(parsed);
            }
            catch (Exception ex)
            {
                result.Skipped = $"it could not be read: {ex.Message}";
                return null;
            }
        }

        /// <summary>An entry name nobody else uses: not in this import, and not in the files RecipeManager has loaded.</summary>
        internal string NewKey(string type, string name, ICollection<string> loaded)
        {
            string baseKey = $"wacky_{type}_{name}";
            string key = baseKey;
            for (int i = 2; usedKeys.Contains(key) || loaded.Contains(key); i++) { key = $"{baseKey}_{i}"; }
            usedKeys.Add(key);
            return key;
        }

        internal static bool IsDelete(string value)
        {
            if (value == null) { return false; }
            string trimmed = value.Trim();
            return trimmed == "-" || string.Equals(trimmed, "delete", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsUpgrader(string item)
        {
            return item != null && UpgraderItem.IsMatch(item);
        }

        /// <summary>
        /// A WackysDatabase station as RecipeManager writes it. WackysDatabase uses the name the game shows ($piece_forge)
        /// or the prefab name, an empty one for none, and leaves it out to keep the station.
        /// </summary>
        internal string Station(WackyNode node, string key, string rmKey, ImportReport.FileResult result)
        {
            string value = node.Str(key);
            if (value == null) { return null; }
            if (value.Trim().Length == 0)
            {
                node.Converted.Add($"{node.Where(key)} -> {rmKey}: {StationRef.None}");
                return StationRef.None;
            }
            CraftingStation station = StationRef.Resolve(value);
            if (station == null)
            {
                result.Warnings.Add($"{node.Where(key)}: no crafting station named {value} exists in this game, it was copied as it is");
                node.Converted.Add($"{node.Where(key)} -> {rmKey}");
                return value;
            }
            string name = StationRef.NameOf(station);
            node.Converted.Add($"{node.Where(key)} -> {rmKey}: {name}");
            return name;
        }

        /// <summary>A status effect slot as RecipeManager writes it, where None empties the slot.</summary>
        internal string Effect(WackyNode node, string key, string rmKey, ImportReport.FileResult result)
        {
            string value = node.Str(key);
            if (value == null) { return null; }
            if (IsDelete(value) || value.Trim().Length == 0)
            {
                node.Converted.Add($"{node.Where(key)} -> {rmKey}: None");
                return "None";
            }
            value = value.Trim();
            if (!CreatedEffects.Contains(value) && !EffectExists(value))
            {
                result.Warnings.Add($"{node.Where(key)}: no status effect named {value} exists in this game");
            }
            node.Converted.Add($"{node.Where(key)} -> {rmKey}");
            return value;
        }

        /// <summary>
        /// A prefab slot as RecipeManager writes it, where none empties the slot. WackysDatabase empties one with
        /// 'delete' or '-', and an empty value changes nothing.
        /// </summary>
        /// <param name="exactDelete">For the slots where WackysDatabase only takes 'delete' exactly as written.</param>
        internal string PrefabRef(WackyNode node, string key, string rmKey, ImportReport.FileResult result, bool exactDelete = false)
        {
            string value = node.Str(key);
            if (value == null) { return null; }
            if (exactDelete ? value == "delete" : IsDelete(value))
            {
                node.Converted.Add($"{node.Where(key)} -> {rmKey}: none");
                return "none";
            }
            value = value.Trim();
            if (value.Length == 0)
            {
                node.Use(key);
                return null;
            }
            if (!ClonedItems.Contains(value) && Jotunn.Managers.PrefabManager.Instance.GetPrefab(value) == null)
            {
                result.Warnings.Add($"{node.Where(key)}: no prefab named {value} exists in this game");
            }
            node.Converted.Add($"{node.Where(key)} -> {rmKey}");
            return value;
        }

        /// <summary>
        /// The item entry this import writes for an item, made as a Modify when there is none yet. Recipe settings
        /// that WackysDatabase applies to the item itself go on it.
        /// </summary>
        internal ItemAddition ItemEntryFor(string prefab, ImportReport.FileResult result)
        {
            foreach (KeyValuePair<string, ItemAddition> entry in Items)
            {
                if (entry.Value.prefab == prefab) { return entry.Value; }
            }
            ItemAddition item = new ItemAddition { action = ItemAction.Modify, prefab = prefab };
            string key = NewKey("item", prefab, ItemUpdater.ItemDefinitions.Keys);
            Items.Add(key, item);
            result.Targets.Add($"{key} (Modify) in Wacky_Items.yaml");
            return item;
        }

        internal bool ItemKnown(string prefab)
        {
            return !string.IsNullOrEmpty(prefab) && (ClonedItems.Contains(prefab) || RecipeBuilder.ResolveItem(prefab) != null);
        }

        internal static GameObject FindItem(string prefab)
        {
            ItemDrop item = RecipeBuilder.ResolveItem(prefab);
            return item != null ? item.gameObject : null;
        }

        internal static bool EffectExists(string name)
        {
            return FindEffect(name) != null;
        }

        internal static StatusEffect FindEffect(string name)
        {
            if (ObjectDB.instance == null || ObjectDB.instance.m_StatusEffects == null || string.IsNullOrEmpty(name)) { return null; }
            return ObjectDB.instance.m_StatusEffects.Find(effect => effect != null && effect.name == name);
        }

        /// <summary>
        /// A recipe requirement, "Item:amount:amountPerLevel:recover". WackysDatabase defaults the amounts to 1, and only
        /// reads recover when there are exactly four parts.
        /// </summary>
        internal Ingrediant RecipeRequirement(string text, string where, ImportReport.FileResult result)
        {
            if (string.IsNullOrWhiteSpace(text)) { return null; }
            string[] parts = text.Split(':');
            string item = parts[0].Trim();
            if (!ItemKnown(item))
            {
                result.Warnings.Add($"{where}: no item named {item} exists in this game, WackysDatabase leaves it out too");
                return null;
            }
            int amount = parts.Length > 1 ? ParseInt(parts[1], 1, where, result) : 1;
            int perLevel = parts.Length > 2 ? ParseInt(parts[2], 1, where, result) : 1;
            bool recover = true;
            if (parts.Length == 4)
            {
                if (!bool.TryParse(parts[3].Trim(), out recover))
                {
                    result.Warnings.Add($"{where}: '{parts[3]}' is not true or false, using true");
                    recover = true;
                }
            }
            else if (parts.Length > 4 && parts[3].Trim().Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add($"{where} says not to refund {item}, but WackysDatabase ignores that when a requirement has more than four parts and refunds it, so the import does too");
            }
            return new Ingrediant
            {
                prefab = item,
                craftCost = (short)amount,
                upgradeCost = (short)perLevel,
                recover = recover,
                upgraderResource = IsUpgrader(item) ? true : (bool?)null
            };
        }

        internal static int ParseInt(string text, int fallback, string where, ImportReport.FileResult result)
        {
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) { return value; }
            result.Warnings.Add($"{where}: '{text}' is not a whole number, using {fallback}");
            return fallback;
        }

        /// <summary>Queues a WackysDatabase icon to be copied in as the item's RecipeManager icon.</summary>
        internal void QueueIcon(WackyNode node, string key, string prefab, ImportReport.FileResult result)
        {
            string icon = node.Str(key);
            if (icon == null) { return; }
            if (IsDelete(icon))
            {
                node.Drop(key, "RecipeManager cannot take an icon away, only replace it");
                return;
            }
            string source = FindIcon(icon);
            if (source == null)
            {
                result.Warnings.Add($"{node.Where(key)}: the icon {icon} was not found in WackysDatabase's Icons folder, it was not copied");
                return;
            }
            icons.Add((source, prefab));
            node.Converted.Add($"{node.Where(key)} -> Textures/{prefab}/icon.png");
        }

        private string FindIcon(string icon)
        {
            string[] folders =
            {
                Path.Combine(Root, "Icons"),
                Path.Combine(Paths.ConfigPath, "wackysDatabase", "Icons")
            };
            foreach (string folder in folders)
            {
                string path = Path.Combine(folder, icon);
                if (File.Exists(path)) { return path; }
            }
            return null;
        }

        /// <summary>
        /// Whether a WackysDatabase value left out is the one the game already has, read off a game object field of
        /// the same name, with or without the m_ in front. Never claimed while WackysDatabase itself is running.
        /// </summary>
        internal bool SameAsGame(object target, string key, object value)
        {
            if (WackyDBLoaded || target == null || value == null) { return false; }
            if (value is IDictionary<object, object> || value is IList<object>) { return false; }
            FieldInfo field = FindField(target.GetType(), key) ?? FindField(target.GetType(), "m_" + key);
            if (field == null) { return false; }
            object current = field.GetValue(target);
            string text = Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
            switch (current)
            {
                case bool flag: return bool.TryParse(text, out bool parsedBool) && parsedBool == flag;
                case int whole: return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedInt) && Math.Abs(parsedInt - whole) < 0.0001f;
                case float number: return float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedFloat) && Math.Abs(parsedFloat - number) < 0.0001f;
                case string words: return words == text;
                case Enum option: return EnumMatches(option, text);
                case UnityEngine.Object reference: return reference.name == text;
                default: return current == null && (text.Length == 0 || IsDelete(text));
            }
        }

        private static bool EnumMatches(Enum current, string text)
        {
            try { return Equals(Enum.Parse(current.GetType(), text, true), current); }
            catch (ArgumentException) { return false; }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The reason a key is left out: the most specific one known for it, for its own name, or for the block it is in.</summary>
        internal static string ReasonFor(Dictionary<string, string> reasons, string key)
        {
            if (reasons.TryGetValue(key, out string reason)) { return reason; }
            string leaf = key.Substring(key.LastIndexOf('.') + 1);
            if (reasons.TryGetValue(leaf, out reason)) { return reason; }
            string block = key.Split('.', '[')[0];
            if (reasons.TryGetValue(block, out reason)) { return reason; }
            return NoEquivalent;
        }

        private void Write()
        {
            Directory.CreateDirectory(Output);
            string header = $"# Converted from WackysDatabase by RM_Import_WackyDB on {DateTime.Now:yyyy-MM-dd}.\n" +
                "# ImportReport.txt, next to this file, lists what each WackysDatabase file became and what could not be carried across.\n" +
                "# This folder is not loaded. Move this file into BepInEx/config/RecipeManager when it looks right.\n";
            WriteFile("Wacky_Recipes.yaml", Recipes.Count, header, () => ValConfig.yamlserializer.Serialize(new RecipeModificationCollection { RecipeModifications = Recipes }));
            WriteFile("Wacky_Items.yaml", Items.Count, header, () => ValConfig.yamlserializer.Serialize(new ItemAdditionCollection { ItemAdditions = Items }));
            WriteFile("Wacky_Pieces.yaml", Pieces.Count, header, () => ValConfig.yamlserializer.Serialize(new PieceModificationCollection { PieceModifications = Pieces }));
            WriteFile("Wacky_Conversions.yaml", Conversions.Count, header, () => ValConfig.yamlserializer.Serialize(new ConversionModificationCollection { ConversionModifications = Conversions }));
            WriteFile("Wacky_StatusEffects.yaml", StatusEffects.Count, header, () => ValConfig.yamlserializer.Serialize(new StatusEffectCollection { statusEffects = StatusEffects }));

            foreach ((string source, string prefab) in icons)
            {
                try
                {
                    string folder = Path.Combine(Output, "Textures", prefab);
                    Directory.CreateDirectory(folder);
                    // The game reads the image from its contents, so a JPG copied in under this name works too
                    File.Copy(source, Path.Combine(folder, "icon.png"), true);
                }
                catch (Exception ex)
                {
                    Report.General.Add($"The icon for {prefab} could not be copied: {ex.Message}");
                }
            }
            if (icons.Count > 0) { Report.General.Add($"{icons.Count} icon(s) copied into Textures. Move that folder's contents into BepInEx/config/RecipeManager/Textures along with the files."); }

            File.WriteAllText(Path.Combine(Output, "ImportReport.txt"), Report.Render());
        }

        /// <summary>Writes one kind of file, or removes the one an earlier import left when there is nothing of that kind now.</summary>
        private void WriteFile(string name, int entries, string header, Func<string> body)
        {
            string path = Path.Combine(Output, name);
            if (entries == 0)
            {
                if (File.Exists(path)) { File.Delete(path); }
                return;
            }
            File.WriteAllText(path, header + "\n" + body());
        }
    }
}

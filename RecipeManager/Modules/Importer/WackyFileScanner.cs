using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RecipeManager.Importer
{
    internal enum WackyType
    {
        Item,
        Piece,
        Recipe,
        StatusEffect,
        Creature,
        Pickable,
        Treebase,
        Projectile,
        Aoe,
        Material,
        Unknown
    }

    internal class WackyFile
    {
        public string FullPath;
        public string RelativePath;
        public WackyType Type;
        public string Text;
    }

    /// <summary>
    /// Finds WackysDatabase data files the way WackysDatabase does: every .yml file under the folder, sorted by a
    /// name pattern and a word the file has to contain. Its Cache folder only holds copies, so it is skipped.
    /// </summary>
    internal static class WackyFileScanner
    {
        // Pattern prefix (first letter free, as WackysDatabase's '?' wildcard), the word the file must contain, and its type
        private static readonly (string rest, string marker, WackyType type)[] Patterns =
        {
            ("tem_", "m_weight", WackyType.Item),
            ("iece_", "piecehammer", WackyType.Piece),
            ("ecipe_", "reqs", WackyType.Recipe),
            ("reature_", "mob_display_name", WackyType.Creature),
            ("ickable_", "itemPrefab", WackyType.Pickable),
            ("reebase_", "treeHealth", WackyType.Treebase)
        };

        internal static List<WackyFile> Scan(string root)
        {
            List<WackyFile> files = new List<WackyFile>();
            foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                // Directory.GetFiles("*.yml") on Windows also matches .yml2, .ymlx and so on
                if (!path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)) { continue; }
                string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string[] folders = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (folders.Take(folders.Length - 1).Any(folder => string.Equals(folder, "Cache", StringComparison.OrdinalIgnoreCase))) { continue; }

                string text;
                try { text = File.ReadAllText(path); }
                catch (Exception ex)
                {
                    files.Add(new WackyFile { FullPath = path, RelativePath = relative, Type = WackyType.Unknown, Text = $"could not be read: {ex.Message}" });
                    continue;
                }
                bool inMaterials = folders.Take(folders.Length - 1).Any(folder => string.Equals(folder, "Materials", StringComparison.OrdinalIgnoreCase));
                files.Add(new WackyFile { FullPath = path, RelativePath = relative, Type = Classify(Path.GetFileName(path), text, inMaterials), Text = text });
            }
            files.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));
            return files;
        }

        private static WackyType Classify(string fileName, string text, bool inMaterials)
        {
            foreach ((string rest, string marker, WackyType type) in Patterns)
            {
                if (fileName.Length > rest.Length && fileName.Substring(1).StartsWith(rest, StringComparison.OrdinalIgnoreCase) && text.Contains(marker))
                {
                    return type;
                }
            }
            // The one pattern WackysDatabase matches without the free first letter
            if (fileName.StartsWith("SE_", StringComparison.OrdinalIgnoreCase) && text.Contains("Status_m_name")) { return WackyType.StatusEffect; }
            if (fileName.IndexOf("projectile", StringComparison.OrdinalIgnoreCase) >= 0) { return WackyType.Projectile; }
            if (fileName.IndexOf("aoe", StringComparison.OrdinalIgnoreCase) >= 0) { return WackyType.Aoe; }
            if (inMaterials) { return WackyType.Material; }
            return WackyType.Unknown;
        }
    }
}

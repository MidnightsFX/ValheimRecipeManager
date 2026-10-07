using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RecipeManager.Importer
{
    /// <summary>What happened to each WackysDatabase file, written out as ImportReport.txt next to the converted files.</summary>
    internal class ImportReport
    {
        internal class FileResult
        {
            public string File;
            public WackyType Type;
            /// <summary>The entries written for this file, as "key (action)". Empty when nothing was written.</summary>
            public List<string> Targets = new List<string>();
            /// <summary>Why the whole file was left out, when it was.</summary>
            public string Skipped;
            public List<string> Converted = new List<string>();
            public List<string> Dropped = new List<string>();
            /// <summary>Left out, but with the value the game already has, so nothing is lost.</summary>
            public List<string> Unchanged = new List<string>();
            public List<string> Warnings = new List<string>();
        }

        public readonly List<FileResult> Files = new List<FileResult>();
        public readonly List<string> General = new List<string>();
        public string Source;
        public string Output;

        public FileResult Add(WackyFile file)
        {
            FileResult result = new FileResult { File = file.RelativePath, Type = file.Type };
            Files.Add(result);
            return result;
        }

        /// <summary>
        /// Copies what a file's node recorded: what was converted, what was dropped and why, and anything left unread.
        /// </summary>
        /// <param name="reasonFor">Why a key RecipeManager cannot take is left out, by its reported name.</param>
        /// <param name="sameAsGame">Whether a key left out holds the value the game already has, by the key on its own and its value. Can be null.</param>
        public static void Collect(FileResult result, WackyNode node, Func<string, string> reasonFor, Func<string, object, bool> sameAsGame)
        {
            result.Converted.AddRange(node.AllConverted());
            foreach (KeyValuePair<string, string> pair in node.Dropped()) { result.Dropped.Add($"{pair.Key} - {pair.Value}"); }
            foreach ((string key, string leaf, object value) in node.Unread())
            {
                if (sameAsGame != null && sameAsGame(leaf, value)) { result.Unchanged.Add(key); }
                else { result.Dropped.Add($"{key} - {reasonFor(key)}"); }
            }
            foreach (string invalid in node.AllInvalid()) { result.Warnings.Add($"{invalid}, it was left out"); }
        }

        public string Render()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("WackysDatabase import report");
            text.AppendLine($"Written {DateTime.Now:yyyy-MM-dd HH:mm}");
            text.AppendLine($"Read from: {Source}");
            text.AppendLine($"Written to: {Output}");
            text.AppendLine();
            text.AppendLine("Nothing in this folder is loaded until you move it. Check the files and this report, then move the");
            text.AppendLine("Wacky_*.yaml files (and the Textures folder, if there is one) into BepInEx/config/RecipeManager.");
            text.AppendLine("Remove WackysDatabase, or its files, before you do: both mods changing the same things fight each other.");
            text.AppendLine();

            text.AppendLine("Summary");
            foreach (IGrouping<WackyType, FileResult> group in Files.GroupBy(file => file.Type).OrderBy(group => group.Key))
            {
                int written = group.Count(file => file.Targets.Count > 0);
                int partial = group.Count(file => file.Targets.Count > 0 && file.Dropped.Count > 0);
                int skipped = group.Count(file => file.Targets.Count == 0);
                text.AppendLine($"  {Plural(group.Key)}: {group.Count()} file(s), {written} converted ({partial} of them partly), {skipped} not converted");
            }
            foreach (string line in General) { text.AppendLine($"  {line}"); }
            text.AppendLine();

            foreach (FileResult file in Files)
            {
                text.AppendLine($"== {file.File} ({file.Type})");
                if (file.Skipped != null) { text.AppendLine($"  Not converted: {file.Skipped}"); }
                foreach (string target in file.Targets) { text.AppendLine($"  Written as {target}"); }
                foreach (string warning in file.Warnings) { text.AppendLine($"  Warning: {warning}"); }
                if (file.Converted.Count > 0)
                {
                    text.AppendLine("  Converted:");
                    foreach (string line in file.Converted) { text.AppendLine($"    {line}"); }
                }
                if (file.Dropped.Count > 0)
                {
                    text.AppendLine("  Left out:");
                    foreach (string line in file.Dropped) { text.AppendLine($"    {line}"); }
                }
                if (file.Unchanged.Count > 0)
                {
                    text.AppendLine($"  Left out, but already the game's value so nothing is lost: {string.Join(", ", file.Unchanged)}");
                }
                text.AppendLine();
            }
            return text.ToString();
        }

        private static string Plural(WackyType type)
        {
            switch (type)
            {
                case WackyType.StatusEffect: return "Status effects";
                case WackyType.Unknown: return "Other files";
                default: return type + "s";
            }
        }
    }
}

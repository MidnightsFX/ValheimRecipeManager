using BepInEx;
using Jotunn.Entities;
using RecipeManager.Importer;
using System;
using System.IO;
using System.Linq;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    internal class WackyImportCommand : ConsoleCommand
    {
        public override string Name => "RM_Import_WackyDB";

        public override string Help => "Converts WackysDatabase files into RecipeManager files, written to BepInEx/config/RecipeManager/WackyImport with a report. Usage: RM_Import_WackyDB [folder], the folder defaults to BepInEx/config/wackysDatabase";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            if (ObjectDB.instance == null || ZNetScene.instance == null)
            {
                ItemCommandUtils.Respond("Load into a world first, the import checks stations and items against the game.");
                return;
            }

            string folder = args != null && args.Length > 0 ? string.Join(" ", args).Trim('"') : Path.Combine(Paths.ConfigPath, "wackysDatabase");
            // A relative folder is taken from BepInEx/config, where WackysDatabase keeps its own
            if (!Path.IsPathRooted(folder)) { folder = Path.Combine(Paths.ConfigPath, folder); }
            if (!Directory.Exists(folder))
            {
                ItemCommandUtils.Respond($"There is no folder {folder}. Give the folder WackysDatabase's files are in, for example RM_Import_WackyDB wackysDatabase");
                return;
            }

            try
            {
                WackyImporter importer = new WackyImporter(folder);
                importer.Run();
                int files = importer.Report.Files.Count;
                int converted = importer.Report.Files.Count(file => file.Targets.Count > 0);
                ItemCommandUtils.Respond($"Converted {converted} of {files} WackysDatabase file(s): {importer.Recipes.Count} recipe(s), {importer.Items.Count} item(s), {importer.Pieces.Count} piece(s), {importer.Conversions.Count} conversion(s) and {importer.StatusEffects.Count} status effect(s).");
                ItemCommandUtils.Respond($"Written to {importer.Output}, nothing there is loaded until you move it. Read ImportReport.txt there first.");
                if (importer.WackyDBLoaded)
                {
                    ItemCommandUtils.Respond("WackysDatabase is running, so the report could not tell which of its values the game already had. Run the import with it disabled for a more accurate report.");
                }
            }
            catch (Exception ex)
            {
                ItemCommandUtils.Respond($"The import failed: {ex.Message}");
                Logger.LogError($"WackysDatabase import from {folder} failed. Error: {ex}");
            }
        }
    }
}

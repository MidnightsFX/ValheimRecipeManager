using Jotunn;
using Jotunn.Entities;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = Jotunn.Logger;

namespace RecipeManager
{
    internal class PieceReloadCommand : ConsoleCommand
    {
        public override string Name => "RM_Pieces_Reload";

        public override string Help => "Resynchronizes piece modifications.";

        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            RecipeUpdater.RecipeRevert();
            // read out the file
            string pieceConfigData = File.ReadAllText(Config.pieceConfigFilePath);
            try
            {
                var pieceDeserializedData = Config.yamldeserializer.Deserialize<RecipeModificationCollection>(pieceConfigData);
                RecipeUpdater.UpdateRecipeModifications(pieceDeserializedData);
            }
            catch
            {
                Logger.LogWarning($"Could not reload the pieces file from disk: {Config.recipeConfigFilePath}");
            }
            PieceUpdater.BuildPieceTracker();
            PieceUpdater.PieceUpdateRunner();
        }
    }

    // Need to actually fill this out
    internal class PiecePrintCommand : ConsoleCommand
    {
        public override string Name => "RM_Pieces_PrintAll";

        public override string Help => "Prints all the Pieces found.";

        public override void Run(string[] args)
        {
            if (Config.EnableDebugMode.Value) { Logger.LogInfo("Starting to dump piece list"); }
            String ODBRecipes_path = Path.Combine(BepInEx.Paths.ConfigPath, "RecipeManager", "AllPiecesDebug.yaml");
            PieceModificationCollection recipeCollection = new PieceModificationCollection();
            using (StreamWriter writetext = new StreamWriter(ODBRecipes_path))
            {
                if (Config.EnableDebugMode.Value) { Logger.LogInfo("Gathering Pieces"); }
                List<Piece> pieces = Resources.FindObjectsOfTypeAll<Piece>().Where(pc => pc.name.EndsWith("(Clone)") != true).ToList<Piece>();

                foreach (Piece pc in pieces)
                {
                    if (pc == null) { continue; }
                    if (pc.name == null) { continue; }
                    // This list of just broken recipes
                    // if (recipe.name == "Recipe_Adze") { continue; }
                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Building Recipe {pc.m_name}"); }
                    PieceModification piece_as_mod = new PieceModification();

                    piece_as_mod.action = PieceAction.Enable;
                    //Logger.LogInfo("Set action");
                    piece_as_mod.EnablePiece = pc.enabled;
                    //Logger.LogInfo("Set enable status");
                    piece_as_mod.CanBeDeconstructed = pc.m_canBeRemoved;
                    //Logger.LogInfo("Set can be deconstructed");
                    piece_as_mod.CultivatedGroundOnly = pc.m_cultivatedGroundOnly;
                    //Logger.LogInfo("Set cultivator ground only");
                    piece_as_mod.ComfortGroup = pc.m_comfortGroup;
                    //Logger.LogInfo("Set comfort group");
                    piece_as_mod.ComfortAmount = pc.m_comfort;
                    //Logger.LogInfo("Set comfort amount");
                    piece_as_mod.GroundPlacement = pc.m_groundPiece;
                    //Logger.LogInfo("Set is ground piece");
                    piece_as_mod.SpaceRequired = pc.m_spaceRequirement;
                    //Logger.LogInfo("Set space required");
                    piece_as_mod.AllowedInDungeon = pc.m_allowedInDungeons;
                    //Logger.LogInfo("Set allowed in dungeon");
                    piece_as_mod.CraftingStationConnectionRadius = pc.m_connectRadius;
                    //Logger.LogInfo("Set crafting radius");
                    piece_as_mod.MustBeAvobeConnectedStation = pc.m_mustBeAboveConnected;
                    //Logger.LogInfo("Set must be above connected station");
                    piece_as_mod.OnlyInSelectBiome = pc.m_onlyInBiome;
                    //Logger.LogInfo("Set only in biome");
                    piece_as_mod.PieceCategory = pc.m_category;
                    //Logger.LogInfo("Set set category");
                    piece_as_mod.PieceDescription = pc.m_description;
                    //Logger.LogInfo("Set description");
                    piece_as_mod.PieceName = pc.m_name;
                    //Logger.LogInfo("Set piecename");
                    piece_as_mod.prefab = pc.gameObject.name;
                    //Logger.LogInfo("Set prefabname");
                    if (pc.m_craftingStation != null) {
                        piece_as_mod.RequiredToPlaceCraftingStation = pc.m_craftingStation.name;
                        //Logger.LogInfo("Set set crafting station");
                    }
                    piece_as_mod.IsUpgradeForStation = pc.m_isUpgrade;
                    //Logger.LogInfo("Set is upgrade");
                    // Build the simple requirements list

                    List<SimpleRequirement> requirements = new List<SimpleRequirement>() { };
                    if (pc.m_resources != null && pc.m_resources.Length > 0)
                    {
                        // if (Config.EnableDebugMode.Value) { Logger.LogInfo("Building resource requirements"); }
                        foreach (Piece.Requirement req in pc.m_resources)
                        {
                            try
                            {
                                requirements.Add(new SimpleRequirement() { amount = req.m_amount, Prefab = req.m_resItem.name });
                            }
                            catch (Exception e)
                            {
                                if (Config.EnableDebugMode.Value) { Logger.LogWarning($"Piece requirement setup error {req} \n{e}"); }
                            }
                        }
                    }
                    piece_as_mod.requirements = requirements;

                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Adding {piece_as_mod.PieceName} to collection."); }
                    if (recipeCollection.PieceModifications.ContainsKey(piece_as_mod.PieceName))
                    {
                        try
                        {
                            int randtag = UnityEngine.Random.Range(0, 1000);
                            recipeCollection.PieceModifications.Add(piece_as_mod.PieceName + $"_{randtag}", piece_as_mod);
                            Logger.LogWarning($"{piece_as_mod.PieceName} was already added to the list of recipes and will be renamed {piece_as_mod.PieceName}_{randtag}, please use unique piece modification names.");
                        }
                        catch
                        {
                            Logger.LogWarning($"{piece_as_mod.PieceName} was already added to the list of recipes and will be skipped, please use unique recipe names.");
                        }
                    }
                    else
                    {
                        recipeCollection.PieceModifications.Add(piece_as_mod.PieceName, piece_as_mod);
                    }

                    if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Piece {piece_as_mod.PieceName} Added."); }
                }
                if (Config.EnableDebugMode.Value) { Logger.LogInfo($"Serializing and printing recipes."); }
                var yaml = Config.yamlserializer.Serialize(recipeCollection);
                writetext.WriteLine(yaml);
                Logger.LogInfo($"Recipes dumped to file {ODBRecipes_path}");
            }
        }
    }
}

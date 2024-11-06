using Jotunn;
using Jotunn.Entities;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RecipeManager.Common.DataObjects;

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
}

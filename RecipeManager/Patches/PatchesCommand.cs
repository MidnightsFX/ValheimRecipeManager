using Jotunn.Entities;
using RecipeManager.Common;

namespace RecipeManager.Patches
{
    internal class PatchesCommand : ConsoleCommand
    {
        public override string Name => "RM_Patches";

        public override string Help => "Lists the game patches RecipeManager can apply, whether each is on, and which config entries use it.";

        public override void Run(string[] args)
        {
            foreach (string line in PatchManager.Describe().TrimEnd().Split('\n'))
            {
                ItemCommandUtils.Respond(line.TrimEnd('\r'));
            }
        }
    }
}

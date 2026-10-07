using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RecipeManager.Common
{
    /// <summary>
    /// One game patch, and what decides whether it is needed. Most of what this mod does is plain data written onto
    /// the game's objects. The few settings the game has no data for need a patch, and a patch is only applied while
    /// a loaded config actually uses one of those settings.
    /// </summary>
    internal class PatchFeature
    {
        public readonly string Name;
        /// <summary>What the patch is for, as shown by RM_Patches.</summary>
        public readonly string Purpose;
        /// <summary>Only applied where there is a player, the game reads these settings on the player's own machine.</summary>
        public readonly bool ClientOnly;
        private readonly Func<IEnumerable<string>> users;
        private readonly List<(MethodBase target, HarmonyMethod prefix, HarmonyMethod postfix)> targets;

        public bool Active { get; private set; }

        /// <param name="users">The config entries using the patch, read from the loaded configs.</param>
        public PatchFeature(string name, string purpose, bool clientOnly, Func<IEnumerable<string>> users,
            params (MethodBase target, HarmonyMethod prefix, HarmonyMethod postfix)[] targets)
        {
            Name = name;
            Purpose = purpose;
            ClientOnly = clientOnly;
            this.users = users;
            this.targets = targets.ToList();
        }

        public List<string> Users()
        {
            try { return users().Distinct().ToList(); }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not tell which configs use the {Name} patch. Error: {ex.Message}");
                return new List<string>();
            }
        }

        internal bool Apply(Harmony harmony)
        {
            foreach ((MethodBase target, HarmonyMethod prefix, HarmonyMethod postfix) in targets)
            {
                if (target == null)
                {
                    Logger.LogError($"The {Name} patch has no method to patch in this version of the game, the settings that need it will not work.");
                    continue;
                }
                harmony.Patch(target, prefix: prefix, postfix: postfix);
            }
            Active = true;
            return true;
        }

        internal void Remove(Harmony harmony)
        {
            foreach ((MethodBase target, HarmonyMethod prefix, HarmonyMethod postfix) in targets)
            {
                if (target == null) { continue; }
                // Only this mod's own patches come off, other mods' patches on the same method stay
                if (prefix != null) { harmony.Unpatch(target, prefix.method); }
                if (postfix != null) { harmony.Unpatch(target, postfix.method); }
            }
            Active = false;
        }
    }

    /// <summary>
    /// Applies each patch while something needs it and takes it off when nothing does. Refresh is cheap and can be
    /// called after anything that changes the loaded configs.
    /// </summary>
    internal static class PatchManager
    {
        private static readonly List<PatchFeature> Features = new List<PatchFeature>();
        private static Harmony harmony;

        internal static void Register(PatchFeature feature)
        {
            if (!Features.Contains(feature)) { Features.Add(feature); }
        }

        internal static void Refresh()
        {
            if (harmony == null) { harmony = new Harmony(RecipeManager.PluginGUID); }
            bool headless = GUIManager.IsHeadless();
            foreach (PatchFeature feature in Features)
            {
                List<string> users = feature.Users();
                bool needed = users.Count > 0 && !(feature.ClientOnly && headless);
                if (needed == feature.Active) { continue; }
                try
                {
                    if (needed)
                    {
                        feature.Apply(harmony);
                        Logger.LogInfo($"Enabled the {feature.Name} patch, used by {Summarise(users)}.");
                    }
                    else
                    {
                        feature.Remove(harmony);
                        Logger.LogInfo($"Disabled the {feature.Name} patch, no config uses it any more.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Could not {(needed ? "apply" : "remove")} the {feature.Name} patch. Error: {ex}");
                }
            }
        }

        private static string Summarise(List<string> users)
        {
            const int shown = 3;
            string named = string.Join(", ", users.Take(shown));
            return users.Count > shown ? $"{named} and {users.Count - shown} more" : named;
        }

        /// <summary>Every patch, whether it is on, and what uses it.</summary>
        internal static string Describe()
        {
            StringBuilder text = new StringBuilder();
            bool headless = GUIManager.IsHeadless();
            foreach (PatchFeature feature in Features)
            {
                List<string> users = feature.Users();
                string state = feature.Active ? "on" : "off";
                if (!feature.Active && users.Count > 0 && feature.ClientOnly && headless) { state = "off, only needed where there is a player"; }
                text.AppendLine($"{feature.Name}: {state}. {feature.Purpose}");
                text.AppendLine(users.Count == 0 ? "  Used by nothing." : $"  Used by: {string.Join(", ", users)}");
            }
            return text.ToString();
        }
    }
}

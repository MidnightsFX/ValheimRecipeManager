using Jotunn.Managers;
using System;

namespace RecipeManager.Common
{
    /// <summary>Display text in configs, which is either a localization token or text registered under one.</summary>
    internal static class Tokens
    {
        internal const string ItemPrefix = "rm_item_";
        internal const string StatusEffectPrefix = "rm_se_";
        internal const string PiecePrefix = "rm_piece_";

        /// <summary>
        /// A value starting with '$' is treated as a token the user manages themselves. Anything else is
        /// registered as the English translation of a generated, per-prefab token.
        /// </summary>
        /// <returns>The token to use, or null for a value that is not set.</returns>
        internal static string Resolve(string configured, string prefix, string prefab, string suffix)
        {
            if (string.IsNullOrEmpty(configured)) { return null; }
            if (configured.StartsWith("$", StringComparison.Ordinal)) { return configured; }

            string token = $"${prefix}{prefab.ToLowerInvariant()}{suffix}";
            LocalizationManager.Instance.GetLocalization().AddTranslation(token, configured);
            return token;
        }
    }
}

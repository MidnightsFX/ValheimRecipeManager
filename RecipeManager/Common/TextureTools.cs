using HarmonyLib;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager.Common
{
    /// <summary>
    /// Dumps prefab icons and material textures to disk as PNGs, and applies user supplied
    /// PNGs back onto a cloned prefab.
    ///
    /// The file names produced by <see cref="DumpPrefab"/> are exactly the ones read back by
    /// <see cref="ApplyTextureOverrides"/>, so the intended workflow is: dump a vanilla prefab,
    /// copy the folder, rename it to the new item's prefab name, then repaint the PNGs.
    /// </summary>
    internal static class TextureTools
    {
        internal const string TextureFolderName = "Textures";
        internal const string DumpInfoFileName = "_dump_info.txt";

        private const string IconPrefix = "icon_";
        private const string SingleIconName = "icon.png";
        private const string Separator = "__";
        private const string RootRendererName = "root";

        /// <summary>Material texture slots that can be dumped and overridden.</summary>
        internal static readonly string[] TextureProperties = { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_EmissionMap" };

        /// <summary>
        /// Slots holding non-colour data. These have to round trip through a linear RenderTexture,
        /// otherwise the gamma conversion mangles them.
        /// </summary>
        private static readonly HashSet<string> LinearProperties = new HashSet<string> { "_BumpMap", "_MetallicGlossMap" };

        public static string GetTextureDirectoryPath(string prefabName)
        {
            return Path.Combine(ValConfig.GetSecondaryConfigDirectoryPath(), TextureFolderName, prefabName);
        }

        /// <summary>
        /// Dumps every icon and material texture found on <paramref name="prefab"/>.
        /// Returns the number of PNGs written.
        /// </summary>
        public static int DumpPrefab(GameObject prefab, out string outputDirectory)
        {
            outputDirectory = GetTextureDirectoryPath(prefab.name);
            Directory.CreateDirectory(outputDirectory);

            int written = 0;
            StringBuilder info = new StringBuilder();
            info.AppendLine($"Texture dump for prefab: {prefab.name}");
            info.AppendLine("Copy this folder and rename it to your new item's prefab name to use these as a starting point.");
            info.AppendLine();

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            Sprite[] icons = itemDrop?.m_itemData?.m_shared?.m_icons;
            if (icons != null && icons.Length > 0)
            {
                info.AppendLine("Icons:");
                for (int i = 0; i < icons.Length; i++)
                {
                    if (icons[i] == null) { continue; }
                    string iconFile = $"{IconPrefix}{i}.png";
                    if (WriteTexture(MakeReadable(icons[i]), Path.Combine(outputDirectory, iconFile)))
                    {
                        written++;
                        info.AppendLine($"  {iconFile}  (sprite '{icons[i].name}', {icons[i].textureRect.width}x{icons[i].textureRect.height})");
                    }
                }
                info.AppendLine();
            }
            else
            {
                info.AppendLine("Icons: none (prefab has no ItemDrop icons)");
                info.AppendLine();
            }

            info.AppendLine("Material textures:");
            foreach (Renderer renderer in ShaderHelper.GetRenderers(prefab))
            {
                if (renderer == null) { continue; }
                string rendererPath = SanitizeFileName(GetTransformPath(prefab.transform, renderer.transform));
                Material[] materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null) { continue; }
                    foreach (string property in TextureProperties)
                    {
                        if (!material.HasProperty(property)) { continue; }
                        Texture texture = material.GetTexture(property);
                        if (texture == null) { continue; }

                        string fileName = BuildTextureFileName(rendererPath, m, property);
                        if (WriteTexture(MakeReadable(texture, IsLinearProperty(property)), Path.Combine(outputDirectory, fileName)))
                        {
                            written++;
                            info.AppendLine($"  {fileName}");
                            info.AppendLine($"      renderer: {GetTransformPath(prefab.transform, renderer.transform)} ({renderer.GetType().Name})");
                            info.AppendLine($"      material: {material.name} [{m}]  shader: {material.shader?.name}");
                            info.AppendLine($"      source:   {texture.width}x{texture.height}  readable: {(texture as Texture2D)?.isReadable}");
                        }
                    }
                }
            }

            try
            {
                File.WriteAllText(Path.Combine(outputDirectory, DumpInfoFileName), info.ToString());
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not write {DumpInfoFileName}: {ex.Message}");
            }

            return written;
        }

        /// <summary>
        /// Returns the icons found in the item's texture folder, or <paramref name="fallback"/> when the
        /// user has not supplied any. Never returns an empty array - an item with no icons cannot be
        /// picked up by the player.
        /// </summary>
        public static Sprite[] LoadIcons(string prefabName, Sprite[] fallback)
        {
            string directory = GetTextureDirectoryPath(prefabName);
            if (!Directory.Exists(directory)) { return fallback; }

            List<Sprite> icons = new List<Sprite>();

            // A single "icon.png" is accepted as a friendlier alias for "icon_0.png".
            string singleIcon = Path.Combine(directory, SingleIconName);
            if (!File.Exists(Path.Combine(directory, $"{IconPrefix}0.png")) && File.Exists(singleIcon))
            {
                Sprite single = LoadSprite(singleIcon);
                if (single != null) { icons.Add(single); }
            }

            if (icons.Count == 0)
            {
                for (int i = 0; ; i++)
                {
                    string path = Path.Combine(directory, $"{IconPrefix}{i}.png");
                    if (!File.Exists(path)) { break; }
                    Sprite sprite = LoadSprite(path);
                    if (sprite == null) { break; }
                    icons.Add(sprite);
                }
            }

            if (icons.Count == 0) { return fallback; }
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Loaded {icons.Count} icon(s) for {prefabName} from {directory}"); }
            return icons.ToArray();
        }

        /// <summary>
        /// Applies any PNGs present in the item's texture folder onto the prefab's renderers.
        /// Materials are copied before being written to, so the source prefab (and therefore the
        /// vanilla item this was cloned from) is never modified.
        /// Returns the number of texture slots overridden.
        /// </summary>
        public static int ApplyTextureOverrides(GameObject prefab, string prefabName)
        {
            string directory = GetTextureDirectoryPath(prefabName);
            if (!Directory.Exists(directory)) { return 0; }

            int applied = 0;
            foreach (Renderer renderer in ShaderHelper.GetRenderers(prefab))
            {
                if (renderer == null) { continue; }
                string rendererPath = SanitizeFileName(GetTransformPath(prefab.transform, renderer.transform));
                Material[] shared = renderer.sharedMaterials;
                Material[] replacement = null;

                for (int m = 0; m < shared.Length; m++)
                {
                    Material material = shared[m];
                    if (material == null) { continue; }
                    foreach (string property in TextureProperties)
                    {
                        if (!material.HasProperty(property)) { continue; }
                        string path = Path.Combine(directory, BuildTextureFileName(rendererPath, m, property));
                        if (!File.Exists(path)) { continue; }

                        Texture2D texture = LoadTextureFile(path, IsLinearProperty(property));
                        if (texture == null) { continue; }

                        // Only clone the material array once we know something is actually being replaced.
                        if (replacement == null) { replacement = (Material[])shared.Clone(); }
                        if (ReferenceEquals(replacement[m], shared[m])) { replacement[m] = new Material(shared[m]); }

                        replacement[m].SetTexture(property, texture);
                        applied++;
                        if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Applied {property} from {Path.GetFileName(path)} to {prefabName}"); }
                    }
                }

                if (replacement != null) { renderer.sharedMaterials = replacement; }
            }

            return applied;
        }

        /// <summary>
        /// Copies a texture into a CPU readable Texture2D via a RenderTexture blit. Valheim's shipped
        /// textures are imported non-readable, so their pixels only exist in VRAM and cannot be encoded
        /// directly.
        /// </summary>
        public static Texture2D MakeReadable(Texture source, bool linear)
        {
            if (source == null) { return null; }

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.Default,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply();
                return readable;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not create a readable copy of texture '{source.name}': {ex.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        /// <summary>
        /// Readable copy of a sprite, cropped to its atlas sub-rect. Item icons are frequently packed
        /// into a shared atlas, so dumping sprite.texture directly would write the whole sheet.
        /// </summary>
        public static Texture2D MakeReadable(Sprite sprite)
        {
            if (sprite == null) { return null; }

            Texture2D full = MakeReadable(sprite.texture, false);
            if (full == null) { return null; }

            Rect rect = sprite.textureRect;
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0 || (width == full.width && height == full.height))
            {
                return full;
            }

            try
            {
                Texture2D cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
                cropped.SetPixels(full.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height));
                cropped.Apply();
                UnityEngine.Object.Destroy(full);
                return cropped;
            }
            catch (Exception ex)
            {
                // Fall back to the uncropped copy rather than losing the dump entirely.
                Logger.LogWarning($"Could not crop sprite '{sprite.name}' to its atlas rect: {ex.Message}");
                return full;
            }
        }

        private static Sprite LoadSprite(string path)
        {
            Texture2D texture = LoadTextureFile(path, false);
            if (texture == null) { return null; }
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Loads a PNG from an absolute path. Done directly rather than through AssetUtils because
        /// AssetUtils.LoadSpriteFromFile cannot take an absolute path, and AssetUtils.LoadTexture
        /// throws on any extension that is not lowercase and gives no control over the linear flag.
        /// </summary>
        private static Texture2D LoadTextureFile(string path, bool linear)
        {
            try
            {
                if (!File.Exists(path)) { return null; }
                byte[] data = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
                if (!LoadImageData(texture, data))
                {
                    Logger.LogWarning($"'{path}' is not a valid image and will be ignored.");
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }
                texture.name = Path.GetFileNameWithoutExtension(path);
                return texture;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not load texture '{path}': {ex.Message}");
                return null;
            }
        }

        private static bool WriteTexture(Texture2D texture, string path)
        {
            if (texture == null) { return false; }
            try
            {
                byte[] png = EncodePng(texture);
                if (png == null) { return false; }
                File.WriteAllBytes(path, png);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not write texture to '{path}': {ex.Message}");
                return false;
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        // UnityEngine.ImageConversion cannot be referenced directly from net48 - it pulls in netstandard
        // 2.1 and the compiler refuses. EncodeToPNG and LoadImage are therefore resolved by reflection,
        // which is the same workaround Jotunn itself uses.
        private static MethodInfo encodeToPNGMethod;
        private static MethodInfo loadImageMethod;
        private static bool imageConversionResolved;

        private static void ResolveImageConversion()
        {
            if (imageConversionResolved) { return; }
            imageConversionResolved = true;

            Type imageConversion = AccessTools.TypeByName("UnityEngine.ImageConversion");
            if (imageConversion == null)
            {
                Logger.LogError("UnityEngine.ImageConversion could not be resolved. Texture dumping and loading will not work.");
                return;
            }
            encodeToPNGMethod = AccessTools.Method(imageConversion, "EncodeToPNG", new Type[] { typeof(Texture2D) });
            loadImageMethod = AccessTools.Method(imageConversion, "LoadImage", new Type[] { typeof(Texture2D), typeof(byte[]) });
        }

        private static byte[] EncodePng(Texture2D texture)
        {
            ResolveImageConversion();
            if (encodeToPNGMethod == null) { return null; }
            return encodeToPNGMethod.Invoke(null, new object[] { texture }) as byte[];
        }

        private static bool LoadImageData(Texture2D texture, byte[] data)
        {
            ResolveImageConversion();
            if (loadImageMethod == null) { return false; }
            return (bool)loadImageMethod.Invoke(null, new object[] { texture, data });
        }

        private static bool IsLinearProperty(string property)
        {
            return LinearProperties.Contains(property);
        }

        private static string BuildTextureFileName(string rendererPath, int materialIndex, string property)
        {
            return $"{rendererPath}{Separator}{materialIndex}{Separator}{property}.png";
        }

        private static string GetTransformPath(Transform root, Transform target)
        {
            if (target == null || target == root) { return RootRendererName; }

            List<string> parts = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return parts.Count == 0 ? RootRendererName : string.Join(".", parts.ToArray());
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return value;
        }
    }
}

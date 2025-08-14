using Jotunn.Managers;
using MonoMod.Utils;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    internal static class PrefabModifier
    {
        public static Dictionary<string, PrefabMod> Modifications = new Dictionary<string, PrefabMod>();

        public static void Runner()
        {
            EnsureFile();
            Loader();
        }

        public static void RunPrefabModifications() {
            foreach (var mod in Modifications) {
                GameObject sourcePrefab = PrefabManager.Instance.GetPrefab(mod.Value.sourcePrefab.name);
                GameObject targetPrefab = PrefabManager.Instance.GetPrefab(mod.Value.targetPrefab);

                Transform attach = targetPrefab.transform;
                if (mod.Value.targetPrefabPath != null && mod.Value.targetPrefabPath.Length > 1) {
                    Logger.LogInfo($"Looking for child path {mod.Value.targetPrefabPath}");
                    foreach (Transform tform in targetPrefab.transform) {
                        Logger.LogInfo($"child: {tform}");
                    }

                    Transform ftpath = targetPrefab.transform.Find(mod.Value.targetPrefabPath);
                    if (ftpath != null) { attach = ftpath; }
                }
                // source prefab find specific path
                GameObject added = GameObject.Instantiate(sourcePrefab, attach);
                if (mod.Value.local_position != null) { added.transform.position = mod.Value.local_position; }
                if (mod.Value.rotation != null) { added.transform.rotation = mod.Value.rotation; }
                if (mod.Value.size != null) { added.transform.localScale = mod.Value.size; }
            }
        }

        public static void EnsureFile() {
            if (!File.Exists(ValConfig.ModificationConfigFilePath)) {
                File.WriteAllText(ValConfig.ModificationConfigFilePath, ValConfig.yamlserializer.Serialize(new PrefabModifierCollection() {
                    PrefabModifications = new Dictionary<string, DataObjects.PrefabMod>() { { "test", new DataObjects.PrefabMod() { 
                        sourcePrefab = new SourcePrefab() { name = "greydwarf", source = PrefabSourceType.ExistingPrefab },
                        targetPrefab = "goblin"
                    } } }
                }));
            }
        }

        // Temp loading of the file
        public static void Loader() {
            PrefabModifierCollection collection = ValConfig.yamldeserializer.Deserialize<PrefabModifierCollection>(File.ReadAllText(ValConfig.ModificationConfigFilePath));
            UpdateModifications(collection);
        }

        public static void UpdateModifications(PrefabModifierCollection newModifications) {
            Modifications.Clear();
            Modifications.AddRange(newModifications.PrefabModifications);
        }
    }
}

using HarmonyLib;
using Jotunn.Extensions;
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
    public static class PrefabModifier
    {
        public static Dictionary<string, PrefabMod> Modifications = new Dictionary<string, PrefabMod>();

        public static void Runner() {
            EnsureFile();
            Loader();
            //RunPrefabModifications();
        }

        //[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
        //public static class CreatureCharacterExtension {
        //    public static void Postfix(ItemDrop __instance)
        //    {
        //        string gameOName = __instance.gameObject.name.Replace("(Clone)", "");
        //        Logger.LogDebug($"Checking {gameOName}");
        //        if (Modifications.ContainsKey(gameOName)) {
        //            PrefabMod mod = Modifications[gameOName];
        //            Logger.LogDebug($"Running {mod.targetPrefab} modification");
        //            GameObject sourcePrefab = PrefabManager.Instance.GetPrefab(mod.sourcePrefab.name);
        //            GameObject targetPrefab = __instance.gameObject;

        //            Transform sourceTransform = sourcePrefab.transform.FindDeepChild(mod.sourcePrefabPath);
        //            if (sourceTransform != null) {
        //                Logger.LogDebug($"Found source transform {mod.sourcePrefabPath} setting as target gameObject");
        //                sourcePrefab = sourceTransform.gameObject;
        //            }
        //            //foreach(var sourcetf in sourcePrefab.transform) {
        //            //    Logger.LogInfo($"child: {sourcetf}");
        //            //}

        //            Transform attach = targetPrefab.transform;
        //            if (mod.targetPrefabPath != null && mod.targetPrefabPath.Length > 1)
        //            {
        //                Logger.LogInfo($"Looking for child path {mod.targetPrefabPath}");
        //                foreach (Transform tform in targetPrefab.transform) {
        //                    Logger.LogInfo($"child: {tform}");
        //                }

        //                Transform ftpath = targetPrefab.transform.FindDeepChild(mod.targetPrefabPath);
        //                if (ftpath != null) {
        //                    Logger.LogDebug($"Found target transform {mod.sourcePrefabPath} setting as parent");
        //                    attach = ftpath;
        //                }
        //            }
        //            // source prefab find specific path
        //            GameObject added = GameObject.Instantiate(sourcePrefab, attach);
        //            if (mod.local_position != null) { added.transform.position = mod.local_position; }
        //            if (mod.rotation != null) { added.transform.rotation = mod.rotation; }
        //            if (mod.size != null) { added.transform.localScale = new Vector3() { x = mod.size.x, y = mod.size.y, z = mod.size.z } ; }
        //        }
        //    }
        //}

        public static void EnsureFile() {
            if (!File.Exists(ValConfig.ModificationConfigFilePath)) {
                File.WriteAllText(ValConfig.ModificationConfigFilePath, ValConfig.yamlserializer.Serialize(new PrefabModifierCollection() {
                    PrefabModifications = new Dictionary<string, DataObjects.PrefabMod>() { { "GoblinClub", new DataObjects.PrefabMod() { 
                        sourcePrefab = new SourcePrefab() { name = "VAFlint_greataxe", source = PrefabSourceType.ExistingPrefab },
                        targetPrefab = "GoblinClub",
                        targetPrefabPath = "model",
                        sourcePrefabPath = "flint_greataxe",
                        size = new Vect() { x = 100, y = 100, z = 100 }
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

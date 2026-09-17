using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using RecipeManager.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    internal class ItemReloadCommand : AdminConsoleCommand
    {
        public override string Name => "RM_Items_Reload";

        public override string Help => "Reloads item additions and modifications. Stats, names, icons and textures are re-applied; new or removed items need a restart.";

        protected override void Execute(string[] args)
        {
            ValConfig.ReloadItemFiles();
            ItemUpdater.ReapplyExistingItems();
            ItemCommandUtils.Respond($"Reloaded {ItemUpdater.ItemDefinitions.Count} item entry(s), {ItemUpdater.ModifiedItems.Count} of which modify an existing item.");

            try
            {
                ValConfig.SendUpdatedItemConfigs();
            }
            catch (Exception ex)
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogDebug($"Item configs were not sent to peers: {ex.Message}"); }
            }
        }
    }

    internal class ItemPrintCommand : AdminConsoleCommand
    {
        public override string Name => "RM_Items_PrintAll";

        public override string Help => "Prints items in the Object DB to a reference file, with their real stats. Usage: RM_Items_PrintAll [nameFilter]";

        protected override void Execute(string[] args)
        {
            if (ObjectDB.instance == null)
            {
                ItemCommandUtils.Respond("The object DB is not available yet, load into a world first.");
                return;
            }

            string filter = (args != null && args.Length > 0) ? args[0] : null;

            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Starting to dump items"); }
            String ODBItems_path = Path.Combine(Paths.ConfigPath, "RecipeManager", "ObjectDBItems.yaml");
            ItemAdditionCollection itemCollection = new ItemAdditionCollection();

            foreach (GameObject itemObject in ObjectDB.instance.m_items.ToList())
            {
                if (itemObject == null) { continue; }
                if (filter != null && itemObject.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) { continue; }

                ItemDrop itemDrop = itemObject.GetComponent<ItemDrop>();
                ItemDrop.ItemData.SharedData shared = itemDrop?.m_itemData?.m_shared;
                if (shared == null) { continue; }

                if (itemCollection.ItemAdditions.ContainsKey(itemObject.name))
                {
                    Logger.LogWarning($"{itemObject.name} was already added to the list of items and will be skipped.");
                    continue;
                }

                if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Building item definition {itemObject.name}"); }
                itemCollection.ItemAdditions.Add(itemObject.name, DescribeItem(itemObject.name, shared));
            }

            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Serializing and printing items."); }
            using (StreamWriter writetext = new StreamWriter(ODBItems_path))
            {
                writetext.WriteLine("# Reference dump of the items in the object DB, with the stats they currently have.");
                writetext.WriteLine("# This file is NOT loaded by the mod, it is here so you can see what the numbers are.");
                writetext.WriteLine("#");
                writetext.WriteLine("# To change one of these items, copy its entry into your own Items.yaml and delete");
                writetext.WriteLine("# every line you do not want to change - anything left out keeps its current value.");
                writetext.WriteLine("# To create a new item from one instead, set 'action: Add', point 'clonedFrom' at the");
                writetext.WriteLine("# prefab below and give 'prefab' a new unique name.");
                writetext.WriteLine();
                writetext.WriteLine(ValConfig.yamlserializer.Serialize(itemCollection));
            }
            ItemCommandUtils.Respond($"Dumped {itemCollection.ItemAdditions.Count} item(s) to {ODBItems_path}");
        }

        /// <summary>
        /// Describes an item as the modification that would reproduce it. Blocks that do not apply to
        /// the item are left out, so a piece of armor does not carry an empty weapon section around.
        /// </summary>
        private static ItemAddition DescribeItem(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            ItemAddition described = new ItemAddition
            {
                action = ItemAction.Modify,
                prefab = prefabName,
                name = shared.m_name,
                description = shared.m_description,
                weight = shared.m_weight,
                maxStackSize = shared.m_maxStackSize,
                value = shared.m_value,
                teleportable = shared.m_teleportable
            };

            bool isFood = shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f
                || shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable;
            if (isFood)
            {
                described.food = new FoodStats
                {
                    health = shared.m_food,
                    stamina = shared.m_foodStamina,
                    eitr = shared.m_foodEitr,
                    duration = shared.m_foodBurnTime,
                    regen = shared.m_foodRegen,
                    eatAnimTime = shared.m_foodEatAnimTime,
                    isDrink = shared.m_isDrink,
                    consumeStatusEffect = shared.m_consumeStatusEffect?.name
                };
            }

            if (shared.m_damages.HaveDamage() || shared.m_damagesPerLevel.HaveDamage() || shared.m_toolTier > 0)
            {
                described.weapon = new WeaponStats
                {
                    damage = DescribeDamage(shared.m_damages),
                    damagePerLevel = DescribeDamage(shared.m_damagesPerLevel),
                    attackForce = shared.m_attackForce,
                    backstabBonus = shared.m_backstabBonus,
                    toolTier = shared.m_toolTier
                };
            }

            bool isArmor = shared.m_armor > 0f || shared.m_blockPower > 0f || shared.m_movementModifier != 0f
                || shared.m_eitrRegenModifier != 0f;
            if (isArmor)
            {
                described.armor = new ArmorStats
                {
                    armor = shared.m_armor,
                    armorPerLevel = shared.m_armorPerLevel,
                    blockPower = shared.m_blockPower,
                    blockPowerPerLevel = shared.m_blockPowerPerLevel,
                    deflectionForce = shared.m_deflectionForce,
                    deflectionForcePerLevel = shared.m_deflectionForcePerLevel,
                    movementModifier = shared.m_movementModifier,
                    eitrRegenModifier = shared.m_eitrRegenModifier
                };
            }

            if (shared.m_damageModifiers != null && shared.m_damageModifiers.Count > 0)
            {
                described.resistances = new List<DamageResistance>();
                foreach (HitData.DamageModPair pair in shared.m_damageModifiers)
                {
                    described.resistances.Add(new DamageResistance { type = pair.m_type, modifier = pair.m_modifier });
                }
            }

            if (shared.m_equipStatusEffect != null || shared.m_attackStatusEffect != null)
            {
                described.statusEffects = new ItemStatusEffects
                {
                    equipped = shared.m_equipStatusEffect?.name,
                    onHit = shared.m_attackStatusEffect?.name,
                    onHitChance = shared.m_attackStatusEffect != null ? shared.m_attackStatusEffectChance : (float?)null
                };
            }

            if (!string.IsNullOrEmpty(shared.m_setName))
            {
                described.setBonus = new SetBonus
                {
                    name = shared.m_setName,
                    size = shared.m_setSize,
                    statusEffect = shared.m_setStatusEffect?.name
                };
            }

            return described;
        }

        private static DamageValues DescribeDamage(HitData.DamageTypes damages)
        {
            return new DamageValues
            {
                damage = damages.m_damage,
                blunt = damages.m_blunt,
                slash = damages.m_slash,
                pierce = damages.m_pierce,
                chop = damages.m_chop,
                pickaxe = damages.m_pickaxe,
                fire = damages.m_fire,
                frost = damages.m_frost,
                lightning = damages.m_lightning,
                poison = damages.m_poison,
                spirit = damages.m_spirit
            };
        }

        public override List<string> CommandOptionList()
        {
            List<string> options = new List<string>();
            if (ObjectDB.instance?.m_items == null) { return options; }
            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                if (item != null) { options.Add(item.name); }
            }
            return options;
        }
    }

    internal class TextureDumpCommand : AdminConsoleCommand
    {
        public override string Name => "RM_Textures_Dump";

        public override string Help => "Dumps a prefab's icons and material textures to disk as PNGs. Usage: RM_Textures_Dump <prefabName>";

        protected override void Execute(string[] args)
        {
            if (args == null || args.Length < 1 || string.IsNullOrEmpty(args[0]))
            {
                ItemCommandUtils.Respond($"Usage: {Name} <prefabName>");
                return;
            }

            string prefabName = args[0];
            GameObject prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                ItemCommandUtils.Respond($"No prefab named '{prefabName}' could be found. Prefab names are case sensitive.");
                return;
            }

            try
            {
                string directory;
                int written = TextureTools.DumpPrefab(prefab, out directory);
                if (written == 0)
                {
                    ItemCommandUtils.Respond($"Found no textures to dump for '{prefabName}'. See {directory}\\{TextureTools.DumpInfoFileName}");
                }
                else
                {
                    ItemCommandUtils.Respond($"Dumped {written} texture(s) for '{prefabName}' to {directory}");
                }
            }
            catch (Exception ex)
            {
                ItemCommandUtils.Respond($"Dumping textures for '{prefabName}' failed: {ex.Message}");
                Logger.LogError($"Texture dump for '{prefabName}' failed. Error: {ex}");
            }
        }

        public override List<string> CommandOptionList()
        {
            List<string> options = new List<string>();
            if (ObjectDB.instance?.m_items == null) { return options; }
            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                if (item != null) { options.Add(item.name); }
            }
            return options;
        }
    }

    internal static class ItemCommandUtils
    {
        /// <summary>
        /// Echoes to the dev console as well as the log. Jotunn 2.24.3 only exposes Run(string[]) with
        /// no terminal argument, so the console is reached through its singleton instead.
        /// </summary>
        internal static void Respond(string message)
        {
            Logger.LogInfo(message);
            if (Console.instance != null) { Console.instance.Print(message); }
        }
    }
}

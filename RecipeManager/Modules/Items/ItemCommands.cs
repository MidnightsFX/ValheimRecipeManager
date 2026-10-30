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
    internal class ItemReloadCommand : ConsoleCommand
    {
        public override string Name => "RM_Items_Reload";

        public override string Help => "Reloads item additions and modifications. Stats, names, icons and textures are re-applied; new or removed items need a restart.";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            ValConfig.Items.ReloadFromDisk();
            ItemCommandUtils.Respond($"Reloaded {ItemUpdater.ItemDefinitions.Count} item entry(s), {ItemUpdater.ModifiedItems.Count} of which modify an existing item.");
        }
    }

    internal class ItemPrintCommand : ConsoleCommand
    {
        internal const string DumpFileName = "ObjectDBItems.yaml";

        public override string Name => "RM_Items_PrintAll";

        public override string Help => "Prints items in the Object DB to a reference file, with their real stats. Usage: RM_Items_PrintAll [nameFilter]";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            if (ObjectDB.instance == null)
            {
                ItemCommandUtils.Respond("The object DB is not available yet, load into a world first.");
                return;
            }

            string filter = (args != null && args.Length > 0) ? args[0] : null;

            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo("Starting to dump items"); }
            String ODBItems_path = Path.Combine(Paths.ConfigPath, "RecipeManager", DumpFileName);
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
                teleportable = shared.m_teleportable,
                equipDuration = shared.m_equipDuration,
                questItem = shared.m_questItem ? true : (bool?)null,
                appendToolTip = shared.m_appendToolTip != null ? shared.m_appendToolTip.gameObject.name : null,
                quality = new ItemQuality
                {
                    maxLevel = shared.m_maxQuality,
                    weightPerLevel = shared.m_scaleWeightByQuality != 0f ? shared.m_scaleWeightByQuality : (float?)null
                }
            };
            // Only items that can be upgraded go to the Forge of Potential
            if (shared.m_maxQuality > 1)
            {
                described.quality.upgradeChance = shared.m_upgradeChance;
                described.quality.breakChance = shared.m_breakChance;
                described.quality.upgradeSteps = shared.m_successUpgradeSteps;
                described.quality.breakRefund = shared.m_breakReturnIngreientsAmount;
            }

            if (shared.m_useDurability)
            {
                described.durability = new ItemDurability
                {
                    enabled = true,
                    max = shared.m_maxDurability,
                    perLevel = shared.m_durabilityPerLevel,
                    drainPerUse = shared.m_useDurabilityDrain,
                    drainPerSecond = shared.m_durabilityDrain,
                    repairable = shared.m_canBeReparied,
                    destroyWhenBroken = shared.m_destroyBroken
                };
            }

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

            // Bucklers fire their attack when their block charges fill up
            bool attacks = UsesAttacks(shared.m_itemType) || (shared.m_itemType == ItemDrop.ItemData.ItemType.Shield && shared.m_buildBlockCharges);
            // Every item carries a block power, only shields and held weapons are ever blocked with
            bool canBlock = shared.m_blockPower > 0f && (shared.m_itemType == ItemDrop.ItemData.ItemType.Shield
                || (UsesAttacks(shared.m_itemType) && shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo));
            if (attacks || shared.m_damages.HaveDamage() || shared.m_damagesPerLevel.HaveDamage() || shared.m_toolTier > 0)
            {
                described.weapon = new WeaponStats
                {
                    damage = DescribeDamage(shared.m_damages),
                    damagePerLevel = DescribeDamage(shared.m_damagesPerLevel),
                    attackForce = shared.m_attackForce,
                    backstabBonus = shared.m_backstabBonus,
                    toolTier = shared.m_toolTier,
                    skill = shared.m_skillType.ToString(),
                    ammoType = string.IsNullOrEmpty(shared.m_ammoType) ? null : shared.m_ammoType,
                    dodgeable = shared.m_dodgeable,
                    blockable = shared.m_blockable,
                    // Left out at the game's default, so a dump copied into a config does not stop fire and lightning defaulting to 0
                    hitVariant = shared.m_hitVariant >= 0 ? shared.m_hitVariant : (int?)null,
                    spawnOnHit = shared.m_spawnOnHit != null ? shared.m_spawnOnHit.name : null,
                    spawnOnHitTerrain = shared.m_spawnOnHitTerrain != null ? shared.m_spawnOnHitTerrain.name : null
                };
                if (attacks)
                {
                    described.weapon.primaryAttack = DescribeAttack(shared.m_attack);
                    // Only weapons with a second attack animation have one worth changing
                    if (shared.m_secondaryAttack != null && !string.IsNullOrEmpty(shared.m_secondaryAttack.m_attackAnimation))
                    {
                        described.weapon.secondaryAttack = DescribeAttack(shared.m_secondaryAttack);
                    }
                }
            }

            bool isArmor = shared.m_armor > 0f || shared.m_blockPower > 0f || shared.m_movementModifier != 0f
                || shared.m_eitrRegenModifier != 0f || shared.m_heatResistanceModifier != 0f;
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
                    eitrRegenModifier = shared.m_eitrRegenModifier,
                    heatResistanceModifier = shared.m_heatResistanceModifier != 0f ? shared.m_heatResistanceModifier : (float?)null
                };
                if (canBlock)
                {
                    described.armor.timedBlockBonus = shared.m_timedBlockBonus;
                    described.armor.perfectBlockStaminaRegen = shared.m_perfectBlockStaminaRegen;
                    described.armor.buildBlockCharges = shared.m_buildBlockCharges;
                    if (shared.m_buildBlockCharges)
                    {
                        described.armor.maxBlockCharges = shared.m_maxBlockCharges;
                        described.armor.blockChargeDecayTime = shared.m_blockChargeDecayTime;
                    }
                }
            }

            bool staminaModified = shared.m_homeItemsStaminaModifier != 0f || shared.m_jumpStaminaModifier != 0f || shared.m_attackStaminaModifier != 0f
                || shared.m_blockStaminaModifier != 0f || shared.m_dodgeStaminaModifier != 0f || shared.m_swimStaminaModifier != 0f
                || shared.m_sneakStaminaModifier != 0f || shared.m_runStaminaModifier != 0f;
            if (staminaModified)
            {
                described.staminaModifiers = new StaminaModifiers
                {
                    homeItems = shared.m_homeItemsStaminaModifier,
                    jump = shared.m_jumpStaminaModifier,
                    attack = shared.m_attackStaminaModifier,
                    block = shared.m_blockStaminaModifier,
                    dodge = shared.m_dodgeStaminaModifier,
                    swim = shared.m_swimStaminaModifier,
                    sneak = shared.m_sneakStaminaModifier,
                    run = shared.m_runStaminaModifier
                };
            }

            // max is read from what is worn, block and perfectBlock from whatever does the blocking
            if (shared.m_maxAdrenaline > 0f || canBlock)
            {
                described.adrenaline = new AdrenalineStats
                {
                    max = shared.m_maxAdrenaline > 0f ? shared.m_maxAdrenaline : (float?)null,
                    block = canBlock ? shared.m_blockAdrenaline : (float?)null,
                    perfectBlock = canBlock ? shared.m_perfectBlockAdrenaline : (float?)null
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

            if (shared.m_equipStatusEffect != null || shared.m_attackStatusEffect != null || shared.m_perfectBlockStatusEffect != null || shared.m_fullAdrenalineSE != null)
            {
                described.statusEffects = new ItemStatusEffects
                {
                    equipped = shared.m_equipStatusEffect?.name,
                    onHit = shared.m_attackStatusEffect?.name,
                    onHitChance = shared.m_attackStatusEffect != null ? shared.m_attackStatusEffectChance : (float?)null,
                    perfectBlock = shared.m_perfectBlockStatusEffect?.name,
                    fullAdrenaline = shared.m_fullAdrenalineSE?.name
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
                spirit = damages.m_spirit,
                nonPlayer = damages.m_nonPlayer != 0f ? damages.m_nonPlayer : (float?)null
            };
        }

        /// <summary>Item types the player swings, fires or throws, which are the ones whose attacks are worth listing.</summary>
        private static bool UsesAttacks(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Ammo:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>An attack with every setting written out. Projectile, reload and draw blocks only where the attack uses them.</summary>
        private static AttackStats DescribeAttack(Attack attack)
        {
            if (attack == null) { return null; }
            AttackStats stats = new AttackStats
            {
                stamina = attack.m_attackStamina,
                eitr = attack.m_attackEitr,
                health = attack.m_attackHealth,
                healthPercentage = attack.m_attackHealthPercentage,
                adrenaline = attack.m_attackAdrenaline,
                useAdrenaline = attack.m_attackUseAdrenaline,
                speedFactor = attack.m_speedFactor,
                rotationSpeedFactor = attack.m_speedFactorRotation,
                damageMultiplier = attack.m_damageMultiplier,
                forceMultiplier = attack.m_forceMultiplier,
                staggerMultiplier = attack.m_staggerMultiplier,
                lastChainDamageMultiplier = attack.m_attackChainLevels > 1 ? attack.m_lastChainDamageMultiplier : (float?)null,
                damageMultiplierPerMissingHealth = attack.m_damageMultiplierPerMissingHP != 0f ? attack.m_damageMultiplierPerMissingHP : (float?)null,
                damageMultiplierByHealthMissing = attack.m_damageMultiplierByTotalHealthMissing != 0f ? attack.m_damageMultiplierByTotalHealthMissing : (float?)null,
                staminaReturnPerMissingHealth = attack.m_staminaReturnPerMissingHP != 0f ? attack.m_staminaReturnPerMissingHP : (float?)null,
                range = attack.m_attackRange,
                height = attack.m_attackHeight,
                angle = attack.m_attackAngle,
                rayWidth = attack.m_attackRayWidth,
                multiHit = attack.m_multiHit,
                lowerDamagePerHit = attack.m_lowerDamagePerHit,
                hitTerrain = attack.m_hitTerrain,
                hitFriendly = attack.m_hitFriendly,
                hitThroughWalls = attack.m_hitThroughWalls,
                recoilPushback = attack.m_recoilPushback,
                selfDamage = attack.m_selfDamage,
                consumeItem = attack.m_consumeItem,
                raiseSkillAmount = attack.m_raiseSkillAmount,
                startNoise = attack.m_attackStartNoise,
                hitNoise = attack.m_attackHitNoise,
                spawnOnTrigger = attack.m_spawnOnTrigger != null ? attack.m_spawnOnTrigger.name : null,
                spawnOnHit = attack.m_spawnOnHit != null ? attack.m_spawnOnHit.name : null,
                spawnOnHitChance = attack.m_spawnOnHit != null ? attack.m_spawnOnHitChance : (float?)null
            };
            if (attack.m_attackProjectile != null || attack.m_attackType == Attack.AttackType.Projectile)
            {
                stats.projectile = new ProjectileStats
                {
                    prefab = attack.m_attackProjectile != null ? attack.m_attackProjectile.name : null,
                    velocity = attack.m_projectileVel,
                    velocityMin = attack.m_projectileVelMin,
                    accuracy = attack.m_projectileAccuracy,
                    accuracyMin = attack.m_projectileAccuracyMin,
                    skillAccuracy = attack.m_skillAccuracy,
                    count = attack.m_projectiles,
                    bursts = attack.m_projectileBursts,
                    burstInterval = attack.m_burstInterval,
                    launchAngle = attack.m_launchAngle
                };
            }
            if (attack.m_requiresReload)
            {
                stats.reload = new ReloadStats { time = attack.m_reloadTime, staminaDrain = attack.m_reloadStaminaDrain, eitrDrain = attack.m_reloadEitrDrain };
            }
            if (attack.m_bowDraw)
            {
                stats.draw = new DrawStats { durationMin = attack.m_drawDurationMin, staminaDrain = attack.m_drawStaminaDrain, eitrDrain = attack.m_drawEitrDrain };
            }
            return stats;
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

    internal class TextureDumpCommand : ConsoleCommand
    {
        public override string Name => "RM_Textures_Dump";

        public override string Help => "Dumps a prefab's icons and material textures to disk as PNGs. Usage: RM_Textures_Dump <prefabName>";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
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

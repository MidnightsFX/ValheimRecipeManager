using System;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager.Importer
{
    /// <summary>A WackysDatabase Item_ file as an Items.yaml entry.</summary>
    internal static class ItemImporter
    {
        private const string AttackShape = "changing an attack's type or animation can leave the weapon stuck, RecipeManager only changes its numbers";

        private static readonly Dictionary<string, string> Reasons = new Dictionary<string, string>
        {
            ["FoodStats.feastStacks"] = WackyImporter.NoEquivalent,
            ["SE_SET_Equips"] = WackyImporter.NeedsPatch,
            ["SE_SET_Equip.HideEquipEffectsUntilSetComplete"] = WackyImporter.NeedsPatch,
            ["material"] = WackyImporter.Visual,
            ["materials"] = WackyImporter.Visual,
            ["customVisual"] = WackyImporter.Visual,
            ["snapshotRotation"] = WackyImporter.Visual,
            ["snapshotOnMaterialChange"] = WackyImporter.Visual,
            ["sizeMultiplier"] = WackyImporter.Visual,
            ["GEffects"] = WackyImporter.Visual,
            ["GEffectsPLUS"] = WackyImporter.Visual,
            ["m_itemType"] = "changing what kind of item it is can break equipping it",
            ["Attach_Override"] = "changing what kind of item it is can break equipping it",
            ["m_animationState"] = "changing what kind of item it is can break equipping it",
            // Inside Primary_Attack and Secondary_Attack, matched on their own name
            ["Custom_AttackSpeed"] = WackyImporter.NeedsPatch,
            ["ReloadTimeMultiplier"] = WackyImporter.NeedsPatch,
            ["Attack_status_effect"] = $"a status effect for one attack {WackyImporter.NeedsPatch}, the item's own onHit effect covers both attacks",
            ["Attack_status_effect_chance"] = $"a status effect for one attack {WackyImporter.NeedsPatch}, the item's own onHit effect covers both attacks",
            ["AttackType"] = AttackShape,
            ["Attack_Animation"] = AttackShape,
            ["Attack_Random_Animation"] = AttackShape,
            ["Chain_Attacks"] = AttackShape,
            ["Reload_Animation"] = AttackShape,
            ["Requires_Reload"] = AttackShape,
            ["Bow_Draw"] = AttackShape,
            ["Bow_Animation_State"] = AttackShape,
            ["Looping_Attack"] = AttackShape,
            ["AEffects"] = WackyImporter.Visual,
            ["AEffectsPLUS"] = WackyImporter.Visual
        };

        internal static void Import(WackyImporter ctx, WackyFile file)
        {
            ImportReport.FileResult result = ctx.Report.Add(file);
            WackyNode node = ctx.Parse(file, result);
            if (node == null) { return; }

            string name = node.Str("name");
            if (string.IsNullOrEmpty(name))
            {
                result.Skipped = "it has no name";
                return;
            }
            if (node.Has("mockName"))
            {
                result.Skipped = "it is an item built from a 3D model file, which RecipeManager cannot do";
                return;
            }

            node.Converted.Add($"name -> prefab: {name}");
            string clone = node.Str("clonePrefabName");
            bool add = !string.IsNullOrEmpty(clone);
            GameObject source = WackyImporter.FindItem(add ? clone : name);
            if (add)
            {
                node.Converted.Add($"clonePrefabName -> action: Add, clonedFrom: {clone}");
                if (source == null) { result.Warnings.Add($"no item named {clone} exists in this game to clone"); }
                if (!ctx.WackyDBLoaded && WackyImporter.FindItem(name) != null)
                {
                    result.Warnings.Add($"an item named {name} already exists, so RecipeManager will not add another one. Use a new name, or change it as a Modify instead");
                }
            }
            else if (source == null)
            {
                result.Warnings.Add($"no item named {name} exists in this game, the entry was written anyway");
            }

            ItemAddition item = new ItemAddition
            {
                action = add ? ItemAction.Add : ItemAction.Modify,
                prefab = name,
                clonedFrom = add ? clone : null,
                weight = node.Float("m_weight", "weight"),
                name = node.Str("m_name", "name"),
                description = node.Str("m_description", "description"),
                maxStackSize = node.Int("m_maxStackSize", "maxStackSize"),
                value = node.Int("m_value", "value"),
                teleportable = node.Bool("m_teleportable", "teleportable"),
                equipDuration = node.Float("m_equipDuration", "equipDuration"),
                questItem = node.Bool("m_questItem", "questItem"),
                appendToolTip = ctx.PrefabRef(node, "AppendToolTip", "appendToolTip", result, exactDelete: true)
            };
            if (add && item.name == null)
            {
                result.Warnings.Add("it sets no m_name, so RecipeManager names the new item after its prefab");
            }

            // Each block is read once, reading one twice would report its leftovers twice
            WackyNode shield = node.Child("ShieldStats");
            WackyNode modifiers = node.Child("Moddifiers");
            WackyNode adrenaline = node.Child("AdrenalineStats");

            item.quality = Quality(node);
            item.durability = Durability(node);
            item.weapon = Weapon(ctx, node, result);
            item.armor = Armor(node.Child("Armor"), shield, modifiers);
            item.staminaModifiers = Stamina(modifiers);
            item.adrenaline = Adrenaline(adrenaline);
            ItemDrop.ItemData.SharedData shared = source != null ? source.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            item.food = Food(ctx, node, result);
            item.statusEffects = StatusEffects(ctx, node, shield, adrenaline, shared, result);
            item.setBonus = SetBonus(ctx, node, result);
            item.resistances = Resistances(node, result);
            ctx.QueueIcon(node, "customIcon", name, result);

            ImportReport.Collect(result, node, key => WackyImporter.ReasonFor(Reasons, key), (key, value) => ctx.SameAsGame(shared, key, value));

            string entryKey = ctx.NewKey("item", name, ItemUpdater.ItemDefinitions.Keys);
            ctx.Items.Add(entryKey, item);
            if (add) { ctx.ClonedItems.Add(name); }
            result.Targets.Add($"{entryKey} ({item.action}) in Wacky_Items.yaml");
        }

        private static ItemQuality Quality(WackyNode node)
        {
            ItemQuality quality = new ItemQuality
            {
                maxLevel = node.Int("m_maxQuality", "quality.maxLevel"),
                weightPerLevel = node.Float("scale_weight_by_quality", "quality.weightPerLevel"),
                upgradeChance = node.Float("m_upgradeChance", "quality.upgradeChance"),
                breakChance = node.Float("m_breakChance", "quality.breakChance"),
                upgradeSteps = node.Int("m_successUpgradeSteps", "quality.upgradeSteps"),
                breakRefund = node.Float("m_breakReturnIngreientsAmount", "quality.breakRefund")
            };
            bool any = quality.maxLevel != null || quality.weightPerLevel != null || quality.upgradeChance != null || quality.breakChance != null
                || quality.upgradeSteps != null || quality.breakRefund != null;
            return any ? quality : null;
        }

        private static ItemDurability Durability(WackyNode node)
        {
            ItemDurability durability = new ItemDurability
            {
                enabled = node.Bool("m_useDurability", "durability.enabled"),
                max = node.Float("m_maxDurability", "durability.max"),
                perLevel = node.Float("m_durabilityPerLevel", "durability.perLevel"),
                drainPerUse = node.Float("m_useDurabilityDrain", "durability.drainPerUse"),
                drainPerSecond = node.Float("m_durabilityDrain", "durability.drainPerSecond"),
                repairable = node.Bool("m_canBeReparied", "durability.repairable"),
                destroyWhenBroken = node.Bool("m_destroyBroken", "durability.destroyWhenBroken")
            };
            bool any = durability.enabled != null || durability.max != null || durability.perLevel != null || durability.drainPerUse != null
                || durability.drainPerSecond != null || durability.repairable != null || durability.destroyWhenBroken != null;
            return any ? durability : null;
        }

        private static WeaponStats Weapon(WackyImporter ctx, WackyNode node, ImportReport.FileResult result)
        {
            WeaponStats weapon = new WeaponStats
            {
                damage = Damage(node.Child("Damage"), "weapon.damage"),
                damagePerLevel = Damage(node.Child("Damage_Per_Level"), "weapon.damagePerLevel"),
                attackForce = node.Float("m_knockback", "weapon.attackForce"),
                backstabBonus = node.Float("m_backstabbonus", "weapon.backstabBonus"),
                toolTier = node.Int("m_toolTier", "weapon.toolTier"),
                skill = node.Str("m_skillType", "weapon.skill"),
                ammoType = AmmoType(node),
                dodgeable = node.Bool("m_dodgeable", "weapon.dodgeable"),
                blockable = node.Bool("blockable", "weapon.blockable"),
                spawnOnHit = ctx.PrefabRef(node, "spawn_on_hit", "weapon.spawnOnHit", result, exactDelete: true),
                spawnOnHitTerrain = ctx.PrefabRef(node, "spawn_on_terrain_hit", "weapon.spawnOnHitTerrain", result, exactDelete: true),
                primaryAttack = Attack(ctx, node.Child("Primary_Attack"), "weapon.primaryAttack", result),
                secondaryAttack = Attack(ctx, node.Child("Secondary_Attack"), "weapon.secondaryAttack", result)
            };
            bool any = weapon.damage != null || weapon.damagePerLevel != null || weapon.attackForce != null || weapon.backstabBonus != null
                || weapon.toolTier != null || weapon.skill != null || weapon.ammoType != null || weapon.dodgeable != null || weapon.blockable != null
                || weapon.spawnOnHit != null || weapon.spawnOnHitTerrain != null || weapon.primaryAttack != null || weapon.secondaryAttack != null;
            return any ? weapon : null;
        }

        /// <summary>'delete' in any case clears the ammo type in WackysDatabase, an empty value does in RecipeManager.</summary>
        private static string AmmoType(WackyNode node)
        {
            string ammo = node.Str("ammoType");
            if (ammo == null) { return null; }
            if (WackyImporter.IsDelete(ammo))
            {
                node.Converted.Add("ammoType: delete -> weapon.ammoType: ''");
                return string.Empty;
            }
            node.Converted.Add("ammoType -> weapon.ammoType");
            return ammo;
        }

        /// <summary>
        /// WackysDatabase sets every damage type from a damage block, so a type it leaves out becomes 0. Every type is
        /// written out to do the same, since RecipeManager keeps the types an entry leaves out.
        /// </summary>
        private static DamageValues Damage(WackyNode block, string rmKey)
        {
            if (block == null) { return null; }
            DamageValues damage = new DamageValues
            {
                damage = block.Float("Damage") ?? 0,
                blunt = block.Float("Blunt") ?? 0,
                slash = block.Float("Slash") ?? 0,
                pierce = block.Float("Pierce") ?? 0,
                chop = block.Float("Chop") ?? 0,
                pickaxe = block.Float("Pickaxe") ?? 0,
                fire = block.Float("Fire") ?? 0,
                frost = block.Float("Frost") ?? 0,
                lightning = block.Float("Lightning") ?? 0,
                poison = block.Float("Poison") ?? 0,
                spirit = block.Float("Spirit") ?? 0,
                nonPlayer = block.Float("NonPlayer") ?? 0
            };
            block.Converted.Add($"{block.Name} -> {rmKey}, every damage type, 0 where it lists none");
            return damage;
        }

        /// <summary>The numbers of one attack. Its type, animations and effects stay as the weapon has them.</summary>
        private static AttackStats Attack(WackyImporter ctx, WackyNode block, string rmKey, ImportReport.FileResult result)
        {
            if (block == null) { return null; }
            string to = rmKey + ".";
            AttackStats attack = new AttackStats
            {
                stamina = block.Float("m_attackStamina", to + "stamina"),
                eitr = block.Float("m_eitrCost", to + "eitr"),
                health = block.Float("AttackHealthCost", to + "health"),
                healthPercentage = block.Float("m_attackHealthPercentage", to + "healthPercentage"),
                adrenaline = block.Float("m_attackAdrenaline", to + "adrenaline"),
                useAdrenaline = block.Float("m_attackUseAdrenaline", to + "useAdrenaline"),
                speedFactor = block.Float("SpeedFactor", to + "speedFactor"),
                damageMultiplier = block.Float("DmgMultiplier", to + "damageMultiplier"),
                forceMultiplier = block.Float("ForceMultiplier", to + "forceMultiplier"),
                staggerMultiplier = block.Float("StaggerMultiplier", to + "staggerMultiplier"),
                lastChainDamageMultiplier = block.Float("Last_Chain_Dmg_Multiplier", to + "lastChainDamageMultiplier"),
                damageMultiplierPerMissingHealth = block.Float("Dmg_Multiplier_Per_Missing_Health", to + "damageMultiplierPerMissingHealth"),
                damageMultiplierByHealthMissing = block.Float("Damage_Multiplier_By_Health_Deficit_Percent", to + "damageMultiplierByHealthMissing"),
                staminaReturnPerMissingHealth = block.Float("Stamina_Return_Per_Missing_HP", to + "staminaReturnPerMissingHealth"),
                range = block.Float("AttackRange", to + "range"),
                height = block.Float("AttackHeight", to + "height"),
                angle = block.Float("Attack_Angle", to + "angle"),
                rayWidth = block.Float("Attack_Ray_Width", to + "rayWidth"),
                multiHit = block.Bool("Multi_Hit", to + "multiHit"),
                lowerDamagePerHit = block.Bool("Lower_Dmg_Per_Hit", to + "lowerDamagePerHit"),
                hitTerrain = block.Bool("Hit_Terrain", to + "hitTerrain"),
                hitFriendly = block.Bool("Hit_Friendly", to + "hitFriendly"),
                hitThroughWalls = block.Bool("Hit_Through_Walls", to + "hitThroughWalls"),
                recoilPushback = block.Float("RecoilMultiplier", to + "recoilPushback"),
                selfDamage = block.Int("SelfDamage", to + "selfDamage"),
                consumeItem = block.Bool("Consume_Item", to + "consumeItem"),
                raiseSkillAmount = block.Float("Raise_Skill_Amount", to + "raiseSkillAmount"),
                startNoise = block.Float("Attack_Start_Noise", to + "startNoise"),
                hitNoise = block.Float("Attack_Hit_Noise", to + "hitNoise"),
                spawnOnTrigger = ctx.PrefabRef(block, "Spawn_On_Trigger", to + "spawnOnTrigger", result),
                spawnOnHit = ctx.PrefabRef(block, "SpawnOnHit", to + "spawnOnHit", result),
                spawnOnHitChance = block.Float("SpawnOnHit_Chance", to + "spawnOnHitChance")
            };

            // WackysDatabase still reads its old misspelling when the new name is not there
            float? accuracy = block.Float("Projectile_Accuracy", to + "projectile.accuracy");
            if (accuracy == null) { accuracy = block.Float("Projectile_Accuraccy", to + "projectile.accuracy"); }
            else { block.Use("Projectile_Accuraccy"); }
            ProjectileStats projectile = new ProjectileStats
            {
                prefab = ctx.PrefabRef(block, "Attack_Projectile", to + "projectile.prefab", result),
                velocity = block.Float("Projectile_Vel", to + "projectile.velocity"),
                accuracy = accuracy,
                accuracyMin = block.Float("Projectile_Accuracy_Min", to + "projectile.accuracyMin"),
                skillAccuracy = block.Bool("Skill_Accuracy", to + "projectile.skillAccuracy"),
                count = block.Int("Projectiles", to + "projectile.count"),
                bursts = block.Int("Projectile_Burst", to + "projectile.bursts"),
                burstInterval = block.Float("Burst_Interval", to + "projectile.burstInterval"),
                launchAngle = block.Float("Launch_Angle", to + "projectile.launchAngle")
            };
            if (projectile.prefab != null || projectile.velocity != null || projectile.accuracy != null || projectile.accuracyMin != null || projectile.skillAccuracy != null
                || projectile.count != null || projectile.bursts != null || projectile.burstInterval != null || projectile.launchAngle != null)
            {
                attack.projectile = projectile;
            }

            ReloadStats reload = new ReloadStats
            {
                time = block.Float("ReloadTime", to + "reload.time"),
                staminaDrain = block.Float("Reload_Stamina_Drain", to + "reload.staminaDrain"),
                eitrDrain = block.Float("Reload_Eitr_Drain", to + "reload.eitrDrain")
            };
            if (reload.time != null || reload.staminaDrain != null || reload.eitrDrain != null) { attack.reload = reload; }

            DrawStats draw = new DrawStats
            {
                durationMin = block.Float("Bow_Duration_Min", to + "draw.durationMin"),
                staminaDrain = block.Float("Bow_Stamina_Drain", to + "draw.staminaDrain")
            };
            if (draw.durationMin != null || draw.staminaDrain != null) { attack.draw = draw; }

            return attack;
        }

        private static ArmorStats Armor(WackyNode armorBlock, WackyNode shield, WackyNode modifiers)
        {
            ArmorStats armor = new ArmorStats
            {
                armor = armorBlock?.Float("armor", "armor.armor"),
                armorPerLevel = armorBlock?.Float("armorPerLevel", "armor.armorPerLevel"),
                blockPower = shield?.Float("m_blockPower", "armor.blockPower"),
                blockPowerPerLevel = shield?.Float("m_blockPowerPerLevel", "armor.blockPowerPerLevel"),
                deflectionForce = shield?.Float("m_deflectionForce", "armor.deflectionForce"),
                deflectionForcePerLevel = shield?.Float("m_deflectionForcePerLevel", "armor.deflectionForcePerLevel"),
                timedBlockBonus = shield?.Float("m_timedBlockBonus", "armor.timedBlockBonus"),
                perfectBlockStaminaRegen = shield?.Float("m_perfectBlockStaminaRegen", "armor.perfectBlockStaminaRegen"),
                buildBlockCharges = shield?.Bool("m_buildBlockCharges", "armor.buildBlockCharges"),
                maxBlockCharges = shield?.Int("m_maxBlockCharges", "armor.maxBlockCharges"),
                blockChargeDecayTime = shield?.Float("m_blockChargeDecayTime", "armor.blockChargeDecayTime"),
                movementModifier = modifiers?.Float("m_movementModifier", "armor.movementModifier"),
                eitrRegenModifier = modifiers?.Float("m_EitrRegen", "armor.eitrRegenModifier"),
                heatResistanceModifier = modifiers?.Float("m_heatResistanceModifier", "armor.heatResistanceModifier")
            };
            bool any = armor.armor != null || armor.armorPerLevel != null || armor.blockPower != null || armor.blockPowerPerLevel != null
                || armor.deflectionForce != null || armor.deflectionForcePerLevel != null || armor.timedBlockBonus != null
                || armor.perfectBlockStaminaRegen != null || armor.buildBlockCharges != null || armor.maxBlockCharges != null
                || armor.blockChargeDecayTime != null || armor.movementModifier != null || armor.eitrRegenModifier != null
                || armor.heatResistanceModifier != null;
            return any ? armor : null;
        }

        private static StaminaModifiers Stamina(WackyNode modifiers)
        {
            if (modifiers == null) { return null; }
            StaminaModifiers stamina = new StaminaModifiers
            {
                homeItems = modifiers.Float("m_homeItemsStaminaModifier", "staminaModifiers.homeItems"),
                jump = modifiers.Float("m_jumpStaminaModifier", "staminaModifiers.jump"),
                attack = modifiers.Float("m_attackStaminaModifier", "staminaModifiers.attack"),
                block = modifiers.Float("m_blockStaminaModifier", "staminaModifiers.block"),
                dodge = modifiers.Float("m_dodgeStaminaModifier", "staminaModifiers.dodge"),
                swim = modifiers.Float("m_swimStaminaModifier", "staminaModifiers.swim"),
                sneak = modifiers.Float("m_sneakStaminaModifier", "staminaModifiers.sneak"),
                run = modifiers.Float("m_runStaminaModifier", "staminaModifiers.run")
            };
            bool any = stamina.homeItems != null || stamina.jump != null || stamina.attack != null || stamina.block != null
                || stamina.dodge != null || stamina.swim != null || stamina.sneak != null || stamina.run != null;
            return any ? stamina : null;
        }

        private static AdrenalineStats Adrenaline(WackyNode block)
        {
            if (block == null) { return null; }
            AdrenalineStats adrenaline = new AdrenalineStats
            {
                max = block.Float("maxAdrenaline", "adrenaline.max"),
                block = block.Float("blockAdrenaline", "adrenaline.block"),
                perfectBlock = block.Float("perfectBlockAdrenaline", "adrenaline.perfectBlock")
            };
            bool any = adrenaline.max != null || adrenaline.block != null || adrenaline.perfectBlock != null;
            return any ? adrenaline : null;
        }

        private static FoodStats Food(WackyImporter ctx, WackyNode node, ImportReport.FileResult result)
        {
            WackyNode block = node.Child("FoodStats");
            FoodStats food = new FoodStats
            {
                health = block?.Float("m_foodHealth", "food.health"),
                stamina = block?.Float("m_foodStamina", "food.stamina"),
                regen = block?.Float("m_foodRegen", "food.regen"),
                duration = block?.Float("m_foodBurnTime", "food.duration"),
                eitr = block?.Float("m_FoodEitr", "food.eitr"),
                eatAnimTime = block?.Float("eatAnimationTime", "food.eatAnimTime"),
                isDrink = block?.Bool("isDrink", "food.isDrink"),
                consumeStatusEffect = ctx.Effect(node, "ConsumableStatusEffect", "food.consumeStatusEffect", result)
            };
            bool any = food.health != null || food.stamina != null || food.regen != null || food.duration != null || food.eitr != null
                || food.eatAnimTime != null || food.isDrink != null || food.consumeStatusEffect != null;
            return any ? food : null;
        }

        private static ItemStatusEffects StatusEffects(WackyImporter ctx, WackyNode node, WackyNode shield, WackyNode adrenaline, ItemDrop.ItemData.SharedData shared, ImportReport.FileResult result)
        {
            WackyNode equip = node.Child("SE_Equip");
            ItemStatusEffects effects = new ItemStatusEffects
            {
                equipped = equip != null ? ctx.Effect(equip, "EffectName", "statusEffects.equipped", result) : null,
                onHit = ctx.Effect(node, "Attack_status_effect", "statusEffects.onHit", result),
                onHitChance = node.Float("Attack_status_effect_chance", "statusEffects.onHitChance"),
                perfectBlock = shield != null ? ctx.Effect(shield, "m_perfectBlockStatusEffect", "statusEffects.perfectBlock", result) : null
            };
            // WackysDatabase only replaces this effect on an item that already has one
            if (adrenaline != null && adrenaline.Has("fullAdrenalineSE"))
            {
                if (shared != null && shared.m_fullAdrenalineSE == null)
                {
                    adrenaline.Drop("fullAdrenalineSE", "WackysDatabase only replaces this effect on an item that already has one, and this item has none");
                }
                else
                {
                    effects.fullAdrenaline = ctx.Effect(adrenaline, "fullAdrenalineSE", "statusEffects.fullAdrenaline", result);
                }
            }
            bool any = effects.equipped != null || effects.onHit != null || effects.onHitChance != null || effects.perfectBlock != null || effects.fullAdrenaline != null;
            return any ? effects : null;
        }

        private static SetBonus SetBonus(WackyImporter ctx, WackyNode node, ImportReport.FileResult result)
        {
            WackyNode block = node.Child("SE_SET_Equip");
            if (block == null) { return null; }
            // WackysDatabase clears the whole set when its effect is deleted
            if (WackyImporter.IsDelete(block.Str("EffectName")))
            {
                block.Use("SetName");
                block.Use("Size");
                block.Converted.Add($"{block.Where("EffectName")}: delete -> setBonus cleared");
                return new SetBonus { name = "none", statusEffect = "None" };
            }
            SetBonus set = new SetBonus
            {
                name = block.Str("SetName", "setBonus.name"),
                size = block.Int("Size", "setBonus.size"),
                statusEffect = ctx.Effect(block, "EffectName", "setBonus.statusEffect", result)
            };
            bool any = set.name != null || set.size != null || set.statusEffect != null;
            return any ? set : null;
        }

        /// <summary>"Type:Modifier" entries, which replace the item's whole list. A first entry of '-' or 'delete' clears it.</summary>
        private static List<DamageResistance> Resistances(WackyNode node, ImportReport.FileResult result)
        {
            List<string> entries = node.StrList("damageModifiers");
            if (entries == null) { return null; }
            // WackysDatabase leaves the item's resistances alone for an empty list, only '-' or 'delete' clears them
            if (entries.Count == 0)
            {
                node.Converted.Add("damageModifiers: [], nothing to do, the item keeps its resistances");
                return null;
            }
            if (WackyImporter.IsDelete(entries[0]))
            {
                node.Converted.Add("damageModifiers: delete -> resistances: [] (none)");
                return new List<DamageResistance>();
            }
            List<DamageResistance> resistances = new List<DamageResistance>();
            for (int i = 0; i < entries.Count; i++)
            {
                string entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry)) { continue; }
                string[] parts = entry.Split(':');
                string where = $"damageModifiers[{i}]";
                if (parts.Length < 2)
                {
                    result.Warnings.Add($"{where}: '{entry}' is not Type:Modifier, it was left out");
                    continue;
                }
                string type = parts[0].Trim();
                if (string.Equals(type, "Water", StringComparison.OrdinalIgnoreCase))
                {
                    result.Dropped.Add($"{where} ({entry}) - the Water damage type is WackysDatabase's own and {WackyImporter.NeedsPatch}");
                    continue;
                }
                if (!Enum.TryParse(type, true, out HitData.DamageType damageType) || !Enum.TryParse(parts[1].Trim(), true, out HitData.DamageModifier modifier))
                {
                    result.Warnings.Add($"{where}: '{entry}' is not a damage type and modifier the game has, it was left out");
                    continue;
                }
                if (!Common.ItemAttributes.ResistableTypes.Contains(damageType))
                {
                    result.Warnings.Add($"{where}: the game has no resistance to {damageType} damage, it was left out");
                    continue;
                }
                resistances.Add(new DamageResistance { type = damageType, modifier = modifier });
            }
            node.Converted.Add("damageModifiers -> resistances");
            return resistances;
        }
    }
}

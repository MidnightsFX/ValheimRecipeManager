using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager.Common
{
    /// <summary>
    /// Reads and writes the stat block of an item.
    ///
    /// Everything here works on ItemDrop.ItemData.SharedData, which every instance of an item holds a
    /// reference to rather than a copy of. Writing to the prefab's shared data therefore also changes
    /// the copies already sitting in chests and inventories, which is what makes modifying an item that
    /// already exists possible at all.
    ///
    /// Because of that reach, the original values of any item this mod touches are snapshotted the
    /// first time it is written to. Applying a definition always restores the snapshot first, so the
    /// configuration stays declarative: removing a field from the yaml puts the vanilla value back
    /// instead of leaving behind whatever was applied last.
    /// </summary>
    internal static class ItemAttributes
    {
        /// <summary>Pristine values, keyed by prefab name, captured before this mod wrote anything.</summary>
        private static readonly Dictionary<string, Snapshot> Originals = new Dictionary<string, Snapshot>();

        /// <summary>Value that clears a status effect or prefab slot instead of naming one.</summary>
        private const string ClearToken = "none";

        /// <summary>The damage types HitData.DamageModifiers.Apply actually understands.</summary>
        internal static readonly HashSet<HitData.DamageType> ResistableTypes = new HashSet<HitData.DamageType>
        {
            HitData.DamageType.Blunt, HitData.DamageType.Slash, HitData.DamageType.Pierce,
            HitData.DamageType.Chop, HitData.DamageType.Pickaxe, HitData.DamageType.Fire,
            HitData.DamageType.Frost, HitData.DamageType.Lightning, HitData.DamageType.Poison,
            HitData.DamageType.Spirit, HitData.DamageType.NonPlayer
        };

        /// <summary>
        /// Records the untouched values for a prefab. Only the first call for a given prefab does
        /// anything, so this is safe to put in front of every apply.
        /// </summary>
        public static void CaptureOriginal(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            if (string.IsNullOrEmpty(prefabName) || shared == null) { return; }
            if (Originals.ContainsKey(prefabName)) { return; }
            Originals.Add(prefabName, Snapshot.From(shared));
        }

        /// <summary>Puts a prefab back to the values it had before this mod first wrote to it.</summary>
        public static bool RestoreOriginal(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            Snapshot original;
            if (shared == null || !Originals.TryGetValue(prefabName, out original)) { return false; }
            original.WriteTo(shared);
            return true;
        }

        public static bool HasOriginal(string prefabName)
        {
            return Originals.ContainsKey(prefabName);
        }

        /// <summary>
        /// Writes every stat the configuration sets onto the item. Fields that are not set are left
        /// alone, which after a RestoreOriginal means they are back at their original values.
        /// </summary>
        /// <param name="warnOnMissingReference">
        /// False while items are still being created, because status effects and other mods' prefabs may
        /// not exist that early and a missing one is expected rather than a mistake.
        /// </param>
        /// <returns>False when a named status effect or prefab could not be resolved and needs a second pass.</returns>
        public static bool Apply(ItemDrop.ItemData.SharedData shared, ItemAddition def, bool warnOnMissingReference)
        {
            if (shared == null || def == null) { return true; }

            ApplyGeneral(shared, def);
            ApplyQuality(shared, def);
            ApplyDurability(shared, def);
            ApplyFood(shared, def);
            ApplyWeapon(shared, def);
            ApplyArmor(shared, def);
            ApplyStaminaModifiers(shared, def);
            ApplyAdrenaline(shared, def);
            ApplyResistances(shared, def);
            ApplySetBonus(shared, def);
            return ApplyReferences(shared, def, warnOnMissingReference);
        }

        private static void ApplyGeneral(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            if (def.weight.HasValue) { shared.m_weight = def.weight.Value; }
            if (def.maxStackSize.HasValue) { shared.m_maxStackSize = def.maxStackSize.Value; }
            if (def.value.HasValue) { shared.m_value = def.value.Value; }
            if (def.teleportable.HasValue) { shared.m_teleportable = def.teleportable.Value; }
            if (def.equipDuration.HasValue) { shared.m_equipDuration = def.equipDuration.Value; }
            if (def.questItem.HasValue) { shared.m_questItem = def.questItem.Value; }
        }

        private static void ApplyQuality(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            ItemQuality quality = def.quality;
            if (quality == null) { return; }

            if (quality.maxLevel.HasValue)
            {
                if (quality.maxLevel.Value < 1)
                {
                    Logger.LogWarning($"{def.prefab} has a quality maxLevel of {quality.maxLevel.Value}, it has to be at least 1 and will be ignored.");
                }
                else { shared.m_maxQuality = quality.maxLevel.Value; }
            }
            if (quality.weightPerLevel.HasValue) { shared.m_scaleWeightByQuality = quality.weightPerLevel.Value; }
            if (quality.upgradeChance.HasValue) { shared.m_upgradeChance = quality.upgradeChance.Value; }
            if (quality.breakChance.HasValue) { shared.m_breakChance = quality.breakChance.Value; }
            if (quality.upgradeSteps.HasValue) { shared.m_successUpgradeSteps = quality.upgradeSteps.Value; }
            if (quality.breakRefund.HasValue) { shared.m_breakReturnIngreientsAmount = quality.breakRefund.Value; }
        }

        private static void ApplyDurability(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            ItemDurability durability = def.durability;
            if (durability == null) { return; }

            if (durability.enabled.HasValue) { shared.m_useDurability = durability.enabled.Value; }
            if (durability.max.HasValue) { shared.m_maxDurability = durability.max.Value; }
            if (durability.perLevel.HasValue) { shared.m_durabilityPerLevel = durability.perLevel.Value; }
            if (durability.drainPerUse.HasValue) { shared.m_useDurabilityDrain = durability.drainPerUse.Value; }
            if (durability.drainPerSecond.HasValue) { shared.m_durabilityDrain = durability.drainPerSecond.Value; }
            if (durability.repairable.HasValue) { shared.m_canBeReparied = durability.repairable.Value; }
            if (durability.destroyWhenBroken.HasValue) { shared.m_destroyBroken = durability.destroyWhenBroken.Value; }
        }

        private static void ApplyFood(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            FoodStats food = def.food;
            if (food == null) { return; }

            if (food.health.HasValue) { shared.m_food = food.health.Value; }
            if (food.stamina.HasValue) { shared.m_foodStamina = food.stamina.Value; }
            if (food.eitr.HasValue) { shared.m_foodEitr = food.eitr.Value; }
            if (food.duration.HasValue) { shared.m_foodBurnTime = food.duration.Value; }
            if (food.regen.HasValue) { shared.m_foodRegen = food.regen.Value; }
            if (food.eatAnimTime.HasValue) { shared.m_foodEatAnimTime = food.eatAnimTime.Value; }
            if (food.isDrink.HasValue) { shared.m_isDrink = food.isDrink.Value; }
        }

        private static void ApplyWeapon(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            WeaponStats weapon = def.weapon;
            if (weapon == null) { return; }

            if (weapon.damage != null)
            {
                HitData.DamageTypes damages = shared.m_damages;
                WriteDamage(ref damages, weapon.damage);
                shared.m_damages = damages;
            }
            if (weapon.damagePerLevel != null)
            {
                HitData.DamageTypes perLevel = shared.m_damagesPerLevel;
                WriteDamage(ref perLevel, weapon.damagePerLevel);
                shared.m_damagesPerLevel = perLevel;
            }

            if (weapon.attackForce.HasValue) { shared.m_attackForce = weapon.attackForce.Value; }
            if (weapon.backstabBonus.HasValue) { shared.m_backstabBonus = weapon.backstabBonus.Value; }
            if (weapon.toolTier.HasValue) { shared.m_toolTier = weapon.toolTier.Value; }
            if (weapon.dodgeable.HasValue) { shared.m_dodgeable = weapon.dodgeable.Value; }
            if (weapon.blockable.HasValue) { shared.m_blockable = weapon.blockable.Value; }
            if (weapon.ammoType != null) { shared.m_ammoType = weapon.ammoType; }
            if (!string.IsNullOrEmpty(weapon.skill))
            {
                Skills.SkillType? skill = ResolveSkill(weapon.skill, def.prefab);
                if (skill.HasValue) { shared.m_skillType = skill.Value; }
            }

            // Every swing is a copy of these, so a change reaches the next swing. The attacks' own values are only
            // recorded the first time this mod writes to them, and only those it can write are put back, so an
            // item whose attacks were never configured is left to whatever else changes them.
            if ((weapon.primaryAttack != null || weapon.secondaryAttack != null) && Originals.TryGetValue(def.prefab, out Snapshot original))
            {
                original.CaptureAttacks(shared);
            }
            ApplyAttack(shared.m_attack, weapon.primaryAttack, def.prefab, "primaryAttack");
            ApplyAttack(shared.m_secondaryAttack, weapon.secondaryAttack, def.prefab, "secondaryAttack");
        }

        /// <summary>DamageTypes is a struct, so this takes it by reference and the caller writes it back.</summary>
        internal static void WriteDamage(ref HitData.DamageTypes target, DamageValues values)
        {
            if (values.damage.HasValue) { target.m_damage = values.damage.Value; }
            if (values.blunt.HasValue) { target.m_blunt = values.blunt.Value; }
            if (values.slash.HasValue) { target.m_slash = values.slash.Value; }
            if (values.pierce.HasValue) { target.m_pierce = values.pierce.Value; }
            if (values.chop.HasValue) { target.m_chop = values.chop.Value; }
            if (values.pickaxe.HasValue) { target.m_pickaxe = values.pickaxe.Value; }
            if (values.fire.HasValue) { target.m_fire = values.fire.Value; }
            if (values.frost.HasValue) { target.m_frost = values.frost.Value; }
            if (values.lightning.HasValue) { target.m_lightning = values.lightning.Value; }
            if (values.poison.HasValue) { target.m_poison = values.poison.Value; }
            if (values.spirit.HasValue) { target.m_spirit = values.spirit.Value; }
            if (values.nonPlayer.HasValue) { target.m_nonPlayer = values.nonPlayer.Value; }
        }

        /// <summary>The numbers and switches of an attack. Its prefabs are set with the other references.</summary>
        private static void ApplyAttack(Attack attack, AttackStats stats, string prefabName, string slot)
        {
            if (stats == null) { return; }
            if (attack == null)
            {
                Logger.LogWarning($"{prefabName} has no {slot} to change, those settings will be ignored.");
                return;
            }

            if (stats.stamina.HasValue) { attack.m_attackStamina = stats.stamina.Value; }
            if (stats.eitr.HasValue) { attack.m_attackEitr = stats.eitr.Value; }
            if (stats.health.HasValue) { attack.m_attackHealth = stats.health.Value; }
            if (stats.healthPercentage.HasValue) { attack.m_attackHealthPercentage = stats.healthPercentage.Value; }
            if (stats.adrenaline.HasValue) { attack.m_attackAdrenaline = stats.adrenaline.Value; }
            if (stats.useAdrenaline.HasValue) { attack.m_attackUseAdrenaline = stats.useAdrenaline.Value; }
            if (stats.speedFactor.HasValue) { attack.m_speedFactor = stats.speedFactor.Value; }
            if (stats.rotationSpeedFactor.HasValue) { attack.m_speedFactorRotation = stats.rotationSpeedFactor.Value; }
            if (stats.damageMultiplier.HasValue) { attack.m_damageMultiplier = stats.damageMultiplier.Value; }
            if (stats.forceMultiplier.HasValue) { attack.m_forceMultiplier = stats.forceMultiplier.Value; }
            if (stats.staggerMultiplier.HasValue) { attack.m_staggerMultiplier = stats.staggerMultiplier.Value; }
            if (stats.lastChainDamageMultiplier.HasValue) { attack.m_lastChainDamageMultiplier = stats.lastChainDamageMultiplier.Value; }
            if (stats.damageMultiplierPerMissingHealth.HasValue) { attack.m_damageMultiplierPerMissingHP = stats.damageMultiplierPerMissingHealth.Value; }
            if (stats.damageMultiplierByHealthMissing.HasValue) { attack.m_damageMultiplierByTotalHealthMissing = stats.damageMultiplierByHealthMissing.Value; }
            if (stats.staminaReturnPerMissingHealth.HasValue) { attack.m_staminaReturnPerMissingHP = stats.staminaReturnPerMissingHealth.Value; }
            if (stats.range.HasValue) { attack.m_attackRange = stats.range.Value; }
            if (stats.height.HasValue) { attack.m_attackHeight = stats.height.Value; }
            if (stats.angle.HasValue) { attack.m_attackAngle = stats.angle.Value; }
            if (stats.rayWidth.HasValue) { attack.m_attackRayWidth = stats.rayWidth.Value; }
            if (stats.multiHit.HasValue) { attack.m_multiHit = stats.multiHit.Value; }
            if (stats.lowerDamagePerHit.HasValue) { attack.m_lowerDamagePerHit = stats.lowerDamagePerHit.Value; }
            if (stats.hitTerrain.HasValue) { attack.m_hitTerrain = stats.hitTerrain.Value; }
            if (stats.hitFriendly.HasValue) { attack.m_hitFriendly = stats.hitFriendly.Value; }
            if (stats.hitThroughWalls.HasValue) { attack.m_hitThroughWalls = stats.hitThroughWalls.Value; }
            if (stats.recoilPushback.HasValue) { attack.m_recoilPushback = stats.recoilPushback.Value; }
            if (stats.selfDamage.HasValue) { attack.m_selfDamage = stats.selfDamage.Value; }
            if (stats.consumeItem.HasValue) { attack.m_consumeItem = stats.consumeItem.Value; }
            if (stats.raiseSkillAmount.HasValue) { attack.m_raiseSkillAmount = stats.raiseSkillAmount.Value; }
            if (stats.startNoise.HasValue) { attack.m_attackStartNoise = stats.startNoise.Value; }
            if (stats.hitNoise.HasValue) { attack.m_attackHitNoise = stats.hitNoise.Value; }
            if (stats.spawnOnHitChance.HasValue) { attack.m_spawnOnHitChance = stats.spawnOnHitChance.Value; }

            ProjectileStats projectile = stats.projectile;
            if (projectile != null)
            {
                if (projectile.velocity.HasValue) { attack.m_projectileVel = projectile.velocity.Value; }
                if (projectile.velocityMin.HasValue) { attack.m_projectileVelMin = projectile.velocityMin.Value; }
                if (projectile.accuracy.HasValue) { attack.m_projectileAccuracy = projectile.accuracy.Value; }
                if (projectile.accuracyMin.HasValue) { attack.m_projectileAccuracyMin = projectile.accuracyMin.Value; }
                if (projectile.skillAccuracy.HasValue) { attack.m_skillAccuracy = projectile.skillAccuracy.Value; }
                if (projectile.count.HasValue) { attack.m_projectiles = projectile.count.Value; }
                if (projectile.bursts.HasValue) { attack.m_projectileBursts = projectile.bursts.Value; }
                if (projectile.burstInterval.HasValue) { attack.m_burstInterval = projectile.burstInterval.Value; }
                if (projectile.launchAngle.HasValue) { attack.m_launchAngle = projectile.launchAngle.Value; }
            }

            ReloadStats reload = stats.reload;
            if (reload != null)
            {
                if (reload.time.HasValue) { attack.m_reloadTime = reload.time.Value; }
                if (reload.staminaDrain.HasValue) { attack.m_reloadStaminaDrain = reload.staminaDrain.Value; }
                if (reload.eitrDrain.HasValue) { attack.m_reloadEitrDrain = reload.eitrDrain.Value; }
            }

            DrawStats draw = stats.draw;
            if (draw != null)
            {
                if (draw.durationMin.HasValue) { attack.m_drawDurationMin = draw.durationMin.Value; }
                if (draw.staminaDrain.HasValue) { attack.m_drawStaminaDrain = draw.staminaDrain.Value; }
                if (draw.eitrDrain.HasValue) { attack.m_drawEitrDrain = draw.eitrDrain.Value; }
            }
        }

        /// <summary>
        /// A skill by name: one of the game's, one a mod registered through Jotunn, or the number of one a mod
        /// added some other way. Null and a warning when it matches none of them.
        /// </summary>
        private static Skills.SkillType? ResolveSkill(string configured, string prefabName)
        {
            if (TryResolveSkill(configured, out Skills.SkillType skill)) { return skill; }
            Logger.LogWarning($"{prefabName} uses the skill {configured}, which does not exist. The item keeps its own skill.");
            return null;
        }

        /// <summary>A skill by name: one of the game's, one a mod registered through Jotunn, or the number of one a mod added some other way.</summary>
        internal static bool TryResolveSkill(string configured, out Skills.SkillType skill)
        {
            string name = configured.Trim();
            if (Enum.TryParse(name, true, out skill)) { return true; }
            Skills.SkillDef custom = SkillManager.Instance.GetSkill(name);
            if (custom != null)
            {
                skill = custom.m_skill;
                return true;
            }
            return false;
        }

        private static void ApplyArmor(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            ArmorStats armor = def.armor;
            if (armor == null) { return; }

            if (armor.armor.HasValue) { shared.m_armor = armor.armor.Value; }
            if (armor.armorPerLevel.HasValue) { shared.m_armorPerLevel = armor.armorPerLevel.Value; }
            if (armor.blockPower.HasValue) { shared.m_blockPower = armor.blockPower.Value; }
            if (armor.blockPowerPerLevel.HasValue) { shared.m_blockPowerPerLevel = armor.blockPowerPerLevel.Value; }
            if (armor.deflectionForce.HasValue) { shared.m_deflectionForce = armor.deflectionForce.Value; }
            if (armor.deflectionForcePerLevel.HasValue) { shared.m_deflectionForcePerLevel = armor.deflectionForcePerLevel.Value; }
            if (armor.movementModifier.HasValue) { shared.m_movementModifier = armor.movementModifier.Value; }
            if (armor.eitrRegenModifier.HasValue) { shared.m_eitrRegenModifier = armor.eitrRegenModifier.Value; }
            if (armor.heatResistanceModifier.HasValue) { shared.m_heatResistanceModifier = armor.heatResistanceModifier.Value; }
            if (armor.timedBlockBonus.HasValue) { shared.m_timedBlockBonus = armor.timedBlockBonus.Value; }
            if (armor.perfectBlockStaminaRegen.HasValue) { shared.m_perfectBlockStaminaRegen = armor.perfectBlockStaminaRegen.Value; }
            if (armor.buildBlockCharges.HasValue) { shared.m_buildBlockCharges = armor.buildBlockCharges.Value; }
            if (armor.maxBlockCharges.HasValue) { shared.m_maxBlockCharges = armor.maxBlockCharges.Value; }
            if (armor.blockChargeDecayTime.HasValue) { shared.m_blockChargeDecayTime = armor.blockChargeDecayTime.Value; }
        }

        private static void ApplyStaminaModifiers(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            StaminaModifiers stamina = def.staminaModifiers;
            if (stamina == null) { return; }

            if (stamina.homeItems.HasValue) { shared.m_homeItemsStaminaModifier = stamina.homeItems.Value; }
            if (stamina.jump.HasValue) { shared.m_jumpStaminaModifier = stamina.jump.Value; }
            if (stamina.attack.HasValue) { shared.m_attackStaminaModifier = stamina.attack.Value; }
            if (stamina.block.HasValue) { shared.m_blockStaminaModifier = stamina.block.Value; }
            if (stamina.dodge.HasValue) { shared.m_dodgeStaminaModifier = stamina.dodge.Value; }
            if (stamina.swim.HasValue) { shared.m_swimStaminaModifier = stamina.swim.Value; }
            if (stamina.sneak.HasValue) { shared.m_sneakStaminaModifier = stamina.sneak.Value; }
            if (stamina.run.HasValue) { shared.m_runStaminaModifier = stamina.run.Value; }
        }

        private static void ApplyAdrenaline(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            AdrenalineStats adrenaline = def.adrenaline;
            if (adrenaline == null) { return; }

            if (adrenaline.max.HasValue) { shared.m_maxAdrenaline = adrenaline.max.Value; }
            if (adrenaline.block.HasValue) { shared.m_blockAdrenaline = adrenaline.block.Value; }
            if (adrenaline.perfectBlock.HasValue) { shared.m_perfectBlockAdrenaline = adrenaline.perfectBlock.Value; }
        }

        /// <summary>
        /// Resistances are replaced wholesale rather than merged, so the yaml always reads as the
        /// complete list of what the item resists. An empty list clears them.
        /// </summary>
        private static void ApplyResistances(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            if (def.resistances == null) { return; }
            shared.m_damageModifiers = BuildResistances(def.resistances, def.prefab);
        }

        /// <summary>The game's resistance list for a configured one, leaving out the types the game has no resistance to.</summary>
        internal static List<HitData.DamageModPair> BuildResistances(List<DamageResistance> resistances, string owner)
        {
            List<HitData.DamageModPair> modifiers = new List<HitData.DamageModPair>();
            foreach (DamageResistance resistance in resistances)
            {
                if (resistance == null) { continue; }
                if (!ResistableTypes.Contains(resistance.type))
                {
                    Logger.LogWarning($"'{resistance.type}' is not a resistance type that {owner} can use, it will be ignored. Use one of Blunt, Slash, Pierce, Chop, Pickaxe, Fire, Frost, Lightning, Poison, Spirit or NonPlayer.");
                    continue;
                }
                modifiers.Add(new HitData.DamageModPair { m_type = resistance.type, m_modifier = resistance.modifier });
            }
            return modifiers;
        }

        private static void ApplySetBonus(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            SetBonus set = def.setBonus;
            if (set == null) { return; }

            if (set.name != null)
            {
                shared.m_setName = string.Equals(set.name, ClearToken, StringComparison.OrdinalIgnoreCase) ? string.Empty : set.name;
            }
            if (set.size.HasValue) { shared.m_setSize = set.size.Value; }
        }

        /// <summary>
        /// Resolves the status effect slots and the prefabs an item points at. Split out from the rest because
        /// status effects only exist once the object DB is built, and another mod's prefabs can arrive late, which
        /// is both later than items are created.
        /// </summary>
        /// <returns>False when something named could not be found and is worth retrying later.</returns>
        public static bool ApplyReferences(ItemDrop.ItemData.SharedData shared, ItemAddition def, bool warnOnMissing)
        {
            if (shared == null || def == null) { return true; }
            bool resolvedEverything = true;
            StatusEffect effect;
            GameObject prefab;
            bool assign;

            if (def.food != null)
            {
                if (TryFindEffect(def.food.consumeStatusEffect, def.prefab, "consume", warnOnMissing, out effect, out assign))
                {
                    if (assign) { shared.m_consumeStatusEffect = effect; }
                }
                else { resolvedEverything = false; }
            }

            ItemStatusEffects effects = def.statusEffects;
            if (effects != null)
            {
                if (TryFindEffect(effects.equipped, def.prefab, "equipped", warnOnMissing, out effect, out assign))
                {
                    if (assign) { shared.m_equipStatusEffect = effect; }
                }
                else { resolvedEverything = false; }

                if (TryFindEffect(effects.onHit, def.prefab, "onHit", warnOnMissing, out effect, out assign))
                {
                    if (assign) { shared.m_attackStatusEffect = effect; }
                }
                else { resolvedEverything = false; }

                if (effects.onHitChance.HasValue) { shared.m_attackStatusEffectChance = effects.onHitChance.Value; }

                if (TryFindEffect(effects.perfectBlock, def.prefab, "perfectBlock", warnOnMissing, out effect, out assign))
                {
                    if (assign) { shared.m_perfectBlockStatusEffect = effect; }
                }
                else { resolvedEverything = false; }

                if (TryFindEffect(effects.fullAdrenaline, def.prefab, "fullAdrenaline", warnOnMissing, out effect, out assign))
                {
                    if (assign) { shared.m_fullAdrenalineSE = effect; }
                }
                else { resolvedEverything = false; }
            }

            if (def.setBonus != null)
            {
                if (TryFindEffect(def.setBonus.statusEffect, def.prefab, "setBonus", warnOnMissing, out effect, out assign))
                {
                    if (assign) { shared.m_setStatusEffect = effect; }
                }
                else { resolvedEverything = false; }
            }

            if (TryFindPrefab(def.appendToolTip, def.prefab, "appendToolTip", warnOnMissing, MustBeItem, out prefab, out assign))
            {
                if (assign) { shared.m_appendToolTip = prefab != null ? prefab.GetComponent<ItemDrop>() : null; }
            }
            else { resolvedEverything = false; }

            WeaponStats weapon = def.weapon;
            if (weapon != null)
            {
                if (TryFindPrefab(weapon.spawnOnHit, def.prefab, "weapon spawnOnHit", warnOnMissing, MustNotBeItem, out prefab, out assign))
                {
                    if (assign) { shared.m_spawnOnHit = prefab; }
                }
                else { resolvedEverything = false; }

                if (TryFindPrefab(weapon.spawnOnHitTerrain, def.prefab, "weapon spawnOnHitTerrain", warnOnMissing, MustNotBeItem, out prefab, out assign))
                {
                    if (assign) { shared.m_spawnOnHitTerrain = prefab; }
                }
                else { resolvedEverything = false; }

                resolvedEverything &= ApplyAttackReferences(shared.m_attack, weapon.primaryAttack, def.prefab, "primaryAttack", warnOnMissing);
                resolvedEverything &= ApplyAttackReferences(shared.m_secondaryAttack, weapon.secondaryAttack, def.prefab, "secondaryAttack", warnOnMissing);
            }

            return resolvedEverything;
        }

        private static bool ApplyAttackReferences(Attack attack, AttackStats stats, string prefabName, string slot, bool warnOnMissing)
        {
            if (attack == null || stats == null) { return true; }
            bool resolvedEverything = true;
            GameObject prefab;
            bool assign;

            if (TryFindPrefab(stats.spawnOnTrigger, prefabName, $"{slot} spawnOnTrigger", warnOnMissing, MustNotBeItem, out prefab, out assign))
            {
                if (assign) { attack.m_spawnOnTrigger = prefab; }
            }
            else { resolvedEverything = false; }

            if (TryFindPrefab(stats.spawnOnHit, prefabName, $"{slot} spawnOnHit", warnOnMissing, MustNotBeItem, out prefab, out assign))
            {
                if (assign)
                {
                    attack.m_spawnOnHit = prefab;
                    // The game only spawns it with a chance above 0, and leaves the chance at 0 by default
                    if (prefab != null && attack.m_spawnOnHitChance <= 0f && warnOnMissing)
                    {
                        Logger.LogWarning($"{prefabName} sets a {slot} spawnOnHit but its spawnOnHitChance is 0, so it is never spawned. Set spawnOnHitChance as well, 1 is every hit.");
                    }
                }
            }
            else { resolvedEverything = false; }

            if (stats.projectile != null)
            {
                if (TryFindPrefab(stats.projectile.prefab, prefabName, $"{slot} projectile", warnOnMissing, MustBeProjectile, out prefab, out assign))
                {
                    if (assign) { attack.m_attackProjectile = prefab; }
                }
                else { resolvedEverything = false; }
            }
            return resolvedEverything;
        }

        // What each kind of prefab slot accepts. An item in a slot the game instantiates would hand out free copies of it.
        private static string MustBeItem(GameObject prefab)
        {
            return prefab.GetComponent<ItemDrop>() != null ? null : "is not an item";
        }

        private static string MustNotBeItem(GameObject prefab)
        {
            return prefab.GetComponent<ItemDrop>() == null ? null : "is an item, which would be spawned as a free copy";
        }

        private static string MustBeProjectile(GameObject prefab)
        {
            return prefab.GetComponent<IProjectile>() != null ? null : "is not a projectile";
        }

        /// <summary>
        /// Looks up a status effect by name. An unset value leaves the slot alone, the literal 'None'
        /// empties it, and anything else has to match a status effect that already exists.
        /// </summary>
        /// <param name="assign">True when the caller should write the resolved effect to the slot.</param>
        /// <returns>False only when a named effect could not be found.</returns>
        private static bool TryFindEffect(string configured, string prefabName, string slot, bool warnOnMissing, out StatusEffect effect, out bool assign)
        {
            effect = null;
            assign = false;
            if (string.IsNullOrEmpty(configured)) { return true; }
            if (string.Equals(configured, ClearToken, StringComparison.OrdinalIgnoreCase))
            {
                assign = true;
                return true;
            }

            if (ObjectDB.instance == null || ObjectDB.instance.m_StatusEffects == null)
            {
                if (warnOnMissing) { Logger.LogWarning($"Cannot resolve the {slot} status effect '{configured}' for {prefabName}, the object DB is not available."); }
                return false;
            }

            StatusEffect found = ObjectDB.instance.m_StatusEffects.Find(se => se != null && se.name == configured);
            if (found == null)
            {
                if (warnOnMissing)
                {
                    Logger.LogWarning($"The {slot} status effect '{configured}' referenced by {prefabName} could not be found. Names are case sensitive, RM_StatusEffects_PrintAll lists the ones that exist and StatusEffects.yaml can add new ones.");
                }
                return false;
            }

            effect = found;
            assign = true;
            return true;
        }

        /// <summary>
        /// Looks up a prefab by name, the same way as a status effect: unset leaves the slot alone, 'None' empties
        /// it, and anything else has to be a prefab that exists. One that exists but does not fit the slot is
        /// reported and the slot is left alone.
        /// </summary>
        /// <param name="unfit">Why a prefab does not fit the slot, or null when it does.</param>
        /// <returns>False only when a named prefab could not be found.</returns>
        private static bool TryFindPrefab(string configured, string prefabName, string slot, bool warnOnMissing, Func<GameObject, string> unfit, out GameObject prefab, out bool assign)
        {
            prefab = null;
            assign = false;
            if (string.IsNullOrEmpty(configured)) { return true; }
            if (string.Equals(configured, ClearToken, StringComparison.OrdinalIgnoreCase))
            {
                assign = true;
                return true;
            }

            GameObject found = PrefabManager.Instance.GetPrefab(configured);
            if (found == null)
            {
                if (warnOnMissing)
                {
                    Logger.LogWarning($"The {slot} prefab '{configured}' referenced by {prefabName} could not be found. Prefab names are case sensitive.");
                }
                return false;
            }
            string problem = unfit(found);
            if (problem != null)
            {
                Logger.LogWarning($"The {slot} prefab '{configured}' referenced by {prefabName} {problem}, so it is left as it is.");
                return true;
            }

            prefab = found;
            assign = true;
            return true;
        }

        /// <summary>
        /// Every value this mod is able to write, as it was before it wrote anything. Reference types
        /// are copied rather than aliased so a later edit cannot reach back into the snapshot.
        /// </summary>
        private class Snapshot
        {
            private string name;
            private string description;
            private float weight;
            private int maxStackSize;
            private int value;
            private bool teleportable;
            private float equipDuration;
            private bool questItem;
            private ItemDrop appendToolTip;

            private int maxQuality;
            private float scaleWeightByQuality;
            private float upgradeChance;
            private float breakChance;
            private int successUpgradeSteps;
            private float breakReturnIngredientsAmount;

            private bool useDurability;
            private float maxDurability;
            private float durabilityPerLevel;
            private float useDurabilityDrain;
            private float durabilityDrain;
            private bool canBeRepaired;
            private bool destroyBroken;

            private float food;
            private float foodStamina;
            private float foodEitr;
            private float foodBurnTime;
            private float foodRegen;
            private float foodEatAnimTime;
            private bool isDrink;

            private HitData.DamageTypes damages;
            private HitData.DamageTypes damagesPerLevel;
            private float attackForce;
            private float backstabBonus;
            private int toolTier;
            private Skills.SkillType skillType;
            private string ammoType;
            private bool dodgeable;
            private bool blockable;
            private GameObject spawnOnHit;
            private GameObject spawnOnHitTerrain;
            // Null until this mod first writes to the item's attacks, see CaptureAttacks
            private AttackValues attack;
            private AttackValues secondaryAttack;

            private float armor;
            private float armorPerLevel;
            private float blockPower;
            private float blockPowerPerLevel;
            private float deflectionForce;
            private float deflectionForcePerLevel;
            private float movementModifier;
            private float eitrRegenModifier;
            private float heatResistanceModifier;
            private float timedBlockBonus;
            private float perfectBlockStaminaRegen;
            private bool buildBlockCharges;
            private int maxBlockCharges;
            private float blockChargeDecayTime;
            private List<HitData.DamageModPair> damageModifiers;

            private float homeItemsStaminaModifier;
            private float jumpStaminaModifier;
            private float attackStaminaModifier;
            private float blockStaminaModifier;
            private float dodgeStaminaModifier;
            private float swimStaminaModifier;
            private float sneakStaminaModifier;
            private float runStaminaModifier;

            private float maxAdrenaline;
            private float blockAdrenaline;
            private float perfectBlockAdrenaline;

            private StatusEffect consumeStatusEffect;
            private StatusEffect equipStatusEffect;
            private StatusEffect attackStatusEffect;
            private float attackStatusEffectChance;
            private StatusEffect perfectBlockStatusEffect;
            private StatusEffect fullAdrenalineStatusEffect;

            private string setName;
            private int setSize;
            private StatusEffect setStatusEffect;

            public static Snapshot From(ItemDrop.ItemData.SharedData shared)
            {
                return new Snapshot
                {
                    name = shared.m_name,
                    description = shared.m_description,
                    weight = shared.m_weight,
                    maxStackSize = shared.m_maxStackSize,
                    value = shared.m_value,
                    teleportable = shared.m_teleportable,
                    equipDuration = shared.m_equipDuration,
                    questItem = shared.m_questItem,
                    appendToolTip = shared.m_appendToolTip,

                    maxQuality = shared.m_maxQuality,
                    scaleWeightByQuality = shared.m_scaleWeightByQuality,
                    upgradeChance = shared.m_upgradeChance,
                    breakChance = shared.m_breakChance,
                    successUpgradeSteps = shared.m_successUpgradeSteps,
                    breakReturnIngredientsAmount = shared.m_breakReturnIngreientsAmount,

                    useDurability = shared.m_useDurability,
                    maxDurability = shared.m_maxDurability,
                    durabilityPerLevel = shared.m_durabilityPerLevel,
                    useDurabilityDrain = shared.m_useDurabilityDrain,
                    durabilityDrain = shared.m_durabilityDrain,
                    canBeRepaired = shared.m_canBeReparied,
                    destroyBroken = shared.m_destroyBroken,

                    food = shared.m_food,
                    foodStamina = shared.m_foodStamina,
                    foodEitr = shared.m_foodEitr,
                    foodBurnTime = shared.m_foodBurnTime,
                    foodRegen = shared.m_foodRegen,
                    foodEatAnimTime = shared.m_foodEatAnimTime,
                    isDrink = shared.m_isDrink,

                    damages = shared.m_damages,
                    damagesPerLevel = shared.m_damagesPerLevel,
                    attackForce = shared.m_attackForce,
                    backstabBonus = shared.m_backstabBonus,
                    toolTier = shared.m_toolTier,
                    skillType = shared.m_skillType,
                    ammoType = shared.m_ammoType,
                    dodgeable = shared.m_dodgeable,
                    blockable = shared.m_blockable,
                    spawnOnHit = shared.m_spawnOnHit,
                    spawnOnHitTerrain = shared.m_spawnOnHitTerrain,

                    armor = shared.m_armor,
                    armorPerLevel = shared.m_armorPerLevel,
                    blockPower = shared.m_blockPower,
                    blockPowerPerLevel = shared.m_blockPowerPerLevel,
                    deflectionForce = shared.m_deflectionForce,
                    deflectionForcePerLevel = shared.m_deflectionForcePerLevel,
                    movementModifier = shared.m_movementModifier,
                    eitrRegenModifier = shared.m_eitrRegenModifier,
                    heatResistanceModifier = shared.m_heatResistanceModifier,
                    timedBlockBonus = shared.m_timedBlockBonus,
                    perfectBlockStaminaRegen = shared.m_perfectBlockStaminaRegen,
                    buildBlockCharges = shared.m_buildBlockCharges,
                    maxBlockCharges = shared.m_maxBlockCharges,
                    blockChargeDecayTime = shared.m_blockChargeDecayTime,
                    damageModifiers = shared.m_damageModifiers == null
                        ? null
                        : new List<HitData.DamageModPair>(shared.m_damageModifiers),

                    homeItemsStaminaModifier = shared.m_homeItemsStaminaModifier,
                    jumpStaminaModifier = shared.m_jumpStaminaModifier,
                    attackStaminaModifier = shared.m_attackStaminaModifier,
                    blockStaminaModifier = shared.m_blockStaminaModifier,
                    dodgeStaminaModifier = shared.m_dodgeStaminaModifier,
                    swimStaminaModifier = shared.m_swimStaminaModifier,
                    sneakStaminaModifier = shared.m_sneakStaminaModifier,
                    runStaminaModifier = shared.m_runStaminaModifier,

                    maxAdrenaline = shared.m_maxAdrenaline,
                    blockAdrenaline = shared.m_blockAdrenaline,
                    perfectBlockAdrenaline = shared.m_perfectBlockAdrenaline,

                    consumeStatusEffect = shared.m_consumeStatusEffect,
                    equipStatusEffect = shared.m_equipStatusEffect,
                    attackStatusEffect = shared.m_attackStatusEffect,
                    attackStatusEffectChance = shared.m_attackStatusEffectChance,
                    perfectBlockStatusEffect = shared.m_perfectBlockStatusEffect,
                    fullAdrenalineStatusEffect = shared.m_fullAdrenalineSE,

                    setName = shared.m_setName,
                    setSize = shared.m_setSize,
                    setStatusEffect = shared.m_setStatusEffect
                };
            }

            public void WriteTo(ItemDrop.ItemData.SharedData shared)
            {
                shared.m_name = name;
                shared.m_description = description;
                shared.m_weight = weight;
                shared.m_maxStackSize = maxStackSize;
                shared.m_value = value;
                shared.m_teleportable = teleportable;
                shared.m_equipDuration = equipDuration;
                shared.m_questItem = questItem;
                shared.m_appendToolTip = appendToolTip;

                shared.m_maxQuality = maxQuality;
                shared.m_scaleWeightByQuality = scaleWeightByQuality;
                shared.m_upgradeChance = upgradeChance;
                shared.m_breakChance = breakChance;
                shared.m_successUpgradeSteps = successUpgradeSteps;
                shared.m_breakReturnIngreientsAmount = breakReturnIngredientsAmount;

                shared.m_useDurability = useDurability;
                shared.m_maxDurability = maxDurability;
                shared.m_durabilityPerLevel = durabilityPerLevel;
                shared.m_useDurabilityDrain = useDurabilityDrain;
                shared.m_durabilityDrain = durabilityDrain;
                shared.m_canBeReparied = canBeRepaired;
                shared.m_destroyBroken = destroyBroken;

                shared.m_food = food;
                shared.m_foodStamina = foodStamina;
                shared.m_foodEitr = foodEitr;
                shared.m_foodBurnTime = foodBurnTime;
                shared.m_foodRegen = foodRegen;
                shared.m_foodEatAnimTime = foodEatAnimTime;
                shared.m_isDrink = isDrink;

                shared.m_damages = damages;
                shared.m_damagesPerLevel = damagesPerLevel;
                shared.m_attackForce = attackForce;
                shared.m_backstabBonus = backstabBonus;
                shared.m_toolTier = toolTier;
                shared.m_skillType = skillType;
                shared.m_ammoType = ammoType;
                shared.m_dodgeable = dodgeable;
                shared.m_blockable = blockable;
                shared.m_spawnOnHit = spawnOnHit;
                shared.m_spawnOnHitTerrain = spawnOnHitTerrain;
                attack?.WriteTo(shared.m_attack);
                secondaryAttack?.WriteTo(shared.m_secondaryAttack);

                shared.m_armor = armor;
                shared.m_armorPerLevel = armorPerLevel;
                shared.m_blockPower = blockPower;
                shared.m_blockPowerPerLevel = blockPowerPerLevel;
                shared.m_deflectionForce = deflectionForce;
                shared.m_deflectionForcePerLevel = deflectionForcePerLevel;
                shared.m_movementModifier = movementModifier;
                shared.m_eitrRegenModifier = eitrRegenModifier;
                shared.m_heatResistanceModifier = heatResistanceModifier;
                shared.m_timedBlockBonus = timedBlockBonus;
                shared.m_perfectBlockStaminaRegen = perfectBlockStaminaRegen;
                shared.m_buildBlockCharges = buildBlockCharges;
                shared.m_maxBlockCharges = maxBlockCharges;
                shared.m_blockChargeDecayTime = blockChargeDecayTime;
                shared.m_damageModifiers = damageModifiers == null
                    ? null
                    : new List<HitData.DamageModPair>(damageModifiers);

                shared.m_homeItemsStaminaModifier = homeItemsStaminaModifier;
                shared.m_jumpStaminaModifier = jumpStaminaModifier;
                shared.m_attackStaminaModifier = attackStaminaModifier;
                shared.m_blockStaminaModifier = blockStaminaModifier;
                shared.m_dodgeStaminaModifier = dodgeStaminaModifier;
                shared.m_swimStaminaModifier = swimStaminaModifier;
                shared.m_sneakStaminaModifier = sneakStaminaModifier;
                shared.m_runStaminaModifier = runStaminaModifier;

                shared.m_maxAdrenaline = maxAdrenaline;
                shared.m_blockAdrenaline = blockAdrenaline;
                shared.m_perfectBlockAdrenaline = perfectBlockAdrenaline;

                shared.m_consumeStatusEffect = consumeStatusEffect;
                shared.m_equipStatusEffect = equipStatusEffect;
                shared.m_attackStatusEffect = attackStatusEffect;
                shared.m_attackStatusEffectChance = attackStatusEffectChance;
                shared.m_perfectBlockStatusEffect = perfectBlockStatusEffect;
                shared.m_fullAdrenalineSE = fullAdrenalineStatusEffect;

                shared.m_setName = setName;
                shared.m_setSize = setSize;
                shared.m_setStatusEffect = setStatusEffect;
            }

            /// <summary>
            /// Records the item's attacks the first time this mod is about to write to them. Taken then rather than
            /// with the rest, so changes another mod made to them in between are what gets put back.
            /// </summary>
            public void CaptureAttacks(ItemDrop.ItemData.SharedData shared)
            {
                if (attack == null && shared.m_attack != null) { attack = AttackValues.From(shared.m_attack); }
                if (secondaryAttack == null && shared.m_secondaryAttack != null) { secondaryAttack = AttackValues.From(shared.m_secondaryAttack); }
            }
        }

        /// <summary>
        /// The values of an attack this mod can write, and nothing else, so putting them back cannot undo another
        /// mod's change to the rest of the attack. Written back into the item's own attack object, which another
        /// mod may hold on to.
        /// </summary>
        private class AttackValues
        {
            private float stamina, eitr, health, healthPercentage, adrenaline, useAdrenaline;
            private float speedFactor, rotationSpeedFactor, damageMultiplier, forceMultiplier, staggerMultiplier, lastChainDamageMultiplier;
            private float damageMultiplierPerMissingHealth, damageMultiplierByHealthMissing, staminaReturnPerMissingHealth;
            private float range, height, angle, rayWidth;
            private bool multiHit, lowerDamagePerHit, hitTerrain, hitFriendly, hitThroughWalls, consumeItem, skillAccuracy;
            private float recoilPushback, raiseSkillAmount, startNoise, hitNoise, spawnOnHitChance;
            private int selfDamage, projectiles, projectileBursts;
            private GameObject spawnOnTrigger, spawnOnHit, projectile;
            private float projectileVelocity, projectileVelocityMin, projectileAccuracy, projectileAccuracyMin, burstInterval, launchAngle;
            private float reloadTime, reloadStaminaDrain, reloadEitrDrain, drawDurationMin, drawStaminaDrain, drawEitrDrain;

            public static AttackValues From(Attack a)
            {
                return new AttackValues
                {
                    stamina = a.m_attackStamina, eitr = a.m_attackEitr, health = a.m_attackHealth, healthPercentage = a.m_attackHealthPercentage,
                    adrenaline = a.m_attackAdrenaline, useAdrenaline = a.m_attackUseAdrenaline,
                    speedFactor = a.m_speedFactor, rotationSpeedFactor = a.m_speedFactorRotation, damageMultiplier = a.m_damageMultiplier,
                    forceMultiplier = a.m_forceMultiplier, staggerMultiplier = a.m_staggerMultiplier, lastChainDamageMultiplier = a.m_lastChainDamageMultiplier,
                    damageMultiplierPerMissingHealth = a.m_damageMultiplierPerMissingHP, damageMultiplierByHealthMissing = a.m_damageMultiplierByTotalHealthMissing,
                    staminaReturnPerMissingHealth = a.m_staminaReturnPerMissingHP,
                    range = a.m_attackRange, height = a.m_attackHeight, angle = a.m_attackAngle, rayWidth = a.m_attackRayWidth,
                    multiHit = a.m_multiHit, lowerDamagePerHit = a.m_lowerDamagePerHit, hitTerrain = a.m_hitTerrain, hitFriendly = a.m_hitFriendly,
                    hitThroughWalls = a.m_hitThroughWalls, consumeItem = a.m_consumeItem, skillAccuracy = a.m_skillAccuracy,
                    recoilPushback = a.m_recoilPushback, raiseSkillAmount = a.m_raiseSkillAmount, startNoise = a.m_attackStartNoise,
                    hitNoise = a.m_attackHitNoise, spawnOnHitChance = a.m_spawnOnHitChance,
                    selfDamage = a.m_selfDamage, projectiles = a.m_projectiles, projectileBursts = a.m_projectileBursts,
                    spawnOnTrigger = a.m_spawnOnTrigger, spawnOnHit = a.m_spawnOnHit, projectile = a.m_attackProjectile,
                    projectileVelocity = a.m_projectileVel, projectileVelocityMin = a.m_projectileVelMin, projectileAccuracy = a.m_projectileAccuracy,
                    projectileAccuracyMin = a.m_projectileAccuracyMin, burstInterval = a.m_burstInterval, launchAngle = a.m_launchAngle,
                    reloadTime = a.m_reloadTime, reloadStaminaDrain = a.m_reloadStaminaDrain, reloadEitrDrain = a.m_reloadEitrDrain,
                    drawDurationMin = a.m_drawDurationMin, drawStaminaDrain = a.m_drawStaminaDrain, drawEitrDrain = a.m_drawEitrDrain
                };
            }

            public void WriteTo(Attack a)
            {
                if (a == null) { return; }
                a.m_attackStamina = stamina; a.m_attackEitr = eitr; a.m_attackHealth = health; a.m_attackHealthPercentage = healthPercentage;
                a.m_attackAdrenaline = adrenaline; a.m_attackUseAdrenaline = useAdrenaline;
                a.m_speedFactor = speedFactor; a.m_speedFactorRotation = rotationSpeedFactor; a.m_damageMultiplier = damageMultiplier;
                a.m_forceMultiplier = forceMultiplier; a.m_staggerMultiplier = staggerMultiplier; a.m_lastChainDamageMultiplier = lastChainDamageMultiplier;
                a.m_damageMultiplierPerMissingHP = damageMultiplierPerMissingHealth; a.m_damageMultiplierByTotalHealthMissing = damageMultiplierByHealthMissing;
                a.m_staminaReturnPerMissingHP = staminaReturnPerMissingHealth;
                a.m_attackRange = range; a.m_attackHeight = height; a.m_attackAngle = angle; a.m_attackRayWidth = rayWidth;
                a.m_multiHit = multiHit; a.m_lowerDamagePerHit = lowerDamagePerHit; a.m_hitTerrain = hitTerrain; a.m_hitFriendly = hitFriendly;
                a.m_hitThroughWalls = hitThroughWalls; a.m_consumeItem = consumeItem; a.m_skillAccuracy = skillAccuracy;
                a.m_recoilPushback = recoilPushback; a.m_raiseSkillAmount = raiseSkillAmount; a.m_attackStartNoise = startNoise;
                a.m_attackHitNoise = hitNoise; a.m_spawnOnHitChance = spawnOnHitChance;
                a.m_selfDamage = selfDamage; a.m_projectiles = projectiles; a.m_projectileBursts = projectileBursts;
                a.m_spawnOnTrigger = spawnOnTrigger; a.m_spawnOnHit = spawnOnHit; a.m_attackProjectile = projectile;
                a.m_projectileVel = projectileVelocity; a.m_projectileVelMin = projectileVelocityMin; a.m_projectileAccuracy = projectileAccuracy;
                a.m_projectileAccuracyMin = projectileAccuracyMin; a.m_burstInterval = burstInterval; a.m_launchAngle = launchAngle;
                a.m_reloadTime = reloadTime; a.m_reloadStaminaDrain = reloadStaminaDrain; a.m_reloadEitrDrain = reloadEitrDrain;
                a.m_drawDurationMin = drawDurationMin; a.m_drawStaminaDrain = drawStaminaDrain; a.m_drawEitrDrain = drawEitrDrain;
            }
        }
    }
}

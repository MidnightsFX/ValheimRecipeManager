using RecipeManager.Common;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using static RecipeManager.Common.DataObjects;
using Logger = RecipeManager.Common.Logger;

namespace RecipeManager
{
    /// <summary>Writes what a status effect entry sets onto the effect. Anything left out is not touched.</summary>
    internal static class StatusEffectAttributes
    {
        internal static void Apply(StatusEffect effect, StatusEffectDefinition def, Sprite icon)
        {
            string name = Tokens.Resolve(def.name, Tokens.StatusEffectPrefix, def.prefab, string.Empty);
            if (name != null) { effect.m_name = name; }
            string tooltip = Tokens.Resolve(def.tooltip, Tokens.StatusEffectPrefix, def.prefab, "_tooltip");
            if (tooltip != null) { effect.m_tooltip = tooltip; }
            if (def.category != null) { effect.m_category = def.category; }
            if (icon != null) { effect.m_icon = icon; }
            Set(ref effect.m_ttl, def.duration);
            Set(ref effect.m_cooldown, def.cooldown);
            Set(ref effect.m_hidden, def.hidden);
            Set(ref effect.m_flashIcon, def.flashIcon);
            Set(ref effect.m_cooldownIcon, def.cooldownIcon);
            Set(ref effect.m_attributes, def.attributes);
            if (def.activationAnimation != null) { effect.m_activationAnimation = def.activationAnimation; }

            StatusEffectMessages messages = def.messages;
            if (messages != null)
            {
                string start = Tokens.Resolve(messages.start, Tokens.StatusEffectPrefix, def.prefab, "_start");
                if (start != null) { effect.m_startMessage = start; }
                string stop = Tokens.Resolve(messages.stop, Tokens.StatusEffectPrefix, def.prefab, "_stop");
                if (stop != null) { effect.m_stopMessage = stop; }
                string repeat = Tokens.Resolve(messages.repeat, Tokens.StatusEffectPrefix, def.prefab, "_repeat");
                if (repeat != null) { effect.m_repeatMessage = repeat; }
                Set(ref effect.m_repeatInterval, messages.repeatInterval);
                Set(ref effect.m_startMessageType, messages.startType);
                Set(ref effect.m_stopMessageType, messages.stopType);
                Set(ref effect.m_repeatMessageType, messages.repeatType);
            }

            if (!(effect is SE_Stats stats))
            {
                if (HasStats(def))
                {
                    Logger.LogWarning($"{def.prefab} is a {effect.GetType().Name}, which has none of the stats a status effect entry can set. Only its name, texts, timings and icon are changed.");
                }
                return;
            }
            ApplyStats(stats, def);
        }

        /// <summary>
        /// Lists the maximum health, stamina and eitr an effect adds under its tooltip. The game lists an effect's own
        /// stats there, but knows nothing of these.
        /// </summary>
        internal static void AddBonusLines(StatusEffect effect, float? health, float? stamina, float? eitr)
        {
            List<string> lines = new List<string>();
            AddBonusLine(lines, "$item_food_health", health);
            AddBonusLine(lines, "$item_food_stamina", stamina);
            AddBonusLine(lines, "$item_food_eitr", eitr);
            if (lines.Count == 0) { return; }
            string bonuses = string.Join("\n", lines);
            effect.m_tooltip = string.IsNullOrEmpty(effect.m_tooltip) ? bonuses : $"{effect.m_tooltip}\n{bonuses}";
        }

        /// <summary>A tooltip without the lines added for maximum health, stamina and eitr, by this mod or by WackysDatabase.</summary>
        internal static string WithoutBonusLines(string tooltip)
        {
            if (string.IsNullOrEmpty(tooltip)) { return tooltip; }
            int start = tooltip.StartsWith(BonusLineStart, System.StringComparison.Ordinal) ? 0 : tooltip.IndexOf("\n" + BonusLineStart, System.StringComparison.Ordinal);
            return start < 0 ? tooltip : tooltip.Substring(0, start);
        }

        private const string BonusLineStart = "$item_food_";

        private static void AddBonusLine(List<string> lines, string label, float? bonus)
        {
            if (!bonus.HasValue || bonus.Value == 0f) { return; }
            string color = bonus.Value < 0f ? "red" : "orange";
            lines.Add($"{label}: <color={color}>{bonus.Value.ToString("+0.##;-0.##", CultureInfo.InvariantCulture)}</color>");
        }

        private static bool HasStats(StatusEffectDefinition def)
        {
            return def.health != null || def.stamina != null || def.staminaUse != null || def.eitr != null || def.adrenaline != null
                || def.armor != null || def.stagger != null || def.damage != null || def.resistances != null || def.skills != null
                || def.movement != null || def.noise != null || def.stealth != null || def.carryWeight != null;
        }

        private static void ApplyStats(SE_Stats stats, StatusEffectDefinition def)
        {
            HealthEffect health = def.health;
            if (health != null)
            {
                Set(ref stats.m_healthUpFront, health.upFront);
                Set(ref stats.m_healthPerTick, health.perTick);
                Set(ref stats.m_tickInterval, health.tickInterval);
                Set(ref stats.m_healthPerTickMinHealthPercentage, health.minHealthPercentage);
                Set(ref stats.m_healthOverTime, health.overTime);
                Set(ref stats.m_healthOverTimeDuration, health.overTimeDuration);
                Set(ref stats.m_healthOverTimeInterval, health.overTimeInterval);
                Set(ref stats.m_healthRegenMultiplier, health.regenMultiplier);
            }

            StaminaEffect stamina = def.stamina;
            if (stamina != null)
            {
                Set(ref stats.m_staminaUpFront, stamina.upFront);
                Set(ref stats.m_staminaOverTime, stamina.overTime);
                Set(ref stats.m_staminaOverTimeDuration, stamina.overTimeDuration);
                Set(ref stats.m_staminaOverTimeIsFraction, stamina.overTimeIsFraction);
                Set(ref stats.m_staminaDrainPerSec, stamina.drainPerSecond);
                Set(ref stats.m_staminaRegenMultiplier, stamina.regenMultiplier);
                Set(ref stats.m_runStaminaDrainModifier, stamina.runDrain);
            }

            StaminaUseEffect use = def.staminaUse;
            if (use != null)
            {
                Set(ref stats.m_homeItemStaminaUseModifier, use.homeItems);
                Set(ref stats.m_jumpStaminaUseModifier, use.jump);
                Set(ref stats.m_attackStaminaUseModifier, use.attack);
                Set(ref stats.m_blockStaminaUseModifier, use.block);
                Set(ref stats.m_blockStaminaUseFlatValue, use.blockFlat);
                Set(ref stats.m_dodgeStaminaUseModifier, use.dodge);
                Set(ref stats.m_swimStaminaUseModifier, use.swim);
                Set(ref stats.m_sneakStaminaUseModifier, use.sneak);
                Set(ref stats.m_runStaminaUseModifier, use.run);
            }

            EitrEffect eitr = def.eitr;
            if (eitr != null)
            {
                Set(ref stats.m_eitrUpFront, eitr.upFront);
                Set(ref stats.m_eitrOverTime, eitr.overTime);
                Set(ref stats.m_eitrOverTimeDuration, eitr.overTimeDuration);
                Set(ref stats.m_eitrRegenMultiplier, eitr.regenMultiplier);
            }

            if (def.adrenaline != null)
            {
                Set(ref stats.m_adrenalineUpFront, def.adrenaline.upFront);
                Set(ref stats.m_adrenalineModifier, def.adrenaline.modifier);
            }
            if (def.armor != null)
            {
                Set(ref stats.m_addArmor, def.armor.add);
                Set(ref stats.m_armorMultiplier, def.armor.multiplier);
            }
            if (def.stagger != null)
            {
                Set(ref stats.m_staggerModifier, def.stagger.modifier);
                Set(ref stats.m_timedBlockBonus, def.stagger.timedBlockBonus);
            }

            DamageEffect damage = def.damage;
            if (damage != null)
            {
                SetSkill(ref stats.m_modifyAttackSkill, damage.skill, def.prefab);
                Set(ref stats.m_damageModifier, damage.multiplier);
                if (damage.percentByType != null)
                {
                    HitData.DamageTypes percent = stats.m_percentigeDamageModifiers;
                    ItemAttributes.WriteDamage(ref percent, damage.percentByType);
                    stats.m_percentigeDamageModifiers = percent;
                }
            }

            if (def.resistances != null) { stats.m_mods = ItemAttributes.BuildResistances(def.resistances, def.prefab); }

            SkillEffect skills = def.skills;
            if (skills != null)
            {
                SetSkill(ref stats.m_raiseSkill, skills.raise, def.prefab);
                Set(ref stats.m_raiseSkillModifier, skills.raiseModifier);
                SetSkill(ref stats.m_skillLevel, skills.level, def.prefab);
                Set(ref stats.m_skillLevelModifier, skills.levelModifier);
                SetSkill(ref stats.m_skillLevel2, skills.level2, def.prefab);
                Set(ref stats.m_skillLevelModifier2, skills.level2Modifier);
            }

            MovementEffect movement = def.movement;
            if (movement != null)
            {
                Set(ref stats.m_speedModifier, movement.speed);
                Set(ref stats.m_swimSpeedModifier, movement.swimSpeed);
                if (movement.jump != null)
                {
                    Vector3 jump = stats.m_jumpModifier;
                    if (movement.jump.x.HasValue) { jump.x = movement.jump.x.Value; }
                    if (movement.jump.y.HasValue) { jump.y = movement.jump.y.Value; }
                    if (movement.jump.z.HasValue) { jump.z = movement.jump.z.Value; }
                    stats.m_jumpModifier = jump;
                }
                Set(ref stats.m_maxMaxFallSpeed, movement.maxFallSpeed);
                Set(ref stats.m_fallDamageModifier, movement.fallDamage);
                Set(ref stats.m_windMovementModifier, movement.wind);
                Set(ref stats.m_windRunStaminaModifier, movement.windRunStamina);
            }

            Set(ref stats.m_noiseModifier, def.noise);
            Set(ref stats.m_stealthModifier, def.stealth);
            Set(ref stats.m_addMaxCarryWeight, def.carryWeight);
        }

        private static void Set<T>(ref T field, T? configured) where T : struct
        {
            if (configured.HasValue) { field = configured.Value; }
        }

        private static void SetSkill(ref Skills.SkillType field, string configured, string effect)
        {
            if (string.IsNullOrEmpty(configured)) { return; }
            if (ItemAttributes.TryResolveSkill(configured, out Skills.SkillType skill)) { field = skill; }
            else { Logger.LogWarning($"{effect} uses the skill {configured}, which does not exist. That setting is left as it is."); }
        }
    }
}

using BepInEx;
using Jotunn.Entities;
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
    internal class StatusEffectReloadCommand : ConsoleCommand
    {
        public override string Name => "RM_StatusEffects_Reload";

        public override string Help => "Reloads status effects. A change reaches each effect the next time it is given.";

        // Gated behind `devcommands` - see IsCheat in Jotunn's ConsoleCommand.
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            if (ConfigChannel.IsConnectedClient())
            {
                ItemCommandUtils.Respond("This game is connected to a server and uses its status effects, change the files on the server instead.");
                return;
            }
            ValConfig.StatusEffects.ReloadFromDisk();
            ItemCommandUtils.Respond($"Reloaded {StatusEffectUpdater.Definitions.Count} status effect entry(s).");
        }
    }

    internal class StatusEffectPrintCommand : ConsoleCommand
    {
        internal const string DumpFileName = "ObjectDBStatusEffects.yaml";

        public override string Name => "RM_StatusEffects_PrintAll";

        public override string Help => "Prints the status effects in the Object DB to a reference file. Usage: RM_StatusEffects_PrintAll [nameFilter]";

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
            string path = Path.Combine(Paths.ConfigPath, "RecipeManager", DumpFileName);
            StatusEffectCollection collection = new StatusEffectCollection();
            foreach (StatusEffect effect in ObjectDB.instance.m_StatusEffects.ToList())
            {
                if (effect == null) { continue; }
                if (filter != null && effect.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                if (collection.statusEffects.ContainsKey(effect.name))
                {
                    Logger.LogWarning($"{effect.name} is in the object DB twice, only the first is written.");
                    continue;
                }
                collection.statusEffects.Add(effect.name, DescribeEffect(effect));
            }
            using (StreamWriter writer = new StreamWriter(path))
            {
                writer.WriteLine("# Reference dump of the status effects in the object DB, as they currently are.");
                writer.WriteLine("# This file is NOT loaded by the mod, it is here so you can see what the effects are.");
                writer.WriteLine("#");
                writer.WriteLine("# To change one of these effects, copy its entry into your own StatusEffects.yaml and delete");
                writer.WriteLine("# every line you do not want to change - anything left out keeps its current value.");
                writer.WriteLine("# To make a new effect from one instead, set 'action: Add', point 'clonedFrom' at it and give");
                writer.WriteLine("# 'prefab' a new unique name. Stats the game leaves at their defaults are not written.");
                writer.WriteLine();
                writer.WriteLine(ValConfig.yamlserializer.Serialize(collection));
            }
            ItemCommandUtils.Respond($"Dumped {collection.statusEffects.Count} status effect(s) to {path}");
        }

        /// <summary>An effect as the Modify that describes it. Stats left at the game's defaults are left out to keep it readable.</summary>
        internal static StatusEffectDefinition DescribeEffect(StatusEffect effect)
        {
            StatusEffectDefinition def = new StatusEffectDefinition
            {
                action = StatusEffectAction.Modify,
                prefab = effect.name,
                name = effect.m_name,
                tooltip = NullIfEmpty(StatusEffectAttributes.WithoutBonusLines(effect.m_tooltip)),
                category = string.IsNullOrEmpty(effect.m_category) ? null : effect.m_category,
                duration = effect.m_ttl,
                cooldown = effect.m_cooldown != 0f ? effect.m_cooldown : (float?)null,
                hidden = effect.m_hidden ? true : (bool?)null,
                flashIcon = effect.m_flashIcon ? true : (bool?)null,
                cooldownIcon = effect.m_cooldownIcon ? true : (bool?)null,
                attributes = effect.m_attributes != StatusEffect.StatusAttribute.None ? effect.m_attributes : (StatusEffect.StatusAttribute?)null,
                activationAnimation = effect.m_cooldown != 0f ? effect.m_activationAnimation : null
            };
            if (!string.IsNullOrEmpty(effect.m_startMessage) || !string.IsNullOrEmpty(effect.m_stopMessage) || !string.IsNullOrEmpty(effect.m_repeatMessage))
            {
                def.messages = new StatusEffectMessages
                {
                    start = NullIfEmpty(effect.m_startMessage),
                    stop = NullIfEmpty(effect.m_stopMessage),
                    repeat = NullIfEmpty(effect.m_repeatMessage),
                    repeatInterval = effect.m_repeatInterval != 0f ? effect.m_repeatInterval : (float?)null,
                    startType = effect.m_startMessageType,
                    stopType = effect.m_stopMessageType,
                    repeatType = string.IsNullOrEmpty(effect.m_repeatMessage) ? (MessageHud.MessageType?)null : effect.m_repeatMessageType
                };
            }
            if (effect is SE_Stats stats) { DescribeStats(stats, def); }
            if (StatusEffectUpdater.StatBonuses.TryGetValue(effect.NameHash(), out Vector3 bonus))
            {
                def.maxHealth = NonZero(bonus.x);
                def.maxStamina = NonZero(bonus.y);
                def.maxEitr = NonZero(bonus.z);
            }
            return def;
        }

        private static void DescribeStats(SE_Stats s, StatusEffectDefinition def)
        {
            HealthEffect health = new HealthEffect
            {
                upFront = NonZero(s.m_healthUpFront),
                perTick = NonZero(s.m_healthPerTick),
                tickInterval = NonZero(s.m_tickInterval),
                minHealthPercentage = NonZero(s.m_healthPerTickMinHealthPercentage),
                overTime = NonZero(s.m_healthOverTime),
                overTimeDuration = NonZero(s.m_healthOverTimeDuration),
                overTimeInterval = s.m_healthOverTime != 0f ? s.m_healthOverTimeInterval : (float?)null,
                regenMultiplier = NotOne(s.m_healthRegenMultiplier)
            };
            if (Any(health.upFront, health.perTick, health.tickInterval, health.minHealthPercentage, health.overTime, health.overTimeDuration, health.overTimeInterval, health.regenMultiplier)) { def.health = health; }

            StaminaEffect stamina = new StaminaEffect
            {
                upFront = NonZero(s.m_staminaUpFront),
                overTime = NonZero(s.m_staminaOverTime),
                overTimeDuration = NonZero(s.m_staminaOverTimeDuration),
                overTimeIsFraction = s.m_staminaOverTimeIsFraction ? true : (bool?)null,
                drainPerSecond = NonZero(s.m_staminaDrainPerSec),
                regenMultiplier = NotOne(s.m_staminaRegenMultiplier),
                runDrain = NonZero(s.m_runStaminaDrainModifier)
            };
            if (Any(stamina.upFront, stamina.overTime, stamina.overTimeDuration, stamina.drainPerSecond, stamina.regenMultiplier, stamina.runDrain) || stamina.overTimeIsFraction != null) { def.stamina = stamina; }

            StaminaUseEffect use = new StaminaUseEffect
            {
                homeItems = NonZero(s.m_homeItemStaminaUseModifier),
                jump = NonZero(s.m_jumpStaminaUseModifier),
                attack = NonZero(s.m_attackStaminaUseModifier),
                block = NonZero(s.m_blockStaminaUseModifier),
                blockFlat = NonZero(s.m_blockStaminaUseFlatValue),
                dodge = NonZero(s.m_dodgeStaminaUseModifier),
                swim = NonZero(s.m_swimStaminaUseModifier),
                sneak = NonZero(s.m_sneakStaminaUseModifier),
                run = NonZero(s.m_runStaminaUseModifier)
            };
            if (Any(use.homeItems, use.jump, use.attack, use.block, use.blockFlat, use.dodge, use.swim, use.sneak, use.run)) { def.staminaUse = use; }

            EitrEffect eitr = new EitrEffect
            {
                upFront = NonZero(s.m_eitrUpFront),
                overTime = NonZero(s.m_eitrOverTime),
                overTimeDuration = NonZero(s.m_eitrOverTimeDuration),
                regenMultiplier = NotOne(s.m_eitrRegenMultiplier)
            };
            if (Any(eitr.upFront, eitr.overTime, eitr.overTimeDuration, eitr.regenMultiplier)) { def.eitr = eitr; }

            if (s.m_adrenalineUpFront != 0f || s.m_adrenalineModifier != 0f)
            {
                def.adrenaline = new AdrenalineEffect { upFront = NonZero(s.m_adrenalineUpFront), modifier = NonZero(s.m_adrenalineModifier) };
            }
            if (s.m_addArmor != 0f || s.m_armorMultiplier != 0f)
            {
                def.armor = new ArmorEffect { add = NonZero(s.m_addArmor), multiplier = NonZero(s.m_armorMultiplier) };
            }
            if (s.m_staggerModifier != 0f || s.m_timedBlockBonus != 0f)
            {
                def.stagger = new StaggerEffect { modifier = NonZero(s.m_staggerModifier), timedBlockBonus = NonZero(s.m_timedBlockBonus) };
            }
            // Any type, the game's own check only looks for damage above 0
            DamageValues percent = DescribeDamage(s.m_percentigeDamageModifiers);
            if (s.m_damageModifier != 1f || percent != null)
            {
                def.damage = new DamageEffect
                {
                    skill = s.m_damageModifier != 1f ? s.m_modifyAttackSkill.ToString() : null,
                    multiplier = NotOne(s.m_damageModifier),
                    percentByType = percent
                };
            }
            if (s.m_mods != null && s.m_mods.Count > 0)
            {
                def.resistances = s.m_mods.Select(mod => new DamageResistance { type = mod.m_type, modifier = mod.m_modifier }).ToList();
            }
            if (s.m_raiseSkillModifier != 0f || s.m_skillLevelModifier != 0f || s.m_skillLevelModifier2 != 0f)
            {
                def.skills = new SkillEffect
                {
                    raise = s.m_raiseSkillModifier != 0f ? s.m_raiseSkill.ToString() : null,
                    raiseModifier = NonZero(s.m_raiseSkillModifier),
                    level = s.m_skillLevelModifier != 0f ? s.m_skillLevel.ToString() : null,
                    levelModifier = NonZero(s.m_skillLevelModifier),
                    level2 = s.m_skillLevelModifier2 != 0f ? s.m_skillLevel2.ToString() : null,
                    level2Modifier = NonZero(s.m_skillLevelModifier2)
                };
            }
            bool jumps = s.m_jumpModifier != Vector3.zero;
            if (s.m_speedModifier != 0f || s.m_swimSpeedModifier != 0f || jumps || s.m_maxMaxFallSpeed != 0f || s.m_fallDamageModifier != 0f
                || s.m_windMovementModifier != 0f || s.m_windRunStaminaModifier != 0f)
            {
                def.movement = new MovementEffect
                {
                    speed = NonZero(s.m_speedModifier),
                    swimSpeed = NonZero(s.m_swimSpeedModifier),
                    jump = jumps ? new VectorValues { x = s.m_jumpModifier.x, y = s.m_jumpModifier.y, z = s.m_jumpModifier.z } : null,
                    maxFallSpeed = NonZero(s.m_maxMaxFallSpeed),
                    fallDamage = NonZero(s.m_fallDamageModifier),
                    wind = NonZero(s.m_windMovementModifier),
                    windRunStamina = NonZero(s.m_windRunStaminaModifier)
                };
            }
            def.noise = NonZero(s.m_noiseModifier);
            def.stealth = NonZero(s.m_stealthModifier);
            def.carryWeight = NonZero(s.m_addMaxCarryWeight);
        }

        private static DamageValues DescribeDamage(HitData.DamageTypes damages)
        {
            DamageValues values = new DamageValues
            {
                damage = NonZero(damages.m_damage),
                blunt = NonZero(damages.m_blunt),
                slash = NonZero(damages.m_slash),
                pierce = NonZero(damages.m_pierce),
                chop = NonZero(damages.m_chop),
                pickaxe = NonZero(damages.m_pickaxe),
                fire = NonZero(damages.m_fire),
                frost = NonZero(damages.m_frost),
                lightning = NonZero(damages.m_lightning),
                poison = NonZero(damages.m_poison),
                spirit = NonZero(damages.m_spirit),
                nonPlayer = NonZero(damages.m_nonPlayer)
            };
            return Any(values.damage, values.blunt, values.slash, values.pierce, values.chop, values.pickaxe, values.fire, values.frost,
                values.lightning, values.poison, values.spirit, values.nonPlayer) ? values : null;
        }

        private static float? NonZero(float value) { return value != 0f ? value : (float?)null; }
        private static float? NotOne(float value) { return value != 1f ? value : (float?)null; }
        private static string NullIfEmpty(string value) { return string.IsNullOrEmpty(value) ? null : value; }
        private static bool Any(params float?[] values) { return values.Any(value => value.HasValue); }

        public override List<string> CommandOptionList()
        {
            List<string> options = new List<string>();
            if (ObjectDB.instance?.m_StatusEffects == null) { return options; }
            foreach (StatusEffect effect in ObjectDB.instance.m_StatusEffects)
            {
                if (effect != null) { options.Add(effect.name); }
            }
            return options;
        }
    }
}

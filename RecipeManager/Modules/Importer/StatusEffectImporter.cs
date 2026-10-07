using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RecipeManager.Common.DataObjects;

namespace RecipeManager.Importer
{
    /// <summary>
    /// A WackysDatabase SE_ file as a StatusEffects.yaml entry. Values that match the effect it changes, or the effect
    /// a new one copies, are left out, since the entry keeps those anyway.
    /// </summary>
    internal static class StatusEffectImporter
    {
        /// <summary>What WackysDatabase copies a new effect from when the file names nothing.</summary>
        internal const string DefaultClone = "SetEffect_TrollArmor";
        private const string RunningState = "this is the effect's running state, not a setting";
        private const string OtherKinds = "shield, poison and frost effects are not part of RecipeManager's status effects";

        private static readonly Dictionary<string, string> Reasons = new Dictionary<string, string>
        {
            ["ReapplyCooldown"] = WackyImporter.NeedsPatch,
            ["EndingStatusEffect"] = $"starting another effect when this one ends {WackyImporter.NeedsPatch}",
            ["StartEffect_PLUS"] = WackyImporter.Visual,
            ["StopEffect_PLUS"] = WackyImporter.Visual,
            ["SeShield"] = OtherKinds,
            ["SePoison"] = OtherKinds,
            ["SeFrost"] = OtherKinds,
            ["IconName"] = "WackysDatabase's dumps only note the icon here, CustomIcon is what changes it",
            ["RepeatInterval"] = "WackysDatabase reads this but never applies it",
            ["m_tickTimer"] = RunningState,
            ["m_healthOverTimeTimer"] = RunningState,
            ["m_healthOverTimeTicks"] = RunningState,
            ["m_healthOverTimeTickHP"] = RunningState
        };

        /// <summary>The game's field for each WackysDatabase key whose name differs from it, for comparing values.</summary>
        private static readonly Dictionary<string, string> FieldNames = new Dictionary<string, string>
        {
            ["Status_m_name"] = "m_name",
            ["Category"] = "m_category",
            ["FlashIcon"] = "m_flashIcon",
            ["CooldownIcon"] = "m_cooldownIcon",
            ["Hidden"] = "m_hidden",
            ["Tooltip"] = "m_tooltip",
            ["Attributes"] = "m_attributes",
            ["StartMessageLoc"] = "m_startMessageType",
            ["StartMessage"] = "m_startMessage",
            ["StopMessageLoc"] = "m_stopMessageType",
            ["StopMessage"] = "m_stopMessage",
            ["RepeatMessageLoc"] = "m_repeatMessageType",
            ["RepeatMessage"] = "m_repeatMessage",
            ["RepeatInterval"] = "m_repeatInterval",
            ["TimeToLive"] = "m_ttl",
            ["Cooldown"] = "m_cooldown",
            ["ActivationAnimation"] = "m_activationAnimation",
            // WackysDatabase's own spellings of these
            ["m_heatlhUpFront"] = "m_healthUpFront",
            ["m_armorAdd"] = "m_addArmor",
            ["m_staggerTimeBlockBonus"] = "m_timedBlockBonus"
        };

        private static readonly string[] DamageKeys =
        {
            "m_damage", "m_blunt", "m_slash", "m_pierce", "m_chop", "m_pickaxe", "m_fire", "m_frost", "m_lightning", "m_poison", "m_spirit", "m_nonPlayer"
        };

        internal static void Import(WackyImporter ctx, WackyFile file)
        {
            ImportReport.FileResult result = ctx.Report.Add(file);
            WackyNode node = ctx.Parse(file, result);
            if (node == null) { return; }
            string name = node.Str("Name")?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                result.Skipped = "it has no Name";
                return;
            }
            node.Converted.Add($"Name -> prefab: {name}");

            StatusEffect existing = WackyImporter.FindEffect(name);
            string clone = node.Str("ClonedSE")?.Trim();
            // WackysDatabase only copies ClonedSE when the effect does not exist. While it runs, the effects it made exist
            // too, and so do the ones RecipeManager made from an earlier import.
            bool madeHere = StatusEffectUpdater.IsCreated(name);
            bool add = existing == null || madeHere || (ctx.WackyDBLoaded && !string.IsNullOrEmpty(clone));
            StatusEffectDefinition def = new StatusEffectDefinition
            {
                action = add ? StatusEffectAction.Add : StatusEffectAction.Modify,
                prefab = name
            };
            StatusEffect baseline = existing;
            if (add)
            {
                if (existing != null && !madeHere)
                {
                    result.Warnings.Add($"WackysDatabase is running, so whether {name} is new could not be told. It was taken as new because it names a ClonedSE");
                }
                if (string.IsNullOrEmpty(clone))
                {
                    clone = DefaultClone;
                    result.Warnings.Add($"it names no ClonedSE, so like WackysDatabase the new effect copies {DefaultClone}");
                }
                else { node.Converted.Add("ClonedSE -> clonedFrom"); }
                def.clonedFrom = clone;
                StatusEffect source = WackyImporter.FindEffect(clone);
                if (source == null && !ctx.CreatedEffects.Contains(clone))
                {
                    result.Warnings.Add($"no status effect named {clone} exists in this game to copy");
                }
                // One RecipeManager made is not the game's, every value is written out against it
                baseline = StatusEffectUpdater.IsCreated(clone) ? null : source;
                result.Warnings.Add($"WackysDatabase's copy has none of the visual start and stop effects of {clone}, RecipeManager's copy keeps them");
                ctx.CreatedEffects.Add(name);
            }
            else if (!string.IsNullOrEmpty(clone))
            {
                node.Use("ClonedSE", $"not used, {name} already exists and WackysDatabase changes it instead of copying {clone}");
            }

            // Compared with the game's own values, not with what RecipeManager's config has made of them
            StatusEffect original = StatusEffectUpdater.OriginalCopy(baseline);
            if (original != null) { baseline = original; }
            Reader read = new Reader(ctx, baseline);
            def.name = read.Str(node, "Status_m_name", "name");
            // A dump made while WackysDatabase ran has its health, stamina and eitr lines in it, which RecipeManager adds itself
            def.tooltip = StatusEffectAttributes.WithoutBonusLines(read.Str(node, "Tooltip", "tooltip"));
            def.category = read.Str(node, "Category", "category");
            def.flashIcon = read.Bool(node, "FlashIcon", "flashIcon");
            def.cooldownIcon = read.Bool(node, "CooldownIcon", "cooldownIcon");
            def.hidden = read.Bool(node, "Hidden", "hidden");
            def.attributes = read.Enum<StatusEffect.StatusAttribute>(node, "Attributes", "attributes");
            def.duration = read.Float(node, "TimeToLive", "duration");
            def.cooldown = read.Float(node, "Cooldown", "cooldown");
            def.activationAnimation = read.Str(node, "ActivationAnimation", "activationAnimation");
            def.messages = Messages(read, node);
            def.maxHealth = node.Float("AddHP", "maxHealth");
            def.maxStamina = node.Float("AddStamina", "maxStamina");
            def.maxEitr = node.Float("AddEitr", "maxEitr");
            ctx.QueueIcon(node, "CustomIcon", name, result);

            if (baseline != null && !(baseline is SE_Stats))
            {
                node.Drop("SeData", $"{baseline.name} is a {baseline.GetType().Name}, which has none of these stats, so WackysDatabase changes nothing with them either");
            }
            else
            {
                WackyNode stats = node.Child("SeData");
                if (stats != null) { Stats(read, stats, def, result); }
            }

            ImportReport.Collect(result, node, key => WackyImporter.ReasonFor(Reasons, key), (key, value) => read.Same(key, value));
            if (original != null) { UnityEngine.Object.Destroy(original); }

            string entryKey = ctx.NewKey("se", name, StatusEffectUpdater.Definitions.Keys);
            ctx.StatusEffects.Add(entryKey, def);
            result.Targets.Add($"{entryKey} ({def.action}) in Wacky_StatusEffects.yaml");
        }

        private static StatusEffectMessages Messages(Reader read, WackyNode node)
        {
            StatusEffectMessages messages = new StatusEffectMessages
            {
                start = read.Str(node, "StartMessage", "messages.start"),
                startType = read.Enum<MessageHud.MessageType>(node, "StartMessageLoc", "messages.startType"),
                stop = read.Str(node, "StopMessage", "messages.stop"),
                stopType = read.Enum<MessageHud.MessageType>(node, "StopMessageLoc", "messages.stopType"),
                repeat = read.Str(node, "RepeatMessage", "messages.repeat"),
                repeatType = read.Enum<MessageHud.MessageType>(node, "RepeatMessageLoc", "messages.repeatType")
            };
            bool any = messages.start != null || messages.startType != null || messages.stop != null || messages.stopType != null
                || messages.repeat != null || messages.repeatType != null;
            return any ? messages : null;
        }

        private static void Stats(Reader read, WackyNode s, StatusEffectDefinition def, ImportReport.FileResult result)
        {
            HealthEffect health = new HealthEffect
            {
                upFront = read.Float(s, "m_heatlhUpFront", "health.upFront"),
                perTick = read.Float(s, "m_healthPerTick", "health.perTick"),
                tickInterval = read.Float(s, "m_tickInterval", "health.tickInterval"),
                minHealthPercentage = read.Float(s, "m_healthPerTickMinHealthPercentage", "health.minHealthPercentage"),
                overTime = read.Float(s, "m_healthOverTime", "health.overTime"),
                overTimeDuration = read.Float(s, "m_healthOverTimeDuration", "health.overTimeDuration"),
                overTimeInterval = read.Float(s, "m_healthOverTimeInterval", "health.overTimeInterval"),
                regenMultiplier = read.Float(s, "m_healthRegenMultiplier", "health.regenMultiplier")
            };
            if (Any(health.upFront, health.perTick, health.tickInterval, health.minHealthPercentage, health.overTime, health.overTimeDuration,
                health.overTimeInterval, health.regenMultiplier)) { def.health = health; }

            StaminaEffect stamina = new StaminaEffect
            {
                upFront = read.Float(s, "m_staminaUpFront", "stamina.upFront"),
                overTime = read.Float(s, "m_staminaOverTime", "stamina.overTime"),
                overTimeDuration = read.Float(s, "m_staminaOverTimeDuration", "stamina.overTimeDuration"),
                drainPerSecond = read.Float(s, "m_staminaDrainPerSec", "stamina.drainPerSecond"),
                regenMultiplier = read.Float(s, "m_staminaRegenMultiplier", "stamina.regenMultiplier"),
                runDrain = read.Float(s, "m_runStaminaDrainModifier", "stamina.runDrain")
            };
            if (Any(stamina.upFront, stamina.overTime, stamina.overTimeDuration, stamina.drainPerSecond, stamina.regenMultiplier, stamina.runDrain)) { def.stamina = stamina; }

            StaminaUseEffect use = new StaminaUseEffect
            {
                homeItems = read.Float(s, "m_homeItemStaminaUseModifier", "staminaUse.homeItems"),
                jump = read.Float(s, "m_jumpStaminaUseModifier", "staminaUse.jump"),
                attack = read.Float(s, "m_attackStaminaUseModifier", "staminaUse.attack"),
                block = read.Float(s, "m_blockStaminaUseModifier", "staminaUse.block"),
                blockFlat = read.Float(s, "m_blockStaminaUseFlatValue", "staminaUse.blockFlat"),
                dodge = read.Float(s, "m_dodgeStaminaUseModifier", "staminaUse.dodge"),
                swim = read.Float(s, "m_swimStaminaUseModifier", "staminaUse.swim"),
                sneak = read.Float(s, "m_sneakStaminaUseModifier", "staminaUse.sneak"),
                run = read.Float(s, "m_runStaminaUseModifier", "staminaUse.run")
            };
            if (Any(use.homeItems, use.jump, use.attack, use.block, use.blockFlat, use.dodge, use.swim, use.sneak, use.run)) { def.staminaUse = use; }

            EitrEffect eitr = new EitrEffect
            {
                upFront = read.Float(s, "m_eitrUpFront", "eitr.upFront"),
                overTime = read.Float(s, "m_eitrOverTime", "eitr.overTime"),
                overTimeDuration = read.Float(s, "m_eitrOverTimeDuration", "eitr.overTimeDuration"),
                regenMultiplier = read.Float(s, "m_eitrRegenMultiplier", "eitr.regenMultiplier")
            };
            if (Any(eitr.upFront, eitr.overTime, eitr.overTimeDuration, eitr.regenMultiplier)) { def.eitr = eitr; }

            AdrenalineEffect adrenaline = new AdrenalineEffect
            {
                upFront = read.Float(s, "m_adrenalineUpFront", "adrenaline.upFront"),
                modifier = read.Float(s, "m_adrenalineModifier", "adrenaline.modifier")
            };
            if (Any(adrenaline.upFront, adrenaline.modifier)) { def.adrenaline = adrenaline; }

            ArmorEffect armor = new ArmorEffect
            {
                add = read.Float(s, "m_armorAdd", "armor.add"),
                multiplier = read.Float(s, "m_armorMultiplier", "armor.multiplier")
            };
            if (Any(armor.add, armor.multiplier)) { def.armor = armor; }

            StaggerEffect stagger = new StaggerEffect
            {
                modifier = read.Float(s, "m_staggerModifier", "stagger.modifier"),
                timedBlockBonus = read.Float(s, "m_staggerTimeBlockBonus", "stagger.timedBlockBonus")
            };
            if (Any(stagger.modifier, stagger.timedBlockBonus)) { def.stagger = stagger; }

            DamageEffect damage = new DamageEffect
            {
                skill = read.Str(s, "m_modifyAttackSkill", "damage.skill"),
                multiplier = read.Float(s, "m_damageModifier", "damage.multiplier"),
                percentByType = PercentDamage(read, s)
            };
            if (damage.skill != null || damage.multiplier != null || damage.percentByType != null) { def.damage = damage; }

            def.resistances = Resistances(read, s, result);

            SkillEffect skills = new SkillEffect
            {
                raise = read.Str(s, "m_raiseSkill", "skills.raise"),
                raiseModifier = read.Float(s, "m_raiseSkillModifier", "skills.raiseModifier"),
                level = read.Str(s, "m_skillLevel", "skills.level"),
                levelModifier = read.Float(s, "m_skillLevelModifier", "skills.levelModifier"),
                level2 = read.Str(s, "m_skillLevel2", "skills.level2"),
                level2Modifier = read.Float(s, "m_skillLevelModifier2", "skills.level2Modifier")
            };
            if (skills.raise != null || skills.level != null || skills.level2 != null || Any(skills.raiseModifier, skills.levelModifier, skills.level2Modifier))
            {
                def.skills = skills;
            }

            MovementEffect movement = new MovementEffect
            {
                speed = read.Float(s, "m_speedModifier", "movement.speed"),
                jump = Jump(read, s),
                maxFallSpeed = read.Float(s, "m_maxMaxFallSpeed", "movement.maxFallSpeed"),
                fallDamage = read.Float(s, "m_fallDamageModifier", "movement.fallDamage"),
                wind = read.Float(s, "m_windMovementModifier", "movement.wind")
            };
            if (movement.jump != null || Any(movement.speed, movement.maxFallSpeed, movement.fallDamage, movement.wind)) { def.movement = movement; }

            def.noise = read.Float(s, "m_noiseModifier", "noise");
            def.stealth = read.Float(s, "m_stealthModifier", "stealth");
            def.carryWeight = read.Float(s, "m_addMaxCarryWeight", "carryWeight");
        }

        /// <summary>WackysDatabase sets the whole set of damage types, so a type it leaves out becomes 0. Every type is written out to do the same.</summary>
        private static DamageValues PercentDamage(Reader read, WackyNode stats)
        {
            if (!stats.Has("m_percentDamageModifiers")) { return null; }
            WackyNode block = stats.Child("m_percentDamageModifiers");
            if (block == null) { return null; }
            float[] values = DamageKeys.Select(key => block.Float(key) ?? 0f).ToArray();
            if (read.Baseline is SE_Stats current && !read.WackyDBLoaded && values.SequenceEqual(Values(current.m_percentigeDamageModifiers)))
            {
                block.Converted.Add($"{block.Name}: the same as the effect already has");
                return null;
            }
            block.Converted.Add($"{block.Name} -> damage.percentByType, every damage type, 0 where it lists none");
            return new DamageValues
            {
                damage = values[0], blunt = values[1], slash = values[2], pierce = values[3], chop = values[4], pickaxe = values[5],
                fire = values[6], frost = values[7], lightning = values[8], poison = values[9], spirit = values[10], nonPlayer = values[11]
            };
        }

        private static float[] Values(HitData.DamageTypes damage)
        {
            return new[]
            {
                damage.m_damage, damage.m_blunt, damage.m_slash, damage.m_pierce, damage.m_chop, damage.m_pickaxe,
                damage.m_fire, damage.m_frost, damage.m_lightning, damage.m_poison, damage.m_spirit, damage.m_nonPlayer
            };
        }

        /// <summary>WackysDatabase sets the whole jump, so a direction it leaves out becomes 0.</summary>
        private static VectorValues Jump(Reader read, WackyNode stats)
        {
            WackyNode block = stats.Child("m_jumpModifier");
            if (block == null) { return null; }
            Vector3 jump = new Vector3(block.Float("x") ?? 0f, block.Float("y") ?? 0f, block.Float("z") ?? 0f);
            if (read.Baseline is SE_Stats current && !read.WackyDBLoaded && current.m_jumpModifier == jump)
            {
                block.Converted.Add($"{block.Name}: the same as the effect already has");
                return null;
            }
            block.Converted.Add($"{block.Name} -> movement.jump");
            return new VectorValues { x = jump.x, y = jump.y, z = jump.z };
        }

        /// <summary>
        /// The resistance list, which replaces the effect's whole list. WackysDatabase's own defaults hand it an empty
        /// list when SeData leaves m_mods out altogether, which empties the effect's resistances, so that does too.
        /// </summary>
        private static List<DamageResistance> Resistances(Reader read, WackyNode stats, ImportReport.FileResult result)
        {
            List<DamageResistance> resistances = new List<DamageResistance>();
            if (!stats.Contains("m_mods"))
            {
                if (read.Baseline is SE_Stats withMods && withMods.m_mods != null && withMods.m_mods.Count > 0)
                {
                    result.Warnings.Add("SeData has no m_mods, which WackysDatabase takes as an empty resistance list and so takes the effect's resistances away. The import does the same");
                    return resistances;
                }
                return null;
            }
            List<WackyNode> entries = stats.ChildList("m_mods");
            // Written with no value, which leaves the resistances alone
            if (entries == null) { return null; }
            for (int i = 0; i < entries.Count; i++)
            {
                WackyNode entry = entries[i];
                string type = entry.Str("m_type")?.Trim();
                if (string.IsNullOrEmpty(type)) { continue; }
                if (string.Equals(type, "Water", StringComparison.OrdinalIgnoreCase))
                {
                    entry.Use("m_modifier");
                    result.Dropped.Add($"{entry.Name} ({type}) - the Water damage type is WackysDatabase's own and {WackyImporter.NeedsPatch}");
                    continue;
                }
                HitData.DamageModifier modifier = entry.Enum<HitData.DamageModifier>("m_modifier") ?? HitData.DamageModifier.Normal;
                if (!Enum.TryParse(type, true, out HitData.DamageType damageType) || !Common.ItemAttributes.ResistableTypes.Contains(damageType))
                {
                    result.Warnings.Add($"{entry.Name}: the game has no resistance to {type} damage, it was left out");
                    continue;
                }
                resistances.Add(new DamageResistance { type = damageType, modifier = modifier });
            }
            if (read.Baseline is SE_Stats current && !read.WackyDBLoaded && SameResistances(current.m_mods, resistances))
            {
                stats.Converted.Add($"{stats.Where("m_mods")}: the same as the effect already has");
                return null;
            }
            stats.Converted.Add($"{stats.Where("m_mods")} -> resistances");
            return resistances;
        }

        private static bool SameResistances(List<HitData.DamageModPair> current, List<DamageResistance> imported)
        {
            if (current == null) { return imported.Count == 0; }
            if (current.Count != imported.Count) { return false; }
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i].m_type != imported[i].type || current[i].m_modifier != imported[i].modifier) { return false; }
            }
            return true;
        }

        private static bool Any(params float?[] values)
        {
            return values.Any(value => value.HasValue);
        }

        /// <summary>
        /// Reads a value unless it is the one the effect already has, which is left unread so the report lists it as
        /// unchanged.
        /// </summary>
        private sealed class Reader
        {
            private readonly WackyImporter ctx;
            public readonly StatusEffect Baseline;

            public Reader(WackyImporter ctx, StatusEffect baseline)
            {
                this.ctx = ctx;
                Baseline = baseline;
            }

            public bool WackyDBLoaded => ctx.WackyDBLoaded;

            /// <summary>Whether a WackysDatabase value is the one the effect already has.</summary>
            public bool Same(string key, object value)
            {
                string field = FieldNames.TryGetValue(key, out string renamed) ? renamed : key;
                return ctx.SameAsGame(Baseline, field, value);
            }

            private bool Skip(WackyNode node, string key)
            {
                return node.Has(key) && Same(key, node.Raw(key));
            }

            public string Str(WackyNode node, string key, string rmKey)
            {
                return Skip(node, key) ? null : node.Str(key, rmKey);
            }

            public float? Float(WackyNode node, string key, string rmKey)
            {
                return Skip(node, key) ? null : node.Float(key, rmKey);
            }

            public bool? Bool(WackyNode node, string key, string rmKey)
            {
                return Skip(node, key) ? null : node.Bool(key, rmKey);
            }

            public T? Enum<T>(WackyNode node, string key, string rmKey) where T : struct
            {
                return Skip(node, key) ? null : node.Enum<T>(key, rmKey);
            }
        }
    }
}

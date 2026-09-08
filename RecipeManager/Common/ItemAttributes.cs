using System;
using System.Collections.Generic;
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

        /// <summary>Value that clears a status effect slot instead of naming one.</summary>
        private const string ClearToken = "none";

        /// <summary>The damage types HitData.DamageModifiers.Apply actually understands.</summary>
        private static readonly HashSet<HitData.DamageType> ResistableTypes = new HashSet<HitData.DamageType>
        {
            HitData.DamageType.Blunt, HitData.DamageType.Slash, HitData.DamageType.Pierce,
            HitData.DamageType.Chop, HitData.DamageType.Pickaxe, HitData.DamageType.Fire,
            HitData.DamageType.Frost, HitData.DamageType.Lightning, HitData.DamageType.Poison,
            HitData.DamageType.Spirit
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
        /// <param name="warnOnMissingStatusEffect">
        /// False while items are still being created, because the status effect list is not populated
        /// that early and a missing effect is expected rather than a mistake.
        /// </param>
        /// <returns>False when a named status effect could not be resolved and needs a second pass.</returns>
        public static bool Apply(ItemDrop.ItemData.SharedData shared, ItemAddition def, bool warnOnMissingStatusEffect)
        {
            if (shared == null || def == null) { return true; }

            ApplyGeneral(shared, def);
            ApplyFood(shared, def);
            ApplyWeapon(shared, def);
            ApplyArmor(shared, def);
            ApplyResistances(shared, def);
            ApplySetBonus(shared, def);
            return ApplyStatusEffects(shared, def, warnOnMissingStatusEffect);
        }

        private static void ApplyGeneral(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            if (def.weight.HasValue) { shared.m_weight = def.weight.Value; }
            if (def.maxStackSize.HasValue) { shared.m_maxStackSize = def.maxStackSize.Value; }
            if (def.value.HasValue) { shared.m_value = def.value.Value; }
            if (def.teleportable.HasValue) { shared.m_teleportable = def.teleportable.Value; }
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
        }

        /// <summary>DamageTypes is a struct, so this takes it by reference and the caller writes it back.</summary>
        private static void WriteDamage(ref HitData.DamageTypes target, DamageValues values)
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
        }

        /// <summary>
        /// Resistances are replaced wholesale rather than merged, so the yaml always reads as the
        /// complete list of what the item resists. An empty list clears them.
        /// </summary>
        private static void ApplyResistances(ItemDrop.ItemData.SharedData shared, ItemAddition def)
        {
            if (def.resistances == null) { return; }

            List<HitData.DamageModPair> modifiers = new List<HitData.DamageModPair>();
            foreach (DamageResistance resistance in def.resistances)
            {
                if (resistance == null) { continue; }
                if (!ResistableTypes.Contains(resistance.type))
                {
                    Logger.LogWarning($"'{resistance.type}' is not a resistance type that {def.prefab} can use, it will be ignored. Use one of Blunt, Slash, Pierce, Chop, Pickaxe, Fire, Frost, Lightning, Poison or Spirit.");
                    continue;
                }
                modifiers.Add(new HitData.DamageModPair { m_type = resistance.type, m_modifier = resistance.modifier });
            }
            shared.m_damageModifiers = modifiers;
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
        /// Resolves the four status effect slots. Split out from the rest because status effects only
        /// exist once the object DB is built, which is later than items are created.
        /// </summary>
        /// <returns>False when something named could not be found and is worth retrying later.</returns>
        public static bool ApplyStatusEffects(ItemDrop.ItemData.SharedData shared, ItemAddition def, bool warnOnMissing)
        {
            if (shared == null || def == null) { return true; }
            bool resolvedEverything = true;
            StatusEffect effect;
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
            }

            if (def.setBonus != null)
            {
                if (TryFindEffect(def.setBonus.statusEffect, def.prefab, "setBonus", warnOnMissing, out effect, out assign))
                {
                    if (assign) { shared.m_setStatusEffect = effect; }
                }
                else { resolvedEverything = false; }
            }

            return resolvedEverything;
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
                    Logger.LogWarning($"The {slot} status effect '{configured}' referenced by {prefabName} could not be found. Only status effects that already exist can be referenced, creating new ones is not supported.");
                }
                return false;
            }

            effect = found;
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

            private float armor;
            private float armorPerLevel;
            private float blockPower;
            private float blockPowerPerLevel;
            private float deflectionForce;
            private float deflectionForcePerLevel;
            private float movementModifier;
            private float eitrRegenModifier;
            private List<HitData.DamageModPair> damageModifiers;

            private StatusEffect consumeStatusEffect;
            private StatusEffect equipStatusEffect;
            private StatusEffect attackStatusEffect;
            private float attackStatusEffectChance;

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

                    armor = shared.m_armor,
                    armorPerLevel = shared.m_armorPerLevel,
                    blockPower = shared.m_blockPower,
                    blockPowerPerLevel = shared.m_blockPowerPerLevel,
                    deflectionForce = shared.m_deflectionForce,
                    deflectionForcePerLevel = shared.m_deflectionForcePerLevel,
                    movementModifier = shared.m_movementModifier,
                    eitrRegenModifier = shared.m_eitrRegenModifier,
                    damageModifiers = shared.m_damageModifiers == null
                        ? null
                        : new List<HitData.DamageModPair>(shared.m_damageModifiers),

                    consumeStatusEffect = shared.m_consumeStatusEffect,
                    equipStatusEffect = shared.m_equipStatusEffect,
                    attackStatusEffect = shared.m_attackStatusEffect,
                    attackStatusEffectChance = shared.m_attackStatusEffectChance,

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

                shared.m_armor = armor;
                shared.m_armorPerLevel = armorPerLevel;
                shared.m_blockPower = blockPower;
                shared.m_blockPowerPerLevel = blockPowerPerLevel;
                shared.m_deflectionForce = deflectionForce;
                shared.m_deflectionForcePerLevel = deflectionForcePerLevel;
                shared.m_movementModifier = movementModifier;
                shared.m_eitrRegenModifier = eitrRegenModifier;
                shared.m_damageModifiers = damageModifiers == null
                    ? null
                    : new List<HitData.DamageModPair>(damageModifiers);

                shared.m_consumeStatusEffect = consumeStatusEffect;
                shared.m_equipStatusEffect = equipStatusEffect;
                shared.m_attackStatusEffect = attackStatusEffect;
                shared.m_attackStatusEffectChance = attackStatusEffectChance;

                shared.m_setName = setName;
                shared.m_setSize = setSize;
                shared.m_setStatusEffect = setStatusEffect;
            }
        }
    }
}

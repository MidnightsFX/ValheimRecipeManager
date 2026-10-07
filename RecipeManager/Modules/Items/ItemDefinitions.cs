using System.Runtime.Serialization;

namespace RecipeManager.Common
{
    // The item blocks added with the extra item stats. Every field is optional and left out keeps the item's own
    // value, like the rest of an item entry.
    internal partial class DataObjects
    {
        /// <summary>Upgrade levels, and what the Forge of Potential does with the item.</summary>
        [DataContract]
        public class ItemQuality
        {
            /// <summary>The highest upgrade level. 1 means it cannot be upgraded.</summary>
            public int? maxLevel { get; set; }
            /// <summary>Weight added by each upgrade level, as a fraction of the weight.</summary>
            public float? weightPerLevel { get; set; }
            /// <summary>Forge of Potential: chance an upgrade works, 0 to 1.</summary>
            public float? upgradeChance { get; set; }
            /// <summary>Forge of Potential: chance a failed upgrade breaks the item, 0 to 1.</summary>
            public float? breakChance { get; set; }
            /// <summary>Forge of Potential: how many levels a successful upgrade adds.</summary>
            public int? upgradeSteps { get; set; }
            /// <summary>Forge of Potential: how much of the ingredients a broken item gives back, 0 to 1.</summary>
            public float? breakRefund { get; set; }
        }

        [DataContract]
        public class ItemDurability
        {
            /// <summary>Whether the item wears out at all.</summary>
            public bool? enabled { get; set; }
            public float? max { get; set; }
            /// <summary>Durability added by each upgrade level.</summary>
            public float? perLevel { get; set; }
            /// <summary>Durability lost each time it is used.</summary>
            public float? drainPerUse { get; set; }
            /// <summary>Durability lost every second while it is in use, like a torch.</summary>
            public float? drainPerSecond { get; set; }
            public bool? repairable { get; set; }
            /// <summary>For an item that wears out while it is equipped, like a torch: whether it is destroyed when it runs out. Weapons and armor that run out just stop working.</summary>
            public bool? destroyWhenBroken { get; set; }
        }

        /// <summary>Changes to how much stamina things cost while the item is equipped, as fractions. -0.1 is 10% cheaper.</summary>
        [DataContract]
        public class StaminaModifiers
        {
            /// <summary>Using tools and building, like the hammer and hoe.</summary>
            public float? homeItems { get; set; }
            public float? jump { get; set; }
            public float? attack { get; set; }
            public float? block { get; set; }
            public float? dodge { get; set; }
            public float? swim { get; set; }
            public float? sneak { get; set; }
            public float? run { get; set; }
        }

        [DataContract]
        public class AdrenalineStats
        {
            /// <summary>How much adrenaline the wearer can build up.</summary>
            public float? max { get; set; }
            /// <summary>Adrenaline gained from a block.</summary>
            public float? block { get; set; }
            /// <summary>Adrenaline gained from a parry.</summary>
            public float? perfectBlock { get; set; }
        }

        /// <summary>
        /// One of a weapon's two attacks. Only numbers and switches that do not depend on the attack's animation,
        /// so changing one cannot leave a weapon stuck mid-swing.
        /// </summary>
        [DataContract]
        public class AttackStats
        {
            public float? stamina { get; set; }
            public float? eitr { get; set; }
            /// <summary>Health the attack costs, like the blood magic staffs.</summary>
            public float? health { get; set; }
            /// <summary>Health the attack costs, as a percentage of the current health.</summary>
            public float? healthPercentage { get; set; }
            /// <summary>Adrenaline gained for each hit.</summary>
            public float? adrenaline { get; set; }
            /// <summary>Adrenaline gained each time the attack is used, whether it hits or not.</summary>
            public float? useAdrenaline { get; set; }
            /// <summary>Movement speed while attacking, as a fraction of normal.</summary>
            public float? speedFactor { get; set; }
            /// <summary>Turning speed while attacking, as a fraction of normal.</summary>
            public float? rotationSpeedFactor { get; set; }
            public float? damageMultiplier { get; set; }
            public float? forceMultiplier { get; set; }
            public float? staggerMultiplier { get; set; }
            /// <summary>Damage multiplier on the last hit of a combo.</summary>
            public float? lastChainDamageMultiplier { get; set; }
            /// <summary>Extra damage for each point of health the attacker is missing.</summary>
            public float? damageMultiplierPerMissingHealth { get; set; }
            /// <summary>Extra damage when the attacker has no health left, scaling up as health drops.</summary>
            public float? damageMultiplierByHealthMissing { get; set; }
            /// <summary>Stamina given back for each point of health the attacker is missing.</summary>
            public float? staminaReturnPerMissingHealth { get; set; }
            public float? range { get; set; }
            public float? height { get; set; }
            /// <summary>Width of a swing, in degrees.</summary>
            public float? angle { get; set; }
            public float? rayWidth { get; set; }
            /// <summary>Whether a swing can hit more than one target.</summary>
            public bool? multiHit { get; set; }
            /// <summary>Whether damage is shared out between the targets a swing hits.</summary>
            public bool? lowerDamagePerHit { get; set; }
            public bool? hitTerrain { get; set; }
            public bool? hitFriendly { get; set; }
            public bool? hitThroughWalls { get; set; }
            /// <summary>How far the attacker is pushed back.</summary>
            public float? recoilPushback { get; set; }
            public int? selfDamage { get; set; }
            /// <summary>Whether the attack uses up the item, like throwing a bomb.</summary>
            public bool? consumeItem { get; set; }
            public float? raiseSkillAmount { get; set; }
            /// <summary>Noise made when the attack starts, which creatures can hear.</summary>
            public float? startNoise { get; set; }
            /// <summary>Noise made when the attack hits.</summary>
            public float? hitNoise { get; set; }
            /// <summary>A prefab spawned when the attack is used, 'none' for nothing.</summary>
            public string spawnOnTrigger { get; set; }
            /// <summary>A prefab spawned where the attack hits, 'none' for nothing. Only spawned with a spawnOnHitChance above 0.</summary>
            public string spawnOnHit { get; set; }
            public float? spawnOnHitChance { get; set; }
            public ProjectileStats projectile { get; set; }
            /// <summary>Crossbows and other weapons that reload. The game only reads the primary attack's.</summary>
            public ReloadStats reload { get; set; }
            /// <summary>Bows and other weapons that are drawn. The game only reads the primary attack's.</summary>
            public DrawStats draw { get; set; }
        }

        /// <summary>
        /// The projectile an attack fires. On a bow or crossbow the ammo's own numbers are added to these, and the
        /// ammo's projectile is the one fired.
        /// </summary>
        [DataContract]
        public class ProjectileStats
        {
            /// <summary>The projectile prefab fired, 'none' for none.</summary>
            public string prefab { get; set; }
            /// <summary>Velocity when fully drawn, or always for a weapon that is not drawn.</summary>
            public float? velocity { get; set; }
            /// <summary>Velocity when not drawn at all.</summary>
            public float? velocityMin { get; set; }
            /// <summary>Spread in degrees when fully drawn, or at full skill with skillAccuracy. Lower is more accurate.</summary>
            public float? accuracy { get; set; }
            /// <summary>Spread in degrees when not drawn at all, or with no skill with skillAccuracy.</summary>
            public float? accuracyMin { get; set; }
            /// <summary>For a weapon that is not drawn, whether the skill level narrows the spread.</summary>
            public bool? skillAccuracy { get; set; }
            /// <summary>Projectiles fired at once.</summary>
            public int? count { get; set; }
            /// <summary>Times the attack fires in a row.</summary>
            public int? bursts { get; set; }
            /// <summary>Seconds between bursts.</summary>
            public float? burstInterval { get; set; }
            public float? launchAngle { get; set; }
        }

        [DataContract]
        public class ReloadStats
        {
            /// <summary>Seconds a reload takes.</summary>
            public float? time { get; set; }
            public float? staminaDrain { get; set; }
            public float? eitrDrain { get; set; }
        }

        [DataContract]
        public class DrawStats
        {
            /// <summary>Seconds to fully draw with no skill.</summary>
            public float? durationMin { get; set; }
            public float? staminaDrain { get; set; }
            public float? eitrDrain { get; set; }
        }
    }
}

using System.Collections.Generic;
using System.Runtime.Serialization;
using YamlDotNet.Serialization;

namespace RecipeManager.Common
{
    // Everything in StatusEffects.yaml. Every field is optional and left out keeps the effect's own value, or the
    // value of the effect it is cloned from.
    internal partial class DataObjects
    {
        public enum StatusEffectAction
        {
            Add,
            Modify
        }

        [DataContract]
        public class StatusEffectCollection
        {
            public Dictionary<string, StatusEffectDefinition> statusEffects { get; set; } = new Dictionary<string, StatusEffectDefinition>();
        }

        [DataContract]
        public class StatusEffectDefinition
        {
            // Always written, Add is the enum's default and would otherwise be left out of dumps and imports
            [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
            public StatusEffectAction action { get; set; }
            /// <summary>The effect's name, which is what items and other configs refer to it by.</summary>
            public string prefab { get; set; }
            /// <summary>For an Add, an existing effect to copy. Left out, the new effect starts with no stats at all.</summary>
            public string clonedFrom { get; set; }
            /// <summary>The name shown in the game. Start it with '$' to use your own localization token.</summary>
            public string name { get; set; }
            public string tooltip { get; set; }
            /// <summary>Effects that share a category replace each other.</summary>
            public string category { get; set; }
            /// <summary>Seconds the effect lasts. 0 lasts until something removes it.</summary>
            public float? duration { get; set; }
            /// <summary>Seconds before a guardian power can be used again.</summary>
            public float? cooldown { get; set; }
            /// <summary>Kept off the list of effects shown on screen.</summary>
            public bool? hidden { get; set; }
            public bool? flashIcon { get; set; }
            /// <summary>Shows the time left on the icon.</summary>
            public bool? cooldownIcon { get; set; }
            /// <summary>Special effects of the game's own: ColdResistance, DoubleImpactDamage, SailingPower, TamingBoost.</summary>
            public StatusEffect.StatusAttribute? attributes { get; set; }
            /// <summary>The animation played when a guardian power is used.</summary>
            public string activationAnimation { get; set; }
            public StatusEffectMessages messages { get; set; }
            public HealthEffect health { get; set; }
            public StaminaEffect stamina { get; set; }
            public StaminaUseEffect staminaUse { get; set; }
            public EitrEffect eitr { get; set; }
            public AdrenalineEffect adrenaline { get; set; }
            public ArmorEffect armor { get; set; }
            public StaggerEffect stagger { get; set; }
            public DamageEffect damage { get; set; }
            /// <summary>Replaces the effect's whole resistance list, [] clears it.</summary>
            public List<DamageResistance> resistances { get; set; }
            public SkillEffect skills { get; set; }
            public MovementEffect movement { get; set; }
            /// <summary>Change to the noise the player makes, as a fraction.</summary>
            public float? noise { get; set; }
            /// <summary>Change to how hard the player is to spot, as a fraction.</summary>
            public float? stealth { get; set; }
            /// <summary>Weight added to how much the player can carry.</summary>
            public float? carryWeight { get; set; }
            /// <summary>Added to the player's maximum health while the effect lasts. Uses a game patch.</summary>
            public float? maxHealth { get; set; }
            /// <summary>Added to the player's maximum stamina while the effect lasts. Uses a game patch.</summary>
            public float? maxStamina { get; set; }
            /// <summary>Added to the player's maximum eitr while the effect lasts. Uses a game patch.</summary>
            public float? maxEitr { get; set; }
        }

        [DataContract]
        public class StatusEffectMessages
        {
            public string start { get; set; }
            public string stop { get; set; }
            /// <summary>Shown again every repeatInterval seconds while the effect lasts.</summary>
            public string repeat { get; set; }
            public float? repeatInterval { get; set; }
            /// <summary>Where each message is shown: TopLeft or Center.</summary>
            public MessageHud.MessageType? startType { get; set; }
            public MessageHud.MessageType? stopType { get; set; }
            public MessageHud.MessageType? repeatType { get; set; }
        }

        [DataContract]
        public class HealthEffect
        {
            /// <summary>Health given when the effect starts.</summary>
            public float? upFront { get; set; }
            /// <summary>Health given every tickInterval seconds, negative for damage.</summary>
            public float? perTick { get; set; }
            public float? tickInterval { get; set; }
            /// <summary>perTick only happens while health is at or above this fraction of maximum health.</summary>
            public float? minHealthPercentage { get; set; }
            /// <summary>Health given in total over overTimeDuration seconds, in steps of overTimeInterval.</summary>
            public float? overTime { get; set; }
            public float? overTimeDuration { get; set; }
            public float? overTimeInterval { get; set; }
            /// <summary>Health regeneration, as a multiplier. 1 is normal.</summary>
            public float? regenMultiplier { get; set; }
        }

        [DataContract]
        public class StaminaEffect
        {
            public float? upFront { get; set; }
            public float? overTime { get; set; }
            public float? overTimeDuration { get; set; }
            /// <summary>Whether overTime is a fraction of maximum stamina rather than an amount.</summary>
            public bool? overTimeIsFraction { get; set; }
            /// <summary>Stamina lost every second.</summary>
            public float? drainPerSecond { get; set; }
            /// <summary>Stamina regeneration, as a multiplier. 1 is normal.</summary>
            public float? regenMultiplier { get; set; }
            /// <summary>Change to the stamina running drains, as a fraction.</summary>
            public float? runDrain { get; set; }
        }

        /// <summary>Changes to what things cost in stamina, as fractions. -0.1 is 10% cheaper.</summary>
        [DataContract]
        public class StaminaUseEffect
        {
            /// <summary>Using tools and building, like the hammer and hoe.</summary>
            public float? homeItems { get; set; }
            public float? jump { get; set; }
            public float? attack { get; set; }
            public float? block { get; set; }
            /// <summary>Stamina added to or taken off every block, as an amount.</summary>
            public float? blockFlat { get; set; }
            public float? dodge { get; set; }
            public float? swim { get; set; }
            public float? sneak { get; set; }
            public float? run { get; set; }
        }

        [DataContract]
        public class EitrEffect
        {
            public float? upFront { get; set; }
            public float? overTime { get; set; }
            public float? overTimeDuration { get; set; }
            /// <summary>Eitr regeneration, as a multiplier. 1 is normal.</summary>
            public float? regenMultiplier { get; set; }
        }

        [DataContract]
        public class AdrenalineEffect
        {
            public float? upFront { get; set; }
            /// <summary>Change to adrenaline gained, as a fraction.</summary>
            public float? modifier { get; set; }
        }

        [DataContract]
        public class ArmorEffect
        {
            /// <summary>Armor added.</summary>
            public float? add { get; set; }
            /// <summary>Change to armor, as a fraction.</summary>
            public float? multiplier { get; set; }
        }

        [DataContract]
        public class StaggerEffect
        {
            /// <summary>Change to how easily the player is staggered, as a fraction.</summary>
            public float? modifier { get; set; }
            /// <summary>Change to how much more a parry absorbs, as a fraction.</summary>
            public float? timedBlockBonus { get; set; }
        }

        [DataContract]
        public class DamageEffect
        {
            /// <summary>The weapon skill whose attacks multiplier applies to, or All for every attack.</summary>
            public string skill { get; set; }
            /// <summary>Damage of those attacks, as a multiplier. 1 is normal.</summary>
            public float? multiplier { get; set; }
            /// <summary>Extra damage of each type, as fractions. 0.1 is 10% more.</summary>
            public DamageValues percentByType { get; set; }
        }

        [DataContract]
        public class SkillEffect
        {
            /// <summary>A skill that levels faster, and by how much as a multiplier.</summary>
            public string raise { get; set; }
            public float? raiseModifier { get; set; }
            /// <summary>A skill whose level is raised, and by how many levels.</summary>
            public string level { get; set; }
            public float? levelModifier { get; set; }
            public string level2 { get; set; }
            public float? level2Modifier { get; set; }
        }

        [DataContract]
        public class MovementEffect
        {
            /// <summary>Change to movement speed, as a fraction.</summary>
            public float? speed { get; set; }
            public float? swimSpeed { get; set; }
            /// <summary>Changes to the jump, as fractions, sideways, upwards and forwards.</summary>
            public VectorValues jump { get; set; }
            /// <summary>The fastest the player falls, 0 for no limit.</summary>
            public float? maxFallSpeed { get; set; }
            /// <summary>Change to fall damage, as a fraction.</summary>
            public float? fallDamage { get; set; }
            /// <summary>How much the wind helps or slows the player, as a fraction.</summary>
            public float? wind { get; set; }
            public float? windRunStamina { get; set; }
        }

        [DataContract]
        public class VectorValues
        {
            public float? x { get; set; }
            public float? y { get; set; }
            public float? z { get; set; }
        }
    }
}

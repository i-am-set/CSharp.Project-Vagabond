using ProjectVagabond.Combat;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjectVagabond
{
    /// <summary>
    /// A component that defines an entity's core combat capabilities,
    /// such as innate skills and default unarmed attacks.
    /// </summary>
    public class CombatantComponent : IComponent, ICloneableComponent
    {
        /// <summary>
        /// The ID of the "weapon" to be used when the entity has no weapon equipped.
        /// This allows unarmed attacks to be fully data-driven.
        /// E.g., "weapon_unarmed_punch", "weapon_unarmed_claw".
        /// </summary>
        public string DefaultWeaponId { get; set; }

        /// <summary>
        /// A list of ActionData IDs for skills or spells that the entity knows innately,
        /// without needing any equipment.
        /// </summary>
        public List<string> InnateActionIds { get; set; } = new List<string>();

        /// <summary>
        /// The total number of cards in this entity's deck. Used for dynamic deck generation.
        /// </summary>
        public int BaseDeckSize { get; set; } = 0;

        /// <summary>
        /// A dictionary defining the weighted preferences for different magic types during deck generation.
        /// </summary>
        [JsonPropertyName("MagicPreference")]
        public Dictionary<string, int> MagicPreference { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// A list of damage types this entity is vulnerable to, taking increased damage.
        /// </summary>
        public List<DamageType> Weaknesses { get; set; } = new List<DamageType>();

        /// <summary>
        /// A list of damage types this entity is resistant to, taking reduced damage.
        /// </summary>
        public List<DamageType> Resistances { get; set; } = new List<DamageType>();


        public IComponent Clone()
        {
            var clone = (CombatantComponent)this.MemberwiseClone();
            // Create new list/dictionary instances for the clone to avoid shared references.
            clone.InnateActionIds = new List<string>(this.InnateActionIds);
            clone.MagicPreference = new Dictionary<string, int>(this.MagicPreference);
            clone.Weaknesses = new List<DamageType>(this.Weaknesses);
            clone.Resistances = new List<DamageType>(this.Resistances);
            return clone;
        }
    }
}
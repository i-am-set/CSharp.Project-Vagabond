using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Combat.Effects;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Combat.Effects
{
    /// <summary>
    /// A data container that defines a single effect of a combat action,
    /// designed to be deserialized from a JSON file.
    /// </summary>
    public class EffectDefinition
    {
        /// <summary>
        /// The type of effect, used to determine which IActionEffect logic to execute.
        /// E.g., "DealDamage", "Heal", "ApplyStatusEffect".
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// The magnitude of the effect, often expressed in dice notation (e.g., "1d6", "2d4+2").
        /// </summary>
        [JsonPropertyName("amount")]
        public string Amount { get; set; }

        /// <summary>
        /// The damage or effect type, used for calculating resistances and vulnerabilities.
        /// </summary>
        [JsonPropertyName("damageType")]
        public DamageType DamageType { get; set; }

        /// <summary>
        /// The unique identifier for a status effect to be applied.
        /// Used only when the effect Type is "ApplyStatusEffect".
        /// </summary>
        [JsonPropertyName("statusEffectId")]
        public string StatusEffectId { get; set; }
    }
}

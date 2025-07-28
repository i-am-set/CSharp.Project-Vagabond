using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Defines the type of a weapon, affecting how its damage and range are calculated.
    /// </summary>
    public enum WeaponType
    {
        /// <summary>
        /// A close-quarters weapon. Its damage is added to the wielder's base damage.
        /// </summary>
        Melee,
        /// <summary>
        /// A ranged weapon. Its damage completely replaces the wielder's base damage.
        /// </summary>
        Ranged
    }

    /// <summary>
    /// Represents the data for a weapon, typically loaded from a JSON definition.
    /// </summary>
    public class Weapon
    {
        /// <summary>
        /// The unique identifier for this weapon (e.g., "short_sword", "longbow").
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The user-friendly display name of the weapon (e.g., "Short Sword", "Longbow").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The damage dealt by the weapon, expressed in dice notation (e.g., "1d4", "2d6+1").
        /// </summary>
        public string Damage { get; set; }

        /// <summary>
        /// The effective range of the weapon in meters. This value overrides the wielder's base attack range.
        /// </summary>
        public float Range { get; set; }

        /// <summary>
        /// The type of the weapon (Melee or Ranged), which determines combat calculation rules.
        /// </summary>
        public WeaponType Type { get; set; }

        /// <summary>
        /// A list of status effects that this weapon applies on a successful hit.
        /// </summary>
        public List<AvailableAttacksComponent.StatusEffectApplication> StatusEffectsToApply { get; set; } = new List<AvailableAttacksComponent.StatusEffectApplication>();
    }
}
using ProjectVagabond.Combat;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjectVagabond
{
    /// <summary>
    /// Represents the data for a weapon, typically loaded from a JSON definition.
    /// A weapon's primary purpose is to grant one or more actions to its wielder.
    /// </summary>
    public class Weapon
    {
        /// <summary>
        /// The unique identifier for this weapon (e.g., "short_sword", "longbow").
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// The user-friendly display name of the weapon (e.g., "Short Sword", "Longbow").
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// The complete data definition for the weapon's primary attack.
        /// </summary>
        [JsonPropertyName("primaryAttack")]
        public ActionData PrimaryAttack { get; set; }

        /// <summary>
        /// A list of ActionData IDs for special moves that are shuffled into the wielder's deck.
        /// </summary>
        [JsonPropertyName("grantedActionIds")]
        public List<string> GrantedActionIds { get; set; } = new List<string>();
    }
}
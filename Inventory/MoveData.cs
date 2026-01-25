#nullable enable
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Items;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents the static data for a single combat move.
    /// </summary>
    public class MoveData
    {
        /// <summary>
        /// A unique string identifier for the move (e.g., "Tackle", "Fireball").
        /// </summary>
        public string MoveID { get; set; } = "";

        /// <summary>
        /// The display name of the move.
        /// </summary>
        public string MoveName { get; set; } = "";

        /// <summary>
        /// Practical explanation of the move's effects.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Lore or visual description of the move.
        /// </summary>
        public string Flavor { get; set; } = "";

        /// <summary>
        /// A custom phrase to display when this move is used.
        /// Supports tags: {user}, {target}, {user_pronoun}, {item_name}, {IsTargetProperNoun}
        /// </summary>
        public string? ActionPhrase { get; set; }

        /// <summary>
        /// If this move was generated from a weapon, this holds the weapon's name (e.g. "Iron Sword").
        /// Used to populate the {item_name} tag.
        /// </summary>
        public string? SourceItemName { get; set; }

        /// <summary>
        /// The base power of the move, used in damage calculation.
        /// </summary>
        public int Power { get; set; }

        /// <summary>
        /// The amount of mana required to use this move. Defaults to 0 if not specified.
        /// </summary>
        public int ManaCost { get; set; } = 0;

        /// <summary>
        /// The fundamental type of the move, distinguishing magical spells from physical actions.
        /// </summary>
        public MoveType MoveType { get; set; }

        /// <summary>
        /// The type of damage the move inflicts upon impact.
        /// </summary>
        public ImpactType ImpactType { get; set; }

        /// <summary>
        /// The stat used to calculate the move's offensive power.
        /// </summary>
        public OffensiveStatType OffensiveStat { get; set; }

        /// <summary>
        /// A boolean indicating whether the move requires the user to make physical contact with the target.
        /// </summary>
        public bool MakesContact { get; set; }

        /// <summary>
        /// Defines the targeting behavior of the move.
        /// </summary>
        public TargetType Target { get; set; }

        /// <summary>
        /// The base accuracy of the move (1-100). A value of -1 represents a "True Hit" that never misses.
        /// </summary>
        public int Accuracy { get; set; }

        /// <summary>
        /// The move's priority for sorting the action queue. Higher values go first.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// A dictionary of special effects for this move. The key is the effect name (e.g., "Lifesteal")
        /// and the value is a string of its parameters (e.g., "50").
        /// </summary>
        public Dictionary<string, string> Effects { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A list of tags for categorizing the move for reward generation and other systems.
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// The filename of the animation sprite sheet in 'Content/Sprites/MoveAnimationSpriteSheets/'.
        /// </summary>
        public string? AnimationSpriteSheet { get; set; }

        /// <summary>
        /// A multiplier for the animation's playback speed. 1.0 is default (12 FPS).
        /// </summary>
        public float AnimationSpeed { get; set; } = 1.0f;

        /// <summary>
        /// The 0-indexed frame number at which damage and haptics should be applied.
        /// Default is 2 (the 3rd frame).
        /// </summary>
        public int DamageFrameIndex { get; set; } = 2;

        /// <summary>
        /// If true, one animation plays in the center of the screen. If false, an animation plays on each target.
        /// </summary>
        public bool IsAnimationCentralized { get; set; } = false;

        /// <summary>
        /// The list of instantiated ability logic objects derived from the Effects dictionary.
        /// </summary>
        public List<IAbility> Abilities { get; set; } = new List<IAbility>();

        /// <summary>
        /// Creates a shallow copy of the MoveData object.
        /// Note: Abilities are shared references (flyweight pattern) unless they are stateful, 
        /// in which case specific logic would be needed. For now, we assume stateless abilities.
        /// </summary>
        public MoveData Clone()
        {
            var clone = (MoveData)this.MemberwiseClone();
            // Shallow copy the list so we can modify the list structure if needed without affecting the original,
            // but the Ability instances themselves remain shared.
            clone.Abilities = new List<IAbility>(this.Abilities);
            return clone;
        }
    }
}

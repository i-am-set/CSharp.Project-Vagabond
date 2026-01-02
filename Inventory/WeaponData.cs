using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents the static data for a Weapon.
    /// Weapons now define their own move data directly.
    /// </summary>
    public class WeaponData
    {
        public string WeaponID { get; set; }
        public string WeaponName { get; set; }
        public string Description { get; set; }
        public int Rarity { get; set; } = 0;
        public int LevelRequirement { get; set; } = 0;

        // --- Embedded Move Data ---
        public string MoveName { get; set; }
        public int Power { get; set; }
        public int ManaCost { get; set; }
        public MoveType MoveType { get; set; }
        public ImpactType ImpactType { get; set; }
        public OffensiveStatType OffensiveStat { get; set; }
        public bool MakesContact { get; set; }
        public TargetType Target { get; set; }
        public int Accuracy { get; set; }
        public int Priority { get; set; }
        public List<int> OffensiveElementIDs { get; set; } = new List<int>();
        public string? AnimationSpriteSheet { get; set; }
        public float AnimationSpeed { get; set; } = 1.0f;
        public int DamageFrameIndex { get; set; } = 2;
        public bool IsAnimationCentralized { get; set; } = false;

        /// <summary>
        /// Passive effects granted while equipped.
        /// Also used to populate the move's abilities.
        /// </summary>
        public Dictionary<string, string> Effects { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Stat modifiers applied while equipped (e.g., "Strength": 5).
        /// </summary>
        public Dictionary<string, int> StatModifiers { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Converts this weapon's data into a functional MoveData object for combat.
        /// </summary>
        public MoveData ToMoveData()
        {
            var move = new MoveData
            {
                MoveID = $"WEAPON_{WeaponID}", // Synthetic ID
                MoveName = MoveName,
                Description = Description, // Use weapon description
                Power = Power,
                ManaCost = ManaCost,
                MoveType = MoveType,
                ImpactType = ImpactType,
                OffensiveStat = OffensiveStat,
                MakesContact = MakesContact,
                Target = Target,
                Accuracy = Accuracy,
                Priority = Priority,
                OffensiveElementIDs = new List<int>(OffensiveElementIDs),
                AnimationSpriteSheet = AnimationSpriteSheet,
                AnimationSpeed = AnimationSpeed,
                DamageFrameIndex = DamageFrameIndex,
                IsAnimationCentralized = IsAnimationCentralized,
                Effects = new Dictionary<string, string>(Effects),
                Tags = new List<string>(Tags)
            };

            // Hydrate abilities
            // We pass an empty stat dict because weapon stats are applied passively, not during the move execution itself
            move.Abilities = AbilityFactory.CreateAbilitiesFromData(move.Effects, new Dictionary<string, int>());

            return move;
        }
    }
}

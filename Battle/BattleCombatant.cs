using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents a single participant in a battle, holding all their current stats and state.
    /// </summary>
    public class BattleCombatant
    {
        /// <summary>
        /// The original Entity ID from the overworld ECS. Used for retrieving visual information.
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// The archetype ID (e.g., "wanderer") used to create this combatant.
        /// </summary>
        public string ArchetypeId { get; set; }

        /// <summary>
        /// A unique identifier for this instance of a combatant in the battle.
        /// </summary>
        public string CombatantID { get; set; }

        /// <summary>
        /// The display name of the combatant.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// An instance of the CombatantStats class holding all core attributes.
        /// </summary>
        public CombatantStats Stats { get; set; }

        /// <summary>
        /// The health value displayed on screen, which animates towards the logical CurrentHP.
        /// </summary>
        public float VisualHP { get; set; }

        /// <summary>
        /// For the player, this manages their deck, hand, and discard pile. Null for enemies.
        /// </summary>
        public CombatDeckManager DeckManager { get; set; }

        /// <summary>
        /// A list of moves this combatant can use. For enemies, this is a static list.
        /// For the player, this is an adapter property that returns their current hand.
        /// </summary>
        public List<MoveData> AvailableMoves
        {
            get
            {
                if (DeckManager != null)
                {
                    return DeckManager.Hand.Where(m => m != null).ToList();
                }
                return _staticMoves;
            }
        }
        private List<MoveData> _staticMoves = new List<MoveData>();


        /// <summary>
        /// A list of currently active status effects.
        /// </summary>
        public List<StatusEffectInstance> ActiveStatusEffects { get; set; } = new List<StatusEffectInstance>();

        /// <summary>
        /// A list of element IDs associated with this combatant's defensive type.
        /// </summary>
        public List<int> DefensiveElementIDs { get; set; } = new List<int>();

        /// <summary>
        /// True if this combatant is controlled by the player.
        /// </summary>
        public bool IsPlayerControlled { get; set; }

        /// <summary>
        /// Gets a value indicating whether the combatant is defeated (CurrentHP is 0 or less).
        /// </summary>
        public bool IsDefeated => Stats.CurrentHP <= 0;

        /// <summary>
        /// Applies a specified amount of damage to the combatant's CurrentHP.
        /// </summary>
        /// <param name="damageAmount">The amount of damage to apply.</param>
        public void ApplyDamage(int damageAmount)
        {
            Stats.CurrentHP -= damageAmount;
            if (Stats.CurrentHP < 0)
            {
                Stats.CurrentHP = 0;
            }
        }

        /// <summary>
        /// Checks if the combatant currently has a specific status effect.
        /// </summary>
        /// <param name="effectType">The status effect to check for.</param>
        /// <returns>True if the effect is active, otherwise false.</returns>
        public bool HasStatusEffect(StatusEffectType effectType)
        {
            return ActiveStatusEffects.Any(e => e.EffectType == effectType);
        }

        /// <summary>
        /// Adds a new status effect to the combatant, resetting the duration if it already exists.
        /// </summary>
        /// <param name="newEffect">The new status effect instance to add.</param>
        public void AddStatusEffect(StatusEffectInstance newEffect)
        {
            // Remove any existing effect of the same type to reset its duration, as per the design document.
            ActiveStatusEffects.RemoveAll(e => e.EffectType == newEffect.EffectType);
            ActiveStatusEffects.Add(newEffect);
        }

        /// <summary>
        /// For non-player combatants, this sets their static list of moves for the battle.
        /// </summary>
        public void SetStaticMoves(List<MoveData> moves)
        {
            _staticMoves = moves;
        }
    }
}
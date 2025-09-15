using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// A stateless, static system for processing secondary effects of moves.
    /// It maps effect IDs to specific logic and publishes an event upon completion.
    /// </summary>
    public static class SecondaryEffectSystem
    {
        // A dictionary mapping string IDs to their corresponding effect logic.
        // The Action takes the actor and the primary target of the move.
        private static readonly Dictionary<string, Action<BattleCombatant, BattleCombatant>> _effects =
            new Dictionary<string, Action<BattleCombatant, BattleCombatant>>
            {
                { "ApplyDodgeStatus", ApplyDodge }
                // Future effects like "Lifesteal" or "ApplyPoison" would be added here.
            };

        /// <summary>
        /// Processes all secondary effects listed in a move.
        /// </summary>
        /// <param name="action">The queued action containing the move and actor.</param>
        public static void ProcessEffects(QueuedAction action)
        {
            if (action.ChosenMove?.SecondaryEffectIDs != null)
            {
                foreach (var effectId in action.ChosenMove.SecondaryEffectIDs)
                {
                    if (_effects.TryGetValue(effectId, out var effectAction))
                    {
                        effectAction?.Invoke(action.Actor, action.Target);
                    }
                }
            }

            // Since all current effects are instantaneous, we can publish completion immediately.
            // For effects with animations, the animation system would be responsible for publishing this event.
            EventBus.Publish(new GameEvents.SecondaryEffectComplete());
        }

        /// <summary>
        /// The specific logic for the "ApplyDodgeStatus" effect.
        /// </summary>
        private static void ApplyDodge(BattleCombatant actor, BattleCombatant target)
        {
            // The "Dodge" move targets Self, so the actor is the one who gets the status.
            actor.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Dodging, 1));
            // Narration for this could be added here or in a more centralized narration system.
        }
    }
}
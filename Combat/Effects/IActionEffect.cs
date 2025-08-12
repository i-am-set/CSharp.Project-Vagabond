using System.Collections.Generic;

namespace ProjectVagabond.Combat.Effects
{
    /// <summary>
    /// Defines the contract for a class that executes the logic for a specific combat effect.
    /// This is part of the Strategy design pattern for action resolution.
    /// </summary>
    public interface IActionEffect
    {
        /// <summary>
        /// Executes the game logic for this effect.
        /// </summary>
        /// <param name="action">The parent combat action being executed.</param>
        /// <param name="caster">The entity performing the action.</param>
        /// <param name="targets">The list of entities targeted by the action.</param>
        /// <param name="definition">The data defining this specific effect instance.</param>
        void Execute(CombatAction action, CombatEntity caster, List<CombatEntity> targets, EffectDefinition definition);
    }
}
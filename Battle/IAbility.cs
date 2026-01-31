using ProjectVagabond.Battle;

namespace ProjectVagabond.Battle.Abilities
{
    public interface IAbility
    {
        string Name { get; }
        string Description { get; }

        /// <summary>
        /// Determines the execution order of abilities. Higher values execute first.
        /// Defaults to 0.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Responds to a game event within the context of the current battle.
        /// </summary>
        /// <param name="e">The event data.</param>
        /// <param name="context">The battle context (manager, RNG, combatants).</param>
        void OnEvent(GameEvent e, BattleContext context);
    }
}
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
        int Priority => 0;

        void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx);
    }
}
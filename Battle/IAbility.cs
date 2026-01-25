namespace ProjectVagabond.Battle.Abilities
{
    public interface IAbility
    {
        string Name { get; }
        string Description { get; }
        void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx);
    }
}
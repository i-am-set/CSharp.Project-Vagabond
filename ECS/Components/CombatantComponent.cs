namespace ProjectVagabond
{
    public class CombatantComponent : IComponent
    {
        public int AttackPower { get; set; }
        public float AttackRange { get; set; }
        public float AggroRange { get; set; }
    }
}
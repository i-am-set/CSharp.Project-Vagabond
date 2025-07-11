namespace ProjectVagabond
{
    public class CombatantComponent : IComponent, ICloneableComponent
    {
        public int AttackPower { get; set; }
        public float AttackRange { get; set; }
        public float AggroRange { get; set; }

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}

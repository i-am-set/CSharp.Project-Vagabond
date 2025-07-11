using System.Collections.Generic;

namespace ProjectVagabond
{
    public class AvailableAttacksComponent : IComponent, ICloneableComponent
    {
        public class CombatAttack
        {
            public string Name { get; set; }
            public float DamageMultiplier { get; set; }
            public int ActionPointCost { get; set; }
        }

        public List<CombatAttack> Attacks { get; set; } = new List<CombatAttack>();

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}
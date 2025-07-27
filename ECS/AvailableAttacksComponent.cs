using System.Collections.Generic;

namespace ProjectVagabond
{
    public class AvailableAttacksComponent : IComponent, ICloneableComponent
    {
        public class StatusEffectApplication
        {
            public string EffectName { get; set; }
            /// <summary>
            /// The potency of the effect, expressed in dice notation (e.g., "1d6", "5", "2d6+1").
            /// </summary>
            public string Amount { get; set; }
        }

        public class CombatAttack
        {
            public string Name { get; set; }
            public float DamageMultiplier { get; set; } = 1.0f;
            public List<StatusEffectApplication> StatusEffectsToApply { get; set; } = new List<StatusEffectApplication>();
        }

        public List<CombatAttack> Attacks { get; set; } = new List<CombatAttack>();

        public IComponent Clone()
        {
            // A simple memberwise clone is sufficient here as the inner lists and objects
            // are read-only templates after being loaded from JSON.
            return (IComponent)this.MemberwiseClone();
        }
    }
}
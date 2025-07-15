using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class ActiveStatusEffectComponent : IComponent, ICloneableComponent
    {
        public List<ActiveStatusEffect> ActiveEffects { get; set; } = new List<ActiveStatusEffect>();

        public IComponent Clone()
        {
            var clone = (ActiveStatusEffectComponent)this.MemberwiseClone();
            // Create a new list for the clone to ensure it doesn't share the list instance.
            // The ActiveStatusEffect instances themselves are not cloned, which is correct
            // as they are runtime instances and shouldn't be part of the template.
            clone.ActiveEffects = new List<ActiveStatusEffect>();
            return clone;
        }
    }
}
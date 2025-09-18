using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// An ECS component that holds a list of passive ability IDs for an entity.
    /// </summary>
    public class PassiveAbilitiesComponent : IComponent, ICloneableComponent
    {
        public List<string> AbilityIDs { get; set; } = new List<string>();

        public IComponent Clone()
        {
            var clone = (PassiveAbilitiesComponent)this.MemberwiseClone();
            clone.AbilityIDs = new List<string>(this.AbilityIDs);
            return clone;
        }
    }
}
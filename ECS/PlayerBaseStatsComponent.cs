using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// An ECS component that stores the base stats for a new player character.
    /// This data is used once to initialize the PlayerState when a new game begins.
    /// </summary>
    public class PlayerBaseStatsComponent : IComponent, ICloneableComponent
    {
        public int MaxHP { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }
        public List<int> DefensiveElementIDs { get; set; } = new List<int>();
        public List<string> StartingMoveIDs { get; set; } = new List<string>();

        public IComponent Clone()
        {
            var clone = (PlayerBaseStatsComponent)this.MemberwiseClone();
            clone.DefensiveElementIDs = new List<int>(this.DefensiveElementIDs);
            clone.StartingMoveIDs = new List<string>(this.StartingMoveIDs);
            return clone;
        }
    }
}
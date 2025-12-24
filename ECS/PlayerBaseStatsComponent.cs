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
        public int MaxMana { get; set; } = 100;
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }

        public List<int> WeaknessElementIDs { get; set; } = new List<int>();
        public List<int> ResistanceElementIDs { get; set; } = new List<int>();

        public List<string> StartingMoveIDs { get; set; } = new List<string>();
        public string DefaultStrikeMoveID { get; set; }

        public Dictionary<string, int> StartingWeapons { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> StartingArmor { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> StartingRelics { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> StartingConsumables { get; set; } = new Dictionary<string, int>();

        public IComponent Clone()
        {
            var clone = (PlayerBaseStatsComponent)this.MemberwiseClone();
            clone.WeaknessElementIDs = new List<int>(this.WeaknessElementIDs);
            clone.ResistanceElementIDs = new List<int>(this.ResistanceElementIDs);
            clone.StartingMoveIDs = new List<string>(this.StartingMoveIDs);

            clone.StartingWeapons = new Dictionary<string, int>(this.StartingWeapons);
            clone.StartingArmor = new Dictionary<string, int>(this.StartingArmor);
            clone.StartingRelics = new Dictionary<string, int>(this.StartingRelics);
            clone.StartingConsumables = new Dictionary<string, int>(this.StartingConsumables);

            return clone;
        }
    }
}
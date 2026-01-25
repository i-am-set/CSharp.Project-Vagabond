using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ProjectVagabond.Battle
{
    public class CombatantStatsComponent : IComponent, ICloneableComponent
    {
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int MaxMana { get; set; }
        public int CurrentMana { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }

        public List<string> AvailableMoveIDs { get; set; } = new List<string>();

        public IComponent Clone()
        {
            var clone = (CombatantStatsComponent)this.MemberwiseClone();
            clone.AvailableMoveIDs = new List<string>(this.AvailableMoveIDs);
            return clone;
        }
    }
}

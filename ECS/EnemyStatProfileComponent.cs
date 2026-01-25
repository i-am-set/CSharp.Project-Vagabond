using Microsoft.Xna.Framework.Input;
using ProjectVagabond;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    public class EnemyStatProfileComponent : IComponent, ICloneableComponent
    {
        public Gender Gender { get; set; } = Gender.Thing;
        public bool IsProperNoun { get; set; } = false;

        public int MinHP { get; set; }
        public int MaxHP { get; set; }
        public int MaxMana { get; set; } = 100;
        public int MinStrength { get; set; }
        public int MaxStrength { get; set; }
        public int MinIntelligence { get; set; }
        public int MaxIntelligence { get; set; }
        public int MinTenacity { get; set; }
        public int MaxTenacity { get; set; }
        public int MinAgility { get; set; }
        public int MaxAgility { get; set; }

        public List<string> MoveLearnset { get; set; } = new List<string>();
        public int MinNumberOfMoves { get; set; }
        public int MaxNumberOfMoves { get; set; }

        public IComponent Clone()
        {
            var clone = (EnemyStatProfileComponent)this.MemberwiseClone();
            clone.MoveLearnset = new List<string>(this.MoveLearnset);
            return clone;
        }
    }
}

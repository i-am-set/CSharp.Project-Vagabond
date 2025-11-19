using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// An ECS component that holds a list of Relic IDs for an entity (primarily for enemies).
    /// </summary>
    public class PassiveAbilitiesComponent : IComponent, ICloneableComponent
    {
        public List<string> RelicIDs { get; set; } = new List<string>();

        public IComponent Clone()
        {
            var clone = (PassiveAbilitiesComponent)this.MemberwiseClone();
            clone.RelicIDs = new List<string>(this.RelicIDs);
            return clone;
        }
    }
}

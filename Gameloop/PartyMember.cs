using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectVagabond
{
    public class PartyMember
    {
        public string Name { get; set; }

        // Stats
        public int Level { get; set; }
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int MaxMana { get; set; }
        public int CurrentMana { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }

        public List<int> WeaknessElementIDs { get; set; } = new List<int>();
        public List<int> ResistanceElementIDs { get; set; } = new List<int>();

        public string DefaultStrikeMoveID { get; set; }
        public int PortraitIndex { get; set; } = 0;

        // Equipment
        public string? EquippedWeaponId { get; set; }
        public string? EquippedArmorId { get; set; }
        public string? EquippedRelicId { get; set; }

        // Fixed Spell Slots (Max 4)
        // Replaces the old inventory/equip system.
        public MoveEntry?[] Spells { get; set; } = new MoveEntry?[4];

        // Actions (e.g. basic commands, not usually "equipped" to slots)
        public List<MoveEntry> Actions { get; set; } = new List<MoveEntry>();

        public PartyMember() { }

        public PartyMember Clone()
        {
            var clone = (PartyMember)this.MemberwiseClone();
            clone.WeaknessElementIDs = new List<int>(this.WeaknessElementIDs);
            clone.ResistanceElementIDs = new List<int>(this.ResistanceElementIDs);

            // Deep copy the spell slots
            clone.Spells = new MoveEntry?[4];
            for (int i = 0; i < 4; i++)
            {
                if (this.Spells[i] != null)
                {
                    clone.Spells[i] = this.Spells[i]!.Clone();
                }
            }

            clone.Actions = new List<MoveEntry>();
            foreach (var a in this.Actions) clone.Actions.Add(a.Clone());

            return clone;
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond.UI
{
    public class ShopItem
    {
        public string ItemId { get; set; }
        public string DisplayName { get; set; }
        public string Type { get; set; } // "Weapon", "Relic"
        public int Price { get; set; }
        public bool IsSold { get; set; }
        public object DataObject { get; set; } // WeaponData, RelicData
    }
}
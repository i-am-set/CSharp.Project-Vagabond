using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Items;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Systems
{
    public class LootManager
    {
        public LootManager()
        {
        }

        public void BuildLootTables()
        {
            // No loot tables to build anymore
        }

        public List<BaseItem> GenerateCombatLoot()
        {
            // Return empty list as loot is disabled
            return new List<BaseItem>();
        }
    }
}

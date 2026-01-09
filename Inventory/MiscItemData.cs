using ProjectVagabond.Battle;
using ProjectVagabond.Items;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public class MiscItemData
    {
        public string ItemID { get; set; }
        public string ItemName { get; set; }

        /// <summary>
        /// Practical information about the item's effect.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Lore or visual description of the item.
        /// </summary>
        public string Flavor { get; set; }

        public int Rarity { get; set; } = 0;
        public string ImagePath { get; set; } // e.g. "Sprites/Items/Misc/gold_coin"
        public List<string> Tags { get; set; } = new List<string>();
    }
}

using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public class MiscItemData
    {
        public string ItemID { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public int Rarity { get; set; } = 0;
        public string ImagePath { get; set; } // e.g. "Sprites/Items/Misc/gold_coin"
        public List<string> Tags { get; set; } = new List<string>();
    }
}
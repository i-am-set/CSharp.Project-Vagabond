using ProjectVagabond.Battle;
using ProjectVagabond.Items;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents the static data for a single consumable item.
    /// </summary>
    public class ConsumableItemData
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

        public string ImagePath { get; set; }
        public ConsumableType Type { get; set; }
        public TargetType Target { get; set; }
        public int PrimaryValue { get; set; }
        public int? SecondaryValue { get; set; }
        public string MoveID { get; set; }
        public int Priority { get; set; }
    }
}

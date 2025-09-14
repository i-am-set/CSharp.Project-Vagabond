using ProjectVagabond.Battle;
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
        public string Description { get; set; }
        public ConsumableType Type { get; set; }
        public TargetType Target { get; set; }
        public int PrimaryValue { get; set; }
        public int? SecondaryValue { get; set; }
        public string? MoveID { get; set; }
        public int Priority { get; set; }
    }
}

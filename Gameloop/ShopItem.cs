using System;

namespace ProjectVagabond.UI
{
    public class ShopItem
    {
        public string ItemId { get; set; }
        public string DisplayName { get; set; }
        public string Type { get; set; } // "Weapon", "Armor", "Relic", "Consumable"
        public int Price { get; set; }
        public bool IsSold { get; set; }
        public object DataObject { get; set; } // WeaponData, ArmorData, etc.
    }
}
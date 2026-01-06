using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle; // Assuming this is where WeaponData etc are

namespace ProjectVagabond.Items
{
    public enum ItemType { Weapon, Armor, Relic, Consumable }

    /// <summary>
    /// A polymorphic wrapper for any item in the game.
    /// </summary>
    public class BaseItem
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Rarity { get; set; } // 0: Common, 1: Uncommon, 2: Rare, etc.
        public ItemType Type { get; set; }

        // Visuals
        public string SpritePath { get; set; }

        // The actual data object (WeaponData, ArmorData, etc.)
        public object OriginalData { get; set; }

        // Factory methods to convert raw data to BaseItem
        public static BaseItem FromWeapon(WeaponData data)
        {
            return new BaseItem
            {
                ID = data.WeaponID,
                Name = data.WeaponName,
                Description = data.Description,
                Rarity = data.Rarity,
                Type = ItemType.Weapon,
                SpritePath = $"Sprites/Items/Weapons/{data.WeaponID}",
                OriginalData = data
            };
        }

        public static BaseItem FromArmor(ArmorData data)
        {
            return new BaseItem
            {
                ID = data.ArmorID,
                Name = data.ArmorName,
                Description = data.Description,
                Rarity = data.Rarity,
                Type = ItemType.Armor,
                SpritePath = $"Sprites/Items/Armor/{data.ArmorID}",
                OriginalData = data
            };
        }

        public static BaseItem FromRelic(RelicData data)
        {
            return new BaseItem
            {
                ID = data.RelicID,
                Name = data.RelicName,
                Description = data.Description,
                Rarity = data.Rarity,
                Type = ItemType.Relic,
                SpritePath = $"Sprites/Items/Relics/{data.RelicID}",
                OriginalData = data
            };
        }
    }
}

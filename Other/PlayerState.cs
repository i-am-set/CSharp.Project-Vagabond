using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Holds all dynamic, persistent data for the player character.
    /// This acts as the single source of truth for the player's state,
    /// separate from their static archetype definition.
    /// </summary>
    public class PlayerState
    {
        // Persistent Stats
        public int Level { get; set; }
        public int MaxHP { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }
        public List<int> DefensiveElementIDs { get; set; } = new List<int>();
        public string DefaultStrikeMoveID { get; set; }

        /// <summary>
        /// Represents the player's spellbook. The size of the list is the number of
        /// spell pages the player has. A null or empty string indicates an empty page.
        /// </summary>
        public List<string> SpellbookPages { get; set; } = new List<string>();

        /// <summary>
        /// Represents the player's inventory, mapping an ItemID to its quantity.
        /// </summary>
        public Dictionary<string, int> Inventory { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Adds an item to the player's inventory.
        /// </summary>
        public void AddItem(string itemID, int quantity = 1)
        {
            if (Inventory.ContainsKey(itemID))
            {
                Inventory[itemID] += quantity;
            }
            else
            {
                Inventory[itemID] = quantity;
            }
        }

        /// <summary>
        /// Removes an item from the player's inventory.
        /// </summary>
        /// <returns>True if the item was successfully removed, false otherwise.</returns>
        public bool RemoveItem(string itemID, int quantity = 1)
        {
            if (Inventory.TryGetValue(itemID, out int currentQuantity) && currentQuantity >= quantity)
            {
                Inventory[itemID] -= quantity;
                if (Inventory[itemID] <= 0)
                {
                    Inventory.Remove(itemID);
                }
                return true;
            }
            return false;
        }
    }
}
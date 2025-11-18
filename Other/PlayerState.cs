using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

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
        public int MaxMana { get; set; } = 100;
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }
        public List<int> DefensiveElementIDs { get; set; } = new List<int>();
        public string DefaultStrikeMoveID { get; set; }

        // --- Inventories ---
        public Dictionary<string, int> WeaponsInventory { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ArmorsInventory { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> RelicInventory { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ConsumableInventory { get; set; } = new Dictionary<string, int>();

        // --- Spell Management ---
        /// <summary>
        /// Represents the player's spellbook. The size of the list is the number of
        /// spell pages the player has. A null entry indicates an empty page.
        /// </summary>
        public List<SpellbookEntry?> SpellbookPages { get; set; } = new List<SpellbookEntry?>();

        /// <summary>
        /// Represents the player's 4-slot combat loadout. Holds references to entries in the SpellbookPages.
        /// </summary>
        public SpellbookEntry?[] EquippedSpells { get; set; } = new SpellbookEntry?[4];

        // --- Equipment ---
        public string? EquippedWeaponId { get; set; }
        public string? EquippedArmorId { get; set; }
        public string?[] EquippedRelics { get; set; } = new string?[3];


        #region Inventory Management
        private void AddItemToInventory(Dictionary<string, int> inventory, string itemId, int quantity)
        {
            if (inventory.ContainsKey(itemId))
            {
                inventory[itemId] += quantity;
            }
            else
            {
                inventory[itemId] = quantity;
            }
        }

        private bool RemoveItemFromInventory(Dictionary<string, int> inventory, string itemId, int quantity)
        {
            if (inventory.TryGetValue(itemId, out int currentQuantity))
            {
                if (currentQuantity <= quantity)
                {
                    inventory.Remove(itemId);
                }
                else
                {
                    inventory[itemId] -= quantity;
                }
                return true;
            }
            return false;
        }

        public void AddWeapon(string weaponId, int quantity = 1) => AddItemToInventory(WeaponsInventory, weaponId, quantity);
        public bool RemoveWeapon(string weaponId, int quantity = 1) => RemoveItemFromInventory(WeaponsInventory, weaponId, quantity);

        public void AddArmor(string armorId, int quantity = 1) => AddItemToInventory(ArmorsInventory, armorId, quantity);
        public bool RemoveArmor(string armorId, int quantity = 1) => RemoveItemFromInventory(ArmorsInventory, armorId, quantity);

        public void AddRelic(string relicId, int quantity = 1) => AddItemToInventory(RelicInventory, relicId, quantity);
        public bool RemoveRelic(string relicId, int quantity = 1) => RemoveItemFromInventory(RelicInventory, relicId, quantity);

        public void AddConsumable(string consumableId, int quantity = 1) => AddItemToInventory(ConsumableInventory, consumableId, quantity);
        public bool RemoveConsumable(string consumableId, int quantity = 1) => RemoveItemFromInventory(ConsumableInventory, consumableId, quantity);

        #endregion
    }
}

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
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond
{
    /// <summary>
    /// Holds all dynamic, persistent data for the player character.
    /// This acts as the single source of truth for the player's state.
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
        // --- INVENTORIES ---
        // String ID -> Quantity
        public Dictionary<string, int> Weapons { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Armors { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Relics { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Consumables { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> MiscItems { get; set; } = new Dictionary<string, int>(); // <--- Added

        // --- MOVES ---
        /// <summary>
        /// Stores infinite list of acquired Spells.
        /// </summary>
        public List<MoveEntry> Spells { get; set; } = new List<MoveEntry>();

        /// <summary>
        /// Stores infinite list of acquired Actions.
        /// </summary>
        public List<MoveEntry> Actions { get; set; } = new List<MoveEntry>();

        // --- EQUIPMENT ---
        public string? EquippedWeaponId { get; set; }
        public string? EquippedArmorId { get; set; }

        /// <summary>
        /// The player's 3 active relic slots. Only these provide passive abilities in combat.
        /// </summary>
        public string?[] EquippedRelics { get; set; } = new string?[3];

        /// <summary>
        /// The player's 4 active combat move slots (Spells only).
        /// </summary>
        public MoveEntry?[] EquippedSpells { get; set; } = new MoveEntry?[4];

        /// <summary>
        /// Helper to get the raw base stat without modifiers.
        /// </summary>
        public int GetBaseStat(string statName)
        {
            switch (statName.ToLowerInvariant())
            {
                case "strength": return Strength;
                case "intelligence": return Intelligence;
                case "tenacity": return Tenacity;
                case "agility": return Agility;
                case "maxhp": return MaxHP;
                case "maxmana": return MaxMana;
                case "level": return Level;
                default: return 0;
            }
        }

        /// <summary>
        /// Calculates the effective value of a stat by adding bonuses from equipped relics, weapons, AND armor to the base value.
        /// Enforces a minimum value of 1.
        /// </summary>
        /// <param name="statName">The name of the stat (e.g., "Strength", "MaxHP"). Case-insensitive.</param>
        /// <returns>The total effective value.</returns>
        public int GetEffectiveStat(string statName)
        {
            int baseValue = GetBaseStat(statName);
            int bonus = 0;

            // 1. Relic Bonuses
            foreach (var relicId in EquippedRelics)
            {
                if (!string.IsNullOrEmpty(relicId) && BattleDataCache.Relics.TryGetValue(relicId, out var relic))
                {
                    if (relic.StatModifiers.TryGetValue(statName, out int mod))
                    {
                        bonus += mod;
                    }
                }
            }

            // 2. Weapon Bonuses
            if (!string.IsNullOrEmpty(EquippedWeaponId) && BattleDataCache.Weapons.TryGetValue(EquippedWeaponId, out var weapon))
            {
                if (weapon.StatModifiers.TryGetValue(statName, out int mod))
                {
                    bonus += mod;
                }
            }

            // 3. Armor Bonuses
            if (!string.IsNullOrEmpty(EquippedArmorId) && BattleDataCache.Armors.TryGetValue(EquippedArmorId, out var armor))
            {
                if (armor.StatModifiers.TryGetValue(statName, out int mod))
                {
                    bonus += mod;
                }
            }

            // Ensure stats don't drop below 1
            return Math.Max(1, baseValue + bonus);
        }

        // --- WEAPON MANAGEMENT ---
        public void AddWeapon(string weaponId, int quantity = 1)
        {
            if (Weapons.ContainsKey(weaponId)) Weapons[weaponId] += quantity;
            else Weapons[weaponId] = quantity;
        }

        public void RemoveWeapon(string weaponId, int quantity = 1)
        {
            if (Weapons.TryGetValue(weaponId, out int current))
            {
                Weapons[weaponId] = Math.Max(0, current - quantity);
                if (Weapons[weaponId] == 0) Weapons.Remove(weaponId);

                if (EquippedWeaponId == weaponId && Weapons.GetValueOrDefault(weaponId) == 0)
                    EquippedWeaponId = null;
            }
        }

        // --- ARMOR MANAGEMENT ---
        public void AddArmor(string armorId, int quantity = 1)
        {
            if (Armors.ContainsKey(armorId)) Armors[armorId] += quantity;
            else Armors[armorId] = quantity;
        }

        public void RemoveArmor(string armorId, int quantity = 1)
        {
            if (Armors.TryGetValue(armorId, out int current))
            {
                Armors[armorId] = Math.Max(0, current - quantity);
                if (Armors[armorId] == 0) Armors.Remove(armorId);

                if (EquippedArmorId == armorId && Armors.GetValueOrDefault(armorId) == 0)
                    EquippedArmorId = null;
            }
        }

        // --- RELIC MANAGEMENT ---
        public void AddRelic(string relicId, int quantity = 1)
        {
            if (Relics.ContainsKey(relicId)) Relics[relicId] += quantity;
            else Relics[relicId] = quantity;
        }

        public void RemoveRelic(string relicId, int quantity = 1)
        {
            if (Relics.TryGetValue(relicId, out int current))
            {
                Relics[relicId] = Math.Max(0, current - quantity);
                if (Relics[relicId] == 0) Relics.Remove(relicId);

                if (!Relics.ContainsKey(relicId))
                {
                    for (int i = 0; i < EquippedRelics.Length; i++)
                    {
                        if (EquippedRelics[i] == relicId) EquippedRelics[i] = null;
                    }
                }
            }
        }

        // --- CONSUMABLE MANAGEMENT ---
        public void AddConsumable(string itemId, int quantity = 1)
        {
            if (Consumables.ContainsKey(itemId)) Consumables[itemId] += quantity;
            else Consumables[itemId] = quantity;
        }

        public bool RemoveConsumable(string itemId, int quantity = 1)
        {
            if (Consumables.TryGetValue(itemId, out int current) && current >= quantity)
            {
                Consumables[itemId] -= quantity;
                if (Consumables[itemId] <= 0) Consumables.Remove(itemId);
                return true;
            }
            return false;
        }

        // --- MISC MANAGEMENT ---
        public void AddMiscItem(string itemId, int quantity = 1)
        {
            if (MiscItems.ContainsKey(itemId)) MiscItems[itemId] += quantity;
            else MiscItems[itemId] = quantity;
        }

        public bool RemoveMiscItem(string itemId, int quantity = 1)
        {
            if (MiscItems.TryGetValue(itemId, out int current) && current >= quantity)
            {
                MiscItems[itemId] -= quantity;
                if (MiscItems[itemId] <= 0) MiscItems.Remove(itemId);
                return true;
            }
            return false;
        }

        // --- MOVE MANAGEMENT ---
        public void AddMove(string moveId)
        {
            if (!BattleDataCache.Moves.TryGetValue(moveId, out var moveData)) return;

            if (moveData.MoveType == MoveType.Spell)
            {
                if (!Spells.Any(m => m.MoveID == moveId))
                {
                    Spells.Add(new MoveEntry(moveId, 0));
                }
            }
            else if (moveData.MoveType == MoveType.Action)
            {
                if (!Actions.Any(m => m.MoveID == moveId))
                {
                    Actions.Add(new MoveEntry(moveId, 0));
                }
            }
        }

        public void RemoveMove(string moveId)
        {
            // Try removing from Spells
            var spellIndex = Spells.FindIndex(m => m.MoveID == moveId);
            if (spellIndex != -1)
            {
                var entry = Spells[spellIndex];
                Spells.RemoveAt(spellIndex);

                // Unequip if equipped
                for (int i = 0; i < EquippedSpells.Length; i++)
                {
                    if (EquippedSpells[i] == entry) EquippedSpells[i] = null;
                }
                return;
            }

            // Try removing from Actions
            var actionIndex = Actions.FindIndex(m => m.MoveID == moveId);
            if (actionIndex != -1)
            {
                Actions.RemoveAt(actionIndex);
            }
        }
    }
}

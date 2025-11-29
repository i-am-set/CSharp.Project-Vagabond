using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public partial class SplitMapInventoryOverlay
    {
        private RelicData? GetRelicData(string relicId)
        {
            if (BattleDataCache.Relics.TryGetValue(relicId, out var data)) return data;
            return null;
        }

        private WeaponData? GetWeaponData(string weaponId)
        {
            if (BattleDataCache.Weapons.TryGetValue(weaponId, out var data)) return data;
            return null;
        }

        private ArmorData? GetArmorData(string armorId)
        {
            if (BattleDataCache.Armors.TryGetValue(armorId, out var data)) return data;
            return null;
        }

        private void RefreshInventorySlots()
        {
            foreach (var slot in _inventorySlots) slot.Clear();

            var items = GetCurrentCategoryItems();

            int totalItems = items.Count;
            _totalPages = (int)Math.Ceiling((double)totalItems / ITEMS_PER_PAGE);

            int startIndex = _currentPage * ITEMS_PER_PAGE;
            int itemsToDisplay = Math.Min(ITEMS_PER_PAGE, items.Count - startIndex);

            for (int i = 0; i < itemsToDisplay; i++)
            {
                var item = items[startIndex + i];
                _inventorySlots[i].AssignItem(item.Name, item.Quantity, item.IconPath, item.Rarity, item.IconTint, item.IsAnimated, item.FallbackIconPath, item.IsEquipped);

                if (_selectedSlotIndex == i)
                {
                    _inventorySlots[i].IsSelected = true;
                }
            }
        }

        private List<(string Name, int Quantity, string? IconPath, int? Uses, int Rarity, Color? IconTint, bool IsAnimated, string? FallbackIconPath, bool IsEquipped)> GetCurrentCategoryItems()
        {
            var currentItems = new List<(string Name, int Quantity, string? IconPath, int? Uses, int Rarity, Color? IconTint, bool IsAnimated, string? FallbackIconPath, bool IsEquipped)>();
            switch (_selectedInventoryCategory)
            {
                case InventoryCategory.Weapons:
                    foreach (var kvp in _gameState.PlayerState.Weapons)
                    {
                        bool isEquipped = kvp.Key == _gameState.PlayerState.EquippedWeaponId;
                        if (BattleDataCache.Weapons.TryGetValue(kvp.Key, out var weaponData))
                        {
                            currentItems.Add((weaponData.WeaponName, kvp.Value, $"Sprites/Items/Weapons/{kvp.Key}", null, weaponData.Rarity, null, false, null, isEquipped));
                        }
                        else
                        {
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Weapons/{kvp.Key}", null, 0, null, false, null, isEquipped));
                        }
                    }
                    break;
                case InventoryCategory.Armor:
                    foreach (var kvp in _gameState.PlayerState.Armors)
                    {
                        bool isEquipped = kvp.Key == _gameState.PlayerState.EquippedArmorId;
                        if (BattleDataCache.Armors.TryGetValue(kvp.Key, out var armorData))
                        {
                            currentItems.Add((armorData.ArmorName, kvp.Value, $"Sprites/Items/Armor/{kvp.Key}", null, armorData.Rarity, null, false, null, isEquipped));
                        }
                        else
                        {
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Armor/{kvp.Key}", null, 0, null, false, null, isEquipped));
                        }
                    }
                    break;
                case InventoryCategory.Relics:
                    foreach (var kvp in _gameState.PlayerState.Relics)
                    {
                        bool isEquipped = _gameState.PlayerState.EquippedRelics.Contains(kvp.Key);
                        if (BattleDataCache.Relics.TryGetValue(kvp.Key, out var data))
                            currentItems.Add((data.RelicName, kvp.Value, $"Sprites/Items/Relics/{data.RelicID}", null, data.Rarity, null, false, null, isEquipped));
                        else
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Relics/{kvp.Key}", null, 0, null, false, null, isEquipped));
                    }
                    break;
                case InventoryCategory.Consumables:
                    foreach (var kvp in _gameState.PlayerState.Consumables)
                    {
                        if (BattleDataCache.Consumables.TryGetValue(kvp.Key, out var data))
                            currentItems.Add((data.ItemName, kvp.Value, data.ImagePath, null, 0, null, false, null, false)); // Consumables default to 0 rarity, never equipped
                        else
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Consumables/{kvp.Key}", null, 0, null, false, null, false));
                    }
                    break;
                case InventoryCategory.Spells:
                    foreach (var entry in _gameState.PlayerState.Spells)
                    {
                        Color? tint = null;
                        string name = entry.MoveID;
                        string iconPath = $"Sprites/Items/Spells/{entry.MoveID}";
                        int rarity = 0;
                        string? fallbackPath = null;
                        bool isEquipped = _gameState.PlayerState.EquippedSpells.Contains(entry);

                        if (BattleDataCache.Moves.TryGetValue(entry.MoveID, out var moveData))
                        {
                            name = moveData.MoveName;
                            rarity = moveData.Rarity;

                            int elementId = moveData.OffensiveElementIDs.FirstOrDefault();
                            if (BattleDataCache.Elements.TryGetValue(elementId, out var elementDef))
                            {
                                string elName = elementDef.ElementName.ToLowerInvariant();
                                if (elName == "---") elName = "neutral";
                                fallbackPath = $"Sprites/Items/Spells/default_{elName}";
                            }
                        }
                        currentItems.Add((name, 1, iconPath, null, rarity, tint, true, fallbackPath, isEquipped));
                    }
                    break;
            }
            return currentItems;
        }

        private (List<string> Positives, List<string> Negatives) GetStatModifierLines(Dictionary<string, int> mods)
        {
            var positives = new List<string>();
            var negatives = new List<string>();
            if (mods == null || mods.Count == 0) return (positives, negatives);

            foreach (var kvp in mods)
            {
                if (kvp.Value == 0) continue;
                string colorTag = kvp.Value > 0 ? "[cpositive]" : "[cnegative]";
                string sign = kvp.Value > 0 ? "+" : "";

                // Map full names to abbreviations
                string statName = kvp.Key.ToLowerInvariant() switch
                {
                    "strength" => "STR",
                    "intelligence" => "INT",
                    "tenacity" => "TEN",
                    "agility" => "AGI",
                    "maxhp" => "HP",
                    "maxmana" => "MP",
                    _ => kvp.Key.ToUpper().Substring(0, Math.Min(3, kvp.Key.Length)) // Fallback
                };

                // Pad short names to 3 characters
                if (statName.Length < 3)
                {
                    statName += " ";
                }

                string line = $"{statName} {colorTag}{sign}{kvp.Value}[/]";

                if (kvp.Value > 0)
                {
                    positives.Add(line);
                }
                else
                {
                    negatives.Add(line);
                }
            }
            return (positives, negatives);
        }
    }
}
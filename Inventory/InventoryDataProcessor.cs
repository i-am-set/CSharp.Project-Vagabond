using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class InventoryDataProcessor
    {
        private readonly SplitMapInventoryOverlay _overlay;

        public InventoryDataProcessor(SplitMapInventoryOverlay overlay)
        {
            _overlay = overlay;
        }

        public RelicData? GetRelicData(string relicId)
        {
            if (BattleDataCache.Relics.TryGetValue(relicId, out var data)) return data;
            return null;
        }

        public WeaponData? GetWeaponData(string weaponId)
        {
            if (BattleDataCache.Weapons.TryGetValue(weaponId, out var data)) return data;
            return null;
        }

        public ArmorData? GetArmorData(string armorId)
        {
            if (BattleDataCache.Armors.TryGetValue(armorId, out var data)) return data;
            return null;
        }

        public void RefreshInventorySlots()
        {
            foreach (var slot in _overlay.InventorySlots) slot.Clear();

            var items = GetCurrentCategoryItems();

            int totalItems = items.Count;
            _overlay.TotalPages = (int)Math.Ceiling((double)totalItems / SplitMapInventoryOverlay.ITEMS_PER_PAGE);

            int startIndex = _overlay.CurrentPage * SplitMapInventoryOverlay.ITEMS_PER_PAGE;
            int itemsToDisplay = Math.Min(SplitMapInventoryOverlay.ITEMS_PER_PAGE, items.Count - startIndex);

            for (int i = 0; i < itemsToDisplay; i++)
            {
                var item = items[startIndex + i];
                _overlay.InventorySlots[i].AssignItem(item.Name, item.Quantity, item.IconPath, item.Rarity, item.IconTint, item.IsAnimated, item.FallbackIconPath, item.IsEquipped);

                if (_overlay.SelectedSlotIndex == i)
                {
                    _overlay.InventorySlots[i].IsSelected = true;
                }
            }
        }

        public List<(string Name, int Quantity, string? IconPath, int? Uses, int Rarity, Color? IconTint, bool IsAnimated, string? FallbackIconPath, bool IsEquipped)> GetCurrentCategoryItems()
        {
            var currentItems = new List<(string Name, int Quantity, string? IconPath, int? Uses, int Rarity, Color? IconTint, bool IsAnimated, string? FallbackIconPath, bool IsEquipped)>();
            var playerState = _overlay.GameState.PlayerState;

            switch (_overlay.SelectedInventoryCategory)
            {
                case InventoryCategory.Weapons:
                    foreach (var kvp in playerState.Weapons)
                    {
                        bool isEquipped = playerState.Party.Any(m => m.EquippedWeaponId == kvp.Key);
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
                    foreach (var kvp in playerState.Armors)
                    {
                        bool isEquipped = playerState.Party.Any(m => m.EquippedArmorId == kvp.Key);
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
                    foreach (var kvp in playerState.Relics)
                    {
                        bool isEquipped = playerState.Party.Any(m => m.EquippedRelicId == kvp.Key);
                        if (BattleDataCache.Relics.TryGetValue(kvp.Key, out var data))
                            currentItems.Add((data.RelicName, kvp.Value, $"Sprites/Items/Relics/{data.RelicID}", null, data.Rarity, null, false, null, isEquipped));
                        else
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Relics/{kvp.Key}", null, 0, null, false, null, isEquipped));
                    }
                    break;
                case InventoryCategory.Consumables:
                    foreach (var kvp in playerState.Consumables)
                    {
                        if (BattleDataCache.Consumables.TryGetValue(kvp.Key, out var data))
                            currentItems.Add((data.ItemName, kvp.Value, $"Sprites/Items/Consumables/{data.ItemID}", null, 0, null, false, null, false));
                        else
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Consumables/{kvp.Key}", null, 0, null, false, null, false));
                    }
                    break;
                case InventoryCategory.Misc:
                    foreach (var kvp in playerState.MiscItems)
                    {
                        if (BattleDataCache.MiscItems.TryGetValue(kvp.Key, out var data))
                            currentItems.Add((data.ItemName, kvp.Value, data.ImagePath, null, data.Rarity, null, false, null, false));
                        else
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Misc/{kvp.Key}", null, 0, null, false, null, false));
                    }
                    break;
            }
            return currentItems;
        }
    }
}

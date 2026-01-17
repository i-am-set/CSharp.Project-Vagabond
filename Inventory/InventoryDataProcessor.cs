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
                _overlay.InventorySlots[i].AssignItem(item.Name, item.Quantity, item.IconPath, item.IconTint, item.IsAnimated, item.FallbackIconPath, item.IsEquipped);

                if (_overlay.SelectedSlotIndex == i)
                {
                    _overlay.InventorySlots[i].IsSelected = true;
                }
            }
        }

        public List<(string Name, int Quantity, string? IconPath, int? Uses, Color? IconTint, bool IsAnimated, string? FallbackIconPath, bool IsEquipped)> GetCurrentCategoryItems()
        {
            var currentItems = new List<(string Name, int Quantity, string? IconPath, int? Uses, Color? IconTint, bool IsAnimated, string? FallbackIconPath, bool IsEquipped)>();
            var playerState = _overlay.GameState.PlayerState;

            switch (_overlay.SelectedInventoryCategory)
            {
                case InventoryCategory.Weapons:
                    foreach (var kvp in playerState.Weapons)
                    {
                        bool isEquipped = playerState.Party.Any(m => m.EquippedWeaponId == kvp.Key);
                        if (BattleDataCache.Weapons.TryGetValue(kvp.Key, out var weaponData))
                        {
                            currentItems.Add((weaponData.WeaponName, kvp.Value, $"Sprites/Items/Weapons/{kvp.Key}", null, null, false, null, isEquipped));
                        }
                        else
                        {
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Weapons/{kvp.Key}", null, null, false, null, isEquipped));
                        }
                    }
                    break;
                case InventoryCategory.Relics:
                    foreach (var kvp in playerState.Relics)
                    {
                        bool isEquipped = playerState.Party.Any(m => m.EquippedRelicId == kvp.Key);
                        if (BattleDataCache.Relics.TryGetValue(kvp.Key, out var data))
                            currentItems.Add((data.RelicName, kvp.Value, $"Sprites/Items/Relics/{data.RelicID}", null, null, false, null, isEquipped));
                        else
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Relics/{kvp.Key}", null, null, false, null, isEquipped));
                    }
                    break;
            }
            return currentItems;
        }
    }
}
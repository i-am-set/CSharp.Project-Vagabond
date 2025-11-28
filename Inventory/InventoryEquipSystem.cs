#nullable enable
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
        private void OpenEquipSubmenu(EquipSlotType slotType)
        {
            _isEquipSubmenuOpen = true;
            _activeEquipSlotType = slotType;
            _equipMenuScrollIndex = 0;
            RefreshEquipSubmenuButtons();
        }

        private void RefreshEquipSubmenuButtons()
        {
            List<string> availableItems = new List<string>();

            if (_activeEquipSlotType == EquipSlotType.Weapon)
            {
                availableItems = _gameState.PlayerState.Weapons.Keys.ToList();
            }
            else if (_activeEquipSlotType == EquipSlotType.Armor)
            {
                availableItems = _gameState.PlayerState.Armors.Keys.ToList();
            }
            else if (_activeEquipSlotType == EquipSlotType.Relic1 || _activeEquipSlotType == EquipSlotType.Relic2 || _activeEquipSlotType == EquipSlotType.Relic3)
            {
                // Get all owned relics
                var allRelics = _gameState.PlayerState.Relics.Keys.ToList();

                // Get set of currently equipped relics to filter them out
                var equippedRelics = new HashSet<string>(_gameState.PlayerState.EquippedRelics.Where(r => !string.IsNullOrEmpty(r)));

                // Filter out any relic that is currently equipped in ANY slot
                availableItems = allRelics.Where(r => !equippedRelics.Contains(r)).ToList();
            }

            int totalItems = 1 + availableItems.Count; // +1 for REMOVE

            for (int i = 0; i < _equipSubmenuButtons.Count; i++)
            {
                var btn = _equipSubmenuButtons[i];
                int virtualIndex = _equipMenuScrollIndex + i;

                btn.IsEnabled = false;
                btn.MainText = "";
                btn.IconTexture = null;
                btn.IconSilhouette = null;
                btn.OnClick = null;
                btn.Rarity = -1; // Reset rarity

                if (i % 2 == 0)
                {
                    btn.CustomDefaultTextColor = _global.Palette_BrightWhite;
                    btn.CustomTitleTextColor = _global.Palette_DarkGray;
                }
                else
                {
                    btn.CustomDefaultTextColor = _global.Palette_White;
                    btn.CustomTitleTextColor = _global.Palette_DarkerGray;
                }

                if (virtualIndex == 0)
                {
                    btn.MainText = "REMOVE";
                    btn.TitleText = "SELECT";
                    btn.CustomDefaultTextColor = _global.Palette_Red;
                    btn.IconTexture = _spriteManager.InventoryEmptySlotSprite;
                    btn.IconSilhouette = null;
                    btn.IsEnabled = true;
                    btn.OnClick = () => SelectEquipItem(null);
                }
                else if (virtualIndex < totalItems)
                {
                    btn.TitleText = "SELECT";
                    int itemIndex = virtualIndex - 1;
                    string itemId = availableItems[itemIndex];

                    if (_activeEquipSlotType == EquipSlotType.Weapon)
                    {
                        var weaponData = GetWeaponData(itemId);
                        if (weaponData != null)
                        {
                            btn.MainText = weaponData.WeaponName.ToUpper();
                            string path = $"Sprites/Items/Weapons/{weaponData.WeaponID}";
                            btn.IconTexture = _spriteManager.GetSmallRelicSprite(path);
                            btn.IconSilhouette = _spriteManager.GetSmallRelicSpriteSilhouette(path);
                            btn.IconSourceRect = null;
                            btn.Rarity = weaponData.Rarity;
                            btn.IsEnabled = true;
                            btn.OnClick = () => SelectEquipItem(itemId);
                        }
                        else
                        {
                            btn.MainText = itemId.ToUpper();
                            btn.IsEnabled = true;
                            btn.OnClick = () => SelectEquipItem(itemId);
                        }
                    }
                    else if (_activeEquipSlotType == EquipSlotType.Armor)
                    {
                        var armorData = GetArmorData(itemId);
                        if (armorData != null)
                        {
                            btn.MainText = armorData.ArmorName.ToUpper();
                            string path = $"Sprites/Items/Armor/{armorData.ArmorID}";
                            btn.IconTexture = _spriteManager.GetSmallRelicSprite(path);
                            btn.IconSilhouette = _spriteManager.GetSmallRelicSpriteSilhouette(path);
                            btn.IconSourceRect = null;
                            btn.Rarity = armorData.Rarity;
                            btn.IsEnabled = true;
                            btn.OnClick = () => SelectEquipItem(itemId);
                        }
                        else
                        {
                            btn.MainText = itemId.ToUpper();
                            btn.IsEnabled = true;
                            btn.OnClick = () => SelectEquipItem(itemId);
                        }
                    }
                    else // Relic
                    {
                        var relicData = GetRelicData(itemId);
                        if (relicData != null)
                        {
                            btn.MainText = relicData.RelicName.ToUpper();
                            string path = $"Sprites/Items/Relics/{relicData.RelicID}";
                            btn.IconTexture = _spriteManager.GetSmallRelicSprite(path);
                            btn.IconSilhouette = _spriteManager.GetSmallRelicSpriteSilhouette(path);
                            btn.IconSourceRect = null;
                            btn.Rarity = relicData.Rarity;
                            btn.IsEnabled = true;
                            btn.OnClick = () => SelectEquipItem(itemId);
                        }
                        else
                        {
                            btn.MainText = itemId.ToUpper();
                            btn.IsEnabled = true;
                            btn.OnClick = () => SelectEquipItem(itemId);
                        }
                    }
                }
                else
                {
                    // Empty slot
                    btn.TitleText = "";
                }
            }
        }

        private void CancelEquipSelection()
        {
            if (_isEquipSubmenuOpen)
            {
                _isEquipSubmenuOpen = false;
                _activeEquipSlotType = EquipSlotType.None;
                _hoveredItemData = null;
            }
        }

        private void SelectEquipItem(string? itemId)
        {
            if (_activeEquipSlotType == EquipSlotType.Weapon)
            {
                _gameState.PlayerState.EquippedWeaponId = itemId;
            }
            else if (_activeEquipSlotType == EquipSlotType.Armor)
            {
                _gameState.PlayerState.EquippedArmorId = itemId;
            }
            else if (_activeEquipSlotType == EquipSlotType.Relic1)
            {
                _gameState.PlayerState.EquippedRelics[0] = itemId;
            }
            else if (_activeEquipSlotType == EquipSlotType.Relic2)
            {
                _gameState.PlayerState.EquippedRelics[1] = itemId;
            }
            else if (_activeEquipSlotType == EquipSlotType.Relic3)
            {
                _gameState.PlayerState.EquippedRelics[2] = itemId;
            }

            _isEquipSubmenuOpen = false;
            _activeEquipSlotType = EquipSlotType.None;
            _hoveredItemData = null;

            // Refresh the main equip buttons to show the new item
            UpdateEquipButtonState(_weaponEquipButton!, _gameState.PlayerState.EquippedWeaponId, EquipSlotType.Weapon);
            UpdateEquipButtonState(_armorEquipButton!, _gameState.PlayerState.EquippedArmorId, EquipSlotType.Armor);
            UpdateEquipButtonState(_relicEquipButton1!, _gameState.PlayerState.EquippedRelics[0], EquipSlotType.Relic1);
            UpdateEquipButtonState(_relicEquipButton2!, _gameState.PlayerState.EquippedRelics[1], EquipSlotType.Relic2);
            UpdateEquipButtonState(_relicEquipButton3!, _gameState.PlayerState.EquippedRelics[2], EquipSlotType.Relic3);

            _hapticsManager.TriggerShake(4f, 0.1f, true, 2f);
        }

        private void UpdateEquipButtonState(EquipButton button, string? itemId, EquipSlotType type)
        {
            string name = "NOTHING";
            Texture2D? icon = null;
            Texture2D? silhouette = null;
            int rarity = -1;

            if (!string.IsNullOrEmpty(itemId))
            {
                string path = "";
                if (type == EquipSlotType.Weapon)
                {
                    var data = GetWeaponData(itemId);
                    if (data != null) { name = data.WeaponName.ToUpper(); path = $"Sprites/Items/Weapons/{data.WeaponID}"; rarity = data.Rarity; }
                    else name = itemId.ToUpper();
                }
                else if (type == EquipSlotType.Armor)
                {
                    var data = GetArmorData(itemId);
                    if (data != null) { name = data.ArmorName.ToUpper(); path = $"Sprites/Items/Armor/{data.ArmorID}"; rarity = data.Rarity; }
                    else name = itemId.ToUpper();
                }
                else if (type == EquipSlotType.Relic1 || type == EquipSlotType.Relic2 || type == EquipSlotType.Relic3)
                {
                    var data = GetRelicData(itemId);
                    if (data != null) { name = data.RelicName.ToUpper(); path = $"Sprites/Items/Relics/{data.RelicID}"; rarity = data.Rarity; }
                    else name = itemId.ToUpper();
                }

                if (!string.IsNullOrEmpty(path))
                {
                    icon = _spriteManager.GetSmallRelicSprite(path);
                    silhouette = _spriteManager.GetSmallRelicSpriteSilhouette(path);
                }
            }
            else
            {
                icon = _spriteManager.InventoryEmptySlotSprite;
                silhouette = null;
            }

            button.MainText = name;
            button.IconTexture = icon;
            button.IconSilhouette = silhouette;
            button.IconSourceRect = null; // Use full texture
            button.Rarity = rarity;
        }
    }
}

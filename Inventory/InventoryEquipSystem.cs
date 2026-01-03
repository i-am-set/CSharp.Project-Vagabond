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
        private void OpenEquipSubmenu(int memberIndex, EquipSlotType slotType)
        {
            _isEquipSubmenuOpen = true;
            _activeEquipSlotType = slotType;
            _currentPartyMemberIndex = memberIndex;
            _equipMenuScrollIndex = 0;
            RefreshEquipSubmenuButtons();
        }

        private void RefreshEquipSubmenuButtons()
        {
            List<string> availableItems = new List<string>();
            var member = _gameState.PlayerState.Party[_currentPartyMemberIndex];

            if (_activeEquipSlotType == EquipSlotType.Weapon)
            {
                availableItems = _gameState.PlayerState.Weapons.Keys.ToList();
            }
            else if (_activeEquipSlotType == EquipSlotType.Armor)
            {
                availableItems = _gameState.PlayerState.Armors.Keys.ToList();
            }
            else if (_activeEquipSlotType == EquipSlotType.Relic)
            {
                // Get all owned relics
                var allRelics = _gameState.PlayerState.Relics.Keys.ToList();

                // Filter out any relic that is currently equipped by THIS member
                if (!string.IsNullOrEmpty(member.EquippedRelicId))
                {
                    availableItems = allRelics.Where(r => r != member.EquippedRelicId).ToList();
                }
                else
                {
                    availableItems = allRelics;
                }
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
                btn.Rarity = -1;

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
                    else if (_activeEquipSlotType == EquipSlotType.Relic)
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
            var member = _gameState.PlayerState.Party[_currentPartyMemberIndex];

            if (_activeEquipSlotType == EquipSlotType.Weapon)
            {
                member.EquippedWeaponId = itemId;
            }
            else if (_activeEquipSlotType == EquipSlotType.Armor)
            {
                member.EquippedArmorId = itemId;
            }
            else if (_activeEquipSlotType == EquipSlotType.Relic)
            {
                member.EquippedRelicId = itemId;
            }

            _isEquipSubmenuOpen = false;
            _activeEquipSlotType = EquipSlotType.None;
            _hoveredItemData = null;

            _hapticsManager.TriggerCompoundShake(0.5f);
        }
    }
}
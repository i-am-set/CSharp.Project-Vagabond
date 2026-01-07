using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class InventoryEquipSystem
    {
        private readonly SplitMapInventoryOverlay _overlay;
        private readonly InventoryDataProcessor _dataProcessor;

        public InventoryEquipSystem(SplitMapInventoryOverlay overlay, InventoryDataProcessor dataProcessor)
        {
            _overlay = overlay;
            _dataProcessor = dataProcessor;
        }

        public void OpenEquipSubmenu(int memberIndex, EquipSlotType slotType)
        {
            _overlay.IsEquipSubmenuOpen = true;
            _overlay.ActiveEquipSlotType = slotType;
            _overlay.CurrentPartyMemberIndex = memberIndex;
            _overlay.EquipMenuScrollIndex = 0;
            RefreshEquipSubmenuButtons();
        }

        public void RefreshEquipSubmenuButtons()
        {
            List<string> availableItems = new List<string>();
            var member = _overlay.GameState.PlayerState.Party[_overlay.CurrentPartyMemberIndex];

            if (_overlay.ActiveEquipSlotType == EquipSlotType.Weapon)
            {
                availableItems = _overlay.GameState.PlayerState.Weapons.Keys.ToList();
            }
            else if (_overlay.ActiveEquipSlotType == EquipSlotType.Armor)
            {
                availableItems = _overlay.GameState.PlayerState.Armors.Keys.ToList();
            }
            else if (_overlay.ActiveEquipSlotType == EquipSlotType.Relic)
            {
                var allRelics = _overlay.GameState.PlayerState.Relics.Keys.ToList();
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

            for (int i = 0; i < _overlay.EquipSubmenuButtons.Count; i++)
            {
                var btn = _overlay.EquipSubmenuButtons[i];
                int virtualIndex = _overlay.EquipMenuScrollIndex + i;

                btn.IsEnabled = false;
                btn.MainText = "";
                btn.IconTexture = null;
                btn.IconSilhouette = null;
                btn.OnClick = null;
                btn.Rarity = -1;

                if (i % 2 == 0)
                {
                    btn.CustomDefaultTextColor = _overlay.Global.Palette_BlueWhite;
                    btn.CustomTitleTextColor = _overlay.Global.Palette_DarkGray;
                }
                else
                {
                    btn.CustomDefaultTextColor = _overlay.Global.Palette_White;
                    btn.CustomTitleTextColor = _overlay.Global.Palette_DarkerGray;
                }

                if (virtualIndex == 0)
                {
                    btn.MainText = "REMOVE";
                    btn.TitleText = "SELECT";
                    btn.CustomDefaultTextColor = _overlay.Global.Palette_Red;
                    btn.IconTexture = _overlay.SpriteManager.InventoryEmptySlotSprite;
                    btn.IconSilhouette = null;
                    btn.IsEnabled = true;
                    btn.OnClick = () => SelectEquipItem(null);
                }
                else if (virtualIndex < totalItems)
                {
                    btn.TitleText = "SELECT";
                    int itemIndex = virtualIndex - 1;
                    string itemId = availableItems[itemIndex];

                    if (_overlay.ActiveEquipSlotType == EquipSlotType.Weapon)
                    {
                        var weaponData = _dataProcessor.GetWeaponData(itemId);
                        if (weaponData != null)
                        {
                            btn.MainText = weaponData.WeaponName.ToUpper();
                            string path = $"Sprites/Items/Weapons/{weaponData.WeaponID}";
                            btn.IconTexture = _overlay.SpriteManager.GetSmallRelicSprite(path);
                            btn.IconSilhouette = _overlay.SpriteManager.GetSmallRelicSpriteSilhouette(path);
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
                    else if (_overlay.ActiveEquipSlotType == EquipSlotType.Armor)
                    {
                        var armorData = _dataProcessor.GetArmorData(itemId);
                        if (armorData != null)
                        {
                            btn.MainText = armorData.ArmorName.ToUpper();
                            string path = $"Sprites/Items/Armor/{armorData.ArmorID}";
                            btn.IconTexture = _overlay.SpriteManager.GetSmallRelicSprite(path);
                            btn.IconSilhouette = _overlay.SpriteManager.GetSmallRelicSpriteSilhouette(path);
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
                    else if (_overlay.ActiveEquipSlotType == EquipSlotType.Relic)
                    {
                        var relicData = _dataProcessor.GetRelicData(itemId);
                        if (relicData != null)
                        {
                            btn.MainText = relicData.RelicName.ToUpper();
                            string path = $"Sprites/Items/Relics/{relicData.RelicID}";
                            btn.IconTexture = _overlay.SpriteManager.GetSmallRelicSprite(path);
                            btn.IconSilhouette = _overlay.SpriteManager.GetSmallRelicSpriteSilhouette(path);
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

        public void CancelEquipSelection()
        {
            if (_overlay.IsEquipSubmenuOpen)
            {
                _overlay.IsEquipSubmenuOpen = false;
                _overlay.ActiveEquipSlotType = EquipSlotType.None;
                _overlay.HoveredItemData = null;
            }
        }

        private void SelectEquipItem(string? itemId)
        {
            var member = _overlay.GameState.PlayerState.Party[_overlay.CurrentPartyMemberIndex];

            if (_overlay.ActiveEquipSlotType == EquipSlotType.Weapon)
            {
                member.EquippedWeaponId = itemId;
            }
            else if (_overlay.ActiveEquipSlotType == EquipSlotType.Armor)
            {
                member.EquippedArmorId = itemId;
            }
            else if (_overlay.ActiveEquipSlotType == EquipSlotType.Relic)
            {
                member.EquippedRelicId = itemId;
            }

            _overlay.IsEquipSubmenuOpen = false;
            _overlay.ActiveEquipSlotType = EquipSlotType.None;
            _overlay.HoveredItemData = null;

            _overlay.HapticsManager.TriggerCompoundShake(0.5f);
        }
    }
}
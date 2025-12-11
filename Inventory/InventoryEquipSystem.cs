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
            else if (_activeEquipSlotType >= EquipSlotType.Spell1 && _activeEquipSlotType <= EquipSlotType.Spell4)
            {
                // Get all learned spells for THIS member
                var allSpells = member.Spells.Select(s => s.MoveID).ToList();

                // Get set of currently equipped spells for THIS member
                var equippedSpells = new HashSet<string>(member.EquippedSpells.Where(s => s != null).Select(s => s!.MoveID));

                // Filter out any spell that is currently equipped in ANY slot
                availableItems = allSpells.Where(s => !equippedSpells.Contains(s)).ToList();
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
                    else if (_activeEquipSlotType >= EquipSlotType.Spell1 && _activeEquipSlotType <= EquipSlotType.Spell4)
                    {
                        if (BattleDataCache.Moves.TryGetValue(itemId, out var moveData))
                        {
                            btn.MainText = moveData.MoveName.ToUpper();
                            string path = $"Sprites/Items/Spells/{moveData.MoveID}";

                            int elementId = moveData.OffensiveElementIDs.FirstOrDefault();
                            string? fallbackPath = null;
                            if (BattleDataCache.Elements.TryGetValue(elementId, out var elementDef))
                            {
                                string elName = elementDef.ElementName.ToLowerInvariant();
                                if (elName == "---") elName = "neutral";
                                fallbackPath = $"Sprites/Items/Spells/default_{elName}";
                            }

                            btn.IconTexture = _spriteManager.GetItemSprite(path, fallbackPath);
                            btn.IconSilhouette = _spriteManager.GetItemSpriteSilhouette(path, fallbackPath);
                            btn.IconSourceRect = null;

                            btn.Rarity = moveData.Rarity;
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
            else if (_activeEquipSlotType >= EquipSlotType.Spell1 && _activeEquipSlotType <= EquipSlotType.Spell4)
            {
                int slotIndex = _activeEquipSlotType - EquipSlotType.Spell1;
                if (itemId == null)
                {
                    member.EquippedSpells[slotIndex] = null;
                }
                else
                {
                    var spellEntry = member.Spells.FirstOrDefault(s => s.MoveID == itemId);
                    if (spellEntry != null)
                    {
                        member.EquippedSpells[slotIndex] = spellEntry;
                    }
                }
            }

            _isEquipSubmenuOpen = false;
            _activeEquipSlotType = EquipSlotType.None;
            _hoveredItemData = null;

            RefreshEquipView();
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
                else if (type == EquipSlotType.Relic)
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
            button.IconSourceRect = null;
            button.Rarity = rarity;
        }

        private void UpdateSpellEquipButtonState(SpellEquipButton button, MoveEntry? spellEntry)
        {
            if (spellEntry != null && BattleDataCache.Moves.TryGetValue(spellEntry.MoveID, out var moveData))
            {
                button.SpellName = moveData.MoveName;
                button.IsEquipped = true;
            }
            else
            {
                button.SpellName = "EMPTY";
                button.IsEquipped = false;
            }
        }
    }
}
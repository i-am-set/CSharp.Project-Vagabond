using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Items;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    /// <summary>
    /// Manages the logic and state for the Equipment Submenu.
    /// Owns the buttons and scroll state for the equip list.
    /// </summary>
    public class InventoryEquipSystem
    {
        private readonly Global _global;
        private readonly SplitMapInventoryOverlay _overlay;
        private readonly InventoryDataProcessor _dataProcessor;

        // --- State Ownership ---
        internal List<EquipButton> EquipSubmenuButtons { get; } = new();
        internal int EquipMenuScrollIndex { get; set; } = 0;
        internal EquipSlotType ActiveEquipSlotType { get; set; } = EquipSlotType.None;

        public InventoryEquipSystem(SplitMapInventoryOverlay overlay, InventoryDataProcessor dataProcessor)
        {
            _overlay = overlay;
            _dataProcessor = dataProcessor;
        }

        public void Initialize()
        {
            EquipSubmenuButtons.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            int equipButtonX = (Global.VIRTUAL_WIDTH - 180) / 2 - 60;
            int submenuStartY = 250 + 19 + 16 - 32;

            for (int i = 0; i < 7; i++)
            {
                int yPos = submenuStartY + (i * 16);
                var button = new EquipButton(new Rectangle(equipButtonX, yPos, 180, 16), "");
                button.TitleText = "";
                button.Font = secondaryFont;
                button.IsEnabled = false;
                button.EnableTextWave = true; // Ensure wave animation is enabled
                EquipSubmenuButtons.Add(button);
            }
        }

        public void OpenEquipSubmenu(int memberIndex, EquipSlotType slotType)
        {
            _overlay.CurrentState = InventoryState.EquipItemSelection;
            ActiveEquipSlotType = slotType;
            _overlay.CurrentPartyMemberIndex = memberIndex;
            EquipMenuScrollIndex = 0;
            RefreshEquipSubmenuButtons();

            // Clear any lingering hover data from the previous menu state to hide old info panels
            _overlay.HoveredItemData = null;
        }

        public void RefreshEquipSubmenuButtons()
        {
            List<string> availableItems = new List<string>();
            var member = _overlay.GameState.PlayerState.Party[_overlay.CurrentPartyMemberIndex];

            if (ActiveEquipSlotType == EquipSlotType.Weapon)
            {
                availableItems = _overlay.GameState.PlayerState.Weapons.Keys.ToList();
            }
            else if (ActiveEquipSlotType == EquipSlotType.Relic)
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

            for (int i = 0; i < EquipSubmenuButtons.Count; i++)
            {
                var btn = EquipSubmenuButtons[i];
                int virtualIndex = EquipMenuScrollIndex + i;

                btn.IsEnabled = false;
                btn.MainText = "";
                btn.IconTexture = null;
                btn.IconSilhouette = null;
                btn.OnClick = null;

                if (i % 2 == 0)
                {
                    btn.CustomDefaultTextColor = _overlay.Global.Palette_Sun;
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

                    if (ActiveEquipSlotType == EquipSlotType.Weapon)
                    {
                        var weaponData = _dataProcessor.GetWeaponData(itemId);
                        if (weaponData != null)
                        {
                            btn.MainText = weaponData.WeaponName.ToUpper();
                            string path = $"Sprites/Items/Weapons/{weaponData.WeaponID}";
                            btn.IconTexture = _overlay.SpriteManager.GetSmallRelicSprite(path);
                            btn.IconSilhouette = _overlay.SpriteManager.GetSmallRelicSpriteSilhouette(path);
                            btn.IconSourceRect = null;
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
                    else if (ActiveEquipSlotType == EquipSlotType.Relic)
                    {
                        var relicData = _dataProcessor.GetRelicData(itemId);
                        if (relicData != null)
                        {
                            btn.MainText = relicData.RelicName.ToUpper();
                            string path = $"Sprites/Items/Relics/{relicData.RelicID}";
                            btn.IconTexture = _overlay.SpriteManager.GetSmallRelicSprite(path);
                            btn.IconSilhouette = _overlay.SpriteManager.GetSmallRelicSpriteSilhouette(path);
                            btn.IconSourceRect = null;
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
            if (_overlay.CurrentState == InventoryState.EquipItemSelection)
            {
                _overlay.CurrentState = InventoryState.EquipTargetSelection;
                ActiveEquipSlotType = EquipSlotType.None;
                _overlay.HoveredItemData = null;
            }
        }

        private void SelectEquipItem(string? itemId)
        {
            var member = _overlay.GameState.PlayerState.Party[_overlay.CurrentPartyMemberIndex];

            if (ActiveEquipSlotType == EquipSlotType.Weapon)
            {
                member.EquippedWeaponId = itemId;
            }
            else if (ActiveEquipSlotType == EquipSlotType.Relic)
            {
                member.EquippedRelicId = itemId;
            }

            _overlay.CurrentState = InventoryState.EquipTargetSelection;
            ActiveEquipSlotType = EquipSlotType.None;
            _overlay.HoveredItemData = null;

            _overlay.HapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
        }
    }
}

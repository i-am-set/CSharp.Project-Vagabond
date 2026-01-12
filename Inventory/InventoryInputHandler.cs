using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class InventoryInputHandler
    {
        private readonly SplitMapInventoryOverlay _overlay;
        private readonly InventoryDataProcessor _dataProcessor;
        private readonly InventoryEquipSystem _equipSystem;

        public InventoryInputHandler(SplitMapInventoryOverlay overlay, InventoryDataProcessor dataProcessor, InventoryEquipSystem equipSystem)
        {
            _overlay = overlay;
            _dataProcessor = dataProcessor;
            _equipSystem = equipSystem;
        }

        public void InitializeInventoryUI()
        {
            if (_overlay.InventoryButton == null)
            {
                var inventoryIcon = _overlay.SpriteManager.SplitMapInventoryButton;
                var rects = _overlay.SpriteManager.SplitMapInventoryButtonSourceRects;
                _overlay.InventoryButton = new ImageButton(new Rectangle(2, 2, 16, 16), inventoryIcon, rects[0], rects[1], enableHoverSway: true);
                _overlay.InventoryButton.OnClick += () => _overlay.TriggerInventoryButtonClicked();
            }
            _overlay.InventoryButton.ResetAnimationState();

            _overlay.InventoryHeaderButtons.Clear();
            _overlay.InventoryHeaderButtonOffsets.Clear();
            _overlay.InventoryHeaderButtonBaseBounds.Clear();
            _overlay.SelectedInventoryCategory = InventoryCategory.Equip;
            _overlay.SelectedSlotIndex = -1;

            int numButtons = _overlay.CategoryOrder.Count;
            const int buttonSpriteSize = 32;
            const int spacing = 4;
            var buttonRects = _overlay.SpriteManager.InventoryHeaderButtonSourceRects;

            int totalWidth = (numButtons * buttonSpriteSize) + ((numButtons - 1) * spacing);
            float startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2f + 19f;
            float buttonY = 200 + 6;

            for (int i = 0; i < numButtons; i++)
            {
                var category = _overlay.CategoryOrder[i];
                Texture2D buttonSpriteSheet = category switch
                {
                    InventoryCategory.Weapons => _overlay.SpriteManager.InventoryHeaderButtonWeapons,
                    InventoryCategory.Armor => _overlay.SpriteManager.InventoryHeaderButtonArmor,
                    InventoryCategory.Relics => _overlay.SpriteManager.InventoryHeaderButtonRelics,
                    InventoryCategory.Consumables => _overlay.SpriteManager.InventoryHeaderButtonConsumables,
                    InventoryCategory.Misc => _overlay.SpriteManager.InventoryHeaderButtonMisc,
                    _ => _overlay.SpriteManager.InventoryHeaderButtonWeapons,
                };

                int menuIndex = (int)category;
                float xPos = startX + i * (buttonSpriteSize + spacing);
                var bounds = new Rectangle((int)MathF.Round(xPos), (int)buttonY, buttonSpriteSize, buttonSpriteSize);

                var button = new InventoryHeaderButton(bounds, buttonSpriteSheet, buttonRects[0], buttonRects[1], buttonRects[2], menuIndex, category.ToString());

                button.OnClick += () =>
                {
                    if (_overlay.SelectedInventoryCategory != category)
                    {
                        SwitchToCategory(category);
                    }
                };

                _overlay.InventoryHeaderButtons.Add(button);
                _overlay.InventoryHeaderButtonOffsets[button] = 0f;
                _overlay.InventoryHeaderButtonBaseBounds[button] = bounds;
            }

            var equipRects = _overlay.SpriteManager.InventoryHeaderButtonSourceRects;
            float equipX = startX - 60f;
            var equipBounds = new Rectangle((int)equipX, (int)buttonY, 32, 32);
            _overlay.InventoryEquipButton = new InventoryHeaderButton(equipBounds, _overlay.SpriteManager.InventoryHeaderButtonEquip, equipRects[0], equipRects[1], equipRects[2], (int)InventoryCategory.Equip, "Equip");
            _overlay.InventoryEquipButton.OnClick += () => SwitchToCategory(InventoryCategory.Equip);

            const int slotContainerWidth = 180;
            const int slotContainerHeight = 132;
            const int slotColumns = 6;
            const int slotRows = 5;
            const int slotSize = 24;
            const int gridPaddingX = 18;
            const int gridPaddingY = 8;

            int containerX = (Global.VIRTUAL_WIDTH - slotContainerWidth) / 2 - 60;
            int containerY = 200 + 6 + 32 + 1;
            _overlay.InventorySlotArea = new Rectangle(containerX, containerY, slotContainerWidth, slotContainerHeight);

            const int statsPanelHeight = 132;
            int statsPanelY = _overlay.InventorySlotArea.Y - 1;
            const int panelWidth = 76;
            int panelStartX = 8;

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var defaultFont = ServiceLocator.Get<BitmapFont>();

            _overlay.PartyEquipButtons.Clear();
            _overlay.PartySpellButtons.Clear();

            for (int i = 0; i < 4; i++)
            {
                _overlay.PartyMemberPanelAreas[i] = new Rectangle(
                    panelStartX + (i * panelWidth),
                    statsPanelY,
                    panelWidth,
                    statsPanelHeight
                );

                int centerX = _overlay.PartyMemberPanelAreas[i].Center.X;
                int currentY = _overlay.PartyMemberPanelAreas[i].Y + 4;
                currentY += defaultFont.LineHeight - 2;
                currentY += 32 + 2 - 6;
                currentY += 8 + secondaryFont.LineHeight + 4 - 3;

                int slotIconSize = 16;
                int gap = 4;
                int totalEquipWidth = (slotIconSize * 3) + (gap * 2);
                int equipStartX = centerX - (totalEquipWidth / 2);

                int memberIndex = i;
                int hitboxWidth = slotIconSize + 4;
                int hitboxXOffset = -2;

                var weaponBtn = new Button(new Rectangle(equipStartX + hitboxXOffset, currentY, hitboxWidth, slotIconSize), "") { EnableHoverSway = false };
                weaponBtn.OnClick += () =>
                {
                    _overlay.HapticsManager.TriggerZoomPulse(1.01f, 0.1f); // Add Haptic
                    _equipSystem.OpenEquipSubmenu(memberIndex, EquipSlotType.Weapon);
                };
                _overlay.PartyEquipButtons.Add(weaponBtn);

                var armorBtn = new Button(new Rectangle(equipStartX + slotIconSize + gap + hitboxXOffset, currentY, hitboxWidth, slotIconSize), "") { EnableHoverSway = false };
                armorBtn.OnClick += () =>
                {
                    _overlay.HapticsManager.TriggerZoomPulse(1.01f, 0.1f); // Add Haptic
                    _equipSystem.OpenEquipSubmenu(memberIndex, EquipSlotType.Armor);
                };
                _overlay.PartyEquipButtons.Add(armorBtn);

                var relicBtn = new Button(new Rectangle(equipStartX + (slotIconSize + gap) * 2 + hitboxXOffset, currentY, hitboxWidth, slotIconSize), "") { EnableHoverSway = false };
                relicBtn.OnClick += () =>
                {
                    _overlay.HapticsManager.TriggerZoomPulse(1.01f, 0.1f); // Add Haptic
                    _equipSystem.OpenEquipSubmenu(memberIndex, EquipSlotType.Relic);
                };
                _overlay.PartyEquipButtons.Add(relicBtn);

                currentY += slotSize + 6 - 5;
                currentY += (int)secondaryFont.LineHeight + 1;
                currentY += (int)secondaryFont.LineHeight + 1;
                currentY += (int)secondaryFont.LineHeight + 1;
                currentY += (int)secondaryFont.LineHeight + 1;

                int spellButtonWidth = 64;
                int spellButtonHeight = 8;
                int spellButtonX = centerX - (spellButtonWidth / 2);
                int spellButtonY = currentY + 2 - 8;

                for (int s = 0; s < 4; s++)
                {
                    var spellBtn = new SpellEquipButton(new Rectangle(spellButtonX, spellButtonY, spellButtonWidth, spellButtonHeight));
                    _overlay.PartySpellButtons.Add(spellBtn);
                    spellButtonY += spellButtonHeight;
                }
            }

            _overlay.InventorySlots.Clear();

            float availableWidth = slotContainerWidth - (gridPaddingX * 2);
            float availableHeight = slotContainerHeight - (gridPaddingY * 2);
            float spaceBetweenX = (slotColumns > 1) ? (availableWidth - slotSize) / (slotColumns - 1) : 0;
            float spaceBetweenY = (slotRows > 1) ? (availableHeight - slotSize) / (slotRows - 1) : 0;

            var slotFrames = _overlay.SpriteManager.InventorySlotSourceRects;
            if (slotFrames != null && slotFrames.Length > 0)
            {
                for (int row = 0; row < slotRows; row++)
                {
                    for (int col = 0; col < slotColumns; col++)
                    {
                        float nodeX = _overlay.InventorySlotArea.X + gridPaddingX + (slotSize / 2f) + (col * spaceBetweenX);
                        float nodeY = _overlay.InventorySlotArea.Y + gridPaddingY + (slotSize / 2f) + (row * spaceBetweenY);

                        var position = new Vector2(MathF.Round(nodeX), MathF.Round(nodeY));
                        var bounds = new Rectangle((int)(position.X - slotSize / 2f), (int)(position.Y - slotSize / 2f), slotSize, slotSize);

                        var slot = new InventorySlot(bounds, slotFrames);
                        slot.OnClick += () =>
                        {
                            if (slot.HasItem)
                            {
                                foreach (var s in _overlay.InventorySlots) s.IsSelected = false;
                                slot.IsSelected = true;
                                _overlay.SelectedSlotIndex = _overlay.InventorySlots.IndexOf(slot);
                            }
                        };
                        _overlay.InventorySlots.Add(slot);
                    }
                }
            }

            _dataProcessor.RefreshInventorySlots();

            var leftArrowRects = _overlay.SpriteManager.InventoryLeftArrowButtonSourceRects;
            var rightArrowRects = _overlay.SpriteManager.InventoryRightArrowButtonSourceRects;

            _overlay.DebugButton1 = new ImageButton(new Rectangle(0, 0, 5, 5), _overlay.SpriteManager.InventoryLeftArrowButton, leftArrowRects[0], leftArrowRects[1]);
            _overlay.DebugButton1.OnClick += () => CycleCategory(-1);

            _overlay.DebugButton2 = new ImageButton(new Rectangle(0, 0, 5, 5), _overlay.SpriteManager.InventoryRightArrowButton, rightArrowRects[0], rightArrowRects[1]);
            _overlay.DebugButton2.OnClick += () => CycleCategory(1);

            _overlay.PageLeftButton = new ImageButton(new Rectangle(0, 0, 5, 5), _overlay.SpriteManager.InventoryLeftArrowButton, leftArrowRects[0], leftArrowRects[1]);
            _overlay.PageLeftButton.OnClick += () => ChangePage(-1);

            _overlay.PageRightButton = new ImageButton(new Rectangle(0, 0, 5, 5), _overlay.SpriteManager.InventoryRightArrowButton, rightArrowRects[0], rightArrowRects[1]);
            _overlay.PageRightButton.OnClick += () => ChangePage(1);
        }

        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState, bool allowAccess, Matrix cameraTransform)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));

            if (_overlay.InventoryButton != null)
            {
                bool isVisible = _overlay.IsOpen || allowAccess;
                if (isVisible)
                {
                    _overlay.InventoryButton.IsEnabled = true;
                    _overlay.InventoryButton.Update(currentMouseState);
                }
                else
                {
                    _overlay.InventoryButton.ResetAnimationState();
                }
            }

            if (!_overlay.IsOpen)
            {
                _overlay.PreviousMouseState = currentMouseState;
                _overlay.PreviousKeyboardState = currentKeyboardState;
                return;
            }

            _overlay.DebugButton1?.Update(currentMouseState, cameraTransform);
            _overlay.DebugButton2?.Update(currentMouseState, cameraTransform);

            if (_overlay.SelectedInventoryCategory == InventoryCategory.Equip)
            {
                _overlay.HoveredItemData = null;
                if (_overlay.CurrentState != InventoryState.EquipItemSelection) _overlay.HoveredMemberIndex = -1;

                if (_overlay.CurrentState == InventoryState.EquipTargetSelection)
                {
                    int partyCount = _overlay.GameState.PlayerState.Party.Count;

                    for (int i = 0; i < _overlay.PartyEquipButtons.Count; i++)
                    {
                        int memberIndex = i / 3;
                        int slotType = i % 3;

                        if (memberIndex < partyCount)
                        {
                            var btn = _overlay.PartyEquipButtons[i];
                            btn.IsEnabled = true;
                            btn.Update(currentMouseState, cameraTransform);

                            if (btn.IsHovered)
                            {
                                _overlay.HoveredMemberIndex = memberIndex;
                                var member = _overlay.GameState.PlayerState.Party[memberIndex];

                                if (slotType == 0 && !string.IsNullOrEmpty(member.EquippedWeaponId))
                                    _overlay.HoveredItemData = _dataProcessor.GetWeaponData(member.EquippedWeaponId);
                                else if (slotType == 1 && !string.IsNullOrEmpty(member.EquippedArmorId))
                                    _overlay.HoveredItemData = _dataProcessor.GetArmorData(member.EquippedArmorId);
                                else if (slotType == 2 && !string.IsNullOrEmpty(member.EquippedRelicId))
                                    _overlay.HoveredItemData = _dataProcessor.GetRelicData(member.EquippedRelicId);
                            }
                        }
                        else
                        {
                            _overlay.PartyEquipButtons[i].IsEnabled = false;
                            _overlay.PartyEquipButtons[i].ResetAnimationState();
                        }
                    }

                    for (int i = 0; i < _overlay.PartySpellButtons.Count; i++)
                    {
                        int memberIndex = i / 4;
                        int slotIndex = i % 4;

                        if (memberIndex < partyCount)
                        {
                            var btn = _overlay.PartySpellButtons[i];
                            btn.IsEnabled = true;
                            btn.Update(currentMouseState, cameraTransform);

                            if (btn.IsHovered)
                            {
                                _overlay.HoveredMemberIndex = memberIndex;
                                var member = _overlay.GameState.PlayerState.Party[memberIndex];
                                var spellEntry = member.Spells[slotIndex];
                                if (spellEntry != null && BattleDataCache.Moves.TryGetValue(spellEntry.MoveID, out var moveData))
                                {
                                    _overlay.HoveredItemData = moveData;
                                    // Use Hint cursor for spell slots (info only)
                                    ServiceLocator.Get<CursorManager>().SetState(CursorState.Hint);
                                }
                            }
                        }
                        else
                        {
                            _overlay.PartySpellButtons[i].IsEnabled = false;
                            _overlay.PartySpellButtons[i].ResetAnimationState();
                        }
                    }
                }
                else if (_overlay.CurrentState == InventoryState.EquipItemSelection)
                {
                    var member = _overlay.GameState.PlayerState.Party[_overlay.CurrentPartyMemberIndex];
                    List<string> availableItems = new List<string>();
                    if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Weapon) availableItems = _overlay.GameState.PlayerState.Weapons.Keys.ToList();
                    else if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Armor) availableItems = _overlay.GameState.PlayerState.Armors.Keys.ToList();
                    else if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Relic) availableItems = _overlay.GameState.PlayerState.Relics.Keys.ToList();

                    for (int i = 0; i < _equipSystem.EquipSubmenuButtons.Count; i++)
                    {
                        var button = _equipSystem.EquipSubmenuButtons[i];
                        button.Update(currentMouseState, cameraTransform);

                        if (button.IsHovered && button.IsEnabled)
                        {
                            int virtualIndex = _equipSystem.EquipMenuScrollIndex + i;
                            if (virtualIndex > 0)
                            {
                                int itemIndex = virtualIndex - 1;
                                if (itemIndex < availableItems.Count)
                                {
                                    string itemId = availableItems[itemIndex];
                                    if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Weapon) _overlay.HoveredItemData = _dataProcessor.GetWeaponData(itemId);
                                    else if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Armor) _overlay.HoveredItemData = _dataProcessor.GetArmorData(itemId);
                                    else if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Relic) _overlay.HoveredItemData = _dataProcessor.GetRelicData(itemId);
                                }
                            }
                        }
                    }
                }
            }

            var slotFrames = _overlay.SpriteManager.InventorySlotSourceRects;
            if (_overlay.SelectedInventoryCategory != _overlay.PreviousInventoryCategory)
            {
                _overlay.InventoryArrowAnimTimer = 0f;

                if (_overlay.SelectedInventoryCategory == InventoryCategory.Equip)
                {
                    _overlay.HapticsManager.TriggerZoomPulse(1.01f, 0.1f);
                }
                else
                {
                    _overlay.HapticsManager.TriggerCompoundShake(0.5f);
                }

                if (slotFrames != null)
                {
                    foreach (var slot in _overlay.InventorySlots) slot.RandomizeFrame();
                }
            }
            _overlay.PreviousInventoryCategory = _overlay.SelectedInventoryCategory;

            if (_overlay.InventoryArrowAnimTimer < SplitMapInventoryOverlay.INVENTORY_ARROW_ANIM_DURATION) _overlay.InventoryArrowAnimTimer += deltaTime;

            _overlay.SelectedHeaderBobTimer += deltaTime;
            if (_overlay.LeftPageArrowBobTimer > 0) _overlay.LeftPageArrowBobTimer -= deltaTime;
            if (_overlay.RightPageArrowBobTimer > 0) _overlay.RightPageArrowBobTimer -= deltaTime;

            int scrollDelta = currentMouseState.ScrollWheelValue - _overlay.PreviousMouseState.ScrollWheelValue;

            var firstHeader = _overlay.InventoryHeaderButtonBaseBounds.Values.First();
            var lastHeader = _overlay.InventoryHeaderButtonBaseBounds.Values.Last();
            var headerArea = new Rectangle(firstHeader.Left, firstHeader.Top, lastHeader.Right - firstHeader.Left, firstHeader.Height);
            headerArea.Y += (int)_overlay.InventoryPositionOffset.Y;

            bool leftPressed = currentKeyboardState.IsKeyDown(Keys.Left) && !_overlay.PreviousKeyboardState.IsKeyDown(Keys.Left);
            bool rightPressed = currentKeyboardState.IsKeyDown(Keys.Right) && !_overlay.PreviousKeyboardState.IsKeyDown(Keys.Right);
            bool shiftHeld = currentKeyboardState.IsKeyDown(Keys.LeftShift) || currentKeyboardState.IsKeyDown(Keys.RightShift);
            bool scrollUp = scrollDelta > 0;
            bool scrollDown = scrollDelta < 0;
            bool isHoveringHeader = headerArea.Contains(mouseInWorldSpace);
            bool rightClickPressed = currentMouseState.RightButton == ButtonState.Pressed && _overlay.PreviousMouseState.RightButton == ButtonState.Released;

            if (_overlay.CurrentState == InventoryState.EquipItemSelection)
            {
                if (rightClickPressed)
                {
                    _equipSystem.CancelEquipSelection();
                }
                else if (scrollDelta != 0)
                {
                    int totalItems = 1;
                    var member = _overlay.GameState.PlayerState.Party[_overlay.CurrentPartyMemberIndex];

                    if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Weapon) totalItems += _overlay.GameState.PlayerState.Weapons.Count;
                    else if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Armor) totalItems += _overlay.GameState.PlayerState.Armors.Count;
                    else if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Relic) totalItems += _overlay.GameState.PlayerState.Relics.Count;

                    int maxScroll = Math.Max(0, totalItems - 7);

                    if (scrollDelta < 0 && _equipSystem.EquipMenuScrollIndex < maxScroll)
                    {
                        _equipSystem.EquipMenuScrollIndex++;
                        _equipSystem.RefreshEquipSubmenuButtons();
                    }
                    else if (scrollDelta > 0 && _equipSystem.EquipMenuScrollIndex > 0)
                    {
                        _equipSystem.EquipMenuScrollIndex--;
                        _equipSystem.RefreshEquipSubmenuButtons();
                    }
                }
            }
            else if (_overlay.CurrentState != InventoryState.EquipItemSelection)
            {
                if (leftPressed)
                {
                    if (shiftHeld) CycleCategory(-1);
                    else ChangePage(-1);
                }
                else if (rightPressed)
                {
                    if (shiftHeld) CycleCategory(1);
                    else ChangePage(1);
                }
                else if (scrollUp)
                {
                    if (isHoveringHeader) CycleCategory(-1);
                    else ChangePage(-1);
                }
                else if (scrollDown)
                {
                    if (isHoveringHeader) CycleCategory(1);
                    else ChangePage(1);
                }
            }

            int selectedIndex = _overlay.CategoryOrder.IndexOf(_overlay.SelectedInventoryCategory);
            if (_overlay.SelectedInventoryCategory == InventoryCategory.Equip) selectedIndex = -1;

            const float repulsionAmount = 8f;
            const float repulsionSpeed = 15f;
            int numButtons = _overlay.InventoryHeaderButtons.Count;
            float rawOffsetFirst = 0f;
            float rawOffsetLast = 0f;

            if (numButtons > 0 && selectedIndex != -1)
            {
                if (0 < selectedIndex) rawOffsetFirst = -repulsionAmount;
                else if (0 > selectedIndex) rawOffsetFirst = repulsionAmount;

                int lastIdx = numButtons - 1;
                if (lastIdx < selectedIndex) rawOffsetLast = -repulsionAmount;
                else if (lastIdx > selectedIndex) rawOffsetLast = repulsionAmount;
            }
            float centeringCorrection = -(rawOffsetFirst + rawOffsetLast) / 2f;

            InventoryHeaderButton? selectedButton = null;

            foreach (var btn in _overlay.InventoryHeaderButtons)
            {
                bool hasItems = HasItems((InventoryCategory)btn.MenuIndex);
                if (_overlay.SelectedInventoryCategory == (InventoryCategory)btn.MenuIndex)
                {
                    btn.IsEnabled = true;
                }
                else
                {
                    btn.IsEnabled = hasItems;
                }
            }

            for (int i = 0; i < _overlay.InventoryHeaderButtons.Count; i++)
            {
                var button = _overlay.InventoryHeaderButtons[i];
                float targetOffset = 0f;
                if (selectedIndex != -1)
                {
                    if (i < selectedIndex) targetOffset = -repulsionAmount;
                    else if (i > selectedIndex) targetOffset = repulsionAmount;
                }
                targetOffset += centeringCorrection;

                float currentOffset = _overlay.InventoryHeaderButtonOffsets[button];
                _overlay.InventoryHeaderButtonOffsets[button] = MathHelper.Lerp(currentOffset, targetOffset, repulsionSpeed * deltaTime);
                float finalOffset = _overlay.InventoryHeaderButtonOffsets[button];

                var baseBounds = _overlay.InventoryHeaderButtonBaseBounds[button];
                button.IsSelected = ((int)_overlay.SelectedInventoryCategory == button.MenuIndex);

                float selectedBobY = 0f;
                if (button.IsSelected)
                {
                    selectedBobY = -MathF.Round((MathF.Sin(_overlay.SelectedHeaderBobTimer * 5f) + 1f) * 0.5f);
                }

                button.Bounds = new Rectangle(
                    baseBounds.X + (int)MathF.Round(finalOffset),
                    baseBounds.Y + (int)MathF.Round(_overlay.InventoryPositionOffset.Y) + (int)selectedBobY,
                    baseBounds.Width,
                    baseBounds.Height);

                if (button.IsSelected) selectedButton = button;
                button.Update(currentMouseState, cameraTransform);
            }

            if (_overlay.InventoryEquipButton != null)
            {
                float equipBaseX = (Global.VIRTUAL_WIDTH - 172) / 2f + 19f - 60f;
                float equipBaseY = 200 + 6;

                _overlay.InventoryEquipButton.IsSelected = _overlay.SelectedInventoryCategory == InventoryCategory.Equip;

                float equipBobY = 0f;
                if (_overlay.InventoryEquipButton.IsSelected)
                {
                    equipBobY = -MathF.Round((MathF.Sin(_overlay.SelectedHeaderBobTimer * 5f) + 1f) * 0.5f);
                }

                _overlay.InventoryEquipButton.Bounds = new Rectangle((int)equipBaseX, (int)(equipBaseY + _overlay.InventoryPositionOffset.Y + equipBobY), 32, 32);
                _overlay.InventoryEquipButton.Update(currentMouseState, cameraTransform);
            }

            if (selectedButton != null && _overlay.DebugButton1 != null && _overlay.DebugButton2 != null)
            {
                float progress = Math.Clamp(_overlay.InventoryArrowAnimTimer / SplitMapInventoryOverlay.INVENTORY_ARROW_ANIM_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutCubic(progress);
                float currentOffset = MathHelper.Lerp(16f, 13f, easedProgress);

                var baseBounds = _overlay.InventoryHeaderButtonBaseBounds[selectedButton];
                int centerY = baseBounds.Center.Y + (int)MathF.Round(_overlay.InventoryPositionOffset.Y);
                int centerX = selectedButton.Bounds.Center.X;

                _overlay.DebugButton1.Bounds = new Rectangle(centerX - (int)currentOffset - (_overlay.DebugButton1.Bounds.Width / 2), centerY - _overlay.DebugButton1.Bounds.Height / 2 - 2, _overlay.DebugButton1.Bounds.Width, _overlay.DebugButton1.Bounds.Height);
                _overlay.DebugButton2.Bounds = new Rectangle(centerX + (int)currentOffset - (_overlay.DebugButton2.Bounds.Width / 2), centerY - _overlay.DebugButton2.Bounds.Height / 2 - 2, _overlay.DebugButton2.Bounds.Width, _overlay.DebugButton2.Bounds.Height);

                int currentIndex = _overlay.CategoryOrder.IndexOf(_overlay.SelectedInventoryCategory);
                _overlay.DebugButton1.IsEnabled = FindNextNonEmptyCategory(currentIndex, -1) != -1;
                _overlay.DebugButton2.IsEnabled = FindNextNonEmptyCategory(currentIndex, 1) != -1;
            }
            else if (_overlay.DebugButton1 != null && _overlay.DebugButton2 != null)
            {
                if (_overlay.SelectedInventoryCategory == InventoryCategory.Equip)
                {
                    _overlay.DebugButton1.IsEnabled = false;
                    _overlay.DebugButton2.IsEnabled = FindNextNonEmptyCategory(-1, 1) != -1;
                }
                else
                {
                    _overlay.DebugButton1.IsEnabled = false;
                    _overlay.DebugButton2.IsEnabled = false;
                }
            }

            if (_overlay.SelectedInventoryCategory != InventoryCategory.Equip)
            {
                _overlay.HoveredItemData = null;
                _overlay.HoveredMemberIndex = -1;
                InventorySlot? bestSlot = null;
                float minDistance = float.MaxValue;
                var inverseCamera = Matrix.Invert(cameraTransform);
                Vector2 mouseWorld = Vector2.Transform(virtualMousePos, inverseCamera);

                foreach (var slot in _overlay.InventorySlots)
                {
                    if (slot.Bounds.Contains(mouseWorld))
                    {
                        float dist = Vector2.DistanceSquared(mouseWorld, slot.Bounds.Center.ToVector2());
                        if (dist < minDistance) { minDistance = dist; bestSlot = slot; }
                    }
                }

                foreach (var slot in _overlay.InventorySlots)
                {
                    if (slot == bestSlot)
                    {
                        slot.Update(gameTime, currentMouseState, cameraTransform);
                    }
                    else
                    {
                        var dummyMouse = new MouseState(-10000, -10000, currentMouseState.ScrollWheelValue, currentMouseState.LeftButton, currentMouseState.MiddleButton, currentMouseState.RightButton, currentMouseState.XButton1, currentMouseState.XButton2);
                        slot.Update(gameTime, dummyMouse, cameraTransform);
                    }
                }

                InventorySlot? activeSlot = _overlay.InventorySlots.FirstOrDefault(s => s.IsSelected);
                if (activeSlot == null)
                {
                    activeSlot = bestSlot;
                }

                if (activeSlot != null && activeSlot.HasItem && !string.IsNullOrEmpty(activeSlot.ItemId))
                {
                    if (_overlay.SelectedInventoryCategory == InventoryCategory.Weapons)
                        _overlay.HoveredItemData = BattleDataCache.Weapons.Values.FirstOrDefault(w => w.WeaponName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                    else if (_overlay.SelectedInventoryCategory == InventoryCategory.Armor)
                        _overlay.HoveredItemData = BattleDataCache.Armors.Values.FirstOrDefault(a => a.ArmorName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                    else if (_overlay.SelectedInventoryCategory == InventoryCategory.Relics)
                        _overlay.HoveredItemData = BattleDataCache.Relics.Values.FirstOrDefault(r => r.RelicName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                    else if (_overlay.SelectedInventoryCategory == InventoryCategory.Consumables)
                        _overlay.HoveredItemData = BattleDataCache.Consumables.Values.FirstOrDefault(c => c.ItemName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                    else if (_overlay.SelectedInventoryCategory == InventoryCategory.Misc)
                        _overlay.HoveredItemData = BattleDataCache.MiscItems.Values.FirstOrDefault(m => m.ItemName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                }

                if (_overlay.TotalPages > 1)
                {
                    var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                    string pageText = $"{_overlay.CurrentPage + 1}/{_overlay.TotalPages}";
                    var textSize = secondaryFont.MeasureString(pageText);
                    float textCenterX = _overlay.InventorySlotArea.Center.X;
                    float textY = _overlay.InventorySlotArea.Bottom - 2;
                    const int buttonGap = 5;

                    float leftBob = 0f;
                    if (_overlay.LeftPageArrowBobTimer > 0)
                    {
                        float progress = 1.0f - (_overlay.LeftPageArrowBobTimer / SplitMapInventoryOverlay.PAGE_ARROW_BOB_DURATION);
                        leftBob = -MathF.Sin(progress * MathHelper.Pi) * 1f;
                    }

                    float rightBob = 0f;
                    if (_overlay.RightPageArrowBobTimer > 0)
                    {
                        float progress = 1.0f - (_overlay.RightPageArrowBobTimer / SplitMapInventoryOverlay.PAGE_ARROW_BOB_DURATION);
                        rightBob = -MathF.Sin(progress * MathHelper.Pi) * 1f;
                    }

                    if (_overlay.PageLeftButton != null)
                    {
                        _overlay.PageLeftButton.Bounds = new Rectangle(
                            (int)(textCenterX - textSize.Width / 2f - _overlay.PageLeftButton.Bounds.Width - buttonGap + 5),
                            (int)(textY + 1 + leftBob),
                            _overlay.PageLeftButton.Bounds.Width,
                            _overlay.PageLeftButton.Bounds.Height
                        );
                        _overlay.PageLeftButton.IsEnabled = true;
                        _overlay.PageLeftButton.Update(currentMouseState, cameraTransform);
                    }

                    if (_overlay.PageRightButton != null)
                    {
                        _overlay.PageRightButton.Bounds = new Rectangle(
                            (int)(textCenterX + textSize.Width / 2f + buttonGap - 3),
                            (int)(textY + 1 + rightBob),
                            _overlay.PageRightButton.Bounds.Width,
                            _overlay.PageRightButton.Bounds.Height
                        );
                        _overlay.PageRightButton.IsEnabled = true;
                        _overlay.PageRightButton.Update(currentMouseState, cameraTransform);
                    }
                }
            }
            else if (_overlay.SelectedInventoryCategory == InventoryCategory.Equip)
            {
                if (_overlay.HoveredItemData == null) _overlay.HoveredMemberIndex = -1;
            }

            _overlay.StatCycleTimer += deltaTime;
            if (_overlay.HoveredItemData != _overlay.PreviousHoveredItemData)
            {
                _overlay.StatCycleTimer = 0f;
                _overlay.PreviousHoveredItemData = _overlay.HoveredItemData;
                _overlay.InfoPanelNameWaveTimer = 0f;
            }

            int nameLength = 0;
            if (_overlay.HoveredItemData is MoveData md) nameLength = md.MoveName.Length;
            else if (_overlay.HoveredItemData is WeaponData wd) nameLength = wd.WeaponName.Length;
            else if (_overlay.HoveredItemData is ArmorData ad) nameLength = ad.ArmorName.Length;
            else if (_overlay.HoveredItemData is RelicData rd) nameLength = rd.RelicName.Length;
            else if (_overlay.HoveredItemData is ConsumableItemData cd) nameLength = cd.ItemName.Length;
            else if (_overlay.HoveredItemData is MiscItemData mid) nameLength = mid.ItemName.Length;

            if (_overlay.HoveredItemData != null)
            {
                _overlay.InfoPanelNameWaveTimer += deltaTime;
                float duration = TextAnimator.GetSmallWaveDuration(nameLength);
                if (_overlay.InfoPanelNameWaveTimer > duration + 0.1f) _overlay.InfoPanelNameWaveTimer = 0f;
            }
            else
            {
                _overlay.InfoPanelNameWaveTimer = 0f;
            }

            _overlay.PreviousMouseState = currentMouseState;
            _overlay.PreviousKeyboardState = currentKeyboardState;
        }

        public void SwitchToCategory(InventoryCategory category)
        {
            _equipSystem.CancelEquipSelection();
            _overlay.SelectedInventoryCategory = category;
            _overlay.CurrentPage = 0;
            _overlay.SelectedSlotIndex = -1;
            _overlay.SelectedHeaderBobTimer = 0f;
            _overlay.CurrentState = category == InventoryCategory.Equip ? InventoryState.EquipTargetSelection : InventoryState.Browse;
            _dataProcessor.RefreshInventorySlots();

            if (category != InventoryCategory.Equip)
            {
                TriggerSlotAnimations();
            }
        }

        private void ChangePage(int direction)
        {
            if (_overlay.TotalPages <= 1) return;

            int totalItems = _dataProcessor.GetCurrentCategoryItems().Count;
            int maxPage = Math.Max(0, (int)Math.Ceiling((double)totalItems / SplitMapInventoryOverlay.ITEMS_PER_PAGE) - 1);

            _overlay.CurrentPage += direction;

            if (_overlay.CurrentPage > maxPage) _overlay.CurrentPage = 0;
            else if (_overlay.CurrentPage < 0) _overlay.CurrentPage = maxPage;

            if (direction < 0) _overlay.LeftPageArrowBobTimer = SplitMapInventoryOverlay.PAGE_ARROW_BOB_DURATION;
            else if (direction > 0) _overlay.RightPageArrowBobTimer = SplitMapInventoryOverlay.PAGE_ARROW_BOB_DURATION;

            _overlay.SelectedSlotIndex = -1;
            _dataProcessor.RefreshInventorySlots();
            TriggerSlotAnimations();
        }

        private void CycleCategory(int direction)
        {
            if (_overlay.SelectedInventoryCategory == InventoryCategory.Equip)
            {
                if (direction > 0)
                {
                    int target = FindNextNonEmptyCategory(-1, 1);
                    if (target != -1)
                    {
                        SwitchToCategory(_overlay.CategoryOrder[target]);
                    }
                }
                return;
            }

            int currentIndex = _overlay.CategoryOrder.IndexOf(_overlay.SelectedInventoryCategory);
            int targetIndex = FindNextNonEmptyCategory(currentIndex, direction);

            if (targetIndex != -1)
            {
                SwitchToCategory(_overlay.CategoryOrder[targetIndex]);
            }
        }

        private int FindNextNonEmptyCategory(int startIndex, int direction)
        {
            int checkIndex = startIndex + direction;
            while (checkIndex >= 0 && checkIndex < _overlay.CategoryOrder.Count)
            {
                if (HasItems(_overlay.CategoryOrder[checkIndex]))
                {
                    return checkIndex;
                }
                checkIndex += direction;
            }
            return -1;
        }

        private bool HasItems(InventoryCategory category)
        {
            return category switch
            {
                InventoryCategory.Weapons => _overlay.GameState.PlayerState.Weapons.Any(),
                InventoryCategory.Armor => _overlay.GameState.PlayerState.Armors.Any(),
                InventoryCategory.Relics => _overlay.GameState.PlayerState.Relics.Any(),
                InventoryCategory.Consumables => _overlay.GameState.PlayerState.Consumables.Any(),
                InventoryCategory.Misc => _overlay.GameState.PlayerState.MiscItems.Any(),
                _ => false
            };
        }

        private void TriggerSlotAnimations()
        {
            float delay = 0f;
            const float stagger = 0.015f;
            foreach (var slot in _overlay.InventorySlots)
            {
                if (slot.HasItem)
                {
                    slot.TriggerPopInAnimation(delay);
                    delay += stagger;
                }
            }
        }
    }
}
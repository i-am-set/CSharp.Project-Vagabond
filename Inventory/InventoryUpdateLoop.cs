#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Timers;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public partial class SplitMapInventoryOverlay
    {
        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState, bool isMapIdle, Matrix cameraTransform)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));

            // Update Toggle Button
            if (_inventoryButton != null)
            {
                bool isVisible = IsOpen || isMapIdle;
                if (isVisible)
                {
                    _inventoryButton.IsEnabled = true;
                    _inventoryButton.Update(currentMouseState);
                }
                else
                {
                    _inventoryButton.ResetAnimationState();
                }
            }

            if (!IsOpen)
            {
                _previousMouseState = currentMouseState;
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            // UPDATE DEBUG BUTTONS
            _debugButton1?.Update(currentMouseState, cameraTransform);
            _debugButton2?.Update(currentMouseState, cameraTransform);

            var slotFrames = _spriteManager.InventorySlotSourceRects;
            if (_selectedInventoryCategory != _previousInventoryCategory)
            {
                _inventoryArrowAnimTimer = 0f;

                _hapticsManager.TriggerShake(2f, 0.1f, true, 2f);

                if (slotFrames != null)
                {
                    foreach (var slot in _inventorySlots) slot.RandomizeFrame();
                }
            }
            _previousInventoryCategory = _selectedInventoryCategory;

            if (_inventoryArrowAnimTimer < INVENTORY_ARROW_ANIM_DURATION) _inventoryArrowAnimTimer += deltaTime;

            // Update Selected Header Bob Timer
            _selectedHeaderBobTimer += deltaTime;
            float selectedBobOffset = MathF.Round((MathF.Sin(_selectedHeaderBobTimer * 2.5f) + 1f) * 0.5f);

            // Update Page Arrow Bob Timers
            if (_leftPageArrowBobTimer > 0) _leftPageArrowBobTimer -= deltaTime;
            if (_rightPageArrowBobTimer > 0) _rightPageArrowBobTimer -= deltaTime;

            // Handle Input for Category Switching
            int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;

            // Calculate header area for scroll interaction
            var firstHeader = _inventoryHeaderButtonBaseBounds.Values.First();
            var lastHeader = _inventoryHeaderButtonBaseBounds.Values.Last();
            var headerArea = new Rectangle(firstHeader.Left, firstHeader.Top, lastHeader.Right - firstHeader.Left, firstHeader.Height);
            headerArea.Y += (int)_inventoryPositionOffset.Y;

            bool leftPressed = currentKeyboardState.IsKeyDown(Keys.Left) && !_previousKeyboardState.IsKeyDown(Keys.Left);
            bool rightPressed = currentKeyboardState.IsKeyDown(Keys.Right) && !_previousKeyboardState.IsKeyDown(Keys.Right);
            bool shiftHeld = currentKeyboardState.IsKeyDown(Keys.LeftShift) || currentKeyboardState.IsKeyDown(Keys.RightShift);
            bool scrollUp = scrollDelta > 0;
            bool scrollDown = scrollDelta < 0;
            bool isHoveringHeader = headerArea.Contains(mouseInWorldSpace);
            bool rightClickPressed = currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            // Handle Submenu Scrolling and Cancellation
            if (_isEquipSubmenuOpen)
            {
                if (rightClickPressed)
                {
                    CancelEquipSelection();
                }
                else if (scrollDelta != 0)
                {
                    int totalItems = 1; // 1 for REMOVE
                    if (_activeEquipSlotType == EquipSlotType.Weapon) totalItems += _gameState.PlayerState.Weapons.Count;
                    else if (_activeEquipSlotType == EquipSlotType.Armor) totalItems += _gameState.PlayerState.Armors.Count;
                    else if (_activeEquipSlotType == EquipSlotType.Relic1 || _activeEquipSlotType == EquipSlotType.Relic2 || _activeEquipSlotType == EquipSlotType.Relic3) totalItems += _gameState.PlayerState.Relics.Count;
                    else if (_activeEquipSlotType >= EquipSlotType.Spell1 && _activeEquipSlotType <= EquipSlotType.Spell4) totalItems += _gameState.PlayerState.Spells.Count;

                    int maxScroll = Math.Max(0, totalItems - 7); // 7 visible slots

                    if (scrollDelta < 0 && _equipMenuScrollIndex < maxScroll)
                    {
                        _equipMenuScrollIndex++;
                        RefreshEquipSubmenuButtons();
                    }
                    else if (scrollDelta > 0 && _equipMenuScrollIndex > 0)
                    {
                        _equipMenuScrollIndex--;
                        RefreshEquipSubmenuButtons();
                    }
                }
            }
            // Handle Category Switching and Pagination
            else if (!_isEquipSubmenuOpen)
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

            // Update Header Buttons
            int selectedIndex = _categoryOrder.IndexOf(_selectedInventoryCategory);
            if (_selectedInventoryCategory == InventoryCategory.Equip) selectedIndex = -1; // Equip is separate

            const float repulsionAmount = 8f;
            const float repulsionSpeed = 15f;
            int numButtons = _inventoryHeaderButtons.Count;
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
            for (int i = 0; i < _inventoryHeaderButtons.Count; i++)
            {
                var button = _inventoryHeaderButtons[i];
                float targetOffset = 0f;
                if (selectedIndex != -1)
                {
                    if (i < selectedIndex) targetOffset = -repulsionAmount;
                    else if (i > selectedIndex) targetOffset = repulsionAmount;
                }
                targetOffset += centeringCorrection;

                float currentOffset = _inventoryHeaderButtonOffsets[button];
                _inventoryHeaderButtonOffsets[button] = MathHelper.Lerp(currentOffset, targetOffset, repulsionSpeed * deltaTime);
                float finalOffset = _inventoryHeaderButtonOffsets[button];

                var baseBounds = _inventoryHeaderButtonBaseBounds[button];

                button.IsSelected = ((int)_selectedInventoryCategory == button.MenuIndex);

                float selectedBobY = 0f;
                if (button.IsSelected)
                {
                    selectedBobY = -MathF.Round((MathF.Sin(_selectedHeaderBobTimer * 5f) + 1f) * 0.5f);
                }

                button.Bounds = new Rectangle(
                    baseBounds.X + (int)MathF.Round(finalOffset),
                    baseBounds.Y + (int)MathF.Round(_inventoryPositionOffset.Y) + (int)selectedBobY,
                    baseBounds.Width,
                    baseBounds.Height);

                if (button.IsSelected) selectedButton = button;
                button.Update(currentMouseState, cameraTransform);
            }

            // Update Equip Button
            if (_inventoryEquipButton != null)
            {
                float equipBaseX = (Global.VIRTUAL_WIDTH - 172) / 2f + 19f - 60f;
                float equipBaseY = 200 + 6;

                _inventoryEquipButton.IsSelected = _selectedInventoryCategory == InventoryCategory.Equip;

                float equipBobY = 0f;
                if (_inventoryEquipButton.IsSelected)
                {
                    equipBobY = -MathF.Round((MathF.Sin(_selectedHeaderBobTimer * 5f) + 1f) * 0.5f);
                }

                _inventoryEquipButton.Bounds = new Rectangle((int)equipBaseX, (int)(equipBaseY + _inventoryPositionOffset.Y + equipBobY), 32, 32);
                _inventoryEquipButton.Update(currentMouseState, cameraTransform);
            }

            // Update Debug Buttons Position
            if (selectedButton != null && _debugButton1 != null && _debugButton2 != null)
            {
                float progress = Math.Clamp(_inventoryArrowAnimTimer / INVENTORY_ARROW_ANIM_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutCubic(progress);
                float currentOffset = MathHelper.Lerp(16f, 13f, easedProgress);

                // Use base bounds to calculate Y position without the selection bob effect
                var baseBounds = _inventoryHeaderButtonBaseBounds[selectedButton];
                int centerY = baseBounds.Center.Y + (int)MathF.Round(_inventoryPositionOffset.Y);
                int centerX = selectedButton.Bounds.Center.X;

                _debugButton1.Bounds = new Rectangle(centerX - (int)currentOffset - (_debugButton1.Bounds.Width / 2), centerY - _debugButton1.Bounds.Height / 2 - 2, _debugButton1.Bounds.Width, _debugButton1.Bounds.Height);
                _debugButton2.Bounds = new Rectangle(centerX + (int)currentOffset - (_debugButton2.Bounds.Width / 2), centerY - _debugButton2.Bounds.Height / 2 - 2, _debugButton2.Bounds.Width, _debugButton2.Bounds.Height);

                // Determine if arrows should be enabled based on the ordered list
                int currentIndex = _categoryOrder.IndexOf(_selectedInventoryCategory);
                _debugButton1.IsEnabled = currentIndex > 0 && _selectedInventoryCategory != InventoryCategory.Equip;
                _debugButton2.IsEnabled = currentIndex < _categoryOrder.Count - 1 && _selectedInventoryCategory != InventoryCategory.Equip;
            }

            // Update Slots or Equip UI
            if (_selectedInventoryCategory != InventoryCategory.Equip)
            {
                _hoveredItemData = null; // Ensure this is cleared when not in equip mode

                InventorySlot? bestSlot = null;
                float minDistance = float.MaxValue;
                var inverseCamera = Matrix.Invert(cameraTransform);
                Vector2 mouseWorld = Vector2.Transform(virtualMousePos, inverseCamera);

                foreach (var slot in _inventorySlots)
                {
                    if (slot.Bounds.Contains(mouseWorld))
                    {
                        float dist = Vector2.DistanceSquared(mouseWorld, slot.Bounds.Center.ToVector2());
                        if (dist < minDistance) { minDistance = dist; bestSlot = slot; }
                    }
                }

                foreach (var slot in _inventorySlots)
                {
                    if (slot == bestSlot) slot.Update(gameTime, currentMouseState, cameraTransform);
                    else
                    {
                        var dummyMouse = new MouseState(-10000, -10000, currentMouseState.ScrollWheelValue, currentMouseState.LeftButton, currentMouseState.MiddleButton, currentMouseState.RightButton, currentMouseState.XButton1, currentMouseState.XButton2);
                        slot.Update(gameTime, dummyMouse, cameraTransform);
                    }
                }

                // Update Page Buttons
                if (_totalPages > 1)
                {
                    var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                    string pageText = $"{_currentPage + 1}/{_totalPages}";
                    var textSize = secondaryFont.MeasureString(pageText);

                    float textCenterX = _inventorySlotArea.Center.X;
                    float textY = _inventorySlotArea.Bottom - 2;

                    const int buttonGap = 5;

                    // Calculate bob offsets
                    float leftBob = 0f;
                    if (_leftPageArrowBobTimer > 0)
                    {
                        float progress = 1.0f - (_leftPageArrowBobTimer / PAGE_ARROW_BOB_DURATION);
                        leftBob = -MathF.Sin(progress * MathHelper.Pi) * 1f;
                    }

                    float rightBob = 0f;
                    if (_rightPageArrowBobTimer > 0)
                    {
                        float progress = 1.0f - (_rightPageArrowBobTimer / PAGE_ARROW_BOB_DURATION);
                        rightBob = -MathF.Sin(progress * MathHelper.Pi) * 1f;
                    }

                    if (_pageLeftButton != null)
                    {
                        _pageLeftButton.Bounds = new Rectangle(
                            (int)(textCenterX - textSize.Width / 2f - _pageLeftButton.Bounds.Width - buttonGap + 5),
                            (int)(textY + 1 + leftBob),
                            _pageLeftButton.Bounds.Width,
                            _pageLeftButton.Bounds.Height
                        );
                        _pageLeftButton.IsEnabled = true;
                        _pageLeftButton.Update(currentMouseState, cameraTransform);
                    }

                    if (_pageRightButton != null)
                    {
                        _pageRightButton.Bounds = new Rectangle(
                            (int)(textCenterX + textSize.Width / 2f + buttonGap - 3),
                            (int)(textY + 1 + rightBob),
                            _pageRightButton.Bounds.Width,
                            _pageRightButton.Bounds.Height
                        );
                        _pageRightButton.IsEnabled = true;
                        _pageRightButton.Update(currentMouseState, cameraTransform);
                    }
                }
            }
            else if (_selectedInventoryCategory == InventoryCategory.Equip)
            {
                _hoveredItemData = null; // Reset hover data each frame

                if (_isEquipSubmenuOpen)
                {
                    List<string> availableItems = new List<string>();
                    if (_activeEquipSlotType == EquipSlotType.Weapon) availableItems = _gameState.PlayerState.Weapons.Keys.ToList();
                    else if (_activeEquipSlotType == EquipSlotType.Armor) availableItems = _gameState.PlayerState.Armors.Keys.ToList();
                    else if (_activeEquipSlotType == EquipSlotType.Relic1 || _activeEquipSlotType == EquipSlotType.Relic2 || _activeEquipSlotType == EquipSlotType.Relic3) availableItems = _gameState.PlayerState.Relics.Keys.ToList();
                    else if (_activeEquipSlotType >= EquipSlotType.Spell1 && _activeEquipSlotType <= EquipSlotType.Spell4) availableItems = _gameState.PlayerState.Spells.Select(s => s.MoveID).ToList();

                    for (int i = 0; i < _equipSubmenuButtons.Count; i++)
                    {
                        var button = _equipSubmenuButtons[i];
                        button.Update(currentMouseState, cameraTransform);

                        if (button.IsHovered && button.IsEnabled)
                        {
                            int virtualIndex = _equipMenuScrollIndex + i;
                            // Index 0 is "REMOVE", so items start at index 1
                            if (virtualIndex > 0)
                            {
                                int itemIndex = virtualIndex - 1;
                                if (itemIndex < availableItems.Count)
                                {
                                    string itemId = availableItems[itemIndex];
                                    if (_activeEquipSlotType == EquipSlotType.Weapon) _hoveredItemData = GetWeaponData(itemId);
                                    else if (_activeEquipSlotType == EquipSlotType.Armor) _hoveredItemData = GetArmorData(itemId);
                                    else if (_activeEquipSlotType == EquipSlotType.Relic1 || _activeEquipSlotType == EquipSlotType.Relic2 || _activeEquipSlotType == EquipSlotType.Relic3) _hoveredItemData = GetRelicData(itemId);
                                    else if (_activeEquipSlotType >= EquipSlotType.Spell1 && _activeEquipSlotType <= EquipSlotType.Spell4)
                                    {
                                        if (BattleDataCache.Moves.TryGetValue(itemId, out var move))
                                        {
                                            _hoveredItemData = move;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Animate icons for spells in submenu
                    if (_activeEquipSlotType >= EquipSlotType.Spell1 && _activeEquipSlotType <= EquipSlotType.Spell4)
                    {
                        foreach (var btn in _equipSubmenuButtons)
                        {
                            if (btn.IconTexture != null)
                            {
                                btn.IconSourceRect = _spriteManager.GetAnimatedIconSourceRect(btn.IconTexture, gameTime);
                            }
                        }
                    }
                }
                else
                {
                    _relicEquipButton1?.Update(currentMouseState, cameraTransform);
                    _relicEquipButton2?.Update(currentMouseState, cameraTransform);
                    _relicEquipButton3?.Update(currentMouseState, cameraTransform);
                    _armorEquipButton?.Update(currentMouseState, cameraTransform);
                    _weaponEquipButton?.Update(currentMouseState, cameraTransform);

                    foreach (var button in _spellEquipButtons)
                    {
                        button.Update(currentMouseState, cameraTransform);
                    }
                }
            }

            // Update Stat Cycle Timer
            _statCycleTimer += deltaTime;
            if (_hoveredItemData != _previousHoveredItemData)
            {
                _statCycleTimer = 0f;
                _previousHoveredItemData = _hoveredItemData;
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        private void ChangePage(int direction)
        {
            if (_totalPages <= 1) return;

            int totalItems = GetCurrentCategoryItems().Count;
            int maxPage = Math.Max(0, (int)Math.Ceiling((double)totalItems / ITEMS_PER_PAGE) - 1);

            _currentPage += direction;

            // Wrap logic
            if (_currentPage > maxPage) _currentPage = 0;
            else if (_currentPage < 0) _currentPage = maxPage;

            // Trigger animation
            if (direction < 0) _leftPageArrowBobTimer = PAGE_ARROW_BOB_DURATION;
            else if (direction > 0) _rightPageArrowBobTimer = PAGE_ARROW_BOB_DURATION;

            _selectedSlotIndex = -1; // Clear selection on page change
            RefreshInventorySlots();
            TriggerSlotAnimations();
        }

        private void CycleCategory(int direction)
        {
            // Handle Equip Category (Index 5)
            if (_selectedInventoryCategory == InventoryCategory.Equip)
            {
                // Scroll Down/Right (1) -> Go to Weapons (0)
                if (direction > 0)
                {
                    SwitchToCategory(InventoryCategory.Weapons);
                }
                // Scroll Up/Left (-1) -> Do nothing
                return;
            }

            // Handle Main Categories using the ordered list
            int currentIndex = _categoryOrder.IndexOf(_selectedInventoryCategory);
            int newIndex = currentIndex + direction;

            if (newIndex >= 0 && newIndex < _categoryOrder.Count)
            {
                SwitchToCategory(_categoryOrder[newIndex]);
            }
        }

        private void SwitchToCategory(InventoryCategory category)
        {
            CancelEquipSelection();
            _selectedInventoryCategory = category;
            _currentPage = 0;
            _selectedSlotIndex = -1;
            _selectedHeaderBobTimer = 0f;
            RefreshInventorySlots();
            if (category != InventoryCategory.Equip)
            {
                TriggerSlotAnimations();
            }
        }

        private void TriggerSlotAnimations()
        {
            float delay = 0f;
            const float stagger = 0.015f;
            foreach (var slot in _inventorySlots)
            {
                if (slot.HasItem) // Only animate if it has an item
                {
                    slot.TriggerPopInAnimation(delay);
                    delay += stagger;
                }
            }
        }
    }
}
﻿
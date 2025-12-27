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
        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState, bool allowAccess, Matrix cameraTransform)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));

            // Update Toggle Button
            if (_inventoryButton != null)
            {
                // Button is visible if the overlay is open OR if access is allowed (Map Idle or Shop)
                bool isVisible = IsOpen || allowAccess;
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

            // UPDATE PARTY MEMBER EQUIP BUTTONS
            if (_selectedInventoryCategory == InventoryCategory.Equip)
            {
                // Reset hover data at start of frame for Equip view
                _hoveredItemData = null;
                // Only reset member index if we aren't in a submenu (to keep context if needed, though for hover it resets)
                if (!_isEquipSubmenuOpen) _hoveredMemberIndex = -1;

                if (!_isEquipSubmenuOpen)
                {
                    int partyCount = _gameState.PlayerState.Party.Count;

                    // Update Equipment Slot Buttons (Weapon, Armor, Relic)
                    for (int i = 0; i < _partyEquipButtons.Count; i++)
                    {
                        int memberIndex = i / 3;
                        int slotType = i % 3; // 0: Weapon, 1: Armor, 2: Relic

                        if (memberIndex < partyCount)
                        {
                            var btn = _partyEquipButtons[i];
                            btn.IsEnabled = true;
                            btn.Update(currentMouseState, cameraTransform);

                            // --- NEW: Hover Logic for Equipment Slots ---
                            if (btn.IsHovered)
                            {
                                _hoveredMemberIndex = memberIndex;
                                var member = _gameState.PlayerState.Party[memberIndex];

                                if (slotType == 0 && !string.IsNullOrEmpty(member.EquippedWeaponId))
                                {
                                    _hoveredItemData = GetWeaponData(member.EquippedWeaponId);
                                }
                                else if (slotType == 1 && !string.IsNullOrEmpty(member.EquippedArmorId))
                                {
                                    _hoveredItemData = GetArmorData(member.EquippedArmorId);
                                }
                                else if (slotType == 2 && !string.IsNullOrEmpty(member.EquippedRelicId))
                                {
                                    _hoveredItemData = GetRelicData(member.EquippedRelicId);
                                }
                            }
                        }
                        else
                        {
                            _partyEquipButtons[i].IsEnabled = false;
                            _partyEquipButtons[i].ResetAnimationState();
                        }
                    }

                    // UPDATE SPELL SLOT BUTTONS
                    for (int i = 0; i < _partySpellButtons.Count; i++)
                    {
                        int memberIndex = i / 4;
                        int slotIndex = i % 4;

                        if (memberIndex < partyCount)
                        {
                            var btn = _partySpellButtons[i];
                            btn.IsEnabled = true;
                            btn.Update(currentMouseState, cameraTransform);

                            // Check for hover to populate info panel
                            if (btn.IsHovered)
                            {
                                _hoveredMemberIndex = memberIndex;
                                var member = _gameState.PlayerState.Party[memberIndex];
                                var spellEntry = member.Spells[slotIndex];
                                if (spellEntry != null && BattleDataCache.Moves.TryGetValue(spellEntry.MoveID, out var moveData))
                                {
                                    _hoveredItemData = moveData;
                                }
                            }
                        }
                        else
                        {
                            _partySpellButtons[i].IsEnabled = false;
                            _partySpellButtons[i].ResetAnimationState();
                        }
                    }
                }
                else
                {
                    // If submenu is open, handle submenu button updates
                    var member = _gameState.PlayerState.Party[_currentPartyMemberIndex];
                    List<string> availableItems = new List<string>();
                    if (_activeEquipSlotType == EquipSlotType.Weapon) availableItems = _gameState.PlayerState.Weapons.Keys.ToList();
                    else if (_activeEquipSlotType == EquipSlotType.Armor) availableItems = _gameState.PlayerState.Armors.Keys.ToList();
                    else if (_activeEquipSlotType == EquipSlotType.Relic) availableItems = _gameState.PlayerState.Relics.Keys.ToList();

                    for (int i = 0; i < _equipSubmenuButtons.Count; i++)
                    {
                        var button = _equipSubmenuButtons[i];
                        button.Update(currentMouseState, cameraTransform);

                        if (button.IsHovered && button.IsEnabled)
                        {
                            int virtualIndex = _equipMenuScrollIndex + i;
                            if (virtualIndex > 0)
                            {
                                int itemIndex = virtualIndex - 1;
                                if (itemIndex < availableItems.Count)
                                {
                                    string itemId = availableItems[itemIndex];
                                    if (_activeEquipSlotType == EquipSlotType.Weapon) _hoveredItemData = GetWeaponData(itemId);
                                    else if (_activeEquipSlotType == EquipSlotType.Armor) _hoveredItemData = GetArmorData(itemId);
                                    else if (_activeEquipSlotType == EquipSlotType.Relic) _hoveredItemData = GetRelicData(itemId);
                                }
                            }
                        }
                    }
                }
            }

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
                    // Logic for scrolling equip submenu items
                    int totalItems = 1; // 1 for REMOVE
                    var member = _gameState.PlayerState.Party[_currentPartyMemberIndex];

                    if (_activeEquipSlotType == EquipSlotType.Weapon) totalItems += _gameState.PlayerState.Weapons.Count;
                    else if (_activeEquipSlotType == EquipSlotType.Armor) totalItems += _gameState.PlayerState.Armors.Count;
                    else if (_activeEquipSlotType == EquipSlotType.Relic) totalItems += _gameState.PlayerState.Relics.Count;

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
            if (_selectedInventoryCategory == InventoryCategory.Equip) selectedIndex = -1;

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

            // Update Header Buttons Enable State based on Inventory Count
            foreach (var btn in _inventoryHeaderButtons)
            {
                bool hasItems = HasItems((InventoryCategory)btn.MenuIndex);

                // If currently selected, force enabled (so it draws fully opaque)
                if (_selectedInventoryCategory == (InventoryCategory)btn.MenuIndex)
                {
                    btn.IsEnabled = true;
                }
                else
                {
                    btn.IsEnabled = hasItems;
                }
            }

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

                var baseBounds = _inventoryHeaderButtonBaseBounds[selectedButton];
                int centerY = baseBounds.Center.Y + (int)MathF.Round(_inventoryPositionOffset.Y);
                int centerX = selectedButton.Bounds.Center.X;

                _debugButton1.Bounds = new Rectangle(centerX - (int)currentOffset - (_debugButton1.Bounds.Width / 2), centerY - _debugButton1.Bounds.Height / 2 - 2, _debugButton1.Bounds.Width, _debugButton1.Bounds.Height);
                _debugButton2.Bounds = new Rectangle(centerX + (int)currentOffset - (_debugButton2.Bounds.Width / 2), centerY - _debugButton2.Bounds.Height / 2 - 2, _debugButton2.Bounds.Width, _debugButton2.Bounds.Height);

                int currentIndex = _categoryOrder.IndexOf(_selectedInventoryCategory);

                // Smart Enable/Disable based on content availability in that direction
                _debugButton1.IsEnabled = FindNextNonEmptyCategory(currentIndex, -1) != -1;
                _debugButton2.IsEnabled = FindNextNonEmptyCategory(currentIndex, 1) != -1;
            }
            else if (_debugButton1 != null && _debugButton2 != null)
            {
                // Disable buttons if no header is selected (e.g. Equip mode)
                // However, if in Equip mode, we might want to allow going Right to the first category
                if (_selectedInventoryCategory == InventoryCategory.Equip)
                {
                    _debugButton1.IsEnabled = false; // Can't go left from Equip
                    _debugButton2.IsEnabled = FindNextNonEmptyCategory(-1, 1) != -1; // Can go right if any category has items
                }
                else
                {
                    _debugButton1.IsEnabled = false;
                    _debugButton2.IsEnabled = false;
                }
            }

            // Update Slots or Equip UI
            if (_selectedInventoryCategory != InventoryCategory.Equip)
            {
                _hoveredItemData = null;
                _hoveredMemberIndex = -1;
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

                // --- FIX: Prioritize Selected Slot over Hovered Slot for Info Panel Data ---
                InventorySlot? activeSlot = _inventorySlots.FirstOrDefault(s => s.IsSelected);
                if (activeSlot == null)
                {
                    activeSlot = bestSlot; // Fallback to hovered slot
                }

                if (activeSlot != null && activeSlot.HasItem && !string.IsNullOrEmpty(activeSlot.ItemId))
                {
                    // Determine item type based on category
                    if (_selectedInventoryCategory == InventoryCategory.Weapons)
                        _hoveredItemData = BattleDataCache.Weapons.Values.FirstOrDefault(w => w.WeaponName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                    else if (_selectedInventoryCategory == InventoryCategory.Armor)
                        _hoveredItemData = BattleDataCache.Armors.Values.FirstOrDefault(a => a.ArmorName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                    else if (_selectedInventoryCategory == InventoryCategory.Relics)
                        _hoveredItemData = BattleDataCache.Relics.Values.FirstOrDefault(r => r.RelicName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                    else if (_selectedInventoryCategory == InventoryCategory.Consumables)
                        _hoveredItemData = BattleDataCache.Consumables.Values.FirstOrDefault(c => c.ItemName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                    else if (_selectedInventoryCategory == InventoryCategory.Misc)
                        _hoveredItemData = BattleDataCache.MiscItems.Values.FirstOrDefault(m => m.ItemName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
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
                // _hoveredItemData is now handled in the Equip Button update loop above
                // Ensure we reset if nothing is hovered
                if (_hoveredItemData == null) _hoveredMemberIndex = -1;
            }

            _statCycleTimer += deltaTime;
            if (_hoveredItemData != _previousHoveredItemData)
            {
                _statCycleTimer = 0f;
                _previousHoveredItemData = _hoveredItemData;
                _infoPanelNameWaveController.Reset(); // Reset wave animation on item change
            }

            // Update Wave Controller for Info Panel Name
            int nameLength = 0;
            if (_hoveredItemData is MoveData md) nameLength = md.MoveName.Length;
            else if (_hoveredItemData is WeaponData wd) nameLength = wd.WeaponName.Length;
            else if (_hoveredItemData is ArmorData ad) nameLength = ad.ArmorName.Length;
            else if (_hoveredItemData is RelicData rd) nameLength = rd.RelicName.Length;
            else if (_hoveredItemData is ConsumableItemData cd) nameLength = cd.ItemName.Length;
            else if (_hoveredItemData is MiscItemData mid) nameLength = mid.ItemName.Length;

            _infoPanelNameWaveController.Update(deltaTime, _hoveredItemData != null, nameLength);

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }
    }
}
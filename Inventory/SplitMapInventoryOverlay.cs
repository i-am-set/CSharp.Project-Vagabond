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
    public enum InventoryCategory { Weapons, Armor, Spells, Relics, Consumables, Equip }

    public class SplitMapInventoryOverlay
    {
        public bool IsOpen { get; private set; } = false;
        public event Action<bool>? OnInventoryToggled;

        // Expose hover state to block map interaction
        public bool IsHovered => _inventoryButton?.IsHovered ?? false;

        private readonly GameState _gameState;
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;

        private ImageButton? _inventoryButton;
        private readonly List<InventoryHeaderButton> _inventoryHeaderButtons = new();
        private readonly Dictionary<InventoryHeaderButton, float> _inventoryHeaderButtonOffsets = new();
        private readonly Dictionary<InventoryHeaderButton, Rectangle> _inventoryHeaderButtonBaseBounds = new();
        private InventoryHeaderButton? _inventoryEquipButton;
        private readonly List<InventorySlot> _inventorySlots = new();
        private Rectangle _inventorySlotArea;
        private Rectangle _statsPanelArea;
        private ImageButton? _debugButton1;
        private ImageButton? _debugButton2;
        private EquipButton? _relicEquipButton;

        // Submenu State
        private bool _isEquipSubmenuOpen = false;
        private readonly List<EquipButton> _equipSubmenuButtons = new();
        private int _equipMenuScrollIndex = 0; // Tracks the scroll position

        // Pagination State
        private int _currentPage = 0;
        private const int ITEMS_PER_PAGE = 12;

        private InventoryCategory _selectedInventoryCategory;
        private InventoryCategory _previousInventoryCategory;
        private int _selectedSlotIndex = -1;

        // Animation State
        private float _inventoryArrowAnimTimer;
        private const float INVENTORY_ARROW_ANIM_DURATION = 0.2f;
        private float _inventoryBobTimer;
        private const float INVENTORY_BOB_DURATION = 0.1f;
        private Vector2 _inventoryPositionOffset = Vector2.Zero;
        private float _selectedHeaderBobTimer;

        // Input State
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        public SplitMapInventoryOverlay()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
        }

        public void Initialize()
        {
            InitializeInventoryUI();
            _previousInventoryCategory = _selectedInventoryCategory;
            _inventoryArrowAnimTimer = INVENTORY_ARROW_ANIM_DURATION;
            _inventoryBobTimer = INVENTORY_BOB_DURATION;
            _inventoryPositionOffset = Vector2.Zero;
            _selectedHeaderBobTimer = 0f;

            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }

        private void InitializeInventoryUI()
        {
            if (_inventoryButton == null)
            {
                var inventoryIcon = _spriteManager.SplitMapInventoryButton;
                var rects = _spriteManager.SplitMapInventoryButtonSourceRects;
                _inventoryButton = new ImageButton(new Rectangle(7, 10, 16, 16), inventoryIcon, rects[0], rects[1]);
                _inventoryButton.OnClick += ToggleInventory;
            }
            _inventoryButton.ResetAnimationState();

            _inventoryHeaderButtons.Clear();
            _inventoryHeaderButtonOffsets.Clear();
            _inventoryHeaderButtonBaseBounds.Clear();
            _selectedInventoryCategory = InventoryCategory.Weapons;
            _selectedSlotIndex = -1;

            var categories = Enum.GetValues(typeof(InventoryCategory)).Cast<InventoryCategory>().Where(c => c != InventoryCategory.Equip).ToList();
            int numButtons = categories.Count;
            const int buttonSpriteSize = 32;
            const int containerWidth = 172;
            var buttonRects = _spriteManager.InventoryHeaderButtonSourceRects;

            float buttonClickableWidth = (float)containerWidth / numButtons;
            float startX = (Global.VIRTUAL_WIDTH - containerWidth) / 2f + 19f;
            float buttonY = 200 + 6;

            for (int i = 0; i < numButtons; i++)
            {
                var category = categories[i];
                Texture2D buttonSpriteSheet = category switch
                {
                    InventoryCategory.Weapons => _spriteManager.InventoryHeaderButtonWeapons,
                    InventoryCategory.Armor => _spriteManager.InventoryHeaderButtonArmor,
                    InventoryCategory.Spells => _spriteManager.InventoryHeaderButtonSpells,
                    InventoryCategory.Relics => _spriteManager.InventoryHeaderButtonRelics,
                    InventoryCategory.Consumables => _spriteManager.InventoryHeaderButtonConsumables,
                    _ => _spriteManager.InventoryHeaderButtonWeapons,
                };

                int menuIndex = (int)category;
                var bounds = new Rectangle((int)MathF.Round(startX + i * buttonClickableWidth), (int)buttonY, (int)MathF.Round(buttonClickableWidth), buttonSpriteSize);
                var button = new InventoryHeaderButton(bounds, buttonSpriteSheet, buttonRects[0], buttonRects[1], buttonRects[2], menuIndex, category.ToString());
                button.OnClick += () => {
                    CancelEquipSelection(); // Failsafe: Close submenu if switching categories
                    _selectedInventoryCategory = category;
                    _selectedSlotIndex = -1;
                    _currentPage = 0; // Reset page on category change
                    RefreshInventorySlots();
                };
                _inventoryHeaderButtons.Add(button);
                _inventoryHeaderButtonOffsets[button] = 0f;
                _inventoryHeaderButtonBaseBounds[button] = bounds;
            }

            // Initialize Equip Button
            var equipRects = _spriteManager.InventoryHeaderButtonSourceRects;
            float equipX = startX - 60f;
            var equipBounds = new Rectangle((int)equipX, (int)buttonY, 32, 32);
            _inventoryEquipButton = new InventoryHeaderButton(equipBounds, _spriteManager.InventoryHeaderButtonEquip, equipRects[0], equipRects[1], equipRects[2], (int)InventoryCategory.Equip, "Equip");
            _inventoryEquipButton.OnClick += () => {
                CancelEquipSelection(); // Failsafe: Ensure clean state when clicking main equip button
                _selectedInventoryCategory = InventoryCategory.Equip;
                _selectedSlotIndex = -1;
                _currentPage = 0;
                RefreshInventorySlots();
            };

            // Initialize inventory slot grid
            const int slotContainerWidth = 180;
            const int slotContainerHeight = 132;
            const int slotColumns = 4;
            const int slotRows = 3;
            const int slotSize = 48;

            int containerX = (Global.VIRTUAL_WIDTH - slotContainerWidth) / 2 - 60;
            int containerY = 200 + 6 + 32 + 1;
            _inventorySlotArea = new Rectangle(containerX, containerY, slotContainerWidth, slotContainerHeight);

            // Initialize Stats Panel Area
            const int statsPanelWidth = 116;
            const int statsPanelHeight = 132;
            int statsPanelX = _inventorySlotArea.Right + 4;
            int statsPanelY = _inventorySlotArea.Y - 1;
            _statsPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);

            _inventorySlots.Clear();

            float spaceBetweenX = (slotColumns > 1) ? (float)(slotContainerWidth - (slotSize)) / (slotColumns - 1) : 0;
            float spaceBetweenY = (slotRows > 1) ? (float)(slotContainerHeight - (slotSize)) / (slotRows - 1) : 0;

            var slotFrames = _spriteManager.InventorySlotSourceRects;
            if (slotFrames != null && slotFrames.Length > 0)
            {
                for (int row = 0; row < slotRows; row++)
                {
                    for (int col = 0; col < slotColumns; col++)
                    {
                        float nodeX = _inventorySlotArea.X + (slotSize / 2f) + (col * spaceBetweenX);
                        float nodeY = _inventorySlotArea.Y + (slotSize / 2f) + (row * spaceBetweenY);
                        var position = new Vector2(MathF.Round(nodeX), MathF.Round(nodeY));
                        var bounds = new Rectangle((int)(position.X - slotSize / 2f), (int)(position.Y - slotSize / 2f), slotSize, slotSize);

                        var slot = new InventorySlot(bounds, slotFrames);
                        slot.OnClick += () => {
                            if (slot.HasItem)
                            {
                                foreach (var s in _inventorySlots) s.IsSelected = false;
                                slot.IsSelected = true;
                                _selectedSlotIndex = _inventorySlots.IndexOf(slot);
                            }
                        };
                        _inventorySlots.Add(slot);
                    }
                }
            }

            RefreshInventorySlots();

            var leftArrowRects = _spriteManager.InventoryLeftArrowButtonSourceRects;
            var rightArrowRects = _spriteManager.InventoryRightArrowButtonSourceRects;

            _debugButton1 = new ImageButton(new Rectangle(0, 0, 5, 5), _spriteManager.InventoryLeftArrowButton, leftArrowRects[0], leftArrowRects[1]);
            _debugButton1.OnClick += () =>
            {
                ChangePage(-1);
            };

            _debugButton2 = new ImageButton(new Rectangle(0, 0, 5, 5), _spriteManager.InventoryRightArrowButton, rightArrowRects[0], rightArrowRects[1]);
            _debugButton2.OnClick += () =>
            {
                ChangePage(1);
            };

            // Initialize Relic Equip Button
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var equipHoverSprite = _spriteManager.InventoryEquipHoverSprite;
            int equipButtonX = (Global.VIRTUAL_WIDTH - 180) / 2 - 60;
            int equipButtonY = 250 + 19 + 16;
            _relicEquipButton = new EquipButton(new Rectangle(equipButtonX, equipButtonY, 180, 16), "NOTHING");
            _relicEquipButton.TitleText = "RELIC";
            _relicEquipButton.ShowTitleOnHoverOnly = false; // Always visible
            _relicEquipButton.Font = secondaryFont;
            _relicEquipButton.OnClick += () => {
                OpenEquipSubmenu();
            };

            // Initialize Submenu Buttons
            _equipSubmenuButtons.Clear();
            int submenuStartY = equipButtonY - 32;

            for (int i = 0; i < 7; i++)
            {
                int yPos = submenuStartY + (i * 16);
                var button = new EquipButton(new Rectangle(equipButtonX, yPos, 180, 16), "");
                button.TitleText = "SELECT";
                button.ShowTitleOnHoverOnly = true; // Only visible on hover
                button.Font = secondaryFont;
                button.IsEnabled = false; // Disabled by default
                _equipSubmenuButtons.Add(button);
            }
        }

        private RelicData? GetRelicData(string relicId)
        {
            // BattleDataCache dictionaries are now case-insensitive, so direct lookup is safe.
            if (BattleDataCache.Relics.TryGetValue(relicId, out var data)) return data;
            return null;
        }

        private void OpenEquipSubmenu()
        {
            _isEquipSubmenuOpen = true;
            _equipMenuScrollIndex = 0; // Reset scroll on open
            RefreshEquipSubmenuButtons();
        }

        private void RefreshEquipSubmenuButtons()
        {
            // 1. Get available relics from inventory
            var availableRelics = _gameState.PlayerState.Relics.Keys.ToList();

            // Total virtual items = 1 (REMOVE button) + number of relics
            int totalItems = 1 + availableRelics.Count;

            for (int i = 0; i < _equipSubmenuButtons.Count; i++)
            {
                var btn = _equipSubmenuButtons[i];
                int virtualIndex = _equipMenuScrollIndex + i;

                // Reset button state
                btn.IsEnabled = false;
                btn.MainText = "";
                btn.IconTexture = null;
                btn.IconSilhouette = null;
                btn.OnClick = null;

                // Apply color pattern
                if (i % 2 == 0)
                {
                    btn.CustomDefaultTextColor = _global.Palette_White;
                    btn.CustomTitleTextColor = _global.Palette_White;
                }
                else
                {
                    btn.CustomDefaultTextColor = _global.Palette_BrightWhite;
                    btn.CustomTitleTextColor = _global.Palette_BrightWhite;
                }

                if (virtualIndex == 0)
                {
                    // This is the "REMOVE" button
                    btn.MainText = "REMOVE";
                    btn.CustomDefaultTextColor = _global.Palette_Red; // Override Main text only
                    // CustomTitleTextColor remains patternColor (White)
                    btn.IconTexture = null;
                    btn.IconSilhouette = null; // Clear silhouette
                    btn.IsEnabled = true;
                    btn.OnClick = () => SelectEquipItem(null);
                }
                else if (virtualIndex < totalItems)
                {
                    // This is a relic item
                    int relicIndex = virtualIndex - 1;
                    string relicId = availableRelics[relicIndex];
                    var relicData = GetRelicData(relicId);

                    if (relicData != null)
                    {
                        btn.MainText = relicData.RelicName.ToUpper();
                        string path = $"Sprites/Items/Relics/{relicData.RelicID}";
                        btn.IconTexture = _spriteManager.GetSmallRelicSprite(path);
                        btn.IconSilhouette = _spriteManager.GetSmallRelicSpriteSilhouette(path);
                        btn.IconSourceRect = null; // Use full texture
                        btn.IsEnabled = true;
                        btn.CustomDefaultTextColor = null; // Reset to default white
                        btn.OnClick = () => SelectEquipItem(relicId);
                    }
                    else
                    {
                        // Fallback if data missing (should be caught by GameState validation, but safe to handle)
                        btn.MainText = relicId.ToUpper();
                        btn.IconTexture = null;
                        btn.IconSilhouette = null;
                        btn.IsEnabled = true;
                        btn.CustomDefaultTextColor = null;
                        btn.OnClick = () => SelectEquipItem(relicId);
                    }
                }
                // Else: button remains disabled/blank
            }
        }

        /// <summary>
        /// Cancels any active equip selection submenu, reverting the UI to the standard equip view.
        /// This acts as a failsafe when switching categories or closing the inventory.
        /// </summary>
        private void CancelEquipSelection()
        {
            if (_isEquipSubmenuOpen)
            {
                _isEquipSubmenuOpen = false;
                // Future logic: Reset any temporary selection states here if needed
            }
        }

        private void SelectEquipItem(string? itemId)
        {
            // Equip the item in slot 0 (currently hardcoded for this button)
            _gameState.PlayerState.EquippedRelics[0] = itemId;

            // Close submenu
            _isEquipSubmenuOpen = false;

            // Refresh the main button text
            if (_relicEquipButton != null)
            {
                string name = "NOTHING";
                if (!string.IsNullOrEmpty(itemId))
                {
                    var data = GetRelicData(itemId);
                    if (data != null) name = data.RelicName.ToUpper();
                    else name = itemId.ToUpper();
                }
                _relicEquipButton.MainText = name;
            }
        }

        private void ToggleInventory()
        {
            IsOpen = !IsOpen;

            if (IsOpen)
            {
                _inventoryButton?.SetSprites(_spriteManager.SplitMapCloseInventoryButton, _spriteManager.SplitMapCloseInventoryButtonSourceRects[0], _spriteManager.SplitMapCloseInventoryButtonSourceRects[1]);
                RefreshInventorySlots();
            }
            else
            {
                _inventoryButton?.SetSprites(_spriteManager.SplitMapInventoryButton, _spriteManager.SplitMapInventoryButtonSourceRects[0], _spriteManager.SplitMapInventoryButtonSourceRects[1]);
                CancelEquipSelection(); // Ensure submenu is closed when inventory closes
            }

            OnInventoryToggled?.Invoke(IsOpen);
        }

        private void RefreshInventorySlots()
        {
            _selectedHeaderBobTimer = 0f; // Reset bob timer to sync animation on change

            foreach (var slot in _inventorySlots) slot.Clear();

            var items = GetCurrentCategoryItems();

            // Pagination Logic
            int startIndex = _currentPage * ITEMS_PER_PAGE;
            int itemsToDisplay = Math.Min(ITEMS_PER_PAGE, items.Count - startIndex);

            for (int i = 0; i < itemsToDisplay; i++)
            {
                var item = items[startIndex + i];
                _inventorySlots[i].AssignItem(item.Name, item.Quantity, item.IconPath, item.IconTint);
            }
        }

        private List<(string Name, int Quantity, string? IconPath, int? Uses, Color? IconTint)> GetCurrentCategoryItems()
        {
            var currentItems = new List<(string Name, int Quantity, string? IconPath, int? Uses, Color? IconTint)>();
            switch (_selectedInventoryCategory)
            {
                case InventoryCategory.Weapons:
                    foreach (var kvp in _gameState.PlayerState.Weapons) currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Weapons/{kvp.Key}", null, null));
                    break;
                case InventoryCategory.Armor:
                    foreach (var kvp in _gameState.PlayerState.Armors) currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Armor/{kvp.Key}", null, null));
                    break;
                case InventoryCategory.Relics:
                    foreach (var kvp in _gameState.PlayerState.Relics)
                    {
                        if (BattleDataCache.Relics.TryGetValue(kvp.Key, out var data))
                            currentItems.Add((data.RelicName, kvp.Value, $"Sprites/Items/Relics/{data.RelicID}", null, null));
                        else
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Relics/{kvp.Key}", null, null));
                    }
                    break;
                case InventoryCategory.Consumables:
                    foreach (var kvp in _gameState.PlayerState.Consumables)
                    {
                        if (BattleDataCache.Consumables.TryGetValue(kvp.Key, out var data))
                            currentItems.Add((data.ItemName, kvp.Value, data.ImagePath, null, null));
                        else
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Consumables/{kvp.Key}", null, null));
                    }
                    break;
                case InventoryCategory.Spells:
                    foreach (var entry in _gameState.PlayerState.Spells)
                    {
                        Color? tint = null;
                        string name = entry.MoveID;
                        string iconPath = $"Sprites/Items/Spells/{entry.MoveID}";

                        if (BattleDataCache.Moves.TryGetValue(entry.MoveID, out var moveData))
                        {
                            name = moveData.MoveName;
                            int elementId = moveData.OffensiveElementIDs.FirstOrDefault();
                            if (_global.ElementColors.TryGetValue(elementId, out var elementColor))
                            {
                                tint = elementColor;
                            }
                        }
                        currentItems.Add((name, 1, iconPath, null, tint));
                    }
                    break;
            }
            return currentItems;
        }

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
                _inventoryBobTimer = 0f;
                if (slotFrames != null)
                {
                    foreach (var slot in _inventorySlots) slot.RandomizeFrame();
                }
            }
            _previousInventoryCategory = _selectedInventoryCategory;

            if (_inventoryArrowAnimTimer < INVENTORY_ARROW_ANIM_DURATION) _inventoryArrowAnimTimer += deltaTime;
            if (_inventoryBobTimer < INVENTORY_BOB_DURATION)
            {
                _inventoryBobTimer += deltaTime;
                float bobProgress = Math.Clamp(_inventoryBobTimer / INVENTORY_BOB_DURATION, 0f, 1f);
                _inventoryPositionOffset.Y = -MathF.Sin(bobProgress * MathHelper.Pi) * 1f;
            }

            // Update Selected Header Bob Timer
            _selectedHeaderBobTimer += deltaTime;
            // Speed reduced to 2.5f (half of 5f).
            // Using (Sin + 1) / 2 ensures a smooth, symmetric 0-1 oscillation, which Round turns into an equal-duration toggle.
            float selectedBobOffset = MathF.Round((MathF.Sin(_selectedHeaderBobTimer * 2.5f) + 1f) * 0.5f);

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
            bool scrollUp = scrollDelta > 0 && headerArea.Contains(mouseInWorldSpace);
            bool scrollDown = scrollDelta < 0 && headerArea.Contains(mouseInWorldSpace);

            // Handle Submenu Scrolling
            if (_isEquipSubmenuOpen && scrollDelta != 0)
            {
                int totalItems = 1 + _gameState.PlayerState.Relics.Count; // 1 for REMOVE
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
                    CycleCategory(-1);
                }
                else if (scrollDown)
                {
                    CycleCategory(1);
                }
            }

            // Update Header Buttons
            int selectedIndex = (int)_selectedInventoryCategory;
            const float repulsionAmount = 8f;
            const float repulsionSpeed = 15f;
            int numButtons = _inventoryHeaderButtons.Count;
            float rawOffsetFirst = 0f;
            float rawOffsetLast = 0f;

            if (numButtons > 0)
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
                if (i < selectedIndex) targetOffset = -repulsionAmount;
                else if (i > selectedIndex) targetOffset = repulsionAmount;
                targetOffset += centeringCorrection;

                float currentOffset = _inventoryHeaderButtonOffsets[button];
                _inventoryHeaderButtonOffsets[button] = MathHelper.Lerp(currentOffset, targetOffset, repulsionSpeed * deltaTime);
                float finalOffset = _inventoryHeaderButtonOffsets[button];

                var baseBounds = _inventoryHeaderButtonBaseBounds[button];

                button.IsSelected = ((int)_selectedInventoryCategory == button.MenuIndex);
                // Removed bobY from header buttons

                button.Bounds = new Rectangle(
                    baseBounds.X + (int)MathF.Round(finalOffset),
                    baseBounds.Y + (int)MathF.Round(_inventoryPositionOffset.Y), // Only global bob
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
                // Removed equipBobY from equip button

                _inventoryEquipButton.Bounds = new Rectangle((int)equipBaseX, (int)(equipBaseY + _inventoryPositionOffset.Y), 32, 32); // Only global bob
                _inventoryEquipButton.Update(currentMouseState, cameraTransform);
            }

            // Update Debug Buttons Position
            if (selectedButton != null && _debugButton1 != null && _debugButton2 != null)
            {
                float progress = Math.Clamp(_inventoryArrowAnimTimer / INVENTORY_ARROW_ANIM_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutCubic(progress);
                float currentOffset = MathHelper.Lerp(16f, 13f, easedProgress);
                var selectedBounds = selectedButton.Bounds;

                _debugButton1.Bounds = new Rectangle(selectedBounds.Center.X - (int)currentOffset - (_debugButton1.Bounds.Width / 2), selectedBounds.Center.Y - _debugButton1.Bounds.Height / 2 - 2, _debugButton1.Bounds.Width, _debugButton1.Bounds.Height);
                _debugButton2.Bounds = new Rectangle(selectedBounds.Center.X + (int)currentOffset - (_debugButton2.Bounds.Width / 2), selectedBounds.Center.Y - _debugButton2.Bounds.Height / 2 - 2, _debugButton2.Bounds.Width, _debugButton2.Bounds.Height);

                _debugButton1.IsEnabled = (int)_selectedInventoryCategory > 0 && _selectedInventoryCategory != InventoryCategory.Equip;
                _debugButton2.IsEnabled = (int)_selectedInventoryCategory < (int)InventoryCategory.Consumables && _selectedInventoryCategory != InventoryCategory.Equip;
            }

            // Update Slots or Equip UI
            if (_selectedInventoryCategory != InventoryCategory.Equip)
            {
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
            }
            else if (_selectedInventoryCategory == InventoryCategory.Equip)
            {
                if (_isEquipSubmenuOpen)
                {
                    foreach (var button in _equipSubmenuButtons)
                    {
                        button.Update(currentMouseState, cameraTransform);
                    }
                }
                else
                {
                    if (_relicEquipButton != null)
                    {
                        _relicEquipButton.Update(currentMouseState, cameraTransform);
                    }
                }
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        private void ChangePage(int direction)
        {
            int totalItems = GetCurrentCategoryItems().Count;
            int maxPage = Math.Max(0, (int)Math.Ceiling((double)totalItems / ITEMS_PER_PAGE) - 1);

            _currentPage += direction;
            _currentPage = Math.Clamp(_currentPage, 0, maxPage);
            RefreshInventorySlots();
        }

        private void CycleCategory(int direction)
        {
            int currentIndex = (int)_selectedInventoryCategory;
            int newIndex = currentIndex + direction;

            // Clamp to valid range [0, Consumables], excluding Equip
            if (newIndex >= 0 && newIndex <= (int)InventoryCategory.Consumables)
            {
                CancelEquipSelection(); // Failsafe
                _selectedInventoryCategory = (InventoryCategory)newIndex;
                _currentPage = 0; // Reset page on category change
                RefreshInventorySlots();
            }
        }

        public void DrawWorld(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsOpen) return;

            float bobOffset = 0f;
            if (_inventoryBobTimer < INVENTORY_BOB_DURATION)
            {
                float bobProgress = Math.Clamp(_inventoryBobTimer / INVENTORY_BOB_DURATION, 0f, 1f);
                bobOffset = -MathF.Sin(bobProgress * MathHelper.Pi) * 1f;
            }

            var inventoryPosition = new Vector2(0, 200 + bobOffset);
            var headerPosition = inventoryPosition + _inventoryPositionOffset;

            spriteBatch.DrawSnapped(_spriteManager.InventoryBorderHeader, headerPosition, Color.White);

            Texture2D selectedBorderSprite;
            if (_selectedInventoryCategory == InventoryCategory.Equip && _isEquipSubmenuOpen)
            {
                selectedBorderSprite = _spriteManager.InventoryBorderEquipSubmenu;
            }
            else
            {
                selectedBorderSprite = _selectedInventoryCategory switch
                {
                    InventoryCategory.Weapons => _spriteManager.InventoryBorderWeapons,
                    InventoryCategory.Armor => _spriteManager.InventoryBorderArmor,
                    InventoryCategory.Spells => _spriteManager.InventoryBorderSpells,
                    InventoryCategory.Relics => _spriteManager.InventoryBorderRelics,
                    InventoryCategory.Consumables => _spriteManager.InventoryBorderConsumables,
                    InventoryCategory.Equip => _spriteManager.InventoryBorderEquip,
                    _ => _spriteManager.InventoryBorderWeapons,
                };
            }
            spriteBatch.DrawSnapped(selectedBorderSprite, inventoryPosition, Color.White);

            foreach (var button in _inventoryHeaderButtons) button.Draw(spriteBatch, font, gameTime, Matrix.Identity);

            if (_selectedInventoryCategory != InventoryCategory.Equip)
            {
                foreach (var slot in _inventorySlots) slot.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }
            else
            {
                if (_isEquipSubmenuOpen)
                {
                    foreach (var button in _equipSubmenuButtons)
                    {
                        button.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    }

                    // Draw Scroll Arrows
                    var arrowTexture = _spriteManager.InventoryScrollArrowsSprite;
                    var arrowRects = _spriteManager.InventoryScrollArrowRects;

                    if (arrowTexture != null && arrowRects != null && _equipSubmenuButtons.Count > 0)
                    {
                        int totalItems = 1 + _gameState.PlayerState.Relics.Count;
                        int maxScroll = Math.Max(0, totalItems - 7);

                        // Up Arrow
                        if (_equipMenuScrollIndex > 0)
                        {
                            var firstButton = _equipSubmenuButtons[0];
                            var arrowPos = new Vector2(
                                firstButton.Bounds.Center.X - arrowRects[0].Width / 2f,
                                firstButton.Bounds.Top - arrowRects[0].Height
                            );
                            spriteBatch.DrawSnapped(arrowTexture, arrowPos, arrowRects[0], Color.White);
                        }

                        // Down Arrow
                        if (_equipMenuScrollIndex < maxScroll)
                        {
                            var lastButton = _equipSubmenuButtons.Last();
                            var arrowPos = new Vector2(
                                lastButton.Bounds.Center.X - arrowRects[1].Width / 2f,
                                lastButton.Bounds.Bottom
                            );
                            spriteBatch.DrawSnapped(arrowTexture, arrowPos, arrowRects[1], Color.White);
                        }
                    }
                }
                else
                {
                    if (_relicEquipButton != null)
                    {
                        _relicEquipButton.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    }
                    // Draw Debug Stats Panel (invisible now)
                    // var pixel = ServiceLocator.Get<Texture2D>();
                    // spriteBatch.DrawSnapped(pixel, _statsPanelArea, Color.HotPink);
                }
            }

            if (_debugButton1 != null && _debugButton1.IsEnabled) _debugButton1.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            if (_debugButton2 != null && _debugButton2.IsEnabled) _debugButton2.Draw(spriteBatch, font, gameTime, Matrix.Identity);

            if (_global.ShowSplitMapGrid)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                spriteBatch.DrawSnapped(pixel, _inventorySlotArea, Color.Blue * 0.5f);
            }

            _inventoryEquipButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
        }

        public void DrawScreen(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Only draw the toggle button here
            _inventoryButton?.Draw(spriteBatch, font, gameTime, transform);
        }
    }
}
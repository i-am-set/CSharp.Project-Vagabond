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
using System.Text;
using System.Text.RegularExpressions;

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
        private readonly HapticsManager _hapticsManager;
        private ImageButton? _inventoryButton;
        private readonly List<InventoryHeaderButton> _inventoryHeaderButtons = new();
        private readonly Dictionary<InventoryHeaderButton, float> _inventoryHeaderButtonOffsets = new();
        private readonly Dictionary<InventoryHeaderButton, Rectangle> _inventoryHeaderButtonBaseBounds = new();
        private InventoryHeaderButton? _inventoryEquipButton;
        private readonly List<InventorySlot> _inventorySlots = new();
        private Rectangle _inventorySlotArea;

        // Panels
        private Rectangle _statsPanelArea;
        private Rectangle _infoPanelArea;

        private ImageButton? _debugButton1;
        private ImageButton? _debugButton2;
        private ImageButton? _pageLeftButton;
        private ImageButton? _pageRightButton;
        private EquipButton? _relicEquipButton;

        // Submenu State
        private bool _isEquipSubmenuOpen = false;
        private readonly List<EquipButton> _equipSubmenuButtons = new();
        private int _equipMenuScrollIndex = 0;

        // Pagination State
        private int _currentPage = 0;
        private int _totalPages = 0;
        private const int ITEMS_PER_PAGE = 12;

        private InventoryCategory _selectedInventoryCategory;
        private InventoryCategory _previousInventoryCategory;
        private int _selectedSlotIndex = -1;

        // Animation State
        private float _inventoryArrowAnimTimer;
        private const float INVENTORY_ARROW_ANIM_DURATION = 0.2f;

        private Vector2 _inventoryPositionOffset = Vector2.Zero;
        private float _selectedHeaderBobTimer;

        // Page Arrow Animation State
        private float _leftPageArrowBobTimer = 0f;
        private float _rightPageArrowBobTimer = 0f;
        private const float PAGE_ARROW_BOB_DURATION = 0.05f;

        // Stat Cycle Animation State
        private float _statCycleTimer = 0f;
        private RelicData? _previousHoveredRelicData;
        private const float STAT_CYCLE_INTERVAL = 1.0f;

        // Input State
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        // Hover Data
        private RelicData? _hoveredRelicData;

        // Text Formatting Tuning
        private const int SPACE_WIDTH = 5;

        public SplitMapInventoryOverlay()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
        }

        public void Initialize()
        {
            InitializeInventoryUI();
            _previousInventoryCategory = _selectedInventoryCategory;
            _inventoryArrowAnimTimer = INVENTORY_ARROW_ANIM_DURATION;
            _inventoryPositionOffset = Vector2.Zero;
            _selectedHeaderBobTimer = 0f;
            _statCycleTimer = 0f;
            _previousHoveredRelicData = null;
            _leftPageArrowBobTimer = 0f;
            _rightPageArrowBobTimer = 0f;

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
                button.OnClick += () => SwitchToCategory(category);
                _inventoryHeaderButtons.Add(button);
                _inventoryHeaderButtonOffsets[button] = 0f;
                _inventoryHeaderButtonBaseBounds[button] = bounds;
            }

            // Initialize Equip Button
            var equipRects = _spriteManager.InventoryHeaderButtonSourceRects;
            float equipX = startX - 60f;
            var equipBounds = new Rectangle((int)equipX, (int)buttonY, 32, 32);
            _inventoryEquipButton = new InventoryHeaderButton(equipBounds, _spriteManager.InventoryHeaderButtonEquip, equipRects[0], equipRects[1], equipRects[2], (int)InventoryCategory.Equip, "Equip");
            _inventoryEquipButton.OnClick += () => SwitchToCategory(InventoryCategory.Equip);

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

            // Initialize Info Panel Area (Identical to Stats Panel)
            _infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);

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
                        slot.OnClick += () =>
                        {
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
                CycleCategory(-1);
            };

            _debugButton2 = new ImageButton(new Rectangle(0, 0, 5, 5), _spriteManager.InventoryRightArrowButton, rightArrowRects[0], rightArrowRects[1]);
            _debugButton2.OnClick += () =>
            {
                CycleCategory(1);
            };

            // Initialize Page Navigation Buttons
            _pageLeftButton = new ImageButton(new Rectangle(0, 0, 5, 5), _spriteManager.InventoryLeftArrowButton, leftArrowRects[0], leftArrowRects[1]);
            _pageLeftButton.OnClick += () => ChangePage(-1);

            _pageRightButton = new ImageButton(new Rectangle(0, 0, 5, 5), _spriteManager.InventoryRightArrowButton, rightArrowRects[0], rightArrowRects[1]);
            _pageRightButton.OnClick += () => ChangePage(1);

            // Initialize Relic Equip Button
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var equipHoverSprite = _spriteManager.InventoryEquipHoverSprite;
            int equipButtonX = (Global.VIRTUAL_WIDTH - 180) / 2 - 60;
            int equipButtonY = 250 + 19 + 16;
            _relicEquipButton = new EquipButton(new Rectangle(equipButtonX, equipButtonY, 180, 16), "NOTHING");
            _relicEquipButton.TitleText = "RELIC";
            _relicEquipButton.ShowTitleOnHoverOnly = false; // Always visible
            _relicEquipButton.Font = secondaryFont;
            _relicEquipButton.OnClick += () =>
            {
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
            if (BattleDataCache.Relics.TryGetValue(relicId, out var data)) return data;
            return null;
        }

        private void OpenEquipSubmenu()
        {
            _isEquipSubmenuOpen = true;
            _equipMenuScrollIndex = 0;
            RefreshEquipSubmenuButtons();
        }

        private void RefreshEquipSubmenuButtons()
        {
            var availableRelics = _gameState.PlayerState.Relics.Keys.ToList();
            int totalItems = 1 + availableRelics.Count;

            for (int i = 0; i < _equipSubmenuButtons.Count; i++)
            {
                var btn = _equipSubmenuButtons[i];
                int virtualIndex = _equipMenuScrollIndex + i;

                btn.IsEnabled = false;
                btn.MainText = "";
                btn.IconTexture = null;
                btn.IconSilhouette = null;
                btn.OnClick = null;

                if (i % 2 == 0)
                {
                    btn.CustomDefaultTextColor = _global.Palette_BrightWhite;
                    btn.CustomTitleTextColor = _global.Palette_BrightWhite;
                }
                else
                {
                    btn.CustomDefaultTextColor = _global.Palette_White;
                    btn.CustomTitleTextColor = _global.Palette_White;
                }

                if (virtualIndex == 0)
                {
                    btn.MainText = "REMOVE";
                    btn.CustomDefaultTextColor = _global.Palette_Red;
                    btn.IconTexture = null;
                    btn.IconSilhouette = null;
                    btn.IsEnabled = true;
                    btn.OnClick = () => SelectEquipItem(null);
                }
                else if (virtualIndex < totalItems)
                {
                    int relicIndex = virtualIndex - 1;
                    string relicId = availableRelics[relicIndex];
                    var relicData = GetRelicData(relicId);

                    if (relicData != null)
                    {
                        btn.MainText = relicData.RelicName.ToUpper();
                        string path = $"Sprites/Items/Relics/{relicData.RelicID}";
                        btn.IconTexture = _spriteManager.GetSmallRelicSprite(path);
                        btn.IconSilhouette = _spriteManager.GetSmallRelicSpriteSilhouette(path);
                        btn.IconSourceRect = null;
                        btn.IsEnabled = true;
                        btn.OnClick = () => SelectEquipItem(relicId);
                    }
                    else
                    {
                        btn.MainText = relicId.ToUpper();
                        btn.IconTexture = null;
                        btn.IconSilhouette = null;
                        btn.IsEnabled = true;
                        btn.OnClick = () => SelectEquipItem(relicId);
                    }
                }
            }
        }

        private void CancelEquipSelection()
        {
            if (_isEquipSubmenuOpen)
            {
                _isEquipSubmenuOpen = false;
                _hoveredRelicData = null;
            }
        }

        private void SelectEquipItem(string? itemId)
        {
            _gameState.PlayerState.EquippedRelics[0] = itemId;
            _isEquipSubmenuOpen = false;
            _hoveredRelicData = null;

            _hapticsManager.TriggerShake(4f, 0.1f, true, 2f);

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
                if (_selectedInventoryCategory != InventoryCategory.Equip)
                {
                    TriggerSlotAnimations();
                }
            }
            else
            {
                _inventoryButton?.SetSprites(_spriteManager.SplitMapInventoryButton, _spriteManager.SplitMapInventoryButtonSourceRects[0], _spriteManager.SplitMapInventoryButtonSourceRects[1]);
                CancelEquipSelection();
                _selectedSlotIndex = -1;
            }

            OnInventoryToggled?.Invoke(IsOpen);
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

        private void RefreshInventorySlots()
        {
            foreach (var slot in _inventorySlots) slot.Clear();

            var items = GetCurrentCategoryItems();

            int totalItems = items.Count;
            _totalPages = (int)Math.Ceiling((double)totalItems / ITEMS_PER_PAGE);

            int startIndex = _currentPage * ITEMS_PER_PAGE;
            int itemsToDisplay = Math.Min(ITEMS_PER_PAGE, items.Count - startIndex);

            for (int i = 0; i < itemsToDisplay; i++)
            {
                var item = items[startIndex + i];
                _inventorySlots[i].AssignItem(item.Name, item.Quantity, item.IconPath, item.IconTint);

                if (_selectedSlotIndex == i)
                {
                    _inventorySlots[i].IsSelected = true;
                }
            }
        }

        private List<(string Name, int Quantity, string? IconPath, int? Uses, Color? IconTint)> GetCurrentCategoryItems()
        {
            var currentItems = new List<(string Name, int Quantity, string? IconPath, int? Uses, Color? IconTint)>();
            switch (_selectedInventoryCategory)
            {
                case InventoryCategory.Weapons:
                    foreach (var kvp in _gameState.PlayerState.Weapons)
                    {
                        if (BattleDataCache.Weapons.TryGetValue(kvp.Key, out var weaponData))
                        {
                            currentItems.Add((weaponData.WeaponName, kvp.Value, $"Sprites/Items/Weapons/{kvp.Key}", null, null));
                        }
                        else
                        {
                            currentItems.Add((kvp.Key, kvp.Value, $"Sprites/Items/Weapons/{kvp.Key}", null, null));
                        }
                    }
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

                _debugButton1.IsEnabled = (int)_selectedInventoryCategory > 0 && _selectedInventoryCategory != InventoryCategory.Equip;
                _debugButton2.IsEnabled = (int)_selectedInventoryCategory < (int)InventoryCategory.Consumables && _selectedInventoryCategory != InventoryCategory.Equip;
            }

            // Update Slots or Equip UI
            if (_selectedInventoryCategory != InventoryCategory.Equip)
            {
                _hoveredRelicData = null; // Ensure this is cleared when not in equip mode

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
                _hoveredRelicData = null; // Reset hover data each frame

                if (_isEquipSubmenuOpen)
                {
                    var availableRelics = _gameState.PlayerState.Relics.Keys.ToList();
                    for (int i = 0; i < _equipSubmenuButtons.Count; i++)
                    {
                        var button = _equipSubmenuButtons[i];
                        button.Update(currentMouseState, cameraTransform);

                        if (button.IsHovered && button.IsEnabled)
                        {
                            int virtualIndex = _equipMenuScrollIndex + i;
                            // Index 0 is "REMOVE", so relics start at index 1
                            if (virtualIndex > 0)
                            {
                                int relicIndex = virtualIndex - 1;
                                if (relicIndex < availableRelics.Count)
                                {
                                    string relicId = availableRelics[relicIndex];
                                    _hoveredRelicData = GetRelicData(relicId);
                                }
                            }
                        }
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

            // Update Stat Cycle Timer
            _statCycleTimer += deltaTime;
            if (_hoveredRelicData != _previousHoveredRelicData)
            {
                _statCycleTimer = 0f;
                _previousHoveredRelicData = _hoveredRelicData;
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        private void ChangePage(int direction)
        {
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

            // Handle Main Categories (0-4)
            int currentIndex = (int)_selectedInventoryCategory;
            int newIndex = currentIndex + direction;

            // Standard navigation within Weapons-Consumables
            if (newIndex >= 0 && newIndex <= (int)InventoryCategory.Consumables)
            {
                SwitchToCategory((InventoryCategory)newIndex);
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

        public void DrawWorld(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsOpen) return;

            var inventoryPosition = new Vector2(0, 200);
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

                // Draw Page Counter
                if (_totalPages > 1)
                {
                    var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                    string pageText = $"{_currentPage + 1}/{_totalPages}";
                    var textSize = secondaryFont.MeasureString(pageText);
                    var textPos = new Vector2(
                        _inventorySlotArea.Center.X - textSize.Width / 2f,
                        _inventorySlotArea.Bottom - 2
                    );

                    // Draw background rectangle
                    var pixel = ServiceLocator.Get<Texture2D>();
                    var bgRect = new Rectangle(
                        (int)textPos.X - 1,
                        (int)textPos.Y + 2,
                        (int)Math.Ceiling(textSize.Width) + 5,
                        (int)textSize.Height
                    );
                    spriteBatch.DrawSnapped(pixel, bgRect, _global.Palette_Black);

                    spriteBatch.DrawStringSnapped(secondaryFont, pageText, textPos, _global.Palette_BrightWhite);

                    _pageLeftButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    _pageRightButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                }

                // Draw Info Panel
                DrawInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont);
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
                }

                // Draw Stats Panel (Moved here to show in both main equip view and submenu)
                DrawStatsPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont);
            }

            if (_debugButton1 != null && _debugButton1.IsEnabled) _debugButton1.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            if (_debugButton2 != null && _debugButton2.IsEnabled) _debugButton2.Draw(spriteBatch, font, gameTime, Matrix.Identity);

            if (_global.ShowSplitMapGrid)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                spriteBatch.DrawSnapped(pixel, _inventorySlotArea, Color.Blue * 0.5f);

                // Debug Draw for Panels
                if (_selectedInventoryCategory == InventoryCategory.Equip)
                {
                    spriteBatch.DrawSnapped(pixel, _statsPanelArea, Color.HotPink * 0.5f);
                }
                else
                {
                    spriteBatch.DrawSnapped(pixel, _infoPanelArea, Color.Cyan * 0.5f);
                }
            }

            _inventoryEquipButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
        }

        private void DrawInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont)
        {
            // Find the active slot (Selected takes precedence over Hovered)
            InventorySlot? activeSlot = _inventorySlots.FirstOrDefault(s => s.IsSelected);
            if (activeSlot == null)
            {
                activeSlot = _inventorySlots.FirstOrDefault(s => s.IsHovered);
            }

            if (activeSlot == null || !activeSlot.HasItem || string.IsNullOrEmpty(activeSlot.ItemId)) return;

            // Retrieve Item Data based on Category
            string name = activeSlot.ItemId.ToUpper();
            string description = "";
            string iconPath = activeSlot.IconPath ?? "";
            Texture2D? iconTexture = null;
            Texture2D? iconSilhouette = null;

            // Attempt to fetch rich data
            if (_selectedInventoryCategory == InventoryCategory.Relics)
            {
                // Try to find by name if ID lookup fails (since slot stores Name)
                var relic = BattleDataCache.Relics.Values.FirstOrDefault(r => r.RelicName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (relic != null)
                {
                    name = relic.RelicName.ToUpper();
                    description = relic.Description.ToUpper();
                    iconPath = $"Sprites/Items/Relics/{relic.RelicID}";
                }
            }
            else if (_selectedInventoryCategory == InventoryCategory.Consumables)
            {
                var item = BattleDataCache.Consumables.Values.FirstOrDefault(c => c.ItemName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    name = item.ItemName.ToUpper();
                    description = item.Description.ToUpper();
                    iconPath = item.ImagePath;
                }
            }
            else if (_selectedInventoryCategory == InventoryCategory.Spells)
            {
                var move = BattleDataCache.Moves.Values.FirstOrDefault(m => m.MoveName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (move != null)
                {
                    name = move.MoveName.ToUpper();
                    description = move.Description.ToUpper();
                    iconPath = $"Sprites/Items/Spells/{move.MoveID}";
                }
            }
            // UPDATED: Handle Weapons Category
            else if (_selectedInventoryCategory == InventoryCategory.Weapons)
            {
                var weapon = BattleDataCache.Weapons.Values.FirstOrDefault(w => w.WeaponName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (weapon != null)
                {
                    name = weapon.WeaponName.ToUpper();
                    description = weapon.Description.ToUpper();
                    iconPath = $"Sprites/Items/Weapons/{weapon.WeaponID}";
                }
            }

            // Load Icon
            if (!string.IsNullOrEmpty(iconPath))
            {
                iconTexture = _spriteManager.GetItemSprite(iconPath);
                iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath);
            }

            // --- Drawing Logic ---
            const int spriteSize = 32;
            const int gap = 4;

            // 1. Calculate Total Height for Centering
            int maxTitleWidth = _infoPanelArea.Width - (4 * 2);
            var titleLines = ParseAndWrapRichText(font, name, maxTitleWidth, _global.Palette_BrightWhite);
            float totalTitleHeight = titleLines.Count * font.LineHeight;

            float totalDescHeight = 0f;
            List<List<ColoredText>> descLines = new List<List<ColoredText>>();
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = _infoPanelArea.Width - (4 * 2);
                descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);
                totalDescHeight = descLines.Count * secondaryFont.LineHeight;
            }

            float totalContentHeight = spriteSize + gap + totalTitleHeight + (totalDescHeight > 0 ? gap + totalDescHeight : 0);

            // 2. Calculate Start Y
            float currentY = _infoPanelArea.Y + (_infoPanelArea.Height - totalContentHeight) / 2f;
            currentY -= 10f; // Move up 10 pixels as requested

            // 3. Draw Sprite
            int spriteX = _infoPanelArea.X + (_infoPanelArea.Width - spriteSize) / 2;

            if (iconSilhouette != null)
            {
                // Use global outline color for Idle state
                Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                // 1. Draw Diagonals (Corners) FIRST (Behind)
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX - 1, currentY - 1), cornerOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX + 1, currentY - 1), cornerOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX - 1, currentY + 1), cornerOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX + 1, currentY + 1), cornerOutlineColor);

                // 2. Draw Cardinals (Main) SECOND (On Top)
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX - 1, currentY), mainOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX + 1, currentY), mainOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX, currentY - 1), mainOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX, currentY + 1), mainOutlineColor);
            }

            if (iconTexture != null)
            {
                Color tint = activeSlot.IconTint ?? Color.White;
                spriteBatch.DrawSnapped(iconTexture, new Vector2(spriteX, currentY), tint);
            }

            currentY += spriteSize + gap;

            // 4. Draw Title
            foreach (var line in titleLines)
            {
                float lineWidth = 0;
                foreach (var segment in line)
                {
                    if (string.IsNullOrWhiteSpace(segment.Text))
                        lineWidth += segment.Text.Length * SPACE_WIDTH;
                    else
                        lineWidth += font.MeasureString(segment.Text).Width;
                }

                float lineX = _infoPanelArea.X + (_infoPanelArea.Width - lineWidth) / 2f;
                float currentX = lineX;

                foreach (var segment in line)
                {
                    float segWidth;
                    if (string.IsNullOrWhiteSpace(segment.Text))
                    {
                        segWidth = segment.Text.Length * SPACE_WIDTH;
                    }
                    else
                    {
                        segWidth = font.MeasureString(segment.Text).Width;
                        spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(currentX, currentY), segment.Color);
                    }
                    currentX += segWidth;
                }
                currentY += font.LineHeight;
            }

            // 5. Draw Description
            if (descLines.Any())
            {
                currentY += gap;
                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text))
                            lineWidth += segment.Text.Length * SPACE_WIDTH;
                        else
                            lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    var lineX = _infoPanelArea.X + (_infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text))
                        {
                            segWidth = segment.Text.Length * SPACE_WIDTH;
                        }
                        else
                        {
                            segWidth = secondaryFont.MeasureString(segment.Text).Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    currentY += secondaryFont.LineHeight;
                }
            }
        }

        private void DrawStatsPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont)
        {
            // --- Draw Hovered Item Details ---
            if (_hoveredRelicData != null)
            {
                const int padding = 2; // Reduced padding for "tightly against top"
                const int spriteSize = 32;
                const int gap = 4;

                // 1. Sprite (Top Left - Centered Horizontally)
                int spriteX = _statsPanelArea.X + (_statsPanelArea.Width - spriteSize) / 2;
                int spriteY = _statsPanelArea.Y; // Tightly against top
                string path = $"Sprites/Items/Relics/{_hoveredRelicData.RelicID}";
                var relicSprite = _spriteManager.GetRelicSprite(path);

                var relicSilhouette = _spriteManager.GetRelicSpriteSilhouette(path);
                if (relicSilhouette != null)
                {
                    // Use global outline color for Idle state
                    Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                    Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                    // 1. Draw Diagonals (Corners) FIRST (Behind)
                    spriteBatch.DrawSnapped(relicSilhouette, new Vector2(spriteX - 1, spriteY - 1), cornerOutlineColor);
                    spriteBatch.DrawSnapped(relicSilhouette, new Vector2(spriteX + 1, spriteY - 1), cornerOutlineColor);
                    spriteBatch.DrawSnapped(relicSilhouette, new Vector2(spriteX - 1, spriteY + 1), cornerOutlineColor);
                    spriteBatch.DrawSnapped(relicSilhouette, new Vector2(spriteX + 1, spriteY + 1), cornerOutlineColor);

                    // 2. Draw Cardinals (Main) SECOND (On Top)
                    spriteBatch.DrawSnapped(relicSilhouette, new Vector2(spriteX - 1, spriteY), mainOutlineColor);
                    spriteBatch.DrawSnapped(relicSilhouette, new Vector2(spriteX + 1, spriteY), mainOutlineColor);
                    spriteBatch.DrawSnapped(relicSilhouette, new Vector2(spriteX, spriteY - 1), mainOutlineColor);
                    spriteBatch.DrawSnapped(relicSilhouette, new Vector2(spriteX, spriteY + 1), mainOutlineColor);
                }

                if (relicSprite != null)
                {
                    spriteBatch.DrawSnapped(relicSprite, new Vector2(spriteX, spriteY), Color.White);
                }

                // 2. Title
                // Anchor: Bottom of the title block is fixed relative to the sprite bottom.
                float titleBottomY = spriteY + spriteSize + gap;

                string name = _hoveredRelicData.RelicName.ToUpper();
                int maxTitleWidth = _statsPanelArea.Width - (4 * 2); // 4px padding on sides

                var titleLines = ParseAndWrapRichText(font, name, maxTitleWidth, _global.Palette_BrightWhite);
                float totalTitleHeight = titleLines.Count * font.LineHeight;

                // Calculate start Y so the block ends at titleBottomY
                float currentTitleY = titleBottomY - totalTitleHeight;

                foreach (var line in titleLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text))
                            lineWidth += segment.Text.Length * SPACE_WIDTH;
                        else
                            lineWidth += font.MeasureString(segment.Text).Width;
                    }

                    // Center horizontally within the panel
                    float lineX = _statsPanelArea.X + (_statsPanelArea.Width - lineWidth) / 2f;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text))
                        {
                            segWidth = segment.Text.Length * SPACE_WIDTH;
                        }
                        else
                        {
                            segWidth = font.MeasureString(segment.Text).Width;
                            spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(currentX, currentTitleY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    currentTitleY += font.LineHeight;
                }

                // 3. Description
                // Area: From titleBottomY to statsStartY
                int statsStartY = _statsPanelArea.Y + 77; // Defined later for stats, used here for boundary
                float descAreaTop = titleBottomY;
                float descAreaBottom = statsStartY;
                float descAreaHeight = descAreaBottom - descAreaTop;

                float descWidth = _statsPanelArea.Width - (4 * 2); // 4px padding
                var descLines = ParseAndWrapRichText(secondaryFont, _hoveredRelicData.Description.ToUpper(), descWidth, _global.Palette_White);
                float totalDescHeight = descLines.Count * secondaryFont.LineHeight;

                // Center vertically in the area
                float currentDescY = descAreaTop + (descAreaHeight - totalDescHeight) / 2f;

                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text))
                            lineWidth += segment.Text.Length * SPACE_WIDTH;
                        else
                            lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    var lineX = _statsPanelArea.X + (_statsPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text))
                        {
                            segWidth = segment.Text.Length * SPACE_WIDTH;
                        }
                        else
                        {
                            segWidth = secondaryFont.MeasureString(segment.Text).Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentDescY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    currentDescY += secondaryFont.LineHeight;
                }
            }

            // --- Draw Stats ---
            var playerState = _gameState.PlayerState;
            if (playerState == null) return;

            var stats = new List<(string Label, string StatKey)>
{
    ("MAX HP", "MaxHP"),
    ("STRNTH", "Strength"),
    ("INTELL", "Intelligence"),
    ("TENACT", "Tenacity"),
    ("AGILTY", "Agility")
};

            int startX = _statsPanelArea.X + 3;
            int startY = _statsPanelArea.Y + 77;
            int rowSpacing = 10;

            int val1RightX = 63;
            int arrowX = 66;
            int val2RightX = 107;

            // Helper to get modifier from an equipped slot
            int GetEquippedModifier(int slotIndex, string statKey)
            {
                if (slotIndex >= playerState.EquippedRelics.Length) return 0;
                string? relicId = playerState.EquippedRelics[slotIndex];
                if (string.IsNullOrEmpty(relicId)) return 0;

                if (BattleDataCache.Relics.TryGetValue(relicId, out var relic))
                {
                    return relic.StatModifiers.GetValueOrDefault(statKey, 0);
                }
                return 0;
            }

            // Helper to get modifier from the hovered relic data
            int GetHoveredModifier(string statKey)
            {
                if (_hoveredRelicData == null) return 0;
                return _hoveredRelicData.StatModifiers.GetValueOrDefault(statKey, 0);
            }

            for (int i = 0; i < stats.Count; i++)
            {
                var stat = stats[i];
                int y = startY + (i * rowSpacing);

                // 1. Calculate Values
                // Get the base stat directly to perform accurate math
                int baseStat = playerState.GetBaseStat(stat.StatKey);

                // Calculate total current modifier from all equipped items
                int totalCurrentMod = 0;
                for (int slot = 0; slot < playerState.EquippedRelics.Length; slot++)
                {
                    totalCurrentMod += GetEquippedModifier(slot, stat.StatKey);
                }

                // Current Effective Value (Clamped)
                int currentVal = Math.Max(1, baseStat + totalCurrentMod);

                // Projected Calculation
                int projectedVal = currentVal;
                int diff = 0;
                bool isComparing = _isEquipSubmenuOpen && _hoveredRelicData != null;

                if (isComparing)
                {
                    // We are replacing slot 0
                    int currentSlotMod = GetEquippedModifier(0, stat.StatKey);
                    int newMod = GetHoveredModifier(stat.StatKey);

                    // Calculate projected raw value
                    int projectedRaw = baseStat + totalCurrentMod - currentSlotMod + newMod;

                    // Clamp projected value
                    projectedVal = Math.Max(1, projectedRaw);

                    // Calculate difference based on clamped values
                    diff = projectedVal - currentVal;
                }

                // 2. Determine Left Text (Modifier or Current)
                string leftText;
                Color leftColor;
                Color labelColor;

                if (isComparing && diff != 0)
                {
                    // Stat is changing: Highlight label
                    labelColor = _global.Palette_BrightWhite;

                    // Cycle logic
                    float cyclePos = _statCycleTimer % (STAT_CYCLE_INTERVAL * 2);
                    bool showModifier = cyclePos >= STAT_CYCLE_INTERVAL;

                    if (showModifier)
                    {
                        // Show Modifier (+5)
                        leftText = (diff > 0 ? "+" : "") + diff.ToString();
                        leftColor = diff > 0 ? _global.Palette_LightGreen : _global.Palette_Red;
                    }
                    else
                    {
                        // Show Current Value (90)
                        leftText = currentVal.ToString();
                        leftColor = _global.Palette_BrightWhite;
                    }
                }
                else
                {
                    // Stat is NOT changing or not comparing: Default label, show current value
                    labelColor = _global.Palette_Gray;
                    leftText = currentVal.ToString();
                    leftColor = _global.Palette_LightGray;
                }

                // 3. Draw Label
                spriteBatch.DrawStringSnapped(secondaryFont, stat.Label, new Vector2(startX, y + 4), labelColor);

                // 4. Draw Left Text (Current Value OR Modifier)
                Vector2 leftSize = font.MeasureString(leftText);
                spriteBatch.DrawStringSnapped(font, leftText, new Vector2(startX + val1RightX - leftSize.X, y + 4), leftColor);

                // 5. Draw Right Side (Only if comparing)
                if (isComparing)
                {
                    // Arrow
                    Color arrowColor = (diff != 0) ? _global.Palette_BrightWhite : _global.Palette_Gray;
                    spriteBatch.DrawStringSnapped(secondaryFont, ">", new Vector2(startX + arrowX, y + 4), arrowColor);

                    // Projected Value
                    string projStr = projectedVal.ToString();
                    Vector2 projSize = font.MeasureString(projStr);
                    Color projColor = _global.Palette_LightGray;
                    if (diff > 0) projColor = _global.Palette_LightGreen;
                    else if (diff < 0) projColor = _global.Palette_Red;

                    spriteBatch.DrawStringSnapped(font, projStr, new Vector2(startX + val2RightX - projSize.X, y + 4), projColor);
                }
            }
        }

        private List<List<ColoredText>> ParseAndWrapRichText(BitmapFont font, string text, float maxWidth, Color defaultColor)
        {
            var lines = new List<List<ColoredText>>();
            if (string.IsNullOrEmpty(text)) return lines;

            var currentLine = new List<ColoredText>();
            float currentLineWidth = 0f;
            Color currentColor = defaultColor;

            // Split by tags OR whitespace (capturing both)
            var parts = Regex.Split(text, @"(\[.*?\]|\s+)");

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string tagContent = part.Substring(1, part.Length - 2).ToLowerInvariant();
                    if (tagContent == "/" || tagContent == "default")
                    {
                        currentColor = defaultColor;
                    }
                    else
                    {
                        currentColor = ParseColor(tagContent);
                    }
                }
                else if (part.Contains("\n"))
                {
                    // Force a new line
                    lines.Add(currentLine);
                    currentLine = new List<ColoredText>();
                    currentLineWidth = 0f;
                }
                else
                {
                    // It's a word or spaces (but not newlines)
                    bool isWhitespace = string.IsNullOrWhiteSpace(part);
                    float partWidth = isWhitespace ? (part.Length * SPACE_WIDTH) : font.MeasureString(part).Width;

                    // If it's a word and it doesn't fit, wrap.
                    // We don't wrap on whitespace; we let it trail off the edge.
                    if (!isWhitespace && currentLineWidth + partWidth > maxWidth && currentLineWidth > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = new List<ColoredText>();
                        currentLineWidth = 0f;
                    }

                    // Optimization: Don't add leading whitespace to a new line
                    if (isWhitespace && currentLineWidth == 0)
                    {
                        continue;
                    }

                    currentLine.Add(new ColoredText(part, currentColor));
                    currentLineWidth += partWidth;
                }
            }

            if (currentLine.Count > 0)
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private Color ParseColor(string colorName)
        {
            string tag = colorName.ToLowerInvariant();

            // 1. Stats
            if (tag == "cstr") return _global.StatColor_Strength;
            if (tag == "cint") return _global.StatColor_Intelligence;
            if (tag == "cten") return _global.StatColor_Tenacity;
            if (tag == "cagi") return _global.StatColor_Agility;

            // 2. General
            if (tag == "cpositive") return _global.ColorPositive;
            if (tag == "cnegative") return _global.ColorNegative;
            if (tag == "ccrit") return _global.ColorCrit;
            if (tag == "cimmune") return _global.ColorImmune;
            if (tag == "cctm") return _global.ColorConditionToMeet;

            // 3. Elements
            if (tag == "cfire") return _global.ElementColors.GetValueOrDefault(2, Color.White);
            if (tag == "cwater") return _global.ElementColors.GetValueOrDefault(3, Color.White);
            if (tag == "carcane") return _global.ElementColors.GetValueOrDefault(4, Color.White);
            if (tag == "cearth") return _global.ElementColors.GetValueOrDefault(5, Color.White);
            if (tag == "cmetal") return _global.ElementColors.GetValueOrDefault(6, Color.White);
            if (tag == "ctoxic") return _global.ElementColors.GetValueOrDefault(7, Color.White);
            if (tag == "cwind") return _global.ElementColors.GetValueOrDefault(8, Color.White);
            if (tag == "cvoid") return _global.ElementColors.GetValueOrDefault(9, Color.White);
            if (tag == "clight") return _global.ElementColors.GetValueOrDefault(10, Color.White);
            if (tag == "celectric") return _global.ElementColors.GetValueOrDefault(11, Color.White);
            if (tag == "cice") return _global.ElementColors.GetValueOrDefault(12, Color.White);
            if (tag == "cnature") return _global.ElementColors.GetValueOrDefault(13, Color.White);

            // 4. Status Effects
            if (tag.StartsWith("c"))
            {
                string effectName = tag.Substring(1);
                if (effectName == "poison") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Poison, Color.White);
                if (effectName == "stun") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Stun, Color.White);
                if (effectName == "regen") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Regen, Color.White);
                if (effectName == "dodging") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Dodging, Color.White);
                if (effectName == "burn") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Burn, Color.White);
                if (effectName == "freeze") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Freeze, Color.White);
                if (effectName == "blind") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Blind, Color.White);
                if (effectName == "confuse") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Confuse, Color.White);
                if (effectName == "silence") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Silence, Color.White);
                if (effectName == "fear") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Fear, Color.White);
                if (effectName == "root") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Root, Color.White);
            }

            // 5. Standard Palette Colors
            switch (tag)
            {
                case "teal": return _global.Palette_Teal;
                case "red": return _global.Palette_Red;
                case "blue": return _global.Palette_LightBlue;
                case "green": return _global.Palette_LightGreen;
                case "yellow": return _global.Palette_Yellow;
                case "orange": return _global.Palette_Orange;
                case "purple": return _global.Palette_LightPurple;
                case "pink": return _global.Palette_Pink;
                case "gray": return _global.Palette_Gray;
                case "white": return _global.Palette_White;
                case "brightwhite": return _global.Palette_BrightWhite;
                case "darkgray": return _global.Palette_DarkGray;
                default: return _global.Palette_White;
            }
        }

        public void DrawScreen(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Only draw the toggle button here
            _inventoryButton?.Draw(spriteBatch, font, gameTime, transform);
        }
    }
}
#nullable restore
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
using ProjectVagabond.Progression;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public enum InventoryCategory { Weapons, Armor, Relics, Consumables, Misc, Equip }
    public partial class SplitMapInventoryOverlay
    {
        public bool IsOpen { get; private set; } = false;
        public bool IsHovered => _inventoryButton?.IsHovered ?? false;
        private readonly GameState _gameState;
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly HapticsManager _hapticsManager;
        private readonly ComponentStore _componentStore;
        private ImageButton? _inventoryButton;
        private readonly List<InventoryHeaderButton> _inventoryHeaderButtons = new();
        private readonly Dictionary<InventoryHeaderButton, float> _inventoryHeaderButtonOffsets = new();
        private readonly Dictionary<InventoryHeaderButton, Rectangle> _inventoryHeaderButtonBaseBounds = new();
        private InventoryHeaderButton? _inventoryEquipButton;
        private readonly List<InventorySlot> _inventorySlots = new();
        private Rectangle _inventorySlotArea;
        private readonly Rectangle[] _partyMemberPanelAreas = new Rectangle[4];

        private readonly List<Button> _partyEquipButtons = new();

        private readonly List<SpellEquipButton> _partySpellButtons = new();

        private ImageButton? _debugButton1;
        private ImageButton? _debugButton2;
        private ImageButton? _pageLeftButton;
        private ImageButton? _pageRightButton;

        private int _currentPartyMemberIndex = 0;
        private int _hoveredMemberIndex = -1; 

        // Submenu State
        private enum EquipSlotType { None, Weapon, Armor, Relic, Spell1, Spell2, Spell3, Spell4 }
        private EquipSlotType _activeEquipSlotType = EquipSlotType.None;
        private bool _isEquipSubmenuOpen = false;
        private readonly List<EquipButton> _equipSubmenuButtons = new();
        private int _equipMenuScrollIndex = 0;

        // Pagination State
        private int _currentPage = 0;
        private int _totalPages = 0;
        private const int ITEMS_PER_PAGE = 30; // 6x5 grid

        private InventoryCategory _selectedInventoryCategory;
        private InventoryCategory _previousInventoryCategory;
        private int _selectedSlotIndex = -1;

        // Navigation Order Definition
        private readonly List<InventoryCategory> _categoryOrder = new()
    {
        InventoryCategory.Weapons,
        InventoryCategory.Armor,
        InventoryCategory.Relics,
        InventoryCategory.Consumables,
        InventoryCategory.Misc
    };

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
        private object? _previousHoveredItemData;
        private const float STAT_CYCLE_INTERVAL = 1.0f;

        // Input State
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        // Hover Data
        private object? _hoveredItemData;

        // Text Formatting Tuning
        private const int SPACE_WIDTH = 5;

        // External Control Event
        public event Action? OnInventoryButtonClicked;

        public SplitMapInventoryOverlay()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        public void Initialize()
        {
            InitializeInventoryUI();
            _previousInventoryCategory = _selectedInventoryCategory;
            _inventoryArrowAnimTimer = INVENTORY_ARROW_ANIM_DURATION;
            _inventoryPositionOffset = Vector2.Zero;
            _selectedHeaderBobTimer = 0f;
            _statCycleTimer = 0f;
            _previousHoveredItemData = null;
            _leftPageArrowBobTimer = 0f;
            _rightPageArrowBobTimer = 0f;
            _currentPartyMemberIndex = 0;

            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }

        private void InitializeInventoryUI()
        {
            if (_inventoryButton == null)
            {
                var inventoryIcon = _spriteManager.SplitMapInventoryButton;
                var rects = _spriteManager.SplitMapInventoryButtonSourceRects;
                _inventoryButton = new ImageButton(new Rectangle(7, 10, 16, 16), inventoryIcon, rects[0], rects[1], enableHoverSway: true);
                _inventoryButton.OnClick += () => OnInventoryButtonClicked?.Invoke();
            }
            _inventoryButton.ResetAnimationState();

            _inventoryHeaderButtons.Clear();
            _inventoryHeaderButtonOffsets.Clear();
            _inventoryHeaderButtonBaseBounds.Clear();
            _selectedInventoryCategory = InventoryCategory.Equip; // Default to Equip
            _selectedSlotIndex = -1;

            // Use the defined order for button creation
            int numButtons = _categoryOrder.Count;
            const int buttonSpriteSize = 32;
            const int spacing = 4; // Increased spacing from 0 to 2
            var buttonRects = _spriteManager.InventoryHeaderButtonSourceRects;

            int totalWidth = (numButtons * buttonSpriteSize) + ((numButtons - 1) * spacing);
            float startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2f + 19f;
            float buttonY = 200 + 6;

            for (int i = 0; i < numButtons; i++)
            {
                var category = _categoryOrder[i];
                Texture2D buttonSpriteSheet = category switch
                {
                    InventoryCategory.Weapons => _spriteManager.InventoryHeaderButtonWeapons,
                    InventoryCategory.Armor => _spriteManager.InventoryHeaderButtonArmor,
                    InventoryCategory.Relics => _spriteManager.InventoryHeaderButtonRelics,
                    InventoryCategory.Consumables => _spriteManager.InventoryHeaderButtonConsumables,
                    InventoryCategory.Misc => _spriteManager.InventoryHeaderButtonMisc,
                    _ => _spriteManager.InventoryHeaderButtonWeapons,
                };

                int menuIndex = (int)category;
                float xPos = startX + i * (buttonSpriteSize + spacing);
                var bounds = new Rectangle((int)MathF.Round(xPos), (int)buttonY, buttonSpriteSize, buttonSpriteSize);

                var button = new InventoryHeaderButton(bounds, buttonSpriteSheet, buttonRects[0], buttonRects[1], buttonRects[2], menuIndex, category.ToString());

                button.OnClick += () =>
                {
                    if (_selectedInventoryCategory != category)
                    {
                        SwitchToCategory(category);
                    }
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
            _inventoryEquipButton.OnClick += () => SwitchToCategory(InventoryCategory.Equip);

            // Initialize inventory slot grid
            const int slotContainerWidth = 180;
            const int slotContainerHeight = 132;
            const int slotColumns = 6;
            const int slotRows = 5;
            const int slotSize = 24;
            const int gridPaddingX = 18;
            const int gridPaddingY = 8;

            int containerX = (Global.VIRTUAL_WIDTH - slotContainerWidth) / 2 - 60;
            int containerY = 200 + 6 + 32 + 1;
            _inventorySlotArea = new Rectangle(containerX, containerY, slotContainerWidth, slotContainerHeight);

            // Initialize Party Member Slot Panels (4 slots)
            // Positioned to the right of the inventory grid
            const int statsPanelWidth = 116;
            const int statsPanelHeight = 132;
            int statsPanelX = _inventorySlotArea.Right + 4;
            int statsPanelY = _inventorySlotArea.Y - 1;

            const int panelWidth = 76;
            int panelStartX = 8; // Moved 2 pixels left (was 10)

            // Calculate layout for hitboxes
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var defaultFont = ServiceLocator.Get<BitmapFont>(); // Get default font

            _partyEquipButtons.Clear();
            _partySpellButtons.Clear(); // Clear spell buttons

            for (int i = 0; i < 4; i++)
            {
                _partyMemberPanelAreas[i] = new Rectangle(
                    panelStartX + (i * panelWidth),
                    statsPanelY,
                    panelWidth,
                    statsPanelHeight
                );

                // Calculate Y positions for hitboxes based on DrawPartyMemberSlots layout
                int centerX = _partyMemberPanelAreas[i].Center.X;
                int currentY = _partyMemberPanelAreas[i].Y + 4;

                // Name (Default Font LineHeight) - 2
                currentY += defaultFont.LineHeight - 2;

                // Portrait (32) + 2
                currentY += 32 + 2 - 6; // Moved up 6 pixels

                // Health Bar (8 + LineHeight + 4)
                currentY += 8 + secondaryFont.LineHeight + 4 - 3; // Moved up 3 pixels

                // Equip Slots
                int slotIconSize = 16;
                int gap = 4;
                int totalEquipWidth = (slotIconSize * 3) + (gap * 2);
                int equipStartX = centerX - (totalEquipWidth / 2);

                // Create buttons for this member
                int memberIndex = i;

                // Hitbox adjustments: 4 pixels wider, centered (-2 offset)
                int hitboxWidth = slotIconSize + 4;
                int hitboxXOffset = -2;

                // Weapon Button
                var weaponBtn = new Button(new Rectangle(equipStartX + hitboxXOffset, currentY, hitboxWidth, slotIconSize), "") { EnableHoverSway = false };
                weaponBtn.OnClick += () => OpenEquipSubmenu(memberIndex, EquipSlotType.Weapon);
                _partyEquipButtons.Add(weaponBtn);

                // Armor Button
                var armorBtn = new Button(new Rectangle(equipStartX + slotIconSize + gap + hitboxXOffset, currentY, hitboxWidth, slotIconSize), "") { EnableHoverSway = false };
                armorBtn.OnClick += () => OpenEquipSubmenu(memberIndex, EquipSlotType.Armor);
                _partyEquipButtons.Add(armorBtn);

                // Relic Button
                var relicBtn = new Button(new Rectangle(equipStartX + (slotIconSize + gap) * 2 + hitboxXOffset, currentY, hitboxWidth, slotIconSize), "") { EnableHoverSway = false };
                relicBtn.OnClick += () => OpenEquipSubmenu(memberIndex, EquipSlotType.Relic);
                _partyEquipButtons.Add(relicBtn);

                currentY += slotSize + 6 - 5; // Moved up 5 pixels

                // Stats (4 lines)
                currentY += (int)secondaryFont.LineHeight + 1; // STR
                currentY += (int)secondaryFont.LineHeight + 1; // INT
                currentY += (int)secondaryFont.LineHeight + 1; // TEN
                currentY += (int)secondaryFont.LineHeight + 1; // AGI

                // Positioned under the stats
                // Button size: 64x8
                // Centered horizontally in the panel (panelWidth = 76)
                int spellButtonWidth = 64;
                int spellButtonHeight = 8;
                int spellButtonX = centerX - (spellButtonWidth / 2);
                int spellButtonY = currentY + 2 - 8;

                for (int s = 0; s < 4; s++)
                {
                    var spellBtn = new SpellEquipButton(new Rectangle(spellButtonX, spellButtonY, spellButtonWidth, spellButtonHeight));
                    _partySpellButtons.Add(spellBtn);
                    spellButtonY += spellButtonHeight; // Stack vertically
                }
            }

            _inventorySlots.Clear();

            float availableWidth = slotContainerWidth - (gridPaddingX * 2);
            float availableHeight = slotContainerHeight - (gridPaddingY * 2);
            float spaceBetweenX = (slotColumns > 1) ? (availableWidth - slotSize) / (slotColumns - 1) : 0;
            float spaceBetweenY = (slotRows > 1) ? (availableHeight - slotSize) / (slotRows - 1) : 0;

            var slotFrames = _spriteManager.InventorySlotSourceRects;
            if (slotFrames != null && slotFrames.Length > 0)
            {
                for (int row = 0; row < slotRows; row++)
                {
                    for (int col = 0; col < slotColumns; col++)
                    {
                        float nodeX = _inventorySlotArea.X + gridPaddingX + (slotSize / 2f) + (col * spaceBetweenX);
                        float nodeY = _inventorySlotArea.Y + gridPaddingY + (slotSize / 2f) + (row * spaceBetweenY);

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
            _debugButton1.OnClick += () => CycleCategory(-1);

            _debugButton2 = new ImageButton(new Rectangle(0, 0, 5, 5), _spriteManager.InventoryRightArrowButton, rightArrowRects[0], rightArrowRects[1]);
            _debugButton2.OnClick += () => CycleCategory(1);

            _pageLeftButton = new ImageButton(new Rectangle(0, 0, 5, 5), _spriteManager.InventoryLeftArrowButton, leftArrowRects[0], leftArrowRects[1]);
            _pageLeftButton.OnClick += () => ChangePage(-1);

            _pageRightButton = new ImageButton(new Rectangle(0, 0, 5, 5), _spriteManager.InventoryRightArrowButton, rightArrowRects[0], rightArrowRects[1]);
            _pageRightButton.OnClick += () => ChangePage(1);

            // Initialize Submenu Buttons
            _equipSubmenuButtons.Clear();
            int equipButtonX = (Global.VIRTUAL_WIDTH - 180) / 2 - 60;
            int submenuStartY = 250 + 19 + 16 - 32; // Approximate Y

            for (int i = 0; i < 7; i++)
            {
                int yPos = submenuStartY + (i * 16);
                var button = new EquipButton(new Rectangle(equipButtonX, yPos, 180, 16), "");
                button.TitleText = "";
                button.Font = secondaryFont;
                button.IsEnabled = false;
                _equipSubmenuButtons.Add(button);
            }
        }

        public void Show()
        {
            IsOpen = true;
            _inventoryButton?.SetSprites(_spriteManager.SplitMapCloseInventoryButton, _spriteManager.SplitMapCloseInventoryButtonSourceRects[0], _spriteManager.SplitMapCloseInventoryButtonSourceRects[1]);
            SwitchToCategory(InventoryCategory.Equip);
        }

        public void Hide()
        {
            IsOpen = false;
            _inventoryButton?.SetSprites(_spriteManager.SplitMapInventoryButton, _spriteManager.SplitMapInventoryButtonSourceRects[0], _spriteManager.SplitMapInventoryButtonSourceRects[1]);
            CancelEquipSelection();
            _selectedSlotIndex = -1;
        }

        public void ForceClose()
        {
            if (IsOpen)
            {
                Hide();
            }
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
                // Scroll Down/Right (1) -> Go to first available category
                if (direction > 0)
                {
                    int target = FindNextNonEmptyCategory(-1, 1);
                    if (target != -1)
                    {
                        SwitchToCategory(_categoryOrder[target]);
                    }
                }
                return;
            }

            // Handle Main Categories
            int currentIndex = _categoryOrder.IndexOf(_selectedInventoryCategory);
            int targetIndex = FindNextNonEmptyCategory(currentIndex, direction);

            if (targetIndex != -1)
            {
                SwitchToCategory(_categoryOrder[targetIndex]);
            }
        }

        private int FindNextNonEmptyCategory(int startIndex, int direction)
        {
            int checkIndex = startIndex + direction;
            while (checkIndex >= 0 && checkIndex < _categoryOrder.Count)
            {
                if (HasItems(_categoryOrder[checkIndex]))
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
                InventoryCategory.Weapons => _gameState.PlayerState.Weapons.Any(),
                InventoryCategory.Armor => _gameState.PlayerState.Armors.Any(),
                InventoryCategory.Relics => _gameState.PlayerState.Relics.Any(),
                InventoryCategory.Consumables => _gameState.PlayerState.Consumables.Any(),
                InventoryCategory.Misc => _gameState.PlayerState.MiscItems.Any(),
                _ => false
            };
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
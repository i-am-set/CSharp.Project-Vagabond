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
    public enum InventoryCategory { Weapons, Armor, Spells, Relics, Consumables, Misc, Equip }

    public partial class SplitMapInventoryOverlay
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

        // Equip Buttons
        private EquipButton? _relicEquipButton1;
        private EquipButton? _relicEquipButton2;
        private EquipButton? _relicEquipButton3;
        private EquipButton? _weaponEquipButton;
        private EquipButton? _armorEquipButton;

        // Submenu State
        private enum EquipSlotType { None, Weapon, Armor, Relic1, Relic2, Relic3 }
        private EquipSlotType _activeEquipSlotType = EquipSlotType.None;
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

        // Navigation Order Definition
        private readonly List<InventoryCategory> _categoryOrder = new()
        {
            InventoryCategory.Weapons,
            InventoryCategory.Armor,
            InventoryCategory.Relics,
            InventoryCategory.Spells,
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
            _previousHoveredItemData = null;
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
            _selectedInventoryCategory = InventoryCategory.Equip; // Default to Equip
            _selectedSlotIndex = -1;

            // Use the defined order for button creation
            int numButtons = _categoryOrder.Count;
            const int buttonSpriteSize = 32;
            const int spacing = 0; // Added spacing between buttons
            var buttonRects = _spriteManager.InventoryHeaderButtonSourceRects;

            // Calculate total width dynamically based on button count and spacing
            int totalWidth = (numButtons * buttonSpriteSize) + ((numButtons - 1) * spacing);

            // Center the group, maintaining the +19f offset for the background art alignment
            float startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2f + 19f;
            float buttonY = 200 + 6;

            for (int i = 0; i < numButtons; i++)
            {
                var category = _categoryOrder[i];
                Texture2D buttonSpriteSheet = category switch
                {
                    InventoryCategory.Weapons => _spriteManager.InventoryHeaderButtonWeapons,
                    InventoryCategory.Armor => _spriteManager.InventoryHeaderButtonArmor,
                    InventoryCategory.Spells => _spriteManager.InventoryHeaderButtonSpells,
                    InventoryCategory.Relics => _spriteManager.InventoryHeaderButtonRelics,
                    InventoryCategory.Consumables => _spriteManager.InventoryHeaderButtonConsumables,
                    InventoryCategory.Misc => _spriteManager.InventoryHeaderButtonMisc,
                    _ => _spriteManager.InventoryHeaderButtonWeapons,
                };

                int menuIndex = (int)category;

                // Calculate X position with spacing
                float xPos = startX + i * (buttonSpriteSize + spacing);
                var bounds = new Rectangle((int)MathF.Round(xPos), (int)buttonY, buttonSpriteSize, buttonSpriteSize);

                var button = new InventoryHeaderButton(bounds, buttonSpriteSheet, buttonRects[0], buttonRects[1], buttonRects[2], menuIndex, category.ToString());

                // Only switch if it's a different category to prevent re-animation
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

            // Initialize Equip Buttons
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            int equipButtonX = (Global.VIRTUAL_WIDTH - 180) / 2 - 60;
            int relicButtonY = 250 + 19 + 16;
            int armorButtonY = relicButtonY - 16; // Middle
            int weaponButtonY = relicButtonY - 32; // Top

            // Weapon Button (Even)
            _weaponEquipButton = new EquipButton(new Rectangle(equipButtonX, weaponButtonY, 180, 16), "NOTHING");
            _weaponEquipButton.TitleText = "WEAPN";
            _weaponEquipButton.Font = secondaryFont;
            _weaponEquipButton.CustomDefaultTextColor = _global.Palette_BrightWhite;
            _weaponEquipButton.CustomTitleTextColor = _global.Palette_DarkGray;
            _weaponEquipButton.OnClick += () => OpenEquipSubmenu(EquipSlotType.Weapon);

            // Armor Button (Odd)
            _armorEquipButton = new EquipButton(new Rectangle(equipButtonX, armorButtonY, 180, 16), "NOTHING");
            _armorEquipButton.TitleText = "ARMOR";
            _armorEquipButton.Font = secondaryFont;
            _armorEquipButton.CustomDefaultTextColor = _global.Palette_White;
            _armorEquipButton.CustomTitleTextColor = _global.Palette_DarkerGray;
            _armorEquipButton.OnClick += () => OpenEquipSubmenu(EquipSlotType.Armor);

            // Relic Button 1 (Even)
            _relicEquipButton1 = new EquipButton(new Rectangle(equipButtonX, relicButtonY, 180, 16), "NOTHING");
            _relicEquipButton1.TitleText = "RELIC";
            _relicEquipButton1.Font = secondaryFont;
            _relicEquipButton1.CustomDefaultTextColor = _global.Palette_BrightWhite;
            _relicEquipButton1.CustomTitleTextColor = _global.Palette_DarkGray;
            _relicEquipButton1.OnClick += () => OpenEquipSubmenu(EquipSlotType.Relic1);

            // Relic Button 2 (Odd)
            _relicEquipButton2 = new EquipButton(new Rectangle(equipButtonX, relicButtonY + 16, 180, 16), "NOTHING");
            _relicEquipButton2.TitleText = "RELIC";
            _relicEquipButton2.Font = secondaryFont;
            _relicEquipButton2.CustomDefaultTextColor = _global.Palette_White;
            _relicEquipButton2.CustomTitleTextColor = _global.Palette_DarkerGray;
            _relicEquipButton2.OnClick += () => OpenEquipSubmenu(EquipSlotType.Relic2);

            // Relic Button 3 (Even)
            _relicEquipButton3 = new EquipButton(new Rectangle(equipButtonX, relicButtonY + 32, 180, 16), "NOTHING");
            _relicEquipButton3.TitleText = "RELIC";
            _relicEquipButton3.Font = secondaryFont;
            _relicEquipButton3.CustomDefaultTextColor = _global.Palette_BrightWhite;
            _relicEquipButton3.CustomTitleTextColor = _global.Palette_DarkGray;
            _relicEquipButton3.OnClick += () => OpenEquipSubmenu(EquipSlotType.Relic3);

            // Initialize Submenu Buttons
            _equipSubmenuButtons.Clear();
            // Submenu starts at the weapon button Y position
            int submenuStartY = weaponButtonY;

            for (int i = 0; i < 7; i++)
            {
                int yPos = submenuStartY + (i * 16);
                var button = new EquipButton(new Rectangle(equipButtonX, yPos, 180, 16), "");
                button.TitleText = ""; // Initialize as empty
                button.Font = secondaryFont;
                button.IsEnabled = false; // Disabled by default
                _equipSubmenuButtons.Add(button);
            }
        }

        private void ToggleInventory()
        {
            IsOpen = !IsOpen;

            if (IsOpen)
            {
                _inventoryButton?.SetSprites(_spriteManager.SplitMapCloseInventoryButton, _spriteManager.SplitMapCloseInventoryButtonSourceRects[0], _spriteManager.SplitMapCloseInventoryButtonSourceRects[1]);

                // Force open to Equip menu
                SwitchToCategory(InventoryCategory.Equip);

                // Refresh equip button texts and icons
                UpdateEquipButtonState(_weaponEquipButton!, _gameState.PlayerState.EquippedWeaponId, EquipSlotType.Weapon);
                UpdateEquipButtonState(_armorEquipButton!, _gameState.PlayerState.EquippedArmorId, EquipSlotType.Armor);
                UpdateEquipButtonState(_relicEquipButton1!, _gameState.PlayerState.EquippedRelics[0], EquipSlotType.Relic1);
                UpdateEquipButtonState(_relicEquipButton2!, _gameState.PlayerState.EquippedRelics[1], EquipSlotType.Relic2);
                UpdateEquipButtonState(_relicEquipButton3!, _gameState.PlayerState.EquippedRelics[2], EquipSlotType.Relic3);
            }
            else
            {
                _inventoryButton?.SetSprites(_spriteManager.SplitMapInventoryButton, _spriteManager.SplitMapInventoryButtonSourceRects[0], _spriteManager.SplitMapInventoryButtonSourceRects[1]);
                CancelEquipSelection();
                _selectedSlotIndex = -1;
            }

            OnInventoryToggled?.Invoke(IsOpen);
        }
    }
}

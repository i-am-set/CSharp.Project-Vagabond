using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    public enum InventoryCategory { Weapons, Armor, Relics, Consumables, Misc, Equip }
    public enum EquipSlotType { None, Weapon, Armor, Relic, Spell1, Spell2, Spell3, Spell4 }

    public class SplitMapInventoryOverlay
    {
        // --- Public State (Accessed by Helpers) ---
        public bool IsOpen { get; set; } = false;
        public bool IsHovered => InventoryButton?.IsHovered ?? false;

        // Services
        public GameState GameState { get; private set; }
        public SpriteManager SpriteManager { get; private set; }
        public Global Global { get; private set; }
        public HapticsManager HapticsManager { get; private set; }
        public ComponentStore ComponentStore { get; private set; }

        // UI Elements
        public ImageButton? InventoryButton { get; set; }
        public List<InventoryHeaderButton> InventoryHeaderButtons { get; } = new();
        public Dictionary<InventoryHeaderButton, float> InventoryHeaderButtonOffsets { get; } = new();
        public Dictionary<InventoryHeaderButton, Rectangle> InventoryHeaderButtonBaseBounds { get; } = new();
        public InventoryHeaderButton? InventoryEquipButton { get; set; }
        public List<InventorySlot> InventorySlots { get; } = new();
        public Rectangle InventorySlotArea { get; set; }
        public Rectangle[] PartyMemberPanelAreas { get; } = new Rectangle[4];
        public List<Button> PartyEquipButtons { get; } = new();
        public List<SpellEquipButton> PartySpellButtons { get; } = new();

        // Navigation Buttons
        public ImageButton? DebugButton1 { get; set; }
        public ImageButton? DebugButton2 { get; set; }
        public ImageButton? PageLeftButton { get; set; }
        public ImageButton? PageRightButton { get; set; }

        // State Variables
        public int CurrentPartyMemberIndex { get; set; } = 0;
        public int HoveredMemberIndex { get; set; } = -1;
        public EquipSlotType ActiveEquipSlotType { get; set; } = EquipSlotType.None;
        public bool IsEquipSubmenuOpen { get; set; } = false;
        public List<EquipButton> EquipSubmenuButtons { get; } = new();
        public int EquipMenuScrollIndex { get; set; } = 0;

        public int CurrentPage { get; set; } = 0;
        public int TotalPages { get; set; } = 0;
        public const int ITEMS_PER_PAGE = 30;

        public InventoryCategory SelectedInventoryCategory { get; set; }
        public InventoryCategory PreviousInventoryCategory { get; set; }
        public int SelectedSlotIndex { get; set; } = -1;

        public List<InventoryCategory> CategoryOrder { get; } = new()
        {
            InventoryCategory.Weapons,
            InventoryCategory.Armor,
            InventoryCategory.Relics,
            InventoryCategory.Consumables,
            InventoryCategory.Misc
        };

        // Animation State
        public float InventoryArrowAnimTimer { get; set; }
        public const float INVENTORY_ARROW_ANIM_DURATION = 0.2f;
        public Vector2 InventoryPositionOffset { get; set; } = Vector2.Zero;
        public float SelectedHeaderBobTimer { get; set; }
        public float LeftPageArrowBobTimer { get; set; } = 0f;
        public float RightPageArrowBobTimer { get; set; } = 0f;
        public const float PAGE_ARROW_BOB_DURATION = 0.05f;
        public float StatCycleTimer { get; set; } = 0f;
        public object? PreviousHoveredItemData { get; set; }
        public float InfoPanelNameWaveTimer { get; set; } = 0f;

        // Input State
        public MouseState PreviousMouseState { get; set; }
        public KeyboardState PreviousKeyboardState { get; set; }

        // Hover Data
        public object? HoveredItemData { get; set; }

        // Events
        public event Action? OnInventoryButtonClicked;

        // --- Helpers ---
        private readonly InventoryDataProcessor _dataProcessor;
        private readonly InventoryDrawer _drawer;
        private readonly InventoryInputHandler _inputHandler;
        private readonly InventoryEquipSystem _equipSystem;

        public SplitMapInventoryOverlay()
        {
            GameState = ServiceLocator.Get<GameState>();
            SpriteManager = ServiceLocator.Get<SpriteManager>();
            Global = ServiceLocator.Get<Global>();
            HapticsManager = ServiceLocator.Get<HapticsManager>();
            ComponentStore = ServiceLocator.Get<ComponentStore>();

            // Instantiate Helpers
            _dataProcessor = new InventoryDataProcessor(this);
            _equipSystem = new InventoryEquipSystem(this, _dataProcessor);
            _drawer = new InventoryDrawer(this, _dataProcessor);
            _inputHandler = new InventoryInputHandler(this, _dataProcessor, _equipSystem);
        }

        public void Initialize()
        {
            _inputHandler.InitializeInventoryUI();
            PreviousInventoryCategory = SelectedInventoryCategory;
            InventoryArrowAnimTimer = INVENTORY_ARROW_ANIM_DURATION;
            InventoryPositionOffset = Vector2.Zero;
            SelectedHeaderBobTimer = 0f;
            StatCycleTimer = 0f;
            PreviousHoveredItemData = null;
            LeftPageArrowBobTimer = 0f;
            RightPageArrowBobTimer = 0f;
            CurrentPartyMemberIndex = 0;

            PreviousMouseState = Mouse.GetState();
            PreviousKeyboardState = Keyboard.GetState();
        }

        public void Show()
        {
            IsOpen = true;
            InventoryButton?.SetSprites(SpriteManager.SplitMapCloseInventoryButton, SpriteManager.SplitMapCloseInventoryButtonSourceRects[0], SpriteManager.SplitMapCloseInventoryButtonSourceRects[1]);
            _inputHandler.SwitchToCategory(InventoryCategory.Equip);
        }

        public void Hide()
        {
            IsOpen = false;
            InventoryButton?.SetSprites(SpriteManager.SplitMapInventoryButton, SpriteManager.SplitMapInventoryButtonSourceRects[0], SpriteManager.SplitMapInventoryButtonSourceRects[1]);
            _equipSystem.CancelEquipSelection();
            SelectedSlotIndex = -1;
        }

        public void ForceClose()
        {
            if (IsOpen) Hide();
        }

        public void TriggerInventoryButtonClicked()
        {
            OnInventoryButtonClicked?.Invoke();
        }

        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState, bool allowAccess, Matrix cameraTransform)
        {
            _inputHandler.Update(gameTime, currentMouseState, currentKeyboardState, allowAccess, cameraTransform);
        }

        public void DrawWorld(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            _drawer.DrawWorld(spriteBatch, font, gameTime);
        }

        public void DrawScreen(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            _drawer.DrawScreen(spriteBatch, font, gameTime, transform);
        }
    }
}
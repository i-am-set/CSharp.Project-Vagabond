using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public enum InventoryCategory { Weapons, Armor, Relics, Consumables, Misc, Equip }
    public enum EquipSlotType { None, Weapon, Armor, Relic, Spell1, Spell2, Spell3, Spell4 }

    // Formal State Machine
    internal enum InventoryState
    {
        Browse,             // Viewing items in grid
        EquipTargetSelection, // Viewing party members to choose a slot
        EquipItemSelection    // Submenu open, picking an item to equip
    }

    public class SplitMapInventoryOverlay
    {
        // --- Public API (Minimal Surface Area) ---
        public bool IsOpen { get; private set; } = false;
        public bool IsHovered => InventoryButton?.IsHovered ?? false;
        public event Action? OnInventoryButtonClicked;

        // --- Internal State (Accessible only to Helpers) ---
        internal InventoryState CurrentState { get; set; } = InventoryState.Browse;

        // Services
        internal GameState GameState { get; private set; }
        internal SpriteManager SpriteManager { get; private set; }
        internal Global Global { get; private set; }
        internal HapticsManager HapticsManager { get; private set; }
        internal ComponentStore ComponentStore { get; private set; }

        // Shared UI Elements (Owned by Coordinator as they are used by multiple helpers)
        internal ImageButton? InventoryButton { get; set; }
        internal List<InventoryHeaderButton> InventoryHeaderButtons { get; } = new();
        internal Dictionary<InventoryHeaderButton, float> InventoryHeaderButtonOffsets { get; } = new();
        internal Dictionary<InventoryHeaderButton, Rectangle> InventoryHeaderButtonBaseBounds { get; } = new();
        internal InventoryHeaderButton? InventoryEquipButton { get; set; }

        internal List<InventorySlot> InventorySlots { get; } = new();
        internal Rectangle InventorySlotArea { get; set; }

        internal Rectangle[] PartyMemberPanelAreas { get; } = new Rectangle[4];
        internal List<Button> PartyEquipButtons { get; } = new();
        internal List<SpellEquipButton> PartySpellButtons { get; } = new();

        // Navigation Buttons
        internal ImageButton? DebugButton1 { get; set; }
        internal ImageButton? DebugButton2 { get; set; }
        internal ImageButton? PageLeftButton { get; set; }
        internal ImageButton? PageRightButton { get; set; }

        // Shared State Variables
        internal int CurrentPartyMemberIndex { get; set; } = 0;
        internal int HoveredMemberIndex { get; set; } = -1;

        internal int CurrentPage { get; set; } = 0;
        internal int TotalPages { get; set; } = 0;
        internal const int ITEMS_PER_PAGE = 30;

        internal InventoryCategory SelectedInventoryCategory { get; set; }
        internal InventoryCategory PreviousInventoryCategory { get; set; }
        internal int SelectedSlotIndex { get; set; } = -1;

        internal List<InventoryCategory> CategoryOrder { get; } = new()
        {
            InventoryCategory.Weapons,
            InventoryCategory.Armor,
            InventoryCategory.Relics,
            InventoryCategory.Consumables,
            InventoryCategory.Misc
        };

        // Animation State
        internal float InventoryArrowAnimTimer { get; set; }
        internal const float INVENTORY_ARROW_ANIM_DURATION = 0.2f;
        internal Vector2 InventoryPositionOffset { get; set; } = Vector2.Zero;
        internal float SelectedHeaderBobTimer { get; set; }
        internal float LeftPageArrowBobTimer { get; set; } = 0f;
        internal float RightPageArrowBobTimer { get; set; } = 0f;
        internal const float PAGE_ARROW_BOB_DURATION = 0.05f;
        internal float StatCycleTimer { get; set; } = 0f;
        internal object? PreviousHoveredItemData { get; set; }
        internal float InfoPanelNameWaveTimer { get; set; } = 0f;

        // Input State
        internal MouseState PreviousMouseState { get; set; }
        internal KeyboardState PreviousKeyboardState { get; set; }

        // Hover Data
        internal object? HoveredItemData { get; set; }

        // --- Subsystems (Exposed internally for cross-communication) ---
        internal readonly InventoryDataProcessor DataProcessor;
        internal readonly InventoryEquipSystem EquipSystem;
        private readonly InventoryDrawer _drawer;
        private readonly InventoryInputHandler _inputHandler;

        public SplitMapInventoryOverlay()
        {
            GameState = ServiceLocator.Get<GameState>();
            SpriteManager = ServiceLocator.Get<SpriteManager>();
            Global = ServiceLocator.Get<Global>();
            HapticsManager = ServiceLocator.Get<HapticsManager>();
            ComponentStore = ServiceLocator.Get<ComponentStore>();

            // Instantiate Helpers
            DataProcessor = new InventoryDataProcessor(this);
            EquipSystem = new InventoryEquipSystem(this, DataProcessor);
            _drawer = new InventoryDrawer(this, DataProcessor, EquipSystem);
            _inputHandler = new InventoryInputHandler(this, DataProcessor, EquipSystem);
        }

        public void Initialize()
        {
            _inputHandler.InitializeInventoryUI();
            EquipSystem.Initialize(); // Initialize Equip System UI

            PreviousInventoryCategory = SelectedInventoryCategory;
            InventoryArrowAnimTimer = INVENTORY_ARROW_ANIM_DURATION;
            InventoryPositionOffset = Vector2.Zero;
            SelectedHeaderBobTimer = 0f;
            StatCycleTimer = 0f;
            PreviousHoveredItemData = null;
            LeftPageArrowBobTimer = 0f;
            RightPageArrowBobTimer = 0f;
            CurrentPartyMemberIndex = 0;
            CurrentState = InventoryState.Browse;

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
            EquipSystem.CancelEquipSelection();
            SelectedSlotIndex = -1;
            CurrentState = InventoryState.Browse;
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

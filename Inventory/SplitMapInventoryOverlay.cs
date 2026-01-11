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

        // --- Party Slot Animation State ---
        internal UIAnimator[] PartySlotAnimators { get; } = new UIAnimator[4];
        internal float[] PartySlotTextTimers { get; } = new float[4];
        internal TextEffectType[] PartySlotTextEffects { get; } = new TextEffectType[4];

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
        private static readonly Random _rng = new Random();

        public SplitMapInventoryOverlay()
        {
            GameState = ServiceLocator.Get<GameState>();
            SpriteManager = ServiceLocator.Get<SpriteManager>();
            Global = ServiceLocator.Get<Global>();
            HapticsManager = ServiceLocator.Get<HapticsManager>();
            ComponentStore = ServiceLocator.Get<ComponentStore>();

            // Initialize Party Slot Animators
            for (int i = 0; i < 4; i++)
            {
                PartySlotAnimators[i] = new UIAnimator
                {
                    EntryStyle = EntryExitStyle.SwoopRight,
                    DurationIn = 0.1f,
                    Magnitude = 20f
                };
                // Removed the OnInComplete callback that was prematurely resetting the text effect
            }

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
            SwitchToCategory(InventoryCategory.Equip);
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
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update Party Slot Animators
            if (SelectedInventoryCategory == InventoryCategory.Equip)
            {
                for (int i = 0; i < 4; i++)
                {
                    PartySlotAnimators[i].Update(dt);
                    PartySlotTextTimers[i] += dt;
                }
            }

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

        public void SwitchToCategory(InventoryCategory category)
        {
            EquipSystem.CancelEquipSelection();
            SelectedInventoryCategory = category;
            CurrentPage = 0;
            SelectedSlotIndex = -1;
            SelectedHeaderBobTimer = 0f;
            CurrentState = category == InventoryCategory.Equip ? InventoryState.EquipTargetSelection : InventoryState.Browse;
            DataProcessor.RefreshInventorySlots();

            if (category != InventoryCategory.Equip)
            {
                TriggerSlotAnimations();
            }
            else
            {
                TriggerEquipMenuAnimations();
            }
        }

        private void TriggerSlotAnimations()
        {
            float delay = 0f;
            const float stagger = 0.015f;
            foreach (var slot in InventorySlots)
            {
                if (slot.HasItem)
                {
                    slot.TriggerPopInAnimation(delay);
                    delay += stagger;
                }
            }
        }

        internal void TriggerEquipMenuAnimations()
        {
            // 1. Create a list of unique delays
            var delays = new List<float> { 0.0f, 0.05f, 0.1f, 0.15f };

            // 2. Shuffle the delays (Fisher-Yates)
            int n = delays.Count;
            while (n > 1)
            {
                n--;
                int k = _rng.Next(n + 1);
                (delays[k], delays[n]) = (delays[n], delays[k]);
            }

            // 3. Apply to slots
            for (int i = 0; i < 4; i++)
            {
                bool isOccupied = i < GameState.PlayerState.Party.Count;
                PartySlotAnimators[i].Reset();

                if (isOccupied)
                {
                    // Occupied slots animate in with delay
                    PartySlotAnimators[i].DurationIn = 0.2f;
                    PartySlotAnimators[i].Show(delay: delays[i]);

                    // Reset text effect to TypewriterPop
                    PartySlotTextEffects[i] = TextEffectType.TypewriterPop;

                    // Set the text timer to negative delay so it starts counting up to 0 exactly when the animation starts
                    PartySlotTextTimers[i] = -delays[i];
                }
                else
                {
                    // Empty slots appear instantly (static)
                    PartySlotAnimators[i].DurationIn = 0f;
                    PartySlotAnimators[i].Show(0f);
                    PartySlotTextEffects[i] = TextEffectType.None;
                    PartySlotTextTimers[i] = 0f;
                }
            }
        }
    }
}
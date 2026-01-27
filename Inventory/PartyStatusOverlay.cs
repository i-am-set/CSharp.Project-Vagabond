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
using System.Linq;

namespace ProjectVagabond.UI
{
    // Simplified State - No more "Equip" or "Browse" distinction needed
    internal enum InventoryState { ViewOnly }

    public class PartyStatusOverlay
    {
        public bool IsOpen { get; private set; } = false;

        // Only report hovered if the menu is actually open.
        public bool IsHovered => IsOpen && (CloseButton?.IsHovered ?? false);

        public event Action? OnCloseRequested;

        internal GameState GameState { get; private set; }
        internal SpriteManager SpriteManager { get; private set; }
        internal Global Global { get; private set; }
        internal HapticsManager HapticsManager { get; private set; }

        // UI Elements
        internal ImageButton? CloseButton { get; set; }

        // Party Panels
        internal Rectangle[] PartyMemberPanelAreas { get; } = new Rectangle[4];
        internal List<SpellEquipButton> PartySpellButtons { get; } = new();

        // Animation State
        internal UIAnimator[] PartySlotAnimators { get; } = new UIAnimator[4];
        internal float[] PartySlotTextTimers { get; } = new float[4];
        internal TextEffectType[] PartySlotTextEffects { get; } = new TextEffectType[4];

        internal Vector2 InventoryPositionOffset { get; set; } = Vector2.Zero;

        // Hover Data
        internal int HoveredMemberIndex { get; set; } = -1;
        internal object? HoveredItemData { get; set; }
        internal object? PreviousHoveredItemData { get; set; }
        internal float InfoPanelNameWaveTimer { get; set; } = 0f;
        internal float StatCycleTimer { get; set; } = 0f;

        // Input State
        internal MouseState PreviousMouseState { get; set; }
        internal KeyboardState PreviousKeyboardState { get; set; }

        // Subsystems
        private readonly InventoryDrawer _drawer;
        private readonly InventoryInputHandler _inputHandler;
        private static readonly Random _rng = new Random();

        public PartyStatusOverlay()
        {
            GameState = ServiceLocator.Get<GameState>();
            SpriteManager = ServiceLocator.Get<SpriteManager>();
            Global = ServiceLocator.Get<Global>();
            HapticsManager = ServiceLocator.Get<HapticsManager>();

            // Initialize Party Slot Animators
            for (int i = 0; i < 4; i++)
            {
                PartySlotAnimators[i] = new UIAnimator
                {
                    EntryStyle = EntryExitStyle.SwoopRight,
                    DurationIn = 0.1f,
                    Magnitude = 20f
                };
            }

            _drawer = new InventoryDrawer(this);
            _inputHandler = new InventoryInputHandler(this);
        }

        public void Initialize()
        {
            _inputHandler.InitializeInventoryUI();
            PreviousHoveredItemData = null;
            StatCycleTimer = 0f;
            InventoryPositionOffset = Vector2.Zero;
            PreviousMouseState = Mouse.GetState();
            PreviousKeyboardState = Keyboard.GetState();
        }

        public void Show()
        {
            IsOpen = true;
            // Reset Close Button Animation so it doesn't start in a weird state
            CloseButton?.ResetAnimationState();
            TriggerPartyAnimations();
        }

        public void Hide()
        {
            IsOpen = false;
            // Explicitly reset the button state when hiding.
            CloseButton?.ResetAnimationState();
        }

        public void ForceClose()
        {
            if (IsOpen) Hide();
        }

        public void TriggerCloseRequested()
        {
            OnCloseRequested?.Invoke();
        }

        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState, bool allowAccess, Matrix cameraTransform)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (IsOpen)
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

        private void TriggerPartyAnimations()
        {
            var delays = new List<float> { 0.0f, 0.05f, 0.1f, 0.15f };
            int n = delays.Count;
            while (n > 1)
            {
                n--;
                int k = _rng.Next(n + 1);
                (delays[k], delays[n]) = (delays[n], delays[k]);
            }

            for (int i = 0; i < 4; i++)
            {
                bool isOccupied = i < GameState.PlayerState.Party.Count;
                PartySlotAnimators[i].Reset();

                if (isOccupied)
                {
                    PartySlotAnimators[i].DurationIn = 0.2f;
                    PartySlotAnimators[i].Show(delay: delays[i]);
                    PartySlotTextEffects[i] = TextEffectType.TypewriterPop;
                    PartySlotTextTimers[i] = -delays[i];
                }
                else
                {
                    PartySlotAnimators[i].DurationIn = 0f;
                    PartySlotAnimators[i].Show(0f);
                    PartySlotTextEffects[i] = TextEffectType.None;
                    PartySlotTextTimers[i] = 0f;
                }
            }
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Encounters;
using System;

namespace ProjectVagabond
{
    /// <summary>
    /// A system that manages the "!?" animation that appears on the map just before an encounter dialog is shown.
    /// </summary>
    public class PreEncounterAnimationSystem : ISystem
    {
        private class PreEncounterIndicator
        {
            public bool IsActive;
            public Vector2 WorldPosition;
            public float AnimationTimer;
            public float CurrentScale;
            public Vector2 ShakeOffset;
            public float YOffset;
        }

        // Dependencies
        private readonly EncounterManager _encounterManager;
        private readonly MapRenderer _mapRenderer;
        private readonly GameState _gameState;
        private readonly Global _global;
        private readonly Random _random = new Random();

        // State
        private readonly PreEncounterIndicator _indicator = new PreEncounterIndicator();
        private EncounterData _pendingEncounter;

        // Animation Tuning
        private const float MaxScale = 3.0f;
        private const float FinalScale = 1.2f;
        private const float TimeToMaxSize = 0.4f; // Time to expand
        private const float AnimationDuration = TimeToMaxSize + 0.2f; // Total duration (expand + shrink)

        public bool IsAnimating => _indicator.IsActive;

        public PreEncounterAnimationSystem()
        {
            _encounterManager = ServiceLocator.Get<EncounterManager>();
            _mapRenderer = ServiceLocator.Get<MapRenderer>();
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            EventBus.Subscribe<GameEvents.EncounterTriggered>(HandleEncounterTriggered);
        }

        private void HandleEncounterTriggered(GameEvents.EncounterTriggered e)
        {
            // Don't start a new animation if one is already playing
            if (_indicator.IsActive) return;

            _pendingEncounter = e.Encounter;
            _indicator.WorldPosition = _gameState.PlayerWorldPos;
            _indicator.AnimationTimer = 0f;
            _indicator.CurrentScale = 0f;
            _indicator.ShakeOffset = Vector2.Zero;
            _indicator.YOffset = 0f;
            _indicator.IsActive = true;
        }

        public void Update(GameTime gameTime)
        {
            if (!_indicator.IsActive) return;

            _indicator.AnimationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            float progress = MathHelper.Clamp(_indicator.AnimationTimer / AnimationDuration, 0f, 1f);

            if (_indicator.AnimationTimer < TimeToMaxSize)
            {
                // Phase 1: Expand from 0 to MaxScale
                float expandProgress = _indicator.AnimationTimer / TimeToMaxSize;
                _indicator.CurrentScale = MathHelper.Lerp(0f, MaxScale, Easing.EaseOutCubic(expandProgress));
            }
            else
            {
                // Phase 2: Shrink from MaxScale to FinalScale
                float shrinkDuration = AnimationDuration - TimeToMaxSize;
                float shrinkProgress = (_indicator.AnimationTimer - TimeToMaxSize) / shrinkDuration;
                _indicator.CurrentScale = MathHelper.Lerp(MaxScale, FinalScale, Easing.EaseInQuad(shrinkProgress));
            }

            if (progress >= 1.0f)
            {
                _indicator.IsActive = false;
                _encounterManager.TriggerEncounter(_pendingEncounter.Id);
                _pendingEncounter = null;
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (!_indicator.IsActive) return;

            Vector2? screenPos = _mapRenderer.MapCoordsToScreen(_indicator.WorldPosition);
            if (!screenPos.HasValue) return;

            string text = "!?";
            Vector2 textSize = font.MeasureString(text);
            Vector2 textOrigin = textSize / 2f;

            // Position the indicator centered horizontally and just above the player's tile
            Vector2 drawPosition = screenPos.Value + new Vector2(Global.GRID_CELL_SIZE / 2f, -textSize.Y / 2f + 2);

            // Draw main text without an outline
            spriteBatch.DrawString(font, text, drawPosition, _global.Palette_Red, 0f, textOrigin, _indicator.CurrentScale, SpriteEffects.None, 0f);
        }
    }
}
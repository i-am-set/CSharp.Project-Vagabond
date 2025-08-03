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
            public Vector2 ShakeOffset;
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
        private const float INDICATOR_SCALE = 1f;
        private const float SHAKE_MAGNITUDE = 1.0f;
        private const float ANIMATION_DURATION = 0.7f;

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
            _indicator.ShakeOffset = Vector2.Zero;
            _indicator.IsActive = true;
        }

        public void Update(GameTime gameTime)
        {
            if (!_indicator.IsActive) return;

            _indicator.AnimationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Apply a random shake offset each frame
            _indicator.ShakeOffset = new Vector2(
                (_random.NextSingle() * 2f - 1f) * SHAKE_MAGNITUDE,
                (_random.NextSingle() * 2f - 1f) * SHAKE_MAGNITUDE
            );

            if (_indicator.AnimationTimer >= ANIMATION_DURATION)
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

            // Position the indicator centered horizontally and just above the player's tile, then apply the shake
            Vector2 drawPosition = screenPos.Value + new Vector2(Global.GRID_CELL_SIZE / 2f, -textSize.Y / 2f) + _indicator.ShakeOffset;

            // Draw main text with a fixed scale
            spriteBatch.DrawString(font, text, drawPosition, _global.Palette_Red, 0f, textOrigin, INDICATOR_SCALE, SpriteEffects.None, 0f);
        }
    }
}
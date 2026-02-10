#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond.UI
{
    public class CursorManager
    {
        private readonly SpriteManager _spriteManager;
        private readonly Dictionary<CursorState, (string assetName, float frameDuration)> _cursorMappings;

        private CursorState _requestedState;
        private CursorState _currentState;

        private (Texture2D Texture, Rectangle[] Frames) _currentSpriteAnimation;
        private float _frameTimer;
        private int _currentFrameIndex;

        // The pixel coordinate of the cursor's "tip" within its sprite frame.
        private static readonly Vector2 CURSOR_HOTSPOT = new Vector2(7, 7);

        // WATCHDOG LOGIC:
        // _hideRequested is reset to false every time we Draw.
        // To hide the cursor, a system must call Hide() EVERY FRAME.
        // This prevents the "Sticky Cursor" bug if a system crashes or exits without resetting visibility.
        private bool _hideRequested = false;

        // Allows manual permanent hiding (e.g. for cutscenes)
        public bool Visible { get; set; } = true;

        // Allows external systems (like HUD) to offset the sprite in VIRTUAL pixels.
        public Vector2 VisualOffset { get; set; } = Vector2.Zero;

        public CursorManager()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _cursorMappings = new Dictionary<CursorState, (string, float)>
            {
                { CursorState.Default, ("cursor_default", 1f / 12f) },
                { CursorState.HoverClickable, ("cursor_hover_clickable", 1f / 12f) },
                { CursorState.Hint, ("cursor_hover_hint", 1f / 12f) },
                { CursorState.HoverClickableHint, ("cursor_hover_clickable_hint", 1f / 12f) },
                { CursorState.HoverDraggable, ("cursor_hover_draggable", 1f / 12f) },
                { CursorState.Dragging, ("cursor_dragging_draggable", 1f / 12f) },
            };

            _requestedState = CursorState.Default;
            _currentState = CursorState.Default;
            _currentSpriteAnimation = _spriteManager.GetCursorAnimation("cursor_default");
        }

        public void SetState(CursorState state)
        {
            if (state > _requestedState)
            {
                _requestedState = state;
            }
        }

        /// <summary>
        /// Suppresses the cursor rendering for THIS FRAME only.
        /// Must be called every update loop to keep the cursor hidden.
        /// </summary>
        public void Hide()
        {
            _hideRequested = true;
        }

        public void Update(GameTime gameTime)
        {
            if (_requestedState != _currentState)
            {
                _currentState = _requestedState;
                var (assetName, _) = _cursorMappings.GetValueOrDefault(_currentState, _cursorMappings[CursorState.Default]);
                _currentSpriteAnimation = _spriteManager.GetCursorAnimation(assetName);
                _currentFrameIndex = 0;
                _frameTimer = 0f;
            }

            _requestedState = CursorState.Default;

            if (_currentSpriteAnimation.Frames.Length > 1)
            {
                var (_, frameDuration) = _cursorMappings.GetValueOrDefault(_currentState, _cursorMappings[CursorState.Default]);
                _frameTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_frameTimer >= frameDuration)
                {
                    _frameTimer -= frameDuration;
                    _currentFrameIndex = (_currentFrameIndex + 1) % _currentSpriteAnimation.Frames.Length;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 screenPosition, float scale)
        {
            // WATCHDOG CHECK:
            // 1. Check if we should draw
            bool shouldDraw = Visible && !_hideRequested;

            // 2. Reset the request flag immediately. 
            // If the HUD stops calling Hide(), this will remain false next frame, and the cursor will reappear.
            _hideRequested = false;

            if (!shouldDraw) return;

            if (_currentSpriteAnimation.Texture == null || _currentSpriteAnimation.Frames.Length == 0)
            {
                return;
            }

            var mouseState = Mouse.GetState();
            var drawPosition = screenPosition + (VisualOffset * scale);

            // Note: The click offset (+2) remains unscaled (raw screen pixels) 
            // to maintain the "subtle" feel described in original comments.
            if (mouseState.LeftButton == ButtonState.Pressed || mouseState.RightButton == ButtonState.Pressed)
            {
                drawPosition.Y += 2;
            }

            var sourceRect = _currentSpriteAnimation.Frames[_currentFrameIndex];

            spriteBatch.Draw(
                _currentSpriteAnimation.Texture,
                drawPosition,
                sourceRect,
                Color.White,
                0f,
                CURSOR_HOTSPOT,
                scale,
                SpriteEffects.None,
                0f
            );
        }
    }
}
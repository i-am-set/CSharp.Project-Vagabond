#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

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

        public CursorManager()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _cursorMappings = new Dictionary<CursorState, (string, float)>
            {
                { CursorState.Default, ("cursor_default", 1f / 12f) },
                { CursorState.HoverClickable, ("cursor_hover_clickable", 1f / 12f) },
                { CursorState.HoverDraggable, ("cursor_hover_draggable", 1f / 12f) },
                { CursorState.Dragging, ("cursor_dragging_draggable", 1f / 12f) },
                // Future cursor states like Click can be mapped to their assets here.
                // { CursorState.Click, ("cursor_click", 1f / 12f) },
            };

            _requestedState = CursorState.Default;
            _currentState = CursorState.Default;
            _currentSpriteAnimation = _spriteManager.GetCursorAnimation("cursor_default");
        }

        public void SetState(CursorState state)
        {
            // The highest priority state set this frame wins.
            // Dragging > Click > HoverClickable > HoverDraggable > Hover > Default
            if (state > _requestedState)
            {
                _requestedState = state;
            }
        }

        public void Update(GameTime gameTime)
        {
            // Process the requested state from the last frame's UI updates.
            if (_requestedState != _currentState)
            {
                _currentState = _requestedState;
                var (assetName, _) = _cursorMappings.GetValueOrDefault(_currentState, _cursorMappings[CursorState.Default]);
                _currentSpriteAnimation = _spriteManager.GetCursorAnimation(assetName);
                _currentFrameIndex = 0;
                _frameTimer = 0f;
            }

            // Reset the requested state for the next frame.
            // UI elements will set it again if they are hovered/clicked.
            _requestedState = CursorState.Default;

            // Update animation of the current cursor.
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

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_currentSpriteAnimation.Texture == null || _currentSpriteAnimation.Frames.Length == 0)
            {
                return;
            }

            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);

            // Add a 1-pixel vertical offset if either mouse button is pressed.
            if (mouseState.LeftButton == ButtonState.Pressed || mouseState.RightButton == ButtonState.Pressed)
            {
                virtualMousePos.Y += 1;
            }

            var sourceRect = _currentSpriteAnimation.Frames[_currentFrameIndex];

            // The cursor is drawn with its hotspot as the origin, aligning the tip with the mouse position.
            // The color is set to white, as the BlendState will handle the inversion.
            spriteBatch.DrawSnapped(
                _currentSpriteAnimation.Texture,
                virtualMousePos,
                sourceRect,
                Color.White,
                0f,
                CURSOR_HOTSPOT,
                1f,
                SpriteEffects.None,
                0f // Topmost layer
            );
        }
    }
}
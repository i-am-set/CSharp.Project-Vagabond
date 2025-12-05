#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Manages the state, animation, and rendering of a player-controlled combatant's sprite.
    /// </summary>
    public class PlayerCombatSprite
    {
        private Texture2D? _texture;
        private Texture2D? _silhouette;
        private Vector2 _position;
        private Vector2 _origin;
        private string _archetypeId;

        // Animation
        private float _frameTimer;
        private int _frameIndex;
        private int _frameCount;
        private int _frameWidth;
        private int _frameHeight;
        private const float FRAME_DURATION = 0.2f; // ~5 FPS

        public PlayerCombatSprite(string archetypeId)
        {
            _archetypeId = archetypeId;
        }

        private void Initialize()
        {
            if (_texture == null)
            {
                var spriteManager = ServiceLocator.Get<SpriteManager>();

                if (_archetypeId == "player")
                {
                    _texture = spriteManager.PlayerHeartSpriteSheet;
                    _silhouette = spriteManager.PlayerHeartSpriteSheetSilhouette;
                    _frameWidth = 32;
                    _frameHeight = 32;
                }
                else
                {
                    // For allies that might be monster archetypes
                    _texture = spriteManager.GetEnemySprite(_archetypeId);
                    _silhouette = spriteManager.GetEnemySpriteSilhouette(_archetypeId);

                    // Determine frame size based on texture
                    // Assuming standard enemy sheets are strips. 
                    // Major enemies are 96x96, normal are 64x64.
                    bool isMajor = spriteManager.IsMajorEnemySprite(_archetypeId);
                    _frameWidth = isMajor ? 96 : 64;
                    _frameHeight = isMajor ? 96 : 64;
                }

                if (_texture != null)
                {
                    _origin = new Vector2(_frameWidth / 2f, _frameHeight / 2f);
                    _frameCount = _texture.Width / _frameWidth;
                }
            }
        }

        public void SetPosition(Vector2 newPosition)
        {
            _position = newPosition;
        }

        public void Update(GameTime gameTime)
        {
            Initialize();
            if (_texture == null || _frameCount <= 1) return;

            _frameTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_frameTimer >= FRAME_DURATION)
            {
                _frameTimer -= FRAME_DURATION;
                _frameIndex = (_frameIndex + 1) % _frameCount;
            }
        }

        public void Draw(SpriteBatch spriteBatch, BattleAnimationManager animationManager, BattleCombatant combatant, Color? tintColorOverride = null, bool isHighlighted = false, float pulseAlpha = 1f, bool asSilhouette = false, Color? silhouetteColor = null)
        {
            Initialize();
            if (_texture == null || combatant == null) return;

            var global = ServiceLocator.Get<Global>();

            var sourceRectangle = new Rectangle(_frameIndex * _frameWidth, 0, _frameWidth, _frameHeight);

            // Get hit flash state and apply shake
            var hitFlashState = animationManager.GetHitFlashState(combatant.CombatantID);
            Vector2 shakeOffset = hitFlashState?.ShakeOffset ?? Vector2.Zero;
            bool isFlashingWhite = hitFlashState != null && hitFlashState.IsCurrentlyWhite;

            var topLeftPosition = new Point(
                (int)MathF.Round(_position.X - _origin.X + shakeOffset.X),
                (int)MathF.Round(_position.Y - _origin.Y + shakeOffset.Y)
            );

            // --- Silhouette Mode (e.g. for non-selectable targets) ---
            if (asSilhouette && _silhouette != null)
            {
                var mainRect = new Rectangle(topLeftPosition.X, topLeftPosition.Y, _frameWidth, _frameHeight);
                spriteBatch.Draw(_silhouette, mainRect, sourceRectangle, silhouetteColor ?? Color.Gray, 0f, Vector2.Zero, SpriteEffects.None, 0.5f);
                return;
            }

            Color mainColor = tintColorOverride ?? Color.White;
            Color outlineColor = isHighlighted ? Color.Yellow : (tintColorOverride ?? global.Palette_DarkGray);

            // --- Draw Outline ---
            if (_silhouette != null)
            {
                // Draw outline using integer-based rectangles offset from the snapped top-left position.
                var rect = new Rectangle(0, 0, _frameWidth, _frameHeight);

                rect.Location = new Point(topLeftPosition.X - 1, topLeftPosition.Y);
                spriteBatch.Draw(_silhouette, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);

                rect.Location = new Point(topLeftPosition.X + 1, topLeftPosition.Y);
                spriteBatch.Draw(_silhouette, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);

                rect.Location = new Point(topLeftPosition.X, topLeftPosition.Y - 1);
                spriteBatch.Draw(_silhouette, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);

                rect.Location = new Point(topLeftPosition.X, topLeftPosition.Y + 1);
                spriteBatch.Draw(_silhouette, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);
            }

            // --- Draw Main Sprite ---
            var mainRectDraw = new Rectangle(topLeftPosition.X, topLeftPosition.Y, _frameWidth, _frameHeight);

            // Flip horizontally if it's NOT the main player heart (assuming allies face right like enemies)
            // Actually, usually allies face right (towards enemies) and enemies face left.
            // If the sprite is an enemy sprite reused, it faces left by default.
            // So we flip it to face right.
            // The Player Heart faces right by default.
            SpriteEffects effects = SpriteEffects.None;
            if (_archetypeId != "player")
            {
                effects = SpriteEffects.FlipHorizontally;
            }

            spriteBatch.Draw(_texture, mainRectDraw, sourceRectangle, mainColor, 0f, Vector2.Zero, effects, 0.5f);

            // --- Draw Highlight Overlay ---
            if (isHighlighted && _silhouette != null)
            {
                spriteBatch.Draw(_silhouette, mainRectDraw, sourceRectangle, Color.Yellow * pulseAlpha, 0f, Vector2.Zero, effects, 0.505f);
            }

            // --- Draw Flash Overlay ---
            if (isFlashingWhite && _silhouette != null)
            {
                spriteBatch.Draw(_silhouette, mainRectDraw, sourceRectangle, Color.White * 0.8f, 0f, Vector2.Zero, effects, 0.51f);
            }
        }
    }
}
#nullable restore

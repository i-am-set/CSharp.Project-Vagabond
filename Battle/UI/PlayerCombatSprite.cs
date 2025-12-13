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
        private Texture2D? _altTexture; // NEW: For player portrait animation
        private Texture2D? _altSilhouette; // NEW: For player portrait animation
        private Vector2 _position;
        private Vector2 _origin;
        private string _archetypeId;

        // Animation
        private float _frameTimer;
        private int _frameIndex;
        private int _frameCount;
        private int _frameWidth;
        private int _frameHeight;
        private const float FRAME_DURATION = 0.2f; // ~5 FPS for enemies

        private bool _useAltFrame = false; // NEW: Toggles between main and alt portrait

        // Noise generator for organic sway
        private static readonly SeededPerlin _swayNoise = new SeededPerlin(8888);

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
                    // Use Portrait Sheets
                    _texture = spriteManager.PlayerPortraitsSpriteSheet;
                    _silhouette = spriteManager.PlayerPortraitsSpriteSheetSilhouette;
                    _altTexture = spriteManager.PlayerPortraitsAltSpriteSheet;
                    _altSilhouette = spriteManager.PlayerPortraitsAltSpriteSheetSilhouette;
                    _frameWidth = 32;
                    _frameHeight = 32;
                    _frameCount = 1; // Not used for strip animation logic
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

                    if (_texture != null)
                    {
                        _frameCount = _texture.Width / _frameWidth;
                    }
                }

                if (_texture != null)
                {
                    _origin = new Vector2(_frameWidth / 2f, _frameHeight / 2f);
                }
            }
        }

        public void SetPosition(Vector2 newPosition)
        {
            _position = newPosition;
        }

        public void Update(GameTime gameTime, bool isActive)
        {
            Initialize();
            if (_texture == null) return;

            // If not active (not their turn), reset to base frame and do not animate
            if (!isActive)
            {
                _useAltFrame = false;
                _frameIndex = 0;
                _frameTimer = 0f;
                return;
            }

            if (_archetypeId == "player")
            {
                // When active, simply use the alt frame (static pose)
                _useAltFrame = true;
            }
            else
            {
                // Enemy animation logic (cycling frames)
                _frameTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_frameCount > 1 && _frameTimer >= FRAME_DURATION)
                {
                    _frameTimer -= FRAME_DURATION;
                    _frameIndex = (_frameIndex + 1) % _frameCount;
                }
            }
        }

        public Rectangle GetVisibleBounds(BattleAnimationManager animationManager, BattleCombatant combatant)
        {
            Initialize();
            if (_texture == null) return Rectangle.Empty;

            var spriteManager = ServiceLocator.Get<SpriteManager>();

            // Get hit flash state for shake
            var hitFlashState = animationManager.GetHitFlashState(combatant.CombatantID);
            Vector2 shakeOffset = hitFlashState?.ShakeOffset ?? Vector2.Zero;

            // Calculate top-left position (same as Draw)
            var topLeftPosition = new Point(
                (int)MathF.Round(_position.X - _origin.X + shakeOffset.X),
                (int)MathF.Round(_position.Y - _origin.Y + shakeOffset.Y)
            );

            if (_archetypeId == "player")
            {
                // For players, return the full frame bounds + padding
                var rect = new Rectangle(topLeftPosition.X, topLeftPosition.Y, _frameWidth, _frameHeight);
                rect.Inflate(2, 2);
                return rect;
            }

            // Get pixel offsets for the current frame (Enemies only)
            var topOffsets = spriteManager.GetEnemySpriteTopPixelOffsets(_archetypeId);
            var leftOffsets = spriteManager.GetEnemySpriteLeftPixelOffsets(_archetypeId);
            var rightOffsets = spriteManager.GetEnemySpriteRightPixelOffsets(_archetypeId);
            var bottomOffsets = spriteManager.GetEnemySpriteBottomPixelOffsets(_archetypeId);

            if (topOffsets == null || _frameIndex >= topOffsets.Length)
            {
                // Fallback to full frame if data missing
                var fallbackRect = new Rectangle(topLeftPosition.X, topLeftPosition.Y, _frameWidth, _frameHeight);
                fallbackRect.Inflate(2, 2);
                return fallbackRect;
            }

            int t = topOffsets[_frameIndex];
            int l = leftOffsets[_frameIndex];
            int r = rightOffsets[_frameIndex];
            int b = bottomOffsets[_frameIndex];

            if (t == int.MaxValue) return Rectangle.Empty; // Empty frame

            // Calculate precise rect
            int x = topLeftPosition.X + l;
            int y = topLeftPosition.Y + t;
            int w = (r - l) + 1;
            int h = (b - t) + 1;

            var preciseRect = new Rectangle(x, y, w, h);
            preciseRect.Inflate(2, 2); // Add 2px padding on all sides
            return preciseRect;
        }

        public Rectangle GetStaticBounds(BattleAnimationManager animationManager, BattleCombatant combatant)
        {
            Initialize();
            if (_texture == null) return Rectangle.Empty;

            var spriteManager = ServiceLocator.Get<SpriteManager>();

            // Get hit flash state for shake (consistent with GetVisibleBounds)
            var hitFlashState = animationManager.GetHitFlashState(combatant.CombatantID);
            Vector2 shakeOffset = hitFlashState?.ShakeOffset ?? Vector2.Zero;

            // Calculate top-left position
            var topLeftPosition = new Point(
                (int)MathF.Round(_position.X - _origin.X + shakeOffset.X),
                (int)MathF.Round(_position.Y - _origin.Y + shakeOffset.Y)
            );

            if (_archetypeId == "player")
            {
                var rect = new Rectangle(topLeftPosition.X, topLeftPosition.Y, _frameWidth, _frameHeight);
                rect.Inflate(4, 4);
                return rect;
            }

            // Use Frame 0 for static size
            int staticFrameIndex = 0;

            var topOffsets = spriteManager.GetEnemySpriteTopPixelOffsets(_archetypeId);
            var leftOffsets = spriteManager.GetEnemySpriteLeftPixelOffsets(_archetypeId);
            var rightOffsets = spriteManager.GetEnemySpriteRightPixelOffsets(_archetypeId);
            var bottomOffsets = spriteManager.GetEnemySpriteBottomPixelOffsets(_archetypeId);

            if (topOffsets == null || staticFrameIndex >= topOffsets.Length)
            {
                var fallbackRect = new Rectangle(topLeftPosition.X, topLeftPosition.Y, _frameWidth, _frameHeight);
                fallbackRect.Inflate(4, 4); // Increased padding
                return fallbackRect;
            }

            int t = topOffsets[staticFrameIndex];
            int l = leftOffsets[staticFrameIndex];
            int r = rightOffsets[staticFrameIndex];
            int b = bottomOffsets[staticFrameIndex];

            if (t == int.MaxValue) return Rectangle.Empty;

            int x = topLeftPosition.X + l;
            int y = topLeftPosition.Y + t;
            int w = (r - l) + 1;
            int h = (b - t) + 1;

            var preciseRect = new Rectangle(x, y, w, h);
            preciseRect.Inflate(4, 4); // Increased padding (was 2,2)
            return preciseRect;
        }

        public void Draw(SpriteBatch spriteBatch, BattleAnimationManager animationManager, BattleCombatant combatant, Color? tintColorOverride = null, bool isHighlighted = false, float pulseAlpha = 1f, bool asSilhouette = false, Color? silhouetteColor = null, GameTime? gameTime = null, Color? highlightColor = null)
        {
            Initialize();
            if (_texture == null || combatant == null) return;

            var global = ServiceLocator.Get<Global>();

            // Determine Texture and Source Rect
            Texture2D textureToDraw = _texture;
            Texture2D? silhouetteToDraw = _silhouette;
            Rectangle sourceRectangle;

            if (_archetypeId == "player")
            {
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                if (spriteManager.PlayerPortraitSourceRects.Count > 0)
                {
                    int index = Math.Clamp(combatant.PortraitIndex, 0, spriteManager.PlayerPortraitSourceRects.Count - 1);
                    sourceRectangle = spriteManager.PlayerPortraitSourceRects[index];
                }
                else
                {
                    sourceRectangle = new Rectangle(0, 0, 32, 32);
                }

                if (_useAltFrame && _altTexture != null)
                {
                    textureToDraw = _altTexture;
                    silhouetteToDraw = _altSilhouette;
                }
            }
            else
            {
                sourceRectangle = new Rectangle(_frameIndex * _frameWidth, 0, _frameWidth, _frameHeight);
            }

            // Get hit flash state and apply shake
            var hitFlashState = animationManager.GetHitFlashState(combatant.CombatantID);
            Vector2 shakeOffset = hitFlashState?.ShakeOffset ?? Vector2.Zero;
            bool isFlashingWhite = hitFlashState != null && hitFlashState.IsCurrentlyWhite;

            // Calculate top-left position based on center position
            var topLeftPosition = new Point(
                (int)MathF.Round(_position.X - _origin.X + shakeOffset.X),
                (int)MathF.Round(_position.Y - _origin.Y + shakeOffset.Y)
            );

            // --- Silhouette Mode (e.g. for non-selectable targets) ---
            if (asSilhouette && silhouetteToDraw != null)
            {
                var mainRect = new Rectangle(topLeftPosition.X, topLeftPosition.Y, _frameWidth, _frameHeight);
                spriteBatch.Draw(silhouetteToDraw, mainRect, sourceRectangle, silhouetteColor ?? Color.Gray, 0f, Vector2.Zero, SpriteEffects.None, 0.5f);
                return;
            }

            // NEW: Highlight Mode (Full Yellow Silhouette)
            if (isHighlighted && silhouetteToDraw != null)
            {
                var mainRect = new Rectangle(topLeftPosition.X, topLeftPosition.Y, _frameWidth, _frameHeight);
                SpriteEffects effects = SpriteEffects.None;
                if (_archetypeId != "player") effects = SpriteEffects.FlipHorizontally;

                // Use specific highlight color if provided, else default to Yellow
                Color hColor = highlightColor ?? Color.Yellow;
                spriteBatch.Draw(silhouetteToDraw, mainRect, sourceRectangle, hColor, 0f, Vector2.Zero, effects, 0.5f);

                // Draw Indicator ONLY if the color is Yellow (The "Flash" state)
                if (hColor == Color.Yellow)
                {
                    var indicator = ServiceLocator.Get<SpriteManager>().TargetingIndicatorSprite;
                    if (indicator != null && gameTime != null)
                    {
                        // Calculate Visual Center Offset
                        Vector2 visualCenterOffset = ServiceLocator.Get<SpriteManager>().GetVisualCenterOffset(_archetypeId);

                        // Base center of the sprite rect
                        Vector2 spriteCenter = new Vector2(mainRect.Center.X, mainRect.Center.Y);

                        // Apply visual center offset
                        // X is geometric center, Y is visual center (center of mass)
                        Vector2 targetCenter = new Vector2(spriteCenter.X, spriteCenter.Y + visualCenterOffset.Y);

                        // Apply Animation Math (Perlin Noise)
                        float t = (float)gameTime.TotalGameTime.TotalSeconds * global.TargetIndicatorNoiseSpeed;

                        // FIX: Scramble the seed significantly to ensure different targets have independent movement
                        int seed = (combatant.CombatantID.GetHashCode() + 1000) * 93821;

                        // Noise lookups (offsets ensure different axes don't sync)
                        float nX = _swayNoise.Noise(t, seed);
                        float nY = _swayNoise.Noise(t, seed + 100);

                        float swayX = nX * global.TargetIndicatorOffsetX;
                        float swayY = nY * global.TargetIndicatorOffsetY;

                        float rotation = 0f;
                        float indicatorScale = 1.0f;

                        Vector2 animatedPos = targetCenter + new Vector2(swayX, swayY) + shakeOffset;
                        Vector2 origin = new Vector2(indicator.Width / 2f, indicator.Height / 2f);

                        spriteBatch.DrawSnapped(indicator, animatedPos, null, Color.White, rotation, origin, indicatorScale, SpriteEffects.None, 0f);
                    }
                }
                return;
            }

            Color mainColor = tintColorOverride ?? Color.White;
            Color outlineColor = tintColorOverride ?? global.Palette_DarkGray;

            // --- Draw Outline ---
            if (silhouetteToDraw != null)
            {
                // Draw outline using integer-based rectangles offset from the snapped top-left position.
                var rect = new Rectangle(0, 0, _frameWidth, _frameHeight);

                rect.Location = new Point(topLeftPosition.X - 1, topLeftPosition.Y);
                spriteBatch.Draw(silhouetteToDraw, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);

                rect.Location = new Point(topLeftPosition.X + 1, topLeftPosition.Y);
                spriteBatch.Draw(silhouetteToDraw, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);

                rect.Location = new Point(topLeftPosition.X, topLeftPosition.Y - 1);
                spriteBatch.Draw(silhouetteToDraw, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);

                rect.Location = new Point(topLeftPosition.X, topLeftPosition.Y + 1);
                spriteBatch.Draw(silhouetteToDraw, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);
            }

            // --- Draw Main Sprite ---
            var mainRectDraw = new Rectangle(topLeftPosition.X, topLeftPosition.Y, _frameWidth, _frameHeight);

            // Flip horizontally if it's NOT the player (assuming allies face right like enemies)
            // Actually, usually allies face right (towards enemies) and enemies face left.
            // If the sprite is an enemy sprite reused, it faces left by default.
            // So we flip it to face right.
            // The Player Portraits face right by default.
            SpriteEffects spriteEffects = SpriteEffects.None;
            if (_archetypeId != "player")
            {
                spriteEffects = SpriteEffects.FlipHorizontally;
            }

            spriteBatch.Draw(textureToDraw, mainRectDraw, sourceRectangle, mainColor, 0f, Vector2.Zero, spriteEffects, 0.5f);

            // --- Draw Flash Overlay ---
            if (isFlashingWhite && silhouetteToDraw != null)
            {
                spriteBatch.Draw(silhouetteToDraw, mainRectDraw, sourceRectangle, Color.White * 0.8f, 0f, Vector2.Zero, spriteEffects, 0.51f);
            }
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
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
        private Texture2D? _altTexture;
        private Texture2D? _altSilhouette;
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

        private bool _useAltFrame = false;

        // Selection Animation State
        private enum SelectionState { None, Jumping, Bobbing }
        private SelectionState _selectionState = SelectionState.None;
        private float _selectionTimer = 0f;
        private float _selectionOffsetY = 0f;

        private const float SELECTION_JUMP_DURATION = 0.25f;
        private const float SELECTION_JUMP_HEIGHT = 4f;
        private const float SELECTION_BOB_CYCLE_DURATION = 4.0f;

        // Squash and Stretch State
        private Vector2 _scale = Vector2.One;

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

        /// <summary>
        /// Applies an immediate scale distortion that decays over time.
        /// </summary>
        /// <param name="x">X Scale (e.g. 1.5 for wide)</param>
        /// <param name="y">Y Scale (e.g. 0.5 for flat)</param>
        public void TriggerSquash(float x, float y)
        {
            _scale = new Vector2(x, y);
        }

        public void Update(GameTime gameTime, bool isActive)
        {
            Initialize();
            if (_texture == null) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update Squash and Stretch (Elastic Recovery)
            var global = ServiceLocator.Get<Global>();
            _scale = Vector2.Lerp(_scale, Vector2.One, dt * global.SquashRecoverySpeed);

            // If not active (not their turn), reset to base frame and do not animate
            if (!isActive)
            {
                _selectionState = SelectionState.None;
                _selectionTimer = 0f;
                _selectionOffsetY = 0f;
                _useAltFrame = false;
                _frameIndex = 0;
                _frameTimer = 0f;
                return;
            }

            if (_archetypeId == "player")
            {
                // Initialize directly to Bobbing (Skip Jump)
                if (_selectionState == SelectionState.None)
                {
                    _selectionState = SelectionState.Bobbing;
                    _selectionTimer = 0f;
                }

                if (_selectionState == SelectionState.Bobbing)
                {
                    _selectionTimer += dt;
                    float t = _selectionTimer % SELECTION_BOB_CYCLE_DURATION;

                    // First half: Alt Sprite (No vertical bob)
                    if (t < SELECTION_BOB_CYCLE_DURATION / 2f)
                    {
                        _selectionOffsetY = 0f;
                        _useAltFrame = true;
                    }
                    // Second half: Main Sprite
                    else
                    {
                        _selectionOffsetY = 0f;
                        _useAltFrame = false;
                    }
                }
            }
            else
            {
                // Enemy animation logic (cycling frames)
                _frameTimer += dt;
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

            // Calculate top-left position (same as Draw, including selection offset)
            var topLeftPosition = new Point(
                (int)MathF.Round(_position.X - _origin.X + shakeOffset.X),
                (int)MathF.Round(_position.Y - _origin.Y + shakeOffset.Y + _selectionOffsetY)
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

            // Calculate top-left position (Static bounds do NOT include selection offset)
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

        public void Draw(SpriteBatch spriteBatch, BattleAnimationManager animationManager, BattleCombatant combatant, Color? tintColorOverride = null, bool isHighlighted = false, float pulseAlpha = 1f, bool asSilhouette = false, Color? silhouetteColor = null, GameTime? gameTime = null, Color? highlightColor = null, Color? outlineColorOverride = null, float scale = 1.0f, Color? lowHealthOverlay = null)
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

            // Calculate top-left position based on center position + selection offset
            var topLeftPosition = new Point(
                (int)MathF.Round(_position.X - _origin.X + shakeOffset.X),
                (int)MathF.Round(_position.Y - _origin.Y + shakeOffset.Y + _selectionOffsetY)
            );

            Color mainColor = tintColorOverride ?? Color.White;
            float alpha = mainColor.A / 255f;
            Color outlineColor = (outlineColorOverride ?? global.Palette_DarkGray) * alpha;

            // Flip horizontally if it's NOT the player (assuming allies face right like enemies)
            SpriteEffects spriteEffects = SpriteEffects.None;
            if (_archetypeId != "player")
            {
                spriteEffects = SpriteEffects.FlipHorizontally;
            }

            Vector2 mainDrawPos = new Vector2(topLeftPosition.X + _frameWidth / 2f, topLeftPosition.Y + _frameHeight / 2f);
            Vector2 mainOrigin = new Vector2(_frameWidth / 2f, _frameHeight / 2f);

            // Combine external scale with internal squash/stretch
            Vector2 finalScale = new Vector2(scale * _scale.X, scale * _scale.Y);

            // --- Draw Outline (Always, if silhouette exists) ---
            if (silhouetteToDraw != null)
            {
                Color cInner = global.Palette_Black * alpha;
                Color cOuter = outlineColor;

                // Layer 2: Outer Color (Cardinals 2, Diagonals 1)
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(-2, 0), sourceRectangle, cOuter, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(2, 0), sourceRectangle, cOuter, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(0, -2), sourceRectangle, cOuter, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(0, 2), sourceRectangle, cOuter, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);

                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(-1, -1), sourceRectangle, cOuter, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(1, -1), sourceRectangle, cOuter, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(-1, 1), sourceRectangle, cOuter, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(1, 1), sourceRectangle, cOuter, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);

                // Layer 1: Inner Black (Cardinals 1)
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(-1, 0), sourceRectangle, cInner, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(1, 0), sourceRectangle, cInner, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(0, -1), sourceRectangle, cInner, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos + new Vector2(0, 1), sourceRectangle, cInner, 0f, mainOrigin, finalScale, spriteEffects, 0.49f);
            }

            // --- Draw Body ---
            if (asSilhouette && silhouetteToDraw != null)
            {
                // Draw Silhouette Body
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos, sourceRectangle, silhouetteColor ?? Color.Gray, 0f, mainOrigin, finalScale, spriteEffects, 0.5f);
            }
            else if (isHighlighted && silhouetteToDraw != null)
            {
                // Draw Highlight Body
                Color hColor = highlightColor ?? Color.Yellow;
                spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos, sourceRectangle, hColor, 0f, mainOrigin, finalScale, spriteEffects, 0.5f);

                // Draw Indicator ONLY if the color is Yellow (The "Flash" state)
                if (hColor == Color.Yellow)
                {
                    var indicator = ServiceLocator.Get<SpriteManager>().TargetingIndicatorSprite;
                    if (indicator != null && gameTime != null)
                    {
                        // Calculate Visual Center Offset
                        Vector2 visualCenterOffset = ServiceLocator.Get<SpriteManager>().GetVisualCenterOffset(_archetypeId);

                        // Base center of the sprite rect
                        Vector2 spriteCenter = new Vector2(topLeftPosition.X + _frameWidth / 2f, topLeftPosition.Y + _frameHeight / 2f);

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
                        Vector2 indOrigin = new Vector2(indicator.Width / 2f, indicator.Height / 2f);

                        spriteBatch.DrawSnapped(indicator, animatedPos, null, Color.White, rotation, indOrigin, indicatorScale, SpriteEffects.None, 0f);
                    }
                }
            }
            else
            {
                // Draw Main Sprite
                spriteBatch.DrawSnapped(textureToDraw, mainDrawPos, sourceRectangle, mainColor, 0f, mainOrigin, finalScale, spriteEffects, 0.5f);

                // --- Draw Flash Overlay ---
                if (isFlashingWhite && silhouetteToDraw != null)
                {
                    spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos, sourceRectangle, Color.White * 0.8f, 0f, mainOrigin, finalScale, spriteEffects, 0.51f);
                }

                // --- Draw Low Health Overlay ---
                if (lowHealthOverlay.HasValue && silhouetteToDraw != null)
                {
                    spriteBatch.DrawSnapped(silhouetteToDraw, mainDrawPos, sourceRectangle, lowHealthOverlay.Value * alpha, 0f, mainOrigin, finalScale, spriteEffects, 0.51f);
                }
            }
        }
    }
}

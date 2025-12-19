#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    public class ShopItemButton : Button
    {
        public ShopItem Item { get; }
        private readonly Texture2D _iconTexture;
        private readonly Texture2D _iconSilhouette;
        private readonly BitmapFont _priceFont; // Secondary (5x5)
        private readonly BitmapFont _currencyFont; // Tertiary (3x3)
        private readonly BitmapFont _nameFont;

        // Animation State
        private float _hoverTimer = 0f;

        // TUNING: Animation Speed (Lower = Faster)
        private const float HOVER_ANIM_DURATION = 0.075f;
        private const float HOVER_LIFT_AMOUNT = 1f; // 1px lift

        // Too Expensive / Rejection Animation State
        private float _rejectionShakeTimer = 0f;

        // TUNING: Rejection Shake 
        private const float REJECTION_SHAKE_DURATION = 0.35f;
        private const float REJECTION_SHAKE_MAGNITUDE = 2f; // Pixels
        private const float REJECTION_SHAKE_SPEED = 60f; // Frequency

        // TUNING: Hover Jitter (Passive "Too Expensive" movement)
        private Vector2 _jitterOffset;
        private Vector2 _jitterTarget;
        private float _jitterWaitTimer;
        private bool _jitterReturningToCenter; // State: True = going to center, False = going out
        private const float JITTER_RADIUS = 2f; // Max distance to wander
        private const float JITTER_SPEED = 1f; // Lerp speed
        private const float JITTER_HOLD_TIME = 0.1f; // How long to stay at a point before moving
        private static readonly Random _rng = new Random();

        // Layout Constants
        private const int BUTTON_SIZE = 32;
        private const int SPRITE_SIZE = 16;
        private const int SPRITE_OFFSET = (BUTTON_SIZE - SPRITE_SIZE) / 2; // 8px offset to center sprite

        public ShopItemButton(Rectangle bounds, ShopItem item, Texture2D icon, Texture2D silhouette, BitmapFont priceFont, BitmapFont currencyFont, BitmapFont nameFont)
            : base(bounds, "")
        {
            Item = item;
            _iconTexture = icon;
            _iconSilhouette = silhouette;
            _priceFont = priceFont;
            _currencyFont = currencyFont;
            _nameFont = nameFont;

            // Ensure button is 32x32 (Doubled size)
            Bounds = new Rectangle(bounds.X, bounds.Y, BUTTON_SIZE, BUTTON_SIZE);

            // at Y=600+ (World Space), and passes a world-space mouse state to Update().
            UseScreenCoordinates = false;

            EnableHoverSway = false; // Disable base sway, we handle custom lift
        }

        public void TriggerTooExpensiveAnimation()
        {
            _rejectionShakeTimer = REJECTION_SHAKE_DURATION;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var gameState = ServiceLocator.Get<GameState>();
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            bool canAfford = gameState.PlayerState.Coin >= Item.Price;

            // --- 1. Update Timers ---
            if (isActivated)
            {
                _hoverTimer += dt;
            }
            else
            {
                _hoverTimer -= dt;
            }
            _hoverTimer = Math.Clamp(_hoverTimer, 0f, HOVER_ANIM_DURATION);

            if (_rejectionShakeTimer > 0)
            {
                _rejectionShakeTimer -= dt;
            }

            // --- 2. Update Jitter Logic ---
            // Only jitter if hovered, too expensive, AND not currently doing the violent shake
            if (isActivated && !canAfford && _rejectionShakeTimer <= 0)
            {
                // Move towards target
                _jitterOffset = Vector2.Lerp(_jitterOffset, _jitterTarget, JITTER_SPEED * dt);

                // Check if close enough to target to pick a new one
                if (Vector2.DistanceSquared(_jitterOffset, _jitterTarget) < 0.25f)
                {
                    _jitterWaitTimer += dt;
                    if (_jitterWaitTimer >= JITTER_HOLD_TIME)
                    {
                        _jitterWaitTimer = 0f;

                        if (_jitterReturningToCenter)
                        {
                            // Was returning to center, now pick a random point outward
                            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
                            _jitterTarget = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * JITTER_RADIUS;
                            _jitterReturningToCenter = false;
                        }
                        else
                        {
                            // Was out, now return to center
                            _jitterTarget = Vector2.Zero;
                            _jitterReturningToCenter = true;
                        }
                    }
                }
            }
            else
            {
                // Reset jitter if not active
                _jitterOffset = Vector2.Zero;
                _jitterTarget = Vector2.Zero;
                _jitterWaitTimer = 0f;
                _jitterReturningToCenter = false; // Reset cycle to start by moving out
            }

            // --- 3. Calculate Offsets ---

            // A. Hover Lift (Instant 1px up)
            float liftOffset = isActivated ? -HOVER_LIFT_AMOUNT : 0f;

            // B. Idle Bob (1 pixel up/down) - Only active when NOT hovered
            float idleBob = 0f;
            if (!isActivated)
            {
                // Use TotalGameTime for continuous loop. Speed 5f gives a nice gentle bob.
                idleBob = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 5f) > 0 ? -1f : 0f;
            }

            // C. Shake (from base class - usually unused here, but kept for compatibility)
            var (baseShakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);

            // D. Rejection Shake (The violent X shake on click)
            float rejectionOffsetX = 0f;
            if (_rejectionShakeTimer > 0)
            {
                // Ease down: (CurrentTime / TotalTime) goes from 1.0 to 0.0
                float progress = _rejectionShakeTimer / REJECTION_SHAKE_DURATION;
                float decay = Easing.EaseOutQuad(progress);
                rejectionOffsetX = MathF.Sin(_rejectionShakeTimer * REJECTION_SHAKE_SPEED) * REJECTION_SHAKE_MAGNITUDE * decay;
            }

            // --- 4. Calculate Positions ---

            float totalX = Bounds.X + (horizontalOffset ?? 0f) + baseShakeOffset.X;

            // Static Y: The base position (includes shake/layout offset, but NO bob/lift)
            // Used for Price and Sold text so they don't move.
            float staticY = Bounds.Y + (verticalOffset ?? 0f) + baseShakeOffset.Y;

            // Animated Y: The position for the sprite (includes bob + lift)
            float animatedSpriteY = staticY + SPRITE_OFFSET + liftOffset + idleBob;
            float spriteAnchorX = totalX + SPRITE_OFFSET;

            // Center X for text alignment (Relative to the button center)
            float centerX = totalX + Bounds.Width / 2f;

            // --- Draw Name (Above) - Only if Hovered ---
            if (isActivated && !Item.IsSold)
            {
                string nameText = Item.DisplayName.ToUpper();
                Vector2 nameSize = _nameFont.MeasureString(nameText);

                // Position relative to the lifted sprite position
                Vector2 namePos = new Vector2(
                    centerX - nameSize.X / 2f,
                    animatedSpriteY - nameSize.Y - 2
                );

                spriteBatch.DrawStringSnapped(_nameFont, nameText, namePos, _global.Palette_BrightWhite);
            }

            // --- Draw Icon (Center) ---
            if (Item.IsSold)
            {
                // Draw Empty Slot / Sold State
                // CEMENTED: Use staticY for vertical centering, ignoring bob/lift
                string soldText = "SOLD";
                Vector2 soldSize = _nameFont.MeasureString(soldText);
                // Center within the 32x32 bounds
                Vector2 soldPos = new Vector2(centerX - soldSize.X / 2f, staticY + (Bounds.Height - soldSize.Y) / 2f);

                spriteBatch.DrawStringSnapped(_nameFont, soldText, soldPos, _global.Palette_Red);
            }
            else
            {
                // Draw Icon
                if (_iconTexture != null)
                {
                    // Sprite uses animatedY (Bob + Lift)
                    Vector2 drawPos = new Vector2(spriteAnchorX, animatedSpriteY);

                    // 1. Draw Two-Tone Silhouette Outline (Always)
                    if (_iconSilhouette != null)
                    {
                        Color mainOutlineColor;
                        Color cornerOutlineColor;

                        if (_rejectionShakeTimer > 0)
                        {
                            // Rejection Animation: Flash Red -> White -> Red -> White
                            float flashInterval = REJECTION_SHAKE_DURATION / 4f;
                            int cycle = (int)(_rejectionShakeTimer / flashInterval);
                            bool isRed = cycle % 2 != 0;

                            mainOutlineColor = isRed ? _global.Palette_Red : Color.White;
                            cornerOutlineColor = mainOutlineColor;
                        }
                        else if (isActivated)
                        {
                            if (!canAfford)
                            {
                                // Expensive Hover: White Outline
                                mainOutlineColor = Color.White;
                                cornerOutlineColor = Color.White;
                            }
                            else
                            {
                                // Normal Hover: Bright Outline
                                mainOutlineColor = _global.ItemOutlineColor_Hover;
                                cornerOutlineColor = _global.ItemOutlineColor_Hover_Corner;
                            }
                        }
                        else
                        {
                            // Idle: Gray Outline
                            mainOutlineColor = _global.ItemOutlineColor_Idle;
                            cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;
                        }

                        // Draw Diagonals (Corners) FIRST (Behind)
                        spriteBatch.DrawSnapped(_iconSilhouette, drawPos + new Vector2(-1, -1), null, cornerOutlineColor);
                        spriteBatch.DrawSnapped(_iconSilhouette, drawPos + new Vector2(1, -1), null, cornerOutlineColor);
                        spriteBatch.DrawSnapped(_iconSilhouette, drawPos + new Vector2(-1, 1), null, cornerOutlineColor);
                        spriteBatch.DrawSnapped(_iconSilhouette, drawPos + new Vector2(1, 1), null, cornerOutlineColor);

                        // Draw Cardinals (Main) SECOND (On Top)
                        spriteBatch.DrawSnapped(_iconSilhouette, drawPos + new Vector2(-1, 0), null, mainOutlineColor);
                        spriteBatch.DrawSnapped(_iconSilhouette, drawPos + new Vector2(1, 0), null, mainOutlineColor);
                        spriteBatch.DrawSnapped(_iconSilhouette, drawPos + new Vector2(0, -1), null, mainOutlineColor);
                        spriteBatch.DrawSnapped(_iconSilhouette, drawPos + new Vector2(0, 1), null, mainOutlineColor);
                    }

                    // 2. Draw Body (Always Texture)
                    spriteBatch.DrawSnapped(_iconTexture, drawPos, Color.White);

                    // 3. Draw X Overlay (Too Expensive)
                    // Condition: Hovering AND Too Expensive, OR Animation is playing
                    if ((isActivated && !canAfford) || _rejectionShakeTimer > 0)
                    {
                        if (spriteManager.ShopXIcon != null)
                        {
                            // Calculate X Position:
                            // Base: Center of sprite (drawPos + 8)
                            // Offset: -16 (to center the 32x32 X icon)
                            // Modifiers: + rejectionOffsetX (violent shake) + _jitterOffset.X (hover wander)

                            // Calculate Y Position:
                            // Base: Center of sprite (drawPos + 8)
                            // Offset: -16
                            // Modifiers: + _jitterOffset.Y (hover wander)

                            Vector2 xPos = new Vector2(
                                drawPos.X + 8 - 16 + rejectionOffsetX + _jitterOffset.X,
                                drawPos.Y + 8 - 16 + _jitterOffset.Y
                            );

                            spriteBatch.DrawSnapped(spriteManager.ShopXIcon, xPos, Color.White);
                        }
                    }
                }
            }

            // --- Draw Price (Below) - Uses staticY (Does not bob/lift) ---
            if (!Item.IsSold)
            {
                string priceNum = Item.Price.ToString();
                string currencySymbol = "G";

                // Measure parts
                Vector2 numSize = _priceFont.MeasureString(priceNum);
                Vector2 symSize = _currencyFont.MeasureString(currencySymbol);
                const int spaceWidth = 2;

                float totalWidth = numSize.X + spaceWidth + symSize.X;
                float startX = centerX - totalWidth / 2f;

                // Position relative to the bottom of the sprite slot (16px height)
                // spriteAnchorY is the top of the sprite slot. +16 is bottom. +2 is padding.
                float drawY = (staticY + SPRITE_OFFSET) + SPRITE_SIZE + 2;

                // Determine Color
                Color priceColor = _global.Palette_Yellow;

                if (isActivated)
                {
                    if (!canAfford)
                    {
                        priceColor = _global.Palette_Red;
                    }
                    else
                    {
                        priceColor = _global.Palette_BrightWhite;
                    }
                }

                // Draw Number (Secondary Font)
                spriteBatch.DrawStringSnapped(_priceFont, priceNum, new Vector2(startX, drawY), priceColor);

                // Draw "G" (Tertiary Font) - Align baseline roughly
                float symY = drawY + (numSize.Y - symSize.Y);
                spriteBatch.DrawStringSnapped(_currencyFont, currencySymbol, new Vector2(startX + numSize.X + spaceWidth, symY), priceColor);
            }
        }
    }
}
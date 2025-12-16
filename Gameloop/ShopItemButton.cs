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

        // Too Expensive Animation State
        private float _xOverlayTimer = 0f;
        private const float X_OVERLAY_DURATION = 0.25f;

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
            UseScreenCoordinates = true;
            EnableHoverSway = false; // Disable base sway, we handle custom lift
        }

        public void TriggerTooExpensiveAnimation()
        {
            _xOverlayTimer = X_OVERLAY_DURATION;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var gameState = ServiceLocator.Get<GameState>();
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

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

            if (_xOverlayTimer > 0)
            {
                _xOverlayTimer -= dt;
            }

            // --- 2. Calculate Offsets ---

            // A. Hover Lift (Instant 1px up)
            float liftOffset = isActivated ? -HOVER_LIFT_AMOUNT : 0f;

            // B. Idle Bob (1 pixel up/down) - Only active when NOT hovered
            float idleBob = 0f;
            if (!isActivated)
            {
                // Use TotalGameTime for continuous loop. Speed 5f gives a nice gentle bob.
                idleBob = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 5f) > 0 ? -1f : 0f;
            }

            // C. Shake (from base class)
            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);

            // --- 3. Calculate Positions ---

            float totalX = Bounds.X + (horizontalOffset ?? 0f) + shakeOffset.X;

            // Static Y: The base position (includes shake/layout offset, but NO bob/lift)
            // Used for Price and Sold text so they don't move.
            float staticY = Bounds.Y + (verticalOffset ?? 0f) + shakeOffset.Y;

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
                    bool canAfford = gameState.PlayerState.Coin >= Item.Price;

                    // 1. Draw Two-Tone Silhouette Outline (Always)
                    if (_iconSilhouette != null)
                    {
                        Color mainOutlineColor;
                        Color cornerOutlineColor;

                        if (isActivated)
                        {
                            if (!canAfford)
                            {
                                // Expensive Hover: Red Outline
                                mainOutlineColor = _global.Palette_Red;
                                cornerOutlineColor = _global.Palette_Red;
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
                bool canAfford = gameState.PlayerState.Coin >= Item.Price;

                if (isActivated && !canAfford)
                {
                    priceColor = _global.Palette_Red;
                }

                // Draw Number (Secondary Font)
                spriteBatch.DrawStringSnapped(_priceFont, priceNum, new Vector2(startX, drawY), priceColor);

                // Draw "G" (Tertiary Font) - Align baseline roughly
                float symY = drawY + (numSize.Y - symSize.Y);
                spriteBatch.DrawStringSnapped(_currencyFont, currencySymbol, new Vector2(startX + numSize.X + spaceWidth, symY), priceColor);
            }

            // --- Draw X Overlay (Too Expensive Animation) ---
            if (_xOverlayTimer > 0 && spriteManager.ShopXIcon != null)
            {
                float alpha = _xOverlayTimer / X_OVERLAY_DURATION;
                // Draw centered on the button (using staticY to avoid bobbing)
                Vector2 xPos = new Vector2(totalX, staticY);
                spriteBatch.DrawSnapped(spriteManager.ShopXIcon, xPos, Color.White * alpha);
            }
        }
    }
}
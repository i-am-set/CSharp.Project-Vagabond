using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class InventorySlot : Button
    {
        public string? ItemId { get; private set; }
        public int Quantity { get; private set; }
        public string? IconPath { get; private set; }
        public string? FallbackIconPath { get; private set; } // New property
        public Color? IconTint { get; private set; }
        public int Rarity { get; private set; } = -1; // -1 means no rarity icon
        public bool IsSelected { get; set; }
        public bool HasItem => !string.IsNullOrEmpty(ItemId);
        public bool IsAnimated { get; private set; } = false;
        public bool IsEquipped { get; private set; }

        private Rectangle _currentIdleFrame;
        private readonly Rectangle[] _idleFrames;
        private float _frameChangeTimer;
        private float _nextFrameChangeTime;
        private static readonly Random _random = new Random();

        // Animation State
        private bool _isPoppingIn;
        private float _popDelay;
        private float _popTimer;
        private const float POP_DURATION = 0.2f; // Snappier duration
        private float _visualScale = 1f;

        // Tuning
        private const float MIN_FRAME_CHANGE_SECONDS = 2.0f;
        private const float MAX_FRAME_CHANGE_SECONDS = 8.0f;

        public InventorySlot(Rectangle bounds, Rectangle[] idleFrames) : base(bounds, "")
        {
            _idleFrames = idleFrames;
            RandomizeFrame();
        }

        public void AssignItem(string itemId, int quantity, string? iconPath, int rarity, Color? iconTint = null, bool isAnimated = false, string? fallbackIconPath = null, bool isEquipped = false)
        {
            ItemId = itemId;
            Quantity = quantity;
            IconPath = iconPath;
            FallbackIconPath = fallbackIconPath;
            Rarity = rarity;
            IconTint = iconTint;
            IsAnimated = isAnimated;
            IsEquipped = isEquipped;
        }

        public void Clear()
        {
            ItemId = null;
            Quantity = 0;
            IconPath = null;
            FallbackIconPath = null;
            Rarity = -1;
            IconTint = null;
            IsSelected = false;
            IsAnimated = false;
            IsEquipped = false;
            // Reset visual scale so empty slots don't get stuck in an invisible state if animation was interrupted
            _visualScale = 1f;
            _isPoppingIn = false;
        }

        public void TriggerPopInAnimation(float delay)
        {
            _isPoppingIn = true;
            _popDelay = delay;
            _popTimer = 0f;
            _visualScale = 0f;
        }

        public void Update(GameTime gameTime, MouseState mouseState, Matrix? transform = null)
        {
            // Pass the transform to the base Button.Update so hit detection works in camera space
            base.Update(mouseState, transform);

            // Idle animation logic
            _frameChangeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_frameChangeTimer >= _nextFrameChangeTime)
            {
                RandomizeFrame();
            }

            // Pop-in animation logic
            if (_isPoppingIn)
            {
                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_popDelay > 0)
                {
                    _popDelay -= dt;
                }
                else
                {
                    _popTimer += dt;
                    float progress = Math.Clamp(_popTimer / POP_DURATION, 0f, 1f);
                    _visualScale = Easing.EaseOutBack(progress);

                    if (progress >= 1f)
                    {
                        _isPoppingIn = false;
                        _visualScale = 1f;
                    }
                }
            }
        }

        public void RandomizeFrame()
        {
            if (_idleFrames != null && _idleFrames.Length > 0)
            {
                _currentIdleFrame = _idleFrames[_random.Next(_idleFrames.Length)];
            }
            _frameChangeTimer = 0f;
            _nextFrameChangeTime = (float)(_random.NextDouble() * (MAX_FRAME_CHANGE_SECONDS - MIN_FRAME_CHANGE_SECONDS) + MIN_FRAME_CHANGE_SECONDS);
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var global = ServiceLocator.Get<Global>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            // Calculate center and origin for scaling
            Vector2 center = new Vector2(Bounds.Center.X, Bounds.Center.Y);
            Vector2 origin = new Vector2(Bounds.Width / 2f, Bounds.Height / 2f);

            // 1. Always draw the idle animation frame as the base (Static scale 1f)
            spriteBatch.DrawSnapped(spriteManager.InventorySlotIdleSpriteSheet, center, _currentIdleFrame, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);

            // 2. Draw overlay if selected or hovered (Static scale 1f)
            if (IsSelected || IsPressed)
            {
                spriteBatch.DrawSnapped(spriteManager.InventorySlotSelectedSprite, center, null, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);
            }
            else if (IsHovered || forceHover)
            {
                spriteBatch.DrawSnapped(spriteManager.InventorySlotHoverSprite, center, null, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);
            }

            // Draw Item Content (Animated scale _visualScale)
            if (HasItem && _visualScale > 0.01f)
            {
                if (!string.IsNullOrEmpty(IconPath))
                {
                    var icon = spriteManager.GetItemSprite(IconPath, FallbackIconPath);
                    Color tint = IconTint ?? Color.White;

                    if (icon != null)
                    {
                        Rectangle? sourceRect = null;
                        Vector2 iconOrigin;

                        if (IsAnimated)
                        {
                            sourceRect = spriteManager.GetAnimatedIconSourceRect(icon, gameTime);
                            iconOrigin = new Vector2(sourceRect.Value.Width / 2f, sourceRect.Value.Height / 2f);
                        }
                        else
                        {
                            iconOrigin = new Vector2(icon.Width / 2f, icon.Height / 2f);
                        }

                        // Draw Outline
                        var silhouette = spriteManager.GetItemSpriteSilhouette(IconPath, FallbackIconPath);
                        if (silhouette != null)
                        {
                            // Use global outline colors
                            Color mainOutlineColor = IsSelected ? global.ItemOutlineColor_Selected : (IsHovered ? global.ItemOutlineColor_Hover : global.ItemOutlineColor_Idle);
                            Color cornerOutlineColor = IsSelected ? global.ItemOutlineColor_Selected_Corner : (IsHovered ? global.ItemOutlineColor_Hover_Corner : global.ItemOutlineColor_Idle_Corner);

                            // 1. Draw Diagonals (Corners) FIRST (Behind)
                            spriteBatch.DrawSnapped(silhouette, center + new Vector2(-1, -1) * _visualScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, _visualScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, center + new Vector2(1, -1) * _visualScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, _visualScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, center + new Vector2(-1, 1) * _visualScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, _visualScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, center + new Vector2(1, 1) * _visualScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, _visualScale, SpriteEffects.None, 0f);

                            // 2. Draw Cardinals (Main) SECOND (On Top)
                            spriteBatch.DrawSnapped(silhouette, center + new Vector2(-1, 0) * _visualScale, sourceRect, mainOutlineColor, 0f, iconOrigin, _visualScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, center + new Vector2(1, 0) * _visualScale, sourceRect, mainOutlineColor, 0f, iconOrigin, _visualScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, center + new Vector2(0, -1) * _visualScale, sourceRect, mainOutlineColor, 0f, iconOrigin, _visualScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, center + new Vector2(0, 1) * _visualScale, sourceRect, mainOutlineColor, 0f, iconOrigin, _visualScale, SpriteEffects.None, 0f);
                        }

                        // Draw Icon
                        spriteBatch.DrawSnapped(icon, center, sourceRect, tint, 0f, iconOrigin, _visualScale, SpriteEffects.None, 0f);

                        // Draw Equipped Icon
                        if (IsEquipped && spriteManager.InventorySlotEquipIconSprite != null)
                        {
                            var equipRect = spriteManager.GetEquipIconSourceRect(gameTime);
                            // Draw at top-left of the slot (Bounds.X, Bounds.Y).
                            // Since we are using center for other things, we can just use Bounds.Location.
                            // We scale it with _visualScale to match the pop-in.
                            // The icon is 32x32, slot is 48x48.
                            // If we draw at Bounds.X, Bounds.Y, it aligns to top-left.
                            // To scale from center of the icon (16,16), we need to adjust position.
                            // Center of icon relative to slot top-left is (16, 16).
                            // Target pos = Bounds.Location + (16, 16).
                            Vector2 equipCenter = new Vector2(Bounds.X + 16, Bounds.Y + 16);
                            Vector2 equipOrigin = new Vector2(16, 16);
                            spriteBatch.DrawSnapped(spriteManager.InventorySlotEquipIconSprite, equipCenter, equipRect, Color.White, 0f, equipOrigin, _visualScale, SpriteEffects.None, 0f);
                        }

                        // Draw Rarity Icon
                        if (Rarity >= 0 && spriteManager.RarityIconsSpriteSheet != null)
                        {
                            var rarityRect = spriteManager.GetRarityIconSourceRect(Rarity, gameTime);
                            // Position at top-right of the item sprite.
                            // Item sprite is centered. Top-right relative to center is (Width/2, -Height/2).
                            // We want the rarity icon's center to be slightly inside the corner.
                            // Let's align the top-right of the rarity icon with the top-right of the item.
                            // Rarity icon is 8x8. Origin is (4,4).
                            // Item Top-Right is (W/2, -H/2).
                            // Rarity Pos = Center + (W/2 - 4, -H/2 + 4) * Scale.
                            float width = IsAnimated ? sourceRect.Value.Width : icon.Width;
                            float height = IsAnimated ? sourceRect.Value.Height : icon.Height;

                            Vector2 rarityOffset = new Vector2(width / 2f - 4, -height / 2f + 4) * _visualScale;
                            Vector2 rarityPos = center + rarityOffset;
                            Vector2 rarityOrigin = new Vector2(4, 4);

                            spriteBatch.DrawSnapped(spriteManager.RarityIconsSpriteSheet, rarityPos, rarityRect, Color.White, 0f, rarityOrigin, _visualScale, SpriteEffects.None, 0f);
                        }
                    }
                }
                else
                {
                    // Fallback Text
                    string displayName = ItemId ?? "???";
                    if (displayName.Length > 8) displayName = displayName.Substring(0, 6) + "..";
                    var textSize = secondaryFont.MeasureString(displayName);
                    Vector2 textOrigin = textSize / 2f;
                    spriteBatch.DrawStringSnapped(secondaryFont, displayName, center, global.Palette_BrightWhite, 0f, textOrigin, _visualScale, SpriteEffects.None, 0f);
                }

                // Draw Quantity
                if (Quantity > 1)
                {
                    string qty = $"x{Quantity}";
                    var qtySize = secondaryFont.MeasureString(qty);
                    // Position quantity at bottom right relative to center
                    Vector2 qtyOffset = new Vector2(Bounds.Width / 2f - qtySize.Width - 4, Bounds.Height / 2f - qtySize.Height - 4);
                    // Scale the offset so it stays relative to the shrinking box
                    Vector2 qtyPos = center + qtyOffset * _visualScale;
                    // Scale the text itself
                    spriteBatch.DrawStringSnapped(secondaryFont, qty, qtyPos, Color.White, 0f, Vector2.Zero, _visualScale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}

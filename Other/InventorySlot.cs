using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Items;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class InventorySlot : Button
    {
        public string? ItemId { get; private set; }
        public int Quantity { get; private set; }
        public string? IconPath { get; private set; }
        public string? FallbackIconPath { get; private set; }
        public Color? IconTint { get; private set; }
        public bool IsSelected { get; set; }
        public bool HasItem => !string.IsNullOrEmpty(ItemId);
        public bool IsAnimated { get; private set; } = false;
        public bool IsEquipped { get; private set; }

        // Animation State
        private bool _isPoppingIn;
        private float _popDelay;
        private float _popTimer;
        private const float POP_DURATION = 0.2f; // Snappier duration
        private float _visualScale = 1f;

        public InventorySlot(Rectangle bounds) : base(bounds, "")
        {
        }

        public void AssignItem(string itemId, int quantity, string? iconPath, Color? iconTint = null, bool isAnimated = false, string? fallbackIconPath = null, bool isEquipped = false)
        {
            ItemId = itemId;
            Quantity = quantity;
            IconPath = iconPath;
            FallbackIconPath = fallbackIconPath;
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
            // No-op
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var global = ServiceLocator.Get<Global>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            // Calculate center and origin for scaling
            Vector2 center = new Vector2(Bounds.Center.X, Bounds.Center.Y);
            Vector2 origin = new Vector2(Bounds.Width / 2f, Bounds.Height / 2f);

            // 1. Draw overlay if selected or hovered (Static scale 1f)
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
                            // Draw centered on the slot.
                            // The equip icon is 32x32. The slot is 24x24.
                            // User requested full size (32x32).
                            Vector2 equipOrigin = new Vector2(16, 16); // Center of 32x32 sprite
                            float equipScale = 1.0f * _visualScale; // Full size + animation scale

                            spriteBatch.DrawSnapped(spriteManager.InventorySlotEquipIconSprite, center, equipRect, Color.White, 0f, equipOrigin, equipScale, SpriteEffects.None, 0f);
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
                    spriteBatch.DrawStringSnapped(secondaryFont, displayName, center, global.Palette_Sun, 0f, textOrigin, _visualScale, SpriteEffects.None, 0f);
                }

                // Draw Quantity
                if (Quantity > 1)
                {
                    string qty = $"x{Quantity}";
                    var qtySize = secondaryFont.MeasureString(qty);
                    // Position quantity at bottom right relative to center
                    Vector2 qtyOffset = new Vector2(Bounds.Width / 2f - qtySize.Width - 2, Bounds.Height / 2f - qtySize.Height - 2);
                    // Scale the offset so it stays relative to the shrinking box
                    Vector2 qtyPos = center + qtyOffset * _visualScale;
                    // Scale the text itself
                    spriteBatch.DrawStringSnapped(secondaryFont, qty, qtyPos, Color.White, 0f, Vector2.Zero, _visualScale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}

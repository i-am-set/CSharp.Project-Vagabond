#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class InventorySlot : Button
    {
        public string? ItemId { get; private set; }
        public int Quantity { get; private set; }
        public string? IconPath { get; private set; }
        public Color? IconTint { get; private set; }
        public bool IsSelected { get; set; }
        public bool HasItem => !string.IsNullOrEmpty(ItemId);

        private Rectangle _currentIdleFrame;
        private readonly Rectangle[] _idleFrames;
        private float _frameChangeTimer;
        private float _nextFrameChangeTime;
        private static readonly Random _random = new Random();

        // Tuning
        private const float MIN_FRAME_CHANGE_SECONDS = 2.0f;
        private const float MAX_FRAME_CHANGE_SECONDS = 8.0f;

        public InventorySlot(Rectangle bounds, Rectangle[] idleFrames) : base(bounds, "")
        {
            _idleFrames = idleFrames;
            RandomizeFrame();
        }

        public void AssignItem(string itemId, int quantity, string? iconPath, Color? iconTint = null)
        {
            ItemId = itemId;
            Quantity = quantity;
            IconPath = iconPath;
            IconTint = iconTint;
        }

        public void Clear()
        {
            ItemId = null;
            Quantity = 0;
            IconPath = null;
            IconTint = null;
            IsSelected = false;
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

            Vector2 position = new Vector2(Bounds.X, Bounds.Y);

            // 1. Always draw the idle animation frame as the base
            spriteBatch.DrawSnapped(spriteManager.InventorySlotIdleSpriteSheet, position, _currentIdleFrame, Color.White);

            // 2. Draw overlay if selected or hovered
            if (IsSelected || IsPressed)
            {
                spriteBatch.DrawSnapped(spriteManager.InventorySlotSelectedSprite, position, null, Color.White);
            }
            else if (IsHovered || forceHover)
            {
                spriteBatch.DrawSnapped(spriteManager.InventorySlotHoverSprite, position, null, Color.White);
            }

            // Draw Item Content
            if (HasItem)
            {
                if (!string.IsNullOrEmpty(IconPath))
                {
                    var icon = spriteManager.GetItemSprite(IconPath);
                    Color tint = IconTint ?? Color.White;

                    if (icon != null)
                    {
                        // Draw Outline
                        var silhouette = spriteManager.GetItemSpriteSilhouette(IconPath);
                        if (silhouette != null)
                        {
                            // Use global outline colors
                            Color outlineColor = IsSelected ? global.ItemOutlineColor_Selected : (IsHovered ? global.ItemOutlineColor_Hover : global.ItemOutlineColor_Idle);

                            Vector2 iconOrigin = new Vector2(icon.Width / 2f, icon.Height / 2f);
                            Vector2 centerPos = position + new Vector2(Bounds.Width / 2f, Bounds.Height / 2f);

                            // Cardinal directions
                            spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(-1, 0), null, outlineColor, 0f, iconOrigin, 1f, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(1, 0), null, outlineColor, 0f, iconOrigin, 1f, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(0, -1), null, outlineColor, 0f, iconOrigin, 1f, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(0, 1), null, outlineColor, 0f, iconOrigin, 1f, SpriteEffects.None, 0f);

                            // Diagonals for full cornered outline
                            spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(-1, -1), null, outlineColor, 0f, iconOrigin, 1f, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(1, -1), null, outlineColor, 0f, iconOrigin, 1f, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(-1, 1), null, outlineColor, 0f, iconOrigin, 1f, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(1, 1), null, outlineColor, 0f, iconOrigin, 1f, SpriteEffects.None, 0f);
                        }

                        // Draw Icon
                        Vector2 iconCenterPos = position + new Vector2(Bounds.Width / 2f, Bounds.Height / 2f);
                        spriteBatch.DrawSnapped(icon, iconCenterPos, null, tint, 0f, new Vector2(icon.Width / 2f, icon.Height / 2f), 1f, SpriteEffects.None, 0f);
                    }
                }
                else
                {
                    // Fallback Text
                    string displayName = ItemId ?? "???";
                    if (displayName.Length > 8) displayName = displayName.Substring(0, 6) + "..";
                    var textSize = secondaryFont.MeasureString(displayName);
                    Vector2 textPos = position + new Vector2(Bounds.Width / 2f - textSize.Width / 2f, Bounds.Height / 2f - textSize.Height / 2f);
                    spriteBatch.DrawStringSnapped(secondaryFont, displayName, textPos, global.Palette_BrightWhite);
                }

                // Draw Quantity
                if (Quantity > 1)
                {
                    string qty = $"x{Quantity}";
                    var qtySize = secondaryFont.MeasureString(qty);
                    var qtyPos = position + new Vector2(Bounds.Width / 2f - qtySize.Width - 4, Bounds.Height / 2f - qtySize.Height - 4);
                    spriteBatch.DrawStringSnapped(secondaryFont, qty, qtyPos, Color.White);
                }
            }
        }
    }
}

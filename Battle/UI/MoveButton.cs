// --- MoveButton.cs ---
// Updated Draw() to change background color to Palette_Fruit when pressed.
// Updated Draw() to force text color to Black when pressed for readability.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle.UI
{
    public class MoveButton : Button
    {
        public MoveData? Move { get; }
        public MoveEntry? Entry { get; }
        public BattleCombatant Owner { get; }

        public bool CanAfford => true;

        public Color BackgroundColor { get; set; } = Color.Transparent;
        public bool DrawSystemBackground { get; set; } = false;

        public int? VisualWidthOverride { get; set; }

        public Rectangle? ActionIconRect { get; set; }
        public Color ActionIconColor { get; set; } = Color.White;
        public Color ActionIconHoverColor { get; set; } = Color.White;

        public Vector2 IconRenderOffset { get; set; } = Vector2.Zero;

        // Opacity for fade-in animations
        public float Opacity { get; set; } = 1.0f;

        private bool _showManaWarning = false;
        public int? VisualHeightOverride { get; set; }

        private bool _isScrollingInitialized = false;
        private float _scrollPosition = 0f;
        private float _scrollWaitTimer = 0f;
        private float _loopWidth = 0f;
        private enum ScrollState { PausedAtStart, Scrolling, PausedAtLoopPoint }
        private ScrollState _scrollState = ScrollState.PausedAtStart;

        private const float SCROLL_SPEED = 25f;
        private const float SCROLL_PAUSE_DURATION = 1.5f;
        private const int SCROLL_GAP_SPACES = 3;
        private static readonly RasterizerState _clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

        public MoveButton(BattleCombatant owner, MoveData? move, MoveEntry? entry, BitmapFont font)
            : base(Rectangle.Empty, "---", font: font)
        {
            Owner = owner;
            Move = move;
            Entry = entry;

            EnableTextWave = false;
            HoverAnimation = HoverAnimationType.None;

            if (move == null) IsEnabled = false;
        }

        public override void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            base.Update(currentMouseState, worldTransform);

            if (!CanAfford && IsEnabled)
            {
                if (IsHovered)
                {
                    _showManaWarning = true;
                    IsHovered = false;
                    _isPressed = false;
                }
                else
                {
                    _showManaWarning = false;
                }
            }
            else
            {
                _showManaWarning = false;
            }
        }

        private void UpdateScrolling(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            switch (_scrollState)
            {
                case ScrollState.PausedAtStart:
                    _scrollWaitTimer -= dt;
                    if (_scrollWaitTimer <= 0) _scrollState = ScrollState.Scrolling;
                    break;

                case ScrollState.Scrolling:
                    _scrollPosition += SCROLL_SPEED * dt;
                    if (_scrollPosition >= _loopWidth)
                    {
                        _scrollPosition = _loopWidth;
                        _scrollState = ScrollState.PausedAtLoopPoint;
                        _scrollWaitTimer = 1.0f;
                    }
                    break;

                case ScrollState.PausedAtLoopPoint:
                    _scrollWaitTimer -= dt;
                    if (_scrollWaitTimer <= 0)
                    {
                        _scrollPosition -= _loopWidth;
                        _scrollState = ScrollState.Scrolling;
                    }
                    break;
            }
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            bool canAfford = CanAfford;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // 1. Calculate Animation Offsets
            float hoverOffset = 0f;
            if (IsPressed)
            {
                // If held down, force offset to 0 to show "pushed down" state
                hoverOffset = 0f;
            }
            else
            {
                hoverOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated && canAfford, HoverLiftOffset, HoverLiftDuration);
            }

            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);

            // 2. Calculate Strict Integer Position & Bounds
            // We round everything immediately to lock it to the pixel grid
            int x = (int)MathF.Round(Bounds.X + shakeOffset.X + (horizontalOffset ?? 0f));
            int y = (int)MathF.Round(Bounds.Y + shakeOffset.Y + hoverOffset + (verticalOffset ?? 0f));
            int w = VisualWidthOverride ?? Bounds.Width;
            int h = VisualHeightOverride ?? Bounds.Height;

            Rectangle drawBounds = new Rectangle(x, y, w, h);
            Vector2 boundsCenter = new Vector2(drawBounds.Center.X, drawBounds.Center.Y);

            bool isStackedLayout = h > 15 && ActionIconRect.HasValue;

            // 3. Draw Background (Pixel Perfect)
            if (DrawSystemBackground)
            {
                Color bgColor = BackgroundColor;
                if (!IsEnabled || !canAfford) bgColor = _global.Palette_Black;
                else if (IsPressed) bgColor = _global.Palette_Fruit; // Selected/Held Color
                else if (isActivated) bgColor = _global.ButtonHoverColor;

                DrawPixelPerfectBevel(spriteBatch, pixel, drawBounds, bgColor * Opacity);
            }

            // 4. Draw Icon (Pixel Perfect)
            if (ActionIconRect.HasValue)
            {
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                if (spriteManager.ActionIconsSpriteSheet != null)
                {
                    // Calculate relative offset based on layout
                    Vector2 iconOffset;
                    if (isStackedLayout)
                    {
                        iconOffset = new Vector2(IconRenderOffset.X, -5f + IconRenderOffset.Y);
                    }
                    else
                    {
                        iconOffset = new Vector2((-w / 2f) + 5f, 0);
                    }

                    // Combine, then ROUND to snap
                    Vector2 rawIconPos = boundsCenter + iconOffset;
                    Vector2 snappedIconPos = new Vector2(MathF.Round(rawIconPos.X), MathF.Round(rawIconPos.Y));

                    // Use integer origin to ensure pixel-perfect alignment on the grid
                    Vector2 iconOrigin = new Vector2(4, 4);
                    Color currentIconColor = (isActivated && canAfford) ? ActionIconHoverColor : ActionIconColor;

                    // No rotation, scale 1.0f (or derived from _currentScale if absolutely needed, but usually 1 for pixel perfect)
                    // Assuming scale remains 1 for pixel perfection unless zooming is critical
                    float scale = 1.0f;

                    spriteBatch.DrawSnapped(
                        spriteManager.ActionIconsSpriteSheet,
                        snappedIconPos,
                        ActionIconRect.Value,
                        currentIconColor * Opacity,
                        0f, // No rotation
                        iconOrigin,
                        new Vector2(scale),
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            // 5. Draw Text (Pixel Perfect)
            Color textColor;
            if (!IsEnabled || !canAfford) textColor = CustomDisabledTextColor ?? _global.ButtonDisableColor;
            else if (IsPressed) textColor = _global.Palette_Black; // Force black on fruit background
            else if (isActivated) textColor = CustomHoverTextColor ?? _global.ButtonHoverColor;
            else textColor = CustomDefaultTextColor ?? _global.GameTextColor;

            if (tintColorOverride.HasValue) textColor = tintColorOverride.Value;

            // Apply Opacity to text
            textColor = textColor * Opacity;

            BitmapFont font = Font ?? defaultFont;
            Vector2 textSize = font.MeasureString(Text);
            float textAvailableWidth = w - 8;
            bool needsScrolling = textSize.X > textAvailableWidth;

            // Handle Text Positioning
            if (needsScrolling)
            {
                // Init scrolling state if needed
                if (!_isScrollingInitialized)
                {
                    _isScrollingInitialized = true;
                    float gapWidth = font.MeasureString(new string(' ', SCROLL_GAP_SPACES)).Width;
                    _loopWidth = textSize.X + gapWidth;
                    _scrollWaitTimer = SCROLL_PAUSE_DURATION;
                    _scrollState = ScrollState.PausedAtStart;
                    _scrollPosition = 0;
                }
                UpdateScrolling(gameTime);

                // Scissor Logic must align with integer bounds
                // Calculate flat coordinates relative to the button
                int textY = (int)MathF.Round(boundsCenter.Y - (font.LineHeight / 2f) + TextRenderOffset.Y);
                if (isStackedLayout) textY += 5;

                // Flatten X start
                int flatX = (int)MathF.Round(boundsCenter.X - (w / 2f) + 4);

                var originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
                var originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
                spriteBatch.End();

                // Start Scissor Batch
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, _clipRasterizerState, null, transform);

                // Set Scissor Rect (must be integers)
                var clipRect = new Rectangle(flatX, drawBounds.Y, (int)textAvailableWidth, drawBounds.Height);
                spriteBatch.GraphicsDevice.ScissorRectangle = clipRect;

                Vector2 scrollingPos = new Vector2(flatX - _scrollPosition, textY);
                // Snap the scrolling position's Y, X is float for smooth scroll, but could be snapped if strictly required
                // For "strict pixel grid", usually the base position is snapped.
                spriteBatch.DrawStringSnapped(font, Text, scrollingPos, textColor);
                spriteBatch.DrawStringSnapped(font, Text, scrollingPos + new Vector2(_loopWidth, 0), textColor);

                spriteBatch.End();
                spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, originalRasterizerState, null, transform);
            }
            else
            {
                _isScrollingInitialized = false;

                // Standard centered text
                Vector2 textOrigin = new Vector2(MathF.Round(textSize.X / 2f), MathF.Round(textSize.Y / 2f) + 1);
                textOrigin -= TextRenderOffset;

                Vector2 textOffset = isStackedLayout ? new Vector2(0, 5f) : Vector2.Zero;

                // Calculate raw, then SNAP
                Vector2 rawTextPos = boundsCenter + textOffset;
                Vector2 snappedTextPos = new Vector2(MathF.Round(rawTextPos.X), MathF.Round(rawTextPos.Y));

                spriteBatch.DrawStringSnapped(font, Text, snappedTextPos, textColor, 0f, textOrigin, 1.0f, SpriteEffects.None, 0f);
            }

            // 6. Draw Disabled Strikethrough (Flat, no rotation)
            if (!IsEnabled || !canAfford)
            {
                // Calculate center
                Vector2 lineCenterOffset = isStackedLayout ? new Vector2(0, 5) : Vector2.Zero;
                Vector2 rawLineCenter = boundsCenter + lineCenterOffset;
                Vector2 snappedLineCenter = new Vector2(MathF.Round(rawLineCenter.X), MathF.Round(rawLineCenter.Y));

                int halfTextW = (int)(textSize.X / 2f) + 2;

                // Draw a simple 1px rectangle
                Rectangle lineRect = new Rectangle(
                    (int)(snappedLineCenter.X - halfTextW),
                    (int)snappedLineCenter.Y,
                    halfTextW * 2,
                    1
                );

                spriteBatch.Draw(ServiceLocator.Get<Texture2D>(), lineRect, _global.ButtonDisableColor * Opacity);
            }

            if (_showManaWarning && IsEnabled)
            {
                string noManaText = "NOT ENOUGH MANA";
                Vector2 noManaSize = font.MeasureString(noManaText);
                Vector2 noManaPos = new Vector2(
                    MathF.Round(drawBounds.X + drawBounds.Width / 2f - noManaSize.X / 2f),
                    MathF.Round(drawBounds.Y + drawBounds.Height / 2f - noManaSize.Y / 2f - 2)
                );
                TextAnimator.DrawTextWithEffectSquareOutlined(spriteBatch, font, noManaText, noManaPos, _global.Palette_Rust * Opacity, Color.Black * Opacity, TextEffectType.None, 0f);
            }
        }

        private void DrawPixelPerfectBevel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, Color color)
        {
            // Top line (indent corners)
            spriteBatch.Draw(pixel, new Rectangle(bounds.X + 1, bounds.Y, bounds.Width - 2, 1), color);
            // Middle block
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y + 1, bounds.Width, bounds.Height - 2), color);
            // Bottom line (indent corners)
            spriteBatch.Draw(pixel, new Rectangle(bounds.X + 1, bounds.Bottom - 1, bounds.Width - 2, 1), color);
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public class MoveButton : Button
    {
        public MoveData? Move { get; }
        public MoveEntry? Entry { get; }
        public BattleCombatant Owner { get; }

        public bool CanAfford
        {
            get
            {
                if (Owner == null || Move == null) return false;
                var manaDump = Move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
                if (manaDump != null) return Owner.Stats.CurrentMana > 0;
                return Owner.Stats.CurrentMana >= Move.ManaCost;
            }
        }

        // --- Background Drawing Properties ---
        public Color BackgroundColor { get; set; } = Color.Transparent;
        public bool DrawSystemBackground { get; set; } = false;

        /// <summary>
        /// If set, the background will be drawn with this width instead of the button's bounds width.
        /// Useful for creating gaps between visuals while maintaining larger hitboxes.
        /// </summary>
        public int? VisualWidthOverride { get; set; }

        private bool _showManaWarning = false;

        // Scrolling state
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

            // Disable wave/drift to ensure text is "welded" to the background during rotation
            EnableTextWave = false;
            HoverAnimation = HoverAnimationType.None;

            // Default to disabled if no move
            if (move == null) IsEnabled = false;
        }

        public override void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            base.Update(currentMouseState, worldTransform);

            // If we can't afford the move, suppress the hover state so the parent menu
            // doesn't trigger targeting previews, but keep a local flag to draw the warning.
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

            // Update animations
            _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated && canAfford);
            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);

            // Dampen the rotation intensity to match the "snappy" feel of larger Main Menu buttons.
            // Small buttons rotate too aggressively with the default width-based scaling.
            // We apply a dampening factor to normalize the visual intensity.
            _currentHoverRotation *= 0.35f;

            // Suppress rotation if cannot afford
            if (!canAfford) _currentHoverRotation = 0f;

            // Calculate unified transform
            float finalScaleX = _currentScale;
            float finalScaleY = _currentScale;
            Vector2 scaleVec = new Vector2(finalScaleX, finalScaleY);

            // Calculate Center Position with Shake
            Vector2 centerPos = new Vector2(Bounds.Center.X, Bounds.Center.Y) + shakeOffset;
            if (horizontalOffset.HasValue) centerPos.X += horizontalOffset.Value;
            if (verticalOffset.HasValue) centerPos.Y += verticalOffset.Value;

            // Adjust for Visual Width Override to ensure pixel-perfect alignment
            float effectiveWidth = VisualWidthOverride ?? Bounds.Width;

            // Pixel Snap Center if rotation is negligible
            if (Math.Abs(_currentHoverRotation) < 0.01f)
            {
                // Calculate target center based on effective width to ensure integer alignment of edges
                float halfWidth = effectiveWidth / 2f;
                float targetLeftX = MathF.Floor(centerPos.X - halfWidth);
                float targetCenterX = targetLeftX + halfWidth;

                float targetCenterY = MathF.Floor(centerPos.Y) + (Bounds.Height % 2 == 0 ? 0.0f : 0.5f);
                centerPos = new Vector2(targetCenterX, targetCenterY);
            }

            // --- DRAW BACKGROUND ---
            if (DrawSystemBackground)
            {
                Color bgColor = BackgroundColor;
                if (!IsEnabled || !canAfford) bgColor = _global.Palette_Black;
                else if (isActivated) bgColor = _global.Palette_Rust;

                DrawRotatedBeveledBackground(spriteBatch, pixel, centerPos, (int)effectiveWidth, Bounds.Height, bgColor, _currentHoverRotation, scaleVec);
            }

            // --- DRAW TEXT ---
            Color textColor;
            if (!IsEnabled || !canAfford)
            {
                textColor = CustomDisabledTextColor ?? _global.ButtonDisableColor;
            }
            else
            {
                if (isActivated)
                    textColor = CustomHoverTextColor ?? _global.ButtonHoverColor;
                else
                    textColor = CustomDefaultTextColor ?? _global.GameTextColor;
            }

            if (tintColorOverride.HasValue) textColor = tintColorOverride.Value;

            BitmapFont font = Font ?? defaultFont;
            Vector2 textSize = font.MeasureString(Text);

            // Check for scrolling
            float textAvailableWidth = effectiveWidth - 8; // Margins based on visual width
            bool needsScrolling = textSize.X > textAvailableWidth;

            // Disable scrolling if rotating to prevent clipping artifacts
            if (needsScrolling && Math.Abs(_currentHoverRotation) < 0.01f)
            {
                // Scrolling Logic (Flat, no rotation support for clipping)
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

                // Calculate flat position
                float flatX = centerPos.X - (effectiveWidth / 2f) + 4; // Left padding
                float flatY = centerPos.Y - (font.LineHeight / 2f) + TextRenderOffset.Y;

                var originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
                var originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
                spriteBatch.End();

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, _clipRasterizerState, null, transform);

                var clipRect = new Rectangle((int)flatX, (int)(centerPos.Y - Bounds.Height / 2f), (int)textAvailableWidth, Bounds.Height);
                spriteBatch.GraphicsDevice.ScissorRectangle = clipRect;

                var scrollingTextPosition = new Vector2(flatX - _scrollPosition, flatY);

                spriteBatch.DrawStringSnapped(font, Text, scrollingTextPosition, textColor);
                spriteBatch.DrawStringSnapped(font, Text, scrollingTextPosition + new Vector2(_loopWidth, 0), textColor);

                spriteBatch.End();
                spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, originalRasterizerState, null, transform);
            }
            else
            {
                _isScrollingInitialized = false;

                // Calculate Origin (Center of Text)
                // Added +1 to Y to shift text up by 1 pixel relative to the center
                Vector2 textOrigin = new Vector2(MathF.Round(textSize.X / 2f), MathF.Round(textSize.Y / 2f) + 1);

                // Apply TextRenderOffset (Inverse because we are adjusting origin to move text)
                textOrigin -= TextRenderOffset;

                // Draw Text "Welded" to the background
                // Using the exact same centerPos, rotation, and scale ensures they move as one unit.
                spriteBatch.DrawStringSnapped(font, Text, centerPos, textColor, _currentHoverRotation, textOrigin, finalScaleX, SpriteEffects.None, 0f);
            }

            // --- STRIKETHROUGH ---
            if (!IsEnabled || !canAfford)
            {
                Vector2 lineStartLocal = new Vector2(-textSize.X / 2f - 2, 0);
                Vector2 lineEndLocal = new Vector2(textSize.X / 2f + 2, 0);

                // Rotate
                float c = MathF.Cos(_currentHoverRotation);
                float s = MathF.Sin(_currentHoverRotation);

                Vector2 RotateLocal(Vector2 v) => new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c) * scaleVec;

                Vector2 p1 = centerPos + RotateLocal(lineStartLocal);
                Vector2 p2 = centerPos + RotateLocal(lineEndLocal);

                spriteBatch.DrawLineSnapped(p1, p2, _global.ButtonDisableColor);
            }

            // --- MANA WARNING ---
            if (_showManaWarning && IsEnabled)
            {
                string noManaText = "NOT ENOUGH MANA";
                Vector2 noManaSize = font.MeasureString(noManaText);
                Vector2 noManaPos = new Vector2(
                    Bounds.Center.X - noManaSize.X / 2f,
                    Bounds.Center.Y - noManaSize.Y / 2f - 2
                );
                TextAnimator.DrawTextWithEffectSquareOutlined(spriteBatch, font, noManaText, noManaPos, _global.Palette_Rust, Color.Black, TextEffectType.None, 0f);
            }
        }

        private void DrawRotatedBeveledBackground(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, int width, int height, Color color, float rotation, Vector2 scale)
        {
            float w = width;
            float h = height;

            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);

            Vector2 Rotate(Vector2 v)
            {
                return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
            }

            // 1. Middle Body: (w, h-2)
            // Origin: (0.5, 0.5) -> Center of 1x1 pixel
            // Scale: (w, h-2) * scale
            Vector2 midScale = new Vector2(w, h - 2) * scale;
            spriteBatch.DrawSnapped(pixel, center, null, color, rotation, new Vector2(0.5f, 0.5f), midScale, SpriteEffects.None, 0f);

            // 2. Top Edge: (w-2, 1)
            // Offset Y: -(h-1)/2
            Vector2 topOffset = new Vector2(0, -(h - 1) / 2f) * scale.Y;
            Vector2 topPos = center + Rotate(topOffset);
            Vector2 topScale = new Vector2(w - 2, 1) * scale;
            spriteBatch.DrawSnapped(pixel, topPos, null, color, rotation, new Vector2(0.5f, 0.5f), topScale, SpriteEffects.None, 0f);

            // 3. Bottom Edge: (w-2, 1)
            // Offset Y: (h-1)/2
            Vector2 botOffset = new Vector2(0, (h - 1) / 2f) * scale.Y;
            Vector2 botPos = center + Rotate(botOffset);
            Vector2 botScale = new Vector2(w - 2, 1) * scale;
            spriteBatch.DrawSnapped(pixel, botPos, null, color, rotation, new Vector2(0.5f, 0.5f), botScale, SpriteEffects.None, 0f);
        }
    }
}
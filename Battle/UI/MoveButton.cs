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

            // FIX: Pass inherited properties
            _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated && canAfford, HoverLiftOffset, HoverLiftDuration);
            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);

            _currentHoverRotation *= 0.35f;

            if (!canAfford) _currentHoverRotation = 0f;

            float finalScaleX = _currentScale;
            float finalScaleY = _currentScale;
            Vector2 scaleVec = new Vector2(finalScaleX, finalScaleY);

            Vector2 centerPos = new Vector2(Bounds.X + Bounds.Width / 2f, Bounds.Y + Bounds.Height / 2f) + shakeOffset;

            if (horizontalOffset.HasValue) centerPos.X += horizontalOffset.Value;
            if (verticalOffset.HasValue) centerPos.Y += verticalOffset.Value;

            float effectiveWidth = VisualWidthOverride ?? Bounds.Width;
            float effectiveHeight = VisualHeightOverride ?? Bounds.Height;

            bool isStackedLayout = effectiveHeight > 15 && ActionIconRect.HasValue;

            if (DrawSystemBackground)
            {
                Color bgColor = BackgroundColor;
                if (!IsEnabled || !canAfford) bgColor = _global.Palette_Black;
                else if (isActivated) bgColor = _global.ButtonHoverColor;

                DrawRotatedBeveledBackground(spriteBatch, pixel, centerPos, (int)effectiveWidth, (int)effectiveHeight, bgColor, _currentHoverRotation, scaleVec);
            }

            if (ActionIconRect.HasValue)
            {
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                if (spriteManager.ActionIconsSpriteSheet != null)
                {
                    Vector2 rotatedOffset;

                    if (isStackedLayout)
                    {
                        float iconOffsetY = -5f + IconRenderOffset.Y;
                        float iconOffsetX = IconRenderOffset.X;

                        float c = MathF.Cos(_currentHoverRotation);
                        float s = MathF.Sin(_currentHoverRotation);

                        rotatedOffset = new Vector2(
                            iconOffsetX * c * scaleVec.X - iconOffsetY * s * scaleVec.Y,
                            iconOffsetX * s * scaleVec.X + iconOffsetY * c * scaleVec.Y
                        );
                    }
                    else
                    {
                        float iconOffsetX = (-effectiveWidth / 2f) + 5f;
                        float c = MathF.Cos(_currentHoverRotation);
                        float s = MathF.Sin(_currentHoverRotation);

                        rotatedOffset = new Vector2(
                            iconOffsetX * c * scaleVec.X,
                            iconOffsetX * s * scaleVec.Y
                        );
                    }

                    Vector2 iconPos = centerPos + rotatedOffset;
                    Vector2 iconOrigin = new Vector2(4.5f, 4.5f);

                    Color currentIconColor = ActionIconColor;
                    if (isActivated && canAfford)
                    {
                        currentIconColor = ActionIconHoverColor;
                    }

                    spriteBatch.DrawSnapped(
                        spriteManager.ActionIconsSpriteSheet,
                        iconPos,
                        ActionIconRect.Value,
                        currentIconColor,
                        _currentHoverRotation,
                        iconOrigin,
                        scaleVec,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

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

            float textAvailableWidth = effectiveWidth - 8;
            bool needsScrolling = textSize.X > textAvailableWidth;

            if (needsScrolling && Math.Abs(_currentHoverRotation) < 0.01f)
            {
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

                float flatX = centerPos.X - (effectiveWidth / 2f) + 4;
                float flatY = centerPos.Y - (font.LineHeight / 2f) + TextRenderOffset.Y;

                if (isStackedLayout) flatY += 5f;

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

                Vector2 textOrigin = new Vector2(MathF.Round(textSize.X / 2f), MathF.Round(textSize.Y / 2f) + 1);
                textOrigin -= TextRenderOffset;

                Vector2 textDrawPos = centerPos;
                if (isStackedLayout)
                {
                    float textOffsetY = 5f;
                    float c = MathF.Cos(_currentHoverRotation);
                    float s = MathF.Sin(_currentHoverRotation);

                    Vector2 rotatedTextOffset = new Vector2(
                        0 * c * scaleVec.X - textOffsetY * s * scaleVec.Y,
                        0 * s * scaleVec.X + textOffsetY * c * scaleVec.Y
                    );
                    textDrawPos += rotatedTextOffset;
                }

                spriteBatch.DrawStringSnapped(font, Text, textDrawPos, textColor, _currentHoverRotation, textOrigin, finalScaleX, SpriteEffects.None, 0f);
            }

            if (!IsEnabled || !canAfford)
            {
                Vector2 lineStartLocal = new Vector2(-textSize.X / 2f - 2, 0);
                Vector2 lineEndLocal = new Vector2(textSize.X / 2f + 2, 0);

                if (isStackedLayout)
                {
                    lineStartLocal.Y += 5f;
                    lineEndLocal.Y += 5f;
                }

                float c = MathF.Cos(_currentHoverRotation);
                float s = MathF.Sin(_currentHoverRotation);

                Vector2 RotateLocal(Vector2 v) => new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c) * scaleVec;

                Vector2 p1 = centerPos + RotateLocal(lineStartLocal);
                Vector2 p2 = centerPos + RotateLocal(lineEndLocal);

                spriteBatch.DrawLineSnapped(p1, p2, _global.ButtonDisableColor);
            }

            if (_showManaWarning && IsEnabled)
            {
                string noManaText = "NOT ENOUGH MANA";
                Vector2 noManaSize = font.MeasureString(noManaText);
                Vector2 noManaPos = new Vector2(
                    Bounds.X + Bounds.Width / 2f - noManaSize.X / 2f,
                    Bounds.Y + Bounds.Height / 2f - noManaSize.Y / 2f - 2
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

            Vector2 midScale = new Vector2(w, h - 2) * scale;
            spriteBatch.DrawSnapped(pixel, center, null, color, rotation, new Vector2(0.5f, 0.5f), midScale, SpriteEffects.None, 0f);

            Vector2 topOffset = new Vector2(0, -(h - 1) / 2f) * scale.Y;
            Vector2 topPos = center + Rotate(topOffset);
            Vector2 topScale = new Vector2(w - 2, 1) * scale;
            spriteBatch.DrawSnapped(pixel, topPos, null, color, rotation, new Vector2(0.5f, 0.5f), topScale, SpriteEffects.None, 0f);

            Vector2 botOffset = new Vector2(0, (h - 1) / 2f) * scale.Y;
            Vector2 botPos = center + Rotate(botOffset);
            Vector2 botScale = new Vector2(w - 2, 1) * scale;
            spriteBatch.DrawSnapped(pixel, botPos, null, color, rotation, new Vector2(0.5f, 0.5f), botScale, SpriteEffects.None, 0f);
        }
    }
}
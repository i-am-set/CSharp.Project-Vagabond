using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

namespace ProjectVagabond.Battle.UI
{
    public class MoveButton : Button
    {
        public MoveData Move { get; }
        public MoveEntry Entry { get; }
        public int DisplayPower { get; }
        private readonly BitmapFont _moveFont;
        private readonly Texture2D? _backgroundSpriteSheet;
        public bool IsAnimating => _animState == AnimationState.Appearing;
        public Texture2D IconTexture { get; set; }
        public Rectangle? IconSourceRect { get; set; }
        private enum AnimationState { Hidden, Idle, Appearing }
        private AnimationState _animState = AnimationState.Idle;
        private float _appearTimer = 0f;
        private const float APPEAR_DURATION = 0.25f;

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

        private static readonly Random _random = new Random();
        private float _overlayFadeTimer;
        private const float OVERLAY_FADE_SPEED = 2.0f;

        public MoveButton(MoveData move, MoveEntry entry, int displayPower, BitmapFont font, Texture2D? backgroundSpriteSheet, Texture2D iconTexture, Rectangle? iconSourceRect, bool startVisible = true)
            : base(Rectangle.Empty, move.MoveName.ToUpper(), function: move.MoveID)
        {
            Move = move;
            Entry = entry;
            DisplayPower = displayPower;
            _moveFont = font;
            _backgroundSpriteSheet = backgroundSpriteSheet;
            IconTexture = iconTexture;
            IconSourceRect = iconSourceRect;
            _animState = startVisible ? AnimationState.Idle : AnimationState.Hidden;
            _overlayFadeTimer = (float)(_random.NextDouble() * Math.PI * 2.0);

            // Updated to use Middle Click for info
            HasMiddleClickHint = true;
            HasRightClickHint = false;

            // Configure Text Animation
            EnableTextWave = true;
            // Use LeftAlignedSmallWave to match the left alignment
            WaveEffectType = TextEffectType.LeftAlignedSmallWave;
        }

        public void TriggerAppearAnimation()
        {
            if (_animState == AnimationState.Hidden)
            {
                _animState = AnimationState.Appearing;
                _appearTimer = 0f;
            }
        }

        public void ShowInstantly()
        {
            _animState = AnimationState.Idle;
        }

        public override void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            base.Update(currentMouseState, worldTransform);
        }

        private void UpdateScrolling(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            switch (_scrollState)
            {
                case ScrollState.PausedAtStart:
                    _scrollWaitTimer -= dt;
                    if (_scrollWaitTimer <= 0)
                    {
                        _scrollState = ScrollState.Scrolling;
                    }
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
            if (_animState == AnimationState.Hidden) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            var player = ServiceLocator.Get<BattleManager>().AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);

            // --- MANA DUMP LOGIC ---
            var manaDump = Move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
            bool canAfford;
            if (manaDump != null)
            {
                canAfford = player != null && player.Stats.CurrentMana > 0;
            }
            else
            {
                canAfford = player != null && player.Stats.CurrentMana >= Move.ManaCost;
            }

            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // Update the animator state but ignore the offset result since we handle shifting manually
            _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);

            _overlayFadeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // --- Calculate Appear Animation Scale ---
            float appearScaleX = 1.0f;
            float appearScaleY = 1.0f;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_animState == AnimationState.Appearing)
            {
                _appearTimer += dt;
                float progress = Math.Clamp(_appearTimer / APPEAR_DURATION, 0f, 1f);

                appearScaleY = Easing.EaseOutBack(progress);

                if (progress >= 1.0f)
                {
                    _animState = AnimationState.Idle;
                }
            }

            // Use only appear scale, no hover scaling
            float finalScaleX = appearScaleX;
            float finalScaleY = appearScaleY;

            if (finalScaleX < 0.01f || finalScaleY < 0.01f) return;

            int animatedWidth = (int)(Bounds.Width * finalScaleX);
            int animatedHeight = (int)(Bounds.Height * finalScaleY);
            var animatedBounds = new Rectangle(
                Bounds.Center.X - animatedWidth / 2 + (int)(horizontalOffset ?? 0f),
                Bounds.Center.Y - animatedHeight / 2 + (int)(verticalOffset ?? 0f),
                animatedWidth,
                animatedHeight
            );

            // Update base class animations (Rotation, Flash)
            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime); // Updates _currentHoverRotation

            Color finalTintColor;
            if (tintColorOverride.HasValue)
            {
                finalTintColor = tintColorOverride.Value;
            }
            else
            {
                finalTintColor = Color.White;
                if (!IsEnabled) finalTintColor = _global.ButtonDisableColor * 0.5f;
                else if (!canAfford) finalTintColor = _global.ButtonDisableColor * 0.5f;
                else if (_isPressed) finalTintColor = _global.Palette_Shadow;
                else if (isActivated) finalTintColor = _global.ButtonHoverColor;
            }

            if (finalScaleX > 0.1f && finalScaleY > 0.1f)
            {
                float contentAlpha = finalTintColor.A / 255f;

                // ROTATION HELPER
                Vector2 RotateOffset(Vector2 local)
                {
                    float cos = MathF.Cos(_currentHoverRotation);
                    float sin = MathF.Sin(_currentHoverRotation);
                    return new Vector2(
                        local.X * cos - local.Y * sin,
                        local.X * sin + local.Y * cos
                    );
                }

                Vector2 centerPos = new Vector2(animatedBounds.Center.X, animatedBounds.Center.Y);

                // Draw background (No Shift, Rotated)
                if (_backgroundSpriteSheet != null)
                {
                    // Round origin to prevent sub-pixel rendering artifacts
                    var origin = new Vector2(MathF.Round(Bounds.Width / 2f), MathF.Round(Bounds.Height / 2f));
                    // Apply Rotation
                    spriteBatch.DrawSnapped(_backgroundSpriteSheet, centerPos, null, finalTintColor, _currentHoverRotation, origin, new Vector2(finalScaleX, finalScaleY), SpriteEffects.None, 0f);
                }

                const int iconSize = 9;
                const int iconPadding = 4;

                // --- LEFT ALIGNED LAYOUT CALCULATION ---
                var moveNameTextSize = _moveFont.MeasureString(this.Text);
                // Fixed left padding from the button's left edge
                const int contentLeftPadding = 6;
                float startX = -Bounds.Width / 2f + contentLeftPadding;

                // Icon Positioning Relative to Center
                float iconLocalX = startX + (iconSize / 2f); // Center of icon
                float iconLocalY = 0; // Centered

                Vector2 iconOffset = new Vector2(iconLocalX, iconLocalY);
                Vector2 rotatedIconPos = centerPos + RotateOffset(iconOffset);

                // Origin is center of icon
                Vector2 iconOrigin = new Vector2(iconSize / 2f, iconSize / 2f);

                if (IconTexture != null && IconSourceRect.HasValue)
                {
                    spriteBatch.DrawSnapped(IconTexture, rotatedIconPos, IconSourceRect.Value, Color.White * contentAlpha, _currentHoverRotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                }
                else
                {
                    // Fallback placeholder
                    spriteBatch.DrawSnapped(pixel, rotatedIconPos, null, _global.Palette_Pink * contentAlpha, _currentHoverRotation, iconOrigin, new Vector2(iconSize, iconSize), SpriteEffects.None, 0f);
                }

                // --- TEXT COLOR LOGIC ---
                Color textColor;
                if (!IsEnabled || !canAfford)
                {
                    textColor = _global.ButtonDisableColor;
                }
                else if (isActivated)
                {
                    textColor = _global.ButtonHoverColor;
                }
                else
                {
                    textColor = _global.GameTextColor;
                }

                // Text Position
                float textLocalX = startX + iconSize + iconPadding; // Left edge of text
                float textLocalY = 0; // Centered Y

                // Check for scrolling (if text is wider than button minus icon)
                // Button Width - Icon - Padding - Margins
                float textAvailableWidth = Bounds.Width - iconSize - iconPadding - 8;
                bool needsScrolling = moveNameTextSize.Width > textAvailableWidth;

                if (needsScrolling)
                {
                    // SCROLLING LOGIC (NO ROTATION SUPPORT)
                    // Clipping rectangles don't support rotation.
                    // Just draw flat for now if scrolling.

                    if (!_isScrollingInitialized)
                    {
                        _isScrollingInitialized = true;
                        float gapWidth = _moveFont.MeasureString(new string(' ', SCROLL_GAP_SPACES)).Width;
                        _loopWidth = moveNameTextSize.Width + gapWidth;
                        _scrollWaitTimer = SCROLL_PAUSE_DURATION;
                        _scrollState = ScrollState.PausedAtStart;
                        _scrollPosition = 0;
                    }

                    UpdateScrolling(gameTime);

                    // For scrolling, we anchor the text start position relative to the icon's visual position
                    // But since we can't rotate the clip rect easily, we just draw it flat.
                    // We'll use the calculated textLocalX relative to the unrotated center.
                    float textStartX = animatedBounds.Center.X + textLocalX;

                    var originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
                    var originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
                    spriteBatch.End();

                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, _clipRasterizerState, null, transform);
                    var clipRect = new Rectangle((int)textStartX, animatedBounds.Y, (int)textAvailableWidth, animatedBounds.Height);
                    spriteBatch.GraphicsDevice.ScissorRectangle = clipRect;

                    var scrollingTextPosition = new Vector2(textStartX - _scrollPosition, animatedBounds.Y + (animatedBounds.Height - _moveFont.LineHeight) / 2f);

                    // Scrolling text doesn't wave to avoid visual chaos
                    spriteBatch.DrawStringSnapped(_moveFont, this.Text, scrollingTextPosition, textColor * contentAlpha);
                    spriteBatch.DrawStringSnapped(_moveFont, this.Text, scrollingTextPosition + new Vector2(_loopWidth, 0), textColor * contentAlpha);

                    spriteBatch.End();
                    spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, originalRasterizerState, null, transform);
                }
                else
                {
                    _isScrollingInitialized = false;

                    Vector2 textOffset = new Vector2(textLocalX, textLocalY);
                    Vector2 rotatedTextPos = centerPos + RotateOffset(textOffset);

                    // Origin for text: Left-Center
                    Vector2 textOrigin = new Vector2(0, _moveFont.LineHeight / 2f);

                    // --- Wave Animation Logic ---
                    if (EnableTextWave && isActivated)
                    {
                        _waveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                        // Only reset timer if it's a one-shot effect like SmallWave
                        if (TextAnimator.IsOneShotEffect(WaveEffectType))
                        {
                            float duration = TextAnimator.GetSmallWaveDuration(Text.Length);
                            if (_waveTimer > duration + 0.1f) _waveTimer = 0f;
                        }
                        // Else: Continuous effects just keep growing _waveTimer

                        TextAnimator.DrawTextWithEffect(spriteBatch, _moveFont, this.Text, rotatedTextPos - textOrigin, textColor * contentAlpha, WaveEffectType, _waveTimer, new Vector2(finalScaleX, finalScaleY), null, _currentHoverRotation);
                    }
                    else
                    {
                        _waveTimer = 0f;
                        spriteBatch.DrawStringSnapped(_moveFont, this.Text, rotatedTextPos, textColor * contentAlpha, _currentHoverRotation, textOrigin, 1.0f, SpriteEffects.None, 0f);
                    }
                }

                // --- Strikethrough Logic for Disabled State ---
                if (!IsEnabled)
                {
                    // Rotate
                    Vector2 lineStartLocal = new Vector2(textLocalX - 2, 0);
                    Vector2 lineEndLocal = new Vector2(textLocalX + Math.Min(moveNameTextSize.Width, textAvailableWidth) + 2, 0);

                    Vector2 p1 = centerPos + RotateOffset(lineStartLocal);
                    Vector2 p2 = centerPos + RotateOffset(lineEndLocal);

                    spriteBatch.DrawLineSnapped(p1, p2, _global.ButtonDisableColor);
                }

                if (!canAfford && isActivated && IsEnabled)
                {
                    string noManaText = "NOT ENOUGH MANA";
                    Vector2 noManaSize = _moveFont.MeasureString(noManaText);
                    Vector2 noManaPos = new Vector2(
                        animatedBounds.Center.X - noManaSize.X / 2f,
                        animatedBounds.Center.Y - noManaSize.Y / 2f
                    );
                    TextAnimator.DrawTextWithEffectSquareOutlined(spriteBatch, _moveFont, noManaText, noManaPos, _global.Palette_Red * contentAlpha, Color.Black * contentAlpha, TextEffectType.None, 0f);
                }
            }
        }
    }
}
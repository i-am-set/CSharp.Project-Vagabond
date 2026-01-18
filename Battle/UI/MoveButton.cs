using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
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

        // --- Content Shift Logic ---
        private float _currentContentShiftX = 0f;
        private const float HOVER_CONTENT_SHIFT_TARGET = -3f; // Shift 3 pixels left
        private const float SHIFT_SPEED = 15f; // Speed of the tween

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
            // Use RightAlignedSmallWave to match the leftward expansion of text
            WaveEffectType = TextEffectType.SmallWave;
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

            // --- Calculate Content Shift (Tweened) ---
            float targetShift = isActivated ? HOVER_CONTENT_SHIFT_TARGET : 0f;
            // FIX: Use Time-Corrected Damping to prevent overshoot at low FPS
            float shiftDamping = 1.0f - MathF.Exp(-SHIFT_SPEED * dt);
            _currentContentShiftX = MathHelper.Lerp(_currentContentShiftX, targetShift, shiftDamping);

            // Use float for smooth movement
            float pixelShiftX = _currentContentShiftX;

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

                // Draw background (No Shift, Rotated)
                if (_backgroundSpriteSheet != null)
                {
                    // Round origin to prevent sub-pixel rendering artifacts
                    var origin = new Vector2(MathF.Round(Bounds.Width / 2f), MathF.Round(Bounds.Height / 2f));
                    // Use the center of the animated bounds as the position
                    var drawPos = new Vector2(animatedBounds.Center.X, animatedBounds.Center.Y);

                    // Apply Rotation
                    spriteBatch.DrawSnapped(_backgroundSpriteSheet, drawPos, null, finalTintColor, _currentHoverRotation, origin, new Vector2(finalScaleX, finalScaleY), SpriteEffects.None, 0f);
                }

                const int iconSize = 9;
                const int iconPadding = 4;

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

                // Apply content shift to icon
                // Calculate local offset from center
                float iconLocalX = -Bounds.Width / 2f + iconPadding + 1 + pixelShiftX;
                float iconLocalY = 0; // Centered Y

                Vector2 iconOffset = new Vector2(iconLocalX, iconLocalY);
                Vector2 rotatedIconPos = new Vector2(animatedBounds.Center.X, animatedBounds.Center.Y) + RotateOffset(iconOffset);

                // Origin is center of icon
                Vector2 iconOrigin = new Vector2(iconSize / 2f, iconSize / 2f);
                // Adjust position to account for origin
                rotatedIconPos += iconOrigin; // Wait, DrawSnapped takes origin. Position is anchor.
                // If we want to draw centered at rotatedIconPos, we pass iconOrigin.

                if (IconTexture != null && IconSourceRect.HasValue)
                {
                    spriteBatch.DrawSnapped(IconTexture, rotatedIconPos, IconSourceRect.Value, Color.White * contentAlpha, _currentHoverRotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                }
                else
                {
                    // Fallback placeholder
                    // We can't rotate a rect directly with DrawSnapped(pixel, rect), need scale/origin.
                    spriteBatch.DrawSnapped(pixel, rotatedIconPos, null, _global.Palette_Pink * contentAlpha, _currentHoverRotation, iconOrigin, new Vector2(iconSize, iconSize), SpriteEffects.None, 0f);
                }

                var textColor = isActivated && canAfford && IsEnabled ? _global.ButtonHoverColor : _global.Palette_Sun;
                if (!canAfford || !IsEnabled)
                {
                    textColor = _global.ButtonDisableColor;
                }

                // Apply content shift to text start position
                // Note: iconPos already includes the shift, so textStartX is relative to the shifted icon
                // Text Start Local X
                float textLocalX = iconLocalX + iconSize + iconPadding; // relative to center
                float textLocalY = 0;

                const int textRightMargin = 4;

                // Calculate available width based on the *original* bounds to prevent jitter in scrolling calculation
                // Bounds.Right relative to center is Width/2
                float rightEdgeLocalX = Bounds.Width / 2f;
                float textAvailableWidth = rightEdgeLocalX - textLocalX - textRightMargin;

                var moveNameTextSize = _moveFont.MeasureString(this.Text);
                bool needsScrolling = moveNameTextSize.Width > textAvailableWidth;

                if (needsScrolling)
                {
                    // SCROLLING LOGIC (NO ROTATION SUPPORT)
                    // Clipping rectangles don't support rotation.
                    // If we need scrolling + rotation, it requires a RenderTarget or Stencil.
                    // For now, we skip rotation if scrolling.

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

                    // Re-calculate absolute coords for scissor
                    // Assume flat because scissor rect is axis aligned.
                    // If rotating, scrolling text might look weird clipped by a box.
                    // Just draw flat for now if scrolling.

                    float textStartX = animatedBounds.X + (animatedBounds.Width / 2f) + textLocalX;

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

                    Vector2 textOffset = new Vector2(textLocalX, textLocalY); // From Center
                    Vector2 rotatedTextPos = new Vector2(animatedBounds.Center.X, animatedBounds.Center.Y) + RotateOffset(textOffset);

                    // Origin for text is Left-Center (since textLocalX is the left edge)
                    // Wait, textLocalX is the left edge relative to center.
                    // So we want to draw at rotatedTextPos with origin (0, Height/2)
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

                        // Use TextAnimator for the wave effect, passing the combined scale
                        // We calculate the top-left of the text block relative to the anchor
                        // TextAnimator.DrawTextWithEffect expects the anchor position.

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

                    Vector2 center = new Vector2(animatedBounds.Center.X, animatedBounds.Center.Y);
                    Vector2 p1 = center + RotateOffset(lineStartLocal);
                    Vector2 p2 = center + RotateOffset(lineEndLocal);

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
                    // This text overlays everything, no rotation needed for readability
                    TextAnimator.DrawTextWithEffectSquareOutlined(spriteBatch, _moveFont, noManaText, noManaPos, _global.Palette_Red * contentAlpha, Color.Black * contentAlpha, TextEffectType.None, 0f);
                }
            }
        }
    }
}
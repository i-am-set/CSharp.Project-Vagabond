using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        // Hover Scaling State (Replicated from Button.cs since _currentScale is private there)
        private float _currentHoverScale = 1.0f;
        private const float HOVER_SCALE = 1.1f;
        private const float PRESS_SCALE = 0.95f;
        private const float SCALE_SPEED = 15f;

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

            float hoverOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            _overlayFadeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // --- Calculate Hover Scale ---
            float targetHoverScale = 1.0f;
            if (_isPressed) targetHoverScale = PRESS_SCALE;
            else if (isActivated) targetHoverScale = HOVER_SCALE;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _currentHoverScale = MathHelper.Lerp(_currentHoverScale, targetHoverScale, dt * SCALE_SPEED);

            // --- Calculate Appear Animation Scale ---
            float appearScaleX = 1.0f;
            float appearScaleY = 1.0f;
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

            // Combine scales
            float finalScaleX = appearScaleX * _currentHoverScale;
            float finalScaleY = appearScaleY * _currentHoverScale;

            if (finalScaleX < 0.01f || finalScaleY < 0.01f) return;

            int animatedWidth = (int)(Bounds.Width * finalScaleX);
            int animatedHeight = (int)(Bounds.Height * finalScaleY);
            var animatedBounds = new Rectangle(
                Bounds.Center.X - animatedWidth / 2 + (int)(horizontalOffset ?? 0f) - (int)hoverOffset,
                Bounds.Center.Y - animatedHeight / 2 + (int)(verticalOffset ?? 0f),
                animatedWidth,
                animatedHeight
            );

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
                else if (_isPressed) finalTintColor = Color.Gray;
                else if (isActivated) finalTintColor = _global.ButtonHoverColor;
            }

            if (finalScaleX > 0.1f && finalScaleY > 0.1f)
            {
                float contentAlpha = finalTintColor.A / 255f;

                // Draw background
                if (_backgroundSpriteSheet != null)
                {
                    // Round origin to prevent sub-pixel rendering artifacts
                    var origin = new Vector2(MathF.Round(Bounds.Width / 2f), MathF.Round(Bounds.Height / 2f));
                    // Use the center of the animated bounds as the position
                    var drawPos = new Vector2(animatedBounds.Center.X, animatedBounds.Center.Y);

                    spriteBatch.DrawSnapped(_backgroundSpriteSheet, drawPos, null, finalTintColor, 0f, origin, new Vector2(finalScaleX, finalScaleY), SpriteEffects.None, 0f);
                }

                const int iconSize = 9;
                const int iconPadding = 4;
                var iconRect = new Rectangle(
                    animatedBounds.X + iconPadding + 1,
                    animatedBounds.Y + (animatedBounds.Height - iconSize) / 2,
                    iconSize,
                    iconSize
                );

                if (IconTexture != null && IconSourceRect.HasValue)
                {
                    spriteBatch.DrawSnapped(IconTexture, iconRect, IconSourceRect.Value, Color.White * contentAlpha);
                }
                else
                {
                    spriteBatch.DrawSnapped(pixel, iconRect, _global.Palette_Pink * contentAlpha);
                }

                var textColor = isActivated && canAfford && IsEnabled ? _global.ButtonHoverColor : _global.Palette_BlueWhite;
                if (!canAfford || !IsEnabled)
                {
                    textColor = _global.ButtonDisableColor;
                }

                float textStartX = iconRect.Right + iconPadding;
                const int textRightMargin = 4;
                float textAvailableWidth = animatedBounds.Right - textStartX - textRightMargin;
                var moveNameTextSize = _moveFont.MeasureString(this.Text);
                bool needsScrolling = moveNameTextSize.Width > textAvailableWidth;

                if (needsScrolling)
                {
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

                    var originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
                    var originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
                    spriteBatch.End();

                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, _clipRasterizerState, null, transform);
                    var clipRect = new Rectangle((int)textStartX, animatedBounds.Y, (int)textAvailableWidth, animatedBounds.Height);
                    spriteBatch.GraphicsDevice.ScissorRectangle = clipRect;

                    var scrollingTextPosition = new Vector2(textStartX - _scrollPosition, animatedBounds.Y + (animatedBounds.Height - _moveFont.LineHeight) / 2);

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
                    var textPosition = new Vector2(textStartX, animatedBounds.Y + (animatedBounds.Height - _moveFont.LineHeight) / 2);

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
                        TextAnimator.DrawTextWithEffect(spriteBatch, _moveFont, this.Text, textPosition, textColor * contentAlpha, WaveEffectType, _waveTimer, new Vector2(finalScaleX, finalScaleY));
                    }
                    else
                    {
                        _waveTimer = 0f;
                        spriteBatch.DrawStringSnapped(_moveFont, this.Text, textPosition, textColor * contentAlpha);
                    }
                }

                // --- Strikethrough Logic for Disabled State ---
                if (!IsEnabled)
                {
                    float lineY = animatedBounds.Center.Y + 1;
                    float startX = textStartX - 2;
                    float endX = textStartX + Math.Min(moveNameTextSize.Width, textAvailableWidth) + 2;
                    spriteBatch.DrawLineSnapped(new Vector2(startX, lineY), new Vector2(endX, lineY), _global.ButtonDisableColor);
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
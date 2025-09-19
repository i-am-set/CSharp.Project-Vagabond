using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle.UI
{
    public class MoveButton : Button
    {
        public MoveData Move { get; }
        public int DisplayPower { get; }
        private readonly BitmapFont _moveFont;
        private readonly Texture2D _backgroundSpriteSheet;
        private readonly bool _isNew;

        public Texture2D IconTexture { get; set; }
        public Rectangle? IconSourceRect { get; set; }

        // Animation State
        private enum AnimationState { Hidden, Idle, Appearing }
        private AnimationState _animState = AnimationState.Idle;
        private float _appearTimer = 0f;
        private const float APPEAR_DURATION = 0.25f; // Duration of the appear animation

        // Scrolling Text State
        private bool _isScrollingInitialized = false;
        private float _scrollPosition = 0f;
        private float _scrollWaitTimer = 0f;
        private float _maxScrollToShowEnd = 0f;
        private enum ScrollState { PausedAtStart, ScrollingToEnd, PausedAtEnd }
        private ScrollState _scrollState = ScrollState.PausedAtStart;

        // Scrolling Tuning
        private const float SCROLL_SPEED = 25f; // pixels per second
        private const float SCROLL_PAUSE_DURATION = 1.5f; // seconds
        private const int EXTRA_SCROLL_SPACES = 1; // Number of extra space widths to scroll past the end

        private static readonly RasterizerState _clipRasterizerState = new RasterizerState { ScissorTestEnable = true };


        public MoveButton(MoveData move, int displayPower, BitmapFont font, Texture2D backgroundSpriteSheet, Texture2D iconTexture, Rectangle? iconSourceRect, bool isNew, bool startVisible = true)
            : base(Rectangle.Empty, move.MoveName.ToUpper(), function: move.MoveID)
        {
            Move = move;
            DisplayPower = displayPower;
            _moveFont = font;
            _backgroundSpriteSheet = backgroundSpriteSheet;
            IconTexture = iconTexture;
            IconSourceRect = iconSourceRect;
            _isNew = isNew;
            _animState = startVisible ? AnimationState.Idle : AnimationState.Hidden;
        }

        public void TriggerAppearAnimation()
        {
            if (_animState == AnimationState.Hidden)
            {
                _animState = AnimationState.Appearing;
                _appearTimer = 0f;
            }
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
                        _scrollState = ScrollState.ScrollingToEnd;
                    }
                    break;

                case ScrollState.ScrollingToEnd:
                    _scrollPosition += SCROLL_SPEED * dt;
                    if (_scrollPosition >= _maxScrollToShowEnd)
                    {
                        _scrollPosition = _maxScrollToShowEnd;
                        _scrollState = ScrollState.PausedAtEnd;
                        _scrollWaitTimer = SCROLL_PAUSE_DURATION;
                    }
                    break;

                case ScrollState.PausedAtEnd:
                    _scrollWaitTimer -= dt;
                    if (_scrollWaitTimer <= 0)
                    {
                        // Snap back to the start and pause again
                        _scrollPosition = 0;
                        _scrollState = ScrollState.PausedAtStart;
                        _scrollWaitTimer = SCROLL_PAUSE_DURATION;
                    }
                    break;
            }
        }


        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false)
        {
            if (_animState == AnimationState.Hidden) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            float hopOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);

            // --- Animation Scaling ---
            float verticalScale = 1.0f;
            if (_animState == AnimationState.Appearing)
            {
                _appearTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Clamp(_appearTimer / APPEAR_DURATION, 0f, 1f);

                // Apply overshoot bounce easing to the Y-scale
                verticalScale = Easing.EaseOutBack(progress);

                if (progress >= 1.0f)
                {
                    _animState = AnimationState.Idle;
                }
            }

            if (verticalScale < 0.01f) return;

            // --- Calculate Animated Bounds ---
            int animatedHeight = (int)(Bounds.Height * verticalScale);
            var animatedBounds = new Rectangle(
                Bounds.X + (int)hopOffset,
                Bounds.Center.Y - animatedHeight / 2, // Expand from the center
                Bounds.Width,
                animatedHeight
            );

            // Draw background texture
            Color tintColor = Color.White;
            if (!IsEnabled) tintColor = _global.ButtonDisableColor * 0.5f;
            else if (_isPressed) tintColor = Color.Gray;
            else if (isActivated) tintColor = _global.ButtonHoverColor;

            if (_isNew && _animState == AnimationState.Appearing)
            {
                // Flash from pink to the normal tint color during the first part of the animation
                const float flashRatio = 0.75f;
                float flashDuration = APPEAR_DURATION * flashRatio;
                if (_appearTimer < flashDuration)
                {
                    float flashProgress = _appearTimer / flashDuration;
                    tintColor = Color.Lerp(_global.Palette_Red, tintColor, Easing.EaseInQuad(flashProgress));
                }
            }

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            if (spriteManager.RarityBackgroundSourceRects.TryGetValue(Move.Rarity, out var bgSourceRect))
            {
                spriteBatch.DrawSnapped(_backgroundSpriteSheet, animatedBounds, bgSourceRect, tintColor);
            }


            // Only draw contents if the button is mostly visible to avoid squashed text/icons
            if (verticalScale > 0.8f)
            {
                // --- Draw Icon/Placeholder ---
                const int iconSize = 9;
                const int iconPadding = 4;
                var iconRect = new Rectangle(
                    animatedBounds.X + iconPadding,
                    animatedBounds.Y + (animatedBounds.Height - iconSize) / 2,
                    iconSize,
                    iconSize
                );

                if (IconTexture != null && IconSourceRect.HasValue)
                {
                    spriteBatch.DrawSnapped(IconTexture, iconRect, IconSourceRect.Value, Color.White);
                }
                else
                {
                    spriteBatch.DrawSnapped(pixel, iconRect, _global.Palette_Pink); // Fallback
                }

                // --- Prepare for text drawing ---
                var textColor = isActivated ? _global.ButtonHoverColor : _global.Palette_BrightWhite;
                if (!IsEnabled)
                {
                    textColor = _global.ButtonDisableColor;
                }
                var statsColor = isActivated ? _global.ButtonHoverColor * 0.9f : _global.Palette_White;
                if (!IsEnabled)
                {
                    statsColor = _global.ButtonDisableColor;
                }

                // --- Calculate text and stats layout ---
                string powerText = DisplayPower > 0 ? DisplayPower.ToString() : "---";
                string accuracyText = Move.Accuracy >= 0 ? $"{Move.Accuracy}%" : "---";
                var powerTextSize = _moveFont.MeasureString(powerText);
                var accuracyTextSize = _moveFont.MeasureString(accuracyText);
                var maxAccuracyTextSize = _moveFont.MeasureString("100%");
                const int rightPadding = 6;
                const int statPadding = 2;
                const int verticalContentPadding = 3;
                float contentTopY = animatedBounds.Y + verticalContentPadding;
                float contentBottomY = animatedBounds.Bottom - verticalContentPadding;
                var accuracyPosition = new Vector2(animatedBounds.Right - rightPadding - accuracyTextSize.Width, contentTopY);
                float powerTextRightEdge = animatedBounds.Right - rightPadding - maxAccuracyTextSize.Width - statPadding;
                var powerPosition = new Vector2(powerTextRightEdge - powerTextSize.Width, contentBottomY - powerTextSize.Height - 1);

                // --- Calculate available space for move name ---
                float textStartX = iconRect.Right + iconPadding;
                const int textRightMargin = 4;
                float textAvailableWidth = powerTextRightEdge - powerTextSize.Width - textStartX - textRightMargin;
                var moveNameTextSize = _moveFont.MeasureString(this.Text);
                bool needsScrolling = moveNameTextSize.Width > textAvailableWidth;

                // --- Draw Move Name (static or scrolling) ---
                if (needsScrolling)
                {
                    if (!_isScrollingInitialized)
                    {
                        _isScrollingInitialized = true;
                        float spaceWidth = _moveFont.MeasureString(" ").Width;
                        _maxScrollToShowEnd = moveNameTextSize.Width - textAvailableWidth + (spaceWidth * EXTRA_SCROLL_SPACES);
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
                    spriteBatch.DrawStringSnapped(_moveFont, this.Text, scrollingTextPosition, textColor);

                    spriteBatch.End();
                    spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, originalRasterizerState, null, transform);
                }
                else
                {
                    _isScrollingInitialized = false;
                    var textPosition = new Vector2(textStartX, animatedBounds.Y + (animatedBounds.Height - _moveFont.LineHeight) / 2);
                    spriteBatch.DrawStringSnapped(_moveFont, this.Text, textPosition, textColor);
                }

                // --- Draw Power & Accuracy ---
                Color powerTextColor = statsColor;
                string powerIndicator = "";

                if (Move.Power > 0 && DisplayPower != Move.Power)
                {
                    if (DisplayPower > Move.Power)
                    {
                        powerIndicator = "+";
                        float increaseRatio = (float)(DisplayPower - Move.Power) / Move.Power;
                        float lerpAmount = Math.Clamp(increaseRatio, 0f, 1f);
                        powerTextColor = Color.Lerp(statsColor, Color.DeepPink, lerpAmount);
                    }
                    else // DisplayPower < Move.Power
                    {
                        powerIndicator = "-";
                        // Use a dim red for reduced power to indicate a penalty.
                        powerTextColor = Color.Lerp(statsColor, _global.Palette_Red, 0.75f);
                    }
                }

                spriteBatch.DrawStringSnapped(_moveFont, accuracyText, accuracyPosition, statsColor);
                spriteBatch.DrawStringSnapped(_moveFont, powerText, powerPosition, powerTextColor);

                if (!string.IsNullOrEmpty(powerIndicator))
                {
                    var indicatorPosition = new Vector2(powerPosition.X + powerTextSize.Width + 1, powerPosition.Y);
                    Color indicatorColor = powerTextColor * 0.25f;
                    spriteBatch.DrawStringSnapped(_moveFont, powerIndicator, indicatorPosition, indicatorColor);
                }


                // --- Draw Target Type Indicator ---
                string targetIndicator = Move.Target switch
                {
                    TargetType.Single => ".",
                    TargetType.Every => "...",
                    TargetType.Self => "^",
                    TargetType.SingleAll => "*",
                    TargetType.EveryAll => "***",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(targetIndicator))
                {
                    var indicatorSize = _moveFont.MeasureString(targetIndicator);
                    // Center the indicator horizontally over the power text.
                    float powerCenterX = powerPosition.X + powerTextSize.Width / 2;
                    var indicatorPosition = new Vector2(
                        powerCenterX - indicatorSize.Width / 2,
                        powerPosition.Y - 7
                    );
                    spriteBatch.DrawStringSnapped(_moveFont, targetIndicator, indicatorPosition, statsColor);
                }
            }
        }
    }
}
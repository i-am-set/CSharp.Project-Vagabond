#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public class MoveButton : Button
    {
        public MoveData Move { get; }
        public SpellbookEntry Entry { get; }
        public int DisplayPower { get; }
        private readonly BitmapFont _moveFont;
        private readonly Texture2D _backgroundSpriteSheet;
        public bool IsAnimating => _animState == AnimationState.Appearing;


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
        private float _loopWidth = 0f;
        private enum ScrollState { PausedAtStart, Scrolling, PausedAtLoopPoint }
        private ScrollState _scrollState = ScrollState.PausedAtStart;

        // Scrolling Tuning
        private const float SCROLL_SPEED = 25f; // pixels per second
        private const float SCROLL_PAUSE_DURATION = 1.5f; // seconds
        private const int SCROLL_GAP_SPACES = 3; // Number of space characters to use as a gap

        private static readonly RasterizerState _clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

        // Overlay Fade Animation
        private static readonly Random _random = new Random();
        private float _overlayFadeTimer;
        private const float OVERLAY_FADE_SPEED = 2.0f;


        public MoveButton(MoveData move, SpellbookEntry entry, int displayPower, BitmapFont font, Texture2D backgroundSpriteSheet, Texture2D iconTexture, Rectangle? iconSourceRect, bool startVisible = true)
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
            _overlayFadeTimer = (float)(_random.NextDouble() * Math.PI * 2.0); // Random start phase for desynchronization
            HasRightClickHint = true;
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
                        _scrollPosition = _loopWidth; // Clamp to the exact loop point
                        _scrollState = ScrollState.PausedAtLoopPoint;
                        _scrollWaitTimer = 1.0f; // The requested 1-second pause
                    }
                    break;

                case ScrollState.PausedAtLoopPoint:
                    _scrollWaitTimer -= dt;
                    if (_scrollWaitTimer <= 0)
                    {
                        // This is the key part. After the pause, we wrap the position
                        // and immediately continue scrolling in the same frame.
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
            bool canAfford = player != null && player.Stats.CurrentMana >= Move.ManaCost;

            bool isActivated = IsEnabled && (IsHovered || forceHover);

            float hoverOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            _overlayFadeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // --- Animation Scaling ---
            float scaleX = 1.0f;
            float scaleY = 1.0f;
            if (_animState == AnimationState.Appearing)
            {
                _appearTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Clamp(_appearTimer / APPEAR_DURATION, 0f, 1f);

                // Pop in vertically
                scaleY = Easing.EaseOutBack(progress);

                if (progress >= 1.0f)
                {
                    _animState = AnimationState.Idle;
                }
            }

            if (scaleX < 0.01f || scaleY < 0.01f) return;

            // --- Calculate Animated Bounds ---
            int animatedWidth = (int)(Bounds.Width * scaleX);
            int animatedHeight = (int)(Bounds.Height * scaleY);
            var animatedBounds = new Rectangle(
                Bounds.Center.X - animatedWidth / 2 + (int)(horizontalOffset ?? 0f) - (int)hoverOffset,
                Bounds.Center.Y - animatedHeight / 2 + (int)(verticalOffset ?? 0f),
                animatedWidth,
                animatedHeight
            );

            // --- Determine Tint Color ---
            Color finalTintColor;
            if (tintColorOverride.HasValue)
            {
                finalTintColor = tintColorOverride.Value;
            }
            else
            {
                finalTintColor = Color.White;
                if (!canAfford) finalTintColor = _global.ButtonDisableColor * 0.5f;
                else if (_isPressed) finalTintColor = Color.Gray;
                else if (isActivated) finalTintColor = _global.ButtonHoverColor;
            }

            // Only draw contents if the button is mostly visible to avoid squashed text/icons
            if (scaleX > 0.1f && scaleY > 0.1f)
            {
                float contentAlpha = finalTintColor.A / 255f;

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
                    spriteBatch.DrawSnapped(IconTexture, iconRect, IconSourceRect.Value, Color.White * contentAlpha);
                }
                else
                {
                    spriteBatch.DrawSnapped(pixel, iconRect, _global.Palette_Pink * contentAlpha); // Fallback
                }

                // --- Prepare for text drawing ---
                var textColor = isActivated && canAfford ? _global.ButtonHoverColor : _global.Palette_BrightWhite;
                if (!canAfford)
                {
                    textColor = _global.ButtonDisableColor;
                }

                // --- Calculate available space for move name ---
                float textStartX = iconRect.Right + iconPadding;
                const int textRightMargin = 4;
                float textAvailableWidth = animatedBounds.Right - textStartX - textRightMargin;
                var moveNameTextSize = _moveFont.MeasureString(this.Text);
                bool needsScrolling = moveNameTextSize.Width > textAvailableWidth;

                // --- Draw Move Name (static or scrolling) ---
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
                    spriteBatch.DrawStringSnapped(_moveFont, this.Text, textPosition, textColor * contentAlpha);
                }

                // --- Draw "NO MANA" overlay ---
                if (!canAfford && isActivated)
                {
                    string noManaText = "NOT ENOUGH MANA";
                    Vector2 noManaSize = _moveFont.MeasureString(noManaText);
                    Vector2 noManaPos = new Vector2(
                        animatedBounds.Center.X - noManaSize.X / 2f,
                        animatedBounds.Center.Y - noManaSize.Y / 2f
                    );
                    spriteBatch.DrawStringOutlinedSnapped(_moveFont, noManaText, noManaPos, _global.Palette_Red * contentAlpha, Color.Black * contentAlpha);
                }
            }
        }
    }
}
#nullable restore
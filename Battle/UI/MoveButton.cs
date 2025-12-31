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
        private readonly Texture2D? _backgroundSpriteSheet; // Made nullable
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
                // If ManaDump is present, cost is "All Remaining", so we can afford it as long as we have > 0.
                canAfford = player != null && player.Stats.CurrentMana > 0;
            }
            else
            {
                canAfford = player != null && player.Stats.CurrentMana >= Move.ManaCost;
            }

            bool isActivated = IsEnabled && (IsHovered || forceHover);

            float hoverOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            _overlayFadeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            float scaleX = 1.0f;
            float scaleY = 1.0f;
            if (_animState == AnimationState.Appearing)
            {
                _appearTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Clamp(_appearTimer / APPEAR_DURATION, 0f, 1f);

                scaleY = Easing.EaseOutBack(progress);

                if (progress >= 1.0f)
                {
                    _animState = AnimationState.Idle;
                }
            }

            if (scaleX < 0.01f || scaleY < 0.01f) return;

            int animatedWidth = (int)(Bounds.Width * scaleX);
            int animatedHeight = (int)(Bounds.Height * scaleY);
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
                if (!IsEnabled) finalTintColor = _global.ButtonDisableColor * 0.5f; // Dim if disabled
                else if (!canAfford) finalTintColor = _global.ButtonDisableColor * 0.5f;
                else if (_isPressed) finalTintColor = Color.Gray;
                else if (isActivated) finalTintColor = _global.ButtonHoverColor;
            }

            if (scaleX > 0.1f && scaleY > 0.1f)
            {
                float contentAlpha = finalTintColor.A / 255f;

                // Draw background only if texture is provided
                if (_backgroundSpriteSheet != null)
                {
                    spriteBatch.DrawSnapped(_backgroundSpriteSheet, animatedBounds, finalTintColor);
                }

                const int iconSize = 9;
                const int iconPadding = 4;
                // Shift content 1 pixel to the right
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

                var textColor = isActivated && canAfford && IsEnabled ? _global.ButtonHoverColor : _global.Palette_BrightWhite;
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
                    UpdateWaveTimer((float)gameTime.ElapsedGameTime.TotalSeconds, isActivated);

                    if (EnableTextWave && _isWaveAnimating)
                    {
                        TextUtils.DrawWavedText(spriteBatch, _moveFont, this.Text, textPosition, textColor * contentAlpha, _waveTimer, WaveSpeed, WaveFrequency, WaveAmplitude);
                    }
                    else
                    {
                        spriteBatch.DrawStringSnapped(_moveFont, this.Text, textPosition, textColor * contentAlpha);
                    }
                }

                // --- Contact Tag (Left of Impact) ---
                if (Move.MakesContact)
                {
                    // Impact is at Right - 3.
                    // Gap 1.
                    // Contact Line at Right - 5.
                    // Height 4 (Centered: CenterY - 2).
                    var contactTagRect = new Rectangle(animatedBounds.Right - 5, animatedBounds.Center.Y, 1, 1);
                    spriteBatch.DrawSnapped(pixel, contactTagRect, _global.Palette_Red * contentAlpha);
                }

                // --- Impact Type Color Tag (Inner) ---
                Color impactColor = Move.ImpactType switch
                {
                    ImpactType.Magical => _global.Palette_LightBlue,
                    ImpactType.Physical => _global.Palette_Orange,
                    _ => _global.Palette_Gray
                };

                // Draw at Right - 3 (leaving 1px gap + 1px for MoveType)
                var impactTagRect = new Rectangle(animatedBounds.Right - 3, animatedBounds.Y, 1, animatedBounds.Height);
                spriteBatch.DrawSnapped(pixel, impactTagRect, impactColor * contentAlpha);

                // --- Move Type Color Tag (Outer) ---
                Color moveTypeColor = Move.MoveType switch
                {
                    MoveType.Action => _global.Palette_Orange,
                    MoveType.Spell => _global.Palette_LightBlue,
                    _ => _global.Palette_Gray
                };

                // Draw at Right - 1 (Far right edge)
                // Adjusted: 1 pixel gap at top, 1 pixel gap at bottom (Height - 2)
                var moveTypeTagRect = new Rectangle(animatedBounds.Right - 1, animatedBounds.Y + 1, 1, animatedBounds.Height - 2);
                spriteBatch.DrawSnapped(pixel, moveTypeTagRect, moveTypeColor * contentAlpha);

                // --- Stat Tag (Right of MoveType) ---
                // Only show if NOT Status impact type
                if (Move.ImpactType != ImpactType.Status)
                {
                    // MoveType is at Right - 1.
                    // Gap 1.
                    // Stat Line at Right + 1.
                    // Height 4 (Centered: CenterY - 2).
                    Color statColor = Move.OffensiveStat switch
                    {
                        OffensiveStatType.Strength => _global.StatColor_Strength,
                        OffensiveStatType.Intelligence => _global.StatColor_Intelligence,
                        OffensiveStatType.Tenacity => _global.StatColor_Tenacity,
                        OffensiveStatType.Agility => _global.StatColor_Agility,
                        _ => _global.Palette_Gray
                    };

                    var statTagRect = new Rectangle(animatedBounds.Right + 1, animatedBounds.Center.Y, 1, 1);
                    spriteBatch.DrawSnapped(pixel, statTagRect, statColor * contentAlpha);
                }

                // --- Strikethrough Logic for Disabled State ---
                if (!IsEnabled)
                {
                    // Moved down 1 pixel (+1)
                    float lineY = animatedBounds.Center.Y + 1;
                    float startX = textStartX - 2;
                    float endX = textStartX + Math.Min(moveNameTextSize.Width, textAvailableWidth) + 2;
                    // Use fully opaque color for the strikethrough
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
                    // Use Square Outline for better visibility
                    spriteBatch.DrawStringSquareOutlinedSnapped(_moveFont, noManaText, noManaPos, _global.Palette_Red * contentAlpha, Color.Black * contentAlpha);
                }
            }
        }
    }
}
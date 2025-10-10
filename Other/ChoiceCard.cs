#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.UI
{
    public class ChoiceCard : Button
    {
        public object Data { get; }
        public string Title { get; private set; }
        public string Description { get; private set; }

        private readonly ChoiceType _cardType;
        private readonly List<(string Label, string Value)> _stats = new List<(string, string)>();
        private readonly List<string> _subTextLines = new List<string>();
        private readonly int _rarity;
        private readonly string _rarityText = "";
        private readonly int _elementId;
        private float _hoverTimer = 0f;
        private float _rarityAnimTimer = 0f;
        private static readonly Random _random = new Random();

        // Relic-specific data
        private readonly string? _abilityName;
        private readonly string? _relicImagePath;

        // Animation State
        private enum CardAnimationState { Hidden, AnimatingIn, AnimatingOut, Idle }
        private CardAnimationState _cardAnimState = CardAnimationState.Hidden;
        private float _cardAnimTimer = 0f;
        private const float CARD_ANIM_DURATION = 0.4f;
        private const float OUTRO_SHRINK_DURATION = 0.3f;
        private Vector2 _startPosition;
        private Vector2 _targetPosition;
        private bool _wasSelectedForOutro;
        private Action _onOutroComplete;
        private float _outroRotation;
        private bool _outroCompleteCallbackFired;


        private enum RarityAnimationState { Hidden, AnimatingIn, Idle }
        private RarityAnimationState _rarityAnimState = RarityAnimationState.Hidden;
        private float _rarityPopInTimer = 0f;
        private const float RARITY_ANIM_DURATION = 0.3f;
        private const float BORDER_ANIM_SPEED = 250f; // pixels per second
        private const int TRAIL_LENGTH = 250;
        private const float TRAIL_FADE_STRENGTH = 0.45f; // 0.0 (long fade) to 1.0 (instant fade)
        private const float AURA_PULSE_SPEED = 2f;
        private const float MIN_AURA_ALPHA = 0.1f;
        private const float MAX_AURA_ALPHA = 0.6f;

        // Idle Animation
        private float _bobTimer;
        private const float CARD_BOB_SPEED = 0.8f;
        private const float CARD_BOB_AMOUNT = 1.0f;
        private const float ICON_BOB_CYCLE_DURATION = 1.0f; // Total time for one up-and-down cycle
        private const float ICON_BOB_AMOUNT = 1.0f; // The pixel distance to bob
        private const float RARITY_SWAY_SPEED = 1.2f;
        private const float RARITY_SWAY_AMOUNT = 2.0f;

        public bool IsIntroAnimating => _cardAnimState == CardAnimationState.AnimatingIn;
        public bool IsAnimatingOut => _cardAnimState == CardAnimationState.AnimatingOut;

        public ChoiceCard(Rectangle bounds, MoveData move) : base(bounds, move.MoveName)
        {
            Data = move;
            _cardType = ChoiceType.Spell;
            Title = move.MoveName.ToUpper();
            Description = move.Description.ToUpper();
            _rarity = move.Rarity;
            _rarityText = GetRarityString(_rarity);
            _elementId = move.OffensiveElementIDs.FirstOrDefault();
            _bobTimer = (float)_random.NextDouble() * MathHelper.TwoPi;

            string power = move.Power > 0 ? move.Power.ToString() : "---";
            string acc = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
            _stats.Add(("POW:", power));
            _stats.Add(("ACC:", acc));
            _stats.Add(("MANA:", $"{move.ManaCost}%"));
        }

        public ChoiceCard(Rectangle bounds, AbilityData ability) : base(bounds, ability.RelicName)
        {
            Data = ability;
            _cardType = ChoiceType.Ability;
            Title = ability.RelicName.ToUpper();
            _abilityName = ability.AbilityName.ToUpper();
            _relicImagePath = ability.RelicImagePath;
            Description = ability.Description.ToUpper();
            _rarity = ability.Rarity;
            _rarityText = GetRarityString(_rarity);
            _elementId = 0; // Abilities have no element
            _bobTimer = (float)_random.NextDouble() * MathHelper.TwoPi;
        }

        public ChoiceCard(Rectangle bounds, ConsumableItemData item) : base(bounds, item.ItemName)
        {
            Data = item;
            _cardType = ChoiceType.Item;
            Title = item.ItemName.ToUpper();
            Description = item.Description.ToUpper();
            _rarity = 0; // Items can be considered "Common" for now
            _rarityText = GetRarityString(_rarity);
            _elementId = 0;
            _subTextLines.Add("CONSUMABLE ITEM");
            _bobTimer = (float)_random.NextDouble() * MathHelper.TwoPi;
        }

        public void StartIntroAnimation()
        {
            _cardAnimState = CardAnimationState.AnimatingIn;
            _cardAnimTimer = 0f;
            _targetPosition = Bounds.Location.ToVector2();
            _startPosition = new Vector2(_targetPosition.X, Global.VIRTUAL_HEIGHT);
        }

        public void StartOutroAnimation(bool wasSelected, Action onComplete = null)
        {
            _cardAnimState = CardAnimationState.AnimatingOut;
            _cardAnimTimer = 0f;
            _wasSelectedForOutro = wasSelected;
            _onOutroComplete = onComplete;
            _outroCompleteCallbackFired = false;
            if (wasSelected)
            {
                _outroRotation = (float)(_random.NextDouble() * 2 - 1) * MathHelper.PiOver4; // Random rotation between -45 and +45 degrees.
            }
        }

        public void StartRarityAnimation()
        {
            if (_rarityAnimState == RarityAnimationState.Hidden)
            {
                _rarityAnimState = RarityAnimationState.AnimatingIn;
                _rarityPopInTimer = 0f;
            }
        }

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _bobTimer += deltaTime;

            if (_cardAnimState == CardAnimationState.AnimatingIn)
            {
                _cardAnimTimer += deltaTime;
                if (_cardAnimTimer >= CARD_ANIM_DURATION)
                {
                    _cardAnimTimer = CARD_ANIM_DURATION;
                    _cardAnimState = CardAnimationState.Idle;
                }
            }
            else if (_cardAnimState == CardAnimationState.AnimatingOut)
            {
                _cardAnimTimer += deltaTime;
                if (_cardAnimTimer >= OUTRO_SHRINK_DURATION)
                {
                    _cardAnimTimer = OUTRO_SHRINK_DURATION;
                    // The card is now fully shrunk/faded. Fire the callback if it hasn't been already.
                    if (!_outroCompleteCallbackFired)
                    {
                        _onOutroComplete?.Invoke();
                        _outroCompleteCallbackFired = true;
                    }
                }
            }

            if (_rarityAnimState == RarityAnimationState.AnimatingIn)
            {
                _rarityPopInTimer += deltaTime;
                if (_rarityPopInTimer >= RARITY_ANIM_DURATION)
                {
                    _rarityPopInTimer = RARITY_ANIM_DURATION;
                    _rarityAnimState = RarityAnimationState.Idle;
                }
            }

            if (_cardAnimState == CardAnimationState.Idle)
            {
                base.Update(currentMouseState);
            }
            else
            {
                IsHovered = false;
            }

            if (IsHovered)
            {
                _hoverTimer += deltaTime;
            }
            _rarityAnimTimer += deltaTime;
        }

        private string GetRarityString(int rarity)
        {
            return rarity switch
            {
                0 => "COMMON",
                1 => "UNCOMMON",
                2 => "RARE",
                3 => "EPIC",
                4 => "MYTHIC",
                5 => "LEGENDARY",
                _ => ""
            };
        }

        private List<string> WrapText(string text, float maxLineWidth, BitmapFont font, bool isTitle = false)
        {
            var words = text.Split(' ');

            // Special wrapping rules for titles
            if (isTitle)
            {
                bool hasMoreThanOneWord = words.Length > 1;
                bool isLongEnoughToWrap = text.Length >= 12;

                if (!hasMoreThanOneWord || !isLongEnoughToWrap)
                {
                    return new List<string> { text };
                }
            }

            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                var testLine = currentLine.Length > 0 ? currentLine.ToString() + " " + word : word;
                if (font.MeasureString(testLine).Width > maxLineWidth)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(word);
                }
                else
                {
                    if (currentLine.Length > 0)
                        currentLine.Append(" ");
                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine.ToString());

            return lines;
        }

        private Vector2 GetPositionOnPerimeter(float distance, Rectangle bounds)
        {
            float perimeter = (bounds.Width + 1) * 2 + (bounds.Height + 1) * 2;
            // Ensure distance is always positive and wraps around
            distance = (distance % perimeter + perimeter) % perimeter;

            float topEdgeLength = bounds.Width + 1;
            float rightEdgeLength = bounds.Height + 1;
            float bottomEdgeLength = bounds.Width + 1;

            float x, y;

            if (distance < topEdgeLength) // Top edge
            {
                x = bounds.X - 1 + distance;
                y = bounds.Y - 1;
            }
            else if (distance < topEdgeLength + rightEdgeLength) // Right edge
            {
                x = bounds.Right;
                y = bounds.Y - 1 + (distance - topEdgeLength);
            }
            else if (distance < topEdgeLength + rightEdgeLength + bottomEdgeLength) // Bottom edge
            {
                x = bounds.Right - (distance - (topEdgeLength + rightEdgeLength));
                y = bounds.Bottom;
            }
            else // Left edge
            {
                x = bounds.X - 1;
                y = bounds.Bottom - (distance - (topEdgeLength + rightEdgeLength + bottomEdgeLength));
            }
            return new Vector2(x, y);
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? externalSwayOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            if (_cardAnimState == CardAnimationState.Hidden) return;

            Vector2 currentPosition = _targetPosition;
            float alpha = 1.0f;
            float whiteOverlayAlpha = 0f;
            Rectangle drawBounds;
            bool skipContent = false;
            float currentRotation = 0f;
            float scale = 1.0f;

            float cardBobOffsetY = 0;
            if (_cardAnimState == CardAnimationState.Idle)
            {
                cardBobOffsetY = MathF.Round(MathF.Sin(_bobTimer * CARD_BOB_SPEED) * CARD_BOB_AMOUNT);
            }

            if (_cardAnimState == CardAnimationState.AnimatingIn)
            {
                float progress = Math.Clamp(_cardAnimTimer / CARD_ANIM_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutBackSlight(progress);
                currentPosition = Vector2.Lerp(_startPosition, _targetPosition, easedProgress);
                drawBounds = new Rectangle((int)currentPosition.X, (int)currentPosition.Y, Bounds.Width, Bounds.Height);
            }
            else if (_cardAnimState == CardAnimationState.AnimatingOut)
            {
                if (_wasSelectedForOutro)
                {
                    // Selected card shrinks and fades to white.
                    float progress = Math.Clamp(_cardAnimTimer / OUTRO_SHRINK_DURATION, 0f, 1f);
                    float easedProgress = Easing.EaseInQuint(progress);
                    scale = 1.0f - easedProgress;

                    float newWidth = Bounds.Width * scale;
                    float newHeight = Bounds.Height * scale;

                    currentPosition = new Vector2(
                        _targetPosition.X + (Bounds.Width - newWidth) / 2f,
                        _targetPosition.Y + (Bounds.Height - newHeight) / 2f
                    );

                    drawBounds = new Rectangle((int)currentPosition.X, (int)currentPosition.Y, (int)newWidth, (int)newHeight);
                    whiteOverlayAlpha = Easing.EaseInQuad(progress);
                    alpha = 1.0f;
                    skipContent = true;
                    currentRotation = MathHelper.Lerp(0, _outroRotation, easedProgress);
                }
                else
                {
                    // Other cards fall down and fade out
                    float progress = Math.Clamp(_cardAnimTimer / OUTRO_SHRINK_DURATION, 0f, 1f);
                    float yOffset = Easing.EaseInQuad(progress) * 50f;
                    currentPosition = _targetPosition + new Vector2(0, yOffset);
                    alpha = 1.0f - Easing.EaseInQuad(progress);
                    drawBounds = new Rectangle((int)currentPosition.X, (int)currentPosition.Y, Bounds.Width, Bounds.Height);
                }
            }
            else
            {
                // For idle state, apply bobbing
                drawBounds = new Rectangle((int)currentPosition.X, (int)(currentPosition.Y + cardBobOffsetY), Bounds.Width, Bounds.Height);
            }

            var staticDrawBounds = new Rectangle((int)currentPosition.X, (int)currentPosition.Y, Bounds.Width, Bounds.Height);


            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // Determine colors based on state and data
            Color rarityColor = _global.RarityColors.GetValueOrDefault(_rarity, _global.Palette_Gray);
            Color titleColor;
            if (_cardType == ChoiceType.Spell)
            {
                titleColor = _global.ElementColors.GetValueOrDefault(_elementId, _global.Palette_BrightWhite);
            }
            else
            {
                titleColor = _global.Palette_BrightWhite; // Neutral color for non-spells
            }

            Color numericColor = (_rarity == 0) ? _global.Palette_Red : rarityColor;
            Color accentColor = _global.Palette_White;
            Color baseBorderColor = _cardType == ChoiceType.Spell ? titleColor : rarityColor;

            // Hover Pulse Effect for Border
            const float PULSE_SPEED = 5f;
            float pulse = isActivated ? (MathF.Sin(_hoverTimer * PULSE_SPEED) + 1f) / 2f : 0f; // Oscillates 0..1
            Color borderColor = Color.Lerp(baseBorderColor, Color.White, pulse);
            borderColor *= 0.5f; // Apply 50% opacity

            // --- Pulsing Aura for Rare and above (drawn first) ---
            if (_rarity >= 2)
            {
                float pulseValue = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * AURA_PULSE_SPEED) + 1f) / 2f; // 0 to 1
                float auraAlpha = MathHelper.Lerp(MIN_AURA_ALPHA, MAX_AURA_ALPHA, pulseValue);
                Color auraColor = rarityColor * auraAlpha;
                var outerBorderRect = new Rectangle(drawBounds.X - 1, drawBounds.Y - 1, drawBounds.Width + 2, drawBounds.Height + 2);
                spriteBatch.DrawSnapped(pixel, outerBorderRect, auraColor * alpha);
            }

            // Draw the semi-transparent border frame first
            spriteBatch.DrawSnapped(pixel, drawBounds, borderColor * alpha);

            // Draw the opaque inner background on top, creating the hollow border effect
            var innerBgRect = new Rectangle(drawBounds.X + 1, drawBounds.Y + 1, drawBounds.Width - 2, drawBounds.Height - 2);
            Color bgColor = isActivated ? _global.Palette_DarkGray : _global.Palette_Black;
            spriteBatch.DrawSnapped(pixel, innerBgRect, bgColor * alpha);

            // --- Conditionally draw content ---
            if (!skipContent)
            {
                // --- Animated Rarity Trail ---
                if (_rarity >= 3) // Zipping Trail for Epic and above
                {
                    // Determine speed based on rarity
                    float currentSpeed = BORDER_ANIM_SPEED;
                    if (_rarity == 3) currentSpeed *= 0.6f;      // Epic speed
                    else if (_rarity == 4) currentSpeed *= 0.8f; // Mythic speed
                                                                 // Legendary (_rarity == 5) uses the full BORDER_ANIM_SPEED

                    float perimeter = (drawBounds.Width + 1) * 2 + (drawBounds.Height + 1) * 2;
                    float headDistance1 = ((float)gameTime.TotalGameTime.TotalSeconds * currentSpeed) % perimeter;
                    for (int i = 0; i < TRAIL_LENGTH; i++)
                    {
                        // Common trail properties
                        float progress = (float)i / TRAIL_LENGTH;
                        float trailAlpha = 1.0f - MathF.Pow(progress, 1.0f - TRAIL_FADE_STRENGTH + 0.01f);
                        Color trailColor = Color.Lerp(rarityColor, Color.White, (float)i / (TRAIL_LENGTH * 2));
                        Color finalColor = trailColor * trailAlpha * alpha;

                        // Draw first trail segment
                        float currentDistance1 = headDistance1 - i;
                        Vector2 pos1 = GetPositionOnPerimeter(currentDistance1, drawBounds);
                        spriteBatch.DrawSnapped(pixel, pos1, finalColor);

                        // For Mythic and Legendary, draw a second, mirrored trail segment
                        if (_rarity >= 4)
                        {
                            float headDistance2 = (headDistance1 + perimeter / 2f);
                            float currentDistance2 = headDistance2 - i;
                            Vector2 pos2 = GetPositionOnPerimeter(currentDistance2, drawBounds);
                            spriteBatch.DrawSnapped(pixel, pos2, finalColor);
                        }
                    }
                }


                // Draw Rarity Text (OUTSIDE the card)
                if (!string.IsNullOrEmpty(_rarityText) && _rarityAnimState != RarityAnimationState.Hidden)
                {
                    float raritySwayOffsetX = 0;
                    if (_cardAnimState == CardAnimationState.Idle)
                    {
                        raritySwayOffsetX = MathF.Round(MathF.Sin(_bobTimer * RARITY_SWAY_SPEED) * RARITY_SWAY_AMOUNT);
                    }

                    float rarityY = drawBounds.Y - secondaryFont.LineHeight - 2;
                    float textScale = 1.0f;
                    if (_rarityAnimState == RarityAnimationState.AnimatingIn)
                    {
                        float progress = Math.Clamp(_rarityPopInTimer / RARITY_ANIM_DURATION, 0f, 1f);
                        textScale = Easing.EaseOutBack(progress);
                    }

                    if (_rarity >= 2 && _rarityAnimState == RarityAnimationState.Idle) // Animate for Rare and above
                    {
                        const float BOUNCE_DURATION = 0.1f;
                        const float CYCLE_DELAY = 0.5f;
                        const float WAVE_AMPLITUDE = 1f;

                        float totalCycleDuration = (_rarityText.Length * BOUNCE_DURATION) + CYCLE_DELAY;
                        float timeInCycle = _rarityAnimTimer % totalCycleDuration;

                        int activeCharIndex = -1;
                        if (timeInCycle < _rarityText.Length * BOUNCE_DURATION)
                        {
                            activeCharIndex = (int)(timeInCycle / BOUNCE_DURATION);
                        }

                        float totalWidthWithGaps = _rarityText.Sum(c => secondaryFont.MeasureString(c.ToString()).Width) + Math.Max(0, _rarityText.Length - 1);
                        float currentX = MathF.Round(drawBounds.Center.X - totalWidthWithGaps / 2f) + raritySwayOffsetX;

                        for (int i = 0; i < _rarityText.Length; i++)
                        {
                            char c = _rarityText[i];
                            string charStr = c.ToString();
                            float yOffset = 0;

                            if (i == activeCharIndex)
                            {
                                float bounceProgress = (timeInCycle % BOUNCE_DURATION) / BOUNCE_DURATION;
                                yOffset = -MathF.Round(MathF.Sin(bounceProgress * MathHelper.Pi) * WAVE_AMPLITUDE);
                            }

                            var charPos = new Vector2(currentX, rarityY + yOffset);
                            spriteBatch.DrawStringSnapped(secondaryFont, charStr, charPos, rarityColor * alpha);
                            currentX += secondaryFont.MeasureString(charStr).Width + 1; // Add 1px gap
                        }
                    }
                    else // Draw statically or with pop-in animation
                    {
                        var rarityTextSize = secondaryFont.MeasureString(_rarityText);
                        var rarityTextPos = new Vector2(drawBounds.Center.X + raritySwayOffsetX, rarityY + rarityTextSize.Height / 2f);
                        var origin = rarityTextSize / 2f;
                        spriteBatch.DrawStringSnapped(secondaryFont, _rarityText, rarityTextPos, rarityColor * alpha, 0f, origin, textScale, SpriteEffects.None, 0f);
                    }
                }

                // --- INTERNAL CONTENT ---
                const int paddingX = 8;
                const int topPadding = 4;
                float contentWidth = staticDrawBounds.Width - (paddingX * 2);
                float currentY;
                float iconBobOffsetY = 0;
                if (_cardAnimState == CardAnimationState.Idle)
                {
                    float cycleProgress = (_bobTimer % ICON_BOB_CYCLE_DURATION) / ICON_BOB_CYCLE_DURATION;
                    iconBobOffsetY = (cycleProgress < 0.5f) ? -ICON_BOB_AMOUNT : 0;
                }


                if (_cardType == ChoiceType.Ability)
                {
                    currentY = staticDrawBounds.Y + 5; // Base Y position for content, relative to the static card position.

                    const int relicIconSize = 32;
                    var relicIconPos = new Vector2(staticDrawBounds.Center.X - relicIconSize / 2f, currentY + iconBobOffsetY);
                    var relicTexture = spriteManager.GetRelicSprite(_relicImagePath);
                    var relicRect = new Rectangle((int)relicIconPos.X, (int)relicIconPos.Y, relicIconSize, relicIconSize);

                    if (isActivated)
                    {
                        var silhouetteTexture = spriteManager.GetRelicSpriteSilhouette(_relicImagePath);
                        var outlineColor = Color.White;
                        spriteBatch.DrawSnapped(silhouetteTexture, new Rectangle(relicRect.X - 1, relicRect.Y, relicRect.Width, relicRect.Height), outlineColor * alpha);
                        spriteBatch.DrawSnapped(silhouetteTexture, new Rectangle(relicRect.X + 1, relicRect.Y, relicRect.Width, relicRect.Height), outlineColor * alpha);
                        spriteBatch.DrawSnapped(silhouetteTexture, new Rectangle(relicRect.X, relicRect.Y - 1, relicRect.Width, relicRect.Height), outlineColor * alpha);
                        spriteBatch.DrawSnapped(silhouetteTexture, new Rectangle(relicRect.X, relicRect.Y + 1, relicRect.Width, relicRect.Height), outlineColor * alpha);
                    }

                    spriteBatch.DrawSnapped(relicTexture, relicRect, Color.White * alpha);

                    // All text content is now positioned relative to the static bounds
                    float titleBlockStartY = staticDrawBounds.Y + 5 + relicIconSize - 4;

                    const int titleLineCountReservation = 3;
                    float titleReservedHeight = defaultFont.LineHeight * titleLineCountReservation;

                    var titleLines = WrapText(Title, contentWidth, defaultFont, isTitle: true);
                    float actualTitleHeight = titleLines.Count * defaultFont.LineHeight;

                    float titleActualStartY = titleBlockStartY + (titleReservedHeight - actualTitleHeight) / 2f;

                    float titleCurrentY = titleActualStartY;
                    foreach (var line in titleLines)
                    {
                        var titleSize = defaultFont.MeasureString(line);
                        var titlePos = new Vector2(staticDrawBounds.Center.X - titleSize.Width / 2, titleCurrentY);
                        spriteBatch.DrawStringSnapped(defaultFont, line, titlePos, (isActivated ? _global.ButtonHoverColor : titleColor) * alpha);
                        titleCurrentY += defaultFont.LineHeight;
                    }

                    currentY = titleBlockStartY + titleReservedHeight + 6;

                    var dividerStart = new Vector2(staticDrawBounds.Left + paddingX, currentY);
                    var dividerEnd = new Vector2(staticDrawBounds.Right - paddingX, currentY);
                    spriteBatch.DrawLineSnapped(dividerStart, dividerEnd, _global.Palette_Gray * alpha);
                    currentY += 6;

                    if (!string.IsNullOrEmpty(_abilityName))
                    {
                        var abilityNameSize = secondaryFont.MeasureString(_abilityName);
                        var abilityNamePos = new Vector2(staticDrawBounds.Center.X - abilityNameSize.Width / 2, currentY);
                        spriteBatch.DrawStringSnapped(secondaryFont, _abilityName, abilityNamePos, _global.Palette_Gray * alpha);
                        currentY += secondaryFont.LineHeight + 4;
                    }
                }
                else // Spell or Item
                {
                    float titleAreaHeight = defaultFont.LineHeight * 2;
                    var titleLines = WrapText(Title, contentWidth, defaultFont, isTitle: true);
                    float totalTitleTextHeight = titleLines.Count * defaultFont.LineHeight;
                    float titleStartY = staticDrawBounds.Y + topPadding + (titleAreaHeight - totalTitleTextHeight) / 2f;
                    currentY = titleStartY;
                    foreach (var line in titleLines)
                    {
                        var titleSize = defaultFont.MeasureString(line);
                        var titlePos = new Vector2(staticDrawBounds.Center.X - titleSize.Width / 2, currentY);
                        spriteBatch.DrawStringSnapped(defaultFont, line, titlePos, (isActivated ? _global.ButtonHoverColor : titleColor) * alpha);
                        currentY += defaultFont.LineHeight;
                    }

                    float descriptionStartY_static = staticDrawBounds.Y + topPadding + titleAreaHeight + secondaryFont.LineHeight + 3;
                    if (_cardType == ChoiceType.Spell && spriteManager.ElementIconSourceRects.TryGetValue(_elementId, out var iconRect))
                    {
                        const int iconSize = 9;
                        const int iconDescGap = 4;
                        var iconPos = new Vector2(staticDrawBounds.Center.X - iconSize / 2f, descriptionStartY_static - iconDescGap - iconSize + 4 + iconBobOffsetY);
                        spriteBatch.DrawSnapped(spriteManager.ElementIconsSpriteSheet, iconPos, iconRect, Color.White * alpha);
                    }
                    currentY = staticDrawBounds.Y + topPadding + titleAreaHeight + secondaryFont.LineHeight + 3;
                }

                // Draw Description (Word Wrapped) - relative to static bounds
                var descLines = WrapText(Description, contentWidth, secondaryFont);
                foreach (var line in descLines)
                {
                    var lineSize = secondaryFont.MeasureString(line);
                    float currentX = staticDrawBounds.Center.X - lineSize.Width / 2;

                    var currentSegment = new StringBuilder();
                    bool? currentSegmentIsNumeric = null;
                    bool previousSegmentWasNumeric = false;

                    Action drawSegment = () => {
                        if (currentSegment.Length > 0)
                        {
                            string segmentStr = currentSegment.ToString();
                            Color segmentColor = (currentSegmentIsNumeric ?? false) ? numericColor : _global.Palette_White;

                            float xPos = currentX;
                            if (segmentStr == "." && previousSegmentWasNumeric)
                            {
                                xPos += 1;
                            }

                            spriteBatch.DrawStringSnapped(secondaryFont, segmentStr, new Vector2(xPos, currentY), segmentColor * alpha);
                            currentX += secondaryFont.MeasureString(segmentStr).Width;

                            if (segmentStr == "." && previousSegmentWasNumeric)
                            {
                                currentX += 1;
                            }

                            currentSegment.Clear();
                            previousSegmentWasNumeric = currentSegmentIsNumeric ?? false;
                        }
                    };

                    for (int i = 0; i < line.Length; i++)
                    {
                        char c = line[i];
                        bool isNumericChar = char.IsDigit(c) || c == '%';

                        if (currentSegmentIsNumeric == null)
                        {
                            currentSegmentIsNumeric = isNumericChar;
                        }

                        if (isNumericChar != currentSegmentIsNumeric)
                        {
                            drawSegment();
                            currentSegmentIsNumeric = isNumericChar;
                        }
                        currentSegment.Append(c);
                    }
                    drawSegment(); // Draw the final segment of the line

                    currentY += secondaryFont.LineHeight + 1;
                }

                // Draw Stats at the bottom - relative to static bounds
                float statsBlockHeight = _stats.Count * secondaryFont.LineHeight;
                float statsStartY = staticDrawBounds.Bottom - paddingX - statsBlockHeight;

                if (_cardType == ChoiceType.Spell)
                {
                    float maxLabelWidth = 0f;
                    float maxValueWidth = 0f;
                    foreach (var (label, value) in _stats)
                    {
                        maxLabelWidth = Math.Max(maxLabelWidth, secondaryFont.MeasureString(label).Width);
                        maxValueWidth = Math.Max(maxValueWidth, secondaryFont.MeasureString(value).Width);
                    }

                    float totalStatWidth = maxLabelWidth + maxValueWidth + 2;
                    float statBlockStartX = staticDrawBounds.X + (staticDrawBounds.Width - totalStatWidth) / 2;
                    float statLabelX = statBlockStartX;
                    float statValueX = statLabelX + maxLabelWidth + 2;

                    for (int i = 0; i < _stats.Count; i++)
                    {
                        var (label, value) = _stats[i];
                        var labelSize = secondaryFont.MeasureString(label);
                        var valueSize = secondaryFont.MeasureString(value);
                        var lineY = statsStartY + i * secondaryFont.LineHeight;

                        var labelPos = new Vector2(statLabelX + (maxLabelWidth - labelSize.Width), lineY);
                        spriteBatch.DrawStringSnapped(secondaryFont, label, labelPos, _global.Palette_Gray * alpha);

                        var valuePos = new Vector2(statValueX + (maxValueWidth - valueSize.Width), lineY);
                        if (value.Contains("%"))
                        {
                            valuePos.X += 5;
                        }
                        spriteBatch.DrawStringSnapped(secondaryFont, value, valuePos, _global.Palette_Gray * alpha);
                    }
                }
                else if (_cardType == ChoiceType.Item)
                {
                    if (_subTextLines.Any())
                    {
                        var subText = _subTextLines[0];
                        var subTextSize = secondaryFont.MeasureString(subText);
                        var subTextPos = new Vector2(staticDrawBounds.Center.X - subTextSize.Width / 2, statsStartY);
                        spriteBatch.DrawStringSnapped(secondaryFont, subText, subTextPos, _global.Palette_Gray * alpha);
                    }
                }

                // Draw Decorative Accents - relative to animated bounds
                const int accentSize = 3;
                const int inset = 2;
                spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Left + inset, drawBounds.Top + inset, accentSize, 1), accentColor * alpha);
                spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Left + inset, drawBounds.Top + inset, 1, accentSize), accentColor * alpha);
                spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Right - inset - accentSize, drawBounds.Top + inset, accentSize, 1), accentColor * alpha);
                spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Right - 1 - inset, drawBounds.Top + inset, 1, accentSize), accentColor * alpha);
                spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Left + inset, drawBounds.Bottom - 1 - inset, accentSize, 1), accentColor * alpha);
                spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Left + inset, drawBounds.Bottom - inset - accentSize, 1, accentSize), accentColor * alpha);
                spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Right - inset - accentSize, drawBounds.Bottom - 1 - inset, accentSize, 1), accentColor * alpha);
                spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Right - 1 - inset, drawBounds.Bottom - inset - accentSize, 1, accentSize), accentColor * alpha);
            }

            if (whiteOverlayAlpha > 0.01f)
            {
                Vector2 origin = new Vector2(0.5f); // Origin for a 1x1 texture is its center
                Vector2 scaleVec = new Vector2(drawBounds.Width, drawBounds.Height);
                spriteBatch.DrawSnapped(pixel, drawBounds.Center.ToVector2(), null, Color.White * whiteOverlayAlpha, currentRotation, origin, scaleVec, SpriteEffects.None, 0f);
            }
        }
    }
}
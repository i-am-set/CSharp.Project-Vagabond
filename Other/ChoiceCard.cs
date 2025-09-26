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

        // Animation State
        private enum CardAnimationState { Hidden, AnimatingIn, Idle }
        private CardAnimationState _cardAnimState = CardAnimationState.Hidden;
        private float _cardAnimTimer = 0f;
        private const float CARD_ANIM_DURATION = 0.4f;
        private Vector2 _startPosition;
        private Vector2 _targetPosition;

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

        public bool IsIntroAnimating => _cardAnimState == CardAnimationState.AnimatingIn;

        public ChoiceCard(Rectangle bounds, MoveData move) : base(bounds, move.MoveName)
        {
            Data = move;
            _cardType = ChoiceType.Spell;
            Title = move.MoveName.ToUpper();
            Description = move.Description.ToUpper();
            _rarity = move.Rarity;
            _rarityText = GetRarityString(_rarity);
            _elementId = move.OffensiveElementIDs.FirstOrDefault();

            string power = move.Power > 0 ? move.Power.ToString() : "---";
            string acc = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
            _stats.Add(("POW:", power));
            _stats.Add(("ACC:", acc));
            _stats.Add(("MANA:", $"{move.ManaCost}%"));
        }

        public ChoiceCard(Rectangle bounds, AbilityData ability) : base(bounds, ability.AbilityName)
        {
            Data = ability;
            _cardType = ChoiceType.Ability;
            Title = ability.AbilityName.ToUpper();
            Description = ability.Description.ToUpper();
            _rarity = 0; // Abilities can be considered "Common" for now
            _rarityText = GetRarityString(_rarity);
            _elementId = 0;
            _subTextLines.Add("PASSIVE ABILITY");
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
        }

        public void StartIntroAnimation()
        {
            _cardAnimState = CardAnimationState.AnimatingIn;
            _cardAnimTimer = 0f;
            _targetPosition = Bounds.Location.ToVector2();
            _startPosition = new Vector2(_targetPosition.X, Global.VIRTUAL_HEIGHT);
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

            if (_cardAnimState == CardAnimationState.AnimatingIn)
            {
                _cardAnimTimer += deltaTime;
                if (_cardAnimTimer >= CARD_ANIM_DURATION)
                {
                    _cardAnimTimer = CARD_ANIM_DURATION;
                    _cardAnimState = CardAnimationState.Idle;
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

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? externalSwayOffset = null)
        {
            if (_cardAnimState == CardAnimationState.Hidden) return;

            Vector2 currentPosition = _targetPosition;
            if (_cardAnimState == CardAnimationState.AnimatingIn)
            {
                float progress = Math.Clamp(_cardAnimTimer / CARD_ANIM_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutBackSlight(progress);
                currentPosition = Vector2.Lerp(_startPosition, _targetPosition, easedProgress);
            }

            var drawBounds = new Rectangle((int)currentPosition.X, (int)currentPosition.Y, Bounds.Width, Bounds.Height);


            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // Determine colors based on state and data
            Color rarityColor = _global.RarityColors.GetValueOrDefault(_rarity, _global.Palette_Gray);
            Color titleColor = _global.ElementColors.GetValueOrDefault(_elementId, _global.Palette_BrightWhite);
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
                float alpha = MathHelper.Lerp(MIN_AURA_ALPHA, MAX_AURA_ALPHA, pulseValue);
                Color auraColor = rarityColor * alpha;
                var outerBorderRect = new Rectangle(drawBounds.X - 1, drawBounds.Y - 1, drawBounds.Width + 2, drawBounds.Height + 2);
                spriteBatch.DrawSnapped(pixel, outerBorderRect, auraColor);
            }

            // Draw the semi-transparent border frame first
            spriteBatch.DrawSnapped(pixel, drawBounds, borderColor);

            // Draw the opaque inner background on top, creating the hollow border effect
            var innerBgRect = new Rectangle(drawBounds.X + 1, drawBounds.Y + 1, drawBounds.Width - 2, drawBounds.Height - 2);
            Color bgColor = isActivated ? _global.Palette_DarkGray : _global.Palette_Black;
            spriteBatch.DrawSnapped(pixel, innerBgRect, bgColor);

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
                    float alpha = 1.0f - MathF.Pow(progress, 1.0f - TRAIL_FADE_STRENGTH + 0.01f);
                    Color trailColor = Color.Lerp(rarityColor, Color.White, (float)i / (TRAIL_LENGTH * 2));
                    Color finalColor = trailColor * alpha;

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
                float rarityY = drawBounds.Y - secondaryFont.LineHeight - 2;
                float scale = 1.0f;
                if (_rarityAnimState == RarityAnimationState.AnimatingIn)
                {
                    float progress = Math.Clamp(_rarityPopInTimer / RARITY_ANIM_DURATION, 0f, 1f);
                    scale = Easing.EaseOutBack(progress);
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
                    float currentX = MathF.Round(drawBounds.Center.X - totalWidthWithGaps / 2f);

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
                        spriteBatch.DrawStringSnapped(secondaryFont, charStr, charPos, rarityColor);
                        currentX += secondaryFont.MeasureString(charStr).Width + 1; // Add 1px gap
                    }
                }
                else // Draw statically or with pop-in animation
                {
                    var rarityTextSize = secondaryFont.MeasureString(_rarityText);
                    var rarityTextPos = new Vector2(drawBounds.Center.X, rarityY + rarityTextSize.Height / 2f);
                    var origin = rarityTextSize / 2f;
                    spriteBatch.DrawStringSnapped(secondaryFont, _rarityText, rarityTextPos, rarityColor, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }

            // --- INTERNAL CONTENT ---
            const int paddingX = 8;
            const int topPadding = 4;
            float contentWidth = drawBounds.Width - (paddingX * 2);

            // --- TITLE AREA ---
            float titleAreaHeight = defaultFont.LineHeight * 2;
            var titleLines = WrapText(Title, contentWidth, defaultFont, isTitle: true);
            float totalTitleTextHeight = titleLines.Count * defaultFont.LineHeight;
            float titleStartY = drawBounds.Y + topPadding + (titleAreaHeight - totalTitleTextHeight) / 2f; // Vertically center the text block

            float currentY = titleStartY;
            foreach (var line in titleLines)
            {
                var titleSize = defaultFont.MeasureString(line);
                var titlePos = new Vector2(drawBounds.Center.X - titleSize.Width / 2, currentY);
                spriteBatch.DrawStringSnapped(defaultFont, line, titlePos, isActivated ? _global.ButtonHoverColor : titleColor);
                currentY += defaultFont.LineHeight;
            }

            // --- DESCRIPTION AREA ---
            float descriptionStartY = drawBounds.Y + topPadding + titleAreaHeight + secondaryFont.LineHeight + 3;

            // Draw Element Icon for Spells, positioned a fixed distance above the description start line.
            if (_cardType == ChoiceType.Spell && spriteManager.ElementIconSourceRects.TryGetValue(_elementId, out var iconRect))
            {
                const int iconSize = 9;
                const int iconDescGap = 4;
                var iconPos = new Vector2(drawBounds.Center.X - iconSize / 2f, descriptionStartY - iconDescGap - iconSize + 4);
                spriteBatch.DrawSnapped(spriteManager.ElementIconsSpriteSheet, iconPos, iconRect, Color.White);
            }

            currentY = descriptionStartY;

            // Draw Description (Word Wrapped)
            var descLines = WrapText(Description, contentWidth, secondaryFont);
            foreach (var line in descLines)
            {
                var descSize = secondaryFont.MeasureString(line);
                var descPos = new Vector2(drawBounds.Center.X - descSize.Width / 2, currentY);
                spriteBatch.DrawStringSnapped(secondaryFont, line, descPos, _global.Palette_White);
                currentY += secondaryFont.LineHeight;
            }

            // Draw Stats at the bottom
            float statsBlockHeight = _stats.Count * secondaryFont.LineHeight;
            float statsStartY = drawBounds.Bottom - paddingX - statsBlockHeight;

            if (_cardType == ChoiceType.Spell)
            {
                // Calculate max widths for alignment
                float maxLabelWidth = 0f;
                float maxValueWidth = 0f;
                foreach (var (label, value) in _stats)
                {
                    maxLabelWidth = Math.Max(maxLabelWidth, secondaryFont.MeasureString(label).Width);
                    maxValueWidth = Math.Max(maxValueWidth, secondaryFont.MeasureString(value).Width);
                }

                float totalStatWidth = maxLabelWidth + maxValueWidth + 2; // 2px gap
                float statBlockStartX = drawBounds.X + (drawBounds.Width - totalStatWidth) / 2;
                float statLabelX = statBlockStartX;
                float statValueX = statLabelX + maxLabelWidth + 2;

                for (int i = 0; i < _stats.Count; i++)
                {
                    var (label, value) = _stats[i];
                    var labelSize = secondaryFont.MeasureString(label);
                    var valueSize = secondaryFont.MeasureString(value);
                    var lineY = statsStartY + i * secondaryFont.LineHeight;

                    // Draw label (right-aligned)
                    var labelPos = new Vector2(statLabelX + (maxLabelWidth - labelSize.Width), lineY);
                    spriteBatch.DrawStringSnapped(secondaryFont, label, labelPos, _global.Palette_Gray);

                    // Draw value (right-aligned)
                    var valuePos = new Vector2(statValueX + (maxValueWidth - valueSize.Width), lineY);
                    if (value.Contains("%"))
                    {
                        valuePos.X += 5;
                    }
                    spriteBatch.DrawStringSnapped(secondaryFont, value, valuePos, _global.Palette_Gray);
                }
            }
            else
            {
                // Centered subtext for abilities/items
                if (_subTextLines.Any())
                {
                    var subText = _subTextLines[0];
                    var subTextSize = secondaryFont.MeasureString(subText);
                    var subTextPos = new Vector2(drawBounds.Center.X - subTextSize.Width / 2, statsStartY);
                    spriteBatch.DrawStringSnapped(secondaryFont, subText, subTextPos, _global.Palette_Gray);
                }
            }

            // Draw Decorative Accents
            const int accentSize = 3;
            const int inset = 2;
            // Top-Left
            spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Left + inset, drawBounds.Top + inset, accentSize, 1), accentColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Left + inset, drawBounds.Top + inset, 1, accentSize), accentColor);

            // Top-Right
            spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Right - inset - accentSize, drawBounds.Top + inset, accentSize, 1), accentColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Right - 1 - inset, drawBounds.Top + inset, 1, accentSize), accentColor);

            // Bottom-Left
            spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Left + inset, drawBounds.Bottom - 1 - inset, accentSize, 1), accentColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Left + inset, drawBounds.Bottom - inset - accentSize, 1, accentSize), accentColor);

            // Bottom-Right
            spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Right - inset - accentSize, drawBounds.Bottom - 1 - inset, accentSize, 1), accentColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(drawBounds.Right - 1 - inset, drawBounds.Bottom - inset - accentSize, 1, accentSize), accentColor);
        }
    }
}
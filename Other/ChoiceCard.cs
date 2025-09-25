using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
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

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            base.Update(currentMouseState);
            if (IsHovered)
            {
                _hoverTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
            _rarityAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        private string GetRarityString(int rarity)
        {
            return rarity switch
            {
                0 => "COMMON",
                1 => "UNCOMMON",
                2 => "RARE",
                3 => "EPIC",
                4 => "LEGENDARY",
                5 => "MYTHIC",
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

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? externalSwayOffset = null)
        {
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


            // Draw Border and Background
            spriteBatch.DrawSnapped(pixel, Bounds, isActivated ? _global.Palette_DarkGray : _global.Palette_Black);
            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Left, Bounds.Top, Bounds.Width, 1), borderColor); // Top
            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Left, Bounds.Bottom - 1, Bounds.Width, 1), borderColor); // Bottom
            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Left, Bounds.Top, 1, Bounds.Height), borderColor); // Left
            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Right - 1, Bounds.Top, 1, Bounds.Height), borderColor); // Right

            const int paddingX = 8;
            const int topPadding = 8;
            float contentWidth = Bounds.Width - (paddingX * 2);
            float currentY = Bounds.Y + topPadding;

            // Draw Rarity Text
            if (!string.IsNullOrEmpty(_rarityText))
            {
                if (_rarity >= 2) // Animate for Rare and above
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
                    float currentX = MathF.Round(Bounds.Center.X - totalWidthWithGaps / 2f);

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

                        var charPos = new Vector2(currentX, currentY + yOffset);
                        spriteBatch.DrawStringSnapped(secondaryFont, charStr, charPos, rarityColor);
                        currentX += secondaryFont.MeasureString(charStr).Width + 1; // Add 1px gap
                    }
                }
                else // Draw statically for Common/Uncommon
                {
                    var rarityTextSize = secondaryFont.MeasureString(_rarityText);
                    var rarityTextPos = new Vector2(Bounds.Center.X - rarityTextSize.Width / 2, currentY);
                    spriteBatch.DrawStringSnapped(secondaryFont, _rarityText, rarityTextPos, rarityColor);
                }
                currentY += secondaryFont.LineHeight + 2;
            }

            // Draw Title (Word Wrapped)
            var titleLines = WrapText(Title, contentWidth, defaultFont, isTitle: true);
            foreach (var line in titleLines)
            {
                var titleSize = defaultFont.MeasureString(line);
                var titlePos = new Vector2(Bounds.Center.X - titleSize.Width / 2, currentY);
                spriteBatch.DrawStringSnapped(defaultFont, line, titlePos, isActivated ? _global.ButtonHoverColor : titleColor);
                currentY += defaultFont.LineHeight;
            }

            float titleBottomY = currentY;

            // Calculate a fixed starting Y position for the description block.
            float titleBlockHeight = (string.IsNullOrEmpty(_rarityText) ? 0 : secondaryFont.LineHeight + 2) + (defaultFont.LineHeight * 2);
            float descriptionStartY = Bounds.Y + topPadding + titleBlockHeight + 10;

            // Draw Element Icon for Spells
            if (_cardType == ChoiceType.Spell && spriteManager.ElementIconSourceRects.TryGetValue(_elementId, out var iconRect))
            {
                const int iconSize = 9;
                float gap = descriptionStartY - titleBottomY;
                var iconPos = new Vector2(Bounds.Center.X - iconSize / 2f, titleBottomY + (gap - iconSize) / 2f);
                spriteBatch.DrawSnapped(spriteManager.ElementIconsSpriteSheet, iconPos, iconRect, Color.White);
            }

            currentY = descriptionStartY;

            // Draw Description (Word Wrapped)
            var descLines = WrapText(Description, contentWidth, secondaryFont);
            foreach (var line in descLines)
            {
                var descSize = secondaryFont.MeasureString(line);
                var descPos = new Vector2(Bounds.Center.X - descSize.Width / 2, currentY);
                spriteBatch.DrawStringSnapped(secondaryFont, line, descPos, _global.Palette_White);
                currentY += secondaryFont.LineHeight;
            }

            // Draw Stats at the bottom
            float statsBlockHeight = _stats.Count * secondaryFont.LineHeight;
            float statsStartY = Bounds.Bottom - paddingX - statsBlockHeight;

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
                float statBlockStartX = Bounds.X + (Bounds.Width - totalStatWidth) / 2;
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
                    var subTextPos = new Vector2(Bounds.Center.X - subTextSize.Width / 2, statsStartY);
                    spriteBatch.DrawStringSnapped(secondaryFont, subText, subTextPos, _global.Palette_Gray);
                }
            }

            // Draw Decorative Accents
            const int accentSize = 3;
            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Left + 1, Bounds.Top + 1, accentSize, 1), accentColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Left + 1, Bounds.Top + 1, 1, accentSize), accentColor);

            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Right - 2 - accentSize, Bounds.Top + 1, accentSize, 1), accentColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Right - 2, Bounds.Top + 1, 1, accentSize), accentColor);

            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Left + 1, Bounds.Bottom - 2, accentSize, 1), accentColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Left + 1, Bounds.Bottom - 2 - accentSize, 1, accentSize), accentColor);

            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Right - 2 - accentSize, Bounds.Bottom - 2, accentSize, 1), accentColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(Bounds.Right - 2, Bounds.Bottom - 2 - accentSize, 1, accentSize), accentColor);
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

        public ChoiceCard(Rectangle bounds, MoveData move) : base(bounds, move.MoveName)
        {
            Data = move;
            _cardType = ChoiceType.Spell;
            Title = move.MoveName.ToUpper();
            Description = move.Description.ToUpper();

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
            _subTextLines.Add("PASSIVE ABILITY");
        }

        public ChoiceCard(Rectangle bounds, ConsumableItemData item) : base(bounds, item.ItemName)
        {
            Data = item;
            _cardType = ChoiceType.Item;
            Title = item.ItemName.ToUpper();
            Description = item.Description.ToUpper();
            _subTextLines.Add("CONSUMABLE ITEM");
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
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // Draw Border and Background
            spriteBatch.DrawSnapped(pixel, Bounds, isActivated ? _global.Palette_DarkGray : _global.Palette_Black);
            spriteBatch.DrawLineSnapped(new Vector2(Bounds.Left, Bounds.Top), new Vector2(Bounds.Right, Bounds.Top), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(Bounds.Left, Bounds.Bottom), new Vector2(Bounds.Right, Bounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(Bounds.Left, Bounds.Top), new Vector2(Bounds.Left, Bounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(Bounds.Right, Bounds.Top), new Vector2(Bounds.Right, Bounds.Bottom), _global.Palette_White);

            const int paddingX = 8;
            const int titleStartY = 15;
            float contentWidth = Bounds.Width - (paddingX * 2);
            float currentY = Bounds.Y + titleStartY;

            // Draw Title (Word Wrapped)
            var titleLines = WrapText(Title, contentWidth, defaultFont, isTitle: true);
            foreach (var line in titleLines)
            {
                var titleSize = defaultFont.MeasureString(line);
                var titlePos = new Vector2(Bounds.Center.X - titleSize.Width / 2, currentY);
                spriteBatch.DrawStringSnapped(defaultFont, line, titlePos, isActivated ? _global.ButtonHoverColor : _global.Palette_BrightWhite);
                currentY += defaultFont.LineHeight;
            }

            // Calculate a fixed starting Y position for the description block.
            // This is based on the maximum possible height of a 2-line title.
            float descriptionStartY = Bounds.Y + titleStartY + (defaultFont.LineHeight * 2) + 10;
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
        }
    }
}
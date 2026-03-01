using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class MoveTooltipRenderer
    {
        private readonly Global _global;
        private readonly Texture2D _pixel;
        private readonly Core _core;

        // Shared Constants
        public const int WIDTH = 140;
        public const int HEIGHT = 42;

        public MoveTooltipRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _pixel = ServiceLocator.Get<Texture2D>();
            _core = ServiceLocator.Get<Core>();
        }

        /// <summary>
        /// Draws the tooltip at a specific, fixed position (Used by ActionMenu).
        /// </summary>
        public void DrawFixed(SpriteBatch sb, Vector2 position, MoveData move)
        {
            DrawContainerAndContent(sb, position, move);
        }

        /// <summary>
        /// Calculates position based on target center, clamps to screen, applies floating animation, 
        /// and draws the tooltip (Used by SplitMapHud).
        /// </summary>
        public void DrawFloating(SpriteBatch sb, GameTime gameTime, Rectangle targetRect, int cardCenterX, MoveData move)
        {
            // 1. Calculate ideal centered X
            float x = cardCenterX - (WIDTH / 2f);

            // 2. Clamp to screen edges with 2px margin
            float minX = 2f;
            float maxX = Global.VIRTUAL_WIDTH - WIDTH - 2f;

            if (x < minX) x = minX;
            if (x > maxX) x = maxX;

            // 3. Position above the target rect (Removed floating animation)
            float y = targetRect.Top - HEIGHT - 4;

            DrawContainerAndContent(sb, new Vector2(x, y), move);
        }

        private void DrawContainerAndContent(SpriteBatch sb, Vector2 pos, MoveData move)
        {
            Vector2 size = new Vector2(WIDTH, HEIGHT);

            // --- Draw Outline ---
            DrawBeveledBackground(sb, pos - new Vector2(1, 1), size + new Vector2(2, 2), _global.Palette_DarkestPale);

            // --- Draw Backgrounds ---
            DrawBeveledBackground(sb, pos, size, _global.Palette_Black);

            // Inner description area background (bottom part)
            Vector2 descPos = new Vector2(pos.X + 1, pos.Y + size.Y - 1 - 18);
            Vector2 descSize = new Vector2(size.X - 2, 18);
            DrawBeveledBackground(sb, descPos, descSize, _global.Palette_Black);

            // --- Draw Content ---
            DrawTextContent(sb, pos, move);
        }

        private void DrawTextContent(SpriteBatch sb, Vector2 boxPos, MoveData move)
        {
            var secondaryFont = _core.SecondaryFont;
            var tertiaryFont = _core.TertiaryFont;

            float startY = boxPos.Y + 1;
            float currentY = startY;

            // Layout Tuning
            int rowSpacing = 8;
            int pairSpacing = 5;
            float labelValueGap = tertiaryFont.MeasureString(" ").Width;

            string name = move.MoveName.ToUpper();

            // Blank Description
            string desc = move.Description;
            Color initialColor = _global.Palette_Sun;

            if (string.IsNullOrWhiteSpace(desc))
            {
                desc = "BLANK";
                initialColor = _global.Palette_DarkestPale;
            }

            string powTxt = move.Power > 0 ? move.Power.ToString() : "--";
            string accTxt = move.Accuracy > 0 ? $"{move.Accuracy}%" : "--";
            string useTxt = GetStatShortName(move.OffensiveStat);

            // --- 1. Stats Row (Centered) ---
            float MeasurePair(string label, string val)
            {
                return tertiaryFont.MeasureString(label).Width + labelValueGap + secondaryFont.MeasureString(val).Width;
            }

            float w1 = MeasurePair("POW", powTxt);
            float w2 = MeasurePair("ACC", accTxt);
            float w3 = MeasurePair("USE", useTxt);
            float totalStatsWidth = w1 + pairSpacing + w2 + pairSpacing + w3;

            float statsCurrentX = boxPos.X + (WIDTH - totalStatsWidth) / 2f;

            void DrawPair(string label, string val)
            {
                sb.DrawStringSnapped(tertiaryFont, label, new Vector2(statsCurrentX, currentY + 1), _global.Palette_DarkPale);
                statsCurrentX += tertiaryFont.MeasureString(label).Width + labelValueGap;

                sb.DrawStringSnapped(secondaryFont, val, new Vector2(statsCurrentX, currentY), _global.Palette_LightPale);
                statsCurrentX += secondaryFont.MeasureString(val).Width + pairSpacing;
            }

            DrawPair("POW", powTxt);
            DrawPair("ACC", accTxt);
            DrawPair("USE", useTxt);

            // --- 1.5. Target Type Row (Centered) ---
            currentY += tertiaryFont.LineHeight + 5;
            string targetText = GetTargetDisplayName(move.Target);
            float targetX = boxPos.X + (WIDTH - tertiaryFont.MeasureString(targetText).Width) / 2f;
            sb.DrawStringSnapped(tertiaryFont, targetText, new Vector2(targetX, currentY), _global.Palette_DarkPale);

            // --- 2. Name (Centered) ---
            currentY += (rowSpacing - 1);
            Vector2 nameSize = secondaryFont.MeasureString(name);
            float centeredNameX = boxPos.X + (WIDTH - nameSize.X) / 2f;
            sb.DrawStringSnapped(secondaryFont, name, new Vector2(centeredNameX, currentY), _global.Palette_Sun);

            // --- 3. Description (Rich Text, Vertically Centered) ---
            currentY += rowSpacing;
            float maxWidth = WIDTH - 8;
            float startX = boxPos.X + 4;

            const float FIXED_SPACE_WIDTH = 3f;

            // Parse text into lines
            var lines = new List<List<(string Text, Color Color)>>();
            var currentLine = new List<(string Text, Color Color)>();
            float currentLineWidth = 0f;
            lines.Add(currentLine);

            var parts = Regex.Split(desc, @"(\[.*?\]|\s+)");
            Color currentColor = initialColor;

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string tag = part.Substring(1, part.Length - 2);
                    if (tag == "/" || tag.Equals("default", StringComparison.OrdinalIgnoreCase))
                        currentColor = _global.Palette_LightPale;
                    else
                        currentColor = _global.GetNarrationColor(tag);
                }
                else
                {
                    string textPart = part.ToUpper();

                    if (string.IsNullOrWhiteSpace(textPart))
                    {
                        if (textPart.Contains("\n"))
                        {
                            lines.Add(new List<(string, Color)>());
                            currentLine = lines.Last();
                            currentLineWidth = 0;
                        }
                        else
                        {
                            if (currentLineWidth + FIXED_SPACE_WIDTH > maxWidth)
                            {
                                lines.Add(new List<(string, Color)>());
                                currentLine = lines.Last();
                                currentLineWidth = 0;
                            }
                            else
                            {
                                currentLine.Add((" ", currentColor));
                                currentLineWidth += FIXED_SPACE_WIDTH;
                            }
                        }
                        continue;
                    }

                    Vector2 size = tertiaryFont.MeasureString(textPart);

                    if (currentLineWidth + size.X > maxWidth)
                    {
                        lines.Add(new List<(string, Color)>());
                        currentLine = lines.Last();
                        currentLineWidth = 0;
                    }

                    currentLine.Add((textPart, currentColor));
                    currentLineWidth += size.X;
                }
            }

            // Calculate Vertical Center with Gaps
            int descLineHeight = tertiaryFont.LineHeight + 1;
            const int DESC_ROW_GAP = 1;

            // Total height = (lines * height) + (gaps between lines)
            int totalDescHeight = (lines.Count * descLineHeight) + (Math.Max(0, lines.Count - 1) * DESC_ROW_GAP);

            float availableHeight = (boxPos.Y + HEIGHT) - currentY - 2;
            float startDrawY = currentY + (availableHeight - totalDescHeight) / 2;

            if (startDrawY < currentY) startDrawY = currentY;

            foreach (var line in lines)
            {
                if (startDrawY + descLineHeight > (boxPos.Y + HEIGHT)) break;

                float lineWidth = 0;
                foreach (var item in line)
                {
                    if (item.Text == " ") lineWidth += FIXED_SPACE_WIDTH;
                    else lineWidth += tertiaryFont.MeasureString(item.Text).Width;
                }

                float lineX = startX + (maxWidth - lineWidth) / 2f;

                foreach (var item in line)
                {
                    if (item.Text == " ")
                    {
                        lineX += FIXED_SPACE_WIDTH;
                    }
                    else
                    {
                        sb.DrawStringSnapped(tertiaryFont, item.Text, new Vector2(lineX, startDrawY), item.Color);
                        lineX += tertiaryFont.MeasureString(item.Text).Width;
                    }
                }

                startDrawY += descLineHeight + DESC_ROW_GAP;
            }
        }

        private void DrawBeveledBackground(SpriteBatch sb, Vector2 pos, Vector2 size, Color color)
        {
            sb.DrawSnapped(_pixel, new Vector2(pos.X + 1, pos.Y), new Rectangle(0, 0, (int)size.X - 2, 1), color);
            sb.DrawSnapped(_pixel, new Vector2(pos.X + 1, pos.Y + size.Y - 1), new Rectangle(0, 0, (int)size.X - 2, 1), color);
            sb.DrawSnapped(_pixel, new Vector2(pos.X, pos.Y + 1), new Rectangle(0, 0, (int)size.X, (int)size.Y - 2), color);
        }

        private string GetStatShortName(OffensiveStatType stat)
        {
            return stat switch
            {
                OffensiveStatType.Strength => "STR",
                OffensiveStatType.Intelligence => "INT",
                OffensiveStatType.Tenacity => "TEN",
                OffensiveStatType.Agility => "AGI",
                _ => "---"
            };
        }

        private string GetTargetDisplayName(TargetType target)
        {
            return target switch
            {
                TargetType.SingleAll => "SINGLE ALL",
                TargetType.SingleTeam => "SINGLE TEAM",
                TargetType.RandomBoth => "RANDOM BOTH",
                TargetType.RandomEvery => "RANDOM EVERY",
                TargetType.RandomAll => "RANDOM ALL",
                _ => target.ToString().ToUpper()
            };
        }
    }
}

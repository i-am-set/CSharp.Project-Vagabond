using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Dice;
using ProjectVagabond.Items;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Systems;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace ProjectVagabond.UI
{
    public class ItemTooltipRenderer
    {
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly Core _core;

        private const int TOOLTIP_WIDTH = 120;
        private const int SCREEN_EDGE_MARGIN = 6;
        private const int SPACE_WIDTH = 5;

        public ItemTooltipRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _core = ServiceLocator.Get<Core>();
        }

        public void DrawTooltip(SpriteBatch spriteBatch, object itemData, Vector2 anchorPosition, GameTime gameTime)
        {
            if (itemData == null) return;

            var font = ServiceLocator.Get<BitmapFont>();
            var secondaryFont = _core.SecondaryFont;

            // 1. Calculate Required Height
            float contentHeight = MeasureContentHeight(font, secondaryFont, itemData);
            int panelHeight = (int)Math.Ceiling(contentHeight) + 8; // Add padding

            // 2. Determine Y Position (Centered on anchor, shifted up if spell)
            float panelY = anchorPosition.Y - panelHeight / 2;
            if (itemData is MoveData) panelY -= 16;

            // 3. Create Rectangle
            int panelWidth = TOOLTIP_WIDTH;
            var infoPanelArea = new Rectangle(
                (int)(anchorPosition.X - panelWidth / 2),
                (int)panelY,
                panelWidth,
                panelHeight
            );

            // 4. Clamp to Screen
            int screenTop = SCREEN_EDGE_MARGIN;
            int screenBottom = Global.VIRTUAL_HEIGHT - SCREEN_EDGE_MARGIN;
            int screenLeft = SCREEN_EDGE_MARGIN;
            int screenRight = Global.VIRTUAL_WIDTH - SCREEN_EDGE_MARGIN;

            if (infoPanelArea.Top < screenTop) infoPanelArea.Y = screenTop;
            if (infoPanelArea.Bottom > screenBottom) infoPanelArea.Y = screenBottom - infoPanelArea.Height;
            if (infoPanelArea.Left < screenLeft) infoPanelArea.X = screenLeft;
            if (infoPanelArea.Right > screenRight) infoPanelArea.X = screenRight - infoPanelArea.Width;

            // 5. Draw Background
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.DrawSnapped(pixel, infoPanelArea, _global.Palette_DarkestGray);
            DrawRectangleBorder(spriteBatch, pixel, infoPanelArea, 1, Color.White);

            // 6. Draw Content
            DrawInfoPanelContent(spriteBatch, itemData, infoPanelArea, font, secondaryFont, gameTime);
        }

        public void DrawInfoPanelContent(SpriteBatch spriteBatch, object itemData, Rectangle bounds, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime)
        {
            if (itemData is MoveData moveData)
            {
                string iconPath = $"Sprites/Spells/{moveData.MoveID}";
                int elementId = moveData.OffensiveElementIDs.FirstOrDefault();
                string? fallbackPath = null;
                if (BattleDataCache.Elements.TryGetValue(elementId, out var elementDef))
                {
                    string elName = elementDef.ElementName.ToLowerInvariant();
                    if (elName == "---") elName = "neutral";
                    fallbackPath = $"Sprites/Spells/default_{elName}";
                }

                var iconTexture = _spriteManager.GetItemSprite(iconPath, fallbackPath);
                var iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath, fallbackPath);
                Rectangle? sourceRect = _spriteManager.GetAnimatedIconSourceRect(iconTexture, gameTime);

                DrawSpellInfoPanel(spriteBatch, font, secondaryFont, moveData, iconTexture, iconSilhouette, sourceRect, null, Rectangle.Empty, Vector2.Zero, false, bounds);
            }
            else
            {
                string name = "";
                string description = "";
                string iconPath = "";
                Dictionary<string, int> stats = new Dictionary<string, int>();

                if (itemData is WeaponData w) { name = w.WeaponName; description = w.Description; iconPath = $"Sprites/Items/Weapons/{w.WeaponID}"; stats = w.StatModifiers; }
                else if (itemData is ArmorData a) { name = a.ArmorName; description = a.Description; iconPath = $"Sprites/Items/Armor/{a.ArmorID}"; stats = a.StatModifiers; }
                else if (itemData is RelicData r) { name = r.RelicName; description = r.Description; iconPath = $"Sprites/Items/Relics/{r.RelicID}"; stats = r.StatModifiers; }
                else if (itemData is ConsumableItemData c) { name = c.ItemName; description = c.Description; iconPath = c.ImagePath; }

                var iconTexture = _spriteManager.GetItemSprite(iconPath);
                var iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath);

                DrawGenericItemInfoPanel(spriteBatch, font, secondaryFont, name, description, iconTexture, iconSilhouette, stats, bounds);
            }
        }

        private float MeasureContentHeight(BitmapFont font, BitmapFont secondaryFont, object? data)
        {
            if (data == null) return 0;

            const int spriteSize = 16;
            const int spellSpriteSize = 32;
            const int gap = 4;
            const int padding = 4;
            float width = TOOLTIP_WIDTH - (padding * 2);

            if (data is MoveData move)
            {
                float h = 2;
                h += spellSpriteSize;
                h += font.LineHeight + 2;
                h += (secondaryFont.LineHeight * 3) + (gap * 2);
                if (move.MakesContact) h += secondaryFont.LineHeight + gap;
                if (!string.IsNullOrEmpty(move.Description))
                {
                    var lines = ParseAndWrapRichText(secondaryFont, move.Description.ToUpper(), width, Color.White);
                    h += lines.Count * secondaryFont.LineHeight;
                }
                return h + padding;
            }
            else
            {
                string name = "";
                string description = "";
                Dictionary<string, int> stats = new Dictionary<string, int>();

                if (data is WeaponData w) { name = w.WeaponName; description = w.Description; stats = w.StatModifiers; }
                else if (data is ArmorData a) { name = a.ArmorName; description = a.Description; stats = a.StatModifiers; }
                else if (data is RelicData r) { name = r.RelicName; description = r.Description; stats = r.StatModifiers; }
                else if (data is ConsumableItemData c) { name = c.ItemName; description = c.Description; }

                float h = 0;
                h += spriteSize + gap;

                var titleLines = ParseAndWrapRichText(font, name.ToUpper(), width, _global.Palette_BlueWhite);
                h += titleLines.Count * font.LineHeight;

                if (!string.IsNullOrEmpty(description))
                {
                    h += gap;
                    var descLines = ParseAndWrapRichText(secondaryFont, description.ToUpper(), width, _global.Palette_White);
                    h += descLines.Count * secondaryFont.LineHeight;
                }

                var (pos, neg) = GetStatModifierLines(stats);
                if (pos.Any() || neg.Any())
                {
                    h += gap;
                    int rows = Math.Max(pos.Count, neg.Count);
                    h += rows * secondaryFont.LineHeight;
                }
                return h + padding;
            }
        }

        private void DrawSpellInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, MoveData move, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle? sourceRect, Color? iconTint, Rectangle idleFrame, Vector2 idleOrigin, bool drawBackground, Rectangle infoPanelArea)
        {
            var tertiaryFont = _core.TertiaryFont;
            const int spriteSize = 32;
            const int padding = 4;
            const int gap = 2;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            int spriteY = infoPanelArea.Y + 2;

            float displayScale = 1.0f;
            Vector2 iconOrigin = new Vector2(16, 16);
            Vector2 drawPos = new Vector2(spriteX + 16, spriteY + 16);

            if (iconSilhouette != null)
            {
                Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, -1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, -1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 0) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 0) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, -1) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, 1) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            if (iconTexture != null)
            {
                Color tint = iconTint ?? Color.White;
                spriteBatch.DrawSnapped(iconTexture, drawPos, sourceRect, tint, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            string name = move.MoveName.ToUpper();
            Vector2 nameSize = font.MeasureString(name);

            Vector2 namePos = new Vector2(
                infoPanelArea.X + (infoPanelArea.Width - nameSize.X) / 2f,
                spriteY + spriteSize - (font.LineHeight / 2f) - 2
            );

            spriteBatch.DrawStringOutlinedSnapped(font, name, namePos, _global.Palette_BlueWhite, _global.Palette_Black);

            float currentY = namePos.Y + font.LineHeight + 2;

            float leftLabelX = infoPanelArea.X + 8;
            float leftValueRightX = infoPanelArea.X + 51;
            float rightLabelX = infoPanelArea.X + 59;
            float rightValueRightX = infoPanelArea.X + 112;

            void DrawStatPair(string label, string value, float labelX, float valueRightX, float y, Color valColor)
            {
                spriteBatch.DrawStringSnapped(secondaryFont, label, new Vector2(labelX, y), _global.Palette_Gray);
                float valWidth = secondaryFont.MeasureString(value).Width;
                spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(valueRightX - valWidth, y), valColor);
            }

            string powVal = move.Power > 0 ? move.Power.ToString() : (move.Effects.ContainsKey("ManaDamage") ? "???" : "---");
            string accVal = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
            DrawStatPair("POW", powVal, leftLabelX, leftValueRightX, currentY, _global.Palette_White);
            DrawStatPair("ACC", accVal, rightLabelX, rightValueRightX, currentY, _global.Palette_White);
            currentY += secondaryFont.LineHeight + gap;

            string mpVal = move.ManaCost > 0 ? move.ManaCost.ToString() : "0";
            string targetVal = move.Target switch
            {
                TargetType.Single => "SINGL",
                TargetType.SingleAll => "ANY",
                TargetType.Both => "BOTH",
                TargetType.Every => "MULTI",
                TargetType.All => "ALL",
                TargetType.Self => "SELF",
                TargetType.Team => "TEAM",
                TargetType.Ally => "ALLY",
                TargetType.SingleTeam => "S-TEAM",
                TargetType.RandomBoth => "R-BOTH",
                TargetType.RandomEvery => "R-EVRY",
                TargetType.RandomAll => "R-ALL",
                TargetType.None => "NONE",
                _ => "---"
            };
            DrawStatPair("MP ", mpVal, leftLabelX, leftValueRightX, currentY, _global.Palette_LightBlue);
            DrawStatPair("TGT", targetVal, rightLabelX, rightValueRightX, currentY, _global.Palette_White);
            currentY += secondaryFont.LineHeight + gap;

            string offStatVal = move.OffensiveStat switch
            {
                OffensiveStatType.Strength => "STR",
                OffensiveStatType.Intelligence => "INT",
                OffensiveStatType.Tenacity => "TEN",
                OffensiveStatType.Agility => "AGI",
                _ => "---"
            };

            Color offColor = move.OffensiveStat switch
            {
                OffensiveStatType.Strength => _global.StatColor_Strength,
                OffensiveStatType.Intelligence => _global.StatColor_Intelligence,
                OffensiveStatType.Tenacity => _global.StatColor_Tenacity,
                OffensiveStatType.Agility => _global.StatColor_Agility,
                _ => _global.Palette_White
            };

            string impactVal = move.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, move.ImpactType.ToString().Length));
            Color impactColor = move.ImpactType == ImpactType.Magical ? _global.Palette_LightBlue : (move.ImpactType == ImpactType.Physical ? _global.Palette_Orange : _global.Palette_Gray);

            DrawStatPair("USE", offStatVal, leftLabelX, leftValueRightX, currentY, offColor);
            DrawStatPair("TYP", impactVal, rightLabelX, rightValueRightX, currentY, impactColor);
            currentY += secondaryFont.LineHeight + gap;

            if (move.MakesContact)
            {
                string contactText = "[ CONTACT ]";
                Vector2 contactSize = tertiaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - contactSize.X) / 2f, currentY + (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f);
                spriteBatch.DrawStringSnapped(tertiaryFont, contactText, contactPos, _global.Palette_Red);
                currentY += secondaryFont.LineHeight + gap;
            }

            string description = move.Description.ToUpper();
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = infoPanelArea.Width - (padding * 2);
                var descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);

                int maxLines = 8;
                if (descLines.Count > maxLines) descLines = descLines.Take(maxLines).ToList();

                float totalDescHeight = descLines.Count * secondaryFont.LineHeight;
                float bottomPadding = 3f;
                float areaTop = currentY;
                float areaBottom = infoPanelArea.Bottom - bottomPadding;
                float areaHeight = areaBottom - areaTop;
                float startY = areaTop + (areaHeight - totalDescHeight) / 2f;
                if (startY < areaTop) startY = areaTop;

                float lineY = startY;
                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * SPACE_WIDTH;
                        else lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text)) segWidth = segment.Text.Length * SPACE_WIDTH;
                        else
                        {
                            segWidth = secondaryFont.MeasureString(segment.Text).Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, lineY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    lineY += secondaryFont.LineHeight;
                }
            }
        }

        private void DrawGenericItemInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, string name, string description, Texture2D? iconTexture, Texture2D? iconSilhouette, Dictionary<string, int> stats, Rectangle infoPanelArea)
        {
            const int spriteSize = 16;
            const int gap = 4;

            var statLines = GetStatModifierLines(stats);

            int maxTitleWidth = infoPanelArea.Width - (4 * 2);
            var titleLines = ParseAndWrapRichText(font, name, maxTitleWidth, _global.Palette_BlueWhite);
            float totalTitleHeight = titleLines.Count * font.LineHeight;

            float totalDescHeight = 0f;
            List<List<ColoredText>> descLines = new List<List<ColoredText>>();
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = infoPanelArea.Width - (4 * 2);
                descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);
                totalDescHeight = descLines.Count * secondaryFont.LineHeight;
            }

            float totalStatHeight = Math.Max(statLines.Positives.Count, statLines.Negatives.Count) * secondaryFont.LineHeight;
            float totalContentHeight = spriteSize + gap + totalTitleHeight + (totalDescHeight > 0 ? gap + totalDescHeight : 0) + (totalStatHeight > 0 ? gap + totalStatHeight : 0);

            float currentY = infoPanelArea.Y + (infoPanelArea.Height - totalContentHeight) / 2f;
            currentY -= 22f;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;

            float displayScale = 1.0f;
            Vector2 iconOrigin = new Vector2(8, 8);
            Vector2 drawPos = new Vector2(spriteX + 8, currentY + 8);

            if (iconSilhouette != null)
            {
                Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, -1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, -1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 0) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 0) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, -1) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, 1) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            if (iconTexture != null)
            {
                spriteBatch.DrawSnapped(iconTexture, drawPos, null, Color.White, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            currentY += spriteSize + gap;

            foreach (var line in titleLines)
            {
                float lineWidth = 0;
                foreach (var segment in line)
                {
                    if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * SPACE_WIDTH;
                    else lineWidth += font.MeasureString(segment.Text).Width;
                }

                float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2f;
                float currentX = lineX;

                foreach (var segment in line)
                {
                    float segWidth;
                    if (string.IsNullOrWhiteSpace(segment.Text)) segWidth = segment.Text.Length * SPACE_WIDTH;
                    else
                    {
                        segWidth = font.MeasureString(segment.Text).Width;
                        spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(currentX, currentY), segment.Color);
                    }
                    currentX += segWidth;
                }
                currentY += font.LineHeight;
            }

            if (descLines.Any())
            {
                currentY += gap;
                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * SPACE_WIDTH;
                        else lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    var lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text)) segWidth = segment.Text.Length * SPACE_WIDTH;
                        else
                        {
                            segWidth = secondaryFont.MeasureString(segment.Text).Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    currentY += secondaryFont.LineHeight;
                }
            }

            if (statLines.Positives.Any() || statLines.Negatives.Any())
            {
                currentY += gap;
                float leftColX = infoPanelArea.X + 8;
                float rightColX = infoPanelArea.X + 64;

                int maxRows = Math.Max(statLines.Positives.Count, statLines.Negatives.Count);

                for (int i = 0; i < maxRows; i++)
                {
                    if (i < statLines.Positives.Count)
                    {
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Positives[i], infoPanelArea.Width / 2, _global.Palette_White);
                        if (lineParts.Count > 0)
                        {
                            var segments = lineParts[0];
                            float currentX = leftColX;
                            foreach (var segment in segments)
                            {
                                float segWidth = string.IsNullOrWhiteSpace(segment.Text) ? segment.Text.Length * SPACE_WIDTH : secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                                currentX += segWidth;
                            }
                        }
                    }

                    if (i < statLines.Negatives.Count)
                    {
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Negatives[i], infoPanelArea.Width / 2, _global.Palette_White);
                        if (lineParts.Count > 0)
                        {
                            var segments = lineParts[0];
                            float currentX = rightColX;
                            foreach (var segment in segments)
                            {
                                float segWidth = string.IsNullOrWhiteSpace(segment.Text) ? segment.Text.Length * SPACE_WIDTH : secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                                currentX += segWidth;
                            }
                        }
                    }
                    currentY += secondaryFont.LineHeight;
                }
            }
        }

        private List<List<ColoredText>> ParseAndWrapRichText(BitmapFont font, string text, float maxWidth, Color defaultColor)
        {
            var lines = new List<List<ColoredText>>();
            if (string.IsNullOrEmpty(text)) return lines;

            var currentLine = new List<ColoredText>();
            float currentLineWidth = 0f;
            Color currentColor = defaultColor;

            var parts = Regex.Split(text, @"(\[.*?\]|\s+)");

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string tagContent = part.Substring(1, part.Length - 2).ToLowerInvariant();
                    if (tagContent == "/" || tagContent == "default")
                    {
                        currentColor = defaultColor;
                    }
                    else
                    {
                        currentColor = ParseColor(tagContent);
                    }
                }
                else if (part.Contains("\n"))
                {
                    lines.Add(currentLine);
                    currentLine = new List<ColoredText>();
                    currentLineWidth = 0f;
                }
                else
                {
                    bool isWhitespace = string.IsNullOrWhiteSpace(part);
                    float partWidth = isWhitespace ? (part.Length * SPACE_WIDTH) : font.MeasureString(part).Width;

                    if (!isWhitespace && currentLineWidth + partWidth > maxWidth && currentLineWidth > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = new List<ColoredText>();
                        currentLineWidth = 0f;
                    }

                    if (isWhitespace && currentLineWidth == 0)
                    {
                        continue;
                    }

                    Color finalColor = currentColor;
                    if (currentColor != defaultColor && !isWhitespace && part.EndsWith("%"))
                    {
                        string numberPart = part.Substring(0, part.Length - 1);
                        if (int.TryParse(numberPart, out int percent))
                        {
                            float amount = Math.Clamp(percent / 100f, 0f, 1f);
                            finalColor = Color.Lerp(_global.Palette_DarkGray, currentColor, amount);
                        }
                    }

                    currentLine.Add(new ColoredText(part, finalColor));
                    currentLineWidth += partWidth;
                }
            }

            if (currentLine.Count > 0)
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private Color ParseColor(string colorName)
        {
            string tag = colorName.ToLowerInvariant();

            if (tag == "cstr") return _global.StatColor_Strength;
            if (tag == "cint") return _global.StatColor_Intelligence;
            if (tag == "cten") return _global.StatColor_Tenacity;
            if (tag == "cagi") return _global.StatColor_Agility;

            if (tag == "cpositive") return _global.ColorPositive;
            if (tag == "cnegative") return _global.ColorNegative;
            if (tag == "ccrit") return _global.ColorCrit;
            if (tag == "cimmune") return _global.ColorImmune;
            if (tag == "cctm") return _global.ColorConditionToMeet;
            if (tag == "cetc") return _global.Palette_DarkGray;

            if (tag == "cfire") return _global.ElementColors.GetValueOrDefault(2, Color.White);
            if (tag == "cwater") return _global.ElementColors.GetValueOrDefault(3, Color.White);
            if (tag == "carcane") return _global.ElementColors.GetValueOrDefault(4, Color.White);
            if (tag == "cearth") return _global.ElementColors.GetValueOrDefault(5, Color.White);
            if (tag == "cmetal") return _global.ElementColors.GetValueOrDefault(6, Color.White);
            if (tag == "ctoxic") return _global.ElementColors.GetValueOrDefault(7, Color.White);
            if (tag == "cwind") return _global.ElementColors.GetValueOrDefault(8, Color.White);
            if (tag == "cvoid") return _global.ElementColors.GetValueOrDefault(9, Color.White);
            if (tag == "clight") return _global.ElementColors.GetValueOrDefault(10, Color.White);
            if (tag == "celectric") return _global.ElementColors.GetValueOrDefault(11, Color.White);
            if (tag == "cice") return _global.ElementColors.GetValueOrDefault(12, Color.White);
            if (tag == "cnature") return _global.ElementColors.GetValueOrDefault(13, Color.White);

            if (tag.StartsWith("c"))
            {
                string effectName = tag.Substring(1);
                if (effectName == "poison") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Poison, Color.White);
                if (effectName == "stun") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Stun, Color.White);
                if (effectName == "regen") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Regen, Color.White);
                if (effectName == "dodging") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Dodging, Color.White);
                if (effectName == "burn") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Burn, Color.White);
                if (effectName == "frostbite") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Frostbite, Color.White);
                if (effectName == "silence") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Silence, Color.White);
            }

            switch (tag)
            {
                case "red": return _global.Palette_Red;
                case "blue": return _global.Palette_LightBlue;
                case "green": return _global.Palette_LightGreen;
                case "yellow": return _global.Palette_Yellow;
                case "orange": return _global.Palette_Orange;
                case "purple": return _global.Palette_LightPurple;
                case "pink": return _global.Palette_Pink;
                case "gray": return _global.Palette_Gray;
                case "white": return _global.Palette_White;
                case "BlueWhite": return _global.Palette_BlueWhite;
                case "darkgray": return _global.Palette_DarkGray;
                default: return _global.Palette_White;
            }
        }

        private (List<string> Positives, List<string> Negatives) GetStatModifierLines(Dictionary<string, int> mods)
        {
            var positives = new List<string>();
            var negatives = new List<string>();
            if (mods == null || mods.Count == 0) return (positives, negatives);

            foreach (var kvp in mods)
            {
                if (kvp.Value == 0) continue;
                string colorTag = kvp.Value > 0 ? "[cpositive]" : "[cnegative]";
                string sign = kvp.Value > 0 ? "+" : "";

                string statName = kvp.Key.ToLowerInvariant() switch
                {
                    "strength" => "STR",
                    "intelligence" => "INT",
                    "tenacity" => "TEN",
                    "agility" => "AGI",
                    "maxhp" => "HP",
                    "maxmana" => "MP",
                    _ => kvp.Key.ToUpper().Substring(0, Math.Min(3, kvp.Key.Length))
                };

                if (statName.Length < 3)
                {
                    statName += " ";
                }

                string line = $"{statName} {colorTag}{sign}{kvp.Value}[/]";

                if (kvp.Value > 0)
                {
                    positives.Add(line);
                }
                else
                {
                    negatives.Add(line);
                }
            }
            return (positives, negatives);
        }

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}

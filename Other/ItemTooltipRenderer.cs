using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
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

        public const float TOOLTIP_DELAY = 0.5f;

        private const int MIN_TOOLTIP_WIDTH = 120;
        private const int SPACE_WIDTH = 5;
        private const int SIDE_PADDING = 6;
        private const float COLUMN_CENTER_OFFSET = 24f;
        private const int SCREEN_EDGE_MARGIN = 10;

        private const int LAYOUT_SPRITE_Y = 6;
        private const int LAYOUT_TITLE_Y = 30;
        private const int LAYOUT_VARS_START_Y = 44;
        private const int LAYOUT_VAR_ROW_HEIGHT = 9;
        private const int LAYOUT_CONTACT_Y = LAYOUT_VARS_START_Y + (3 * LAYOUT_VAR_ROW_HEIGHT);
        private const int LAYOUT_DESC_START_Y = LAYOUT_CONTACT_Y + LAYOUT_VAR_ROW_HEIGHT - 3;

        public Color DimmerColor { get; set; } = Color.Black;
        public float DimmerOpacity { get; set; } = 0.7f;

        public ItemTooltipRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _core = ServiceLocator.Get<Core>();
        }

        public void DrawTooltip(SpriteBatch spriteBatch, object itemData, Vector2 anchorPosition, GameTime gameTime, Vector2? scale = null, float opacity = 1.0f)
        {
            _core.RequestFullscreenOverlay((sb, uiMatrix) =>
            {
                DrawTooltipImmediate(sb, uiMatrix, itemData, anchorPosition, gameTime, scale, opacity, drawDimmer: true);
            });
        }

        public void DrawTooltipImmediate(SpriteBatch sb, Matrix uiMatrix, object itemData, Vector2 anchorPosition, GameTime gameTime, Vector2? scale = null, float opacity = 1.0f, bool drawDimmer = true)
        {
            if (itemData == null || opacity <= 0.01f) return;

            Vector2 drawScale = scale ?? Vector2.One;
            var font = ServiceLocator.Get<BitmapFont>();
            var secondaryFont = _core.SecondaryFont;

            int panelWidth = CalculateDynamicWidth(itemData, font);
            float contentHeight = MeasureContentHeight(font, secondaryFont, itemData, panelWidth);
            int panelHeight = (int)Math.Ceiling(contentHeight) + 8 + 2;
            float spriteCenterOffsetY = GetSpriteCenterOffsetY(itemData);

            float panelY = anchorPosition.Y - spriteCenterOffsetY;
            float panelX = anchorPosition.X - (panelWidth / 2f);

            var infoPanelArea = new Rectangle(
                (int)MathF.Round(panelX),
                (int)MathF.Round(panelY),
                panelWidth,
                panelHeight
            );

            int screenTop = SCREEN_EDGE_MARGIN;
            int screenBottom = Global.VIRTUAL_HEIGHT - SCREEN_EDGE_MARGIN;
            int screenLeft = SCREEN_EDGE_MARGIN;
            int screenRight = Global.VIRTUAL_WIDTH - SCREEN_EDGE_MARGIN;

            if (infoPanelArea.X < screenLeft) infoPanelArea.X = screenLeft;
            if (infoPanelArea.Right > screenRight) infoPanelArea.X = screenRight - infoPanelArea.Width;
            if (infoPanelArea.Y < screenTop) infoPanelArea.Y = screenTop;
            if (infoPanelArea.Bottom > screenBottom) infoPanelArea.Y = screenBottom - infoPanelArea.Height;

            if (drawDimmer)
            {
                var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
                int screenW = graphicsDevice.PresentationParameters.BackBufferWidth;
                int screenH = graphicsDevice.PresentationParameters.BackBufferHeight;
                var pixel = ServiceLocator.Get<Texture2D>();

                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.Identity);
                sb.Draw(pixel, new Rectangle(0, 0, screenW, screenH), DimmerColor * DimmerOpacity * opacity);
                sb.End();
            }

            Vector2 pivotPoint = anchorPosition;
            Matrix animTransform = Matrix.CreateTranslation(-pivotPoint.X, -pivotPoint.Y, 0) *
                                   Matrix.CreateScale(drawScale.X, drawScale.Y, 1.0f) *
                                   Matrix.CreateTranslation(pivotPoint.X, pivotPoint.Y, 0);

            Matrix finalTransform = animTransform * uiMatrix;

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, finalTransform);

            var pixelTex = ServiceLocator.Get<Texture2D>();
            DrawBeveledBackground(sb, pixelTex, infoPanelArea, _global.Palette_Black * opacity);
            DrawBeveledBorder(sb, pixelTex, infoPanelArea, _global.Palette_Sun * opacity);
            DrawInfoPanelContent(sb, itemData, infoPanelArea, font, secondaryFont, gameTime, opacity);

            sb.End();
        }

        private float GetSpriteCenterOffsetY(object itemData)
        {
            if (itemData is MoveData)
            {
                return (LAYOUT_SPRITE_Y - 3) + 16;
            }
            else
            {
                return (LAYOUT_SPRITE_Y + 2) + 8;
            }
        }

        private List<string> WrapName(string name)
        {
            var words = name.Split(' ');
            var lines = new List<string>();
            string currentLine = "";

            foreach (var word in words)
            {
                if ((currentLine + (currentLine.Length > 0 ? " " : "") + word).Length > 12)
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                        currentLine = "";
                    }
                }

                if (currentLine.Length > 0) currentLine += " ";
                currentLine += word;
            }
            if (!string.IsNullOrEmpty(currentLine)) lines.Add(currentLine);

            return lines;
        }

        private int CalculateDynamicWidth(object itemData, BitmapFont font)
        {
            string name = "";
            if (itemData is MoveData m) name = m.MoveName;

            var lines = WrapName(name.ToUpper());

            float maxLineWidth = 0;
            foreach (var line in lines)
            {
                float w = font.MeasureString(line).Width;
                if (w > maxLineWidth) maxLineWidth = w;
            }

            int calculatedWidth = (int)Math.Ceiling(maxLineWidth) + (SIDE_PADDING * 2);
            return Math.Max(MIN_TOOLTIP_WIDTH, calculatedWidth);
        }

        public void DrawInfoPanelContent(SpriteBatch spriteBatch, object itemData, Rectangle bounds, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime, float opacity)
        {
            var tertiaryFont = _core.TertiaryFont;
            string typeText = "ITEM";

            if (itemData is MoveData moveHeader)
            {
                typeText = moveHeader.MoveType == MoveType.Spell ? "SPELL" : "ACTION";
            }

            spriteBatch.DrawStringSnapped(tertiaryFont, typeText, new Vector2(bounds.X + 4, bounds.Y + 2), _global.Palette_DarkShadow * opacity);

            if (itemData is MoveData moveData)
            {
                string iconPath = $"Sprites/Spells/{moveData.MoveID}";
                string? fallbackPath = null;

                string impactName = moveData.ImpactType.ToString().ToLowerInvariant();
                fallbackPath = $"Sprites/Spells/default_{impactName}";

                var iconTexture = _spriteManager.GetItemSprite(iconPath, fallbackPath);
                var iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath, fallbackPath);
                Rectangle? sourceRect = _spriteManager.GetAnimatedIconSourceRect(iconTexture, gameTime);

                DrawSpellInfoPanel(spriteBatch, font, secondaryFont, moveData, iconTexture, iconSilhouette, sourceRect, null, Rectangle.Empty, Vector2.Zero, false, bounds, gameTime, opacity);
            }
        }

        private float MeasureContentHeight(BitmapFont font, BitmapFont secondaryFont, object? data, int panelWidth)
        {
            if (data == null) return 0;

            var tertiaryFont = _core.TertiaryFont;
            const int padding = 4;
            const int lineSpacing = 1;
            float width = panelWidth - (padding * 2);

            float baseHeight = LAYOUT_DESC_START_Y;

            string description = "";
            string flavor = "";

            if (data is MoveData move)
            {
                description = move.Description;
                flavor = move.Flavor;
            }

            float descHeight = 0f;
            if (!string.IsNullOrEmpty(description))
            {
                var lines = ParseAndWrapRichText(secondaryFont, description.ToUpper(), width, Color.White);
                descHeight = lines.Count * secondaryFont.LineHeight;
            }

            float flavorHeight = 0f;
            if (!string.IsNullOrEmpty(flavor))
            {
                var lines = ParseAndWrapRichText(tertiaryFont, flavor.ToUpper(), width, _global.Palette_DarkShadow, 3);
                flavorHeight = (lines.Count * tertiaryFont.LineHeight) + ((lines.Count - 1) * lineSpacing);
                flavorHeight += 2;
            }

            return baseHeight + descHeight + flavorHeight + padding;
        }

        private void DrawItemName(SpriteBatch spriteBatch, BitmapFont font, string name, Rectangle infoPanelArea, float opacity, GameTime gameTime)
        {
            var lines = WrapName(name.ToUpper());
            float bottomY = infoPanelArea.Y + LAYOUT_TITLE_Y;

            for (int i = lines.Count - 1; i >= 0; i--)
            {
                string line = lines[i];
                Vector2 lineSize = font.MeasureString(line);
                int linesFromBottom = (lines.Count - 1) - i;
                float yPos = bottomY - (linesFromBottom * font.LineHeight);

                Vector2 namePos = new Vector2(
                    infoPanelArea.X + (infoPanelArea.Width - lineSize.X) / 2f,
                    yPos
                );
                namePos = new Vector2(MathF.Round(namePos.X), MathF.Round(namePos.Y));

                TextAnimator.DrawTextWithEffectOutlined(spriteBatch, font, line, namePos, _global.Palette_Sun * opacity, _global.Palette_Black * opacity, TextEffectType.Drift, (float)gameTime.TotalGameTime.TotalSeconds);
            }
        }

        private void DrawSpellInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, MoveData move, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle? sourceRect, Color? iconTint, Rectangle idleFrame, Vector2 idleOrigin, bool drawBackground, Rectangle infoPanelArea, GameTime gameTime, float opacity)
        {
            var tertiaryFont = _core.TertiaryFont;
            const int spriteSize = 32;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            int spriteY = infoPanelArea.Y + LAYOUT_SPRITE_Y - 3;

            Vector2 iconOrigin = new Vector2(16, 16);
            Vector2 staticDrawPos = new Vector2(spriteX + 16, spriteY + 16);

            DrawIconWithSilhouette(spriteBatch, iconTexture, iconSilhouette, staticDrawPos, iconOrigin, 1.0f, sourceRect, iconTint, opacity);
            DrawItemName(spriteBatch, font, move.MoveName, infoPanelArea, opacity, gameTime);

            float currentY = infoPanelArea.Y + LAYOUT_VARS_START_Y;

            float centerX = infoPanelArea.Center.X;
            float leftCenter = centerX - COLUMN_CENTER_OFFSET;
            float rightCenter = centerX + COLUMN_CENTER_OFFSET;
            float labelOffset = 18f;
            float valueOffset = 2f;

            float leftLabelX = leftCenter - labelOffset;
            float leftValueX = leftCenter + valueOffset;
            float rightLabelX = rightCenter - labelOffset;
            float rightValueX = rightCenter + valueOffset;

            void DrawStatPair(string label, string value, float labelX, float valueX, float y, Color valColor)
            {
                float yOffset = (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
                yOffset = MathF.Round(yOffset);
                spriteBatch.DrawStringSnapped(tertiaryFont, label, new Vector2(labelX, y + yOffset), _global.Palette_DarkShadow * opacity);
                spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(valueX, y), valColor * opacity);
            }

            string powVal = move.Power > 0 ? move.Power.ToString() : (move.Effects.ContainsKey("ManaDamage") ? "???" : "---");
            string accVal = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
            string mpVal = (move.ManaCost > 0 ? move.ManaCost.ToString() : "0");
            var manaDump = move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
            if (manaDump != null) powVal = "???";

            DrawStatPair("POW", powVal, leftLabelX, leftValueX, currentY, _global.Palette_Sun);
            DrawStatPair("ACC", accVal, rightLabelX, rightValueX, currentY, _global.Palette_Sun);
            currentY += LAYOUT_VAR_ROW_HEIGHT;

            DrawStatPair("MANA", mpVal, leftLabelX, leftValueX, currentY, _global.Palette_Sun);
            DrawStatPair("TGT", GetTargetString(move.Target), rightLabelX, rightValueX, currentY, _global.Palette_Sun);
            currentY += LAYOUT_VAR_ROW_HEIGHT;

            string offStatVal = GetStatString(move.OffensiveStat);
            Color offColor = _global.Palette_Sun;
            string impactVal = move.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, move.ImpactType.ToString().Length));
            Color impactColor = move.ImpactType == ImpactType.Magical ? _global.Palette_Sky : (move.ImpactType == ImpactType.Physical ? _global.Palette_Fruit : _global.Palette_DarkShadow);

            DrawStatPair("USE", offStatVal, leftLabelX, leftValueX, currentY, offColor);
            DrawStatPair("TYP", impactVal, rightLabelX, rightValueX, currentY, impactColor);

            currentY = infoPanelArea.Y + LAYOUT_CONTACT_Y;
            if (move.MakesContact)
            {
                string contactText = "[ CONTACT ]";
                Vector2 contactSize = tertiaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - contactSize.X) / 2f, currentY + (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f);
                spriteBatch.DrawStringSnapped(tertiaryFont, contactText, contactPos, _global.Palette_Rust * opacity);
            }

            float flavorHeight = DrawFlavorText(spriteBatch, infoPanelArea, move.Flavor, opacity);
            currentY = infoPanelArea.Y + LAYOUT_DESC_START_Y;
            DrawDescription(spriteBatch, secondaryFont, move.Description, infoPanelArea, currentY, flavorHeight, gameTime, opacity);
        }

        private void DrawIconWithSilhouette(SpriteBatch spriteBatch, Texture2D? texture, Texture2D? silhouette, Vector2 pos, Vector2 origin, float scale, Rectangle? sourceRect = null, Color? tint = null, float opacity = 1.0f)
        {
            if (texture != null)
            {
                spriteBatch.DrawSnapped(texture, pos, sourceRect, (tint ?? Color.White) * opacity, 0f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        private float DrawFlavorText(SpriteBatch spriteBatch, Rectangle infoPanelArea, string flavorText, float opacity)
        {
            if (string.IsNullOrEmpty(flavorText)) return 0f;

            var tertiaryFont = _core.TertiaryFont;
            const int padding = 4;
            const int spaceWidth = 3;
            const int lineSpacing = 1;

            float width = infoPanelArea.Width - (padding * 2);
            var lines = ParseAndWrapRichText(tertiaryFont, flavorText.ToUpper(), width, _global.Palette_DarkShadow, spaceWidth);

            if (lines.Count == 0) return 0f;

            float lineHeight = tertiaryFont.LineHeight;
            float totalHeight = (lines.Count * lineHeight) + ((lines.Count - 1) * lineSpacing);

            float startY = infoPanelArea.Bottom - (padding + 2) - totalHeight + 3;

            foreach (var line in lines)
            {
                float lineWidth = 0;
                foreach (var segment in line)
                {
                    if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * spaceWidth;
                    else lineWidth += tertiaryFont.MeasureString(segment.Text).Width;
                }

                float currentX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2f;

                foreach (var segment in line)
                {
                    float segWidth;
                    if (string.IsNullOrWhiteSpace(segment.Text))
                    {
                        segWidth = segment.Text.Length * spaceWidth;
                    }
                    else
                    {
                        segWidth = tertiaryFont.MeasureString(segment.Text).Width;
                        spriteBatch.DrawStringSnapped(tertiaryFont, segment.Text, new Vector2(currentX, startY), segment.Color * opacity);
                    }
                    currentX += segWidth;
                }
                startY += lineHeight + lineSpacing;
            }
            return totalHeight + 2 - 3;
        }

        private void DrawDescription(SpriteBatch spriteBatch, BitmapFont font, string description, Rectangle infoPanelArea, float startY, float flavorHeight, GameTime gameTime, float opacity)
        {
            if (string.IsNullOrEmpty(description)) return;

            const int padding = 4;
            float descWidth = infoPanelArea.Width - (padding * 2);
            var descLines = ParseAndWrapRichText(font, description.ToUpper(), descWidth, _global.GameTextColor);

            float lineY = startY;
            foreach (var line in descLines)
            {
                if (lineY + font.LineHeight > infoPanelArea.Bottom - padding - flavorHeight) break;

                float lineWidth = 0;
                foreach (var segment in line)
                {
                    if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * SPACE_WIDTH;
                    else lineWidth += font.MeasureString(segment.Text).Width;
                }

                float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
                float currentX = lineX;

                foreach (var segment in line)
                {
                    float segWidth;
                    if (string.IsNullOrWhiteSpace(segment.Text)) segWidth = segment.Text.Length * SPACE_WIDTH;
                    else
                    {
                        segWidth = font.MeasureString(segment.Text).Width;
                        if (segment.Effect != TextEffectType.None)
                            TextAnimator.DrawTextWithEffect(spriteBatch, font, segment.Text, new Vector2(currentX, lineY), segment.Color * opacity, segment.Effect, (float)gameTime.TotalGameTime.TotalSeconds);
                        else
                            spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(currentX, lineY), segment.Color * opacity);
                    }
                    currentX += segWidth;
                }
                lineY += font.LineHeight;
            }
        }

        private string GetTargetString(TargetType target)
        {
            return target switch
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
        }

        private string GetStatString(OffensiveStatType stat)
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

        private List<List<ColoredText>> ParseAndWrapRichText(BitmapFont font, string text, float maxWidth, Color defaultColor, int spaceWidth = 5)
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
                    float partWidth = isWhitespace ? (part.Length * spaceWidth) : font.MeasureString(part).Width;

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
            return _global.GetNarrationColor(colorName);
        }

        private void DrawBeveledBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
        {
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Y + 1, rect.Width - 4, 1), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Bottom - 2, rect.Width - 4, 1), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 1, rect.Y + 2, rect.Width - 2, rect.Height - 4), color);
        }

        private void DrawBeveledBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
        {
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Y, rect.Width - 4, 1), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Bottom - 1, rect.Width - 4, 1), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X, rect.Y + 2, 1, rect.Height - 4), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.Right - 1, rect.Y + 2, 1, rect.Height - 4), color);
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.X + 1, rect.Y + 1), color);
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.Right - 2, rect.Y + 1), color);
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.X + 1, rect.Bottom - 2), color);
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.Right - 2, rect.Bottom - 2), color);
        }
    }
}

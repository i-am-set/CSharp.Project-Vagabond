using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class SplitMapHudRenderer
    {
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly GameState _gameState;
        private readonly Texture2D _pixel;

        private const int HUD_HEIGHT = 98;
        private const int CARD_WIDTH = 78;
        private const int START_Y = Global.VIRTUAL_HEIGHT - HUD_HEIGHT;

        // --- Hover State ---
        private readonly List<(Rectangle Bounds, MoveData? Move, int CardCenterX)> _activeHitboxes = new();
        private float _hoverTimer;
        private (Rectangle Bounds, MoveData? Move, int CardCenterX)? _currentHoveredItem;
        private const float HOVER_DELAY = 0.2f;
        private Vector2 _lastMousePos;
        private bool _isTooltipVisible;

        public SplitMapHudRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _pixel = ServiceLocator.Get<Texture2D>();
        }

        public void Update(GameTime gameTime, Vector2 virtualMousePos)
        {
            bool found = false;
            var cursorManager = ServiceLocator.Get<CursorManager>();

            foreach (var item in _activeHitboxes)
            {
                if (item.Bounds.Contains(virtualMousePos))
                {
                    if (item.Move != null)
                    {
                        cursorManager.SetState(CursorState.Hint);

                        if (_currentHoveredItem.HasValue && _currentHoveredItem.Value.Bounds == item.Bounds)
                        {
                            if (!_isTooltipVisible)
                            {
                                if (virtualMousePos == _lastMousePos)
                                {
                                    _hoverTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                                    if (_hoverTimer >= HOVER_DELAY)
                                    {
                                        _isTooltipVisible = true;
                                    }
                                }
                                else
                                {
                                    _hoverTimer = 0f;
                                }
                            }
                        }
                        else
                        {
                            _currentHoveredItem = item;
                            _hoverTimer = 0f;
                            _isTooltipVisible = false;
                        }
                        found = true;
                    }
                    break;
                }
            }

            if (!found)
            {
                _currentHoveredItem = null;
                _hoverTimer = 0f;
                _isTooltipVisible = false;
            }

            _lastMousePos = virtualMousePos;
        }

        public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
        {
            _activeHitboxes.Clear();

            var core = ServiceLocator.Get<Core>();
            var defaultFont = core.DefaultFont;
            var secondaryFont = core.SecondaryFont;
            var tertiaryFont = core.TertiaryFont;

            if (defaultFont == null || secondaryFont == null || tertiaryFont == null) return;

            // Draw Background
            spriteBatch.DrawSnapped(_pixel, new Rectangle(0, START_Y, Global.VIRTUAL_WIDTH, HUD_HEIGHT), _global.Palette_Black);
            spriteBatch.DrawSnapped(_pixel, new Rectangle(0, START_Y, Global.VIRTUAL_WIDTH, 1), _global.Palette_DarkestPale);

            var party = _gameState.PlayerState.Party;
            int count = party.Count;

            int totalWidth = count * CARD_WIDTH;
            int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

            for (int i = 0; i < count; i++)
            {
                int x = startX + (i * CARD_WIDTH);
                DrawCard(spriteBatch, gameTime, party[i], x, i, defaultFont, secondaryFont, tertiaryFont);
            }

            // Draw Tooltip
            if (_currentHoveredItem.HasValue && _isTooltipVisible && _currentHoveredItem.Value.Move != null)
            {
                DrawTooltip(spriteBatch, gameTime, _currentHoveredItem.Value.Bounds, _currentHoveredItem.Value.CardCenterX, _currentHoveredItem.Value.Move);
            }
        }

        private void DrawCard(SpriteBatch spriteBatch, GameTime gameTime, PartyMember member, int xPosition, int index, BitmapFont defaultFont, BitmapFont secondaryFont, BitmapFont tertiaryFont)
        {
            int y = START_Y + 6;
            int centerX = xPosition + (CARD_WIDTH / 2);

            // --- 1. Name ---
            string name = member.Name.ToUpper();
            Color nameColor = _global.Palette_LightPale;
            Vector2 nameSize = defaultFont.MeasureString(name);
            spriteBatch.DrawStringSnapped(defaultFont, name, new Vector2(centerX - nameSize.X / 2, y), nameColor);

            y += (int)nameSize.Y - 4;

            // --- 2. Portrait ---
            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            float bobSpeed = 3f;
            float wavePhase = index * 1.0f;
            float sineValue = MathF.Sin(time * bobSpeed + wavePhase);
            float bobOffset = sineValue * 0.5f;

            float seed = index * 13.5f;
            float floatSpeedX = 0.1f;
            float floatSpeedY = 0.1f;
            float floatAmp = 1.5f;

            float floatX = MathF.Sin(time * floatSpeedX + seed) * floatAmp;
            float floatY = MathF.Cos(time * floatSpeedY + seed) * floatAmp;

            float rotSpeed = 0.2f;
            float rotAmp = 0.02f;
            float rotation = MathF.Sin(time * rotSpeed + seed) * rotAmp;

            PlayerSpriteType type = sineValue < 0 ? PlayerSpriteType.Alt : PlayerSpriteType.Normal;
            var sourceRect = _spriteManager.GetPlayerSourceRect(member.PortraitIndex, type);

            Vector2 origin = new Vector2(16, 16);
            Vector2 pos = new Vector2(centerX, y + 16) + new Vector2(floatX, bobOffset + floatY);

            spriteBatch.DrawSnapped(_spriteManager.PlayerMasterSpriteSheet, pos, sourceRect, Color.White, rotation, origin, 1.0f, SpriteEffects.None, 0f);

            y += 32 + 4;

            // --- 3. HP Bar ---
            Texture2D hpBg = _spriteManager.InventoryPlayerHealthBarEmpty;
            if (hpBg != null)
            {
                int barX = centerX - (hpBg.Width / 2);
                string hpText = $"{member.CurrentHP}/{member.MaxHP}";
                spriteBatch.DrawStringSnapped(tertiaryFont, hpText, new Vector2(barX, (y - 9) - tertiaryFont.LineHeight + 1), _global.Palette_DarkShadow);
                spriteBatch.DrawSnapped(hpBg, new Vector2(barX, y - 8), Color.White);

                if (_spriteManager.InventoryPlayerHealthBarFull != null)
                {
                    float hpPercent = (float)member.CurrentHP / Math.Max(1, member.MaxHP);
                    int visibleWidth = (int)(_spriteManager.InventoryPlayerHealthBarFull.Width * hpPercent);
                    var srcRect = new Rectangle(0, 0, visibleWidth, _spriteManager.InventoryPlayerHealthBarFull.Height);
                    spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, y - 8), srcRect, Color.White);
                }
            }

            y += 0;

            // --- 4. Stats ---
            string[] labels = { "STR", "INT", "TEN", "AGI" };
            string[] keys = { "Strength", "Intelligence", "Tenacity", "Agility" };
            int statBlockStartX = centerX - 30;

            for (int s = 0; s < 4; s++)
            {
                Color labelColor = _global.Palette_DarkPale;
                spriteBatch.DrawStringSnapped(secondaryFont, labels[s], new Vector2(statBlockStartX, y), labelColor);

                Texture2D statBg = _spriteManager.InventoryStatBarEmpty;
                if (statBg != null)
                {
                    int pipX = statBlockStartX + 19;
                    int pipY = y + (int)(secondaryFont.LineHeight / 2) - (statBg.Height / 2);
                    spriteBatch.DrawSnapped(statBg, new Vector2(pipX, pipY), Color.White);

                    if (_spriteManager.InventoryStatBarFull != null)
                    {
                        int val = _gameState.PlayerState.GetBaseStat(member, keys[s]);
                        int basePoints = Math.Clamp(val, 0, 10);
                        if (basePoints > 0)
                        {
                            var srcBase = new Rectangle(0, 0, basePoints * 4, 3);
                            spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarFull, new Vector2(pipX, pipY), srcBase, Color.White);
                        }
                    }
                }
                y += (int)secondaryFont.LineHeight + 1;
            }

            y += 2;

            // --- 5. Moves ---
            int moveStartX = statBlockStartX - 6;
            DrawMoveName(spriteBatch, member.BasicMove, "bas", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
            DrawMoveName(spriteBatch, member.CoreMove, "cor", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
            DrawMoveName(spriteBatch, member.AltMove, "alt", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
        }

        private void DrawMoveName(SpriteBatch sb, MoveEntry? move, string label, int x, int centerX, ref int y, BitmapFont labelFont, BitmapFont font)
        {
            string text = "EMPTY";
            Color color = _global.Palette_DarkShadow;
            bool isMovePresent = false;
            MoveData? moveData = null;

            if (move != null)
            {
                if (BattleDataCache.Moves.TryGetValue(move.MoveID, out var data))
                {
                    text = data.MoveName;
                    moveData = data;

                    if (label == "bas")
                        color = _global.Palette_DarkestPale;
                    else if (label == "alt")
                        color = _global.Palette_Pale;
                    else
                        color = _global.Palette_LightPale;

                    isMovePresent = true;
                }
            }

            int cardStartX = centerX - (CARD_WIDTH / 2);
            int lineHeight = (int)font.LineHeight;
            Rectangle hitRect = new Rectangle(cardStartX, y - 2, CARD_WIDTH, lineHeight + 4);

            bool isHovered = _currentHoveredItem.HasValue && _currentHoveredItem.Value.Bounds == hitRect;

            if (isHovered && isMovePresent)
            {
                Color c = _global.Palette_Sun;
                sb.DrawSnapped(_pixel, new Rectangle(hitRect.X, hitRect.Y, hitRect.Width, 1), c);
                sb.DrawSnapped(_pixel, new Rectangle(hitRect.X, hitRect.Bottom - 1, hitRect.Width, 1), c);
                sb.DrawSnapped(_pixel, new Rectangle(hitRect.X, hitRect.Y, 1, hitRect.Height), c);
                sb.DrawSnapped(_pixel, new Rectangle(hitRect.Right - 1, hitRect.Y, 1, hitRect.Height), c);
            }

            if (isMovePresent)
            {
                sb.DrawStringSnapped(labelFont, label, new Vector2(x, y), _global.Palette_DarkShadow);
                float labelWidth = labelFont.MeasureString(label).Width;
                float labelEndX = x + labelWidth;
                int cardEndX = cardStartX + CARD_WIDTH;
                float availableWidth = cardEndX - labelEndX;
                float textWidth = font.MeasureString(text).Width;
                float textX = MathF.Floor(labelEndX + (availableWidth - textWidth) / 2f);
                sb.DrawStringSnapped(font, text, new Vector2(textX, y), color);
            }
            else
            {
                Vector2 size = font.MeasureString(text);
                sb.DrawStringSnapped(font, text, new Vector2(centerX - size.X / 2, y), color);
            }

            _activeHitboxes.Add((hitRect, moveData, centerX));

            y += lineHeight + 3;
        }

        private void DrawTooltip(SpriteBatch sb, GameTime gameTime, Rectangle targetRect, int cardCenterX, MoveData move)
        {
            const int INFO_BOX_WIDTH = 140;
            const int INFO_BOX_HEIGHT = 34;

            // --- Positioning with Clamping (Float Precision) ---
            float x = cardCenterX - (INFO_BOX_WIDTH / 2f);

            // Clamp to screen edges with 2px margin
            float minX = 2f;
            float maxX = Global.VIRTUAL_WIDTH - INFO_BOX_WIDTH - 2f;

            if (x < minX) x = minX;
            if (x > maxX) x = maxX;

            // Floating animation logic (Float Precision)
            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            float floatY = MathF.Cos(time * 0.25f + (cardCenterX * 0.05f)) * 1.5f;
            float y = targetRect.Top - INFO_BOX_HEIGHT - 4 + floatY;

            Vector2 boxPos = new Vector2(x, y);
            Vector2 boxSize = new Vector2(INFO_BOX_WIDTH, INFO_BOX_HEIGHT);

            // --- Draw Backgrounds ---
            DrawBeveledBackground(sb, _pixel, boxPos, boxSize, _global.Palette_DarkShadow);

            // Inner description area background
            Vector2 descPos = new Vector2(boxPos.X + 1, boxPos.Y + boxSize.Y - 1 - 18);
            Vector2 descSize = new Vector2(boxSize.X - 2, 18);
            DrawBeveledBackground(sb, _pixel, descPos, descSize, _global.Palette_Black);

            // --- Draw Content ---
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            float startX = boxPos.X + 4;
            float startY = boxPos.Y + 1;

            // Layout Tuning
            int rowSpacing = 8;
            int pairSpacing = 5;
            float labelValueGap = tertiaryFont.MeasureString(" ").Width;

            float currentY = startY;

            string name = move.MoveName.ToUpper();
            string desc = move.Description;
            string powTxt = move.Power > 0 ? move.Power.ToString() : "--";
            string accTxt = move.Accuracy > 0 ? $"{move.Accuracy}%" : "--";
            string useTxt = GetStatShortName(move.OffensiveStat);

            // --- Center Stats Logic ---
            float MeasurePair(string label, string val)
            {
                return tertiaryFont.MeasureString(label).Width + labelValueGap + secondaryFont.MeasureString(val).Width;
            }

            float w1 = MeasurePair("POW", powTxt);
            float w2 = MeasurePair("ACC", accTxt);
            float w3 = MeasurePair("USE", useTxt);
            float totalStatsWidth = w1 + pairSpacing + w2 + pairSpacing + w3;

            // Center the block
            float statsCurrentX = boxPos.X + (INFO_BOX_WIDTH - totalStatsWidth) / 2f;

            void DrawPair(string label, string val)
            {
                sb.DrawStringSnapped(tertiaryFont, label, new Vector2(statsCurrentX, currentY + 1), _global.Palette_Black);
                statsCurrentX += tertiaryFont.MeasureString(label).Width + labelValueGap;

                sb.DrawStringSnapped(secondaryFont, val, new Vector2(statsCurrentX, currentY), _global.Palette_DarkPale);
                statsCurrentX += secondaryFont.MeasureString(val).Width + pairSpacing;
            }

            DrawPair("POW", powTxt);
            DrawPair("ACC", accTxt);
            DrawPair("USE", useTxt);

            // Name
            currentY += (rowSpacing - 2);
            Vector2 nameSize = secondaryFont.MeasureString(name);
            float centeredNameX = boxPos.X + (INFO_BOX_WIDTH - nameSize.X) / 2f;
            sb.DrawStringSnapped(secondaryFont, name, new Vector2(centeredNameX, currentY), _global.Palette_LightPale);

            // Description
            currentY += rowSpacing;
            float maxWidth = INFO_BOX_WIDTH - 8;

            // --- Rich Text Centering Logic ---
            var lines = new List<List<(string Text, Color Color)>>();
            var currentLine = new List<(string Text, Color Color)>();
            float currentLineWidth = 0f;
            lines.Add(currentLine);

            var parts = Regex.Split(desc, @"(\[.*?\]|\s+)");
            Color currentColor = _global.Palette_Sun;

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
                    Vector2 size = tertiaryFont.MeasureString(textPart);

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
                            if (currentLineWidth + size.X > maxWidth)
                            {
                                lines.Add(new List<(string, Color)>());
                                currentLine = lines.Last();
                                currentLineWidth = 0;
                            }
                            else
                            {
                                currentLine.Add((textPart, currentColor));
                                currentLineWidth += size.X;
                            }
                        }
                        continue;
                    }

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

            int descLineHeight = tertiaryFont.LineHeight + 1;
            int totalDescHeight = lines.Count * descLineHeight;
            float availableHeight = (boxPos.Y + boxSize.Y) - currentY - 2;
            float startDrawY = currentY + (availableHeight - totalDescHeight) / 2;

            if (startDrawY < currentY) startDrawY = currentY;

            foreach (var line in lines)
            {
                if (startDrawY + descLineHeight > (boxPos.Y + boxSize.Y)) break;

                float lineWidth = 0;
                foreach (var item in line) lineWidth += tertiaryFont.MeasureString(item.Text).Width;

                float lineX = startX + (maxWidth - lineWidth) / 2f;

                foreach (var item in line)
                {
                    sb.DrawStringSnapped(tertiaryFont, item.Text, new Vector2(lineX, startDrawY), item.Color);
                    lineX += tertiaryFont.MeasureString(item.Text).Width;
                }
                startDrawY += descLineHeight;
            }
        }

        private void DrawBeveledBackground(SpriteBatch spriteBatch, Texture2D pixel, Vector2 pos, Vector2 size, Color color)
        {
            // Top
            spriteBatch.DrawSnapped(pixel, new Vector2(pos.X + 1, pos.Y), new Rectangle(0, 0, (int)size.X - 2, 1), color);
            // Bottom
            spriteBatch.DrawSnapped(pixel, new Vector2(pos.X + 1, pos.Y + size.Y - 1), new Rectangle(0, 0, (int)size.X - 2, 1), color);
            // Middle
            spriteBatch.DrawSnapped(pixel, new Vector2(pos.X, pos.Y + 1), new Rectangle(0, 0, (int)size.X, (int)size.Y - 2), color);
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
    }
}

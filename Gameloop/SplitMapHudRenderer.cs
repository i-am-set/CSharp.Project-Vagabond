using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    public class SplitMapHudRenderer
    {
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly GameState _gameState;
        private readonly Texture2D _pixel;
        private readonly MoveTooltipRenderer _tooltipRenderer;

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
            _tooltipRenderer = new MoveTooltipRenderer();
        }

        public void Update(GameTime gameTime, Vector2 virtualMousePos)
        {
            bool found = false;
            var cursorManager = ServiceLocator.Get<CursorManager>();

            foreach (var item in _activeHitboxes)
            {
                if (item.Bounds.Contains(virtualMousePos))
                {
                    // Only show cursor hint and tooltip if there is actual move data
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

            // Draw Tooltip via Shared Renderer
            if (_currentHoveredItem.HasValue && _isTooltipVisible && _currentHoveredItem.Value.Move != null)
            {
                _tooltipRenderer.DrawFloating(
                    spriteBatch,
                    gameTime,
                    _currentHoveredItem.Value.Bounds,
                    _currentHoveredItem.Value.CardCenterX,
                    _currentHoveredItem.Value.Move
                );
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
    }
}
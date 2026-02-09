using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class SplitMapHudRenderer
    {
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly GameState _gameState;
        private readonly Texture2D _pixel;
        private readonly MoveTooltipRenderer _tooltipRenderer;

        public const int HUD_HEIGHT = 98;
        private const int CARD_WIDTH = 78;

        // Base Y position (Default state)
        private int BaseY => Global.VIRTUAL_HEIGHT - HUD_HEIGHT;

        // --- Hover State ---
        private readonly List<(Rectangle Bounds, MoveData? Move, int CardCenterX)> _activeHitboxes = new();
        private float _hoverTimer;
        private (Rectangle Bounds, MoveData? Move, int CardCenterX)? _currentHoveredItem;
        private const float HOVER_DELAY = 0.2f;
        private Vector2 _lastMousePos;
        private bool _isTooltipVisible;

        // --- Drag & Drop State ---
        private PartyMember? _draggedMember;
        private float _dragOffsetX;
        private bool _isDragging;

        // --- Animation State ---
        private readonly Dictionary<PartyMember, float> _visualPositions = new Dictionary<PartyMember, float>();
        private readonly Dictionary<PartyMember, float> _verticalOffsets = new Dictionary<PartyMember, float>();

        private const float CARD_MOVE_SPEED = 15f;
        private const float CARD_DROP_SPEED = 10f;
        private const float ENTRY_Y_OFFSET = -16f;

        public SplitMapHudRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _pixel = ServiceLocator.Get<Texture2D>();
            _tooltipRenderer = new MoveTooltipRenderer();
        }

        public void Update(GameTime gameTime, Vector2 virtualMousePos, float verticalOffset)
        {
            var cursorManager = ServiceLocator.Get<CursorManager>();
            var party = _gameState.PlayerState.Party;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            float currentHudY = BaseY + verticalOffset;

            // --- Drag Logic ---
            if (_isDragging && _draggedMember != null)
            {
                cursorManager.SetState(CursorState.Dragging);

                float newX = virtualMousePos.X + _dragOffsetX;
                newX = Math.Clamp(newX, 0, Global.VIRTUAL_WIDTH - CARD_WIDTH);
                _visualPositions[_draggedMember] = newX;

                int currentIndex = party.IndexOf(_draggedMember);
                int totalWidth = party.Count * CARD_WIDTH;
                int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

                for (int i = 0; i < party.Count; i++)
                {
                    if (i == currentIndex) continue;
                    float slotCenterX = startX + (i * CARD_WIDTH) + (CARD_WIDTH / 2);
                    float cardCenterX = newX + (CARD_WIDTH / 2);

                    if (Math.Abs(cardCenterX - slotCenterX) < (CARD_WIDTH / 2))
                    {
                        var temp = party[currentIndex];
                        party[currentIndex] = party[i];
                        party[i] = temp;
                        break;
                    }
                }

                if (Mouse.GetState().LeftButton == ButtonState.Released)
                {
                    _isDragging = false;
                    _draggedMember = null;
                }

                goto ProcessTweening;
            }

            // --- Card Hover & Drag Start Logic ---
            int totalW = party.Count * CARD_WIDTH;
            int sX = (Global.VIRTUAL_WIDTH - totalW) / 2;

            for (int i = 0; i < party.Count; i++)
            {
                var member = party[i];
                if (!_visualPositions.ContainsKey(member)) continue;

                float x = _visualPositions[member];
                // CHANGED: Increased height by 6px (HUD_HEIGHT - 4 instead of -10)
                Rectangle cardRect = new Rectangle((int)x, (int)currentHudY + 6, CARD_WIDTH, HUD_HEIGHT - 5);

                if (cardRect.Contains(virtualMousePos))
                {
                    cursorManager.SetState(CursorState.HoverDraggable);

                    if (Mouse.GetState().LeftButton == ButtonState.Pressed && !_isDragging)
                    {
                        _isDragging = true;
                        _draggedMember = member;
                        _dragOffsetX = _visualPositions[member] - virtualMousePos.X;
                    }
                }
            }

            // --- Tooltip Logic ---
            bool found = false;
            // CHANGED: Iterate in reverse so top-most elements (dragged card) are checked first
            for (int i = _activeHitboxes.Count - 1; i >= 0; i--)
            {
                var item = _activeHitboxes[i];
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
                                    _hoverTimer += dt;
                                    if (_hoverTimer >= HOVER_DELAY) _isTooltipVisible = true;
                                }
                                else _hoverTimer = 0f;
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
                    break; // Stop checking after finding the top-most match
                }
            }

            if (!found)
            {
                _currentHoveredItem = null;
                _hoverTimer = 0f;
                _isTooltipVisible = false;
            }
            _lastMousePos = virtualMousePos;

        ProcessTweening:
            // --- Card Position Tweening ---
            int totalWidthTween = party.Count * CARD_WIDTH;
            int startXTween = (Global.VIRTUAL_WIDTH - totalWidthTween) / 2;

            for (int i = 0; i < party.Count; i++)
            {
                var member = party[i];
                float targetX = startXTween + (i * CARD_WIDTH);

                if (!_visualPositions.ContainsKey(member))
                    _visualPositions[member] = targetX;
                else
                {
                    if (member != _draggedMember)
                    {
                        float currentX = _visualPositions[member];
                        float dampingX = 1.0f - MathF.Exp(-CARD_MOVE_SPEED * dt);
                        _visualPositions[member] = MathHelper.Lerp(currentX, targetX, dampingX);
                    }
                }

                if (!_verticalOffsets.ContainsKey(member))
                    _verticalOffsets[member] = ENTRY_Y_OFFSET;
                else
                {
                    float currentY = _verticalOffsets[member];
                    float dampingY = 1.0f - MathF.Exp(-CARD_DROP_SPEED * dt);
                    _verticalOffsets[member] = MathHelper.Lerp(currentY, 0f, dampingY);
                }
            }

            var activeMembers = new HashSet<PartyMember>(party);
            var keysToRemove = _visualPositions.Keys.Where(k => !activeMembers.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _visualPositions.Remove(key);
                _verticalOffsets.Remove(key);
            }
        }

        public void Draw(SpriteBatch spriteBatch, GameTime gameTime, float verticalOffset = 0f)
        {
            _activeHitboxes.Clear();

            var core = ServiceLocator.Get<Core>();
            var defaultFont = core.DefaultFont;
            var secondaryFont = core.SecondaryFont;
            var tertiaryFont = core.TertiaryFont;

            if (defaultFont == null || secondaryFont == null || tertiaryFont == null) return;

            float currentStartY = BaseY + verticalOffset;
            float t = Math.Clamp(verticalOffset / 6f, 0f, 1f);
            Color lineColor = Color.Lerp(_global.Palette_DarkPale, _global.Palette_DarkestPale, t);

            spriteBatch.Draw(_pixel, new Vector2(0, currentStartY), null, _global.Palette_Black, 0f, Vector2.Zero, new Vector2(Global.VIRTUAL_WIDTH, HUD_HEIGHT), SpriteEffects.None, 0f);
            spriteBatch.Draw(_pixel, new Vector2(0, currentStartY), null, lineColor, 0f, Vector2.Zero, new Vector2(Global.VIRTUAL_WIDTH, 1), SpriteEffects.None, 0f);

            var party = _gameState.PlayerState.Party;
            int count = party.Count;
            var mousePos = Core.TransformMouse(Mouse.GetState().Position);

            // Store dragged card data to draw it last (on top)
            (PartyMember Member, float X, float YOffset, int Index)? draggedCardData = null;

            for (int i = 0; i < count; i++)
            {
                var member = party[i];

                if (!_visualPositions.ContainsKey(member))
                {
                    int totalW = count * CARD_WIDTH;
                    int sX = (Global.VIRTUAL_WIDTH - totalW) / 2;
                    _visualPositions[member] = sX + (i * CARD_WIDTH);
                }
                if (!_verticalOffsets.ContainsKey(member))
                {
                    _verticalOffsets[member] = ENTRY_Y_OFFSET;
                }

                float x = _visualPositions[member];
                float yOffset = _verticalOffsets[member] + verticalOffset;

                // If this is the dragged card, skip drawing it now
                if (_isDragging && _draggedMember == member)
                {
                    draggedCardData = (member, x, yOffset, i);
                    continue;
                }

                // Draw Hover Rect for non-dragged cards
                // CHANGED: Use Vector2 for smooth rect drawing
                Vector2 cardPos = new Vector2(x, BaseY + 6 + yOffset);
                Vector2 cardSize = new Vector2(CARD_WIDTH, HUD_HEIGHT - 4);
                Rectangle cardRect = new Rectangle((int)x, (int)(BaseY + 6 + yOffset), CARD_WIDTH, HUD_HEIGHT - 4);

                if (cardRect.Contains(mousePos) && !_isDragging)
                {
                    DrawHollowRectSmooth(spriteBatch, cardPos, cardSize, _global.Palette_Pale);
                }

                DrawCard(spriteBatch, gameTime, member, (int)x, yOffset, i, defaultFont, secondaryFont, tertiaryFont);
            }

            // Draw Dragged Card Last (On Top)
            if (draggedCardData != null)
            {
                var d = draggedCardData.Value;
                Vector2 bgPos = new Vector2(d.X, BaseY + 6 + d.YOffset);
                Vector2 bgSize = new Vector2(CARD_WIDTH, HUD_HEIGHT - 4);

                // CHANGED: Draw Black Background behind dragged card
                spriteBatch.Draw(_pixel, bgPos, null, _global.Palette_Black, 0f, Vector2.Zero, bgSize, SpriteEffects.None, 0f);

                // Draw Drag Highlight
                DrawHollowRectSmooth(spriteBatch, bgPos, bgSize, _global.Palette_Sun);

                // Draw Content
                DrawCard(spriteBatch, gameTime, d.Member, (int)d.X, d.YOffset, d.Index, defaultFont, secondaryFont, tertiaryFont);
            }

            if (_currentHoveredItem.HasValue && _isTooltipVisible && _currentHoveredItem.Value.Move != null && !_isDragging)
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

        // CHANGED: New helper for smooth sub-pixel rectangles
        private void DrawHollowRectSmooth(SpriteBatch spriteBatch, Vector2 pos, Vector2 size, Color color)
        {
            // Top
            spriteBatch.Draw(_pixel, pos, null, color, 0f, Vector2.Zero, new Vector2(size.X, 1), SpriteEffects.None, 0f);
            // Bottom
            spriteBatch.Draw(_pixel, new Vector2(pos.X, pos.Y + size.Y - 1), null, color, 0f, Vector2.Zero, new Vector2(size.X, 1), SpriteEffects.None, 0f);
            // Left
            spriteBatch.Draw(_pixel, pos, null, color, 0f, Vector2.Zero, new Vector2(1, size.Y), SpriteEffects.None, 0f);
            // Right
            spriteBatch.Draw(_pixel, new Vector2(pos.X + size.X - 1, pos.Y), null, color, 0f, Vector2.Zero, new Vector2(1, size.Y), SpriteEffects.None, 0f);
        }

        private void DrawCard(SpriteBatch spriteBatch, GameTime gameTime, PartyMember member, int xPosition, float yOffset, int index, BitmapFont defaultFont, BitmapFont secondaryFont, BitmapFont tertiaryFont)
        {
            float y = BaseY + 6 + yOffset;
            int centerX = xPosition + (CARD_WIDTH / 2);

            string name = member.Name.ToUpper();
            Color nameColor = _global.Palette_LightPale;
            Vector2 nameSize = defaultFont.MeasureString(name);
            spriteBatch.DrawStringSnapped(defaultFont, name, new Vector2(centerX - nameSize.X / 2, y), nameColor);

            y += (int)nameSize.Y - 4;

            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            float bobSpeed = 3f;
            float wavePhase = index * 1.0f;
            float sineValue = MathF.Sin(time * bobSpeed + wavePhase);
            float bobOffset = sineValue * 0.5f;

            float seed = index * 13.5f;
            float floatX = MathF.Sin(time * 0.1f + seed) * 1.5f;
            float floatY = MathF.Cos(time * 0.1f + seed) * 1.5f;
            float rotation = MathF.Sin(time * 0.2f + seed) * 0.02f;

            PlayerSpriteType type = sineValue < 0 ? PlayerSpriteType.Alt : PlayerSpriteType.Normal;
            var sourceRect = _spriteManager.GetPlayerSourceRect(member.PortraitIndex, type);

            Vector2 origin = new Vector2(16, 16);
            Vector2 pos = new Vector2(centerX, y + 16) + new Vector2(floatX, bobOffset + floatY);

            spriteBatch.DrawSnapped(_spriteManager.PlayerMasterSpriteSheet, pos, sourceRect, Color.White, rotation, origin, 1.0f, SpriteEffects.None, 0f);

            y += 32 + 4;

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

            string[] labels = { "STR", "INT", "TEN", "AGI" };
            string[] keys = { "Strength", "Intelligence", "Tenacity", "Agility" };
            int statBlockStartX = centerX - 30;

            for (int s = 0; s < 4; s++)
            {
                spriteBatch.DrawStringSnapped(secondaryFont, labels[s], new Vector2(statBlockStartX, y), _global.Palette_DarkPale);

                Texture2D statBg = _spriteManager.InventoryStatBarEmpty;
                if (statBg != null)
                {
                    int pipX = statBlockStartX + 19;
                    float pipY = y + (secondaryFont.LineHeight / 2f) - (statBg.Height / 2f);
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

            int moveStartX = statBlockStartX - 6;
            DrawMoveName(spriteBatch, member.BasicMove, "bas", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
            DrawMoveName(spriteBatch, member.CoreMove, "cor", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
            DrawMoveName(spriteBatch, member.AltMove, "alt", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
        }

        private void DrawMoveName(SpriteBatch sb, MoveEntry? move, string label, int x, int centerX, ref float y, BitmapFont labelFont, BitmapFont font)
        {
            string text = "EMPTY";
            Color color = _global.Palette_DarkShadow;
            bool isMovePresent = false;
            MoveData? moveData = null;

            if (move != null && BattleDataCache.Moves.TryGetValue(move.MoveID, out var data))
            {
                text = data.MoveName;
                moveData = data;
                isMovePresent = true;

                if (label == "bas") color = _global.Palette_DarkestPale;
                else if (label == "alt") color = _global.Palette_Pale;
                else color = _global.Palette_LightPale;
            }

            int cardStartX = centerX - (CARD_WIDTH / 2);
            int lineHeight = (int)font.LineHeight;

            Rectangle hitRect = new Rectangle(cardStartX, (int)y - 2, CARD_WIDTH, lineHeight + 4);
            bool isHovered = _currentHoveredItem.HasValue && _currentHoveredItem.Value.Bounds == hitRect;

            if (isHovered && isMovePresent)
            {
                Color c = _global.Palette_Sun;
                sb.DrawSnapped(_pixel, new Vector2(hitRect.X, y - 2), null, c, 0f, Vector2.Zero, new Vector2(hitRect.Width, 1), SpriteEffects.None, 0f);
                sb.DrawSnapped(_pixel, new Vector2(hitRect.X, y - 2 + hitRect.Height - 1), null, c, 0f, Vector2.Zero, new Vector2(hitRect.Width, 1), SpriteEffects.None, 0f);
                sb.DrawSnapped(_pixel, new Vector2(hitRect.X, y - 2), null, c, 0f, Vector2.Zero, new Vector2(1, hitRect.Height), SpriteEffects.None, 0f);
                sb.DrawSnapped(_pixel, new Vector2(hitRect.Right - 1, y - 2), null, c, 0f, Vector2.Zero, new Vector2(1, hitRect.Height), SpriteEffects.None, 0f);
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
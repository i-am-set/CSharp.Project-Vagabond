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
        private const int CARD_SPACING = 1;

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
        private bool _isDragging;
        private int _dragStartMouseY;

        // Tracks the "Ghost" mouse position (User Intent) in Virtual Pixels
        private float _virtualPullX;
        // Tracks the last known Screen X to calculate deltas
        private int _lastScreenMouseX;

        // Tracks the card that should render on top (last dragged)
        private PartyMember? _topmostMember;

        // Tracks the visual lift of the cursor sprite
        private float _currentCursorLift = 0f;

        // --- Elastic Physics Constants ---
        private const float MAX_ELASTIC_STRETCH = 45f;
        private const float ELASTIC_STIFFNESS = 60f;
        private const float DRAG_TETHER_SPEED = 25f;
        private const float MAX_PULL_DISTANCE = 180f;
        private const float SWAP_THRESHOLD_FACTOR = 0.5f;

        // Public property for SplitMapScene to prevent HUD sliding
        public bool IsDragging => _isDragging;

        // --- Animation State ---
        private readonly Dictionary<PartyMember, float> _visualPositions = new Dictionary<PartyMember, float>();
        private readonly Dictionary<PartyMember, float> _verticalOffsets = new Dictionary<PartyMember, float>();

        private const float CARD_MOVE_SPEED = 15f;
        private const float CARD_DROP_SPEED = 10f;
        private const float ENTRY_Y_OFFSET = -16f;
        private const float DRAG_LIFT_OFFSET = -8f;

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
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();

            float currentHudY = BaseY + verticalOffset;

            // --- Drag Logic ---
            if (_isDragging && _draggedMember != null)
            {
                cursorManager.SetState(CursorState.Dragging);

                // 1. Calculate Scale Factor
                float scaleX = (float)graphicsDevice.Viewport.Width / Global.VIRTUAL_WIDTH;
                float scaleY = (float)graphicsDevice.Viewport.Height / Global.VIRTUAL_HEIGHT;
                float scale = Math.Min(scaleX, scaleY);

                // 2. Calculate Input Delta
                var currentMouseState = Mouse.GetState();
                int screenDeltaX = currentMouseState.X - _lastScreenMouseX;
                float virtualDeltaX = screenDeltaX / scale;

                // 3. Calculate Anchor
                int currentIndex = party.IndexOf(_draggedMember);
                int totalWidth = (party.Count * CARD_WIDTH) + ((party.Count > 0 ? party.Count - 1 : 0) * CARD_SPACING);
                int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;
                float anchorSlotX = startX + (currentIndex * (CARD_WIDTH + CARD_SPACING));

                // 4. Update "Ghost" Pull Position with Ratcheting Logic
                float currentDiff = _virtualPullX - anchorSlotX;

                // Check if moving towards center (relaxing tension)
                bool movingTowardsCenter = (currentDiff > 0 && virtualDeltaX < 0) || (currentDiff < 0 && virtualDeltaX > 0);

                if (movingTowardsCenter)
                {
                    // INVERSE TANH LOGIC:
                    // We want the visual card to move 1:1 with the mouse when returning.
                    // 1. Calculate where the visual card IS (based on current physics)
                    float currentVisualOffset = MAX_ELASTIC_STRETCH * MathF.Tanh(currentDiff / ELASTIC_STIFFNESS);

                    // 2. Apply the mouse delta directly to the visual offset (1:1 movement)
                    float targetVisualOffset = currentVisualOffset + virtualDeltaX;

                    // 3. Clamp to avoid domain errors in Atanh (must be < Max)
                    float maxVal = MAX_ELASTIC_STRETCH * 0.9999f;
                    targetVisualOffset = Math.Clamp(targetVisualOffset, -maxVal, maxVal);

                    // 4. Reverse-solve the physics to find the _virtualPullX that produces this visual position
                    // diff = k * Atanh(visual / Max)
                    // Atanh(x) = 0.5 * ln((1+x)/(1-x))
                    float ratio = targetVisualOffset / MAX_ELASTIC_STRETCH;
                    float newDiff = ELASTIC_STIFFNESS * 0.5f * MathF.Log((1f + ratio) / (1f - ratio));

                    _virtualPullX = anchorSlotX + newDiff;
                }
                else
                {
                    // Standard accumulation (Resistance applies via Tanh later)
                    _virtualPullX += virtualDeltaX;
                }

                // 5. Clamp Input (Safety Wall)
                float diff = _virtualPullX - anchorSlotX;
                diff = Math.Clamp(diff, -MAX_PULL_DISTANCE, MAX_PULL_DISTANCE);
                _virtualPullX = anchorSlotX + diff;

                // 6. Calculate Elastic Target
                float dampedDiff = MAX_ELASTIC_STRETCH * MathF.Tanh(diff / ELASTIC_STIFFNESS);
                float targetVisualX = anchorSlotX + dampedDiff;

                // 7. Interpolate Card Position
                float prevVisualX = _visualPositions[_draggedMember];
                float tetherDamping = 1.0f - MathF.Exp(-DRAG_TETHER_SPEED * dt);
                float newVisualX = MathHelper.Lerp(prevVisualX, targetVisualX, tetherDamping);
                _visualPositions[_draggedMember] = newVisualX;

                // 8. Force Mouse to Follow Card
                float virtualMoveAmt = newVisualX - prevVisualX;
                int screenMoveAmt = (int)MathF.Round(virtualMoveAmt * scale);
                int newScreenMouseX = _lastScreenMouseX + screenMoveAmt;

                Mouse.SetPosition(newScreenMouseX, _dragStartMouseY);
                _lastScreenMouseX = newScreenMouseX;

                // 9. Swap Logic
                float swapThreshold = CARD_WIDTH * SWAP_THRESHOLD_FACTOR;

                for (int i = 0; i < party.Count; i++)
                {
                    if (i == currentIndex) continue;

                    float neighborSlotCenterX = startX + (i * (CARD_WIDTH + CARD_SPACING)) + (CARD_WIDTH / 2);
                    float ghostCardCenterX = _virtualPullX + (CARD_WIDTH / 2);

                    if (Math.Abs(ghostCardCenterX - neighborSlotCenterX) < swapThreshold)
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
            int totalW = (party.Count * CARD_WIDTH) + ((party.Count > 0 ? party.Count - 1 : 0) * CARD_SPACING);
            int sX = (Global.VIRTUAL_WIDTH - totalW) / 2;

            for (int i = 0; i < party.Count; i++)
            {
                var member = party[i];
                if (!_visualPositions.ContainsKey(member)) continue;

                float x = _visualPositions[member];
                Rectangle cardRect = new Rectangle((int)x, (int)currentHudY + 3, CARD_WIDTH, HUD_HEIGHT - 4);

                if (cardRect.Contains(virtualMousePos))
                {
                    cursorManager.SetState(CursorState.HoverDraggable);

                    if (Mouse.GetState().LeftButton == ButtonState.Pressed && !_isDragging)
                    {
                        _isDragging = true;
                        _draggedMember = member;
                        _topmostMember = member;

                        _dragStartMouseY = Mouse.GetState().Y;
                        _lastScreenMouseX = Mouse.GetState().X;

                        float grabOffset = _visualPositions[member] - virtualMousePos.X;
                        _virtualPullX = virtualMousePos.X + grabOffset;
                    }
                }
            }

            // --- Tooltip Logic ---
            bool found = false;
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

        ProcessTweening:
            // --- Cursor Lift Animation ---
            float targetCursorLift = _isDragging ? DRAG_LIFT_OFFSET : 0f;
            float cursorDamping = 1.0f - MathF.Exp(-CARD_DROP_SPEED * dt);
            _currentCursorLift = MathHelper.Lerp(_currentCursorLift, targetCursorLift, cursorDamping);

            cursorManager.VisualOffset = new Vector2(0, _currentCursorLift);

            // --- Card Position Tweening ---
            int totalWidthTween = (party.Count * CARD_WIDTH) + ((party.Count > 0 ? party.Count - 1 : 0) * CARD_SPACING);
            int startXTween = (Global.VIRTUAL_WIDTH - totalWidthTween) / 2;

            for (int i = 0; i < party.Count; i++)
            {
                var member = party[i];
                float targetX = startXTween + (i * (CARD_WIDTH + CARD_SPACING));

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
                {
                    _verticalOffsets[member] = ENTRY_Y_OFFSET;
                }
                else
                {
                    float currentY = _verticalOffsets[member];
                    float targetY = (member == _draggedMember) ? DRAG_LIFT_OFFSET : 0f;
                    float dampingY = 1.0f - MathF.Exp(-CARD_DROP_SPEED * dt);
                    _verticalOffsets[member] = MathHelper.Lerp(currentY, targetY, dampingY);
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

            // 1. Draw all cards EXCEPT the topmost one
            for (int i = 0; i < count; i++)
            {
                var member = party[i];
                if (member == _topmostMember) continue;

                DrawMemberCard(spriteBatch, gameTime, member, i, verticalOffset, mousePos, defaultFont, secondaryFont, tertiaryFont);
            }

            // 2. Draw the topmost card last
            if (_topmostMember != null && party.Contains(_topmostMember))
            {
                int index = party.IndexOf(_topmostMember);
                DrawMemberCard(spriteBatch, gameTime, _topmostMember, index, verticalOffset, mousePos, defaultFont, secondaryFont, tertiaryFont);
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

        private void DrawMemberCard(SpriteBatch spriteBatch, GameTime gameTime, PartyMember member, int index, float verticalOffset, Vector2 mousePos, BitmapFont defaultFont, BitmapFont secondaryFont, BitmapFont tertiaryFont)
        {
            if (!_visualPositions.ContainsKey(member)) return;
            if (!_verticalOffsets.ContainsKey(member)) return;

            float x = _visualPositions[member];
            float yOffset = _verticalOffsets[member] + verticalOffset;

            // Draw Background
            Vector2 cardPos = new Vector2(x, BaseY + 3 + yOffset);
            Vector2 cardSize = new Vector2(CARD_WIDTH, HUD_HEIGHT - 4);
            spriteBatch.Draw(_pixel, cardPos, null, _global.Palette_Black, 0f, Vector2.Zero, cardSize, SpriteEffects.None, 0f);

            bool isBeingDragged = (_isDragging && _draggedMember == member);

            if (isBeingDragged)
            {
                DrawHollowRectSmooth(spriteBatch, cardPos, cardSize, _global.Palette_Sun);
            }
            else
            {
                Rectangle cardRect = new Rectangle((int)x, (int)(BaseY + 3 + yOffset), CARD_WIDTH, HUD_HEIGHT - 4);
                if (cardRect.Contains(mousePos) && !_isDragging)
                {
                    DrawHollowRectSmooth(spriteBatch, cardPos, cardSize, _global.Palette_Pale);
                }
                else
                {
                    // NEW: Always draw a dark shadow border when idle
                    DrawHollowRectSmooth(spriteBatch, cardPos, cardSize, _global.Palette_DarkShadow);
                }
            }

            DrawCardContents(spriteBatch, gameTime, member, x, yOffset, index, defaultFont, secondaryFont, tertiaryFont);
        }

        private void DrawHollowRectSmooth(SpriteBatch spriteBatch, Vector2 pos, Vector2 size, Color color)
        {
            spriteBatch.Draw(_pixel, pos, null, color, 0f, Vector2.Zero, new Vector2(size.X, 1), SpriteEffects.None, 0f);
            spriteBatch.Draw(_pixel, new Vector2(pos.X, pos.Y + size.Y - 1), null, color, 0f, Vector2.Zero, new Vector2(size.X, 1), SpriteEffects.None, 0f);
            spriteBatch.Draw(_pixel, pos, null, color, 0f, Vector2.Zero, new Vector2(1, size.Y), SpriteEffects.None, 0f);
            spriteBatch.Draw(_pixel, new Vector2(pos.X + size.X - 1, pos.Y), null, color, 0f, Vector2.Zero, new Vector2(1, size.Y), SpriteEffects.None, 0f);
        }

        private void DrawCardContents(SpriteBatch spriteBatch, GameTime gameTime, PartyMember member, float xPosition, float yOffset, int index, BitmapFont defaultFont, BitmapFont secondaryFont, BitmapFont tertiaryFont)
        {
            float y = BaseY + 5 + yOffset;
            float centerX = xPosition + (CARD_WIDTH / 2f);

            string name = member.Name.ToUpper();
            Color nameColor = _global.Palette_LightPale;
            Vector2 nameSize = defaultFont.MeasureString(name);

            spriteBatch.DrawString(defaultFont, name, new Vector2(centerX - nameSize.X / 2f, y), nameColor);

            y += nameSize.Y - 4;

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

            spriteBatch.Draw(_spriteManager.PlayerMasterSpriteSheet, pos, sourceRect, Color.White, rotation, origin, 1.0f, SpriteEffects.None, 0f);

            y += 32 + 4;

            Texture2D hpBg = _spriteManager.InventoryPlayerHealthBarEmpty;
            if (hpBg != null)
            {
                float barX = centerX - (hpBg.Width / 2f);
                string hpText = $"{member.CurrentHP}/{member.MaxHP}";

                spriteBatch.DrawString(tertiaryFont, hpText, new Vector2(barX, (y - 9) - tertiaryFont.LineHeight + 1), _global.Palette_DarkShadow);
                spriteBatch.Draw(hpBg, new Vector2(barX, y - 8), Color.White);

                if (_spriteManager.InventoryPlayerHealthBarFull != null)
                {
                    float hpPercent = (float)member.CurrentHP / Math.Max(1, member.MaxHP);
                    int visibleWidth = (int)(_spriteManager.InventoryPlayerHealthBarFull.Width * hpPercent);
                    var srcRect = new Rectangle(0, 0, visibleWidth, _spriteManager.InventoryPlayerHealthBarFull.Height);
                    spriteBatch.Draw(_spriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, y - 8), srcRect, Color.White);
                }
            }

            string[] labels = { "STR", "INT", "TEN", "AGI" };
            string[] keys = { "Strength", "Intelligence", "Tenacity", "Agility" };
            float statBlockStartX = centerX - 30;

            for (int s = 0; s < 4; s++)
            {
                spriteBatch.DrawString(secondaryFont, labels[s], new Vector2(statBlockStartX, y), _global.Palette_DarkPale);

                Texture2D statBg = _spriteManager.InventoryStatBarEmpty;
                if (statBg != null)
                {
                    float pipX = statBlockStartX + 19;
                    float pipY = y + (secondaryFont.LineHeight / 2f) - (statBg.Height / 2f);
                    spriteBatch.Draw(statBg, new Vector2(pipX, pipY), Color.White);

                    if (_spriteManager.InventoryStatBarFull != null)
                    {
                        int val = _gameState.PlayerState.GetBaseStat(member, keys[s]);
                        int basePoints = Math.Clamp(val, 0, 10);
                        if (basePoints > 0)
                        {
                            var srcBase = new Rectangle(0, 0, basePoints * 4, 3);
                            spriteBatch.Draw(_spriteManager.InventoryStatBarFull, new Vector2(pipX, pipY), srcBase, Color.White);
                        }
                    }
                }
                y += secondaryFont.LineHeight + 1;
            }

            y += 2;

            float moveStartX = statBlockStartX - 6;
            DrawMoveName(spriteBatch, member.BasicMove, "bas", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
            DrawMoveName(spriteBatch, member.CoreMove, "cor", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
            DrawMoveName(spriteBatch, member.AltMove, "alt", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
        }

        private void DrawMoveName(SpriteBatch sb, MoveEntry? move, string label, float x, float centerX, ref float y, BitmapFont labelFont, BitmapFont font)
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

            float cardStartX = centerX - (CARD_WIDTH / 2f);
            int lineHeight = (int)font.LineHeight;

            Rectangle hitRect = new Rectangle((int)cardStartX, (int)y - 2, CARD_WIDTH, lineHeight + 4);
            bool isHovered = !_isDragging && _currentHoveredItem.HasValue && _currentHoveredItem.Value.Bounds == hitRect;

            if (isHovered && isMovePresent)
            {
                Color c = _global.Palette_Sun;
                // FIX: Use float coordinates for smooth rendering instead of snapping to hitRect integers
                DrawHollowRectSmooth(sb, new Vector2(cardStartX, y - 2), new Vector2(CARD_WIDTH, lineHeight + 4), c);
            }

            if (isMovePresent)
            {
                sb.DrawString(labelFont, label, new Vector2(x, y), _global.Palette_DarkShadow);
                float labelWidth = labelFont.MeasureString(label).Width;
                float labelEndX = x + labelWidth;
                float cardEndX = cardStartX + CARD_WIDTH;
                float availableWidth = cardEndX - labelEndX;
                float textWidth = font.MeasureString(text).Width;
                float textX = labelEndX + (availableWidth - textWidth) / 2f;
                sb.DrawString(font, text, new Vector2(textX, y), color);
            }
            else
            {
                Vector2 size = font.MeasureString(text);
                sb.DrawString(font, text, new Vector2(centerX - size.X / 2f, y), color);
            }

            _activeHitboxes.Add((hitRect, moveData, (int)centerX));
            y += lineHeight + 3;
        }
    }
}
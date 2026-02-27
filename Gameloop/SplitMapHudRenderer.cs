using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
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
        private const float HOVER_DELAY = 0.25f;
        private Vector2 _lastMousePos;
        private bool _isTooltipVisible;
        private PartyMember? _hoveredMember;

        // --- Drag & Drop State ---
        private PartyMember? _draggedMember;
        private bool _isDragging;
        private int _dragStartMouseY;
        private Vector2 _dragGrabOffsetFromCenter;
        private float _virtualPullX;
        private int _lastScreenMouseX;
        private PartyMember? _topmostMember;
        private float _currentCursorLift = 0f;

        // Input Latch
        private ButtonState _prevLeftButtonState = ButtonState.Released;
        private ButtonState _prevRightButtonState = ButtonState.Released;

        // --- Physics & Rotation State ---
        private float _currentDragVelocity = 0f;
        private float _currentDragRotation = 0f;
        private const float ROTATION_TILT_FACTOR = 0.001f;
        private const float MAX_ROTATION = 0.10f;
        private const float ROTATION_SMOOTHING = 10f;

        // --- Elastic Physics Constants ---
        private const float MAX_ELASTIC_STRETCH = 45f;
        private const float ELASTIC_STIFFNESS = 60f;
        private const float DRAG_TETHER_SPEED = 25f;
        private const float MAX_PULL_DISTANCE = 180f;
        private const float SWAP_THRESHOLD_FACTOR = 0.5f;

        public bool IsDragging => _isDragging;

        // --- Animation State ---
        private readonly Dictionary<PartyMember, float> _visualPositions = new Dictionary<PartyMember, float>();
        private readonly Dictionary<PartyMember, float> _verticalOffsets = new Dictionary<PartyMember, float>();
        private readonly Dictionary<PartyMember, float> _tagOffsets = new Dictionary<PartyMember, float>();
        private readonly Dictionary<PartyMember, int> _currentTagNumbers = new Dictionary<PartyMember, int>();

        // --- Flip State ---
        // 0.0 = Front, 1.0 = Back
        private readonly Dictionary<PartyMember, float> _flipTargets = new Dictionary<PartyMember, float>();
        private readonly Dictionary<PartyMember, float> _flipProgress = new Dictionary<PartyMember, float>();

        private const float FLIP_SPEED = 10f;
        private const int FLIP_BUTTON_SIZE = 8;

        // --- Ability Info Cache (Name, Description) ---
        private readonly Dictionary<string, (string Name, string Description)> _abilityInfoCache = new Dictionary<string, (string, string)>();

        private const float CARD_MOVE_SPEED = 15f;
        private const float CARD_DROP_SPEED = 20f;
        private const float ENTRY_Y_OFFSET = -16f;
        private const float DRAG_LIFT_OFFSET = -8f;
        private const float HOVER_LIFT_OFFSET = -1f;
        private const float TAG_DEFAULT_OFFSET = 4f;
        private const float TAG_HOVER_OFFSET = 2f;
        private const float TAG_HIDDEN_OFFSET = 12f;

        public SplitMapHudRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _pixel = ServiceLocator.Get<Texture2D>();
            _tooltipRenderer = new MoveTooltipRenderer();
        }

        /// <summary>
        /// Forces all cards to flip back to the front side (Target = 0).
        /// Called when the HUD loses focus.
        /// </summary>
        public void ResetAllFlips()
        {
            foreach (var member in _flipTargets.Keys.ToList())
            {
                _flipTargets[member] = 0f;
            }
        }

        public void Update(GameTime gameTime, Vector2 virtualMousePos, float verticalOffset)
        {
            var cursorManager = ServiceLocator.Get<CursorManager>();
            var party = _gameState.PlayerState.Party;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var currentMouseState = Mouse.GetState();

            float currentHudY = BaseY + verticalOffset;
            _hoveredMember = null;

            // --- 0. Update Flip Animations ---
            foreach (var member in party)
            {
                if (!_flipTargets.ContainsKey(member)) _flipTargets[member] = 0f;
                if (!_flipProgress.ContainsKey(member)) _flipProgress[member] = 0f;

                float target = _flipTargets[member];
                float current = _flipProgress[member];

                if (Math.Abs(current - target) > 0.01f)
                {
                    float move = FLIP_SPEED * dt;
                    if (current < target) _flipProgress[member] = Math.Min(target, current + move);
                    else _flipProgress[member] = Math.Max(target, current - move);
                }
                else
                {
                    _flipProgress[member] = target;
                }
            }

            // --- Drag Logic ---
            if (_isDragging && _draggedMember != null)
            {
                // Force flip back to front immediately if dragging
                _flipTargets[_draggedMember] = 0f;

                cursorManager.SetState(CursorState.Dragging);

                float scaleX = (float)graphicsDevice.Viewport.Width / Global.VIRTUAL_WIDTH;
                float scaleY = (float)graphicsDevice.Viewport.Height / Global.VIRTUAL_HEIGHT;
                float scale = Math.Min(scaleX, scaleY);

                int screenDeltaX = currentMouseState.X - _lastScreenMouseX;
                float virtualDeltaX = screenDeltaX / scale;

                int currentIndex = party.IndexOf(_draggedMember);
                int totalWidth = (party.Count * CARD_WIDTH) + ((party.Count > 0 ? party.Count - 1 : 0) * CARD_SPACING);
                int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;
                float anchorSlotX = startX + (currentIndex * (CARD_WIDTH + CARD_SPACING));

                float currentDiff = _virtualPullX - anchorSlotX;
                bool movingTowardsCenter = (currentDiff > 0 && virtualDeltaX < 0) || (currentDiff < 0 && virtualDeltaX > 0);

                if (movingTowardsCenter)
                {
                    float currentVisualOffset = MAX_ELASTIC_STRETCH * MathF.Tanh(currentDiff / ELASTIC_STIFFNESS);
                    float targetVisualOffset = currentVisualOffset + virtualDeltaX;
                    float maxVal = MAX_ELASTIC_STRETCH * 0.9999f;
                    targetVisualOffset = Math.Clamp(targetVisualOffset, -maxVal, maxVal);

                    float ratio = targetVisualOffset / MAX_ELASTIC_STRETCH;
                    float newDiff = ELASTIC_STIFFNESS * 0.5f * MathF.Log((1f + ratio) / (1f - ratio));

                    _virtualPullX = anchorSlotX + newDiff;
                }
                else
                {
                    _virtualPullX += virtualDeltaX;
                }

                float diff = _virtualPullX - anchorSlotX;
                diff = Math.Clamp(diff, -MAX_PULL_DISTANCE, MAX_PULL_DISTANCE);
                _virtualPullX = anchorSlotX + diff;

                float dampedDiff = MAX_ELASTIC_STRETCH * MathF.Tanh(diff / ELASTIC_STIFFNESS);
                float targetVisualX = anchorSlotX + dampedDiff;

                float prevVisualX = _visualPositions[_draggedMember];
                float tetherDamping = 1.0f - MathF.Exp(-DRAG_TETHER_SPEED * dt);
                float newVisualX = MathHelper.Lerp(prevVisualX, targetVisualX, tetherDamping);

                _currentDragVelocity = (newVisualX - prevVisualX) / dt;
                _visualPositions[_draggedMember] = newVisualX;

                float virtualMoveAmt = newVisualX - prevVisualX;
                int screenMoveAmt = (int)MathF.Round(virtualMoveAmt * scale);
                int newScreenMouseX = _lastScreenMouseX + screenMoveAmt;

                Mouse.SetPosition(newScreenMouseX, _dragStartMouseY);
                _lastScreenMouseX = newScreenMouseX;

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

                if (currentMouseState.LeftButton == ButtonState.Released)
                {
                    _isDragging = false;
                    _draggedMember = null;
                }

                goto ProcessTweening;
            }
            else
            {
                _currentDragVelocity = 0f;
            }

            // --- Card Hover & Drag Start Logic ---
            int totalW = (party.Count * CARD_WIDTH) + ((party.Count > 0 ? party.Count - 1 : 0) * CARD_SPACING);
            int sX = (Global.VIRTUAL_WIDTH - totalW) / 2;

            for (int i = 0; i < party.Count; i++)
            {
                var member = party[i];
                if (!_visualPositions.ContainsKey(member)) continue;

                float x = _visualPositions[member];
                // Added +3 to match Draw method offset. This aligns the hitbox with the visual.
                float y = currentHudY + 3 + (_verticalOffsets.ContainsKey(member) ? _verticalOffsets[member] : 0);

                // Use MathF.Round to match DrawSnapped logic for precise hitboxes
                int snappedX = (int)MathF.Round(x);
                int snappedY = (int)MathF.Round(y);

                Rectangle cardRect = new Rectangle(snappedX, snappedY, CARD_WIDTH, HUD_HEIGHT);

                // Flip Button Hitbox (Top-Left) - Snapped relative to card
                Rectangle flipBtnRect = new Rectangle(snappedX + 2, snappedY + 2, FLIP_BUTTON_SIZE, FLIP_BUTTON_SIZE);

                if (cardRect.Contains(virtualMousePos))
                {
                    // --- Right Click Flip (Anywhere on Card) ---
                    if (currentMouseState.RightButton == ButtonState.Pressed && _prevRightButtonState == ButtonState.Released && !_isDragging)
                    {
                        float currentTarget = _flipTargets[member];
                        _flipTargets[member] = (currentTarget == 0f) ? 1f : 0f;
                        ServiceLocator.Get<HapticsManager>().TriggerUICompoundShake(0.3f);
                    }

                    // 1. Check Flip Button First (Priority)
                    // Only interactable if we are hovering the card (which we are)
                    if (flipBtnRect.Contains(virtualMousePos))
                    {
                        cursorManager.SetState(CursorState.HoverClickable);
                        _hoveredMember = member; // Keep card hovered so it doesn't drop down

                        // Only trigger on FRESH click
                        if (currentMouseState.LeftButton == ButtonState.Pressed && _prevLeftButtonState == ButtonState.Released)
                        {
                            // Toggle Flip Target
                            float currentTarget = _flipTargets[member];
                            _flipTargets[member] = (currentTarget == 0f) ? 1f : 0f;

                            ServiceLocator.Get<HapticsManager>().TriggerUICompoundShake(0.3f);
                        }

                        // CRITICAL: If we are hovering the button, we DO NOT process drag logic for this card.
                        continue;
                    }

                    // 2. Normal Card Hover (Only if NOT hovering button)
                    cursorManager.SetState(CursorState.HoverDraggable);
                    _hoveredMember = member;

                    if (currentMouseState.LeftButton == ButtonState.Pressed && _prevLeftButtonState == ButtonState.Released && !_isDragging)
                    {
                        _isDragging = true;
                        _draggedMember = member;
                        _topmostMember = member;

                        // Force flip back to front immediately on interaction
                        _flipTargets[member] = 0f;

                        _dragStartMouseY = currentMouseState.Y;
                        _lastScreenMouseX = currentMouseState.X;

                        float grabOffset = _visualPositions[member] - virtualMousePos.X;
                        _virtualPullX = virtualMousePos.X + grabOffset;

                        float currentCardX = _visualPositions[member];
                        float currentCardY = BaseY + 3 + _verticalOffsets[member] + verticalOffset;
                        Vector2 cardCenter = new Vector2(currentCardX + CARD_WIDTH / 2f, currentCardY + (HUD_HEIGHT - 4) / 2f);

                        _dragGrabOffsetFromCenter = virtualMousePos - cardCenter;

                        _currentDragRotation = 0f;
                        _currentDragVelocity = 0f;
                    }
                }
            }

            // --- Tooltip Logic ---
            if (verticalOffset <= 1.0f)
            {
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
            }
            else
            {
                _currentHoveredItem = null;
                _hoverTimer = 0f;
                _isTooltipVisible = false;
            }

            _lastMousePos = virtualMousePos;

        ProcessTweening:
            // Update Input Latch
            _prevLeftButtonState = currentMouseState.LeftButton;
            _prevRightButtonState = currentMouseState.RightButton;

            float targetCursorLift = _isDragging ? DRAG_LIFT_OFFSET : 0f;
            float cursorDamping = 1.0f - MathF.Exp(-CARD_DROP_SPEED * dt);
            _currentCursorLift = MathHelper.Lerp(_currentCursorLift, targetCursorLift, cursorDamping);

            float targetRotation = _currentDragVelocity * ROTATION_TILT_FACTOR;
            targetRotation = Math.Clamp(targetRotation, -MAX_ROTATION, MAX_ROTATION);
            if (!_isDragging) targetRotation = 0f;

            float rotDamping = 1.0f - MathF.Exp(-ROTATION_SMOOTHING * dt);
            _currentDragRotation = MathHelper.Lerp(_currentDragRotation, targetRotation, rotDamping);

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
                    float targetY = 0f;
                    if (i >= 2) targetY = 2f;

                    if (member == _draggedMember) targetY = DRAG_LIFT_OFFSET;
                    else if (member == _hoveredMember) targetY = HOVER_LIFT_OFFSET;

                    float dampingY = 1.0f - MathF.Exp(-CARD_DROP_SPEED * dt);
                    _verticalOffsets[member] = MathHelper.Lerp(currentY, targetY, dampingY);
                }

                if (!_tagOffsets.ContainsKey(member))
                {
                    _tagOffsets[member] = TAG_DEFAULT_OFFSET;
                }

                if (!_currentTagNumbers.ContainsKey(member))
                {
                    _currentTagNumbers[member] = i + 1;
                }

                int actualNumber = i + 1;
                int currentNumber = _currentTagNumbers[member];
                float currentTagY = _tagOffsets[member];
                float targetTagY;

                if (currentNumber != actualNumber)
                {
                    targetTagY = TAG_HIDDEN_OFFSET;
                    if (currentTagY >= TAG_HIDDEN_OFFSET - 0.5f)
                    {
                        _currentTagNumbers[member] = actualNumber;
                    }
                }
                else
                {
                    targetTagY = (member == _hoveredMember || member == _draggedMember) ? TAG_HOVER_OFFSET : TAG_DEFAULT_OFFSET;
                }

                float dampingTag = 1.0f - MathF.Exp(-CARD_DROP_SPEED * dt);
                _tagOffsets[member] = MathHelper.Lerp(currentTagY, targetTagY, dampingTag);
            }

            var activeMembers = new HashSet<PartyMember>(party);
            var keysToRemove = _visualPositions.Keys.Where(k => !activeMembers.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _visualPositions.Remove(key);
                _verticalOffsets.Remove(key);
                _tagOffsets.Remove(key);
                _currentTagNumbers.Remove(key);
                _flipTargets.Remove(key);
                _flipProgress.Remove(key);
            }

            if (_isDragging)
            {
                cursorManager.Hide();
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

            float scale = core.FinalScale;
            float tx = core.FinalRenderRectangle.X;
            float ty = core.FinalRenderRectangle.Y;
            Matrix globalTransform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(tx, ty, 0f);

            float currentStartY = BaseY + verticalOffset;
            float t = Math.Clamp(verticalOffset / 24f, 0f, 1f);
            Color lineColor = Color.Lerp(_global.Palette_DarkPale, _global.Palette_DarkestPale, t);

            spriteBatch.DrawSnapped(_pixel, new Vector2(0, currentStartY), null, _global.Palette_Black, 0f, Vector2.Zero, new Vector2(Global.VIRTUAL_WIDTH, HUD_HEIGHT), SpriteEffects.None, 0f);

            int dotSize = 1;
            int gapSize = 1;
            Vector2 dotScale = new Vector2(dotSize, 1);

            for (int x = 0; x < Global.VIRTUAL_WIDTH; x += (dotSize + gapSize))
            {
                spriteBatch.DrawSnapped(_pixel, new Vector2(x, currentStartY), null, lineColor, 0f, Vector2.Zero, dotScale, SpriteEffects.None, 0f);
            }

            var party = _gameState.PlayerState.Party;
            int count = party.Count;
            var mousePos = Core.TransformMouse(Mouse.GetState().Position);

            for (int i = 0; i < count; i++)
            {
                var member = party[i];
                if (member == _topmostMember) continue;
                DrawMemberCard(spriteBatch, gameTime, member, i, verticalOffset, mousePos, defaultFont, secondaryFont, tertiaryFont, globalTransform);
            }

            if (_topmostMember != null && party.Contains(_topmostMember))
            {
                int index = party.IndexOf(_topmostMember);
                DrawMemberCard(spriteBatch, gameTime, _topmostMember, index, verticalOffset, mousePos, defaultFont, secondaryFont, tertiaryFont, globalTransform);

                if (_isDragging && _draggedMember == _topmostMember)
                {
                    var (cursorTex, cursorFrames) = _spriteManager.GetCursorAnimation("cursor_dragging_draggable");
                    if (cursorTex != null && cursorFrames.Length > 0)
                    {
                        float x = _visualPositions[_draggedMember];
                        float yOffset = _verticalOffsets[_draggedMember] + verticalOffset;
                        Vector2 cardCenter = new Vector2(x + CARD_WIDTH / 2f, BaseY + 3 + yOffset + (HUD_HEIGHT - 4) / 2f);
                        Vector2 rotatedOffset = Vector2.Transform(_dragGrabOffsetFromCenter, Matrix.CreateRotationZ(_currentDragRotation));
                        Vector2 cursorPos = cardCenter + rotatedOffset;
                        spriteBatch.DrawSnapped(cursorTex, cursorPos, cursorFrames[0], Color.White, 0f, new Vector2(7, 7), 1.0f, SpriteEffects.None, 0f);
                    }
                }
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

        private void DrawMemberCard(SpriteBatch spriteBatch, GameTime gameTime, PartyMember member, int index, float verticalOffset, Vector2 mousePos, BitmapFont defaultFont, BitmapFont secondaryFont, BitmapFont tertiaryFont, Matrix globalTransform)
        {
            if (!_visualPositions.ContainsKey(member)) return;
            if (!_verticalOffsets.ContainsKey(member)) return;

            float x = _visualPositions[member];
            float yOffset = _verticalOffsets[member] + verticalOffset;
            bool isBeingDragged = (_isDragging && _draggedMember == member);

            // --- Flip Animation Math ---
            float flipP = _flipProgress.ContainsKey(member) ? _flipProgress[member] : 0f;
            // Scale goes 1 -> 0 -> 1. 
            // 0.0 to 0.5 is Front shrinking. 0.5 to 1.0 is Back expanding.
            float scaleX = Math.Abs(1f - 2f * flipP);
            bool showBack = flipP > 0.5f;

            // --- Flip Hop Effect ---
            float flipHop = MathF.Sin(flipP * MathHelper.Pi) * -5.0f;
            yOffset += flipHop;

            // --- Matrix Transformation ---
            // We combine Rotation (Drag) and Scale (Flip)
            bool useTransform = isBeingDragged || flipP > 0.01f;

            Vector2 cardCenter = new Vector2(MathF.Round(x + CARD_WIDTH / 2f), MathF.Round(BaseY + 3 + yOffset + (HUD_HEIGHT - 4) / 2f));

            if (useTransform)
            {
                spriteBatch.End();

                float rotation = isBeingDragged ? _currentDragRotation : 0f;

                Matrix localTransform = Matrix.CreateTranslation(new Vector3(-cardCenter, 0)) *
                                        Matrix.CreateScale(scaleX, 1f, 1f) *
                                        Matrix.CreateRotationZ(rotation) *
                                        Matrix.CreateTranslation(new Vector3(cardCenter, 0));

                Matrix finalTransform = localTransform * globalTransform;

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, finalTransform);
            }

            // --- DRAW SLOT TAG (Behind Card) ---
            float tagOffset = _tagOffsets.ContainsKey(member) ? _tagOffsets[member] : TAG_DEFAULT_OFFSET;
            int displayedNumber = _currentTagNumbers.ContainsKey(member) ? _currentTagNumbers[member] : (index + 1);
            DrawSlotTag(spriteBatch, x, yOffset, displayedNumber, tertiaryFont, tagOffset, showBack);

            // Draw Background
            int snappedX = (int)MathF.Round(x);
            int snappedY = (int)MathF.Round(BaseY + 3 + yOffset);
            Vector2 cardPos = new Vector2(snappedX, snappedY);
            Vector2 cardSize = new Vector2(CARD_WIDTH, HUD_HEIGHT - 4);
            spriteBatch.DrawSnapped(_pixel, cardPos, null, _global.Palette_Black, 0f, Vector2.Zero, cardSize, SpriteEffects.None, 0f);

            // Border Color
            Color borderColor;
            if (isBeingDragged) borderColor = _global.Palette_Sun;
            else
            {
                // Use snapped coordinates for hit testing to match visual rendering
                Rectangle cardRect = new Rectangle(snappedX, snappedY, CARD_WIDTH, HUD_HEIGHT - 4);

                if (cardRect.Contains(mousePos) && !_isDragging) borderColor = _global.Palette_Pale;
                else borderColor = (index >= 2) ? _global.Palette_DarkestPale : _global.Palette_DarkPale;
            }
            DrawHollowRectSmooth(spriteBatch, cardPos, cardSize, borderColor);

            // --- Draw Content (Front or Back) ---
            if (showBack)
            {
                DrawCardBack(spriteBatch, member, x, yOffset, defaultFont, secondaryFont, tertiaryFont);
            }
            else
            {
                DrawCardContents(spriteBatch, gameTime, member, x, yOffset, index, defaultFont, secondaryFont, tertiaryFont);
            }

            // --- Draw Flip Button ---
            // Only draw if the card is wide enough to look good AND we are hovering this specific card
            if (scaleX > 0.2f && member == _hoveredMember && !_isDragging)
            {
                var flipIcon = _spriteManager.CardFlipIcon;
                if (flipIcon != null)
                {
                    // Determine hover state for the button sprite
                    Rectangle btnRect = new Rectangle(snappedX + 2, snappedY + 2, FLIP_BUTTON_SIZE, FLIP_BUTTON_SIZE);
                    bool btnHover = btnRect.Contains(mousePos);

                    // Source rect: 0=Idle, 1=Hover. 8x8 sprites.
                    Rectangle src = new Rectangle(btnHover ? 8 : 0, 0, 8, 8);

                    // Use Vector2 for position to ensure it snaps exactly like the card body
                    Vector2 btnPos = new Vector2(snappedX + 2, snappedY + 2);

                    // Draw a small backing to make it easier to see
                    spriteBatch.DrawSnapped(_pixel, btnPos, null, _global.Palette_Black * 0.6f, 0f, Vector2.Zero, new Vector2(8, 8), SpriteEffects.None, 0f);

                    spriteBatch.DrawSnapped(flipIcon, btnPos, src, Color.White);
                }
            }

            if (useTransform)
            {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, globalTransform);
            }
        }

        private void DrawCardBack(SpriteBatch spriteBatch, PartyMember member, float xPosition, float yOffset, BitmapFont defaultFont, BitmapFont secondaryFont, BitmapFont tertiaryFont)
        {
            // Use dynamic coordinates to ensure sync with card body
            float y = BaseY + 5 + yOffset;
            float centerX = xPosition + (CARD_WIDTH / 2f);
            float maxWidth = CARD_WIDTH - 4; // 2px padding on sides

            // 1. Header: INFO
            string title = "INFO";
            Vector2 titleSize = defaultFont.MeasureString(title);
            // Snap text position relative to center
            spriteBatch.DrawStringSnapped(defaultFont, title, new Vector2(centerX - MathF.Floor(titleSize.X / 2f), y), _global.Palette_Rust);
            y += 12; // Spacing

            // 2. Section: ABILITY
            string abilityLabel = "ABILITY";
            Vector2 abilityLabelSize = tertiaryFont.MeasureString(abilityLabel);
            spriteBatch.DrawStringSnapped(tertiaryFont, abilityLabel, new Vector2(centerX - MathF.Floor(abilityLabelSize.X / 2f), y), _global.Palette_DarkPale);
            y += tertiaryFont.LineHeight + 1;

            // Ability Name & Desc
            string abilityName = "NONE";
            string abilityDesc = "";

            if (member.IntrinsicAbilities != null && member.IntrinsicAbilities.Count > 0)
            {
                var kvp = member.IntrinsicAbilities.First();
                string abilityId = kvp.Key;

                var info = GetAbilityInfo(abilityId);

                abilityName = info.Name;
                abilityDesc = string.IsNullOrEmpty(kvp.Value) ? info.Description : kvp.Value;
            }

            // Draw Name (Secondary, Centered, Highlighted)
            Vector2 nameSize = secondaryFont.MeasureString(abilityName);
            spriteBatch.DrawStringSnapped(secondaryFont, abilityName, new Vector2(centerX - MathF.Floor(nameSize.X / 2f), y), _global.Palette_LightPale);
            y += secondaryFont.LineHeight + 1;

            // Draw Description (Tertiary, Centered, Wrapped)
            if (!string.IsNullOrEmpty(abilityDesc))
            {
                var words = abilityDesc.Split(' ');
                string line = "";
                foreach (var word in words)
                {
                    string testLine = string.IsNullOrEmpty(line) ? word : line + " " + word;
                    if (tertiaryFont.MeasureString(testLine).Width > maxWidth)
                    {
                        // Draw current line
                        Vector2 lineSize = tertiaryFont.MeasureString(line);
                        spriteBatch.DrawStringSnapped(tertiaryFont, line, new Vector2(centerX - MathF.Floor(lineSize.X / 2f), y), _global.Palette_Pale);
                        y += tertiaryFont.LineHeight + 2; // Line spacing
                        line = word;
                    }
                    else
                    {
                        line = testLine;
                    }
                }
                // Draw last line
                if (!string.IsNullOrEmpty(line))
                {
                    Vector2 lineSize = tertiaryFont.MeasureString(line);
                    spriteBatch.DrawStringSnapped(tertiaryFont, line, new Vector2(centerX - MathF.Floor(lineSize.X / 2f), y), _global.Palette_Pale);
                    y += tertiaryFont.LineHeight + 2; // Line spacing
                }
            }

            y += 4; // Gap before Status

            // 3. Section: STATUS
            string statusLabel = "STATUS";
            Vector2 statusLabelSize = tertiaryFont.MeasureString(statusLabel);
            spriteBatch.DrawStringSnapped(tertiaryFont, statusLabel, new Vector2(centerX - MathF.Floor(statusLabelSize.X / 2f), y), _global.Palette_DarkPale);
            y += tertiaryFont.LineHeight + 1;

            string statusText = member.ActiveBuffs.Count > 0 ? $"{member.ActiveBuffs.Count} EFF" : "NORMAL";
            Vector2 statusTextSize = secondaryFont.MeasureString(statusText);
            spriteBatch.DrawStringSnapped(secondaryFont, statusText, new Vector2(centerX - MathF.Floor(statusTextSize.X / 2f), y), _global.Palette_LightPale);
        }

        /// <summary>
        /// Helper to retrieve ability info (Name, Description) from the class definition if missing from instance data.
        /// </summary>
        private (string Name, string Description) GetAbilityInfo(string abilityId)
        {
            if (_abilityInfoCache.TryGetValue(abilityId, out var cachedInfo))
            {
                return cachedInfo;
            }

            string friendlyName = abilityId; // Default to ID if not found
            string description = "";

            try
            {
                // Assume naming convention "AbilityName" + "Ability"
                var typeName = $"ProjectVagabond.Battle.Abilities.{abilityId}Ability";
                var type = Type.GetType(typeName);

                // Fallback search in all assemblies if not found directly
                if (type == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = asm.GetType(typeName);
                        if (type != null) break;
                    }
                }

                if (type != null)
                {
                    // Attempt to create an instance. Most passive abilities are parameterless.
                    // If it requires parameters, this might fail, but the try-catch handles it.
                    var instance = Activator.CreateInstance(type) as IAbility;
                    if (instance != null)
                    {
                        friendlyName = instance.Name;
                        description = instance.Description;

                        var info = (friendlyName, description);
                        _abilityInfoCache[abilityId] = info;
                        return info;
                    }
                }
            }
            catch
            {
                // Silently fail
            }

            // Cache failure to prevent retry
            var fallback = (friendlyName, description);
            _abilityInfoCache[abilityId] = fallback;
            return fallback;
        }

        private void DrawSlotTag(SpriteBatch sb, float x, float yOffset, int displayedNumber, BitmapFont font, float tagOffset, bool isFlipped)
        {
            float tagWidth = 7f;
            float tagHeight = 10f;
            float visibleHeight = 10f;

            // If flipped, position on the right side
            float tagX = isFlipped ? (x + CARD_WIDTH - 7f - tagWidth) : (x + 7f);

            float cardTopY = BaseY + 3 + yOffset;
            float tagY = cardTopY - visibleHeight + tagOffset;

            sb.DrawSnapped(_pixel, new Vector2(tagX, tagY), null, _global.Palette_DarkestPale, 0f, Vector2.Zero, new Vector2(tagWidth, tagHeight - 1), SpriteEffects.None, 0f);

            string num = displayedNumber.ToString();
            Vector2 size = font.MeasureString(num);
            float textX = tagX + MathF.Floor((tagWidth - size.X) / 2f);
            float textY = tagY + MathF.Floor((visibleHeight - size.Y) / 2f) - 2f;

            sb.DrawStringSnapped(font, num, new Vector2(textX, textY), displayedNumber == 1 || displayedNumber == 2 ? _global.Palette_LightPale : _global.Palette_DarkPale);
        }

        private void DrawHollowRectSmooth(SpriteBatch spriteBatch, Vector2 pos, Vector2 size, Color color)
        {
            spriteBatch.DrawSnapped(_pixel, pos, null, color, 0f, Vector2.Zero, new Vector2(size.X, 1), SpriteEffects.None, 0f);
            spriteBatch.DrawSnapped(_pixel, new Vector2(pos.X, pos.Y + size.Y - 1), null, color, 0f, Vector2.Zero, new Vector2(size.X, 1), SpriteEffects.None, 0f);
            spriteBatch.DrawSnapped(_pixel, pos, null, color, 0f, Vector2.Zero, new Vector2(1, size.Y), SpriteEffects.None, 0f);
            spriteBatch.DrawSnapped(_pixel, new Vector2(pos.X + size.X - 1, pos.Y), null, color, 0f, Vector2.Zero, new Vector2(1, size.Y), SpriteEffects.None, 0f);
        }

        private void DrawCardContents(SpriteBatch spriteBatch, GameTime gameTime, PartyMember member, float xPosition, float yOffset, int index, BitmapFont defaultFont, BitmapFont secondaryFont, BitmapFont tertiaryFont)
        {
            // FIX: Round the base coordinates so all subsequent additions are integers
            float y = MathF.Round(BaseY + 5 + yOffset);
            float centerX = MathF.Round(xPosition + (CARD_WIDTH / 2f));

            string name = member.Name.ToUpper();
            Color nameColor = _global.Palette_LightPale;
            Vector2 nameSize = defaultFont.MeasureString(name);

            spriteBatch.DrawStringSnapped(defaultFont, name, new Vector2(centerX - MathF.Floor(nameSize.X / 2f), y), nameColor);

            y += nameSize.Y - 4;

            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            float bobSpeed = 3f;
            float wavePhase = index * 1.0f;
            float sineValue = MathF.Sin(time * bobSpeed + wavePhase);

            // Determine if we are in the "Alt" frame state
            bool isAltFrame = sineValue < 0;

            // Head bobs up 1 pixel (-1 Y) exactly when the Alt frame is active
            float bobOffset = isAltFrame ? -1f : 0f;

            Vector2 origin = new Vector2(16, 16);

            // Draw Body (Static Y position)
            PlayerSpriteType bodyType = isAltFrame ? PlayerSpriteType.BodyAlt : PlayerSpriteType.BodyNormal;
            var bodySourceRect = _spriteManager.GetPlayerSourceRect(member.PortraitIndex, bodyType);

            Vector2 bodyPos = new Vector2(centerX, y + 16);
            spriteBatch.DrawSnapped(_spriteManager.PlayerMasterSpriteSheet, bodyPos, bodySourceRect, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);

            // Draw Head (Applies bobOffset)
            PlayerSpriteType type = isAltFrame ? PlayerSpriteType.Alt : PlayerSpriteType.Normal;
            var sourceRect = _spriteManager.GetPlayerSourceRect(member.PortraitIndex, type);

            Vector2 pos = new Vector2(centerX, y + 16 + bobOffset);
            spriteBatch.DrawSnapped(_spriteManager.PlayerMasterSpriteSheet, pos, sourceRect, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);

            y += 32 + 4;

            Texture2D hpBg = _spriteManager.InventoryPlayerHealthBarEmpty;
            if (hpBg != null)
            {
                float barX = centerX - MathF.Floor(hpBg.Width / 2f);
                string hpText = $"{member.CurrentHP}/{member.MaxHP}";

                spriteBatch.DrawStringSnapped(tertiaryFont, hpText, new Vector2(barX, (y - 9) - tertiaryFont.LineHeight + 1), _global.Palette_DarkPale);
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
            float statBlockStartX = centerX - 30;

            for (int s = 0; s < 4; s++)
            {
                spriteBatch.DrawStringSnapped(secondaryFont, labels[s], new Vector2(statBlockStartX, y), _global.Palette_DarkPale);

                Texture2D statBg = _spriteManager.InventoryStatBarEmpty;
                if (statBg != null)
                {
                    float pipX = statBlockStartX + 19;
                    float pipY = y + MathF.Ceiling((secondaryFont.LineHeight - statBg.Height) / 2f);
                    spriteBatch.DrawSnapped(statBg, new Vector2(pipX, pipY), Color.White);

                    if (_spriteManager.InventoryStatBarFull != null)
                    {
                        int val = _gameState.PlayerState.GetBaseStat(member, keys[s]);
                        int basePoints = Math.Clamp(val, 0, 10);
                        if (basePoints > 0)
                        {
                            var srcBase = new Rectangle(0, 0, basePoints * 4, 3);
                            Color pipColor = Color.White;
                            if (basePoints >= 8) pipColor = _global.StatColor_High;
                            else if (basePoints >= 4) pipColor = _global.StatColor_Average;
                            else pipColor = _global.StatColor_Low;

                            spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarFull, new Vector2(pipX, pipY), srcBase, pipColor);
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

            if (isHovered && isMovePresent && _isTooltipVisible)
            {
                Color c = _global.Palette_Sun;
                DrawHollowRectSmooth(sb, new Vector2(cardStartX, y - 2), new Vector2(CARD_WIDTH, lineHeight + 4), c);
            }

            if (isMovePresent)
            {
                sb.DrawStringSnapped(labelFont, label, new Vector2(x, y), _global.Palette_DarkShadow);
                float labelWidth = labelFont.MeasureString(label).Width;
                float labelEndX = x + labelWidth;
                float cardEndX = cardStartX + CARD_WIDTH;
                float availableWidth = cardEndX - labelEndX;
                float textWidth = font.MeasureString(text).Width;
                float textX = labelEndX + MathF.Floor((availableWidth - textWidth) / 2f);
                sb.DrawStringSnapped(font, text, new Vector2(textX, y), color);
            }
            else
            {
                Vector2 size = font.MeasureString(text);
                sb.DrawStringSnapped(font, text, new Vector2(centerX - MathF.Floor(size.X / 2f), y), color);
            }

            _activeHitboxes.Add((hitRect, moveData, (int)centerX));
            y += lineHeight + 3;
        }
    }
}
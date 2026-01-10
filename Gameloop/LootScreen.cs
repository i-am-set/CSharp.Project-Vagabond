using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Items;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class LootScreen
    {
        // Dependencies
        private Global _global;
        private SpriteManager _spriteManager;
        private GameState _gameState;
        private HapticsManager _hapticsManager;
        private ItemTooltipRenderer _tooltipRenderer;

        // State
        public bool IsActive { get; private set; }

        // Wrapper class to manage item state + animation state together
        private class LootCard
        {
            public BaseItem Item;
            public UIAnimator Animator;
            public bool IsCollected; // True if clicked/collecting, but animation not finished
            public bool IsMouseHovering; // True if mouse is physically over the rect (Instant feedback)
            public Rectangle CurrentBounds;

            // Layout Animation
            public Vector2 VisualPosition; // Current X,Y for drawing
            public Vector2 TargetPosition; // Where it wants to go

            // Tracer State
            public List<Point> TracerPath; // The ordered list of pixels around the perimeter
            public float TracerProgress;   // Current index (float) along the path
        }
        private List<LootCard> _cards;

        // Layout Constants
        private Rectangle _lootArea;
        private const int CARD_SIZE = 32;
        private const int AREA_WIDTH = 90;
        private const int AREA_HEIGHT = 60;
        private const float CARD_MOVE_SPEED = 10f; // Speed of re-centering tween

        // --- TUNING: Hover Animation Speeds ---
        private const float LOOT_HOVER_FLOAT_SPEED = 3.0f;    // Multiplier for the figure-8 sway speed
        private const float LOOT_HOVER_ROTATION_SPEED = 1.0f; // Multiplier for the tilt speed
        private const float LOOT_HOVER_FLOAT_DISTANCE = 1.0f; // Max distance to sway (pixels)

        // --- TUNING: Rarity Tracer ---
        private const float TRACER_SPEED = 30f; // Pixels per second
        private const int TRAIL_LENGTH = 8;     // Number of pixels in the tail
        private const float TRAIL_START_ALPHA = 1.0f;
        private const float TRAIL_END_ALPHA = 0.0f;

        // --- TUNING: Rarity Pulse ---
        private const float PULSE_SPEED = 4.0f; // Speed of the sine wave

        // Buttons
        private Button _collectAllButton;
        private Button _skipButton;

        // Input State
        private MouseState _prevMouse;

        // Collection Sequence State
        private bool _isCollectingAll = false;
        private float _collectTimer = 0f;
        private const float COLLECT_DELAY = 0.1f;

        // Tooltip State
        private object? _hoveredItemData;
        private object? _lastHoveredItemData;
        private float _tooltipTimer = 0f;
        private const float TOOLTIP_DELAY = 0.6f;

        // Tooltip Animation
        private UIAnimator _tooltipAnimator;

        public LootScreen()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _tooltipRenderer = ServiceLocator.Get<ItemTooltipRenderer>();
            _cards = new List<LootCard>();

            // Initialize Tooltip Animator
            _tooltipAnimator = new UIAnimator
            {
                EntryStyle = EntryExitStyle.Pop,
                DurationIn = 0.2f,
                DurationOut = 0.1f // Instant hide handled by Reset(), but good to have
            };

            // Center the loot area
            int x = (Global.VIRTUAL_WIDTH - AREA_WIDTH) / 2;
            int y = (Global.VIRTUAL_HEIGHT - AREA_HEIGHT) / 2 - 24;
            _lootArea = new Rectangle(x, y, AREA_WIDTH, AREA_HEIGHT);

            // Initialize Control Buttons
            int btnY = _lootArea.Bottom + 20;

            _collectAllButton = new Button(new Rectangle(x, btnY, 80, 15), "COLLECT ALL", font: ServiceLocator.Get<Core>().SecondaryFont);
            _collectAllButton.OnClick += StartCollectAllSequence;

            _skipButton = new Button(new Rectangle(_lootArea.Right - 60, btnY, 60, 15), "SKIP", font: ServiceLocator.Get<Core>().SecondaryFont);
            _skipButton.OnClick += SkipAll;
        }

        public void Show(List<BaseItem> loot)
        {
            _cards.Clear();
            IsActive = true;
            _prevMouse = Mouse.GetState();
            _isCollectingAll = false;
            _collectTimer = 0f;
            _hoveredItemData = null;
            _lastHoveredItemData = null;
            _tooltipTimer = 0f;
            _tooltipAnimator.Reset();

            if (loot != null)
            {
                // Pre-calculate initial positions so they don't fly in from (0,0)
                int count = loot.Count;
                float segmentWidth = (float)_lootArea.Width / count;
                int cardY = _lootArea.Center.Y - (CARD_SIZE / 2);

                for (int i = 0; i < count; i++)
                {
                    var animator = new UIAnimator
                    {
                        EntryStyle = EntryExitStyle.PopJiggle,
                        ExitStyle = EntryExitStyle.JuicyCollect, // Juicy spring animation
                        IdleStyle = IdleAnimationType.None, // No idle bobbing (Juicy handles it)

                        // --- JUICY HOVER CONFIGURATION ---
                        HoverStyle = HoverAnimationType.Juicy,
                        HoverScale = 1.0f,      // Locked to 1.0x to prevent mangling
                        HoverLift = -3f,       // Lift up 3 pixels
                        InteractionSpeed = 55f, // Very snappy response

                        // Apply Tunable Speeds & Distance
                        HoverSwaySpeed = LOOT_HOVER_FLOAT_SPEED,
                        HoverRotationSpeed = LOOT_HOVER_ROTATION_SPEED,
                        HoverSwayDistance = LOOT_HOVER_FLOAT_DISTANCE,

                        DurationIn = 0.4f,
                        DurationOut = 0.15f // Snappy exit (was 0.35f)
                    };
                    // Stagger entrance
                    animator.Show(delay: i * 0.1f);

                    // Calculate initial centered position
                    float centerX = _lootArea.X + (segmentWidth * i) + (segmentWidth / 2f);
                    float targetX = centerX - (CARD_SIZE / 2f);
                    Vector2 pos = new Vector2(targetX, cardY);

                    // Generate Tracer Path
                    var icon = _spriteManager.GetItemSprite(loot[i].SpritePath);
                    var path = GenerateTracerPath(icon);

                    _cards.Add(new LootCard
                    {
                        Item = loot[i],
                        Animator = animator,
                        IsCollected = false,
                        IsMouseHovering = false,
                        VisualPosition = pos,
                        TargetPosition = pos,
                        CurrentBounds = new Rectangle((int)pos.X, (int)pos.Y, CARD_SIZE, CARD_SIZE),
                        TracerPath = path,
                        TracerProgress = 0f
                    });
                }
            }
        }

        /// <summary>
        /// Generates an ordered list of points representing the outer perimeter of the sprite's outline.
        /// </summary>
        private List<Point> GenerateTracerPath(Texture2D texture)
        {
            if (texture == null) return new List<Point>();

            int w = texture.Width;
            int h = texture.Height;
            Color[] data = new Color[w * h];
            texture.GetData(data);

            // 1. Create a boolean grid representing the "Dilated" shape (Sprite + 1px Outline)
            // We add padding to the grid to handle the expansion
            int gridW = w + 4; // +2 for dilation, +2 for safety padding
            int gridH = h + 4;
            bool[,] grid = new bool[gridW, gridH];

            // Offset to center the original sprite in the grid
            int offsetX = 2;
            int offsetY = 2;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (data[y * w + x].A > 0)
                    {
                        // Mark this pixel and all 8 neighbors as solid
                        // This simulates the 1px outline added by the silhouette renderer
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                grid[x + offsetX + dx, y + offsetY + dy] = true;
                            }
                        }
                    }
                }
            }

            // 2. Moore-Neighbor Tracing to find the perimeter
            List<Point> path = new List<Point>();
            Point start = Point.Zero;
            bool foundStart = false;

            // Find start point (Top-Left most pixel)
            for (int y = 0; y < gridH; y++)
            {
                for (int x = 0; x < gridW; x++)
                {
                    if (grid[x, y])
                    {
                        start = new Point(x, y);
                        foundStart = true;
                        break;
                    }
                }
                if (foundStart) break;
            }

            if (!foundStart) return path; // Empty sprite

            // Tracing
            Point current = start;
            Point backtrack = new Point(start.X - 1, start.Y); // Enter from left

            // Safety limit
            int maxSteps = gridW * gridH * 2;
            int steps = 0;

            // Moore-Neighbor offsets (Clockwise)
            Point[] offsets = {
                new Point(0, -1), new Point(1, -1), new Point(1, 0), new Point(1, 1),
                new Point(0, 1), new Point(-1, 1), new Point(-1, 0), new Point(-1, -1)
            };

            do
            {
                path.Add(current);

                // Find the neighbor to move to
                // Start checking from the backtrack direction, moving clockwise
                int checkIndex = -1;

                // Find index of backtrack relative to current
                Point relBack = new Point(backtrack.X - current.X, backtrack.Y - current.Y);
                for (int i = 0; i < 8; i++)
                {
                    if (offsets[i] == relBack)
                    {
                        checkIndex = i;
                        break;
                    }
                }

                // Start checking clockwise from backtrack
                bool foundNext = false;
                for (int i = 1; i <= 8; i++) // Check all 8 neighbors
                {
                    int idx = (checkIndex + i) % 8;
                    Point neighbor = new Point(current.X + offsets[idx].X, current.Y + offsets[idx].Y);

                    if (neighbor.X >= 0 && neighbor.X < gridW && neighbor.Y >= 0 && neighbor.Y < gridH && grid[neighbor.X, neighbor.Y])
                    {
                        // Backtrack is the previous empty pixel in the scan.
                        // Simplified: Just set backtrack to the pixel *before* the one we found in the clockwise sweep.
                        int prevIdx = (idx + 7) % 8;
                        backtrack = new Point(current.X + offsets[prevIdx].X, current.Y + offsets[prevIdx].Y);

                        current = neighbor;
                        foundNext = true;
                        break;
                    }
                }

                if (!foundNext) break; // Isolated pixel

                steps++;
            } while (current != start && steps < maxSteps);

            // 3. Convert Grid Coordinates back to Texture Relative Coordinates
            // Grid (2,2) corresponds to Texture (0,0).
            // We want coordinates relative to the texture's top-left (0,0).
            for (int i = 0; i < path.Count; i++)
            {
                path[i] = new Point(path[i].X - offsetX, path[i].Y - offsetY);
            }

            return path;
        }

        public void Close()
        {
            IsActive = false;
            _cards.Clear();
            _isCollectingAll = false;
            _tooltipAnimator.Reset();
        }

        public void Reset()
        {
            Close();
            _collectAllButton.ResetAnimationState();
            _skipButton.ResetAnimationState();
        }

        private void StartCollectAllSequence()
        {
            if (_cards.Count == 0)
            {
                Close();
                return;
            }

            _isCollectingAll = true;
            _collectTimer = 0f; // Immediate start
        }

        private void SkipAll()
        {
            // Trigger exit animation for all cards
            foreach (var card in _cards)
            {
                if (!card.IsCollected)
                {
                    // Bake current offset to prevent snapping
                    Vector2 currentOffset = card.Animator.GetCurrentOffset();
                    card.VisualPosition += currentOffset;
                    card.Animator.ForceOffset(Vector2.Zero);

                    // Skip style: Slide Down
                    card.Animator.Hide(delay: 0f, overrideStyle: EntryExitStyle.SlideDown);
                    card.IsCollected = true; // Mark as collected so we don't interact with them
                }
            }
        }

        private void CollectCard(LootCard card)
        {
            if (card.IsCollected) return;

            // Bake the current hover/animation offset into the visual position
            // so the exit animation starts exactly where the item currently is.
            Vector2 currentOffset = card.Animator.GetCurrentOffset();
            card.VisualPosition += currentOffset;

            // Reset the animator's offset so it doesn't double-apply
            card.Animator.ForceOffset(Vector2.Zero);

            card.IsCollected = true;
            AddItemToInventory(card.Item);

            // Trigger the juicy exit animation
            card.Animator.Hide();

            _hapticsManager.TriggerCompoundShake(0.2f);
        }

        private void AddItemToInventory(BaseItem item)
        {
            switch (item.Type)
            {
                case ItemType.Weapon: _gameState.PlayerState.AddWeapon(item.ID); break;
                case ItemType.Armor: _gameState.PlayerState.AddArmor(item.ID); break;
                case ItemType.Relic: _gameState.PlayerState.AddRelic(item.ID); break;
            }
        }

        public void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            MouseState mouse = Mouse.GetState();
            Vector2 mousePos = Core.TransformMouse(mouse.Position);
            bool clicked = mouse.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed;

            // --- Collect All Sequence Logic ---
            if (_isCollectingAll)
            {
                _collectTimer -= dt;
                if (_collectTimer <= 0f)
                {
                    // Find next uncollected card
                    var nextCard = _cards.FirstOrDefault(c => !c.IsCollected);
                    if (nextCard != null)
                    {
                        CollectCard(nextCard);
                        _collectTimer = COLLECT_DELAY;
                    }
                }
                // Block other input while collecting
                _prevMouse = mouse;
            }
            else
            {
                _collectAllButton.Update(mouse);
                _skipButton.Update(mouse);
            }

            // Recalculate layout targets based on remaining cards
            UpdateCardLayout();

            // 1. Update Animators & Positions for ALL cards
            foreach (var card in _cards)
            {
                // Tween position
                // Only tween if not collected (collected cards stay put and animate out)
                if (!card.IsCollected)
                {
                    card.VisualPosition = Vector2.Lerp(card.VisualPosition, card.TargetPosition, dt * CARD_MOVE_SPEED);

                    // Update bounds for hit detection
                    card.CurrentBounds = new Rectangle(
                        (int)card.VisualPosition.X,
                        (int)card.VisualPosition.Y,
                        CARD_SIZE,
                        CARD_SIZE
                    );

                    // Update Tracer
                    if (card.TracerPath != null && card.TracerPath.Count > 0)
                    {
                        // Get rarity-specific speed
                        var config = GetTracerConfig(card.Item.Rarity);
                        card.TracerProgress += dt * config.Speed;
                        if (card.TracerProgress >= card.TracerPath.Count)
                        {
                            card.TracerProgress -= card.TracerPath.Count;
                        }
                    }
                }

                card.Animator.Update(dt);
            }

            // 2. Handle Input (Exclusive Hover - Topmost/Rightmost wins)
            _hoveredItemData = null; // Reset hover data

            if (!_isCollectingAll)
            {
                LootCard topHoveredCard = null;

                // Iterate backwards to find the top-most card under the mouse
                // The list is ordered Left->Right (Bottom->Top), so last index is top.
                for (int i = _cards.Count - 1; i >= 0; i--)
                {
                    var card = _cards[i];
                    // Only consider cards that are not collected and are interactive
                    if (!card.IsCollected && card.Animator.IsInteractive && card.CurrentBounds.Contains(mousePos))
                    {
                        topHoveredCard = card;
                        break; // Stop at the first (highest) card found
                    }
                }

                // Apply hover state to all cards
                foreach (var card in _cards)
                {
                    // Only update interactive cards
                    if (!card.IsCollected && card.Animator.IsInteractive)
                    {
                        bool isTarget = (card == topHoveredCard);

                        // Update visual hover state
                        card.IsMouseHovering = isTarget;

                        // Update animator state
                        card.Animator.SetHover(isTarget);

                        if (isTarget)
                        {
                            _hoveredItemData = card.Item.OriginalData; // Set tooltip data

                            if (clicked)
                            {
                                CollectCard(card);
                                // Consume click so it doesn't trigger anything else this frame
                                clicked = false;
                            }
                        }
                    }
                    else
                    {
                        // Ensure non-interactive cards don't get stuck in hover state
                        card.IsMouseHovering = false;
                        card.Animator.SetHover(false);
                    }
                }
            }

            // --- TOOLTIP TIMER & ANIMATION LOGIC ---
            if (_hoveredItemData != _lastHoveredItemData)
            {
                _tooltipTimer = 0f;
                _lastHoveredItemData = _hoveredItemData;
                _tooltipAnimator.Reset(); // Instant hide on switch
            }

            if (_hoveredItemData != null)
            {
                _tooltipTimer += dt;
                if (_tooltipTimer >= TOOLTIP_DELAY && !_tooltipAnimator.IsVisible)
                {
                    _tooltipAnimator.Show();
                }
            }
            else
            {
                _tooltipTimer = 0f;
                _tooltipAnimator.Reset();
            }
            _tooltipAnimator.Update(dt);

            // Cleanup: Remove cards that have finished animating out
            _cards.RemoveAll(c => c.IsCollected && !c.Animator.IsVisible);

            // If all cards are gone, close
            if (_cards.Count == 0)
            {
                Close();
            }

            _prevMouse = mouse;
        }

        private void UpdateCardLayout()
        {
            // Filter to only active (uncollected) cards for layout purposes
            var activeCards = _cards.Where(c => !c.IsCollected).ToList();
            int count = activeCards.Count;
            if (count == 0) return;

            int cardY = _lootArea.Center.Y - (CARD_SIZE / 2);

            // Divide the total area width into equal segments
            float segmentWidth = (float)_lootArea.Width / count;

            for (int i = 0; i < count; i++)
            {
                // Calculate the center X of the segment
                float segmentCenterX = _lootArea.X + (segmentWidth * i) + (segmentWidth / 2f);

                // Center the card within that segment
                float targetX = segmentCenterX - (CARD_SIZE / 2f);

                activeCards[i].TargetPosition = new Vector2(targetX, cardY);
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsActive) return;

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            // Draw Cards
            foreach (var card in _cards)
            {
                var state = card.Animator.GetVisualState();
                if (!state.IsVisible) continue;

                // Apply Animator Transform to the VisualPosition
                // VisualPosition is the top-left of the card's current layout slot
                Vector2 basePos = card.VisualPosition;
                Vector2 center = basePos + new Vector2(CARD_SIZE / 2f, CARD_SIZE / 2f);

                // The animator offset is relative to the center
                Vector2 drawPos = center + state.Offset;

                // --- SHADOW LOGIC ---
                // Draw shadow at the BASE position (the "floor"), not the animated position.
                // Scale shadow down as the item lifts up to simulate height.
                // Max lift is roughly -10px.
                float liftRatio = Math.Abs(state.Offset.Y) / 10f; // 0 to 1
                float shadowScale = MathHelper.Lerp(1.0f, 0.6f, liftRatio) * state.Scale.X; // Shrink shadow
                float shadowAlpha = MathHelper.Lerp(0.5f, 0.2f, liftRatio) * state.Opacity; // Fade shadow

                // Draw Item Sprite Shadow (Silhouette)
                Texture2D icon = _spriteManager.GetItemSprite(card.Item.SpritePath);
                Texture2D silhouette = _spriteManager.GetItemSpriteSilhouette(card.Item.SpritePath);

                if (icon != null)
                {
                    Vector2 origin = new Vector2(icon.Width / 2f, icon.Height / 2f);

                    // 1. Draw Floor Shadow: Flattened Y, Tinted Black
                    spriteBatch.Draw(icon, center + new Vector2(state.Offset.X, 8), null, Color.Black * shadowAlpha, state.Rotation, origin, new Vector2(state.Scale.X, state.Scale.Y * 0.3f), SpriteEffects.None, 0f);

                    // 2. Draw Outline (If Silhouette exists)
                    if (silhouette != null)
                    {
                        // Determine colors based on explicit mouse hover state (Instant feedback)
                        bool isHovered = card.IsMouseHovering;

                        Color mainOutlineColor = isHovered ? _global.ItemOutlineColor_Hover : _global.Palette_DarkGray;
                        Color cornerOutlineColor = isHovered ? _global.ItemOutlineColor_Hover_Corner : _global.Palette_DarkerGray;

                        // Draw Diagonals (Corners)
                        spriteBatch.Draw(silhouette, drawPos + new Vector2(-1, -1), null, cornerOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        spriteBatch.Draw(silhouette, drawPos + new Vector2(1, -1), null, cornerOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        spriteBatch.Draw(silhouette, drawPos + new Vector2(-1, 1), null, cornerOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        spriteBatch.Draw(silhouette, drawPos + new Vector2(1, 1), null, cornerOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);

                        // Draw Cardinals (Main)
                        spriteBatch.Draw(silhouette, drawPos + new Vector2(-1, 0), null, mainOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        spriteBatch.Draw(silhouette, drawPos + new Vector2(1, 0), null, mainOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        spriteBatch.Draw(silhouette, drawPos + new Vector2(0, -1), null, mainOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        spriteBatch.Draw(silhouette, drawPos + new Vector2(0, 1), null, mainOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                    }

                    // --- 2.5. Draw Rarity Tracer & Pulse ---
                    // Only draw if not collected and visible
                    if (!card.IsCollected && state.Opacity > 0.5f && card.TracerPath != null && card.TracerPath.Count > 0)
                    {
                        Color rarityColor = _global.RarityColors.GetValueOrDefault(card.Item.Rarity, Color.White);
                        int pathLength = card.TracerPath.Count;

                        // --- A. Draw Pulsing Outline ---
                        float pulseTime = (float)gameTime.TotalGameTime.TotalSeconds * PULSE_SPEED;
                        float pulseSine = (MathF.Sin(pulseTime) + 1f) / 2f; // 0 to 1

                        // Calculate Intensity based on rarity
                        float maxIntensity = GetPulseIntensity(card.Item.Rarity);
                        float currentAlpha = pulseSine * maxIntensity * state.Opacity;

                        if (currentAlpha > 0.01f)
                        {
                            foreach (var point in card.TracerPath)
                            {
                                Vector2 pointVec = point.ToVector2();
                                Vector2 relativePos = (pointVec - origin) * state.Scale;

                                if (state.Rotation != 0f)
                                {
                                    float cos = MathF.Cos(state.Rotation);
                                    float sin = MathF.Sin(state.Rotation);
                                    relativePos = new Vector2(
                                        relativePos.X * cos - relativePos.Y * sin,
                                        relativePos.X * sin + relativePos.Y * cos
                                    );
                                }

                                Vector2 finalPos = drawPos + relativePos;
                                spriteBatch.DrawSnapped(pixel, finalPos, null, rarityColor * currentAlpha, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                            }
                        }

                        // --- B. Draw Moving Tracer Dots ---
                        var config = GetTracerConfig(card.Item.Rarity);
                        float spacing = pathLength / (float)config.DotCount;

                        for (int d = 0; d < config.DotCount; d++)
                        {
                            float dotHeadPos = (card.TracerProgress + (d * spacing)) % pathLength;

                            for (int i = 0; i < config.TrailLength; i++)
                            {
                                float trailIndexFloat = dotHeadPos - i;
                                if (trailIndexFloat < 0) trailIndexFloat += pathLength;
                                int trailIndex = (int)trailIndexFloat % pathLength;

                                Point point = card.TracerPath[trailIndex];

                                float alphaRatio = 1.0f - ((float)i / config.TrailLength);
                                float alpha = MathHelper.Lerp(TRAIL_END_ALPHA, TRAIL_START_ALPHA, alphaRatio) * state.Opacity * config.Opacity;

                                Vector2 pointVec = point.ToVector2();
                                Vector2 relativePos = (pointVec - origin) * state.Scale;

                                if (state.Rotation != 0f)
                                {
                                    float cos = MathF.Cos(state.Rotation);
                                    float sin = MathF.Sin(state.Rotation);
                                    relativePos = new Vector2(
                                        relativePos.X * cos - relativePos.Y * sin,
                                        relativePos.X * sin + relativePos.Y * cos
                                    );
                                }

                                Vector2 finalPos = drawPos + relativePos;
                                spriteBatch.DrawSnapped(pixel, finalPos, null, rarityColor * alpha, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                            }
                        }
                    }

                    // 3. Draw Item Sprite Body
                    spriteBatch.Draw(icon, drawPos, null, Color.White * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);

                    // 4. Draw Instant Name on Hover (NEW)
                    if (card.IsMouseHovering && !card.IsCollected)
                    {
                        string name = card.Item.Name.ToUpper();
                        Vector2 nameSize = secondaryFont.MeasureString(name);

                        // Position: Center X, Above Sprite Y
                        // Assuming max sprite height is roughly CARD_SIZE (32), half is 16.
                        // Add padding (2px).
                        Vector2 textPos = new Vector2(
                            drawPos.X - nameSize.X / 2f,
                            drawPos.Y - 16 - nameSize.Y - 2
                        );

                        // Snap to pixels
                        textPos = new Vector2(MathF.Round(textPos.X), MathF.Round(textPos.Y));

                        spriteBatch.DrawStringSnapped(secondaryFont, name, textPos, _global.Palette_BlueWhite * state.Opacity);
                    }
                }
            }

            _collectAllButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            _skipButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);

            // Title
            string title = "VICTORY!";
            Vector2 titleSize = font.MeasureString(title);
            float titleBob = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f) * 2f;

            // Decoupled position: Fixed Y near top (20px), centered horizontally
            float titleY = 20f;
            spriteBatch.DrawString(font, title, new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, titleY + titleBob), _global.Palette_Yellow);

            // --- Draw Tooltip ---
            if (_hoveredItemData != null && _tooltipAnimator.IsVisible)
            {
                // Find the card that corresponds to the hovered data to get its position
                var hoveredCard = _cards.FirstOrDefault(c => c.Item.OriginalData == _hoveredItemData);
                if (hoveredCard != null)
                {
                    // Use the visual center of the card as the anchor
                    Vector2 basePos = hoveredCard.VisualPosition;
                    Vector2 center = basePos + new Vector2(CARD_SIZE / 2f, CARD_SIZE / 2f);

                    var state = _tooltipAnimator.GetVisualState();

                    // Pass the static center as the anchor, and the animator state
                    // Force opacity to 1.0f to prevent fade-in, but use scale for pop-in
                    _tooltipRenderer.DrawTooltip(spriteBatch, _hoveredItemData, center, gameTime, state.Scale, 1.0f);
                }
            }

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                // Draw Area Background
                spriteBatch.DrawSnapped(pixel, _lootArea, Color.Magenta * 0.3f);

                // Draw Border
                spriteBatch.DrawLineSnapped(new Vector2(_lootArea.Left, _lootArea.Top), new Vector2(_lootArea.Right, _lootArea.Top), Color.Magenta);
                spriteBatch.DrawLineSnapped(new Vector2(_lootArea.Left, _lootArea.Bottom), new Vector2(_lootArea.Right, _lootArea.Bottom), Color.Magenta);
                spriteBatch.DrawLineSnapped(new Vector2(_lootArea.Left, _lootArea.Top), new Vector2(_lootArea.Left, _lootArea.Bottom), Color.Magenta);
                spriteBatch.DrawLineSnapped(new Vector2(_lootArea.Right, _lootArea.Top), new Vector2(_lootArea.Right, _lootArea.Bottom), Color.Magenta);

                // Draw Dimensions Text
                string debugText = $"RECT: {_lootArea.X},{_lootArea.Y} {_lootArea.Width}x{_lootArea.Height}";
                Vector2 textPos = new Vector2(_lootArea.X, _lootArea.Bottom + 2);
                spriteBatch.DrawStringSnapped(secondaryFont, debugText, textPos, Color.Yellow);
            }
        }

        /// <summary>
        /// Returns the tracer configuration (Speed, DotCount, TrailLength, Opacity) based on item rarity.
        /// </summary>
        private (float Speed, int DotCount, int TrailLength, float Opacity) GetTracerConfig(int rarity)
        {
            return rarity switch
            {
                0 => (15f, 1, 4, 0.5f),  // Common: Slow, faint, short
                1 => (20f, 1, 6, 0.7f),  // Uncommon: Slightly faster/brighter
                2 => (30f, 1, 8, 1.0f),  // Rare: Baseline
                3 => (40f, 2, 10, 1.0f), // Epic: Fast, 2 dots
                4 => (50f, 3, 12, 1.0f), // Mythic: Very fast, 3 dots
                5 => (60f, 4, 15, 1.0f), // Legendary: Hyper, 4 dots
                _ => (30f, 1, 8, 1.0f)   // Default
            };
        }

        /// <summary>
        /// Returns the maximum opacity for the pulsing outline based on rarity.
        /// </summary>
        private float GetPulseIntensity(int rarity)
        {
            return rarity switch
            {
                0 => 0.15f, // Common: Very faint
                1 => 0.25f, // Uncommon: Faint
                2 => 0.40f, // Rare: Visible
                3 => 0.60f, // Epic: Bright
                4 => 0.80f, // Mythic: Very Bright
                5 => 1.00f, // Legendary: Full Opacity
                _ => 0.40f
            };
        }
    }
}
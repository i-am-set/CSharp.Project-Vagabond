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
using System.Collections;
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
        private Core _core;

        // State
        public bool IsActive { get; private set; }

        // Wrapper class to manage item state + animation state together
        private class LootCard
        {
            public BaseItem Item;
            public UIAnimator Animator;
            public bool IsCollected; // True if clicked/collecting
            public bool IsDiscarded; // True if another card was picked
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
        private const int AREA_WIDTH = 90; // Reduced from 140 to bring items closer
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
        private Button _skipButton;

        // Input State
        private MouseState _prevMouse;

        // Selection State
        private bool _selectionMade = false;
        private float _autoCloseTimer = 0f;
        private const float AUTO_CLOSE_DELAY = 1.5f; // Time to wait after picking before closing

        // Tooltip State
        private object? _hoveredItemData;
        private object? _lastHoveredItemData;
        private float _tooltipTimer = 0f;
        private const float TOOLTIP_DELAY = 0.0f;

        // Tooltip Animation
        private UIAnimator _tooltipAnimator;

        // Text Wave Timer
        private float _textWaveTimer = 0f;

        public LootScreen()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _tooltipRenderer = ServiceLocator.Get<ItemTooltipRenderer>();
            _core = ServiceLocator.Get<Core>();
            _cards = new List<LootCard>();

            // Initialize Tooltip Animator
            _tooltipAnimator = new UIAnimator
            {
                EntryStyle = EntryExitStyle.Pop,
                DurationIn = 0.2f,
                DurationOut = 0.1f
            };

            // Center the loot area
            int x = (Global.VIRTUAL_WIDTH - AREA_WIDTH) / 2;
            int y = (Global.VIRTUAL_HEIGHT - AREA_HEIGHT) / 2 - 24;
            _lootArea = new Rectangle(x, y, AREA_WIDTH, AREA_HEIGHT);

            // Initialize Control Buttons
            // Moved down 16 pixels (30 -> 46)
            int btnY = _lootArea.Bottom + 46;

            _skipButton = new Button(new Rectangle((Global.VIRTUAL_WIDTH - 60) / 2, btnY, 60, 15), "SKIP", font: ServiceLocator.Get<Core>().SecondaryFont);
            _skipButton.OnClick += SkipAll;
        }

        public void Show(List<BaseItem> loot)
        {
            _cards.Clear();
            IsActive = true;
            _prevMouse = Mouse.GetState();
            _selectionMade = false;
            _autoCloseTimer = 0f;
            _hoveredItemData = null;
            _lastHoveredItemData = null;
            _tooltipTimer = 0f;
            _textWaveTimer = 0f;
            _tooltipAnimator.Reset();
            _skipButton.ResetAnimationState();

            if (loot != null)
            {
                // Pre-calculate initial positions
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
                        HoverLift = -2f,       // Lift up 2 pixels
                        InteractionSpeed = 55f, // Very snappy response

                        // Apply Tunable Speeds & Distance
                        HoverSwaySpeed = LOOT_HOVER_FLOAT_SPEED,
                        HoverRotationSpeed = LOOT_HOVER_ROTATION_SPEED,
                        HoverSwayDistance = LOOT_HOVER_FLOAT_DISTANCE,

                        DurationIn = 0.4f,
                        DurationOut = 0.15f // Snappy exit
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
                        IsDiscarded = false,
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

        private List<Point> GenerateTracerPath(Texture2D texture)
        {
            if (texture == null) return new List<Point>();

            int w = texture.Width;
            int h = texture.Height;
            Color[] data = new Color[w * h];
            texture.GetData(data);

            int gridW = w + 4;
            int gridH = h + 4;
            bool[,] grid = new bool[gridW, gridH];
            int offsetX = 2;
            int offsetY = 2;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (data[y * w + x].A > 0)
                    {
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

            List<Point> path = new List<Point>();
            Point start = Point.Zero;
            bool foundStart = false;

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

            if (!foundStart) return path;

            Point current = start;
            Point backtrack = new Point(start.X - 1, start.Y);
            int maxSteps = gridW * gridH * 2;
            int steps = 0;

            Point[] offsets = {
                new Point(0, -1), new Point(1, -1), new Point(1, 0), new Point(1, 1),
                new Point(0, 1), new Point(-1, 1), new Point(-1, 0), new Point(-1, -1)
            };

            do
            {
                path.Add(current);
                int checkIndex = -1;
                Point relBack = new Point(backtrack.X - current.X, backtrack.Y - current.Y);
                for (int i = 0; i < 8; i++)
                {
                    if (offsets[i] == relBack)
                    {
                        checkIndex = i;
                        break;
                    }
                }

                bool foundNext = false;
                for (int i = 1; i <= 8; i++)
                {
                    int idx = (checkIndex + i) % 8;
                    Point neighbor = new Point(current.X + offsets[idx].X, current.Y + offsets[idx].Y);

                    if (neighbor.X >= 0 && neighbor.X < gridW && neighbor.Y >= 0 && neighbor.Y < gridH && grid[neighbor.X, neighbor.Y])
                    {
                        int prevIdx = (idx + 7) % 8;
                        backtrack = new Point(current.X + offsets[prevIdx].X, current.Y + offsets[prevIdx].Y);
                        current = neighbor;
                        foundNext = true;
                        break;
                    }
                }

                if (!foundNext) break;
                steps++;
            } while (current != start && steps < maxSteps);

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
            _selectionMade = false;
            _tooltipAnimator.Reset();
        }

        public void Reset()
        {
            Close();
            _skipButton.ResetAnimationState();
        }

        private void SkipAll()
        {
            if (_selectionMade) return;
            _selectionMade = true;
            foreach (var card in _cards)
            {
                Vector2 currentOffset = card.Animator.GetCurrentOffset();
                card.VisualPosition += currentOffset;
                card.Animator.ForceOffset(Vector2.Zero);
                card.Animator.Hide(delay: 0f, overrideStyle: EntryExitStyle.SlideDown);
                card.IsDiscarded = true;
            }
            _autoCloseTimer = AUTO_CLOSE_DELAY;
        }

        private void CollectCard(LootCard card)
        {
            if (_selectionMade) return;
            _selectionMade = true;

            Vector2 currentOffset = card.Animator.GetCurrentOffset();
            card.VisualPosition += currentOffset;
            card.Animator.ForceOffset(Vector2.Zero);

            card.IsCollected = true;
            AddItemToInventory(card.Item);

            card.Animator.DurationOut = 0.6f;
            card.Animator.Hide(delay: 0f, overrideStyle: EntryExitStyle.JuicyCollect);

            _hapticsManager.TriggerCompoundShake(0.2f);

            foreach (var otherCard in _cards)
            {
                if (otherCard != card)
                {
                    otherCard.IsDiscarded = true;
                    Vector2 otherOffset = otherCard.Animator.GetCurrentOffset();
                    otherCard.VisualPosition += otherOffset;
                    otherCard.Animator.ForceOffset(Vector2.Zero);
                    otherCard.Animator.DurationOut = 0.25f;
                    otherCard.Animator.Hide(delay: 0f, overrideStyle: EntryExitStyle.Fade);
                }
            }
            _autoCloseTimer = AUTO_CLOSE_DELAY;
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

            _textWaveTimer += dt;

            if (_selectionMade)
            {
                _autoCloseTimer -= dt;
                if (_autoCloseTimer <= 0f)
                {
                    Close();
                    return;
                }
            }
            else
            {
                _skipButton.Update(mouse);
            }

            UpdateCardLayout();

            foreach (var card in _cards)
            {
                if (!card.IsCollected && !card.IsDiscarded)
                {
                    card.VisualPosition = Vector2.Lerp(card.VisualPosition, card.TargetPosition, dt * CARD_MOVE_SPEED);
                    card.CurrentBounds = new Rectangle((int)card.VisualPosition.X, (int)card.VisualPosition.Y, CARD_SIZE, CARD_SIZE);

                    if (card.TracerPath != null && card.TracerPath.Count > 0)
                    {
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

            _hoveredItemData = null;

            if (!_selectionMade)
            {
                LootCard topHoveredCard = null;
                for (int i = _cards.Count - 1; i >= 0; i--)
                {
                    var card = _cards[i];
                    if (card.Animator.IsInteractive && card.CurrentBounds.Contains(mousePos))
                    {
                        topHoveredCard = card;
                        break;
                    }
                }

                foreach (var card in _cards)
                {
                    if (card.Animator.IsInteractive)
                    {
                        bool isTarget = (card == topHoveredCard);
                        card.IsMouseHovering = isTarget;
                        card.Animator.SetHover(isTarget);

                        if (isTarget)
                        {
                            _hoveredItemData = card.Item.OriginalData;
                            ServiceLocator.Get<CursorManager>().SetState(CursorState.HoverClickableHint);
                            if (clicked)
                            {
                                CollectCard(card);
                                clicked = false;
                            }
                        }
                    }
                    else
                    {
                        card.IsMouseHovering = false;
                        card.Animator.SetHover(false);
                    }
                }
            }

            if (_hoveredItemData != _lastHoveredItemData)
            {
                _tooltipTimer = 0f;
                _lastHoveredItemData = _hoveredItemData;
                _tooltipAnimator.Reset();
            }

            if (_hoveredItemData != null && !_selectionMade)
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

            _prevMouse = mouse;
        }

        private void UpdateCardLayout()
        {
            int count = _cards.Count;
            if (count == 0) return;

            int cardY = _lootArea.Center.Y - (CARD_SIZE / 2);
            float segmentWidth = (float)_lootArea.Width / count;

            for (int i = 0; i < count; i++)
            {
                float segmentCenterX = _lootArea.X + (segmentWidth * i) + (segmentWidth / 2f);
                float targetX = segmentCenterX - (CARD_SIZE / 2f);
                _cards[i].TargetPosition = new Vector2(targetX, cardY);
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (!IsActive) return;

            // Request the overlay, but delegate the logic to a cleaner method
            _core.RequestFullscreenOverlay((sb, uiMatrix) =>
            {
                DrawOverlayContent(sb, uiMatrix, font, gameTime, transform);
            });
        }

        private void DrawOverlayContent(SpriteBatch sb, Matrix uiMatrix, BitmapFont font, GameTime gameTime, Matrix virtualTransform)
        {
            // --- PASS 1: DIMMER ---
            DrawDimmerPass(sb);

            // --- PASS 2: CONTENT ---
            DrawContentPass(sb, uiMatrix, font, gameTime);

            // --- PASS 3: TOOLTIP ---
            DrawTooltipPass(sb, uiMatrix, gameTime);
        }

        private void DrawDimmerPass(SpriteBatch sb)
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            int screenW = graphicsDevice.PresentationParameters.BackBufferWidth;
            int screenH = graphicsDevice.PresentationParameters.BackBufferHeight;
            var pixel = ServiceLocator.Get<Texture2D>();

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.Identity);
            try
            {
                // Calculate dimmer alpha based on tooltip visibility
                var tooltipState = _tooltipAnimator.GetVisualState();
                float dimmerAlpha = 0f;
                if (_hoveredItemData != null && tooltipState.IsVisible && !_selectionMade)
                {
                    dimmerAlpha = tooltipState.Opacity * 0.85f;
                }

                if (dimmerAlpha > 0.01f)
                {
                    sb.Draw(pixel, new Rectangle(0, 0, screenW, screenH), Color.Black * dimmerAlpha);
                }
            }
            finally
            {
                sb.End();
            }
        }

        private void DrawContentPass(SpriteBatch sb, Matrix uiMatrix, BitmapFont font, GameTime gameTime)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, uiMatrix);
            try
            {
                DrawLootCards(sb, font, gameTime);

                if (!_selectionMade)
                {
                    _skipButton.Draw(sb, secondaryFont, gameTime, Matrix.Identity);
                }

                // Title
                string title = "PICK ONE!";
                Vector2 titleSize = font.MeasureString(title);
                float titleBob = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f) * 2f;
                float titleY = 20f;
                sb.DrawString(font, title, new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, titleY + titleBob), _global.Palette_Yellow);

                // Debug
                if (_global.ShowSplitMapGrid)
                {
                    sb.DrawSnapped(pixel, _lootArea, Color.Magenta * 0.3f);
                    sb.DrawLineSnapped(new Vector2(_lootArea.Left, _lootArea.Top), new Vector2(_lootArea.Right, _lootArea.Top), Color.Magenta);
                    sb.DrawLineSnapped(new Vector2(_lootArea.Left, _lootArea.Bottom), new Vector2(_lootArea.Right, _lootArea.Bottom), Color.Magenta);
                    sb.DrawLineSnapped(new Vector2(_lootArea.Left, _lootArea.Top), new Vector2(_lootArea.Left, _lootArea.Bottom), Color.Magenta);
                    sb.DrawLineSnapped(new Vector2(_lootArea.Right, _lootArea.Top), new Vector2(_lootArea.Right, _lootArea.Bottom), Color.Magenta);
                }
            }
            finally
            {
                sb.End();
            }
        }

        private void DrawLootCards(SpriteBatch sb, BitmapFont font, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            foreach (var card in _cards)
            {
                var state = card.Animator.GetVisualState();
                if (!state.IsVisible) continue;

                Vector2 basePos = card.VisualPosition;
                Vector2 center = basePos + new Vector2(CARD_SIZE / 2f, CARD_SIZE / 2f);
                Vector2 drawPos = center + state.Offset;

                // --- SHADOW LOGIC ---
                float liftRatio = Math.Abs(state.Offset.Y) / 10f;
                float shadowScale = MathHelper.Lerp(1.0f, 0.6f, liftRatio) * state.Scale.X;
                float shadowAlpha = MathHelper.Lerp(0.5f, 0.2f, liftRatio) * state.Opacity;

                Texture2D icon = _spriteManager.GetItemSprite(card.Item.SpritePath);
                Texture2D silhouette = _spriteManager.GetItemSpriteSilhouette(card.Item.SpritePath);

                if (icon != null)
                {
                    Vector2 origin = new Vector2(icon.Width / 2f, icon.Height / 2f);

                    // 1. Draw Floor Shadow
                    sb.Draw(icon, center + new Vector2(state.Offset.X, 8), null, Color.Black * shadowAlpha, state.Rotation, origin, new Vector2(state.Scale.X, state.Scale.Y * 0.3f), SpriteEffects.None, 0f);

                    // 2. Draw Outline
                    if (silhouette != null)
                    {
                        bool isHovered = card.IsMouseHovering && !_selectionMade;
                        Color mainOutlineColor = isHovered ? _global.ItemOutlineColor_Hover : _global.Palette_DarkGray;
                        Color cornerOutlineColor = isHovered ? _global.ItemOutlineColor_Hover_Corner : _global.Palette_DarkerGray;

                        // Corners
                        sb.DrawSnapped(silhouette, drawPos + new Vector2(-1, -1), null, cornerOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        sb.DrawSnapped(silhouette, drawPos + new Vector2(1, -1), null, cornerOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        sb.DrawSnapped(silhouette, drawPos + new Vector2(-1, 1), null, cornerOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        sb.DrawSnapped(silhouette, drawPos + new Vector2(1, 1), null, cornerOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);

                        // Cardinals
                        sb.DrawSnapped(silhouette, drawPos + new Vector2(-1, 0), null, mainOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        sb.DrawSnapped(silhouette, drawPos + new Vector2(1, 0), null, mainOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        sb.DrawSnapped(silhouette, drawPos + new Vector2(0, -1), null, mainOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                        sb.DrawSnapped(silhouette, drawPos + new Vector2(0, 1), null, mainOutlineColor * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                    }

                    // --- 2.5. Draw Rarity Tracer & Pulse ---
                    if (!card.IsCollected && !card.IsDiscarded && !card.IsMouseHovering && state.Opacity > 0.5f && card.TracerPath != null && card.TracerPath.Count > 0)
                    {
                        Color rarityColor = _global.RarityColors.GetValueOrDefault(card.Item.Rarity, Color.White);
                        int pathLength = card.TracerPath.Count;

                        // A. Pulse
                        float pulseTime = (float)gameTime.TotalGameTime.TotalSeconds * PULSE_SPEED;
                        float pulseSine = (MathF.Sin(pulseTime) + 1f) / 2f;
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
                                    relativePos = new Vector2(relativePos.X * cos - relativePos.Y * sin, relativePos.X * sin + relativePos.Y * cos);
                                }
                                sb.DrawSnapped(pixel, drawPos + relativePos, null, rarityColor * currentAlpha, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                            }
                        }

                        // B. Tracer Dots
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
                                    relativePos = new Vector2(relativePos.X * cos - relativePos.Y * sin, relativePos.X * sin + relativePos.Y * cos);
                                }
                                sb.DrawSnapped(pixel, drawPos + relativePos, null, rarityColor * alpha, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                            }
                        }
                    }

                    // 3. Draw Item Sprite Body
                    // FIX: Use DrawSnapped to ensure body aligns with outline
                    sb.DrawSnapped(icon, drawPos, null, Color.White * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);

                    // --- 3.5 Draw Rarity Symbol ---
                    if (card.Item.Rarity >= 0 && _spriteManager.RarityIconsSpriteSheet != null)
                    {
                        var rarityRect = _spriteManager.GetRarityIconSourceRect(card.Item.Rarity, gameTime);
                        Vector2 rarityOffset = new Vector2(12, -12) * state.Scale;
                        Vector2 rarityPos = center + rarityOffset;
                        Vector2 rarityOrigin = new Vector2(4, 4);
                        sb.DrawSnapped(_spriteManager.RarityIconsSpriteSheet, rarityPos, rarityRect, Color.White * state.Opacity, 0f, rarityOrigin, state.Scale, SpriteEffects.None, 0f);
                    }
                }
            }
        }

        private void DrawTooltipPass(SpriteBatch sb, Matrix uiMatrix, GameTime gameTime)
        {
            if (_hoveredItemData != null && _tooltipAnimator.GetVisualState().IsVisible && !_selectionMade)
            {
                var hoveredCard = _cards.FirstOrDefault(c => c.Item.OriginalData == _hoveredItemData);
                if (hoveredCard != null)
                {
                    Vector2 center = hoveredCard.VisualPosition + new Vector2(CARD_SIZE / 2f, CARD_SIZE / 2f);
                    var state = _tooltipAnimator.GetVisualState();

                    // Use the new immediate renderer logic.
                    // Pass drawDimmer: false because we already handled the dimmer in Pass 1.
                    // Pass transform: This is the UI Matrix (Virtual -> Screen).
                    _tooltipRenderer.DrawTooltipImmediate(sb, uiMatrix, _hoveredItemData, center, gameTime, state.Scale, 1.0f, drawDimmer: false);
                }
            }
        }

        private (float Speed, int DotCount, int TrailLength, float Opacity) GetTracerConfig(int rarity)
        {
            return rarity switch
            {
                0 => (15f, 1, 4, 0.15f),
                1 => (20f, 1, 6, 0.20f),
                2 => (30f, 1, 8, 0.25f),
                3 => (40f, 2, 10, 0.30f),
                4 => (50f, 3, 12, 0.35f),
                5 => (60f, 4, 15, 0.40f),
                _ => (30f, 1, 8, 0.25f)
            };
        }

        private float GetPulseIntensity(int rarity)
        {
            return rarity switch
            {
                0 => 0.10f,
                1 => 0.15f,
                2 => 0.20f,
                3 => 0.25f,
                4 => 0.30f,
                5 => 0.35f,
                _ => 0.20f
            };
        }
    }
}
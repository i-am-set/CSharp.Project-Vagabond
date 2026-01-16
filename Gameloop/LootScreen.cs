using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
using ProjectVagabond.Items;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

        // --- Smooth Dimmer State ---
        // We use a normalized value (0.0 to 1.0) to track progress, then map it to opacity.
        private float _dimmerProgress = 0f;
        private float _dimmerAlpha = 0f; // The actual alpha used for drawing
        private const float DIMMER_TARGET_OPACITY = 0.85f;
        private const float DIMMER_FADE_SPEED = 5.0f; // 1.0 / 5.0 = 0.2 seconds to fade

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

            // Reset dimmer state
            _dimmerProgress = 0f;
            _dimmerAlpha = 0f;

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

                    _cards.Add(new LootCard
                    {
                        Item = loot[i],
                        Animator = animator,
                        IsCollected = false,
                        IsDiscarded = false,
                        IsMouseHovering = false,
                        VisualPosition = pos,
                        TargetPosition = pos,
                        CurrentBounds = new Rectangle((int)pos.X, (int)pos.Y, CARD_SIZE, CARD_SIZE)
                    });
                }
            }
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

            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

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

            // --- REVISED TOOLTIP LOGIC ---
            if (_hoveredItemData != null && !_selectionMade)
            {
                if (_hoveredItemData != _lastHoveredItemData)
                {
                    _tooltipTimer = 0f;
                }

                _tooltipTimer += dt;

                if (_tooltipTimer >= TOOLTIP_DELAY)
                {
                    if (_hoveredItemData != _lastHoveredItemData)
                    {
                        _tooltipAnimator.Reset();
                        _tooltipAnimator.Show();
                    }
                    else if (!_tooltipAnimator.IsVisible)
                    {
                        _tooltipAnimator.Show();
                    }
                }
            }
            else // Not hovering anything
            {
                if (_lastHoveredItemData != null && _tooltipAnimator.IsVisible)
                {
                    _tooltipAnimator.Hide();
                }
                _tooltipTimer = 0f;
            }

            _lastHoveredItemData = _hoveredItemData;
            _tooltipAnimator.Update(dt);

            // --- NEW: SMOOTH DIMMER LOGIC ---
            // Decoupled from UIAnimator state to ensure smoothness.
            // We simply check if we *want* to show the tooltip based on hover state.
            bool shouldShowDimmer = (_hoveredItemData != null && !_selectionMade && _tooltipTimer >= TOOLTIP_DELAY);

            // Linear move towards target (0 or 1)
            float targetValue = shouldShowDimmer ? 1.0f : 0.0f;
            float moveSpeed = DIMMER_FADE_SPEED * dt;

            if (_dimmerProgress < targetValue)
            {
                _dimmerProgress = Math.Min(_dimmerProgress + moveSpeed, targetValue);
            }
            else if (_dimmerProgress > targetValue)
            {
                _dimmerProgress = Math.Max(_dimmerProgress - moveSpeed, targetValue);
            }

            // Map 0..1 to 0..MaxOpacity using SmoothStep for a nice curve
            _dimmerAlpha = MathHelper.SmoothStep(0f, DIMMER_TARGET_OPACITY, _dimmerProgress);

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
                // Only draw if there's any opacity to show
                if (_dimmerAlpha > 0.001f)
                {
                    sb.Draw(pixel, new Rectangle(0, 0, screenW, screenH), Color.Black * _dimmerAlpha);
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
                        Color mainOutlineColor = isHovered ? _global.ItemOutlineColor_Hover : _global.Palette_Black;
                        Color cornerOutlineColor = isHovered ? _global.ItemOutlineColor_Hover_Corner : _global.Palette_Black;

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

                    // 3. Draw Item Sprite Body
                    // FIX: Use DrawSnapped to ensure body aligns with outline
                    sb.DrawSnapped(icon, drawPos, null, Color.White * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
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
    }
}
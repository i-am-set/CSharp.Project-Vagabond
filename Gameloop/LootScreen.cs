using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Items;
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
        }
        private List<LootCard> _cards;

        // Layout Constants
        private Rectangle _lootArea;
        private const int CARD_SIZE = 32;
        private const int AREA_WIDTH = 140; // Reduced from 280
        private const int AREA_HEIGHT = 60;
        private const float CARD_MOVE_SPEED = 10f; // Speed of re-centering tween

        // --- TUNING: Hover Animation Speeds ---
        private const float LOOT_HOVER_FLOAT_SPEED = 1.0f;    // Multiplier for the figure-8 sway speed
        private const float LOOT_HOVER_ROTATION_SPEED = 1.0f; // Multiplier for the tilt speed
        private const float LOOT_HOVER_FLOAT_DISTANCE = 1.5f; // Max distance to sway (pixels)

        // Buttons
        private Button _collectAllButton;
        private Button _skipButton;

        // Input State
        private MouseState _prevMouse;

        // Collection Sequence State
        private bool _isCollectingAll = false;
        private float _collectTimer = 0f;
        private const float COLLECT_DELAY = 0.1f;

        public LootScreen()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _cards = new List<LootCard>();

            // Center the loot area
            int x = (Global.VIRTUAL_WIDTH - AREA_WIDTH) / 2;
            int y = (Global.VIRTUAL_HEIGHT - AREA_HEIGHT) / 2;
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
                        IdleStyle = IdleAnimationType.None, // No idle bobbing

                        // --- JUICY HOVER CONFIGURATION ---
                        HoverStyle = HoverAnimationType.Juicy,
                        HoverScale = 1.0f,      // Locked to 1.0x to prevent mangling
                        HoverLift = -3f,       // Lift up 10 pixels
                        InteractionSpeed = 15f, // Very snappy response

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

                    _cards.Add(new LootCard
                    {
                        Item = loot[i],
                        Animator = animator,
                        IsCollected = false,
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
            _isCollectingAll = false;
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

            // Update Animators & Input
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
                }

                card.Animator.Update(dt);

                // Only handle input if not already collected and not in auto-collect mode
                if (!_isCollectingAll && !card.IsCollected && card.Animator.IsInteractive)
                {
                    bool isHovered = card.CurrentBounds.Contains(mousePos);

                    // Store explicit mouse hover state for instant visual feedback
                    card.IsMouseHovering = isHovered;

                    // Pass hover state to animator for physics/movement
                    card.Animator.SetHover(isHovered);

                    if (isHovered && clicked)
                    {
                        CollectCard(card);
                        // CRITICAL FIX: Break loop to prevent double-collection or click-through issues in the same frame
                        break;
                    }
                }
                else
                {
                    card.IsMouseHovering = false;
                }
            }

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

            // Removed the background dimmer draw call as requested

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
                    // FIX: Added state.Offset.X to position so shadow follows horizontal sway
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

                    // 3. Draw Item Sprite Body
                    spriteBatch.Draw(icon, drawPos, null, Color.White * state.Opacity, state.Rotation, origin, state.Scale, SpriteEffects.None, 0f);
                }
            }

            _collectAllButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            _skipButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);

            // Title
            string title = "VICTORY!";
            Vector2 titleSize = font.MeasureString(title);
            float titleBob = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f) * 2f;
            spriteBatch.DrawString(font, title, new Vector2(_lootArea.Center.X - titleSize.X / 2, _lootArea.Top - 40 + titleBob), _global.Palette_Yellow);
        }
    }
}
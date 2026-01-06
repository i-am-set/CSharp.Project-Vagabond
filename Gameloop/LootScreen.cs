using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Items;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Scenes
{
    public class LootScreen
    {
        // Dependencies
        private Global _global;
        private SpriteManager _spriteManager;
        private GameState _gameState;
        // State
        public bool IsActive { get; private set; }
        private List<BaseItem> _currentLoot;
        private List<UIAnimator> _cardAnimators; // One animator per card

        // Layout Constants
        private Rectangle _lootArea;
        private const int CARD_SIZE = 32;
        private const int CARD_PADDING = 2;
        private const int AREA_WIDTH = 280;
        private const int AREA_HEIGHT = 60;

        // Buttons
        private Button _collectAllButton;
        private Button _skipButton;

        // Input State
        private MouseState _prevMouse;

        public LootScreen()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _currentLoot = new List<BaseItem>();
            _cardAnimators = new List<UIAnimator>();

            // Center the loot area
            int x = (Global.VIRTUAL_WIDTH - AREA_WIDTH) / 2;
            int y = (Global.VIRTUAL_HEIGHT - AREA_HEIGHT) / 2;
            _lootArea = new Rectangle(x, y, AREA_WIDTH, AREA_HEIGHT);

            // Initialize Control Buttons
            int btnY = _lootArea.Bottom + 20;

            _collectAllButton = new Button(new Rectangle(x, btnY, 80, 15), "COLLECT ALL", font: ServiceLocator.Get<Core>().SecondaryFont);
            _collectAllButton.OnClick += CollectAll;

            _skipButton = new Button(new Rectangle(_lootArea.Right - 60, btnY, 60, 15), "SKIP", font: ServiceLocator.Get<Core>().SecondaryFont);
            _skipButton.OnClick += SkipAll;
        }

        public void Show(List<BaseItem> loot)
        {
            _currentLoot = loot ?? new List<BaseItem>();
            IsActive = true;
            _prevMouse = Mouse.GetState();

            // Initialize Animators
            _cardAnimators.Clear();
            for (int i = 0; i < _currentLoot.Count; i++)
            {
                var animator = new UIAnimator
                {
                    EntryStyle = EntryExitStyle.PopJiggle,
                    ExitStyle = EntryExitStyle.Zoom, // Default exit (Collect)
                    IdleStyle = IdleAnimationType.Bob,
                    HoverStyle = HoverAnimationType.Lift,
                    DurationIn = 0.4f,
                    DurationOut = 0.25f
                };
                // Stagger entrance
                animator.Show(delay: i * 0.1f);
                _cardAnimators.Add(animator);
            }
        }

        public void Close()
        {
            IsActive = false;
            _currentLoot.Clear();
            _cardAnimators.Clear();
        }

        public void Reset()
        {
            Close();
            _collectAllButton.ResetAnimationState();
            _skipButton.ResetAnimationState();
        }

        private void CollectAll()
        {
            // Trigger exit animation for all cards
            for (int i = 0; i < _cardAnimators.Count; i++)
            {
                // Collect style: Zoom out
                _cardAnimators[i].Hide(delay: i * 0.05f, overrideStyle: EntryExitStyle.Zoom);
            }

            // Actually add items logic is handled when animation finishes or we force close?
            // For simplicity in this refactor, we add them now but wait to close.
            foreach (var item in _currentLoot) AddItemToInventory(item);

            // We need a way to wait for animations. For now, just close after a delay or immediately.
            // A robust system would wait for OnOutComplete.
            // Let's just close immediately for now to keep logic simple, 
            // or we could implement a "Closing" state.
            Close();
        }

        private void SkipAll()
        {
            // Trigger exit animation for all cards
            for (int i = 0; i < _cardAnimators.Count; i++)
            {
                // Skip style: Slide Down
                _cardAnimators[i].Hide(delay: i * 0.05f, overrideStyle: EntryExitStyle.SlideDown);
            }
            Close();
        }

        private void CollectItem(int index)
        {
            if (index < 0 || index >= _currentLoot.Count) return;

            var item = _currentLoot[index];
            AddItemToInventory(item);

            // Remove from lists
            _currentLoot.RemoveAt(index);
            _cardAnimators.RemoveAt(index);

            if (_currentLoot.Count == 0) Close();
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

            _collectAllButton.Update(mouse);
            _skipButton.Update(mouse);

            List<Rectangle> cardRects = CalculateCardPositions();

            // Update Animators & Input
            for (int i = 0; i < _cardAnimators.Count; i++)
            {
                var animator = _cardAnimators[i];
                animator.Update(dt);

                // Only handle input if fully visible/interactive
                if (animator.IsInteractive && i < cardRects.Count)
                {
                    bool isHovered = cardRects[i].Contains(mousePos);
                    animator.SetHover(isHovered);

                    if (isHovered && clicked)
                    {
                        CollectItem(i);
                        // Adjust index since we removed one
                        i--;
                    }
                }
            }

            _prevMouse = mouse;
        }

        private List<Rectangle> CalculateCardPositions()
        {
            List<Rectangle> rects = new List<Rectangle>();
            int count = _currentLoot.Count;
            if (count == 0) return rects;

            int cardY = _lootArea.Center.Y - (CARD_SIZE / 2);
            float naturalWidth = (count * CARD_SIZE) + ((count - 1) * CARD_PADDING);

            if (naturalWidth <= _lootArea.Width)
            {
                float startX = _lootArea.Center.X - (naturalWidth / 2);
                for (int i = 0; i < count; i++)
                {
                    rects.Add(new Rectangle((int)(startX + i * (CARD_SIZE + CARD_PADDING)), cardY, CARD_SIZE, CARD_SIZE));
                }
            }
            else
            {
                float startX = _lootArea.X;
                float endX = _lootArea.Right - CARD_SIZE;
                float step = (endX - startX) / (count - 1);
                for (int i = 0; i < count; i++)
                {
                    rects.Add(new Rectangle((int)(startX + (i * step)), cardY, CARD_SIZE, CARD_SIZE));
                }
            }
            return rects;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsActive) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            // Dimmer
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black * 0.7f);

            List<Rectangle> cardRects = CalculateCardPositions();

            // Draw Cards
            for (int i = 0; i < _currentLoot.Count; i++)
            {
                if (i >= cardRects.Count) break;

                var item = _currentLoot[i];
                var baseRect = cardRects[i];
                var animator = _cardAnimators[i];
                var state = animator.GetVisualState();

                if (!state.IsVisible) continue;

                // Apply Animator Transform
                // Calculate center for scaling/rotation
                Vector2 center = baseRect.Center.ToVector2();
                Vector2 drawPos = center + state.Offset;

                // We need to draw manually with scale/rotation, so we can't use simple Draw(rect).
                // We'll draw the background box centered.

                Color cardColor = _global.RarityColors.ContainsKey(item.Rarity) ? _global.RarityColors[item.Rarity] : Color.White;

                // Draw Shadow (Offset, no rotation/scale usually, but let's scale it)
                spriteBatch.Draw(pixel, drawPos + new Vector2(2, 2), null, Color.Black * 0.5f * state.Opacity, state.Rotation, new Vector2(0.5f), new Vector2(CARD_SIZE, CARD_SIZE) * state.Scale, SpriteEffects.None, 0f);

                // Draw Card Background
                spriteBatch.Draw(pixel, drawPos, null, cardColor * state.Opacity, state.Rotation, new Vector2(0.5f), new Vector2(CARD_SIZE, CARD_SIZE) * state.Scale, SpriteEffects.None, 0f);

                // Draw Inner Dark Background
                spriteBatch.Draw(pixel, drawPos, null, _global.Palette_DarkGray * state.Opacity, state.Rotation, new Vector2(0.5f), new Vector2(CARD_SIZE - 2, CARD_SIZE - 2) * state.Scale, SpriteEffects.None, 0f);

                // Draw Item Sprite
                Texture2D icon = _spriteManager.GetItemSprite(item.SpritePath);
                if (icon != null)
                {
                    // Draw sprite centered
                    Vector2 origin = new Vector2(icon.Width / 2f, icon.Height / 2f);
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
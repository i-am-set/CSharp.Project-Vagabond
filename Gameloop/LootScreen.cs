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

        // Layout Constants
        private Rectangle _lootArea;
        private const int CARD_SIZE = 32; // Reduced to 32x32 for a tighter look
        private const int CARD_PADDING = 2; // Tighter padding
        private const int AREA_WIDTH = 280; // Width of the container
        private const int AREA_HEIGHT = 60; // Reduced height since cards are smaller

        // Buttons
        private Button _collectAllButton;
        private Button _skipButton;

        // Input State
        private MouseState _prevMouse;

        // Animation
        private float _timeOpen = 0f;

        public LootScreen()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _currentLoot = new List<BaseItem>();

            // Center the loot area
            int x = (Global.VIRTUAL_WIDTH - AREA_WIDTH) / 2;
            int y = (Global.VIRTUAL_HEIGHT - AREA_HEIGHT) / 2;
            _lootArea = new Rectangle(x, y, AREA_WIDTH, AREA_HEIGHT);

            // Initialize Control Buttons
            // Position them a bit lower now that the area is smaller
            int btnY = _lootArea.Bottom + 20;

            _collectAllButton = new Button(new Rectangle(x, btnY, 80, 15), "COLLECT ALL", font: ServiceLocator.Get<Core>().SecondaryFont);
            _collectAllButton.OnClick += CollectAll;

            _skipButton = new Button(new Rectangle(_lootArea.Right - 60, btnY, 60, 15), "SKIP", font: ServiceLocator.Get<Core>().SecondaryFont);
            _skipButton.OnClick += Close;
        }

        public void Show(List<BaseItem> loot)
        {
            _currentLoot = loot ?? new List<BaseItem>();
            IsActive = true;
            _timeOpen = 0f;
            _prevMouse = Mouse.GetState(); // Prevent instant clicks from previous frame
        }

        public void Close()
        {
            IsActive = false;
            _currentLoot.Clear();
        }

        /// <summary>
        /// Fully resets the screen state. Called on Game Reset or Battle Start.
        /// </summary>
        public void Reset()
        {
            Close();
            _timeOpen = 0f;
            _collectAllButton.ResetAnimationState();
            _skipButton.ResetAnimationState();
        }

        private void CollectAll()
        {
            // Iterate backwards to safely remove
            for (int i = _currentLoot.Count - 1; i >= 0; i--)
            {
                CollectItem(i);
            }
            Close();
        }

        private void CollectItem(int index)
        {
            if (index < 0 || index >= _currentLoot.Count) return;

            var item = _currentLoot[index];

            // Add to actual inventory based on type
            switch (item.Type)
            {
                case ItemType.Weapon:
                    _gameState.PlayerState.AddWeapon(item.ID);
                    break;
                case ItemType.Armor:
                    _gameState.PlayerState.AddArmor(item.ID);
                    break;
                case ItemType.Relic:
                    _gameState.PlayerState.AddRelic(item.ID);
                    break;
            }

            // Remove from screen
            _currentLoot.RemoveAt(index);

            // Auto-close if empty
            if (_currentLoot.Count == 0)
            {
                Close();
            }
        }

        public void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            _timeOpen += (float)gameTime.ElapsedGameTime.TotalSeconds;

            MouseState mouse = Mouse.GetState();
            Vector2 mousePos = Core.TransformMouse(mouse.Position);
            bool clicked = mouse.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed;

            _collectAllButton.Update(mouse);
            _skipButton.Update(mouse);

            List<Rectangle> cardRects = CalculateCardPositions();

            // Handle Card Input (Backwards loop for Z-order)
            for (int i = cardRects.Count - 1; i >= 0; i--)
            {
                if (cardRects[i].Contains(mousePos))
                {
                    if (clicked)
                    {
                        CollectItem(i);
                    }
                    break; // Stop checking once we hit the top-most card
                }
            }

            _prevMouse = mouse;
        }

        /// <summary>
        /// Calculates the exact screen rectangle for every card based on count and container width.
        /// </summary>
        private List<Rectangle> CalculateCardPositions()
        {
            List<Rectangle> rects = new List<Rectangle>();
            int count = _currentLoot.Count;
            if (count == 0) return rects;

            int cardY = _lootArea.Center.Y - (CARD_SIZE / 2);

            // Calculate total width if we just spaced them normally
            float naturalWidth = (count * CARD_SIZE) + ((count - 1) * CARD_PADDING);

            if (naturalWidth <= _lootArea.Width)
            {
                // CASE A: Fits normally. Center the group.
                float startX = _lootArea.Center.X - (naturalWidth / 2);
                for (int i = 0; i < count; i++)
                {
                    rects.Add(new Rectangle((int)(startX + i * (CARD_SIZE + CARD_PADDING)), cardY, CARD_SIZE, CARD_SIZE));
                }
            }
            else
            {
                // CASE B: Overflow. Squeeze them.
                // First card at Left Edge, Last card at Right Edge.
                float startX = _lootArea.X;
                float endX = _lootArea.Right - CARD_SIZE; // The X position of the last card

                // The total distance available to distribute the *starts* of the cards
                float availableSpan = endX - startX;

                // Step size between card starts
                float step = availableSpan / (count - 1);

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

            // Draw Overlay Dimmer
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black * 0.7f);

            // NOTE: Background rectangle removed as requested.

            List<Rectangle> cardRects = CalculateCardPositions();
            Vector2 mousePos = Core.TransformMouse(Mouse.GetState().Position);

            // Draw Cards (Front to Back order for rendering painter's algorithm)
            for (int i = 0; i < _currentLoot.Count; i++)
            {
                var item = _currentLoot[i];
                var baseRect = cardRects[i];

                // Hover Logic
                bool isHovered = baseRect.Contains(mousePos);

                // If overlapping, strictly only the top-most card should react visually to hover
                // But for simplicity in this loop, we just check bounds. 
                // Since we draw front-to-back, the last one drawn (on top) will cover others.

                float hoverOffset = isHovered ? -4f : 0f;

                // Apply a small "pop in" animation based on index
                float popDelay = i * 0.1f;
                float popProgress = Math.Clamp((_timeOpen - popDelay) / 0.3f, 0f, 1f);
                float popScale = Easing.EaseOutBack(popProgress);

                // Calculate final draw rect with hover offset
                Rectangle drawRect = new Rectangle(
                    baseRect.X,
                    baseRect.Y + (int)hoverOffset,
                    baseRect.Width,
                    baseRect.Height
                );

                if (popScale < 0.01f) continue;

                // Draw Shadow
                spriteBatch.Draw(pixel, new Rectangle(drawRect.X + 2, drawRect.Y + 2, drawRect.Width, drawRect.Height), Color.Black * 0.5f);

                // Draw Card Background (Rarity Color)
                Color cardColor = _global.RarityColors.ContainsKey(item.Rarity) ? _global.RarityColors[item.Rarity] : Color.White;
                spriteBatch.Draw(pixel, drawRect, cardColor);

                // Draw Inner Dark Background (to make sprite pop)
                Rectangle innerRect = new Rectangle(drawRect.X + 1, drawRect.Y + 1, drawRect.Width - 2, drawRect.Height - 2);
                spriteBatch.Draw(pixel, innerRect, _global.Palette_DarkGray);

                // Draw Border Highlight if hovered
                if (isHovered)
                {
                    DrawRectangleBorder(spriteBatch, pixel, drawRect, 1, _global.Palette_Yellow);
                }

                // Draw Item Sprite (Native Resolution, Centered)
                Texture2D icon = _spriteManager.GetItemSprite(item.SpritePath);
                if (icon != null)
                {
                    // Calculate centered position without scaling
                    Vector2 spritePos = new Vector2(
                        drawRect.Center.X - (icon.Width / 2),
                        drawRect.Center.Y - (icon.Height / 2)
                    );

                    spriteBatch.Draw(icon, spritePos, Color.White);
                }

                // Optional: Draw Rarity Dot in corner
                // spriteBatch.Draw(pixel, new Rectangle(drawRect.Right - 4, drawRect.Bottom - 4, 2, 2), cardColor);
            }

            // Draw Buttons
            _collectAllButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            _skipButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);

            // Draw Title
            string title = "VICTORY!";
            Vector2 titleSize = font.MeasureString(title);

            // Bobbing animation for title
            float titleBob = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f) * 2f;

            spriteBatch.DrawString(font, title, new Vector2(_lootArea.Center.X - titleSize.X / 2, _lootArea.Top - 40 + titleBob), _global.Palette_Yellow);
        }

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class SplitMapShopOverlay
    {
        public bool IsOpen { get; private set; } = false;
        public event Action OnLeaveRequested;

        private readonly GameState _gameState;
        private readonly Global _global;
        private readonly HapticsManager _hapticsManager;
        private readonly SpriteManager _spriteManager;

        private List<ShopItem> _premiumItems = new List<ShopItem>();
        private List<ShopItem> _consumableItems = new List<ShopItem>();

        private List<Button> _itemButtons = new List<Button>();
        private Button _leaveButton;

        // Layout Constants
        private const float WORLD_Y_OFFSET = 600f; // Below Settings
        private const int BUTTON_HEIGHT = 15;

        public SplitMapShopOverlay()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();

            _leaveButton = new Button(Rectangle.Empty, "LEAVE SHOP", font: ServiceLocator.Get<Core>().SecondaryFont)
            {
                CustomDefaultTextColor = _global.Palette_Red,
                CustomHoverTextColor = _global.Palette_White,
                UseScreenCoordinates = true
            };
            _leaveButton.OnClick += () => OnLeaveRequested?.Invoke();
        }

        public void Show(List<ShopItem> premiumStock, List<ShopItem> consumableStock)
        {
            IsOpen = true;
            _premiumItems = premiumStock;
            _consumableItems = consumableStock;
            RebuildButtons();
        }

        public void Resume()
        {
            if (_premiumItems.Any() || _consumableItems.Any())
            {
                IsOpen = true;
            }
        }

        public void Hide()
        {
            IsOpen = false;
        }

        private void RebuildButtons()
        {
            _itemButtons.Clear();

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            int centerX = Global.VIRTUAL_WIDTH / 2;
            int startY = (int)WORLD_Y_OFFSET + 45; // Push down to make room for headers

            // --- GEAR GRID (Left Side) ---
            // 2x2 Grid
            // Center of Left Section is roughly centerX - 80
            int gearCenterX = centerX - 80;
            int gearSpacingX = 50; // Horizontal gap between columns
            int gearSpacingY = 35; // Vertical gap between rows (accommodates text)

            for (int i = 0; i < _premiumItems.Count; i++)
            {
                if (i >= 4) break; // Max 4 items in 2x2 grid

                var item = _premiumItems[i];

                // Calculate Grid Position
                // 0: TL, 1: TR, 2: BL, 3: BR
                int col = i % 2;
                int row = i / 2;

                int x = gearCenterX + (col == 0 ? -gearSpacingX / 2 : gearSpacingX / 2) - 8; // -8 to center 16px button
                int y = startY + (row * gearSpacingY);

                // Pass secondaryFont for price, tertiaryFont for name
                var btn = CreateShopItemButton(item, x, y, secondaryFont, tertiaryFont);
                _itemButtons.Add(btn);
            }

            // --- CONSUMABLES (Right Side) ---
            // Vertical Stack
            int consumableCenterX = centerX + 80;
            int consumableSpacingY = 35;

            for (int i = 0; i < _consumableItems.Count; i++)
            {
                var item = _consumableItems[i];
                int x = consumableCenterX - 8; // Center 16px button
                int y = startY + (i * consumableSpacingY);

                // Pass secondaryFont for price, tertiaryFont for name
                var btn = CreateShopItemButton(item, x, y, secondaryFont, tertiaryFont);
                _itemButtons.Add(btn);
            }

            // --- LEAVE BUTTON ---
            // Position at absolute bottom of the screen (relative to the offset)
            int screenBottom = (int)WORLD_Y_OFFSET + Global.VIRTUAL_HEIGHT;
            int leaveMarginBottom = 10;
            int leaveY = screenBottom - BUTTON_HEIGHT - leaveMarginBottom;

            _leaveButton.Bounds = new Rectangle(centerX - 40, leaveY, 80, BUTTON_HEIGHT);
        }

        private ShopItemButton CreateShopItemButton(ShopItem item, int x, int y, BitmapFont priceFont, BitmapFont nameFont)
        {
            // Resolve Sprite
            string iconPath = "";
            if (item.Type == "Weapon") iconPath = $"Sprites/Items/Weapons/{item.ItemId}";
            else if (item.Type == "Armor") iconPath = $"Sprites/Items/Armor/{item.ItemId}";
            else if (item.Type == "Relic") iconPath = $"Sprites/Items/Relics/{item.ItemId}";
            else if (item.Type == "Consumable") iconPath = $"Sprites/Items/Consumables/{item.ItemId}";

            var icon = _spriteManager.GetItemSprite(iconPath);
            var silhouette = _spriteManager.GetItemSpriteSilhouette(iconPath);

            var btn = new ShopItemButton(new Rectangle(x, y, 16, 16), item, icon, silhouette, priceFont, nameFont);

            btn.OnClick += () => TryBuyItem(item, btn);
            return btn;
        }

        private void TryBuyItem(ShopItem item, Button btn)
        {
            if (item.IsSold) return;

            if (_gameState.PlayerState.Coin >= item.Price)
            {
                _gameState.PlayerState.Coin -= item.Price;
                item.IsSold = true;
                btn.IsEnabled = false; // ShopItemButton handles visual state for disabled/sold

                // Add to Inventory
                if (item.Type == "Weapon") _gameState.PlayerState.AddWeapon(item.ItemId);
                else if (item.Type == "Armor") _gameState.PlayerState.AddArmor(item.ItemId);
                else if (item.Type == "Relic") _gameState.PlayerState.AddRelic(item.ItemId);
                else if (item.Type == "Consumable") _gameState.PlayerState.AddConsumable(item.ItemId);

                _hapticsManager.TriggerShake(2f, 0.1f);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Bought {item.DisplayName}!" });
            }
            else
            {
                _hapticsManager.TriggerShake(4f, 0.1f);
                EventBus.Publish(new GameEvents.AlertPublished { Message = "NOT ENOUGH COIN" });
            }
        }

        public void Update(GameTime gameTime, MouseState mouseState, Matrix cameraTransform)
        {
            if (!IsOpen) return;

            // Transform mouse to world space
            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));

            // Create a fake mouse state for the buttons using world coordinates
            var worldMouseState = new MouseState((int)mouseInWorldSpace.X, (int)mouseInWorldSpace.Y, mouseState.ScrollWheelValue, mouseState.LeftButton, mouseState.MiddleButton, mouseState.RightButton, mouseState.XButton1, mouseState.XButton2);

            foreach (var btn in _itemButtons) btn.Update(worldMouseState);
            _leaveButton.Update(worldMouseState);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsOpen) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            // Draw Background
            var bgRect = new Rectangle(0, (int)WORLD_Y_OFFSET, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            spriteBatch.DrawSnapped(pixel, bgRect, _global.GameBg);

            // Draw Border
            if (_spriteManager.ShopBorderMain != null)
            {
                spriteBatch.DrawSnapped(_spriteManager.ShopBorderMain, new Vector2(0, WORLD_Y_OFFSET), Color.White);
            }

            // Title
            string title = "SHOP";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, WORLD_Y_OFFSET + 10);
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_BrightWhite);

            // Coin Display
            string coinText = $"COIN: {_gameState.PlayerState.Coin}";
            Vector2 coinSize = secondaryFont.MeasureString(coinText);
            float coinX = (Global.VIRTUAL_WIDTH - coinSize.X) / 2f;
            float coinY = _leaveButton.Bounds.Top - coinSize.Y - 4;
            spriteBatch.DrawStringSnapped(secondaryFont, coinText, new Vector2(coinX, coinY), _global.Palette_Yellow);

            // Buttons
            foreach (var btn in _itemButtons) btn.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            _leaveButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                foreach (var btn in _itemButtons)
                {
                    spriteBatch.DrawSnapped(pixel, btn.Bounds, Color.Cyan * 0.5f);
                }
                spriteBatch.DrawSnapped(pixel, _leaveButton.Bounds, Color.Red * 0.5f);
            }
        }
    }
}
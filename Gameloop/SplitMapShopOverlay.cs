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
    public class ShopItem
    {
        public string ItemId { get; set; }
        public string DisplayName { get; set; }
        public string Type { get; set; } // "Weapon", "Armor", "Relic", "Consumable"
        public int Price { get; set; }
        public bool IsSold { get; set; }
        public object DataObject { get; set; } // WeaponData, ArmorData, etc.
    }

    public class SplitMapShopOverlay
    {
        public bool IsOpen { get; private set; } = false;
        public event Action OnLeaveRequested;

        private readonly GameState _gameState;
        private readonly Global _global;
        private readonly HapticsManager _hapticsManager;

        private List<ShopItem> _premiumItems = new List<ShopItem>();
        private List<ShopItem> _consumableItems = new List<ShopItem>();

        private List<Button> _premiumButtons = new List<Button>();
        private List<Button> _consumableButtons = new List<Button>();
        private Button _leaveButton;

        // Layout Constants
        private const float WORLD_Y_OFFSET = 600f; // Below Settings
        private const int PANEL_WIDTH = 280;
        private const int PANEL_HEIGHT = 160;
        private const int BUTTON_HEIGHT = 15;
        private const int BUTTON_SPACING = 4;

        public SplitMapShopOverlay()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();

            _leaveButton = new Button(Rectangle.Empty, "LEAVE SHOP", font: ServiceLocator.Get<Core>().SecondaryFont)
            {
                CustomDefaultTextColor = _global.Palette_Red,
                CustomHoverTextColor = _global.Palette_White,
                UseScreenCoordinates = true // FIX: Essential for World Space UI
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

        public void Hide()
        {
            IsOpen = false;
        }

        private void RebuildButtons()
        {
            _premiumButtons.Clear();
            _consumableButtons.Clear();

            var font = ServiceLocator.Get<Core>().SecondaryFont;

            // Layout Calculations
            int centerX = Global.VIRTUAL_WIDTH / 2;
            int startY = (int)WORLD_Y_OFFSET + 40;

            // Premium Column (Left)
            int premiumX = centerX - 80;
            for (int i = 0; i < _premiumItems.Count; i++)
            {
                var item = _premiumItems[i];
                var btn = CreateItemButton(item, premiumX, startY + (i * (BUTTON_HEIGHT + BUTTON_SPACING)), font);
                _premiumButtons.Add(btn);
            }

            // Consumable Column (Right)
            int consumableX = centerX + 80;
            for (int i = 0; i < _consumableItems.Count; i++)
            {
                var item = _consumableItems[i];
                var btn = CreateItemButton(item, consumableX, startY + (i * (BUTTON_HEIGHT + BUTTON_SPACING)), font);
                _consumableButtons.Add(btn);
            }

            // Leave Button
            int leaveY = startY + (Math.Max(_premiumItems.Count, _consumableItems.Count) * (BUTTON_HEIGHT + BUTTON_SPACING)) + 20;
            _leaveButton.Bounds = new Rectangle(centerX - 40, leaveY, 80, BUTTON_HEIGHT);
        }

        private Button CreateItemButton(ShopItem item, int centerX, int y, BitmapFont font)
        {
            string text = item.IsSold ? "SOLD" : $"{item.DisplayName} - {item.Price}G";
            int width = 140;
            var bounds = new Rectangle(centerX - width / 2, y, width, BUTTON_HEIGHT);

            var btn = new Button(bounds, text, font: font)
            {
                IsEnabled = !item.IsSold,
                CustomDisabledTextColor = _global.Palette_DarkGray,
                UseScreenCoordinates = true // FIX: Essential for World Space UI
            };

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
                btn.Text = "SOLD";
                btn.IsEnabled = false;

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

            // Create a fake mouse state for the buttons
            var worldMouseState = new MouseState((int)mouseInWorldSpace.X, (int)mouseInWorldSpace.Y, mouseState.ScrollWheelValue, mouseState.LeftButton, mouseState.MiddleButton, mouseState.RightButton, mouseState.XButton1, mouseState.XButton2);

            foreach (var btn in _premiumButtons) btn.Update(worldMouseState);
            foreach (var btn in _consumableButtons) btn.Update(worldMouseState);
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

            // Title
            string title = "MERCHANT";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, WORLD_Y_OFFSET + 10);
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_BrightWhite);

            // Coin Display
            string coinText = $"COIN: {_gameState.PlayerState.Coin}";
            Vector2 coinSize = secondaryFont.MeasureString(coinText);
            Vector2 coinPos = new Vector2(Global.VIRTUAL_WIDTH - coinSize.X - 10, WORLD_Y_OFFSET + 10);
            spriteBatch.DrawStringSnapped(secondaryFont, coinText, coinPos, _global.Palette_Yellow);

            // Headers
            int centerX = Global.VIRTUAL_WIDTH / 2;
            int headerY = (int)WORLD_Y_OFFSET + 30;

            string gearHeader = "GEAR";
            Vector2 gearSize = secondaryFont.MeasureString(gearHeader);
            spriteBatch.DrawStringSnapped(secondaryFont, gearHeader, new Vector2(centerX - 80 - gearSize.X / 2, headerY), _global.Palette_LightBlue);

            string itemHeader = "ITEMS";
            Vector2 itemSize = secondaryFont.MeasureString(itemHeader);
            spriteBatch.DrawStringSnapped(secondaryFont, itemHeader, new Vector2(centerX + 80 - itemSize.X / 2, headerY), _global.Palette_LightBlue);

            // Buttons
            foreach (var btn in _premiumButtons) btn.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            foreach (var btn in _consumableButtons) btn.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            _leaveButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
        }
    }
}
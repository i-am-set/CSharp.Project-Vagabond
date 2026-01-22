using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

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
        private readonly Core _core;

        private List<ShopItem> _premiumItems = new List<ShopItem>();

        private List<Button> _itemButtons = new List<Button>();
        private Button _leaveButton;

        // Layout Constants
        private const float WORLD_Y_OFFSET = 600f; // Below Settings
        private const int BUTTON_HEIGHT = 15;

        public SplitMapShopOverlay()
        {
            _core = ServiceLocator.Get<Core>();
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();

            _leaveButton = new Button(Rectangle.Empty, "LEAVE", font: _core.SecondaryFont)
            {
                CustomDefaultTextColor = _global.Palette_Sun,
                CustomHoverTextColor = _global.Palette_Rust,
                UseScreenCoordinates = true
            };
            _leaveButton.OnClick += () => OnLeaveRequested?.Invoke();
        }

        public void Show(List<ShopItem> premiumStock)
        {
            IsOpen = true;
            _premiumItems = premiumStock;
            RebuildButtons();
        }

        public void Resume()
        {
            if (_premiumItems.Any())
            {
                IsOpen = true;
                // Safety check: If buttons are missing but items exist, rebuild.
                if (!_itemButtons.Any())
                {
                    RebuildButtons();
                }
            }
        }

        public void Hide()
        {
            IsOpen = false;
        }

        private void RebuildButtons()
        {
            _itemButtons.Clear();

            var secondaryFont = _core.SecondaryFont;
            var tertiaryFont = _core.TertiaryFont;

            int centerX = Global.VIRTUAL_WIDTH / 2;
            int startY = (int)WORLD_Y_OFFSET + 45; // Push down to make room for headers

            // --- GEAR GRID (Centered) ---
            // 2x2 Grid
            int gearCenterX = centerX;
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

                // Center the 32px button on the grid point
                int x = gearCenterX + (col == 0 ? -gearSpacingX / 2 : gearSpacingX / 2) - 16;
                // Adjust Y to keep the sprite visually where it was.
                int y = startY + (row * gearSpacingY) - 8;

                // Pass secondaryFont for price, tertiaryFont for currency symbol and name
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

        private ShopItemButton CreateShopItemButton(ShopItem item, int x, int y, BitmapFont priceFont, BitmapFont tertiaryFont)
        {
            // Resolve Sprite
            string iconPath = "";
            if (item.Type == "Weapon") iconPath = $"Sprites/Items/Weapons/{item.ItemId}";
            else if (item.Type == "Relic") iconPath = $"Sprites/Items/Relics/{item.ItemId}";

            var icon = _spriteManager.GetItemSprite(iconPath);
            var silhouette = _spriteManager.GetItemSpriteSilhouette(iconPath);

            // Pass tertiaryFont for both Currency Symbol and Name
            // Bounds are now 32x32 (handled in ShopItemButton constructor)
            var btn = new ShopItemButton(new Rectangle(x, y, 32, 32), item, icon, silhouette, priceFont, tertiaryFont, tertiaryFont);

            // Since we are manually calculating world-space mouse coordinates in Update(),
            // we don't want the Button class to re-transform them.
            btn.UseScreenCoordinates = true;

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
                else if (item.Type == "Relic") _gameState.PlayerState.AddRelic(item.ItemId);

                _hapticsManager.TriggerShake(10f, 0.1f);

                // Trigger Smooth Screen Flash (White, 0.75s fade)
                _core.TriggerFullscreenFlash(_global.Palette_Leaf, 0.20f);

                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Bought {item.DisplayName}!" });
            }
            else
            {
                _hapticsManager.TriggerShake(12f, 0.1f);
                EventBus.Publish(new GameEvents.AlertPublished { Message = "NOT ENOUGH COIN" });

                // Trigger the X overlay animation
                if (btn is ShopItemButton shopBtn)
                {
                    shopBtn.TriggerTooExpensiveAnimation();
                }
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
            var secondaryFont = _core.SecondaryFont;

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
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_Sun);

            // Coin Display
            string coinText = $"COIN: {_gameState.PlayerState.Coin}";
            Vector2 coinSize = secondaryFont.MeasureString(coinText);
            float coinX = (Global.VIRTUAL_WIDTH - coinSize.X) / 2f;
            float coinY = _leaveButton.Bounds.Top - coinSize.Y - 4;
            spriteBatch.DrawStringSnapped(secondaryFont, coinText, new Vector2(coinX, coinY), _global.Palette_DarkSun);

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

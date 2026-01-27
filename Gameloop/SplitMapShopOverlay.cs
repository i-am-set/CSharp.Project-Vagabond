using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;


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

        public void Show()
        {
            IsOpen = true;
            RebuildButtons();
        }

        public void Resume()
        {
            IsOpen = true;
            RebuildButtons();
        }

        public void Hide()
        {
            IsOpen = false;
        }

        private void RebuildButtons()
        {
            int centerX = Global.VIRTUAL_WIDTH / 2;

            // --- LEAVE BUTTON ---
            // Position at absolute bottom of the screen (relative to the offset)
            int screenBottom = (int)WORLD_Y_OFFSET + Global.VIRTUAL_HEIGHT;
            int leaveMarginBottom = 10;
            int leaveY = screenBottom - BUTTON_HEIGHT - leaveMarginBottom;

            _leaveButton.Bounds = new Rectangle(centerX - 40, leaveY, 80, BUTTON_HEIGHT);
        }

        public void Update(GameTime gameTime, MouseState mouseState, Matrix cameraTransform)
        {
            if (!IsOpen) return;

            // Transform mouse to world space
            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));

            // Create a fake mouse state for the buttons using world coordinates
            var worldMouseState = new MouseState((int)mouseInWorldSpace.X, (int)mouseInWorldSpace.Y, mouseState.ScrollWheelValue, mouseState.LeftButton, mouseState.MiddleButton, mouseState.RightButton, mouseState.XButton1, mouseState.XButton2);

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

            _leaveButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                spriteBatch.DrawSnapped(pixel, _leaveButton.Bounds, Color.Red * 0.5f);
            }
        }
    }
}

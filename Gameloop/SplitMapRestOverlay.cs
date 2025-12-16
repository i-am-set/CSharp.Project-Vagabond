using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class SplitMapRestOverlay
    {
        public bool IsOpen { get; private set; } = false;
        public event Action OnLeaveRequested;

        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly Core _core;

        private Button _leaveButton;

        // Layout Constants
        private const float WORLD_Y_OFFSET = 600f; // Same offset as Shop/Settings to keep camera logic consistent
        private const int BUTTON_HEIGHT = 15;

        public SplitMapRestOverlay()
        {
            _core = ServiceLocator.Get<Core>();
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();

            _leaveButton = new Button(Rectangle.Empty, "SKIP", font: _core.SecondaryFont)
            {
                CustomDefaultTextColor = _global.Palette_BrightWhite,
                CustomHoverTextColor = _global.Palette_Red,
                UseScreenCoordinates = true
            };
            _leaveButton.OnClick += () => OnLeaveRequested?.Invoke();
        }

        public void Show()
        {
            IsOpen = true;
            RebuildLayout();
        }

        public void Hide()
        {
            IsOpen = false;
        }

        private void RebuildLayout()
        {
            int centerX = Global.VIRTUAL_WIDTH / 2;

            // Position at absolute bottom of the screen (relative to the offset)
            int screenBottom = (int)WORLD_Y_OFFSET + Global.VIRTUAL_HEIGHT;
            int leaveMarginBottom = 10;
            int leaveY = screenBottom - BUTTON_HEIGHT - leaveMarginBottom;

            // Measure text to make button skinnier
            var font = _core.SecondaryFont;
            var textSize = font.MeasureString("SKIP");
            int buttonWidth = (int)textSize.Width + 16; // 8px padding on each side

            _leaveButton.Bounds = new Rectangle(centerX - buttonWidth / 2, leaveY, buttonWidth, BUTTON_HEIGHT);
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

            // Draw Border (Reuse Shop Border for now as a placeholder)
            if (_spriteManager.ShopBorderMain != null)
            {
                spriteBatch.DrawSnapped(_spriteManager.ShopBorderMain, new Vector2(0, WORLD_Y_OFFSET), Color.White);
            }

            // Title
            string title = "REST SITE";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, WORLD_Y_OFFSET + 10);
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_BrightWhite);

            _leaveButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                spriteBatch.DrawSnapped(pixel, _leaveButton.Bounds, Color.Red * 0.5f);
            }
        }
    }
}
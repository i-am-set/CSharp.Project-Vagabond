using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Editor
{
    /// <summary>
    /// A UI component for browsing and selecting action animation files.
    /// </summary>
    public class FileBrowser
    {
        public Rectangle Bounds { get; set; }
        public event Action<string, ActionData> OnFileSelected;

        private List<Button> _fileButtons = new List<Button>();
        private List<string> _filePaths = new List<string>();
        private float _scrollOffset = 0f;
        private float _totalContentHeight = 0f;
        private int _selectedIndex = -1;

        public void Populate(string directoryPath)
        {
            _fileButtons.Clear();
            _filePaths.Clear();
            _selectedIndex = -1;

            if (!Directory.Exists(directoryPath)) return;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories)
                                 .OrderBy(f => Path.GetFileName(f));

            foreach (var file in files)
            {
                try
                {
                    string jsonContent = File.ReadAllText(file);
                    var actionData = JsonSerializer.Deserialize<ActionData>(jsonContent, jsonOptions);
                    if (actionData != null && actionData.Timeline != null)
                    {
                        var localFile = file; // Capture the variable for the lambda
                        var button = new Button(Rectangle.Empty, Path.GetFileNameWithoutExtension(file), alignLeft: true, enableHoverSway: false);
                        button.OnClick += () => OnFileSelected?.Invoke(localFile, actionData);
                        _fileButtons.Add(button);
                        _filePaths.Add(file);
                    }
                }
                catch (Exception) { /* Ignore files that fail to parse */ }
            }
        }

        public void Update(GameTime gameTime, bool isEditMode)
        {
            if (isEditMode) return; // Ignore all input if in edit mode

            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);

            if (!Bounds.Contains(virtualMousePos)) return;

            // Handle Scrolling
            int scrollDelta = mouseState.ScrollWheelValue - ServiceLocator.Get<Global>().previousScrollValue;
            if (scrollDelta != 0)
            {
                _scrollOffset += scrollDelta * -0.1f; // Adjust multiplier for scroll speed
            }

            float maxScroll = Math.Max(0, _totalContentHeight - Bounds.Height + 10);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

            // Update Buttons
            for (int i = 0; i < _fileButtons.Count; i++)
            {
                var button = _fileButtons[i];
                button.Update(mouseState);
                if (button.IsHovered)
                {
                    _selectedIndex = i;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var global = ServiceLocator.Get<Global>();

            // This component manages its own SpriteBatch begin/end to use a scissor rectangle for clipping.
            var originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState { ScissorTestEnable = true }, null, transform);

            // Draw panel background and border (inside the new batch)
            spriteBatch.Draw(pixel, Bounds, global.TerminalBg);
            spriteBatch.Draw(pixel, new Rectangle(Bounds.Right, Bounds.Top, 2, Bounds.Height), global.Palette_White);
            spriteBatch.Draw(pixel, new Rectangle(Bounds.Left, Bounds.Top, Bounds.Width, 2), global.Palette_White);
            spriteBatch.Draw(pixel, new Rectangle(Bounds.Left, Bounds.Bottom, Bounds.Width + 2, 2), global.Palette_White);
            spriteBatch.Draw(pixel, new Rectangle(Bounds.Left, Bounds.Top, 2, Bounds.Height), global.Palette_White);


            spriteBatch.GraphicsDevice.ScissorRectangle = Bounds;

            float currentY = Bounds.Y + 5 - _scrollOffset;
            const int buttonHeight = 18;

            for (int i = 0; i < _fileButtons.Count; i++)
            {
                var button = _fileButtons[i];
                button.Bounds = new Rectangle(Bounds.X + 5, (int)currentY, Bounds.Width - 10, buttonHeight);

                bool isSelected = i == _selectedIndex;
                if (isSelected)
                {
                    spriteBatch.Draw(pixel, button.Bounds, global.Palette_DarkGray);
                }

                button.Draw(spriteBatch, font, gameTime, isSelected);
                currentY += buttonHeight;
            }
            _totalContentHeight = (currentY + _scrollOffset) - Bounds.Y;

            // Restore original ScissorRectangle and end the custom batch
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
            spriteBatch.End();
        }
    }
}
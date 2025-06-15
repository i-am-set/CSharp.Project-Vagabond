using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class SettingsScene : GameScene
    {
        private List<object> _uiElements = new();
        private int _selectedIndex = 0;
        private string _confirmationMessage = "";
        private float _confirmationTimer = 0f;

        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;

        private GameSettings _tempSettings;

        public override void Enter()
        {
            _tempSettings = new GameSettings
            {
                Resolution = Core.Settings.Resolution,
                IsFullscreen = Core.Settings.IsFullscreen,
                IsVsync = Core.Settings.IsVsync,
                IsFrameLimiterEnabled = Core.Settings.IsFrameLimiterEnabled,
                TargetFramerate = Core.Settings.TargetFramerate,
                UseImperialUnits = Core.Settings.UseImperialUnits,
                Use24HourClock = Core.Settings.Use24HourClock
            };

            BuildInitialUI();
            _selectedIndex = FindNextSelectable(-1, 1);
        }

        private void BuildInitialUI()
        {
            _uiElements.Clear();

            // --- Graphics Settings ---
            _uiElements.Add("Graphics");
            var resolutions = new List<KeyValuePair<string, Point>>
            {
                new("960 x 540", new Point(960, 540)),
                new("1280 x 720", new Point(1280, 720)),
                new("1366 x 768", new Point(1366, 768)),
                new("1600 x 900", new Point(1600, 900)),
                new("1920 x 1080", new Point(1920, 1080)),
            };
            _uiElements.Add(new OptionSettingControl<Point>("Resolution", resolutions, () => _tempSettings.Resolution, v => _tempSettings.Resolution = v));
            _uiElements.Add(new BoolSettingControl("Fullscreen", () => _tempSettings.IsFullscreen, v => _tempSettings.IsFullscreen = v));
            _uiElements.Add(new BoolSettingControl("VSync", () => _tempSettings.IsVsync, v => _tempSettings.IsVsync = v));
            _uiElements.Add(new BoolSettingControl("Frame Limiter", () => _tempSettings.IsFrameLimiterEnabled, v => _tempSettings.IsFrameLimiterEnabled = v));

            // --- Game Settings ---
            _uiElements.Add("Game");
            _uiElements.Add(new BoolSettingControl("24-Hour Clock", () => _tempSettings.Use24HourClock, v => _tempSettings.Use24HourClock = v));
            _uiElements.Add(new BoolSettingControl("Imperial Units", () => _tempSettings.UseImperialUnits, v => _tempSettings.UseImperialUnits = v));

            // --- Controls ---
            _uiElements.Add("Controls");

            // --- Action Buttons ---
            var applyButton = new Button(new Rectangle(0, 0, 250, 20), "Apply");
            applyButton.OnClick += ApplySettings;
            _uiElements.Add(applyButton);

            var backButton = new Button(new Rectangle(0, 0, 250, 20), "Back");
            backButton.OnClick += BackToPreviousScene;
            _uiElements.Add(backButton);

            // Dynamically add the framerate control if needed on initial build
            UpdateFramerateControl();
        }

        /// <summary>
        /// FIX: Dynamically adds or removes the Target Framerate control without rebuilding the whole UI.
        /// This preserves the "dirty" state of other controls.
        /// </summary>
        private void UpdateFramerateControl()
        {
            var framerateControl = _uiElements.OfType<ISettingControl>().FirstOrDefault(c => c.Label == "Target Framerate");
            int limiterIndex = _uiElements.FindIndex(item => item is ISettingControl s && s.Label == "Frame Limiter");

            if (_tempSettings.IsFrameLimiterEnabled && framerateControl == null && limiterIndex != -1)
            {
                var framerates = new List<KeyValuePair<string, int>>
                {
                    new("30 FPS", 30), new("60 FPS", 60), new("75 FPS", 75),
                    new("120 FPS", 120), new("144 FPS", 144), new("240 FPS", 240)
                };
                var newControl = new OptionSettingControl<int>("Target Framerate", framerates, () => _tempSettings.TargetFramerate, v => _tempSettings.TargetFramerate = v);
                _uiElements.Insert(limiterIndex + 1, newControl);
            }
            else if (!_tempSettings.IsFrameLimiterEnabled && framerateControl != null)
            {
                _uiElements.Remove(framerateControl);
            }
        }

        private void ApplySettings()
        {
            Core.Settings.Resolution = _tempSettings.Resolution;
            Core.Settings.IsFullscreen = _tempSettings.IsFullscreen;
            Core.Settings.IsVsync = _tempSettings.IsVsync;
            Core.Settings.IsFrameLimiterEnabled = _tempSettings.IsFrameLimiterEnabled;
            Core.Settings.TargetFramerate = _tempSettings.TargetFramerate;
            Core.Settings.UseImperialUnits = _tempSettings.UseImperialUnits;
            Core.Settings.Use24HourClock = _tempSettings.Use24HourClock;

            Core.Settings.ApplyGraphicsSettings(Global.Instance.CurrentGraphics, Core.Instance);
            Core.Settings.ApplyGameSettings();

            foreach (var item in _uiElements.OfType<ISettingControl>()) item.Apply();

            _confirmationMessage = "Settings Applied!";
            _confirmationTimer = 3f;
        }

        private static void BackToPreviousScene()
        {
            Core.CurrentSceneManager.ChangeScene(GameSceneState.MainMenu);
        }

        private bool IsDirty() => _uiElements.OfType<ISettingControl>().Any(s => s.IsDirty);

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);

        public override void Update(GameTime gameTime)
        {
            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

            if (_confirmationTimer > 0)
            {
                _confirmationTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            // This now dynamically adds/removes the control instead of rebuilding
            UpdateFramerateControl();

            // --- Mouse hover detection for selection ---
            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            Vector2 currentPos = new Vector2(0, 50); // Match the Draw method starting position

            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                currentPos.X = (Global.VIRTUAL_WIDTH - 450) / 2;

                if (item is ISettingControl setting)
                {
                    // Create hover rectangle for setting control (match Draw method positioning)
                    var hoverRect = new Rectangle((int)currentPos.X - 5, (int)currentPos.Y + 5, 460, 20);
                    if (hoverRect.Contains(virtualMousePos))
                    {
                        _selectedIndex = i;
                    }
                    currentPos.Y += 20; // Match Draw method increment
                }
                else if (item is Button button)
                {
                    // Create hover rectangle for button
                    var buttonRect = new Rectangle((Global.VIRTUAL_WIDTH - button.Bounds.Width) / 2, (int)currentPos.Y, button.Bounds.Width, button.Bounds.Height);
                    if (buttonRect.Contains(virtualMousePos))
                    {
                        _selectedIndex = i;
                    }
                    currentPos.Y += 25; // Match Draw method increment
                }
                else if (item is string)
                {
                    currentPos.Y += 5; // Match Draw method increment for headers
                    currentPos.Y += 20; // Match the additional increment after drawing divider
                }
            }

            HandleKeyboardInput(currentKeyboardState);

            // --- Update all UI elements ---
            // The individual controls' Update methods handle their own mouse logic.
            currentPos = new Vector2(0, 150);
            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                currentPos.X = (Global.VIRTUAL_WIDTH - 450) / 2;

                if (item is ISettingControl setting)
                {
                    // FIX: Mouse hover is now handled inside the control's own update for clicks.
                    // The selection highlight is now purely for keyboard focus.
                    setting.Update(currentPos, i == _selectedIndex, currentMouseState, _previousMouseState);
                    currentPos.Y += 40;
                }
                else if (item is Button button)
                {
                    button.Update(currentMouseState);
                    currentPos.Y += 55;
                }
                else if (item is string)
                {
                    currentPos.Y += 45;
                }
            }

            var applyButton = _uiElements.OfType<Button>().FirstOrDefault(b => b.Text == "Apply");
            if (applyButton != null) applyButton.IsEnabled = IsDirty();

            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                BackToPreviousScene();
            }

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;
        }

        private void HandleKeyboardInput(KeyboardState currentKeyboardState)
        {
            if (KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState))
            {
                _selectedIndex = FindNextSelectable(_selectedIndex, 1);
            }
            if (KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState))
            {
                _selectedIndex = FindNextSelectable(_selectedIndex, -1);
            }

            // Only process input if the selected index is valid
            if (_selectedIndex >= 0 && _selectedIndex < _uiElements.Count)
            {
                var selectedItem = _uiElements[_selectedIndex];
                if (selectedItem is ISettingControl setting)
                {
                    if (KeyPressed(Keys.Left, currentKeyboardState, _previousKeyboardState)) setting.HandleInput(Keys.Left);
                    if (KeyPressed(Keys.Right, currentKeyboardState, _previousKeyboardState)) setting.HandleInput(Keys.Right);
                    if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState) && IsDirty()) ApplySettings();
                }
                else if (selectedItem is Button button)
                {
                    if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState)) button.TriggerClick();
                }
            }
        }

        private int FindNextSelectable(int currentIndex, int direction)
        {
            int newIndex = currentIndex;
            for (int i = 0; i < _uiElements.Count; i++) // Safety loop
            {
                newIndex += direction;
                if (newIndex < 0 || newIndex >= _uiElements.Count)
                {
                    return currentIndex; // Stop at the ends
                }
                if (_uiElements[newIndex] is ISettingControl || _uiElements[newIndex] is Button)
                {
                    return newIndex; // Found a selectable item
                }
            }
            return currentIndex; // Should not be reached
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = Global.Instance.CurrentSpriteBatch;
            var font = Global.Instance.DefaultFont;
            int screenWidth = Global.VIRTUAL_WIDTH;
            int screenHeight = Global.VIRTUAL_HEIGHT;

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            Core.Pixel.SetData(new[] { Color.White });

            string title = "Settings";
            Vector2 titleSize = font.MeasureString(title) * 2f;
            spriteBatch.DrawString(font, title, new Vector2(screenWidth / 2 - titleSize.X / 2, 10), Global.Instance.palette_BrightWhite, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);

            Vector2 currentPos = new Vector2(0, 50);
            Vector2 buttonPos = new Vector2(0, 65);
            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                bool isSelected = (i == _selectedIndex);
                currentPos.X = (screenWidth - 450) / 2;

                // Draw highlight for keyboard-selected item
                if (isSelected)
                {
                    float itemHeight = 20;
                    if (item is Button) itemHeight = 20;
                    else if (item is string) itemHeight = 0; // Don't highlight headers

                    if (itemHeight > 0)
                    {
                        var highlightRect = new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, 460, (int)itemHeight);
                        DrawRectangleBorder(spriteBatch, Core.Pixel, highlightRect, 2, Global.Instance.palette_Yellow);
                    }
                }

                if (item is ISettingControl setting)
                {
                    setting.Draw(spriteBatch, new Vector2(currentPos.X, currentPos.Y + 5), isSelected);
                    currentPos.Y += 20;
                }
                else if (item is Button button)
                {
                    button.Bounds = new Rectangle((screenWidth - button.Bounds.Width) / 2, (int)currentPos.Y, button.Bounds.Width, button.Bounds.Height);
                    button.Draw(spriteBatch, font);
                    currentPos.Y += 25;
                }
                else if (item is string header)
                {
                    currentPos.Y += 5;
                    spriteBatch.DrawString(font, header, new Vector2(screenWidth / 2 - font.MeasureString(header).Width / 2, currentPos.Y), Global.Instance.palette_LightGray);
                    var dividerRect = new Rectangle(screenWidth / 2 - 180, (int)currentPos.Y + 10, 360, 1);
                    spriteBatch.Draw(Core.Pixel, dividerRect, Global.Instance.palette_Gray);
                    currentPos.Y += 20;
                }
            }

            if (_confirmationTimer > 0)
            {
                Vector2 msgSize = font.MeasureString(_confirmationMessage);
                spriteBatch.DrawString(font, _confirmationMessage, new Vector2(screenWidth / 2 - msgSize.X / 2, Global.VIRTUAL_HEIGHT - 50), Global.Instance.palette_Teal);
            }
            spriteBatch.End();
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

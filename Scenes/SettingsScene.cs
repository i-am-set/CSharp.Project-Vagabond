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
        private int _hoveredIndex = -1; // NEW: Tracks the element currently under the mouse
        private string _confirmationMessage = "";
        private float _confirmationTimer = 0f;
        private int _settingsStartY = 105;

        // ADDED: Input delay to prevent click-through from previous scene
        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private bool _keyboardNavigatedLastFrame = false;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;

        private GameSettings _tempSettings;
        private List<KeyValuePair<string, Point>> _resolutions;

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

            // Initialize the resolutions list
            _resolutions = new List<KeyValuePair<string, Point>>
            {
                new("960 x 540", new Point(960, 540)),
                new("1280 x 720", new Point(1280, 720)),
                new("1366 x 768", new Point(1366, 768)),
                new("1600 x 900", new Point(1600, 900)),
                new("1920 x 1080", new Point(1920, 1080)),
            };

            BuildInitialUI();
            _selectedIndex = FindNextSelectable(-1, 1);
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
            Core.Instance.IsMouseVisible = true;

            _currentInputDelay = _inputDelay; // ADDED: Start the input delay timer
        }

        private void BuildInitialUI()
        {
            _uiElements.Clear();

            // Graphics Settings //
            _uiElements.Add("Graphics");

            _uiElements.Add(new OptionSettingControl<Point>("Resolution", _resolutions, () => _tempSettings.Resolution, v => _tempSettings.Resolution = v));

            _uiElements.Add(new BoolSettingControl("Fullscreen", () => _tempSettings.IsFullscreen, v => {
                _tempSettings.IsFullscreen = v;
                if (v) // When enabling fullscreen
                {
                    SetResolutionToNearestNative();
                }
            }));

            _uiElements.Add(new BoolSettingControl("VSync", () => _tempSettings.IsVsync, v => _tempSettings.IsVsync = v));
            _uiElements.Add(new BoolSettingControl("Frame Limiter", () => _tempSettings.IsFrameLimiterEnabled, v => _tempSettings.IsFrameLimiterEnabled = v));

            // Game Settings //
            _uiElements.Add("Game");
            _uiElements.Add(new BoolSettingControl("24-Hour Clock", () => _tempSettings.Use24HourClock, v => _tempSettings.Use24HourClock = v));
            _uiElements.Add(new BoolSettingControl("Imperial Units", () => _tempSettings.UseImperialUnits, v => _tempSettings.UseImperialUnits = v));

            // Controls //
            _uiElements.Add("Controls");

            // Action Buttons //
            var applyButton = new Button(new Rectangle(0, 0, 250, 20), "Apply");
            applyButton.OnClick += ApplySettings;
            _uiElements.Add(applyButton);

            var backButton = new Button(new Rectangle(0, 0, 250, 20), "Back");
            backButton.OnClick += BackToPreviousScene;
            _uiElements.Add(backButton);

            UpdateFramerateControl();
        }

        private void SetResolutionToNearestNative()
        {
            var nativeResolution = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            Point nativePoint = new Point(nativeResolution.Width, nativeResolution.Height);

            Point closestResolution = _resolutions[0].Value;
            double minDistance = double.MaxValue;

            foreach (var resolution in _resolutions)
            {
                double distance = Math.Sqrt(
                    Math.Pow(resolution.Value.X - nativePoint.X, 2) +
                    Math.Pow(resolution.Value.Y - nativePoint.Y, 2)
                );

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestResolution = resolution.Value;
                }
            }

            _tempSettings.Resolution = closestResolution;

            var resolutionControl = _uiElements.OfType<ISettingControl>().FirstOrDefault(c => c.Label == "Resolution");
            if (resolutionControl != null)
            {
                resolutionControl.RefreshValue();
            }
        }

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
            _confirmationTimer = 5f;
        }

        private static void BackToPreviousScene()
        {
            Core.CurrentSceneManager.ChangeScene(GameSceneState.MainMenu);
        }

        private bool IsDirty() => _uiElements.OfType<ISettingControl>().Any(s => s.IsDirty);

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);

        private void MoveMouseToSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _uiElements.Count) return;

            Vector2 currentPos = new Vector2(0, _settingsStartY);

            for (int i = 0; i <= _selectedIndex; i++)
            {
                var item = _uiElements[i];
                currentPos.X = (Global.VIRTUAL_WIDTH - 450) / 2;

                if (i == _selectedIndex)
                {
                    Point mousePos = Point.Zero;

                    if (item is ISettingControl)
                    {
                        mousePos = new Point((int)currentPos.X + 230, (int)currentPos.Y + 10);
                    }
                    else if (item is Button button)
                    {
                        mousePos = new Point(Global.VIRTUAL_WIDTH / 2, (int)currentPos.Y + 10);
                    }

                    Point screenPos = Core.TransformVirtualToScreen(mousePos);
                    Mouse.SetPosition(screenPos.X, screenPos.Y);
                    break;
                }

                if (item is ISettingControl)
                {
                    currentPos.Y += 20;
                }
                else if (item is Button)
                {
                    currentPos.Y += 25;
                }
                else if (item is string)
                {
                    currentPos.Y += 5;
                    currentPos.Y += 20;
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();
            _hoveredIndex = -1;

            if (_currentInputDelay > 0)
            {
                _currentInputDelay -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            if (_keyboardNavigatedLastFrame)
            {
                _keyboardNavigatedLastFrame = false;
            }
            else if (currentMouseState.Position != _previousMouseState.Position)
            {
                Core.Instance.IsMouseVisible = true;
            }

            if (_confirmationTimer > 0)
            {
                _confirmationTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            UpdateFramerateControl();

            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            Vector2 currentPos = new Vector2(0, _settingsStartY);

            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                bool isSelected = (i == _selectedIndex);
                currentPos.X = (Global.VIRTUAL_WIDTH - 450) / 2;

                if (item is ISettingControl setting)
                {
                    Vector2 drawPosition = new Vector2(currentPos.X, currentPos.Y + 5);

                    var hoverRect = new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, 460, 20);
                    if (hoverRect.Contains(virtualMousePos))
                    {
                        _selectedIndex = i;
                        _hoveredIndex = i;
                    }

                    if (_currentInputDelay <= 0)
                    {
                        setting.Update(drawPosition, isSelected, currentMouseState, _previousMouseState);
                    }

                    currentPos.Y += 20;
                }
                else if (item is Button button)
                {
                    button.Bounds = new Rectangle((Global.VIRTUAL_WIDTH - button.Bounds.Width) / 2, (int)currentPos.Y, button.Bounds.Width, button.Bounds.Height);

                    if (button.Bounds.Contains(virtualMousePos))
                    {
                        _selectedIndex = i;
                        _hoveredIndex = i;
                    }

                    if (_currentInputDelay <= 0)
                    {
                        button.Update(currentMouseState);
                    }

                    currentPos.Y += 25;
                }
                else if (item is string)
                {
                    currentPos.Y += 5;
                    currentPos.Y += 20;
                }
            }

            if (_currentInputDelay <= 0)
            {
                HandleKeyboardInput(currentKeyboardState);
            }

            var applyButton = _uiElements.OfType<Button>().FirstOrDefault(b => b.Text == "Apply");
            if (applyButton != null) applyButton.IsEnabled = IsDirty();

            if (_currentInputDelay <= 0 && KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                BackToPreviousScene();
            }

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;
        }

        private void HandleKeyboardInput(KeyboardState currentKeyboardState)
        {
            bool selectionChanged = false;
            if (KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState))
            {
                _selectedIndex = FindNextSelectable(_selectedIndex, 1);
                selectionChanged = true;
            }
            if (KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState))
            {
                _selectedIndex = FindNextSelectable(_selectedIndex, -1);
                selectionChanged = true;
            }

            if (selectionChanged)
            {
                MoveMouseToSelected();
                Core.Instance.IsMouseVisible = false;
                _keyboardNavigatedLastFrame = true;
            }

            if (_selectedIndex >= 0 && _selectedIndex < _uiElements.Count)
            {
                var selectedItem = _uiElements[_selectedIndex];
                if (selectedItem is ISettingControl setting)
                {
                    if (KeyPressed(Keys.Left, currentKeyboardState, _previousKeyboardState)) setting.HandleInput(Keys.Left);
                    if (KeyPressed(Keys.Right, currentKeyboardState, _previousKeyboardState)) setting.HandleInput(Keys.Right);

                    if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
                    {
                        if (_selectedIndex == _hoveredIndex && IsDirty())
                        {
                            ApplySettings();
                        }
                    }
                }
                else if (selectedItem is Button button)
                {
                    if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
                    {
                        if (button.IsHovered)
                        {
                            button.TriggerClick();
                        }
                    }
                }
            }
        }

        private int FindNextSelectable(int currentIndex, int direction)
        {
            int searchIndex = currentIndex;

            for (int i = 0; i < _uiElements.Count; i++)
            {
                searchIndex += direction;

                if (searchIndex >= _uiElements.Count)
                {
                    searchIndex = 0;
                }
                else if (searchIndex < 0)
                {
                    searchIndex = _uiElements.Count - 1;
                }

                var item = _uiElements[searchIndex];

                if (item is ISettingControl || (item is Button button && button.IsEnabled))
                {
                    return searchIndex;
                }
            }

            return currentIndex;
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = Global.Instance.CurrentSpriteBatch;
            var font = Global.Instance.DefaultFont;
            int screenWidth = Global.VIRTUAL_WIDTH;
            var virtualMousePos = Core.TransformMouse(Mouse.GetState().Position);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            Core.Pixel.SetData(new[] { Color.White });

            string title = "Settings";
            Vector2 titleSize = font.MeasureString(title) * 2f;
            spriteBatch.DrawString(font, title, new Vector2(screenWidth / 2 - titleSize.X / 2, 75), Global.Instance.Palette_BrightWhite, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);

            Vector2 currentPos = new Vector2(0, _settingsStartY);
            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                bool isSelected = (i == _selectedIndex);
                currentPos.X = (screenWidth - 450) / 2;

                if (isSelected)
                {
                    bool isHovered = false;
                    if (item is ISettingControl)
                    {
                        var hoverRect = new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, 460, 20);
                        isHovered = hoverRect.Contains(virtualMousePos);
                    }
                    else if (item is Button button)
                    {
                        isHovered = button.IsHovered;
                    }

                    if (isHovered || _keyboardNavigatedLastFrame)
                    {
                        float itemHeight = 20;
                        if (item is Button) itemHeight = 20;
                        else if (item is string) itemHeight = 0;

                        if (itemHeight > 0)
                        {
                            var highlightRect = new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, 460, (int)itemHeight);
                            DrawRectangleBorder(spriteBatch, Core.Pixel, highlightRect, 1, Global.Instance.OptionHoverColor);
                        }
                    }
                }

                if (item is ISettingControl setting)
                {
                    setting.Draw(spriteBatch, new Vector2(currentPos.X, currentPos.Y + 5), isSelected);
                    currentPos.Y += 20;
                }
                else if (item is Button button)
                {
                    button.Draw(spriteBatch, font);
                    currentPos.Y += 25;
                }
                else if (item is string header)
                {
                    currentPos.Y += 5;
                    spriteBatch.DrawString(font, header, new Vector2(screenWidth / 2 - font.MeasureString(header).Width / 2, currentPos.Y), Global.Instance.Palette_LightGray);
                    var dividerRect = new Rectangle(screenWidth / 2 - 180, (int)currentPos.Y + 10, 360, 1);
                    spriteBatch.Draw(Core.Pixel, dividerRect, Global.Instance.Palette_Gray);
                    currentPos.Y += 20;
                }
            }

            if (_confirmationTimer > 0)
            {
                Vector2 msgSize = font.MeasureString(_confirmationMessage);
                spriteBatch.DrawString(font, _confirmationMessage, new Vector2(screenWidth / 2 - msgSize.X / 2, Global.VIRTUAL_HEIGHT - 50), Global.Instance.Palette_Teal);
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
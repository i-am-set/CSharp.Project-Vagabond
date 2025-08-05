using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class SettingsScene : GameScene
    {
        private readonly GameSettings _settings;
        private readonly SceneManager _sceneManager;
        private readonly GraphicsDeviceManager _graphics;
        private readonly Global _global;

        private List<object> _uiElements = new();
        private int _selectedIndex = -1;
        private int _hoveredIndex = -1;
        private string _confirmationMessage = "";
        private float _confirmationTimer = 0f;
        private int _settingsStartY = 105;

        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private float _titleBobTimer = 0f;
        private const float TitleBobAmount = 3f;
        private const float TitleBobSpeed = 2f;

        private GameSettings _tempSettings;
        private ConfirmationDialog _confirmationDialog;

        public GameSceneState ReturnScene { get; set; } = GameSceneState.MainMenu;

        public SettingsScene()
        {
            _settings = ServiceLocator.Get<GameSettings>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _graphics = ServiceLocator.Get<GraphicsDeviceManager>();
            _global = ServiceLocator.Get<Global>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Enter()
        {
            base.Enter();
            _confirmationDialog = new ConfirmationDialog(this);
            
            RefreshUIFromSettings();

            if (this.LastUsedInputForNav == InputDevice.Keyboard)
            {
                _selectedIndex = FindNextSelectable(-1, 1);
                PositionMouseOnFirstSelectable();
            }
            else
            {
                _selectedIndex = -1;
                _hoveredIndex = -1;
            }

            _previousKeyboardState = Keyboard.GetState();
            _currentInputDelay = _inputDelay;
        }

        /// <summary>
        /// Refreshes the temporary settings from the master settings and rebuilds the UI.
        /// This is called on scene entry and when the window is resized externally.
        /// </summary>
        public void RefreshUIFromSettings()
        {
            _tempSettings = new GameSettings
            {
                Resolution = _settings.Resolution,
                Mode = _settings.Mode,
                IsVsync = _settings.IsVsync,
                IsFrameLimiterEnabled = _settings.IsFrameLimiterEnabled,
                TargetFramerate = _settings.TargetFramerate,
                SmallerUi = _settings.SmallerUi,
                UseImperialUnits = _settings.UseImperialUnits,
                Use24HourClock = _settings.Use24HourClock
            };
            _titleBobTimer = 0f;
            BuildInitialUI();
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
            Vector2 currentPos = new Vector2(0, _settingsStartY);
            currentPos.X = (Global.VIRTUAL_WIDTH - 450) / 2;

            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                if (item is ISettingControl || item is Button)
                {
                    float itemHeight = (item is ISettingControl) ? 20 : 25;
                    return new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, 460, (int)itemHeight);
                }
                if (item is ISettingControl) currentPos.Y += 20;
                else if (item is Button) currentPos.Y += 25;
                else if (item is string) currentPos.Y += 25;
            }
            return null;
        }

        private void BuildInitialUI()
        {
            _uiElements.Clear();
            _uiElements.Add("Graphics");

            var resolutions = SettingsManager.GetResolutions();
            var resolutionDisplayList = resolutions.Select(kvp =>
            {
                string display = kvp.Key;
                int aspectIndex = display.IndexOf(" (");
                if (aspectIndex != -1) display = display.Substring(0, aspectIndex);
                return new KeyValuePair<string, Point>(display.Trim(), kvp.Value);
            }).ToList();

            // Check if the current resolution is non-standard and add a "CUSTOM" entry if it is.
            bool isCustomResolution = !resolutionDisplayList.Any(kvp => kvp.Value == _tempSettings.Resolution);
            if (isCustomResolution)
            {
                resolutionDisplayList.Insert(0, new KeyValuePair<string, Point>("CUSTOM", _tempSettings.Resolution));
            }

            var windowModes = new List<KeyValuePair<string, WindowMode>>
            {
                new("Windowed", WindowMode.Windowed),
                new("Borderless", WindowMode.Borderless),
                new("Fullscreen", WindowMode.Fullscreen)
            };

            var resolutionControl = new OptionSettingControl<Point>("Resolution", resolutionDisplayList, () => _tempSettings.Resolution, v => _tempSettings.Resolution = v);
            resolutionControl.GetValueColor = (pointValue) =>
            {
                bool isStandard = SettingsManager.GetResolutions().Any(r => r.Value == pointValue);
                return isStandard ? (Color?)null : _global.Palette_LightYellow;
            };
            _uiElements.Add(resolutionControl);

            _uiElements.Add(new OptionSettingControl<WindowMode>("Window Mode", windowModes, () => _tempSettings.Mode, v => { _tempSettings.Mode = v; if (v == WindowMode.Borderless) SetResolutionToNative(); }));
            _uiElements.Add(new BoolSettingControl("Smaller UI", () => _tempSettings.SmallerUi, v => _tempSettings.SmallerUi = v));
            _uiElements.Add(new BoolSettingControl("VSync", () => _tempSettings.IsVsync, v => _tempSettings.IsVsync = v));
            _uiElements.Add(new BoolSettingControl("Frame Limiter", () => _tempSettings.IsFrameLimiterEnabled, v => _tempSettings.IsFrameLimiterEnabled = v));

            var applyButton = new Button(new Rectangle(0, 0, 250, 20), "Apply");
            applyButton.OnClick += ConfirmApplySettings;
            _uiElements.Add(applyButton);

            var backButton = new Button(new Rectangle(0, 0, 250, 20), "Back");
            backButton.OnClick += AttemptToGoBack;
            _uiElements.Add(backButton);

            var resetButton = new Button(new Rectangle(0, 0, 250, 20), "Reset to Default");
            resetButton.OnClick += ConfirmResetSettings;
            _uiElements.Add(resetButton);

            UpdateFramerateControl();
            LayoutUI();
            applyButton.IsEnabled = IsDirty();
        }

        private void LayoutUI()
        {
            Vector2 currentPos = new Vector2(0, _settingsStartY);
            int screenWidth = Global.VIRTUAL_WIDTH;
            foreach (var item in _uiElements)
            {
                if (item is Button button)
                {
                    button.Bounds = new Rectangle((screenWidth - button.Bounds.Width) / 2, (int)currentPos.Y, button.Bounds.Width, button.Bounds.Height);
                }
                if (item is ISettingControl) currentPos.Y += 20;
                else if (item is Button) currentPos.Y += 25;
                else if (item is string) currentPos.Y += 25;
            }
        }

        private void SetResolutionToNative()
        {
            var nativeResolution = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            var nativePoint = new Point(nativeResolution.Width, nativeResolution.Height);
            _tempSettings.Resolution = SettingsManager.FindClosestResolution(nativePoint);
            _uiElements.OfType<ISettingControl>().FirstOrDefault(c => c.Label == "Resolution")?.RefreshValue();
        }

        private void UpdateFramerateControl()
        {
            var framerateControl = _uiElements.OfType<ISettingControl>().FirstOrDefault(c => c.Label == "Target Framerate");
            int limiterIndex = _uiElements.FindIndex(item => item is ISettingControl s && s.Label == "Frame Limiter");
            bool changed = false;

            if (_tempSettings.IsFrameLimiterEnabled && framerateControl == null && limiterIndex != -1)
            {
                var framerates = new List<KeyValuePair<string, int>> { new("30 FPS", 30), new("60 FPS", 60), new("75 FPS", 75), new("120 FPS", 120), new("144 FPS", 144), new("240 FPS", 240) };
                var newControl = new OptionSettingControl<int>("Target Framerate", framerates, () => _tempSettings.TargetFramerate, v => _tempSettings.TargetFramerate = v);
                _uiElements.Insert(limiterIndex + 1, newControl);
                changed = true;
            }
            else if (!_tempSettings.IsFrameLimiterEnabled && framerateControl != null)
            {
                _uiElements.Remove(framerateControl);
                changed = true;
            }
            if (changed) LayoutUI();
        }

        private void ConfirmApplySettings()
        {
            if (!IsDirty()) return;
            var details = _uiElements.OfType<ISettingControl>().Where(c => c.IsDirty).Select(c => $"{c.Label}: {c.GetSavedValueAsString()} -> {c.GetCurrentValueAsString()}").ToList();
            _confirmationDialog.Show("Apply the following changes?", new List<Tuple<string, Action>> { Tuple.Create("YES", new Action(() => { ExecuteApplySettings(); _confirmationDialog.Hide(); })), Tuple.Create("[gray]NO", new Action(() => _confirmationDialog.Hide())) }, details);
        }

        private void ExecuteApplySettings()
        {
            _settings.Resolution = _tempSettings.Resolution;
            _settings.Mode = _tempSettings.Mode;
            _settings.IsVsync = _tempSettings.IsVsync;
            _settings.IsFrameLimiterEnabled = _tempSettings.IsFrameLimiterEnabled;
            _settings.TargetFramerate = _tempSettings.TargetFramerate;
            _settings.SmallerUi = _tempSettings.SmallerUi;
            _settings.UseImperialUnits = _tempSettings.UseImperialUnits;
            _settings.Use24HourClock = _tempSettings.Use24HourClock;

            _settings.ApplyGraphicsSettings(_graphics, _core);
            _settings.ApplyGameSettings();
            SettingsManager.SaveSettings(_settings);

            foreach (var item in _uiElements.OfType<ISettingControl>()) item.Apply();
            _confirmationMessage = "Settings Applied!";
            _confirmationTimer = 5f;
            MoveMouseToSelected();
        }

        private void RevertChanges()
        {
            foreach (var item in _uiElements.OfType<ISettingControl>()) item.Revert();
            _tempSettings.Resolution = _settings.Resolution;
            _tempSettings.Mode = _settings.Mode;
            _tempSettings.IsVsync = _settings.IsVsync;
            _tempSettings.IsFrameLimiterEnabled = _settings.IsFrameLimiterEnabled;
            _tempSettings.TargetFramerate = _settings.TargetFramerate;
            _tempSettings.SmallerUi = _settings.SmallerUi;
            _tempSettings.UseImperialUnits = _settings.UseImperialUnits;
            _tempSettings.Use24HourClock = _settings.Use24HourClock;
            foreach (var item in _uiElements.OfType<ISettingControl>()) item.RefreshValue();
            UpdateFramerateControl();
        }

        private void ConfirmResetSettings()
        {
            _confirmationDialog.Show("Reset all settings to default? This cannot be undone.", new List<Tuple<string, Action>> { Tuple.Create("YES", new Action(() => { ExecuteResetSettings(); _confirmationDialog.Hide(); })), Tuple.Create("[gray]NO", new Action(() => _confirmationDialog.Hide())) });
        }

        private void ExecuteResetSettings()
        {
            _tempSettings = new GameSettings();
            _tempSettings.Resolution = SettingsManager.FindClosestResolution(_tempSettings.Resolution);
            foreach (var item in _uiElements.OfType<ISettingControl>()) item.RefreshValue();
            UpdateFramerateControl();
            ExecuteApplySettings();
            _confirmationMessage = "Settings Reset to Default!";
            _confirmationTimer = 5f;
        }

        private void AttemptToGoBack()
        {
            if (IsDirty())
            {
                _confirmationDialog.Show("You have unsaved changes.", new List<Tuple<string, Action>> { Tuple.Create("APPLY", new Action(() => { ExecuteApplySettings(); _sceneManager.ChangeScene(ReturnScene); })), Tuple.Create("DISCARD", new Action(() => { RevertChanges(); _sceneManager.ChangeScene(ReturnScene); })), Tuple.Create("[gray]CANCEL", new Action(() => _confirmationDialog.Hide())) });
            }
            else
            {
                _sceneManager.ChangeScene(ReturnScene);
            }
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
                    Point mousePos = (item is ISettingControl) ? new Point((int)currentPos.X + 230, (int)currentPos.Y + 10) : new Point(Global.VIRTUAL_WIDTH / 2, (int)currentPos.Y + 10);
                    Point screenPos = Core.TransformVirtualToScreen(mousePos);
                    Mouse.SetPosition(screenPos.X, screenPos.Y);
                    break;
                }
                if (item is ISettingControl) currentPos.Y += 20;
                else if (item is Button) currentPos.Y += 25;
                else if (item is string) currentPos.Y += 25;
            }
        }

        public override void Update(GameTime gameTime)
        {
            _titleBobTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (IsInputBlocked)
            {
                base.Update(gameTime);
                return;
            }

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Update(gameTime);
                base.Update(gameTime);
                return;
            }

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();
            var font = ServiceLocator.Get<BitmapFont>();
            _hoveredIndex = -1;

            if (currentMouseState.Position != previousMouseState.Position || (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released))
            {
                _sceneManager.LastInputDevice = InputDevice.Mouse;
            }

            if (_sceneManager.LastInputDevice == InputDevice.Mouse) _selectedIndex = -1;

            if (_currentInputDelay > 0) _currentInputDelay -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_confirmationTimer > 0) _confirmationTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

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
                    var hoverRect = new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, 460, 20);
                    if (hoverRect.Contains(virtualMousePos)) { _selectedIndex = i; _hoveredIndex = i; }
                    if (_currentInputDelay <= 0) setting.Update(new Vector2(currentPos.X, currentPos.Y + 5), isSelected, currentMouseState, previousMouseState, virtualMousePos, font);
                    currentPos.Y += 20;
                }
                else if (item is Button button)
                {
                    if (button.Bounds.Contains(virtualMousePos)) { _selectedIndex = i; _hoveredIndex = i; }
                    if (_currentInputDelay <= 0) button.Update(currentMouseState);
                    currentPos.Y += 25;
                }
                else if (item is string)
                {
                    currentPos.Y += 25;
                }
            }

            if (_currentInputDelay <= 0) HandleKeyboardInput(currentKeyboardState);
            var applyButton = _uiElements.OfType<Button>().FirstOrDefault(b => b.Text == "Apply");
            if (applyButton != null) applyButton.IsEnabled = IsDirty();
            if (_currentInputDelay <= 0 && KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState)) AttemptToGoBack();

            base.Update(gameTime);
        }

        private void HandleKeyboardInput(KeyboardState currentKeyboardState)
        {
            bool selectionChanged = false;
            if (KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState)) { _sceneManager.LastInputDevice = InputDevice.Keyboard; _selectedIndex = FindNextSelectable(_selectedIndex, 1); selectionChanged = true; }
            if (KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState)) { _sceneManager.LastInputDevice = InputDevice.Keyboard; _selectedIndex = FindNextSelectable(_selectedIndex, -1); selectionChanged = true; }

            if (selectionChanged) { MoveMouseToSelected(); _core.IsMouseVisible = false; keyboardNavigatedLastFrame = true; }

            if (_selectedIndex >= 0 && _selectedIndex < _uiElements.Count)
            {
                var selectedItem = _uiElements[_selectedIndex];
                if (selectedItem is ISettingControl setting)
                {
                    if (KeyPressed(Keys.Left, currentKeyboardState, _previousKeyboardState)) { _sceneManager.LastInputDevice = InputDevice.Keyboard; setting.HandleInput(Keys.Left); }
                    if (KeyPressed(Keys.Right, currentKeyboardState, _previousKeyboardState)) { _sceneManager.LastInputDevice = InputDevice.Keyboard; setting.HandleInput(Keys.Right); }
                }
                else if (selectedItem is Button button && KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
                {
                    _sceneManager.LastInputDevice = InputDevice.Keyboard;
                    button.TriggerClick();
                }
            }
        }

        private int FindNextSelectable(int currentIndex, int direction)
        {
            int searchIndex = currentIndex;
            for (int i = 0; i < _uiElements.Count; i++)
            {
                searchIndex = (searchIndex + direction + _uiElements.Count) % _uiElements.Count;
                var item = _uiElements[searchIndex];
                if (item is ISettingControl || (item is Button button && button.IsEnabled)) return searchIndex;
            }
            return currentIndex;
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            var virtualMousePos = Core.TransformMouse(Mouse.GetState().Position);
            var pixel = ServiceLocator.Get<Texture2D>();

            string title = "Settings";
            Vector2 titleSize = font.MeasureString(title) * 2f;
            float yOffset = (float)Math.Sin(_titleBobTimer * TitleBobSpeed) * TitleBobAmount;
            Vector2 titlePosition = new Vector2(screenWidth / 2 - titleSize.X / 2, 75 + yOffset);
            spriteBatch.DrawString(font, title, titlePosition, _global.Palette_BrightWhite, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);

            Vector2 currentPos = new Vector2(0, _settingsStartY);
            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                bool isSelected = (i == _selectedIndex);
                currentPos.X = (screenWidth - 450) / 2;

                if (isSelected)
                {
                    bool isHovered = (item is ISettingControl s && new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, 460, 20).Contains(virtualMousePos)) || (item is Button b && b.IsHovered);
                    if (isHovered || keyboardNavigatedLastFrame)
                    {
                        float itemHeight = (item is ISettingControl) ? 20 : (item is Button) ? 20 : 0;
                        if (itemHeight > 0) DrawRectangleBorder(spriteBatch, pixel, new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, 460, (int)itemHeight), 1, _global.ButtonHoverColor);
                    }
                }

                if (item is ISettingControl setting)
                {
                    setting.Draw(spriteBatch, font, new Vector2(currentPos.X, currentPos.Y + 5), isSelected, gameTime);
                    if (setting.Label == "Resolution")
                    {
                        string extraText = "";
                        var currentResPoint = _tempSettings.Resolution;
                        var standardEntry = SettingsManager.GetResolutions().FirstOrDefault(r => r.Value == currentResPoint);

                        if (standardEntry.Key != null) // It's a standard resolution
                        {
                            int aspectIndex = standardEntry.Key.IndexOf(" (");
                            if (aspectIndex != -1)
                            {
                                extraText = standardEntry.Key.Substring(aspectIndex).Trim();
                            }
                        }
                        else // It's a custom resolution
                        {
                            extraText = $"({currentResPoint.X}x{currentResPoint.Y})";
                        }

                        if (!string.IsNullOrEmpty(extraText))
                        {
                            spriteBatch.DrawString(font, extraText, new Vector2(currentPos.X + 460, currentPos.Y + 5), _global.Palette_DarkGray);
                        }
                    }
                    currentPos.Y += 20;
                }
                else if (item is Button button)
                {
                    button.Draw(spriteBatch, font, gameTime, isSelected);
                    currentPos.Y += 25;
                }
                else if (item is string header)
                {
                    currentPos.Y += 5;
                    spriteBatch.DrawString(font, header, new Vector2(screenWidth / 2 - font.MeasureString(header).Width / 2, currentPos.Y), _global.Palette_LightGray);
                    spriteBatch.Draw(pixel, new Rectangle(screenWidth / 2 - 180, (int)currentPos.Y + 10, 360, 1), _global.Palette_Gray);
                    currentPos.Y += 20;
                }
            }

            if (_confirmationTimer > 0)
            {
                Vector2 msgSize = font.MeasureString(_confirmationMessage);
                spriteBatch.DrawString(font, _confirmationMessage, new Vector2(screenWidth / 2 - msgSize.X / 2, Global.VIRTUAL_HEIGHT - 50), _global.Palette_Teal);
            }

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.DrawContent(spriteBatch, font, gameTime);
            }
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.DrawOverlay(spriteBatch);
            }
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
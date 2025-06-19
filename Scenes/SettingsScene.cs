﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
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
        private int _hoveredIndex = -1;
        private string _confirmationMessage = "";
        private float _confirmationTimer = 0f;
        private int _settingsStartY = 105;

        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState; 

        private GameSettings _tempSettings;
        private ConfirmationDialog _confirmationDialog;

        public override void Enter()
        {
            base.Enter();
            _confirmationDialog = new ConfirmationDialog(this);
            _tempSettings = new GameSettings
            {
                Resolution = Core.Settings.Resolution,
                IsFullscreen = Core.Settings.IsFullscreen,
                IsVsync = Core.Settings.IsVsync,
                IsFrameLimiterEnabled = Core.Settings.IsFrameLimiterEnabled,
                TargetFramerate = Core.Settings.TargetFramerate,
                SmallerUi = Core.Settings.SmallerUi,
                UseImperialUnits = Core.Settings.UseImperialUnits,
                Use24HourClock = Core.Settings.Use24HourClock
            };

            var resolutions = SettingsManager.GetResolutions();
            _tempSettings.Resolution = SettingsManager.FindClosestResolution(_tempSettings.Resolution);

            BuildInitialUI();
            _selectedIndex = FindNextSelectable(-1, 1);
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();

            _currentInputDelay = _inputDelay;

            PositionMouseOnFirstSelectable();
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
                    currentPos.Y += 25;
                }
            }

            return null; // No selectable elements found
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
                return new KeyValuePair<string, Point>(display, kvp.Value);
            }).ToList();
            _uiElements.Add(new OptionSettingControl<Point>("Resolution", resolutionDisplayList, () => _tempSettings.Resolution, v => _tempSettings.Resolution = v));
            _uiElements.Add(new BoolSettingControl("Fullscreen", () => _tempSettings.IsFullscreen, v => { _tempSettings.IsFullscreen = v; if (v) SetResolutionToNearestNative(); }));
            _uiElements.Add(new BoolSettingControl("Smaller UI", () => _tempSettings.SmallerUi, v => _tempSettings.SmallerUi = v));
            _uiElements.Add(new BoolSettingControl("VSync", () => _tempSettings.IsVsync, v => _tempSettings.IsVsync = v));
            _uiElements.Add(new BoolSettingControl("Frame Limiter", () => _tempSettings.IsFrameLimiterEnabled, v => _tempSettings.IsFrameLimiterEnabled = v));

            _uiElements.Add("Game");
            _uiElements.Add(new BoolSettingControl("24-Hour Clock", () => _tempSettings.Use24HourClock, v => _tempSettings.Use24HourClock = v));
            _uiElements.Add(new BoolSettingControl("Imperial Units", () => _tempSettings.UseImperialUnits, v => _tempSettings.UseImperialUnits = v));

            _uiElements.Add("Controls");

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

        /// <summary>
        /// Calculates and sets the correct positions for all UI elements, specifically Buttons.
        /// </summary>
        private void LayoutUI()
        {
            Vector2 currentPos = new Vector2(0, _settingsStartY);
            int screenWidth = Global.VIRTUAL_WIDTH;

            foreach (var item in _uiElements)
            {
                if (item is Button button)
                {
                    button.Bounds = new Rectangle(
                        (screenWidth - button.Bounds.Width) / 2,
                        (int)currentPos.Y,
                        button.Bounds.Width,
                        button.Bounds.Height
                    );
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
                    currentPos.Y += 25;
                }
            }
        }

        private void SetResolutionToNearestNative()
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
                var framerates = new List<KeyValuePair<string, int>>
                {
                    new("30 FPS", 30), new("60 FPS", 60), new("75 FPS", 75),
                    new("120 FPS", 120), new("144 FPS", 144), new("240 FPS", 240)
                };
                var newControl = new OptionSettingControl<int>("Target Framerate", framerates, () => _tempSettings.TargetFramerate, v => _tempSettings.TargetFramerate = v);
                _uiElements.Insert(limiterIndex + 1, newControl);
                changed = true;
            }
            else if (!_tempSettings.IsFrameLimiterEnabled && framerateControl != null)
            {
                _uiElements.Remove(framerateControl);
                changed = true;
            }

            if (changed)
            {
                LayoutUI();
            }
        }

        private void ConfirmApplySettings()
        {
            if (!IsDirty()) return;

            var details = _uiElements.OfType<ISettingControl>()
                .Where(c => c.IsDirty)
                .Select(c => $"{c.Label}: {c.GetSavedValueAsString()} -> {c.GetCurrentValueAsString()}")
                .ToList();

            _confirmationDialog.Show(
                "Apply the following changes?",
                new List<Tuple<string, Action>>
                {
                    Tuple.Create("[gray]NO", new Action(() => _confirmationDialog.Hide())),
                    Tuple.Create("YES", new Action(() => { ExecuteApplySettings(); _confirmationDialog.Hide(); }))
                },
                details
            );
        }

        private void ExecuteApplySettings()
        {
            Core.Settings.Resolution = _tempSettings.Resolution;
            Core.Settings.IsFullscreen = _tempSettings.IsFullscreen;
            Core.Settings.IsVsync = _tempSettings.IsVsync;
            Core.Settings.IsFrameLimiterEnabled = _tempSettings.IsFrameLimiterEnabled;
            Core.Settings.TargetFramerate = _tempSettings.TargetFramerate;
            Core.Settings.SmallerUi = _tempSettings.SmallerUi;
            Core.Settings.UseImperialUnits = _tempSettings.UseImperialUnits;
            Core.Settings.Use24HourClock = _tempSettings.Use24HourClock;

            Core.Settings.ApplyGraphicsSettings(Global.Instance.CurrentGraphics, Core.Instance);
            Core.Settings.ApplyGameSettings();
            SettingsManager.SaveSettings(Core.Settings);

            foreach (var item in _uiElements.OfType<ISettingControl>()) item.Apply();

            _confirmationMessage = "Settings Applied!";
            _confirmationTimer = 5f;
            MoveMouseToSelected();
        }

        private void ConfirmResetSettings()
        {
            _confirmationDialog.Show(
                "Reset all settings to default? This cannot be undone.",
                new List<Tuple<string, Action>>
                {
                    Tuple.Create("YES", new Action(() => { ExecuteResetSettings(); _confirmationDialog.Hide(); })),
                    Tuple.Create("NO", new Action(() => _confirmationDialog.Hide()))
                }
            );
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

        private void RevertChanges()
        {
            foreach (var item in _uiElements.OfType<ISettingControl>()) item.Revert();
            _tempSettings.Resolution = Core.Settings.Resolution;
            _tempSettings.IsFullscreen = Core.Settings.IsFullscreen;
            _tempSettings.IsVsync = Core.Settings.IsVsync;
            _tempSettings.IsFrameLimiterEnabled = Core.Settings.IsFrameLimiterEnabled;
            _tempSettings.TargetFramerate = Core.Settings.TargetFramerate;
            _tempSettings.SmallerUi = Core.Settings.SmallerUi;
            _tempSettings.UseImperialUnits = Core.Settings.UseImperialUnits;
            _tempSettings.Use24HourClock = Core.Settings.Use24HourClock;
            foreach (var item in _uiElements.OfType<ISettingControl>()) item.RefreshValue();
            UpdateFramerateControl();
        }

        private void AttemptToGoBack()
        {
            if (IsDirty())
            {
                _confirmationDialog.Show(
                    "You have unsaved changes.",
                    new List<Tuple<string, Action>>
                    {
                        Tuple.Create("[gray]CANCEL", new Action(() => _confirmationDialog.Hide())),
                        Tuple.Create("APPLY", new Action(() => { ExecuteApplySettings(); Core.CurrentSceneManager.ChangeScene(GameSceneState.MainMenu); })),
                        Tuple.Create("DISCARD", new Action(() => { RevertChanges(); Core.CurrentSceneManager.ChangeScene(GameSceneState.MainMenu); }))
                    }
                );
            }
            else
            {
                Core.CurrentSceneManager.ChangeScene(GameSceneState.MainMenu);
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
                    Point mousePos = (item is ISettingControl)
                        ? new Point((int)currentPos.X + 230, (int)currentPos.Y + 10)
                        : new Point(Global.VIRTUAL_WIDTH / 2, (int)currentPos.Y + 10);
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
            base.Update(gameTime);

            if (_confirmationDialog.IsActive)
            {
                if (IsInputBlocked) { return; }
                _confirmationDialog.Update(gameTime);
                return;
            }

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();
            _hoveredIndex = -1;

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
                    if (_currentInputDelay <= 0) setting.Update(new Vector2(currentPos.X, currentPos.Y + 5), isSelected, currentMouseState, _previousMouseState);
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

            if (IsInputBlocked) { return; }

            if (_currentInputDelay <= 0) HandleKeyboardInput(currentKeyboardState);
            var applyButton = _uiElements.OfType<Button>().FirstOrDefault(b => b.Text == "Apply");
            if (applyButton != null) applyButton.IsEnabled = IsDirty();
            if (_currentInputDelay <= 0 && KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState)) AttemptToGoBack();

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;
        }

        private void HandleKeyboardInput(KeyboardState currentKeyboardState)
        {
            bool selectionChanged = false;
            if (KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState)) { _selectedIndex = FindNextSelectable(_selectedIndex, 1); selectionChanged = true; }
            if (KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState)) { _selectedIndex = FindNextSelectable(_selectedIndex, -1); selectionChanged = true; }

            if (selectionChanged) { MoveMouseToSelected(); Core.Instance.IsMouseVisible = false; keyboardNavigatedLastFrame = true; }

            if (_selectedIndex >= 0 && _selectedIndex < _uiElements.Count)
            {
                var selectedItem = _uiElements[_selectedIndex];
                if (selectedItem is ISettingControl setting)
                {
                    if (KeyPressed(Keys.Left, currentKeyboardState, _previousKeyboardState)) setting.HandleInput(Keys.Left);
                    if (KeyPressed(Keys.Right, currentKeyboardState, _previousKeyboardState)) setting.HandleInput(Keys.Right);
                }
                else if (selectedItem is Button button && KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
                {
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
                    bool isHovered = (item is ISettingControl s && new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, 460, 20).Contains(virtualMousePos))
                                  || (item is Button b && b.IsHovered);
                    if (isHovered || keyboardNavigatedLastFrame)
                    {
                        float itemHeight = (item is ISettingControl) ? 20 : (item is Button) ? 20 : 0;
                        if (itemHeight > 0) DrawRectangleBorder(spriteBatch, Core.Pixel, new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, 460, (int)itemHeight), 1, Global.Instance.OptionHoverColor);
                    }
                }

                if (item is ISettingControl setting)
                {
                    setting.Draw(spriteBatch, new Vector2(currentPos.X, currentPos.Y + 5), isSelected);
                    if (setting.Label == "Resolution")
                    {
                        var originalEntry = SettingsManager.GetResolutions().FirstOrDefault(r => r.Value == _tempSettings.Resolution);
                        if (originalEntry.Key != null && originalEntry.Key.Contains(" ("))
                        {
                            string aspectRatio = originalEntry.Key.Substring(originalEntry.Key.IndexOf(" (")).Trim();
                            spriteBatch.DrawString(font, aspectRatio, new Vector2(currentPos.X + 460, currentPos.Y + 5), Global.Instance.Palette_DarkGray);
                        }
                    }
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
                    spriteBatch.Draw(Core.Pixel, new Rectangle(screenWidth / 2 - 180, (int)currentPos.Y + 10, 360, 1), Global.Instance.Palette_Gray);
                    currentPos.Y += 20;
                }
            }

            if (_confirmationTimer > 0)
            {
                Vector2 msgSize = font.MeasureString(_confirmationMessage);
                spriteBatch.DrawString(font, _confirmationMessage, new Vector2(screenWidth / 2 - msgSize.X / 2, Global.VIRTUAL_HEIGHT - 50), Global.Instance.Palette_Teal);
            }
            spriteBatch.End();

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Draw(gameTime);
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
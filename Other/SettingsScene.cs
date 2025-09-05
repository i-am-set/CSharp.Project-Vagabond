﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
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
        private List<Vector2> _uiElementPositions = new(); // To store calculated positions
        private int _selectedIndex = -1;
        private string _confirmationMessage = "";
        private float _confirmationTimer = 0f;

        // --- Layout Tuning ---
        private const int SETTINGS_START_Y = 35;
        private const int ITEM_VERTICAL_SPACING = 15;
        private const int BUTTON_VERTICAL_SPACING = 14;
        private const int SETTINGS_PANEL_WIDTH = 280;
        private const int SETTINGS_PANEL_X = (Global.VIRTUAL_WIDTH - SETTINGS_PANEL_WIDTH) / 2;


        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private float _titleBobTimer = 0f;
        private const float TitleBobAmount = 1.5f;
        private const float TitleBobSpeed = 2f;

        private GameSettings _tempSettings;
        private ConfirmationDialog _confirmationDialog;
        private RevertDialog _revertDialog;

        private bool _isApplyingSettings = false;

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
            _isApplyingSettings = false; // Ensure the state flag is reset on scene entry.
            _confirmationDialog = new ConfirmationDialog(this);
            _revertDialog = new RevertDialog(this);

            RefreshUIFromSettings();

            // Reset animation states after they are built
            foreach (var item in _uiElements)
            {
                if (item is ISettingControl setting)
                {
                    setting.ResetAnimationState();
                }
                else if (item is Button button)
                {
                    button.ResetAnimationState();
                }
            }

            if (this.LastUsedInputForNav == InputDevice.Keyboard)
            {
                _selectedIndex = FindNextSelectable(-1, 1);
                PositionMouseOnFirstSelectable();
            }
            else
            {
                _selectedIndex = -1;
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
            // Block UI refresh if we are in the middle of applying settings to prevent state corruption.
            if (_isApplyingSettings) return;

            _tempSettings = new GameSettings
            {
                Resolution = _settings.Resolution,
                Mode = _settings.Mode,
                IsVsync = _settings.IsVsync,
                IsFrameLimiterEnabled = _settings.IsFrameLimiterEnabled,
                TargetFramerate = _settings.TargetFramerate,
                SmallerUi = _settings.SmallerUi,
                UseImperialUnits = _settings.UseImperialUnits,
                Use24HourClock = _settings.Use24HourClock,
                DisplayIndex = _settings.DisplayIndex,
                Gamma = _settings.Gamma
            };
            _titleBobTimer = 0f;
            BuildInitialUI();
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
            if (_uiElements.Count > 0 && _uiElementPositions.Count > 0)
            {
                var firstPos = _uiElementPositions[0];
                return new Rectangle((int)firstPos.X - 5, (int)firstPos.Y, SETTINGS_PANEL_WIDTH + 10, ITEM_VERTICAL_SPACING);
            }
            return null;
        }

        private void BuildInitialUI()
        {
            _uiElements.Clear();

            var resolutions = SettingsManager.GetResolutions();
            var resolutionDisplayList = resolutions.Select(kvp =>
            {
                string display = kvp.Key;
                int aspectIndex = display.IndexOf(" (");
                if (aspectIndex != -1) display = display.Substring(0, aspectIndex);
                return new KeyValuePair<string, Point>(display.Trim(), kvp.Value);
            }).ToList();

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

            var nativeDisplayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            resolutionControl.IsOptionNotRecommended = (res) => res.X > nativeDisplayMode.Width || res.Y > nativeDisplayMode.Height;

            resolutionControl.GetValueColor = (pointValue) =>
            {
                bool isStandard = SettingsManager.GetResolutions().Any(r => r.Value == pointValue);
                return isStandard ? (Color?)null : _global.Palette_LightYellow;
            };
            resolutionControl.ExtraInfoTextGetter = () =>
            {
                var currentResPoint = _tempSettings.Resolution;
                var standardEntry = SettingsManager.GetResolutions().FirstOrDefault(r => r.Value == currentResPoint);

                if (standardEntry.Key != null) // It's a standard resolution
                {
                    int aspectIndex = standardEntry.Key.IndexOf(" (");
                    if (aspectIndex != -1)
                    {
                        return standardEntry.Key.Substring(aspectIndex).Trim();
                    }
                }
                // It's a custom resolution
                return $"({currentResPoint.X}x{currentResPoint.Y})";
            };
            _uiElements.Add(resolutionControl);

            var windowModeControl = new OptionSettingControl<WindowMode>("Window Mode", windowModes, () => _tempSettings.Mode, v => {
                _tempSettings.Mode = v;
                if (v == WindowMode.Borderless) SetResolutionToNative();
            });
            _uiElements.Add(windowModeControl);

            _uiElements.Add(new SegmentedBarSettingControl("Gamma", 1.0f, 2.0f, 11, () => _tempSettings.Gamma, v => _tempSettings.Gamma = v));
            _uiElements.Add(new BoolSettingControl("Smaller UI", () => _tempSettings.SmallerUi, v => _tempSettings.SmallerUi = v));
            _uiElements.Add(new BoolSettingControl("VSync", () => _tempSettings.IsVsync, v => _tempSettings.IsVsync = v));
            _uiElements.Add(new BoolSettingControl("Frame Limiter", () => _tempSettings.IsFrameLimiterEnabled, v => _tempSettings.IsFrameLimiterEnabled = v));

            var framerates = new List<KeyValuePair<string, int>> { new("30 FPS", 30), new("60 FPS", 60), new("75 FPS", 75), new("120 FPS", 120), new("144 FPS", 144), new("240 FPS", 240) };
            _uiElements.Add(new OptionSettingControl<int>("Target Framerate", framerates, () => _tempSettings.TargetFramerate, v => _tempSettings.TargetFramerate = v));

            var applyButton = new Button(new Rectangle(0, 0, 125, 10), "Apply");
            applyButton.OnClick += ApplySettings;
            _uiElements.Add(applyButton);

            var backButton = new Button(new Rectangle(0, 0, 125, 10), "Back");
            backButton.OnClick += AttemptToGoBack;
            _uiElements.Add(backButton);

            var resetButton = new Button(new Rectangle(0, 0, 125, 10), "Restore Defaults")
            {
                CustomDefaultTextColor = _global.Palette_LightYellow
            };
            resetButton.OnClick += ConfirmResetSettings;
            _uiElements.Add(resetButton);

            CalculateLayoutPositions(); // Ensure positions are calculated after building the list.
            applyButton.IsEnabled = IsDirty();
        }

        private void CalculateLayoutPositions()
        {
            _uiElementPositions.Clear();
            Vector2 currentPos = new Vector2(SETTINGS_PANEL_X, SETTINGS_START_Y);

            foreach (var item in _uiElements)
            {
                _uiElementPositions.Add(currentPos);

                if (item is Button button)
                {
                    button.Bounds = new Rectangle((Global.VIRTUAL_WIDTH - button.Bounds.Width) / 2, (int)currentPos.Y, button.Bounds.Width, button.Bounds.Height);
                    currentPos.Y += BUTTON_VERTICAL_SPACING;
                }
                else if (item is ISettingControl)
                {
                    currentPos.Y += ITEM_VERTICAL_SPACING;
                }
            }
        }

        private void SetResolutionToNative()
        {
            var nativeResolution = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            var nativePoint = new Point(nativeResolution.Width, nativeResolution.Height);
            _tempSettings.Resolution = SettingsManager.FindClosestResolution(nativePoint);
            _uiElements.OfType<ISettingControl>().FirstOrDefault(c => c.Label == "Resolution")?.RefreshValue();
        }

        private void ApplySettings()
        {
            if (!IsDirty()) return;

            ResetInputBlockTimer();
            _isApplyingSettings = true; // Set flag to prevent UI refresh during apply

            bool graphicsChanged = _tempSettings.Resolution != _settings.Resolution || _tempSettings.Mode != _settings.Mode;

            if (graphicsChanged)
            {
                var revertState = new GameSettings
                {
                    Resolution = _settings.Resolution,
                    Mode = _settings.Mode
                };

                _tempSettings.ApplyGraphicsSettings(_graphics, _core);

                _revertDialog.Show(
                    "Keep these display settings?",
                    onConfirm: () => {
                        FinalizeAndSaveAllSettings();
                    },
                    onRevert: () => {
                        revertState.ApplyGraphicsSettings(_graphics, _core);
                        RevertChanges();
                    },
                    countdownDuration: 10f
                );
            }
            else
            {
                FinalizeAndSaveAllSettings();
            }
        }

        private void FinalizeAndSaveAllSettings()
        {
            _settings.Resolution = _tempSettings.Resolution;
            _settings.Mode = _tempSettings.Mode;
            _settings.IsVsync = _tempSettings.IsVsync;
            _settings.IsFrameLimiterEnabled = _tempSettings.IsFrameLimiterEnabled;
            _settings.TargetFramerate = _tempSettings.TargetFramerate;
            _settings.SmallerUi = _tempSettings.SmallerUi;
            _settings.UseImperialUnits = _tempSettings.UseImperialUnits;
            _settings.Use24HourClock = _tempSettings.Use24HourClock;
            _settings.DisplayIndex = _tempSettings.DisplayIndex;
            _settings.Gamma = _tempSettings.Gamma;

            _settings.ApplyGraphicsSettings(_graphics, _core);
            _settings.ApplyGameSettings();
            SettingsManager.SaveSettings(_settings);

            foreach (var item in _uiElements.OfType<ISettingControl>()) item.Apply();
            _confirmationMessage = "Settings Applied!";
            _confirmationTimer = 5f;
            _isApplyingSettings = false; // Unset flag
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
            _tempSettings.DisplayIndex = _settings.DisplayIndex;
            _tempSettings.Gamma = _settings.Gamma;
            foreach (var item in _uiElements.OfType<ISettingControl>()) item.RefreshValue();
            _isApplyingSettings = false; // Unset flag
        }

        private void ConfirmResetSettings()
        {
            _confirmationDialog.Show("Reset all settings to default?\n\nThis cannot be undone.", new List<Tuple<string, Action>> { Tuple.Create("YES", new Action(() => { ExecuteResetSettings(); _confirmationDialog.Hide(); })), Tuple.Create("[gray]NO", new Action(() => _confirmationDialog.Hide())) });
        }

        private void ExecuteResetSettings()
        {
            // Preserve the current display settings
            var preservedResolution = _tempSettings.Resolution;
            var preservedWindowMode = _tempSettings.Mode;

            // Create a new GameSettings instance to get all other default values
            _tempSettings = new GameSettings();

            // Restore the preserved display settings
            _tempSettings.Resolution = preservedResolution;
            _tempSettings.Mode = preservedWindowMode;

            // Refresh the UI to show the new default values for all controls
            foreach (var item in _uiElements.OfType<ISettingControl>())
            {
                item.RefreshValue();
            }

            ApplySettings(); // This will apply and save the new settings

            _confirmationMessage = "Settings Reset to Default!";
            _confirmationTimer = 5f;
        }

        private void AttemptToGoBack()
        {
            if (IsDirty())
            {
                _confirmationDialog.Show("You have unsaved changes.", new List<Tuple<string, Action>> { Tuple.Create("APPLY", new Action(() => { ApplySettings(); _sceneManager.ChangeScene(ReturnScene); })), Tuple.Create("DISCARD", new Action(() => { RevertChanges(); _sceneManager.ChangeScene(ReturnScene); })), Tuple.Create("[gray]CANCEL", new Action(() => _confirmationDialog.Hide())) });
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

            if (_uiElementPositions.Count > _selectedIndex)
            {
                var item = _uiElements[_selectedIndex];
                var itemPos = _uiElementPositions[_selectedIndex];
                Point mousePos = (item is ISettingControl) ? new Point((int)itemPos.X + 115, (int)itemPos.Y + 5) : new Point(Global.VIRTUAL_WIDTH / 2, (int)itemPos.Y + 5);
                Point screenPos = Core.TransformVirtualToScreen(mousePos);
                Mouse.SetPosition(screenPos.X, screenPos.Y);
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

            if (_revertDialog.IsActive)
            {
                _revertDialog.Update(gameTime);
                base.Update(gameTime);
                return;
            }

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();
            var font = ServiceLocator.Get<BitmapFont>();

            if (currentMouseState.Position != previousMouseState.Position || (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released))
            {
                _sceneManager.LastInputDevice = InputDevice.Mouse;
            }

            if (_sceneManager.LastInputDevice == InputDevice.Mouse) _selectedIndex = -1;

            if (_currentInputDelay > 0) _currentInputDelay -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_confirmationTimer > 0) _confirmationTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update control enabled states based on dependencies
            var framerateControl = _uiElements.OfType<ISettingControl>().FirstOrDefault(c => c.Label == "Target Framerate");
            if (framerateControl != null)
            {
                framerateControl.IsEnabled = _tempSettings.IsFrameLimiterEnabled;
            }

            CalculateLayoutPositions(); // Recalculate positions every frame

            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                var currentPos = _uiElementPositions[i];

                if (item is ISettingControl setting)
                {
                    var hoverRect = new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, SETTINGS_PANEL_WIDTH + 10, ITEM_VERTICAL_SPACING);
                    if (hoverRect.Contains(virtualMousePos)) { _selectedIndex = i; }
                    if (_currentInputDelay <= 0) setting.Update(new Vector2(currentPos.X, currentPos.Y + 2), i == _selectedIndex, currentMouseState, previousMouseState, virtualMousePos, font);
                }
                else if (item is Button button)
                {
                    if (button.Bounds.Contains(virtualMousePos)) { _selectedIndex = i; }
                    if (_currentInputDelay <= 0) button.Update(currentMouseState);
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

            if (selectionChanged)
            {
                _core.IsMouseVisible = false;
                keyboardNavigatedLastFrame = true;
            }

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
                if (item is ISettingControl setting && setting.IsEnabled) return searchIndex;
                if (item is Button button && button.IsEnabled) return searchIndex;
            }
            return currentIndex;
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            var virtualMousePos = Core.TransformMouse(Mouse.GetState().Position);
            var pixel = ServiceLocator.Get<Texture2D>();

            string title = "Settings";
            Vector2 titleSize = font.MeasureString(title);
            float yOffset = (float)Math.Sin(_titleBobTimer * TitleBobSpeed) * TitleBobAmount;
            float titleBaseY = 15f;
            Vector2 titlePosition = new Vector2(screenWidth / 2 - titleSize.X / 2, titleBaseY + yOffset);
            spriteBatch.DrawString(font, title, titlePosition, _global.Palette_BrightWhite, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            // Draw divider line
            int dividerY = (int)(titleBaseY + titleSize.Y + 5);
            spriteBatch.Draw(pixel, new Rectangle(screenWidth / 2 - 90, dividerY, 180, 1), _global.Palette_Gray);


            if (_confirmationTimer > 0)
            {
                Vector2 msgSize = font.MeasureString(_confirmationMessage);
                Vector2 messagePosition = new Vector2(screenWidth / 2 - msgSize.X / 2, 5);
                spriteBatch.DrawString(font, _confirmationMessage, messagePosition, _global.Palette_Teal);
            }

            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                var currentPos = _uiElementPositions[i];
                bool isSelected = (i == _selectedIndex);

                if (isSelected)
                {
                    bool isHovered = (item is ISettingControl s && new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, SETTINGS_PANEL_WIDTH + 10, ITEM_VERTICAL_SPACING).Contains(virtualMousePos)) || (item is Button b && b.IsHovered);
                    if (isHovered || keyboardNavigatedLastFrame)
                    {
                        float itemHeight = (item is ISettingControl) ? ITEM_VERTICAL_SPACING : (item is Button) ? BUTTON_VERTICAL_SPACING : 0;
                        if (itemHeight > 0) DrawRectangleBorder(spriteBatch, pixel, new Rectangle((int)currentPos.X - 5, (int)currentPos.Y - 2, SETTINGS_PANEL_WIDTH + 10, (int)itemHeight), 1, _global.ButtonHoverColor);
                    }
                }

                if (item is ISettingControl setting)
                {
                    setting.Draw(spriteBatch, font, new Vector2(currentPos.X, currentPos.Y + 2), isSelected, gameTime);
                }
                else if (item is Button button)
                {
                    button.Draw(spriteBatch, font, gameTime, transform, isSelected);
                }
            }

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.DrawContent(spriteBatch, font, gameTime, transform);
            }

            if (_revertDialog.IsActive)
            {
                _revertDialog.DrawContent(spriteBatch, font, gameTime, transform);
            }
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_confirmationDialog.IsActive || _revertDialog.IsActive)
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
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
        private readonly HapticsManager _hapticsManager;
        private List<object> _uiElements = new();
        private List<Vector2> _uiElementPositions = new(); // To store calculated positions
        private int _selectedIndex = -1;
        private string _confirmationMessage = "";
        private float _confirmationTimer = 0f;

        // --- Layout Tuning ---
        private const int SETTINGS_START_Y = 30; // Moved up 5px (was 35)
        private const int ITEM_VERTICAL_SPACING = 12; // Standardized to 12px
        private const int BUTTON_VERTICAL_SPACING = 12; // Standardized to 12px
        private const int SETTINGS_PANEL_WIDTH = 280;
        private const int SETTINGS_PANEL_X = (Global.VIRTUAL_WIDTH - SETTINGS_PANEL_WIDTH) / 2;


        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private float _titleBobTimer = 0f;

        private GameSettings _tempSettings;
        private ConfirmationDialog _confirmationDialog;
        private RevertDialog _revertDialog;

        private bool _isApplyingSettings = false;

        public SettingsScene()
        {
            _settings = ServiceLocator.Get<GameSettings>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _graphics = ServiceLocator.Get<GraphicsDeviceManager>();
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
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

            // Subscribe to resolution changes to update UI dynamically
            EventBus.Subscribe<GameEvents.UIThemeOrResolutionChanged>(OnResolutionChanged);

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

            if (this.LastInputDevice == InputDevice.Keyboard)
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

        public override void Exit()
        {
            base.Exit();
            EventBus.Unsubscribe<GameEvents.UIThemeOrResolutionChanged>(OnResolutionChanged);
        }

        private void OnResolutionChanged(GameEvents.UIThemeOrResolutionChanged e)
        {
            RefreshUIFromSettings();
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
                // Return the exact 12px high slot
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
                return isStandard ? (Color?)null : _global.Palette_DarkSun;
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

            var windowModeControl = new OptionSettingControl<WindowMode>("Window Mode", windowModes, () => _tempSettings.Mode, v =>
            {
                _tempSettings.Mode = v;
                if (v == WindowMode.Borderless) SetResolutionToNative();
            });
            _uiElements.Add(windowModeControl);

            _uiElements.Add(new SegmentedBarSettingControl("Gamma", 1.0f, 2.0f, 11, () => _tempSettings.Gamma, v => _tempSettings.Gamma = v));
            _uiElements.Add(new BoolSettingControl("Smaller UI", () => _tempSettings.SmallerUi, v => _tempSettings.SmallerUi = v));
            _uiElements.Add(new BoolSettingControl("VSync", () => _tempSettings.IsVsync, v => _tempSettings.IsVsync = v));
            _uiElements.Add(new BoolSettingControl("Frame Limiter", () => _tempSettings.IsFrameLimiterEnabled, v => _tempSettings.IsFrameLimiterEnabled = v));

            var framerates = new List<KeyValuePair<string, int>> { new("30 FPS", 30), new("60 FPS", 60), new("75 FPS", 75), new("120 FPS", 120), new("144 FPS", 144), new("240 FPS", 240) };
            var framerateControl = new OptionSettingControl<int>("Target Framerate", framerates, () => _tempSettings.TargetFramerate, v => _tempSettings.TargetFramerate = v);
            // Initialize the enabled state immediately to prevent visual popping during the input block period
            framerateControl.IsEnabled = _tempSettings.IsFrameLimiterEnabled;
            _uiElements.Add(framerateControl);

            // Buttons now 12px high
            var applyButton = new Button(new Rectangle(0, 0, 125, 12), "APPLY")
            {
                TextRenderOffset = new Vector2(0, 1)
            };
            applyButton.OnClick += () => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); ApplySettings(); };
            _uiElements.Add(applyButton);

            var backButton = new Button(new Rectangle(0, 0, 125, 12), "BACK")
            {
                TextRenderOffset = new Vector2(0, 1)
            };
            backButton.OnClick += () => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); AttemptToGoBack(); };
            _uiElements.Add(backButton);

            var resetButton = new Button(new Rectangle(0, 0, 125, 12), "RESTORE DEFAULTS")
            {
                CustomDefaultTextColor = _global.HighlightTextColor,
                TextRenderOffset = new Vector2(0, 1)
            };
            resetButton.OnClick += () => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); ConfirmResetSettings(); };
            _uiElements.Add(resetButton);

            CalculateLayoutPositions(); // Ensure positions are calculated after building the list.
            applyButton.IsEnabled = IsDirty();
        }

        private void CalculateLayoutPositions()
        {
            _uiElementPositions.Clear();
            Vector2 currentSettingPos = new Vector2(SETTINGS_PANEL_X, SETTINGS_START_Y);

            // Anchor buttons to bottom of screen
            // 3 buttons * 12px spacing = 36px height.
            // Add some margin from bottom (e.g. 8px).
            int bottomMargin = 8;
            int startButtonY = Global.VIRTUAL_HEIGHT - bottomMargin - (3 * BUTTON_VERTICAL_SPACING);
            Vector2 currentButtonPos = new Vector2(SETTINGS_PANEL_X, startButtonY);

            var font = ServiceLocator.Get<BitmapFont>();

            foreach (var item in _uiElements)
            {
                if (item is ISettingControl)
                {
                    _uiElementPositions.Add(currentSettingPos);
                    currentSettingPos.Y += ITEM_VERTICAL_SPACING;
                }
                else if (item is Button button)
                {
                    _uiElementPositions.Add(currentButtonPos);

                    // Measure text to determine width
                    var textSize = font.MeasureString(button.Text);
                    int width = (int)textSize.Width + 20;

                    // Center the button horizontally on the screen
                    int centeredX = (Global.VIRTUAL_WIDTH - width) / 2;

                    button.Bounds = new Rectangle(
                        centeredX,
                        (int)currentButtonPos.Y,
                        width,
                        BUTTON_VERTICAL_SPACING
                    );

                    currentButtonPos.Y += BUTTON_VERTICAL_SPACING;
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
                    onConfirm: () =>
                    {
                        _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                        FinalizeAndSaveAllSettings();
                    },
                    onRevert: () =>
                    {
                        _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
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
            _tempSettings.UseImperialUnits = _tempSettings.UseImperialUnits;
            _tempSettings.Use24HourClock = _tempSettings.Use24HourClock;
            _tempSettings.DisplayIndex = _settings.DisplayIndex;
            _tempSettings.Gamma = _settings.Gamma;
            foreach (var item in _uiElements.OfType<ISettingControl>()) item.RefreshValue();
            _isApplyingSettings = false; // Unset flag
        }

        private void ConfirmResetSettings()
        {
            _confirmationDialog.Show("Reset all settings to default?\n\nThis cannot be undone.", new List<Tuple<string, Action>> { Tuple.Create("YES", new Action(() => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); ; ExecuteResetSettings(); _confirmationDialog.Hide(); })), Tuple.Create("[chighlight]NO", new Action(() => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); _confirmationDialog.Hide(); })) });
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
                _confirmationDialog.Show(
                    "You have unsaved changes.",
                    new List<Tuple<string, Action>> {
                        Tuple.Create("APPLY", new Action(() => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); ApplySettings(); _sceneManager.HideModal(); })),
                        Tuple.Create("DISCARD", new Action(() => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); RevertChanges(); _sceneManager.HideModal(); })),
                        Tuple.Create("[chighlight]CANCEL", new Action(() => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); _confirmationDialog.Hide(); }))
                    }
                );
            }
            else
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                _sceneManager.HideModal();
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

            CalculateLayoutPositions();

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
                    // Only allow selection if the setting is enabled
                    if (hoverRect.Contains(virtualMousePos) && setting.IsEnabled) { _selectedIndex = i; }
                    if (_currentInputDelay <= 0) setting.Update(new Vector2(currentPos.X, currentPos.Y + 3), i == _selectedIndex, currentMouseState, previousMouseState, virtualMousePos, font);
                }
                else if (item is Button button)
                {
                    // Only allow selection if the button is enabled
                    if (button.Bounds.Contains(virtualMousePos) && button.IsEnabled) { _selectedIndex = i; }
                    if (_currentInputDelay <= 0) button.Update(currentMouseState);
                }
            }

            if (_currentInputDelay <= 0) HandleKeyboardInput(currentKeyboardState);

            // FIX: Use "APPLY" (uppercase) to match button creation
            var applyButton = _uiElements.OfType<Button>().FirstOrDefault(b => b.Text == "APPLY");
            if (applyButton != null) applyButton.IsEnabled = IsDirty();

            if (_currentInputDelay <= 0 && KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                AttemptToGoBack();
            }

            // Right Click to Go Back
            if (currentMouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released)
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                AttemptToGoBack();
            }

            base.Update(gameTime);
        }

        private void HandleKeyboardInput(KeyboardState currentKeyboardState)
        {
            bool selectionChanged = false;
            if (KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState)) { _sceneManager.LastInputDevice = InputDevice.Keyboard; _selectedIndex = FindNextSelectable(_selectedIndex, 1); selectionChanged = true; }
            if (KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState)) { _sceneManager.LastInputDevice = InputDevice.Keyboard; _selectedIndex = FindNextSelectable(_selectedIndex, -1); selectionChanged = true; }

            if (selectionChanged)
            {
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
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            string title = "SETTINGS";
            Vector2 titleSize = font.MeasureString(title);

            // Pixel-perfect bob: 0 or -1
            float yOffset = (MathF.Sin(_titleBobTimer * 4f) > 0) ? -1f : 0f;

            float titleBaseY = 10f; // Moved up 5px (was 15f)
            Vector2 titlePosition = new Vector2(screenWidth / 2 - titleSize.X / 2, titleBaseY + yOffset);

            // Use DrawStringSnapped for pixel perfection
            spriteBatch.DrawStringSnapped(font, title, titlePosition, _global.GameTextColor);

            // Draw divider line
            int dividerY = (int)(titleBaseY + titleSize.Y + 5);
            spriteBatch.Draw(pixel, new Rectangle(screenWidth / 2 - 90, dividerY, 180, 1), _global.Palette_DarkShadow);


            if (_confirmationTimer > 0)
            {
                Vector2 msgSize = font.MeasureString(_confirmationMessage);
                Vector2 messagePosition = new Vector2(screenWidth / 2 - msgSize.X / 2, 5);
                spriteBatch.DrawStringOutlinedSnapped(font, _confirmationMessage, messagePosition, _global.ConfirmSettingsColor, _global.Palette_Black);
            }

            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                var currentPos = _uiElementPositions[i];
                bool isSelected = (i == _selectedIndex);

                if (isSelected)
                {
                    if (item is ISettingControl)
                    {
                        var hoverRect = new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, SETTINGS_PANEL_WIDTH + 10, ITEM_VERTICAL_SPACING);
                        if (hoverRect.Contains(virtualMousePos) || keyboardNavigatedLastFrame)
                        {
                            // Draw highlight box expanded by 1px up and down to make it 14px tall
                            DrawRectangleBorder(spriteBatch, pixel, new Rectangle(hoverRect.X, hoverRect.Y - 1, hoverRect.Width, hoverRect.Height + 2), 1, _global.ButtonHoverColor);
                        }
                    }
                    else if (item is Button button)
                    {
                        if (button.IsHovered || keyboardNavigatedLastFrame)
                        {
                            // Draw highlight box for button
                            DrawRectangleBorder(spriteBatch, pixel, new Rectangle(button.Bounds.X, button.Bounds.Y - 1, button.Bounds.Width, button.Bounds.Height + 2), 1, _global.ButtonHoverColor);
                        }
                    }
                }

                if (item is ISettingControl setting)
                {
                    // Pass secondaryFont for labels, font (IBM) for values
                    // Pass Y + 3 for text centering
                    setting.Draw(spriteBatch, secondaryFont, font, new Vector2(currentPos.X, currentPos.Y + 3), isSelected, gameTime);
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
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var screenBounds = new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(ServiceLocator.Get<Texture2D>(), screenBounds, _global.GameBg);
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
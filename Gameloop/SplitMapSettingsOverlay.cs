#nullable enable
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

namespace ProjectVagabond.UI
{
    public class SplitMapSettingsOverlay
    {
        public bool IsOpen { get; private set; } = false;
        public event Action? OnCloseRequested;
        private readonly GameSettings _settings;
        private readonly GraphicsDeviceManager _graphics;
        private readonly Global _global;
        private readonly Core _core;
        private readonly GameScene _parentScene;

        private List<object> _uiElements = new();
        private List<Vector2> _uiElementPositions = new();
        private int _selectedIndex = -1;
        private string _confirmationMessage = "";
        private float _confirmationTimer = 0f;

        // --- Layout Tuning (Identical to SettingsScene) ---
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
        private bool _keyboardNavigatedLastFrame = false;

        // The world Y position where this overlay is drawn
        private const float WORLD_Y_OFFSET = 400f;

        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        public SplitMapSettingsOverlay(GameScene parentScene)
        {
            _parentScene = parentScene;
            _settings = ServiceLocator.Get<GameSettings>();
            _graphics = ServiceLocator.Get<GraphicsDeviceManager>();
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();

            _confirmationDialog = new ConfirmationDialog(_parentScene);
            _revertDialog = new RevertDialog(_parentScene);
        }

        public void Initialize()
        {
            RefreshUIFromSettings();
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }

        public void Show()
        {
            IsOpen = true;
            _currentInputDelay = _inputDelay;
            RefreshUIFromSettings();

            // Reset animation states
            foreach (var item in _uiElements)
            {
                if (item is ISettingControl setting) setting.ResetAnimationState();
                else if (item is Button button) button.ResetAnimationState();
            }
        }

        public void Hide()
        {
            IsOpen = false;
            _confirmationDialog.Hide();
            _revertDialog.Hide();
        }

        public bool IsDirty() => _uiElements.OfType<ISettingControl>().Any(s => s.IsDirty);

        public void AttemptClose(Action onClose)
        {
            if (IsDirty())
            {
                _confirmationDialog.Show(
                    "Discard unsaved changes?",
                    new List<Tuple<string, Action>> {
                    Tuple.Create("YES", new Action(() => {
                        RevertChanges();
                        _confirmationDialog.Hide();
                        onClose?.Invoke();
                    })),
                    Tuple.Create("[gray]NO", new Action(() => {
                        _confirmationDialog.Hide();
                    }))
                    }
                );
            }
            else
            {
                onClose?.Invoke();
            }
        }

        public void RefreshUIFromSettings()
        {
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
                if (standardEntry.Key != null)
                {
                    int aspectIndex = standardEntry.Key.IndexOf(" (");
                    if (aspectIndex != -1) return standardEntry.Key.Substring(aspectIndex).Trim();
                }
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
            framerateControl.IsEnabled = _tempSettings.IsFrameLimiterEnabled;
            _uiElements.Add(framerateControl);

            // IMPORTANT: Set UseScreenCoordinates = true for these buttons.
            var applyButton = new Button(new Rectangle(0, 0, 125, 10), "Apply")
            {
                TextRenderOffset = new Vector2(0, 1),
                UseScreenCoordinates = true
            };
            applyButton.OnClick += ApplySettings;
            _uiElements.Add(applyButton);

            var discardButton = new Button(new Rectangle(0, 0, 125, 10), "Discard")
            {
                TextRenderOffset = new Vector2(0, 1),
                UseScreenCoordinates = true
            };
            discardButton.OnClick += () => AttemptClose(() => OnCloseRequested?.Invoke());
            _uiElements.Add(discardButton);

            var resetButton = new Button(new Rectangle(0, 0, 125, 10), "Restore Defaults")
            {
                CustomDefaultTextColor = _global.Palette_LightYellow,
                TextRenderOffset = new Vector2(0, 1),
                UseScreenCoordinates = true
            };
            resetButton.OnClick += ConfirmResetSettings;
            _uiElements.Add(resetButton);

            CalculateLayoutPositions();
            applyButton.IsEnabled = IsDirty();
        }

        private void CalculateLayoutPositions()
        {
            _uiElementPositions.Clear();
            // Base position is offset by WORLD_Y_OFFSET
            Vector2 currentPos = new Vector2(SETTINGS_PANEL_X, SETTINGS_START_Y + WORLD_Y_OFFSET);

            foreach (var item in _uiElements)
            {
                _uiElementPositions.Add(currentPos);

                if (item is Button button)
                {
                    button.Bounds = new Rectangle(
                        (int)currentPos.X - 5,
                        (int)currentPos.Y - 2,
                        SETTINGS_PANEL_WIDTH + 10,
                        BUTTON_VERTICAL_SPACING
                    );
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

            _isApplyingSettings = true;
            bool graphicsChanged = _tempSettings.Resolution != _settings.Resolution || _tempSettings.Mode != _settings.Mode;

            if (graphicsChanged)
            {
                var revertState = new GameSettings { Resolution = _settings.Resolution, Mode = _settings.Mode };
                _tempSettings.ApplyGraphicsSettings(_graphics, _core);

                _revertDialog.Show(
                    "Keep these display settings?",
                    onConfirm: () => FinalizeAndSaveAllSettings(),
                    onRevert: () =>
                    {
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
            _isApplyingSettings = false;
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
            _isApplyingSettings = false;
        }

        private void ConfirmResetSettings()
        {
            _confirmationDialog.Show("Reset all settings to default?\n\nThis cannot be undone.", new List<Tuple<string, Action>> { Tuple.Create("YES", new Action(() => { ExecuteResetSettings(); _confirmationDialog.Hide(); })), Tuple.Create("[gray]NO", new Action(() => _confirmationDialog.Hide())) });
        }

        private void ExecuteResetSettings()
        {
            var preservedResolution = _tempSettings.Resolution;
            var preservedWindowMode = _tempSettings.Mode;
            _tempSettings = new GameSettings();
            _tempSettings.Resolution = preservedResolution;
            _tempSettings.Mode = preservedWindowMode;

            foreach (var item in _uiElements.OfType<ISettingControl>()) item.RefreshValue();
            ApplySettings();
            _confirmationMessage = "Settings Reset to Default!";
            _confirmationTimer = 5f;
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);

        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState, Matrix cameraTransform)
        {
            if (!IsOpen)
            {
                _previousMouseState = currentMouseState;
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            _titleBobTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Update(gameTime);
                return;
            }
            if (_revertDialog.IsActive)
            {
                _revertDialog.Update(gameTime);
                return;
            }

            if (_currentInputDelay > 0) _currentInputDelay -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_confirmationTimer > 0) _confirmationTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            var framerateControl = _uiElements.OfType<ISettingControl>().FirstOrDefault(c => c.Label == "Target Framerate");
            if (framerateControl != null) framerateControl.IsEnabled = _tempSettings.IsFrameLimiterEnabled;

            CalculateLayoutPositions();

            // Transform mouse to world space to interact with the offset UI
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));

            // Reset selection if mouse moved
            if (currentMouseState.Position != _previousMouseState.Position)
            {
                _selectedIndex = -1;
            }

            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                var currentPos = _uiElementPositions[i];

                if (item is ISettingControl setting)
                {
                    var hoverRect = new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, SETTINGS_PANEL_WIDTH + 10, ITEM_VERTICAL_SPACING);
                    if (hoverRect.Contains(mouseInWorldSpace) && setting.IsEnabled) { _selectedIndex = i; }
                    if (_currentInputDelay <= 0) setting.Update(new Vector2(currentPos.X, currentPos.Y + 2), i == _selectedIndex, currentMouseState, _previousMouseState, mouseInWorldSpace, ServiceLocator.Get<BitmapFont>());
                }
                else if (item is Button button)
                {
                    if (button.Bounds.Contains(mouseInWorldSpace) && button.IsEnabled) { _selectedIndex = i; }

                    // Create a fake mouse state that matches the world space for the button's logic.
                    var worldMouseState = new MouseState((int)mouseInWorldSpace.X, (int)mouseInWorldSpace.Y, currentMouseState.ScrollWheelValue, currentMouseState.LeftButton, currentMouseState.MiddleButton, currentMouseState.RightButton, currentMouseState.XButton1, currentMouseState.XButton2);

                    // Pass the world-space mouse state. Because UseScreenCoordinates is true, 
                    // the Button class will use these coordinates directly without re-transforming them.
                    if (_currentInputDelay <= 0) button.Update(worldMouseState);
                }
            }

            if (_currentInputDelay <= 0) HandleKeyboardInput(currentKeyboardState);
            var applyButton = _uiElements.OfType<Button>().FirstOrDefault(b => b.Text == "Apply");
            if (applyButton != null) applyButton.IsEnabled = IsDirty();

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        private void HandleKeyboardInput(KeyboardState currentKeyboardState)
        {
            if (KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState)) { _selectedIndex = FindNextSelectable(_selectedIndex, 1); _keyboardNavigatedLastFrame = true; }
            if (KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState)) { _selectedIndex = FindNextSelectable(_selectedIndex, -1); _keyboardNavigatedLastFrame = true; }

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
                if (item is ISettingControl setting && setting.IsEnabled) return searchIndex;
                if (item is Button button && button.IsEnabled) return searchIndex;
            }
            return currentIndex;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix currentTransform)
        {
            if (!IsOpen) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            int screenWidth = Global.VIRTUAL_WIDTH;

            // Draw Background for the settings area
            var bgRect = new Rectangle(0, (int)WORLD_Y_OFFSET, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            spriteBatch.DrawSnapped(pixel, bgRect, _global.GameBg);

            string title = "Settings";
            Vector2 titleSize = font.MeasureString(title);
            float yOffset = (float)Math.Sin(_titleBobTimer * TitleBobSpeed) * TitleBobAmount;
            float titleBaseY = 15f + WORLD_Y_OFFSET;
            Vector2 titlePosition = new Vector2(screenWidth / 2 - titleSize.X / 2, titleBaseY + yOffset);
            spriteBatch.DrawString(font, title, titlePosition, _global.Palette_BrightWhite);

            int dividerY = (int)(titleBaseY + titleSize.Y + 5);
            spriteBatch.Draw(pixel, new Rectangle(screenWidth / 2 - 90, dividerY, 180, 1), _global.Palette_Gray);

            if (_confirmationTimer > 0)
            {
                Vector2 msgSize = font.MeasureString(_confirmationMessage);
                Vector2 messagePosition = new Vector2(screenWidth / 2 - msgSize.X / 2, 5 + WORLD_Y_OFFSET);
                spriteBatch.DrawString(font, _confirmationMessage, messagePosition, _global.Palette_Teal);
            }

            for (int i = 0; i < _uiElements.Count; i++)
            {
                var item = _uiElements[i];
                var currentPos = _uiElementPositions[i];
                bool isSelected = (i == _selectedIndex);

                if (isSelected)
                {
                    // Draw highlight box
                    float itemHeight = (item is ISettingControl) ? ITEM_VERTICAL_SPACING : (item is Button) ? BUTTON_VERTICAL_SPACING : 0;
                    if (itemHeight > 0) DrawRectangleBorder(spriteBatch, pixel, new Rectangle((int)currentPos.X - 5, (int)currentPos.Y - 2, SETTINGS_PANEL_WIDTH + 10, (int)itemHeight), 1, _global.ButtonHoverColor);
                }

                if (item is ISettingControl setting)
                {
                    setting.Draw(spriteBatch, font, new Vector2(currentPos.X, currentPos.Y + 2), isSelected, gameTime);
                }
                else if (item is Button button)
                {
                    // Button draws itself based on its Bounds, which are already in world space
                    button.Draw(spriteBatch, font, gameTime, Matrix.Identity, isSelected);
                }
            }

            // --- DIALOG DRAWING FIX ---
            // If a dialog is active, we need to draw it in Screen Space (Matrix.Identity)
            // because the dialogs calculate their bounds based on the screen center (0,0 to 320,180).
            // The current batch has a camera transform applied (e.g., Y translation of -400).
            // Drawing at Y=50 with a -400 transform puts it at -350 (off screen).
            // So we interrupt the batch, draw in screen space, and then resume.
            if (_confirmationDialog.IsActive || _revertDialog.IsActive)
            {
                spriteBatch.End();
                spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: Matrix.Identity);

                if (_confirmationDialog.IsActive) _confirmationDialog.DrawContent(spriteBatch, font, gameTime, Matrix.Identity);
                if (_revertDialog.IsActive) _revertDialog.DrawContent(spriteBatch, font, gameTime, Matrix.Identity);

                spriteBatch.End();
                // Resume the batch with the original transform so the caller (SplitMapScene) can finish cleanly
                spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: currentTransform);
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
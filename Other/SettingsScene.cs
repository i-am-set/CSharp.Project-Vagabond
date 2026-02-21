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

        // Separate lists for scrolling items vs fixed buttons
        private List<ISettingControl> _settingControls = new();
        private List<Button> _footerButtons = new();

        // Selection tracking
        private int _selectedSettingIndex = -1; // -1 means focus is on buttons
        private int _selectedButtonIndex = -1;  // -1 means focus is on settings

        private string _confirmationMessage = "";
        private float _confirmationTimer = 0f;

        // --- Layout Tuning ---
        private const int SETTINGS_START_Y = 30;
        private const int ITEM_VERTICAL_SPACING = 12;
        private const int BUTTON_VERTICAL_SPACING = 12;
        private const int SETTINGS_PANEL_WIDTH = 280;
        private const int SETTINGS_PANEL_X = (Global.VIRTUAL_WIDTH - SETTINGS_PANEL_WIDTH) / 2;

        // --- Scrolling Tuning ---
        private const int VISIBLE_ITEMS_COUNT = 8; // How many items fit in the window
        private const int SCROLL_VIEW_HEIGHT = VISIBLE_ITEMS_COUNT * ITEM_VERTICAL_SPACING;
        private Rectangle _listViewPort; // The clipping rectangle
        private float _scrollOffset = 0f;
        private float _targetScrollOffset = 0f;
        private int _previousScrollValue;
        private const float SCROLL_SMOOTHING = 0.2f;

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
            _isApplyingSettings = false;
            _confirmationDialog = new ConfirmationDialog(this);
            _revertDialog = new RevertDialog(this);

            RefreshUIFromSettings();

            EventBus.Subscribe<GameEvents.UIThemeOrResolutionChanged>(OnResolutionChanged);

            foreach (var item in _settingControls) item.ResetAnimationState();
            foreach (var item in _footerButtons) item.ResetAnimationState();

            if (this.LastInputDevice == InputDevice.Keyboard)
            {
                _selectedSettingIndex = 0;
                _selectedButtonIndex = -1;
                EnsureSelectionVisible();
            }
            else
            {
                _selectedSettingIndex = -1;
                _selectedButtonIndex = -1;
            }

            _previousKeyboardState = Keyboard.GetState();
            _previousScrollValue = Mouse.GetState().ScrollWheelValue;
            _currentInputDelay = _inputDelay;
            _scrollOffset = 0;
            _targetScrollOffset = 0;
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
                Gamma = _settings.Gamma,
                EnableGlitchEffects = _settings.EnableGlitchEffects
            };
            _titleBobTimer = 0f;
            BuildInitialUI();
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
            // Just return the first slot in the list view
            return new Rectangle(SETTINGS_PANEL_X - 5, SETTINGS_START_Y, SETTINGS_PANEL_WIDTH + 10, ITEM_VERTICAL_SPACING);
        }

        private void BuildInitialUI()
        {
            _settingControls.Clear();
            _footerButtons.Clear();

            // --- 1. Resolution ---
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
                if (standardEntry.Key != null)
                {
                    int aspectIndex = standardEntry.Key.IndexOf(" (");
                    if (aspectIndex != -1) return standardEntry.Key.Substring(aspectIndex).Trim();
                }
                return $"({currentResPoint.X}x{currentResPoint.Y})";
            };
            _settingControls.Add(resolutionControl);

            // --- 2. Window Mode ---
            var windowModes = new List<KeyValuePair<string, WindowMode>>
            {
                new("Windowed", WindowMode.Windowed),
                new("Borderless", WindowMode.Borderless),
                new("Fullscreen", WindowMode.Fullscreen)
            };
            var windowModeControl = new OptionSettingControl<WindowMode>("Window Mode", windowModes, () => _tempSettings.Mode, v =>
            {
                _tempSettings.Mode = v;
                if (v == WindowMode.Borderless) SetResolutionToNative();
            });
            _settingControls.Add(windowModeControl);

            // --- 3. Other Settings ---
            _settingControls.Add(new SegmentedBarSettingControl("Gamma", 1.0f, 2.0f, 11, () => _tempSettings.Gamma, v => _tempSettings.Gamma = v));
            _settingControls.Add(new BoolSettingControl("Smaller UI", () => _tempSettings.SmallerUi, v => _tempSettings.SmallerUi = v));
            _settingControls.Add(new BoolSettingControl("VSync", () => _tempSettings.IsVsync, v => _tempSettings.IsVsync = v));
            _settingControls.Add(new BoolSettingControl("Frame Limiter", () => _tempSettings.IsFrameLimiterEnabled, v => _tempSettings.IsFrameLimiterEnabled = v));
            var framerates = new List<KeyValuePair<string, int>> { new("30 FPS", 30), new("60 FPS", 60), new("75 FPS", 75), new("120 FPS", 120), new("144 FPS", 144), new("240 FPS", 240) };
            var framerateControl = new OptionSettingControl<int>("Target Framerate", framerates, () => _tempSettings.TargetFramerate, v => _tempSettings.TargetFramerate = v);
            framerateControl.IsEnabled = _tempSettings.IsFrameLimiterEnabled;
            _settingControls.Add(framerateControl);

            // --- 4. Visual ---
            _settingControls.Add(new BoolSettingControl("Glitch Effects", () => _tempSettings.EnableGlitchEffects, v => _tempSettings.EnableGlitchEffects = v));

            // --- Footer Buttons ---
            var applyButton = new Button(new Rectangle(0, 0, 125, 12), "APPLY") { TextRenderOffset = new Vector2(0, 1) };
            applyButton.OnClick += () => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); ApplySettings(); };
            _footerButtons.Add(applyButton);

            var backButton = new Button(new Rectangle(0, 0, 125, 12), "BACK") { TextRenderOffset = new Vector2(0, 1) };
            backButton.OnClick += () => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); AttemptToGoBack(); };
            _footerButtons.Add(backButton);

            var resetButton = new Button(new Rectangle(0, 0, 125, 12), "RESTORE DEFAULTS") { CustomDefaultTextColor = _global.HighlightTextColor, TextRenderOffset = new Vector2(0, 1) };
            resetButton.OnClick += () => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); ConfirmResetSettings(); };
            _footerButtons.Add(resetButton);

            // Define the viewport for the scrollable list
            // Expanded vertically by 2px top and 2px bottom to prevent border clipping
            _listViewPort = new Rectangle(SETTINGS_PANEL_X - 10, SETTINGS_START_Y - 2, SETTINGS_PANEL_WIDTH + 30, SCROLL_VIEW_HEIGHT + 4);

            CalculateButtonLayout();
            applyButton.IsEnabled = IsDirty();
        }

        private void CalculateButtonLayout()
        {
            int bottomMargin = 8;
            int startButtonY = Global.VIRTUAL_HEIGHT - bottomMargin - (3 * BUTTON_VERTICAL_SPACING);
            Vector2 currentButtonPos = new Vector2(SETTINGS_PANEL_X, startButtonY);
            var font = ServiceLocator.Get<BitmapFont>();

            foreach (var button in _footerButtons)
            {
                var textSize = font.MeasureString(button.Text);
                int width = (int)textSize.Width + 20;
                int centeredX = (Global.VIRTUAL_WIDTH - width) / 2;

                button.Bounds = new Rectangle(centeredX, (int)currentButtonPos.Y, width, BUTTON_VERTICAL_SPACING);
                currentButtonPos.Y += BUTTON_VERTICAL_SPACING;
            }
        }

        private void SetResolutionToNative()
        {
            var nativeResolution = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            var nativePoint = new Point(nativeResolution.Width, nativeResolution.Height);
            _tempSettings.Resolution = SettingsManager.FindClosestResolution(nativePoint);
            _settingControls.OfType<ISettingControl>().FirstOrDefault(c => c.Label == "Resolution")?.RefreshValue();
        }

        private void ApplySettings()
        {
            if (!IsDirty()) return;

            ResetInputBlockTimer();
            _isApplyingSettings = true;

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
            _settings.EnableGlitchEffects = _tempSettings.EnableGlitchEffects;

            _settings.ApplyGraphicsSettings(_graphics, _core);
            _settings.ApplyGameSettings();
            SettingsManager.SaveSettings(_settings);

            foreach (var item in _settingControls) item.Apply();
            _confirmationMessage = "Settings Applied!";
            _confirmationTimer = 5f;
            _isApplyingSettings = false;
        }

        private void RevertChanges()
        {
            foreach (var item in _settingControls) item.Revert();
            _tempSettings.Resolution = _settings.Resolution;
            _tempSettings.Mode = _settings.Mode;
            _tempSettings.IsVsync = _settings.IsVsync;
            _tempSettings.IsFrameLimiterEnabled = _settings.IsFrameLimiterEnabled;
            _tempSettings.TargetFramerate = _settings.TargetFramerate;
            _tempSettings.SmallerUi = _settings.SmallerUi;
            _tempSettings.UseImperialUnits = _settings.UseImperialUnits;
            _tempSettings.Use24HourClock = _tempSettings.Use24HourClock;
            _tempSettings.DisplayIndex = _settings.DisplayIndex;
            _tempSettings.Gamma = _settings.Gamma;
            _tempSettings.EnableGlitchEffects = _settings.EnableGlitchEffects;

            foreach (var item in _settingControls) item.RefreshValue();
            _isApplyingSettings = false;
        }

        private void ConfirmResetSettings()
        {
            _confirmationDialog.Show("Reset all settings to default?\n\nThis cannot be undone.", new List<Tuple<string, Action>> { Tuple.Create("YES", new Action(() => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); ; ExecuteResetSettings(); _confirmationDialog.Hide(); })), Tuple.Create("[chighlight]NO", new Action(() => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); _confirmationDialog.Hide(); })) });
        }

        private void ExecuteResetSettings()
        {
            var preservedResolution = _tempSettings.Resolution;
            var preservedWindowMode = _tempSettings.Mode;
            _tempSettings = new GameSettings();
            _tempSettings.Resolution = preservedResolution;
            _tempSettings.Mode = preservedWindowMode;

            foreach (var item in _settingControls) item.RefreshValue();
            ApplySettings();
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

        private bool IsDirty() => _settingControls.Any(s => s.IsDirty);
        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);

        public override void Update(GameTime gameTime)
        {
            _titleBobTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (IsInputBlocked) { base.Update(gameTime); return; }
            if (_confirmationDialog.IsActive) { _confirmationDialog.Update(gameTime); base.Update(gameTime); return; }
            if (_revertDialog.IsActive) { _revertDialog.Update(gameTime); base.Update(gameTime); return; }

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();
            var font = ServiceLocator.Get<BitmapFont>();

            // --- Input Device Detection ---
            if (currentMouseState.Position != previousMouseState.Position || (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released) || currentMouseState.ScrollWheelValue != _previousScrollValue)
            {
                _sceneManager.LastInputDevice = InputDevice.Mouse;
            }

            if (_sceneManager.LastInputDevice == InputDevice.Mouse)
            {
                // Clear keyboard selection visual if moving mouse, unless we are hovering something
                // We'll let the hover logic below handle setting the index
            }

            if (_currentInputDelay > 0) _currentInputDelay -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_confirmationTimer > 0) _confirmationTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Dependency Update
            var framerateControl = _settingControls.FirstOrDefault(c => c.Label == "Target Framerate");
            if (framerateControl != null) framerateControl.IsEnabled = _tempSettings.IsFrameLimiterEnabled;

            // --- Scroll Logic ---
            float maxScroll = Math.Max(0, (_settingControls.Count * ITEM_VERTICAL_SPACING) - SCROLL_VIEW_HEIGHT);
            int scrollDelta = (currentMouseState.ScrollWheelValue - _previousScrollValue);
            if (scrollDelta != 0)
            {
                _targetScrollOffset -= Math.Sign(scrollDelta) * ITEM_VERTICAL_SPACING;
                _targetScrollOffset = Math.Clamp(_targetScrollOffset, 0, maxScroll);
            }
            _previousScrollValue = currentMouseState.ScrollWheelValue;

            // Smooth Scroll
            _scrollOffset = MathHelper.Lerp(_scrollOffset, _targetScrollOffset, SCROLL_SMOOTHING);
            if (Math.Abs(_scrollOffset - _targetScrollOffset) < 0.5f) _scrollOffset = _targetScrollOffset;

            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            // --- Update Settings List ---
            bool mouseOverAnySetting = false;
            for (int i = 0; i < _settingControls.Count; i++)
            {
                var item = _settingControls[i];
                float itemY = SETTINGS_START_Y + (i * ITEM_VERTICAL_SPACING) - _scrollOffset;

                // Only update if visible in viewport
                if (itemY >= SETTINGS_START_Y - ITEM_VERTICAL_SPACING && itemY < SETTINGS_START_Y + SCROLL_VIEW_HEIGHT)
                {
                    Vector2 itemPos = new Vector2(SETTINGS_PANEL_X, itemY);
                    var hoverRect = new Rectangle((int)itemPos.X - 5, (int)itemPos.Y, SETTINGS_PANEL_WIDTH + 10, ITEM_VERTICAL_SPACING);

                    bool isHovered = hoverRect.Contains(virtualMousePos) && _listViewPort.Contains(virtualMousePos); // Check clipping rect too

                    if (isHovered && item.IsEnabled)
                    {
                        if (_sceneManager.LastInputDevice == InputDevice.Mouse)
                        {
                            _selectedSettingIndex = i;
                            _selectedButtonIndex = -1;
                        }
                        mouseOverAnySetting = true;
                    }

                    if (_currentInputDelay <= 0)
                    {
                        // Pass Y+2 for text alignment
                        item.Update(new Vector2(itemPos.X, itemPos.Y + 2), i == _selectedSettingIndex, currentMouseState, previousMouseState, virtualMousePos, font);
                    }
                }
            }

            // --- Update Footer Buttons ---
            for (int i = 0; i < _footerButtons.Count; i++)
            {
                var button = _footerButtons[i];
                if (button.Bounds.Contains(virtualMousePos) && button.IsEnabled)
                {
                    if (_sceneManager.LastInputDevice == InputDevice.Mouse)
                    {
                        _selectedButtonIndex = i;
                        _selectedSettingIndex = -1;
                    }
                }
                if (_currentInputDelay <= 0) button.Update(currentMouseState);
            }

            if (_sceneManager.LastInputDevice == InputDevice.Mouse && !mouseOverAnySetting && _selectedButtonIndex == -1)
            {
                _selectedSettingIndex = -1;
            }

            if (_currentInputDelay <= 0) HandleKeyboardInput(currentKeyboardState);

            var applyButton = _footerButtons.FirstOrDefault(b => b.Text == "APPLY");
            if (applyButton != null) applyButton.IsEnabled = IsDirty();

            if (_currentInputDelay <= 0 && KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                AttemptToGoBack();
            }

            if (currentMouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released)
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                AttemptToGoBack();
            }

            base.Update(gameTime);
        }

        private void HandleKeyboardInput(KeyboardState currentKeyboardState)
        {
            bool upPressed = KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState);
            bool downPressed = KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState);

            if (upPressed || downPressed)
            {
                _sceneManager.LastInputDevice = InputDevice.Keyboard;
                keyboardNavigatedLastFrame = true;

                if (_selectedSettingIndex != -1)
                {
                    // Navigating within Settings List
                    if (upPressed)
                    {
                        int next = _selectedSettingIndex - 1;
                        if (next >= 0) _selectedSettingIndex = next;
                        else
                        {
                            // Wrap around to bottom buttons? Or stay at top?
                            // Let's stay at top for list, or maybe wrap to bottom buttons?
                            // Standard behavior: Stay at top.
                        }
                    }
                    else if (downPressed)
                    {
                        int next = _selectedSettingIndex + 1;
                        if (next < _settingControls.Count) _selectedSettingIndex = next;
                        else
                        {
                            // Move to Buttons
                            _selectedSettingIndex = -1;
                            _selectedButtonIndex = 0;
                        }
                    }
                }
                else if (_selectedButtonIndex != -1)
                {
                    // Navigating within Buttons
                    if (upPressed)
                    {
                        int next = _selectedButtonIndex - 1;
                        if (next >= 0) _selectedButtonIndex = next;
                        else
                        {
                            // Move back to Settings List (Bottom Item)
                            _selectedButtonIndex = -1;
                            _selectedSettingIndex = _settingControls.Count - 1;
                        }
                    }
                    else if (downPressed)
                    {
                        int next = _selectedButtonIndex + 1;
                        if (next < _footerButtons.Count) _selectedButtonIndex = next;
                    }
                }
                else
                {
                    // Nothing selected, select first item
                    _selectedSettingIndex = 0;
                }

                EnsureSelectionVisible();
            }

            // Handle Interaction
            if (_selectedSettingIndex != -1)
            {
                var setting = _settingControls[_selectedSettingIndex];
                if (KeyPressed(Keys.Left, currentKeyboardState, _previousKeyboardState)) setting.HandleInput(Keys.Left);
                if (KeyPressed(Keys.Right, currentKeyboardState, _previousKeyboardState)) setting.HandleInput(Keys.Right);
            }
            else if (_selectedButtonIndex != -1)
            {
                var button = _footerButtons[_selectedButtonIndex];
                if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState)) button.TriggerClick();
            }
        }

        private void EnsureSelectionVisible()
        {
            if (_selectedSettingIndex == -1) return;

            float itemTop = _selectedSettingIndex * ITEM_VERTICAL_SPACING;
            float itemBottom = itemTop + ITEM_VERTICAL_SPACING;

            // If item is above view
            if (itemTop < _targetScrollOffset)
            {
                _targetScrollOffset = itemTop;
            }
            // If item is below view
            else if (itemBottom > _targetScrollOffset + SCROLL_VIEW_HEIGHT)
            {
                _targetScrollOffset = itemBottom - SCROLL_VIEW_HEIGHT;
            }

            // Clamp
            float maxScroll = Math.Max(0, (_settingControls.Count * ITEM_VERTICAL_SPACING) - SCROLL_VIEW_HEIGHT);
            _targetScrollOffset = Math.Clamp(_targetScrollOffset, 0, maxScroll);
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            var virtualMousePos = Core.TransformMouse(Mouse.GetState().Position);
            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            string title = "SETTINGS";
            Vector2 titleSize = font.MeasureString(title);
            float yOffset = (MathF.Sin(_titleBobTimer * 4f) > 0) ? -1f : 0f;
            float titleBaseY = 10f;
            Vector2 titlePosition = new Vector2(screenWidth / 2 - titleSize.X / 2, titleBaseY + yOffset);

            spriteBatch.DrawStringSnapped(font, title, titlePosition, _global.GameTextColor);
            int dividerY = (int)(titleBaseY + titleSize.Y + 5);
            spriteBatch.Draw(pixel, new Rectangle(screenWidth / 2 - 90, dividerY, 180, 1), _global.DullTextColor);

            if (_confirmationTimer > 0)
            {
                Vector2 msgSize = font.MeasureString(_confirmationMessage);
                Vector2 messagePosition = new Vector2(screenWidth / 2 - msgSize.X / 2, 5);
                spriteBatch.DrawStringOutlinedSnapped(font, _confirmationMessage, messagePosition, _global.ConfirmSettingsColor, _global.Palette_Black);
            }

            // --- Draw Scrollable List (With Scissor) ---
            spriteBatch.End(); // End current batch to apply scissor

            var core = ServiceLocator.Get<Core>();
            Rectangle screenScissor = ScaleRectToScreen(_listViewPort, core.FinalScale, core.FinalRenderRectangle.Location);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState { ScissorTestEnable = true }, null, transform);
            _graphics.GraphicsDevice.ScissorRectangle = screenScissor;

            for (int i = 0; i < _settingControls.Count; i++)
            {
                var item = _settingControls[i];
                float itemY = SETTINGS_START_Y + (i * ITEM_VERTICAL_SPACING) - _scrollOffset;
                Vector2 currentPos = new Vector2(SETTINGS_PANEL_X, itemY);
                bool isSelected = (i == _selectedSettingIndex);

                if (isSelected)
                {
                    var hoverRect = new Rectangle((int)currentPos.X - 5, (int)currentPos.Y, SETTINGS_PANEL_WIDTH + 10, ITEM_VERTICAL_SPACING);
                    DrawRectangleBorder(spriteBatch, pixel, new Rectangle(hoverRect.X, hoverRect.Y - 1, hoverRect.Width, hoverRect.Height + 1), 1, _global.ButtonHoverColor);
                }

                item.Draw(spriteBatch, secondaryFont, font, new Vector2(currentPos.X, currentPos.Y + 2), isSelected, gameTime);
            }

            spriteBatch.End();

            // --- Draw Scrollbar & Footer (No Scissor) ---
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            DrawScrollbar(spriteBatch, pixel);

            for (int i = 0; i < _footerButtons.Count; i++)
            {
                var button = _footerButtons[i];
                bool isSelected = (i == _selectedButtonIndex);
                if (isSelected && (button.IsHovered || keyboardNavigatedLastFrame))
                {
                    DrawRectangleBorder(spriteBatch, pixel, new Rectangle(button.Bounds.X, button.Bounds.Y - 1, button.Bounds.Width, button.Bounds.Height + 2), 1, _global.ButtonHoverColor);
                }
                button.Draw(spriteBatch, font, gameTime, transform, isSelected);
            }

            if (_confirmationDialog.IsActive) _confirmationDialog.DrawContent(spriteBatch, font, gameTime, transform);
            if (_revertDialog.IsActive) _revertDialog.DrawContent(spriteBatch, font, gameTime, transform);
        }

        private void DrawScrollbar(SpriteBatch spriteBatch, Texture2D pixel)
        {
            float totalContentHeight = _settingControls.Count * ITEM_VERTICAL_SPACING;
            if (totalContentHeight <= SCROLL_VIEW_HEIGHT) return;

            // Position scrollbar to the right of the settings panel
            int trackX = SETTINGS_PANEL_X + SETTINGS_PANEL_WIDTH + 6;
            int trackY = SETTINGS_START_Y;
            int trackHeight = SCROLL_VIEW_HEIGHT;
            int trackWidth = 3;

            // Draw Track
            spriteBatch.Draw(pixel, new Rectangle(trackX, trackY, trackWidth, trackHeight), _global.Palette_DarkShadow);

            // Calculate Thumb
            float viewRatio = (float)SCROLL_VIEW_HEIGHT / totalContentHeight;
            float thumbHeight = Math.Max(10, trackHeight * viewRatio);

            float maxScroll = totalContentHeight - SCROLL_VIEW_HEIGHT;
            float scrollRatio = _scrollOffset / maxScroll;
            float thumbY = trackY + (scrollRatio * (trackHeight - thumbHeight));

            // Draw Thumb
            spriteBatch.Draw(pixel, new Rectangle(trackX, (int)thumbY, trackWidth, (int)thumbHeight), _global.Palette_Sun);
        }

        private Rectangle ScaleRectToScreen(Rectangle virtualRect, float scale, Point offset)
        {
            // This helper ensures the scissor rect aligns with the scaled viewport
            return new Rectangle(
                (int)(virtualRect.X * scale) + offset.X,
                (int)(virtualRect.Y * scale) + offset.Y,
                (int)(virtualRect.Width * scale),
                (int)(virtualRect.Height * scale)
            );
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Dice;
using ProjectVagabond.Encounters;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Scenes
{
    public class TerminalMapScene : GameScene
    {
        private readonly GameState _coreState;
        private readonly SceneManager _sceneManager;
        private readonly ClockRenderer _clockRenderer;
        private readonly SpriteManager _spriteManager;
        private readonly WorldClockManager _worldClockManager;
        private readonly InputHandler _inputHandler;
        private readonly MapInputHandler _mapInputHandler;
        private readonly MapRenderer _mapRenderer;
        private readonly StatsRenderer _statsRenderer;
        private readonly HapticsManager _hapticsManager;
        private readonly TerminalRenderer _terminalRenderer;
        private readonly AutoCompleteManager _autoCompleteManager;
        private readonly DiceRollingSystem _diceRollingSystem;
        private readonly PromptRenderer _promptRenderer;
        private readonly PreEncounterAnimationSystem _preEncounterAnimationSystem;
        private WaitDialog _waitDialog;
        private EncounterDialog _encounterDialog;
        private ImageButton _settingsButton;
        private KeyboardState _previousKeyboardState;
        private readonly LoadingScreen _loadingScreen;
        private bool _isInitialLoad = true;

        private const float TIME_PASS_FAILSAFE_SECONDS = 10.0f;

        public TerminalMapScene()
        {
            _coreState = ServiceLocator.Get<GameState>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _clockRenderer = ServiceLocator.Get<ClockRenderer>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _inputHandler = ServiceLocator.Get<InputHandler>();
            _mapInputHandler = ServiceLocator.Get<MapInputHandler>();
            _mapRenderer = ServiceLocator.Get<MapRenderer>();
            _statsRenderer = ServiceLocator.Get<StatsRenderer>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _terminalRenderer = ServiceLocator.Get<TerminalRenderer>();
            _autoCompleteManager = ServiceLocator.Get<AutoCompleteManager>();
            _diceRollingSystem = ServiceLocator.Get<DiceRollingSystem>();
            _loadingScreen = new LoadingScreen();
            _promptRenderer = ServiceLocator.Get<PromptRenderer>();
            _preEncounterAnimationSystem = ServiceLocator.Get<PreEncounterAnimationSystem>();

            EventBus.Subscribe<GameEvents.EntityTookDamage>(OnEntityTookDamage);
        }

        private void OnEntityTookDamage(GameEvents.EntityTookDamage e)
        {
            // If the player is the one taking damage, trigger a more intense shake.
            if (e.EntityId == _coreState.PlayerEntityId)
            {
                _hapticsManager.TriggerShake(10.0f, 0.35f);
            }
            else
            {
                // If an enemy takes damage, trigger the requested shake for attack feedback.
                _hapticsManager.TriggerShake(6, 0.35f);
            }
        }

        public override void Enter()
        {
            base.Enter();
            _core.IsMouseVisible = true;
            _waitDialog = new WaitDialog(this);
            _encounterDialog = new EncounterDialog(this);
            _clockRenderer.OnClockClicked += ShowWaitDialog;
            _diceRollingSystem.OnRollCompleted += OnDiceRollCompleted;

            if (_isInitialLoad)
            {
                _isInitialLoad = false;
                _loadingScreen.AddTask(new DiceWarmupTask());
                _loadingScreen.Start();
            }

            if (_settingsButton == null)
            {
                var settingsIcon = _spriteManager.SettingsIconSprite;
                var buttonSize = 16;
                if (settingsIcon != null) buttonSize = Math.Max(settingsIcon.Width, settingsIcon.Height);
                _settingsButton = new ImageButton(new Rectangle(5, 5, buttonSize, buttonSize), settingsIcon);
            }
            _settingsButton.OnClick += OpenSettings;

            _previousKeyboardState = Keyboard.GetState();
        }

        public override void Exit()
        {
            base.Exit();
            _clockRenderer.OnClockClicked -= ShowWaitDialog;
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
            EventBus.Unsubscribe<GameEvents.EntityTookDamage>(OnEntityTookDamage);
            _diceRollingSystem.OnRollCompleted -= OnDiceRollCompleted;
        }

        public void ShowEncounter(EncounterData encounterData)
        {
            _encounterDialog.Show(encounterData);
        }

        private void OnDiceRollCompleted(DiceRollResult result)
        {
            foreach (var groupResult in result.ResultsByGroup)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{groupResult.Key} dice : Sum = {groupResult.Value.Sum()}" });
            }


        }

        private void OpenSettings()
        {
            var settingsScene = _sceneManager.GetScene(GameSceneState.Settings) as SettingsScene;
            if (settingsScene != null) settingsScene.ReturnScene = GameSceneState.TerminalMap;
            _sceneManager.LastInputDevice = InputDevice.Mouse;
            _sceneManager.ChangeScene(GameSceneState.Settings);
        }

        private void ShowWaitDialog()
        {
            if (_waitDialog.IsActive || _worldClockManager.IsInterpolatingTime) return;
            _waitDialog.Show((hours, minutes, seconds) =>
            {
                double totalSeconds = (hours * 3600) + (minutes * 60) + seconds;
                if (totalSeconds > 0)
                {
                    _coreState.CancelExecutingActions();
                    // The real-world duration is proportional to the in-game duration, but capped.
                    float realDuration = Math.Clamp((float)totalSeconds * 0.1f, 0.5f, 5.0f);
                    _worldClockManager.PassTime(totalSeconds, realDuration, ActivityType.Waiting);
                }
            });
        }

        public override void Update(GameTime gameTime)
        {
            if (_loadingScreen.IsActive)
            {
                _loadingScreen.Update(gameTime);
                // The dice system needs to update for the warmup roll to simulate
                _diceRollingSystem.Update(gameTime);
                return; // Block all other updates
            }

            // Handle the input lock state for post-cancellation time passing
            if (_coreState.IsAwaitingTimePass)
            {
                _coreState.TimePassFailsafeTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (!_worldClockManager.IsInterpolatingTime || _coreState.TimePassFailsafeTimer <= 0)
                {
                    _coreState.IsAwaitingTimePass = false;
                }
            }

            var currentKeyboardState = Keyboard.GetState();
            _diceRollingSystem.Update(gameTime);

            var font = ServiceLocator.Get<BitmapFont>();
            _waitDialog.Update(gameTime);
            _encounterDialog.Update(gameTime);
            if (_waitDialog.IsActive || _encounterDialog.IsActive) return;

            // Calculate terminal bounds once for the update frame
            int mapTotalWidth = (int)(Global.VIRTUAL_WIDTH * Global.MAP_AREA_WIDTH_PERCENT);
            int mapX = (Global.VIRTUAL_WIDTH - mapTotalWidth) / 2;
            int mapSize = Math.Min(mapTotalWidth, Global.VIRTUAL_HEIGHT - Global.MAP_TOP_PADDING - Global.TERMINAL_AREA_HEIGHT - 10);
            var mapBounds = new Rectangle(mapX, Global.MAP_TOP_PADDING, mapSize, mapSize);
            int terminalY = mapBounds.Bottom + 5; // Position terminal top flush with map bottom frame
            int terminalHeight = Global.VIRTUAL_HEIGHT - terminalY - 10; // Extend height to bottom padding
            var terminalBounds = new Rectangle(mapBounds.X, terminalY, mapBounds.Width, terminalHeight);

            // The input handler must run every frame to catch the unpause command.
            // It has its own internal logic to halt other inputs when paused.
            _inputHandler.HandleInput(gameTime, terminalBounds);

            // UI elements that should remain interactive while paused are updated here.
            var currentMouseState = Mouse.GetState();
            _settingsButton?.Update(currentMouseState);
            _clockRenderer.Update(gameTime); // Updates the clock's buttons

            // If the game is paused, halt all other scene-specific updates.
            if (_coreState.IsPaused)
            {
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            if (_coreState.IsInCombat)
            {
                // Exit combat with the F5 debug key.
                if (currentKeyboardState.IsKeyDown(Keys.F5) && !_previousKeyboardState.IsKeyDown(Keys.F5))
                {
                    _coreState.EndCombat();
                }
            }
            else
            {
                // Standard out-of-combat updates
                _mapInputHandler.Update(gameTime);
                _mapRenderer.Update(gameTime, font);
                _statsRenderer.Update(gameTime);
            }

            _hapticsManager.Update(gameTime);
            _worldClockManager.Update(gameTime);
            _previousKeyboardState = currentKeyboardState;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_loadingScreen.IsActive)
            {
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _loadingScreen.Draw(spriteBatch, font);
                spriteBatch.End();
                return; // Block all other drawing
            }

            var currentMouseState = Mouse.GetState();

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // --- Draw UI Panels ---
            if (_coreState.IsInCombat)
            {
                // Draw a black screen with "COMBAT" text
                var pixel = ServiceLocator.Get<Texture2D>();
                var screenBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
                spriteBatch.Draw(pixel, screenBounds, Color.Black);

                string combatText = "COMBAT";
                Vector2 textSize = font.MeasureString(combatText);
                Vector2 textPosition = new Vector2(
                    (Global.VIRTUAL_WIDTH - textSize.X) / 2,
                    (Global.VIRTUAL_HEIGHT - textSize.Y) / 2
                );
                spriteBatch.DrawString(font, combatText, textPosition, Color.White);
            }
            else
            {
                // Draw map with default out-of-combat bounds
                _mapRenderer.DrawMap(spriteBatch, font, gameTime);

                // Draw the pre-encounter animation on top of the map
                _preEncounterAnimationSystem.Draw(spriteBatch, font);

                // Calculate terminal bounds based on the map's position and size
                var mapBounds = _mapRenderer.MapScreenBounds;
                int terminalY = mapBounds.Bottom + 5; // Position terminal top flush with map bottom frame
                int terminalHeight = Global.VIRTUAL_HEIGHT - terminalY - 10; // Extend height to bottom padding
                var terminalBounds = new Rectangle(mapBounds.X, terminalY, mapBounds.Width, terminalHeight);

                _terminalRenderer.DrawTerminal(spriteBatch, font, gameTime, terminalBounds);

                // --- Side Column Layout ---
                int leftColumnWidth = mapBounds.X;
                int rightColumnX = mapBounds.Right;
                int rightColumnWidth = Global.VIRTUAL_WIDTH - rightColumnX;

                // Draw Stats in Left Column
                if (leftColumnWidth > 20) // Ensure there's space for padding
                {
                    _statsRenderer.DrawStats(spriteBatch, font, new Vector2(10, mapBounds.Y), leftColumnWidth - 20);
                }

                // Draw Clock and Settings in Right Column
                if (rightColumnWidth > 20)
                {
                    const int padding = 5;
                    // Settings Button in top-right corner
                    if (_settingsButton != null)
                    {
                        int settingsButtonX = Global.VIRTUAL_WIDTH - _settingsButton.Bounds.Width - padding;
                        int settingsButtonY = padding;
                        _settingsButton.Bounds = new Rectangle(settingsButtonX, settingsButtonY, _settingsButton.Bounds.Width, _settingsButton.Bounds.Height);
                        _settingsButton.Draw(spriteBatch, font, gameTime);
                    }

                    // Clock centered in the right column area
                    int clockX = rightColumnX + (rightColumnWidth - _clockRenderer.ClockSize) / 2;
                    int clockY = mapBounds.Y + (mapBounds.Height - _clockRenderer.ClockSize) / 2;
                    _clockRenderer.DrawClock(spriteBatch, font, gameTime, new Vector2(clockX, clockY));
                }

                // Draw Prompt/Status in Bottom-Left
                _promptRenderer.Draw(spriteBatch, font, new Vector2(10, Global.VIRTUAL_HEIGHT - 18));
            }

            spriteBatch.End();

            // Draw autocomplete on top of everything else
            if (!_coreState.IsInCombat && _autoCompleteManager.ShowingAutoCompleteSuggestions)
            {
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _terminalRenderer.DrawAutoComplete(spriteBatch, font);
                spriteBatch.End();
            }

            // Draw dialogs last so they appear on top of all other UI
            _waitDialog.Draw(spriteBatch, font, gameTime);
            _encounterDialog.Draw(spriteBatch, font, gameTime);
        }

        private void DrawHollowRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}
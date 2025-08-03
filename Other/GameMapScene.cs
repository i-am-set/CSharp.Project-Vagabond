using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Dice;
using ProjectVagabond.Encounters;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using System;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class GameMapScene : GameScene
    {
        // Dependencies
        private readonly GameState _gameState;
        private readonly SceneManager _sceneManager;
        private readonly SpriteManager _spriteManager;
        private readonly MapInputHandler _mapInputHandler;
        private readonly MapRenderer _mapRenderer;
        private readonly StatsRenderer _statsRenderer;
        private readonly HapticsManager _hapticsManager;
        private readonly DiceRollingSystem _diceRollingSystem;
        private readonly PreEncounterAnimationSystem _preEncounterAnimationSystem;
        private readonly PlayerInputSystem _playerInputSystem;
        private WaitDialog _waitDialog;
        private EncounterDialog _encounterDialog;
        private ImageButton _settingsButton;
        private readonly LoadingScreen _loadingScreen;

        // State
        private bool _isInitialLoad = true;
        private KeyboardState _previousKeyboardState;

        public GameMapScene()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _mapInputHandler = ServiceLocator.Get<MapInputHandler>();
            _mapRenderer = ServiceLocator.Get<MapRenderer>();
            _statsRenderer = ServiceLocator.Get<StatsRenderer>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _diceRollingSystem = ServiceLocator.Get<DiceRollingSystem>();
            _preEncounterAnimationSystem = ServiceLocator.Get<PreEncounterAnimationSystem>();
            _playerInputSystem = ServiceLocator.Get<PlayerInputSystem>();
            _loadingScreen = new LoadingScreen();
        }

        public override void Enter()
        {
            base.Enter();
            _core.IsMouseVisible = true;
            _waitDialog = new WaitDialog(this);
            _encounterDialog = new EncounterDialog(this);

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
                _settingsButton = new ImageButton(new Rectangle(0, 0, buttonSize, buttonSize), settingsIcon)
                {
                    UseScreenCoordinates = false // Render as part of the virtual scene
                };
                _settingsButton.OnClick += OpenSettings;
            }

            _previousKeyboardState = Keyboard.GetState();
        }

        public override void Exit()
        {
            base.Exit();
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
        }

        public void ShowEncounter(EncounterData encounterData)
        {
            _encounterDialog.Show(encounterData);
        }

        private void OpenSettings()
        {
            var settingsScene = _sceneManager.GetScene(GameSceneState.Settings) as SettingsScene;
            if (settingsScene != null) settingsScene.ReturnScene = GameSceneState.TerminalMap; // This should now be GameMapScene
            _sceneManager.LastInputDevice = InputDevice.Mouse;
            _sceneManager.ChangeScene(GameSceneState.Settings);
        }

        public override void Update(GameTime gameTime)
        {
            if (_loadingScreen.IsActive)
            {
                _loadingScreen.Update(gameTime);
                _diceRollingSystem.Update(gameTime);
                return;
            }

            if (_gameState.IsAwaitingTimePass)
            {
                var worldClock = ServiceLocator.Get<WorldClockManager>();
                _gameState.TimePassFailsafeTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (!worldClock.IsInterpolatingTime || _gameState.TimePassFailsafeTimer <= 0)
                {
                    _gameState.IsAwaitingTimePass = false;
                }
            }

            var currentKeyboardState = Keyboard.GetState();
            _diceRollingSystem.Update(gameTime);

            var font = ServiceLocator.Get<BitmapFont>();
            _waitDialog.Update(gameTime);
            _encounterDialog.Update(gameTime);
            if (_waitDialog.IsActive || _encounterDialog.IsActive || _preEncounterAnimationSystem.IsAnimating)
            {
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            // --- Keyboard Shortcuts for Action Queue ---
            if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
            {
                if (_gameState.PendingActions.Any() && !_gameState.IsExecutingActions)
                {
                    _gameState.ToggleExecutingActions(true);
                }
            }

            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                if (_gameState.IsExecutingActions)
                {
                    _gameState.CancelExecutingActions(false); // false: not an interruption
                }
                else if (_gameState.PendingActions.Any())
                {
                    _playerInputSystem.CancelPendingActions(_gameState);
                }
            }

            var currentMouseState = Mouse.GetState();
            _settingsButton?.Update(currentMouseState);

            if (_gameState.IsPaused)
            {
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            if (_gameState.IsInCombat)
            {
                // Combat logic would go here
            }
            else
            {
                _mapInputHandler.Update(gameTime);
                _mapRenderer.Update(gameTime, font);
                _statsRenderer.Update(gameTime);
            }

            _hapticsManager.Update(gameTime);
            _previousKeyboardState = currentKeyboardState;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_loadingScreen.IsActive)
            {
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _loadingScreen.Draw(spriteBatch, font);
                spriteBatch.End();
                return;
            }

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            if (_gameState.IsInCombat)
            {
                // Combat drawing logic
            }
            else
            {
                _mapRenderer.DrawMap(spriteBatch, font, gameTime);
                _preEncounterAnimationSystem.Draw(spriteBatch, font);

                var mapBounds = _mapRenderer.MapScreenBounds;
                int leftColumnWidth = mapBounds.X;
                if (leftColumnWidth > 20)
                {
                    _statsRenderer.DrawStats(spriteBatch, font, new Vector2(10, mapBounds.Y), leftColumnWidth - 20);
                }
            }

            // Draw the settings button within the main render target so it scales correctly.
            if (_settingsButton != null)
            {
                const int padding = 5;
                int buttonX = Global.VIRTUAL_WIDTH - _settingsButton.Bounds.Width - padding;
                int buttonY = Global.VIRTUAL_HEIGHT - _settingsButton.Bounds.Height - padding;
                _settingsButton.Bounds = new Rectangle(buttonX, buttonY, _settingsButton.Bounds.Width, _settingsButton.Bounds.Height);
                _settingsButton.Draw(spriteBatch, font, gameTime);
            }

            spriteBatch.End();

            _waitDialog.Draw(spriteBatch, font, gameTime);
            _encounterDialog.Draw(spriteBatch, font, gameTime);
        }

        public override void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // The settings button has been moved to the main Draw method to ensure it scales.
            // This method is now empty for this scene.
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Dice;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
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
        private readonly HapticsManager _hapticsManager;
        private readonly DiceRollingSystem _diceRollingSystem;
        private readonly PlayerInputSystem _playerInputSystem;
        private readonly AnimationManager _animationManager;
        private ImageButton _settingsButton;
        private readonly Global _global;

        private MouseState _previousMouseState;

        public GameMapScene()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _mapInputHandler = ServiceLocator.Get<MapInputHandler>();
            _mapRenderer = ServiceLocator.Get<MapRenderer>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _diceRollingSystem = ServiceLocator.Get<DiceRollingSystem>();
            _playerInputSystem = ServiceLocator.Get<PlayerInputSystem>();
            _animationManager = ServiceLocator.Get<AnimationManager>();
            _global = ServiceLocator.Get<Global>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            // The bounds should be the entire map frame.
            // We need to calculate the layout once to get the correct bounds.
            _mapRenderer.Update(new GameTime(), null); // A bit of a hack to force layout calculation
            var mapBounds = _mapRenderer.MapScreenBounds;
            return new Rectangle(mapBounds.X - 5, mapBounds.Y - 5, mapBounds.Width + 10, mapBounds.Height + 10);
        }

        public override void Enter()
        {
            base.Enter();
            _core.IsMouseVisible = true;
            _mapRenderer.ResetHeaderState();

            if (_settingsButton == null)
            {
                var settingsIcon = _spriteManager.SettingsIconSprite;
                var buttonSize = 16;
                if (settingsIcon != null) buttonSize = Math.Max(settingsIcon.Width, settingsIcon.Height);
                _settingsButton = new ImageButton(new Rectangle(0, 0, buttonSize, buttonSize), settingsIcon)
                {
                    UseScreenCoordinates = false // Render as part of the virtual scene
                };
            }
            // The event is unsubscribed in Exit(), so it must be re-subscribed every time the scene is entered.
            _settingsButton.OnClick += OpenSettings;

            // Set the button's position once, as it's static.
            const int padding = 5;
            int buttonX = Global.VIRTUAL_WIDTH - _settingsButton.Bounds.Width - padding;
            int buttonY = Global.VIRTUAL_HEIGHT - _settingsButton.Bounds.Height - padding;
            _settingsButton.Bounds = new Rectangle(buttonX, buttonY, _settingsButton.Bounds.Width, _settingsButton.Bounds.Height);

            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();

            _animationManager.Register("MapBorderSway", _mapRenderer.SwayAnimation);
        }

        public override void Exit()
        {
            base.Exit();
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
            _animationManager.Unregister("MapBorderSway");
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
            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();
            var font = ServiceLocator.Get<BitmapFont>();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            _diceRollingSystem.Update(gameTime);
            _settingsButton?.Update(currentMouseState);

            if (IsInputBlocked)
            {
                base.Update(gameTime);
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

            if (KeyPressed(Keys.Space, currentKeyboardState, _previousKeyboardState))
            {
                _mapRenderer.ResetCamera();
            }

            // --- Handle Map Zoom ---
            if (currentMouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue && _mapRenderer.MapScreenBounds.Contains(virtualMousePos))
            {
                if (currentMouseState.ScrollWheelValue > _previousMouseState.ScrollWheelValue)
                {
                    _mapRenderer.ZoomIn();
                }
                else
                {
                    _mapRenderer.ZoomOut();
                }
            }

            if (_gameState.IsPaused)
            {
                base.Update(gameTime);
                return;
            }

            _mapInputHandler.Update(gameTime);
            _mapRenderer.Update(gameTime, font);

            _hapticsManager.Update(gameTime);

            _previousMouseState = currentMouseState;
            base.Update(gameTime); // This now updates the intro animator and previous input states
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            _mapRenderer.DrawMap(spriteBatch, font, gameTime, transform);

            // Draw the settings button. Its position is now static and set in Enter().
            _settingsButton?.Draw(spriteBatch, font, gameTime, transform);
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
        }

        public override void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // This method is available for UI elements that need to be drawn on top of the entire scene,
            // directly to the backbuffer.
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}
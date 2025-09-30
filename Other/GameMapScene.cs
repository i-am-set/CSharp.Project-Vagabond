using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        private readonly TooltipManager _tooltipManager;
        private ImageButton _settingsButton;
        private readonly Global _global;
        private readonly ProgressionManager _progressionManager;
        private readonly ProgressionNarrator _progressionNarrator;

        private MouseState _previousMouseState;
        private bool _progressionStarted = false;
        private bool _modalWasActiveLastFrame = false;

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
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _global = ServiceLocator.Get<Global>();
            _progressionManager = ServiceLocator.Get<ProgressionManager>();

            var narratorBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            _progressionNarrator = new ProgressionNarrator(narratorBounds);
        }

        public override Rectangle GetAnimatedBounds()
        {
            // The bounds should be the entire map frame.
            _mapRenderer.Update(new GameTime(), null); // A bit of a hack to force layout calculation
            var mapBounds = _mapRenderer.MapScreenBounds;
            return new Rectangle(mapBounds.X - 5, mapBounds.Y - 5, mapBounds.Width + 10, mapBounds.Height + 10);
        }

        public override void Enter()
        {
            base.Enter();
            _core.IsMouseVisible = true;
            _mapRenderer.ResetHeaderState();
            _progressionNarrator.Clear();
            _modalWasActiveLastFrame = false;

            // The ProgressionManager is now the primary driver of the game loop.
            // If it hasn't been started yet, this is the first time we've entered the "game" proper.
            if (!_progressionStarted)
            {
                _progressionManager.StartNewGame();
                _progressionStarted = true;
            }

            if (_settingsButton == null)
            {
                var settingsIcon = _spriteManager.SettingsIconSprite;
                var buttonSize = 16;
                if (settingsIcon != null) buttonSize = Math.Max(settingsIcon.Width, settingsIcon.Height);
                _settingsButton = new ImageButton(new Rectangle(0, 0, buttonSize, buttonSize), settingsIcon)
                {
                    UseScreenCoordinates = true // This button operates in screen space, not virtual space.
                };
            }
            _settingsButton.OnClick += OpenSettings;
            EventBus.Subscribe<GameEvents.ProgressionNarrated>(OnProgressionNarrated);

            _settingsButton.ResetAnimationState();
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _animationManager.Register("MapBorderSway", _mapRenderer.SwayAnimation);
        }

        public override void Exit()
        {
            base.Exit();
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
            EventBus.Unsubscribe<GameEvents.ProgressionNarrated>(OnProgressionNarrated);
            _animationManager.Unregister("MapBorderSway");
        }

        private void OnProgressionNarrated(GameEvents.ProgressionNarrated e)
        {
            if (e.ClearPrevious)
            {
                _progressionNarrator.Clear();
            }
            _progressionNarrator.Show(e.Message, ServiceLocator.Get<Core>().SecondaryFont);
        }

        private void OpenSettings()
        {
            _sceneManager.ShowModal(GameSceneState.Settings);
        }

        public override void Update(GameTime gameTime)
        {
            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();
            var font = ServiceLocator.Get<BitmapFont>();

            bool modalIsActiveThisFrame = _sceneManager.IsModalActive;
            bool modalJustClosed = _modalWasActiveLastFrame && !modalIsActiveThisFrame;

            if (modalJustClosed)
            {
                // A choice was just made. Force the narrator to clear so the game loop can advance.
                _progressionNarrator.Clear();
            }

            _progressionNarrator.Update(gameTime);
            _tooltipManager.Update(gameTime);

            // The game loop is gated by the narrator and modal scenes.
            if (!_progressionNarrator.IsBusy && !modalIsActiveThisFrame)
            {
                // First, check if there's a pending action waiting for the narration to finish.
                if (_progressionManager.OnNarrationCompleteAction != null)
                {
                    var actionToExecute = _progressionManager.OnNarrationCompleteAction;
                    _progressionManager.ClearPendingAction(); // Consume the action
                    actionToExecute.Invoke(); // Execute the scene change, modal pop, etc.
                }
                else
                {
                    // Only if there is no pending action, we ask the manager to process the next event.
                    _progressionManager.AdvanceToNextEvent();
                }
            }


            if (_settingsButton != null)
            {
                float scale = _core.FinalScale;
                int buttonVirtualSize = 16;
                int buttonScreenSize = (int)(buttonVirtualSize * scale);

                var screenBounds = _core.GraphicsDevice.PresentationParameters.Bounds;
                const int padding = 5;

                int buttonX = screenBounds.Width - buttonScreenSize - padding;
                int buttonY = padding;

                _settingsButton.Bounds = new Rectangle(buttonX, buttonY, buttonScreenSize, buttonScreenSize);
            }

            _settingsButton?.Update(currentMouseState);

            if (IsInputBlocked)
            {
                base.Update(gameTime);
                _modalWasActiveLastFrame = modalIsActiveThisFrame;
                return;
            }

            // Other update logic... (input, map renderer etc.) is paused while narrator is busy.
            if (!_progressionNarrator.IsBusy)
            {
                _mapInputHandler.Update(gameTime);
            }
            _mapRenderer.Update(gameTime, font);

            _hapticsManager.Update(gameTime);

            _previousMouseState = currentMouseState;
            _modalWasActiveLastFrame = modalIsActiveThisFrame;
            base.Update(gameTime);
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Draw the base scene content (which is now just a black background)
            base.Draw(spriteBatch, font, gameTime, transform);

            // Now draw the narrator on top, within the same virtual resolution space
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            _progressionNarrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);
            spriteBatch.End();
        }


        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // This method is now intentionally left blank. The Core class clears the render target
            // to black, and the tiled background is no longer desired for this scene.
        }

        public override void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // This method is for UI that needs to be drawn in raw screen coordinates, ignoring camera/scene transforms.
            // We begin a new SpriteBatch without a matrix.
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            if (_settingsButton != null)
            {
                // Bounds are now set in Update, so we just draw.
                _settingsButton.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }

            spriteBatch.End();
        }

        public override void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            _tooltipManager.Draw(spriteBatch, font);
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}

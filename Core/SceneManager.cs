using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Scenes;
using System.Collections.Generic;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond
{
    public enum FadeState
    {
        Idle,
        FadingOut,
        FadingIn
    }

    public class SceneManager
    {
        // Injected Dependencies
        private readonly Global _global;
        private readonly GraphicsDeviceManager _graphics;

        // Scene Management State
        private readonly Dictionary<GameSceneState, GameScene> _scenes = new();
        private GameScene _currentScene;

        // Fade Transition State
        private FadeState _fadeState = FadeState.Idle;
        private float _fadeAlpha = 0.0f;
        private float _fadeDuration = 0.1f;
        private Texture2D _fadeTexture;
        private GameSceneState _nextSceneState;

        /// <summary>
        /// The currently active scene.
        /// </summary>
        public GameScene CurrentActiveScene => _currentScene;

        /// <summary>
        /// The last input device used to trigger a major action, like changing a scene.
        /// </summary>
        public InputDevice LastInputDevice { get; set; } = InputDevice.Mouse;

        public SceneManager()
        {
            // Acquire dependencies from the ServiceLocator
            _global = ServiceLocator.Get<Global>();
            _graphics = ServiceLocator.Get<GraphicsDeviceManager>();
        }

        /// <summary>
        /// Adds a scene to the manager and initializes it.
        /// </summary>
        public void AddScene(GameSceneState state, GameScene scene)
        {
            _scenes[state] = scene;
            scene.Initialize();
        }

        /// <summary>
        /// Retrieves a scene instance from the manager.
        /// </summary>
        /// <param name="state">The state of the scene to retrieve.</param>
        /// <returns>The GameScene instance, or null if not found.</returns>
        public GameScene GetScene(GameSceneState state)
        {
            _scenes.TryGetValue(state, out var scene);
            return scene;
        }

        /// <summary>
        /// Changes the currently active scene, with an optional fade transition.
        /// </summary>
        /// <param name="state">The state of the scene to switch to.</param>
        /// <param name="fade_duration">The duration of the fade transition. If 0, the switch is instant.</param>
        public void ChangeScene(GameSceneState state, float fade_duration = 0.0f)
        {
            if (_fadeState != FadeState.Idle)
            {
                return;
            }

            if (fade_duration > 0.0f)
            {
                _fadeDuration = fade_duration;
                _nextSceneState = state;
                _fadeState = FadeState.FadingOut;
            }
            else
            {
                SwitchToScene(state);
            }
        }

        /// <summary>
        /// Performs the actual scene switch.
        /// </summary>
        private void SwitchToScene(GameSceneState state)
        {
            if (_scenes.TryGetValue(state, out var newScene))
            {
                _currentScene?.Exit();
                _currentScene = newScene;
                _currentScene.LastUsedInputForNav = this.LastInputDevice;
                _currentScene.Enter();
            }
        }

        public void Update(GameTime gameTime)
        {
            if (_fadeState == FadeState.Idle)
            {
                _currentScene?.Update(gameTime);
            }
            else
            {
                UpdateFade(gameTime);
            }
        }

        private void UpdateFade(GameTime gameTime)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_fadeState == FadeState.FadingOut)
            {
                _fadeAlpha += delta / _fadeDuration;
                if (_fadeAlpha >= 1.0f)
                {
                    _fadeAlpha = 1.0f;
                    SwitchToScene(_nextSceneState);
                    _fadeState = FadeState.FadingIn;
                }
            }
            else if (_fadeState == FadeState.FadingIn)
            {
                _fadeAlpha -= delta / _fadeDuration;
                if (_fadeAlpha <= 0.0f)
                {
                    _fadeAlpha = 0.0f;
                    _fadeState = FadeState.Idle;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            _currentScene?.Draw(spriteBatch, font, gameTime);
        }

        public void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            _currentScene?.DrawUnderlay(spriteBatch, font, gameTime);
        }

        public void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            _currentScene?.DrawOverlay(spriteBatch, font, gameTime);

            if (spriteBatch == null)
            {
                return;
            }

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            DrawFade(spriteBatch, _graphics.GraphicsDevice);

            if (font != null)
            {
                string versionText = $"v{Global.GAME_VERSION}";
                float padding = 5f;
                var screenHeight = _graphics.PreferredBackBufferHeight;
                var versionPosition = new Vector2(padding, screenHeight - font.LineHeight - padding);
                spriteBatch.DrawString(font, versionText, versionPosition, _global.Palette_DarkGray);
            }

            spriteBatch.End();
        }

        private void DrawFade(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice)
        {
            if (_fadeAlpha <= 0.0f)
            {
                return;
            }

            if (_fadeTexture == null)
            {
                _fadeTexture = new Texture2D(graphicsDevice, 1, 1);
                _fadeTexture.SetData(new[] { Color.White });
            }

            var screenBounds = graphicsDevice.Viewport.Bounds;
            var fadeColor = _global.Palette_Black * _fadeAlpha;

            spriteBatch.Draw(_fadeTexture, screenBounds, fadeColor);
        }
    }
}
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
        private readonly Dictionary<GameSceneState, GameScene> _scenes = new();
        private GameScene _currentScene;

        private FadeState _fadeState = FadeState.Idle;
        private float _fadeAlpha = 0.0f;
        private float _fadeDuration = 0.1f;
        private Texture2D _fadeTexture;
        private GameSceneState _nextSceneState;

        /// <summary>
        /// The last input device used to trigger a major action, like changing a scene.
        /// </summary>
        public InputDevice LastInputDevice { get; set; } = InputDevice.Mouse;

        /// <summary>
        /// Adds a scene to the manager and initializes it.
        /// </summary>
        public void AddScene(GameSceneState state, GameScene scene)
        {
            _scenes[state] = scene;
            scene.Initialize();
        }

        /// <summary>
        /// Changes the currently active scene, with an optional fade transition.
        /// </summary>
        /// <param name="state">The state of the scene to switch to.</param>
        /// <param name="fade_duration">If true, the screen will fade to black and then fade in to the new scene.</param>
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

        public void Draw(GameTime gameTime)
        {
            _currentScene?.Draw(gameTime);
        }

        public void DrawUnderlay(GameTime gameTime)
        {
            _currentScene?.DrawUnderlay(gameTime);
        }

        public void DrawOverlay(GameTime gameTime)
        {
            _currentScene?.DrawOverlay(gameTime);

            var spriteBatch = Global.Instance.CurrentSpriteBatch;
            var font = Global.Instance.DefaultFont;
            var graphics = Global.Instance.CurrentGraphics;

            if (graphics == null || spriteBatch == null)
            {
                return;
            }

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            DrawFade(spriteBatch, graphics.GraphicsDevice);

            if (font != null)
            {
                string versionText = $"v{Global.GAME_VERSION}";
                float padding = 5f;
                var screenHeight = graphics.PreferredBackBufferHeight;
                var versionPosition = new Vector2(padding, screenHeight - font.LineHeight - padding);
                spriteBatch.DrawString(font, versionText, versionPosition, Global.Instance.Palette_Gray);
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
            var fadeColor = Global.Instance.Palette_Black * _fadeAlpha;

            spriteBatch.Draw(_fadeTexture, screenBounds, fadeColor);
        }
    }
}
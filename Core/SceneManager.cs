﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Scenes;
using System.Collections.Generic;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond
{
    public class SceneManager
    {
        private readonly Dictionary<GameSceneState, GameScene> _scenes = new();
        private GameScene _currentScene;

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
        /// Changes the currently active scene.
        /// </summary>
        public void ChangeScene(GameSceneState state)
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
            _currentScene?.Update(gameTime);
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

            if (font == null || graphics == null || spriteBatch == null)
            {
                return;
            }

            string versionText = $"v{Global.GAME_VERSION}";
            float padding = 5f;

            var screenHeight = graphics.PreferredBackBufferHeight;

            var versionPosition = new Vector2(padding, screenHeight - font.LineHeight - padding);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.DrawString(font, versionText, versionPosition, Global.Instance.Palette_Gray);
            spriteBatch.End();
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class BackgroundNoiseRenderer
    {
        private readonly Global _global;
        private readonly Core _core;
        private Effect _effect;
        private RenderTarget2D _renderTarget;
        private float _time;

        public Texture2D Texture => _renderTarget;

        public BackgroundNoiseRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
        }

        public void LoadContent()
        {
            try
            {
                _effect = _core.Content.Load<Effect>("Shaders/BackgroundNoise");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundNoiseRenderer] Failed to load shader: {ex.Message}");
            }
        }

        public void Update(GameTime gameTime)
        {
            _time += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        /// <summary>
        /// Applies the noise effect to the source texture and renders it to an internal target.
        /// </summary>
        /// <param name="graphicsDevice">The GraphicsDevice.</param>
        /// <param name="sourceTexture">The full-screen composite texture containing the game scene.</param>
        /// <param name="gameTime">Current game time.</param>
        /// <param name="scale">The current game scale factor.</param>
        public void Apply(GraphicsDevice graphicsDevice, Texture2D sourceTexture, GameTime gameTime, float scale)
        {
            if (_effect == null || sourceTexture == null) return;
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            if (spriteManager.NoiseTexture == null) return;

            int width = sourceTexture.Width;
            int height = sourceTexture.Height;

            // Recreate render target if size changed
            if (_renderTarget == null || _renderTarget.Width != width || _renderTarget.Height != height)
            {
                _renderTarget?.Dispose();
                _renderTarget = new RenderTarget2D(graphicsDevice, width, height);
            }

            // Save current render targets
            var bindings = graphicsDevice.GetRenderTargets();

            // Set our target
            graphicsDevice.SetRenderTarget(_renderTarget);
            graphicsDevice.Clear(Color.Transparent);

            // Set shader parameters
            _effect.Parameters["Time"]?.SetValue(_time);
            _effect.Parameters["Resolution"]?.SetValue(new Vector2(width, height));
            _effect.Parameters["VirtualResolution"]?.SetValue(new Vector2(Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT));
            _effect.Parameters["Color1"]?.SetValue(_global.Palette_Black.ToVector4());
            _effect.Parameters["Color2"]?.SetValue(_global.BackgroundNoiseColor.ToVector4());
            _effect.Parameters["PaletteBlack"]?.SetValue(_global.Palette_Black.ToVector4());
            _effect.Parameters["Threshold"]?.SetValue(_global.BackgroundNoiseThreshold);
            _effect.Parameters["Opacity"]?.SetValue(_global.BackgroundNoiseOpacity);
            _effect.Parameters["Scale"]?.SetValue(_global.BackgroundNoiseScale);
            _effect.Parameters["Speed"]?.SetValue(_global.BackgroundScrollSpeedX);
            _effect.Parameters["DistortionScale"]?.SetValue(_global.BackgroundDistortionScale);
            _effect.Parameters["DistortionSpeed"]?.SetValue(_global.BackgroundDistortionSpeed);
            _effect.Parameters["NoiseTexture"]?.SetValue(spriteManager.NoiseTexture);
            _effect.Parameters["SceneTexture"]?.SetValue(sourceTexture);

            // Draw a full-screen quad with the shader
            var spriteBatch = ServiceLocator.Get<SpriteBatch>();
            spriteBatch.Begin(effect: _effect, samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(sourceTexture, new Rectangle(0, 0, width, height), Color.White);
            spriteBatch.End();

            // Restore previous render targets
            graphicsDevice.SetRenderTargets(bindings);
        }
    }
}
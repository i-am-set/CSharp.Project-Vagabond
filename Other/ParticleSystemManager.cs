using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Particles
{
    public class ParticleSystemManager
    {
        private readonly List<ParticleEmitter> _emitters = new List<ParticleEmitter>();
        private readonly Dictionary<BlendState, List<ParticleEmitter>> _renderBatches = new Dictionary<BlendState, List<ParticleEmitter>>();
        private readonly VectorField _vectorField;

        public ParticleSystemManager()
        {
            // Define the bounds and properties of the field.
            // These values can be tuned to change the overall feel of all particle fluid dynamics.
            Rectangle fieldBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            Point gridSize = new Point(40, 23); // A 40x23 grid provides roughly 16x16 pixel cells in a 640x360 resolution.
            _vectorField = new VectorField(fieldBounds, gridSize,
                noiseScale: 0.2f,
                forceMagnitude: 60f,
                timeEvolutionSpeed: 0.3f,
                upwardBias: 0.85f);
        }

        public ParticleEmitter CreateEmitter(ParticleEmitterSettings settings)
        {
            var emitter = new ParticleEmitter(settings);
            _emitters.Add(emitter);
            return emitter;
        }

        public void DestroyEmitter(ParticleEmitter emitter)
        {
            if (emitter != null)
            {
                _emitters.Remove(emitter);
            }
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update the vector field first, so all emitters in this frame use the same state.
            _vectorField.Update(deltaTime);

            for (int i = _emitters.Count - 1; i >= 0; i--)
            {
                // Pass the updated vector field to each emitter.
                _emitters[i].Update(deltaTime, _vectorField);
            }
        }

        public void Draw(SpriteBatch spriteBatch, Matrix? transformMatrix = null)
        {
            // Group emitters by blend state for efficient rendering
            _renderBatches.Clear();
            foreach (var emitter in _emitters)
            {
                if (!_renderBatches.ContainsKey(emitter.Settings.BlendMode))
                {
                    _renderBatches[emitter.Settings.BlendMode] = new List<ParticleEmitter>();
                }
                _renderBatches[emitter.Settings.BlendMode].Add(emitter);
            }

            // Render each batch
            foreach (var batch in _renderBatches)
            {
                // Use BackToFront sorting to respect the LayerDepth of each particle effect.
                spriteBatch.Begin(sortMode: SpriteSortMode.BackToFront, blendState: batch.Key, samplerState: SamplerState.PointClamp, transformMatrix: transformMatrix);
                foreach (var emitter in batch.Value)
                {
                    emitter.Draw(spriteBatch);
                }
                spriteBatch.End();
            }

            // --- DEBUG VISUALIZATION ---
            var global = ServiceLocator.Get<Global>();
            if (global.ShowDebugOverlays)
            {
                spriteBatch.Begin(sortMode: SpriteSortMode.Deferred, blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transformMatrix);
                _vectorField.DebugDraw(spriteBatch);
                spriteBatch.End();
            }
        }
    }
}
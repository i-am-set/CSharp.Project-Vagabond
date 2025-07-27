﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Particles
{
    public class ParticleSystemManager
    {
        private readonly List<ParticleEmitter> _emitters = new List<ParticleEmitter>();
        private readonly Dictionary<BlendState, List<ParticleEmitter>> _renderBatches = new Dictionary<BlendState, List<ParticleEmitter>>();

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
            // Use a for loop for safe removal during iteration if needed in the future
            for (int i = _emitters.Count - 1; i >= 0; i--)
            {
                _emitters[i].Update(deltaTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
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
                spriteBatch.Begin(sortMode: SpriteSortMode.BackToFront, blendState: batch.Key, samplerState: SamplerState.PointClamp);
                foreach (var emitter in batch.Value)
                {
                    emitter.Draw(spriteBatch);
                }
                spriteBatch.End();
            }
        }
    }
}

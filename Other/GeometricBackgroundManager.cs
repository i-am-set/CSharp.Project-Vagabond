using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System.Collections.Generic;

namespace ProjectVagabond.Particles
{
    public class GeometricBackgroundManager
    {
        private readonly ParticleSystemManager _particleSystemManager;
        private readonly TextureFactory _textureFactory;
        private readonly Global _global;

        private Texture2D _texTriangle;
        private Texture2D _texSquare;
        private Texture2D _texHexagon;

        private readonly List<ParticleEmitter> _emitters = new List<ParticleEmitter>();

        public GeometricBackgroundManager()
        {
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _textureFactory = ServiceLocator.Get<TextureFactory>();
            _global = ServiceLocator.Get<Global>();
        }

        private void CreateEmitters()
        {
            if (_texTriangle == null) _texTriangle = _textureFactory.CreatePolygonTexture(24, 3);
            if (_texSquare == null) _texSquare = _textureFactory.CreatePolygonTexture(24, 4);
            if (_texHexagon == null) _texHexagon = _textureFactory.CreatePolygonTexture(24, 6);

            _emitters.Clear();

            // Layer 1: Far (Small, Slow, Dark)
            _emitters.Add(_particleSystemManager.CreateEmitter(ParticleEffects.CreateGeometricBackgroundShape(_texTriangle, 0.5f, 5f, 0.15f, _global)));
            _emitters.Add(_particleSystemManager.CreateEmitter(ParticleEffects.CreateGeometricBackgroundShape(_texSquare, 0.6f, 6f, 0.15f, _global)));
            _emitters.Add(_particleSystemManager.CreateEmitter(ParticleEffects.CreateGeometricBackgroundShape(_texHexagon, 0.7f, 7f, 0.15f, _global)));

            // Layer 2: Mid (Medium, Medium Speed, Medium Alpha)
            _emitters.Add(_particleSystemManager.CreateEmitter(ParticleEffects.CreateGeometricBackgroundShape(_texTriangle, 1.0f, 12f, 0.25f, _global)));
            _emitters.Add(_particleSystemManager.CreateEmitter(ParticleEffects.CreateGeometricBackgroundShape(_texSquare, 1.2f, 14f, 0.25f, _global)));
            _emitters.Add(_particleSystemManager.CreateEmitter(ParticleEffects.CreateGeometricBackgroundShape(_texHexagon, 1.4f, 16f, 0.25f, _global)));

            // Layer 3: Near (Large, Fast, Higher Alpha)
            _emitters.Add(_particleSystemManager.CreateEmitter(ParticleEffects.CreateGeometricBackgroundShape(_texTriangle, 2.0f, 25f, 0.4f, _global)));
            _emitters.Add(_particleSystemManager.CreateEmitter(ParticleEffects.CreateGeometricBackgroundShape(_texSquare, 2.5f, 30f, 0.4f, _global)));
            _emitters.Add(_particleSystemManager.CreateEmitter(ParticleEffects.CreateGeometricBackgroundShape(_texHexagon, 3.0f, 35f, 0.4f, _global)));

            foreach (var e in _emitters)
            {
                e.Position = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);

                // Pre-warm the particles so the screen isn't empty on load
                for (int i = 0; i < 200; i++)
                {
                    e.Update(0.1f, null);
                }
            }
        }

        public void Show(float intensity = 1.0f)
        {
            if (_emitters.Count == 0)
            {
                CreateEmitters();
            }

            foreach (var e in _emitters)
            {
                e.IsActive = true;
                e.EmissionStrength = intensity;
            }
        }

        public void Hide(bool instant = false)
        {
            foreach (var e in _emitters)
            {
                e.IsActive = false;
                if (instant)
                {
                    e.Clear();
                }
            }
        }

        public void Reset()
        {
            _emitters.Clear();
        }
    }
}
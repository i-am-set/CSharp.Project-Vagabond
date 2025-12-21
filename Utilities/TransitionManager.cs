using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Transitions;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public class TransitionManager
    {
        public bool IsTransitioning => _currentState != TransitionState.Idle;
        public bool IsScreenObscured => _currentState == TransitionState.Hold || (_currentState == TransitionState.Out && _currentEffect != null && _currentEffect.IsComplete);

        private TransitionState _currentState = TransitionState.Idle;
        private ITransitionEffect _currentEffect;
        private Action _onMidpoint;
        private Action _onComplete;

        private readonly Dictionary<TransitionType, ITransitionEffect> _effects;
        private readonly Random _random = new Random();

        public TransitionManager()
        {
            _effects = new Dictionary<TransitionType, ITransitionEffect>
            {
                { TransitionType.Fade, new FadeTransition() },
                { TransitionType.Shutters, new ShuttersTransition() },
                { TransitionType.Diamonds, new DiamondWipeTransition() },
                { TransitionType.Blocks, new BlocksWipeTransition() }
            };
        }

        public void StartTransition(TransitionType type, Action onMidpoint, Action onComplete = null)
        {
            if (type == TransitionType.None)
            {
                onMidpoint?.Invoke();
                onComplete?.Invoke();
                return;
            }

            if (!_effects.TryGetValue(type, out _currentEffect))
            {
                _currentEffect = _effects[TransitionType.Fade];
            }

            _onMidpoint = onMidpoint;
            _onComplete = onComplete;

            _currentState = TransitionState.Out;
            _currentEffect.Start(true);
        }

        public TransitionType GetRandomCombatTransition()
        {
            int roll = _random.Next(3);
            if (roll == 0) return TransitionType.Diamonds;
            if (roll == 1) return TransitionType.Shutters;
            return TransitionType.Blocks;
        }

        public void Update(GameTime gameTime)
        {
            if (_currentState == TransitionState.Idle) return;

            if (_currentEffect != null)
            {
                _currentEffect.Update(gameTime);

                if (_currentState == TransitionState.Out)
                {
                    if (_currentEffect.IsComplete)
                    {
                        _currentState = TransitionState.Hold;
                        _onMidpoint?.Invoke();
                        _currentState = TransitionState.In;
                        _currentEffect.Start(false);
                    }
                }
                else if (_currentState == TransitionState.In)
                {
                    if (_currentEffect.IsComplete)
                    {
                        _currentState = TransitionState.Idle;
                        _onComplete?.Invoke();
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_currentState == TransitionState.Idle) return;

            if (_currentState == TransitionState.Hold)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                // Draw black over the entire virtual resolution
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), ServiceLocator.Get<Global>().Palette_Black);
            }
            else if (_currentEffect != null)
            {
                _currentEffect.Draw(spriteBatch);
            }
        }
    }
}
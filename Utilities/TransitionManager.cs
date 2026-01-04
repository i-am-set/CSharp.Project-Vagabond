using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Transitions
{
    public class TransitionManager
    {
        public bool IsTransitioning => _currentState != TransitionState.Idle;
        public bool IsScreenObscured => _currentState == TransitionState.Hold || (_currentState == TransitionState.Out && _currentEffect != null && _currentEffect.IsComplete);

        private TransitionState _currentState = TransitionState.Idle;
        private ITransitionEffect _currentEffect;
        private TransitionType _pendingInType;
        private Action _onMidpoint;
        private Action _onComplete;

        // Delay Timer State
        private float _holdDuration = 0f;
        private float _holdTimer = 0f;
        private bool _midpointExecuted = false;

        private readonly Dictionary<TransitionType, ITransitionEffect> _effects;
        private readonly Random _random = new Random();

        public TransitionManager()
        {
            _effects = new Dictionary<TransitionType, ITransitionEffect>
            {
                { TransitionType.None, new BigBlocksEaseTransition() }, // Fallback
                
                // Standard Transitions
                { TransitionType.Shutter, new ShutterTransition() },
                { TransitionType.Curtain, new CurtainTransition() },
                { TransitionType.Aperture, new ApertureTransition() },
                { TransitionType.Diamonds, new DiamondWipeTransition() },
                { TransitionType.BigBlocksEase, new BigBlocksEaseTransition() },
                
                // Shape Transitions
                { TransitionType.SpinningSquare, new SpinningSquareTransition() },
                { TransitionType.CenterSquare, new CenterSquareTransition() },
                { TransitionType.CenterDiamond, new CenterDiamondTransition() }
            };
        }

        public void Reset()
        {
            _currentState = TransitionState.Idle;
            _currentEffect = null;
            _pendingInType = TransitionType.None;
            _onMidpoint = null;
            _onComplete = null;
            _holdDuration = 0f;
            _holdTimer = 0f;
            _midpointExecuted = false;
        }

        public void StartTransition(TransitionType outType, TransitionType inType, Action onMidpoint, float holdDuration = 0f, Action onComplete = null)
        {
            _pendingInType = inType;
            _onMidpoint = onMidpoint;
            _onComplete = onComplete;
            _holdDuration = holdDuration;
            _holdTimer = 0f;
            _midpointExecuted = false;

            if (outType == TransitionType.None)
            {
                _currentState = TransitionState.Hold;
            }
            else
            {
                if (!_effects.TryGetValue(outType, out _currentEffect))
                {
                    _currentEffect = _effects[TransitionType.BigBlocksEase]; // Default fallback
                }

                _currentState = TransitionState.Out;
                _currentEffect.Start(true);
            }
        }

        private void StartInTransition()
        {
            if (_pendingInType == TransitionType.None)
            {
                _currentState = TransitionState.Idle;
                _onComplete?.Invoke();
            }
            else
            {
                if (!_effects.TryGetValue(_pendingInType, out _currentEffect))
                {
                    _currentEffect = _effects[TransitionType.BigBlocksEase];
                }

                _currentState = TransitionState.In;
                _currentEffect.Start(false);
            }
        }

        public TransitionType GetRandomCombatTransition()
        {
            // Pick from the high-quality transitions
            int roll = _random.Next(5);
            return roll switch
            {
                0 => TransitionType.SpinningSquare,
                1 => TransitionType.Curtain,
                2 => TransitionType.Shutter,
                3 => TransitionType.CenterDiamond,
                4 => TransitionType.CenterSquare,
                5 => TransitionType.Aperture,
                _ => TransitionType.BigBlocksEase
            };
        }

        public void Update(GameTime gameTime)
        {
            if (_currentState == TransitionState.Idle) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_currentState == TransitionState.Out)
            {
                if (_currentEffect != null)
                {
                    _currentEffect.Update(gameTime);
                    if (_currentEffect.IsComplete)
                    {
                        _currentState = TransitionState.Hold;
                        _holdTimer = 0f;
                        _midpointExecuted = false;
                    }
                }
            }
            else if (_currentState == TransitionState.Hold)
            {
                if (!_midpointExecuted)
                {
                    _onMidpoint?.Invoke();
                    _midpointExecuted = true;
                }

                _holdTimer += dt;
                if (_holdTimer >= _holdDuration)
                {
                    StartInTransition();
                }
            }
            else if (_currentState == TransitionState.In)
            {
                if (_currentEffect != null)
                {
                    _currentEffect.Update(gameTime);
                    if (_currentEffect.IsComplete)
                    {
                        _currentState = TransitionState.Idle;
                        _onComplete?.Invoke();
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float scale)
        {
            if (_currentState == TransitionState.Idle) return;

            if (_currentState == TransitionState.Hold)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                spriteBatch.Draw(pixel, bounds, Color.Black);
            }
            else if (_currentEffect != null)
            {
                _currentEffect.Draw(spriteBatch, bounds, scale);
            }
        }
    }
}

    
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
                { TransitionType.Fade, new FadeTransition() },
                { TransitionType.Shutters, new ShuttersTransition() },
                { TransitionType.Diamonds, new DiamondWipeTransition() },
                { TransitionType.Blocks, new BlocksWipeTransition() },
                { TransitionType.BigBlocksEase, new BigBlocksEaseTransition() },
                { TransitionType.Pixels, new PixelWipeTransition()   }
            };
        }

        /// <summary>
        /// Immediately stops any active transition and resets the manager to Idle.
        /// Used during hard resets (e.g. F5) to ensure scene changes are accepted.
        /// </summary>
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

        /// <summary>
        /// Starts a transition sequence.
        /// </summary>
        /// <param name="outType">The effect to cover the screen.</param>
        /// <param name="inType">The effect to reveal the new screen.</param>
        /// <param name="onMidpoint">Action to execute when screen is fully obscured (scene swap).</param>
        /// <param name="holdDuration">Time in seconds to wait while screen is obscured before starting In transition.</param>
        /// <param name="onComplete">Action to execute when transition is fully finished.</param>
        public void StartTransition(TransitionType outType, TransitionType inType, Action onMidpoint, float holdDuration = 0f, Action onComplete = null)
        {
            _pendingInType = inType;
            _onMidpoint = onMidpoint;
            _onComplete = onComplete;
            _holdDuration = holdDuration;
            _holdTimer = 0f;
            _midpointExecuted = false;

            // If Out is None, skip directly to Hold state logic
            if (outType == TransitionType.None)
            {
                _currentState = TransitionState.Hold;
                // We will process the midpoint and delay in the Update loop
            }
            else
            {
                if (!_effects.TryGetValue(outType, out _currentEffect))
                {
                    _currentEffect = _effects[TransitionType.Fade];
                }

                _currentState = TransitionState.Out;
                _currentEffect.Start(true);
            }
        }

        private void StartInTransition()
        {
            // If In is None, we are done immediately
            if (_pendingInType == TransitionType.None)
            {
                _currentState = TransitionState.Idle;
                _onComplete?.Invoke();
            }
            else
            {
                if (!_effects.TryGetValue(_pendingInType, out _currentEffect))
                {
                    _currentEffect = _effects[TransitionType.Fade];
                }

                _currentState = TransitionState.In;
                _currentEffect.Start(false);
            }
        }

        public TransitionType GetRandomCombatTransition()
        {
            int roll = _random.Next(3);
            if (roll == 0) return TransitionType.Diamonds;
            if (roll == 1) return TransitionType.Shutters;
            return TransitionType.BigBlocksEase;
        }

        public void Update(GameTime gameTime)
        {
            if (_currentState == TransitionState.Idle) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // 1. Handle Out Transition
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
            // 2. Handle Hold (Black Screen + Delay)
            else if (_currentState == TransitionState.Hold)
            {
                // Execute the scene swap immediately upon entering Hold
                if (!_midpointExecuted)
                {
                    _onMidpoint?.Invoke();
                    _midpointExecuted = true;
                }

                // Wait for the delay timer
                _holdTimer += dt;
                if (_holdTimer >= _holdDuration)
                {
                    StartInTransition();
                }
            }
            // 3. Handle In Transition
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
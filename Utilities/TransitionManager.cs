using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        // --- NEW: Manual Hold for Loading ---
        /// <summary>
        /// If true, the transition will stay in the 'Hold' (screen covered) state indefinitely
        /// until set to false. Used to keep the screen black while the LoadingScreen runs.
        /// </summary>
        public bool ManualHold { get; set; } = false;

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
            ManualHold = false;
        }

        public void StartTransition(TransitionType outType, TransitionType inType, Action onMidpoint, float holdDuration = 0f, Action onComplete = null)
        {
            _pendingInType = inType;
            _onMidpoint = onMidpoint;
            _onComplete = onComplete;
            _holdDuration = holdDuration;
            _holdTimer = 0f;
            _midpointExecuted = false;
            ManualHold = false; // Reset manual hold, caller must set it immediately after if needed

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

        public TransitionType GetRandomTransition()
        {
            var values = Enum.GetValues(typeof(TransitionType));
            var list = new List<TransitionType>();
            foreach (TransitionType t in values)
            {
                if (t != TransitionType.None) list.Add(t);
            }
            return list[_random.Next(list.Count)];
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

                // --- NEW: Check Manual Hold ---
                // If ManualHold is true (e.g. Loading Screen is active), we do NOT advance the timer.
                // We stay in Hold state until the SceneManager sets ManualHold to false.
                if (!ManualHold)
                {
                    _holdTimer += dt;
                    if (_holdTimer >= _holdDuration)
                    {
                        StartInTransition();
                    }
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

        public void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale)
        {
            if (_currentState == TransitionState.Idle) return;

            if (_currentState == TransitionState.Hold)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                // Draw full screen black using Vector2 scale
                spriteBatch.Draw(pixel, Vector2.Zero, null, Color.Black, 0f, Vector2.Zero, screenSize, SpriteEffects.None, 0f);
            }
            else if (_currentEffect != null)
            {
                _currentEffect.Draw(spriteBatch, screenSize, contentScale);
            }
        }
    }
}
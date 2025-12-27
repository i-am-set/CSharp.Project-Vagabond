using Microsoft.Xna.Framework;
using System;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// Manages the state and timing for the text wave animation.
    /// This abstracts the logic previously found in Button.cs so it can be used on any text.
    /// </summary>
    public class TextWaveController
    {
        // --- Configuration ---
        public float WaveSpeed { get; set; } = 30f;
        public float WaveFrequency { get; set; } = 0.8f;
        public float WaveAmplitude { get; set; } = 1.0f;
        public float WaveRepeatDelay { get; set; } = 0.25f;
        public bool IsEnabled { get; set; } = true;

        // --- State ---
        private float _waveTimer = 0f;
        private float _waveDelayTimer = 0f;
        private bool _isWaveAnimating = false;
        private bool _wasActiveLastFrame = false;

        public float CurrentTimer => _waveTimer;
        public bool IsAnimating => _isWaveAnimating;

        public void Reset()
        {
            _waveTimer = 0f;
            _waveDelayTimer = 0f;
            _isWaveAnimating = false;
            _wasActiveLastFrame = false;
        }

        /// <summary>
        /// Updates the wave timer based on whether the text is currently "active" (e.g. hovered).
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds.</param>
        /// <param name="isActive">Whether the trigger condition (hover) is met.</param>
        /// <param name="textLength">Length of the string to calculate animation duration.</param>
        public void Update(float deltaTime, bool isActive, int textLength)
        {
            if (!IsEnabled)
            {
                Reset();
                return;
            }

            if (!isActive)
            {
                _isWaveAnimating = false;
                _waveTimer = 0f;
                _waveDelayTimer = 0f;
                _wasActiveLastFrame = false;
                return;
            }

            // Trigger animation only on the rising edge of activation
            if (!_wasActiveLastFrame)
            {
                _isWaveAnimating = true;
                _waveTimer = 0f;
                _waveDelayTimer = 0f;
            }

            _wasActiveLastFrame = true;

            if (_isWaveAnimating)
            {
                _waveTimer += deltaTime;

                // Heuristic: (TextLength * Frequency + Pi) / Speed is roughly when the last char finishes the wave
                float estimatedDuration = (textLength * WaveFrequency + MathHelper.Pi) / WaveSpeed + 0.5f;

                if (_waveTimer > estimatedDuration)
                {
                    _isWaveAnimating = false;
                    _waveTimer = 0f;
                    _waveDelayTimer = WaveRepeatDelay; // Start delay before repeating
                }
            }
            else
            {
                // In delay phase
                if (_waveDelayTimer > 0)
                {
                    _waveDelayTimer -= deltaTime;
                    if (_waveDelayTimer <= 0)
                    {
                        // Restart animation
                        _isWaveAnimating = true;
                        _waveTimer = 0f;
                    }
                }
            }
        }
    }
}
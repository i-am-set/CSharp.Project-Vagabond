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
        public float WaveFrequency { get; set; } = 1.0f;
        public float WaveAmplitude { get; set; } = 1.0f;

        // Deprecated but kept for compatibility, effectively ignored in the new seamless loop
        public float WaveRepeatDelay { get; set; } = 0f;

        public bool IsEnabled { get; set; } = true;

        // --- State ---
        private float _waveTimer = 0f;
        private bool _isWaveAnimating = false;

        public float CurrentTimer => _waveTimer;

        // Returns true if the animation logic is active. 
        // Now stays true continuously while hovered to prevent rendering artifacts.
        public bool IsAnimating => _isWaveAnimating;

        public void Reset()
        {
            _waveTimer = 0f;
            _isWaveAnimating = false;
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
                return;
            }

            // If active, we are always animating to ensure consistent text rendering
            _isWaveAnimating = true;
            _waveTimer += deltaTime;

            // Calculate the duration of one full pass of the wave
            // Formula: (TextLength * Frequency + Pi) / Speed
            // This calculates the exact time it takes for the sine wave (0 to Pi) to traverse the entire string length
            float loopDuration = (textLength * WaveFrequency + MathHelper.Pi) / WaveSpeed;

            // Seamless loop: If we exceed the duration, wrap around.
            // We add a tiny buffer (0.1s) just to ensure the tail has fully cleared before restarting, 
            // though mathematically the formula covers it.
            if (_waveTimer > loopDuration + 0.1f)
            {
                _waveTimer = 0f;
            }
        }
    }
}
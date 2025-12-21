using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A centralized controller for the specific "Jump then Bounce" animation used by 
    /// party members and enemies. Encapsulates the math to ensure uniformity across the game.
    /// </summary>
    public class SpriteHopAnimationController
    {
        // --- Tuning Constants (Centralized) ---
        private const float DURATION = 0.35f;

        // Separate heights for players (hop up) and enemies (lunge down)
        private const float PLAYER_HOP_HEIGHT = 6f;
        private const float ENEMY_LUNGE_HEIGHT = 16f; // Increased significantly for visibility

        private float _timer = 0f;
        public bool IsActive { get; private set; } = false;

        public void Trigger()
        {
            _timer = 0f;
            IsActive = true;
        }

        public void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_timer >= DURATION)
            {
                IsActive = false;
                _timer = 0f;
            }
        }

        /// <summary>
        /// Calculates the current Y offset.
        /// </summary>
        /// <param name="invert">If true (Player), the hop goes UP (negative Y). If false (Enemy), it goes DOWN (positive Y).</param>
        /// <returns>The pixel offset to apply to the sprite.</returns>
        public float GetOffset(bool invert)
        {
            if (!IsActive) return 0f;

            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);
            float bobValue = 0f;

            // Phase 1: Main Jump (0% to 60% of duration)
            if (progress < 0.6f)
            {
                float p = progress / 0.6f;
                bobValue = MathF.Sin(p * MathHelper.Pi);
            }
            // Phase 2: The Bounce (60% to 100% of duration)
            else
            {
                float p = (progress - 0.6f) / 0.4f;
                bobValue = MathF.Sin(p * MathHelper.Pi) * 0.3f; // 30% height bounce
            }

            // Determine magnitude and direction based on who is acting
            float height = invert ? PLAYER_HOP_HEIGHT : ENEMY_LUNGE_HEIGHT;
            float direction = invert ? -1f : 1f;

            return bobValue * height * direction;
        }
    }
}
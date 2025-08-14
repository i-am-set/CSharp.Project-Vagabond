using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// A self-contained visual effect for displaying damage or healing numbers.
    /// </summary>
    public class HitMarker
    {
        public string Text { get; }
        public Color Color { get; }
        public float Lifetime { get; set; }
        public Vector2 PositionOffset { get; set; }
        public float Alpha { get; set; }
        public float Scale { get; set; }

        public HitMarker(int amount)
        {
            if (amount >= 0) // Healing
            {
                Text = $"+{amount}";
                Color = Color.LawnGreen;
            }
            else // Damage
            {
                Text = $"{Math.Abs(amount)}";
                Color = Color.White;
            }
            Alpha = 1f;
            Scale = 1f;
        }
    }

    /// <summary>
    /// Represents a single entity (player or enemy) within the combat scene.
    /// It manages its own visual state, such as position, scale, and targeting indicators.
    /// </summary>
    public class CombatEntity
    {
        public int EntityId { get; }
        public Texture2D Texture { get; }

        public Vector2 Position { get; private set; }
        public Rectangle Bounds { get; private set; }

        public bool IsTargeted { get; set; }

        // --- TUNING CONSTANTS ---
        private const float HIT_MARKER_LIFETIME = 0.75f;
        private const float HIT_MARKER_FLOAT_SPEED = 30f;

        private readonly HealthComponent _healthComponent;
        private readonly List<HitMarker> _hitMarkers = new List<HitMarker>();

        public CombatEntity(int entityId, Texture2D texture)
        {
            EntityId = entityId;
            Texture = texture;

            _healthComponent = ServiceLocator.Get<ComponentStore>().GetComponent<HealthComponent>(entityId);
            if (_healthComponent != null)
            {
                _healthComponent.OnHealthChanged += HandleHealthChange;
            }
        }

        /// <summary>
        /// Unsubscribes from events to prevent memory leaks when combat ends.
        /// </summary>
        public void UnsubscribeEvents()
        {
            if (_healthComponent != null)
            {
                _healthComponent.OnHealthChanged -= HandleHealthChange;
            }
        }

        private void HandleHealthChange(int amount)
        {
            var marker = new HitMarker(amount);
            marker.Lifetime = HIT_MARKER_LIFETIME;
            _hitMarkers.Add(marker);
        }

        /// <summary>
        /// Sets the entity's position and size, and recalculates its bounds.
        /// This is typically called by a layout manager in the CombatScene.
        /// </summary>
        public void SetLayout(Vector2 position, Point size)
        {
            Position = position;
            Bounds = new Rectangle(
                (int)(position.X - size.X / 2f),
                (int)(position.Y - size.Y / 2f),
                size.X,
                size.Y
            );
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update hit markers
            for (int i = _hitMarkers.Count - 1; i >= 0; i--)
            {
                var marker = _hitMarkers[i];
                marker.Lifetime -= deltaTime;

                if (marker.Lifetime <= 0)
                {
                    _hitMarkers.RemoveAt(i);
                }
                else
                {
                    // Animate position and fade
                    marker.PositionOffset -= new Vector2(0, HIT_MARKER_FLOAT_SPEED * deltaTime);
                    float progress = marker.Lifetime / HIT_MARKER_LIFETIME;
                    marker.Alpha = Math.Clamp(progress * 2f, 0f, 1f); // Fade out in the last half of its life
                    marker.Scale = 1f + (1f - progress); // "Pop" effect
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (Texture == null) return;

            spriteBatch.Draw(Texture, Bounds, Color.White);

            // Draw Health Bar for enemies
            var gameState = ServiceLocator.Get<GameState>();
            if (EntityId != gameState.PlayerEntityId)
            {
                DrawHealthBar(spriteBatch);
            }
        }

        private void DrawHealthBar(SpriteBatch spriteBatch)
        {
            if (_healthComponent == null || _healthComponent.CurrentHealth <= 0) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            float healthPercent = (float)_healthComponent.CurrentHealth / _healthComponent.MaxHealth;

            int barWidth = (int)(Bounds.Width * 0.8f);
            int barHeight = 8;
            int barY = Bounds.Bottom + 5;
            int barX = Bounds.Center.X - barWidth / 2;

            var bgRect = new Rectangle(barX, barY, barWidth, barHeight);
            var fillRect = new Rectangle(barX, barY, (int)(barWidth * healthPercent), barHeight);

            spriteBatch.Draw(pixel, bgRect, Color.DarkRed);
            spriteBatch.Draw(pixel, fillRect, Color.Red);
        }

        public void DrawHitMarkers(SpriteBatch spriteBatch, BitmapFont font, Vector2 basePosition)
        {
            foreach (var marker in _hitMarkers)
            {
                Vector2 textSize = font.MeasureString(marker.Text);
                Vector2 drawPosition = basePosition + marker.PositionOffset - (textSize / 2f * marker.Scale);

                spriteBatch.DrawString(font, marker.Text, drawPosition, marker.Color * marker.Alpha, 0f, Vector2.Zero, marker.Scale, SpriteEffects.None, 0f);
            }
        }
    }
}
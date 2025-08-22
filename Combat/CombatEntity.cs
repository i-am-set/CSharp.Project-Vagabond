﻿using Microsoft.Xna.Framework;
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
        public bool IsDefeated { get; private set; }

        // --- VISUAL STATE & TWEENING ---
        public Vector2 VisualOffset { get; private set; }
        public float VisualScale { get; private set; }
        public Color VisualTint { get; private set; }

        private Vector2 _startOffset, _targetOffset;
        private float _startScale, _targetScale;
        private Color _startTint, _targetTint;
        private float _animationTimer;
        private bool _isAnimating;
        private const float ANIMATION_DURATION = 0.2f;

        // --- TUNING CONSTANTS ---
        private const float HIT_MARKER_LIFETIME = 0.75f;
        private const float HIT_MARKER_FLOAT_SPEED = 30f;
        private static readonly Vector2 INACTIVE_OFFSET = new Vector2(0, -10);
        private const float INACTIVE_SCALE = 0.9f;
        private static readonly Color INACTIVE_TINT = Color.Gray;
        private static readonly Color DEFEATED_TINT = new Color(100, 20, 20); // Dim red for defeated state


        private readonly HealthComponent _healthComponent;
        private readonly List<HitMarker> _hitMarkers = new List<HitMarker>();

        public CombatEntity(int entityId, Texture2D texture)
        {
            EntityId = entityId;
            Texture = texture;
            VisualScale = 1.0f;
            VisualTint = Color.White;
            VisualOffset = Vector2.Zero;
            IsDefeated = false;

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

            if (_healthComponent.CurrentHealth <= 0)
            {
                IsDefeated = true;
            }
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

            // Update visual state tweening
            if (_isAnimating)
            {
                _animationTimer += deltaTime;
                float progress = Math.Clamp(_animationTimer / ANIMATION_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutCubic(progress);

                VisualOffset = Vector2.Lerp(_startOffset, _targetOffset, easedProgress);
                VisualScale = MathHelper.Lerp(_startScale, _targetScale, easedProgress);
                VisualTint = Color.Lerp(_startTint, _targetTint, easedProgress);

                if (progress >= 1f)
                {
                    _isAnimating = false;
                }
            }

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

            // If the entity is defeated, it uses a special, static visual state.
            if (IsDefeated)
            {
                spriteBatch.Draw(Texture, Bounds, DEFEATED_TINT);
                return;
            }

            // Otherwise, use the dynamic visual state properties for drawing.
            Rectangle destinationRect = new Rectangle(
                (int)(Bounds.X + VisualOffset.X),
                (int)(Bounds.Y + VisualOffset.Y),
                (int)(Bounds.Width * VisualScale),
                (int)(Bounds.Height * VisualScale)
            );

            spriteBatch.Draw(Texture, destinationRect, null, VisualTint, 0f, Vector2.Zero, SpriteEffects.None, 0f);
        }

        public void SetActiveVisuals()
        {
            AnimateTo(Vector2.Zero, 1.0f, Color.White);
        }

        public void SetInactiveVisuals()
        {
            AnimateTo(INACTIVE_OFFSET, INACTIVE_SCALE, INACTIVE_TINT);
        }

        private void AnimateTo(Vector2 targetOffset, float targetScale, Color targetTint)
        {
            _startOffset = VisualOffset;
            _startScale = VisualScale;
            _startTint = VisualTint;
            _targetOffset = targetOffset;
            _targetScale = targetScale;
            _targetTint = targetTint;
            _animationTimer = 0f;
            _isAnimating = true;
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

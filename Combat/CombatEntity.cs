using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond.Combat
{
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

        // --- NEW: Hit Effect State ---
        private float _shakeTimer;
        private int _flashFrames; // How many frames the flash should last
        private readonly Random _random = new Random();

        // --- TUNING CONSTANTS ---
        private static readonly Vector2 INACTIVE_OFFSET = new Vector2(0, -10);
        private const float INACTIVE_SCALE = 0.9f;
        private static readonly Color INACTIVE_TINT = Color.Gray;
        private static readonly Color DEFEATED_TINT = new Color(100, 20, 20);
        private const float HIT_SHAKE_DURATION = 0.3f;
        private const float HIT_SHAKE_MAGNITUDE = 5.0f;
        private static readonly Color HIT_FLASH_COLOR = Color.White;


        private readonly HealthComponent _healthComponent;
        private readonly RenderableComponent _renderableComponent;

        public CombatEntity(int entityId, Texture2D texture)
        {
            EntityId = entityId;
            Texture = texture;
            VisualScale = 1.0f;
            VisualTint = Color.White;
            VisualOffset = Vector2.Zero;
            IsDefeated = false;

            var componentStore = ServiceLocator.Get<ComponentStore>();
            _healthComponent = componentStore.GetComponent<HealthComponent>(entityId);
            _renderableComponent = componentStore.GetComponent<RenderableComponent>(entityId);

            if (_healthComponent != null)
            {
                _healthComponent.OnHealthChanged += HandleHealthChange;
            }

            bool isFallback = texture?.Width == 1 && texture?.Height == 1;
            Debug.WriteLine($"[CombatEntity] [DIAGNOSTIC] (ID: {entityId}): Initialized. Final texture is '{(texture?.Name ?? "null")}' [{texture?.Width ?? 0}x{texture?.Height ?? 0}]. Is fallback pixel: {isFallback}.");
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
            // Only trigger effects on taking damage.
            if (amount < 0)
            {
                TriggerHitEffects();
            }

            if (_healthComponent.CurrentHealth <= 0)
            {
                IsDefeated = true;
            }
        }

        /// <summary>
        /// Activates the shake and flash effects for one hit.
        /// </summary>
        public void TriggerHitEffects()
        {
            _shakeTimer = HIT_SHAKE_DURATION;
            _flashFrames = 2; // Flash for two frames
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

            // Update shake effect timer
            if (_shakeTimer > 0)
            {
                _shakeTimer -= deltaTime;
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (Texture == null) return;

            // Calculate shake offset
            Vector2 shakeOffset = Vector2.Zero;
            if (_shakeTimer > 0)
            {
                // Ease out the magnitude over the duration of the shake.
                float progress = _shakeTimer / HIT_SHAKE_DURATION;
                float currentMagnitude = HIT_SHAKE_MAGNITUDE * Easing.EaseOutCubic(progress);
                shakeOffset.X = (float)(_random.NextDouble() * 2 - 1) * currentMagnitude;
            }

            // Determine base color from the RenderableComponent, or default to white.
            Color baseColor = _renderableComponent?.Color ?? Color.White;

            // Combine the base color with the animated visual tint.
            Color combinedTint = new Color(
                (byte)(baseColor.R * VisualTint.R / 255),
                (byte)(baseColor.G * VisualTint.G / 255),
                (byte)(baseColor.B * VisualTint.B / 255),
                (byte)(baseColor.A * VisualTint.A / 255)
            );

            // Determine final tint (flash takes priority over everything)
            Color finalTint = combinedTint;
            if (_flashFrames > 0)
            {
                finalTint = HIT_FLASH_COLOR;
                _flashFrames--; // Decrement the flash frame counter
            }

            // If the entity is defeated, it uses a special, static visual state.
            if (IsDefeated)
            {
                spriteBatch.DrawSnapped(Texture, Bounds, DEFEATED_TINT);
                return;
            }

            // Otherwise, use the dynamic visual state properties for drawing.
            var destinationRect = new Rectangle(
                (int)(Bounds.X + VisualOffset.X + shakeOffset.X), // Apply shake
                (int)(Bounds.Y + VisualOffset.Y + shakeOffset.Y), // Apply shake
                (int)(Bounds.Width * VisualScale),
                (int)(Bounds.Height * VisualScale)
            );

            spriteBatch.DrawSnapped(Texture, destinationRect, null, finalTint);
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
    }
}
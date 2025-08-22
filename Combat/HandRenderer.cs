﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Combat.UI
{
    #region Aseprite JSON Data Structures
    // These classes are minimal DTOs (Data Transfer Objects) for deserializing Aseprite's JSON output.
    // They are internal to the HandRenderer as they are an implementation detail of its content loading.

    internal class AsepriteRect
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("w")] public int W { get; set; }
        [JsonPropertyName("h")] public int H { get; set; }
    }

    internal class AsepriteFrame
    {
        [JsonPropertyName("frame")] public AsepriteRect Frame { get; set; }
        [JsonPropertyName("duration")] public int Duration { get; set; }
    }

    internal class AsepriteFrameTag
    {
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("from")] public int From { get; set; }
        [JsonPropertyName("to")] public int To { get; set; }
        [JsonPropertyName("direction")] public string Direction { get; set; }
    }

    internal class AsepriteMeta
    {
        [JsonPropertyName("frameTags")] public List<AsepriteFrameTag> FrameTags { get; set; }
    }

    internal class AsepriteData
    {
        [JsonPropertyName("frames")] public List<AsepriteFrame> Frames { get; set; }
        [JsonPropertyName("meta")] public AsepriteMeta Meta { get; set; }
    }

    #endregion

    /// <summary>
    /// Responsible for rendering a single player hand in the combat scene.
    /// It uses a data-driven custom animator to handle complex animations, layered
    /// with a procedural sway for a dynamic, life-like effect.
    /// </summary>
    public class HandRenderer
    {
        private readonly HandType _handType;
        private Vector2 _initialPosition;
        private Vector2 _offscreenPosition;
        private readonly Random _random = new Random();

        // --- TUNING CONSTANTS ---
        private const float IDLE_SWAY_SPEED_X = 0.8f;
        private const float IDLE_SWAY_SPEED_Y = 0.6f;
        private const float IDLE_SWAY_AMOUNT = 1.5f;

        // Sprite Animation
        private SimpleAnimator _animator;

        // Transform State (Position, Rotation, Scale)
        private Vector2 _currentPosition;
        private float _currentRotation;
        private float _currentScale;

        // Tweening State for Position
        private bool _isMoving;
        private Vector2 _startPosition;
        private Vector2 _targetPosition;
        private float _moveTimer;
        private float _moveDuration;
        private Func<float, float> _moveEasing;

        // Tweening State for Rotation
        private bool _isRotating;
        private float _startRotation;
        private float _targetRotation;
        private float _rotateTimer;
        private float _rotateDuration;
        private Func<float, float> _rotateEasing;

        // Tweening State for Scale
        private bool _isScaling;
        private float _startScale;
        private float _targetScale;
        private float _scaleTimer;
        private float _scaleDuration;
        private Func<float, float> _scaleEasing;

        public OrganicSwayAnimation SwayAnimation { get; }

        public HandRenderer(HandType handType, Vector2 initialPosition)
        {
            _handType = handType;
            _initialPosition = initialPosition;
            _offscreenPosition = initialPosition;
            _currentPosition = initialPosition;
            _currentRotation = 0f;
            _currentScale = 1f;
            SwayAnimation = new OrganicSwayAnimation(IDLE_SWAY_SPEED_X, IDLE_SWAY_SPEED_Y, IDLE_SWAY_AMOUNT, IDLE_SWAY_AMOUNT);
        }

        /// <summary>
        /// Sets the hand's base idle position. Called by the CombatScene during layout calculations.
        /// </summary>
        public void SetIdlePosition(Vector2 position)
        {
            _initialPosition = position;
        }

        /// <summary>
        /// Sets the hand's off-screen starting/ending position.
        /// </summary>
        public void SetOffscreenPosition(Vector2 position)
        {
            _offscreenPosition = position;
        }

        public void LoadContent()
        {
            var core = ServiceLocator.Get<Core>();
            string handTypeString = _handType == HandType.Left ? "left" : "right";
            string assetName = $"Sprites/Hands/cat_hand_{handTypeString}";
            string jsonPath = Path.Combine(core.Content.RootDirectory, $"{assetName}.json");

            try
            {
                var texture = core.Content.Load<Texture2D>(assetName);
                var jsonContent = File.ReadAllText(jsonPath);
                var asepriteData = JsonSerializer.Deserialize<AsepriteData>(jsonContent);

                var cycles = new Dictionary<string, AnimationCycle>();
                foreach (var tag in asepriteData.Meta.FrameTags)
                {
                    var frames = new List<AnimationFrame>();
                    for (int i = tag.From; i <= tag.To; i++)
                    {
                        var frameData = asepriteData.Frames[i];
                        frames.Add(new AnimationFrame(
                            new Rectangle(frameData.Frame.X, frameData.Frame.Y, frameData.Frame.W, frameData.Frame.H),
                            frameData.Duration / 1000.0f
                        ));
                    }
                    bool isLooping = tag.Direction.Equals("forward", StringComparison.OrdinalIgnoreCase);
                    cycles[tag.Name] = new AnimationCycle(tag.Name, isLooping, frames.ToArray());
                }

                var spriteSheet = new SimpleSpriteSheet(texture, cycles);
                _animator = new SimpleAnimator(spriteSheet);
                _animator.Play("idle_loop");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Could not load and parse hand spritesheet from '{assetName}.png' and '{jsonPath}': {ex.Message}");
                _animator = null;
            }
        }

        public void EnterScene()
        {
            _currentPosition = _offscreenPosition;
            _currentRotation = 0f;
            _currentScale = 1f;
            _isMoving = _isRotating = _isScaling = false;
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update Position Tween
            if (_isMoving)
            {
                _moveTimer += deltaTime;
                float progress = Math.Clamp(_moveTimer / _moveDuration, 0f, 1f);
                _currentPosition = Vector2.Lerp(_startPosition, _targetPosition, _moveEasing(progress));
                if (progress >= 1f) _isMoving = false;
            }

            // Update Rotation Tween
            if (_isRotating)
            {
                _rotateTimer += deltaTime;
                float progress = Math.Clamp(_rotateTimer / _rotateDuration, 0f, 1f);
                _currentRotation = MathHelper.Lerp(_startRotation, _targetRotation, _rotateEasing(progress));
                if (progress >= 1f) _isRotating = false;
            }

            // Update Scale Tween
            if (_isScaling)
            {
                _scaleTimer += deltaTime;
                float progress = Math.Clamp(_scaleTimer / _scaleDuration, 0f, 1f);
                _currentScale = MathHelper.Lerp(_startScale, _targetScale, _scaleEasing(progress));
                if (progress >= 1f) _isScaling = false;
            }

            SwayAnimation.Update(gameTime);
            _animator?.Update(gameTime);
        }

        public void PlayAnimation(string animationName)
        {
            if (_animator != null && _animator.HasAnimation(animationName))
            {
                _animator.Play(animationName);
            }
            else
            {
                Debug.WriteLine($"[HandRenderer] [WARNING] Requested animation '{animationName}' not found. Playing fallback.");
                if (_animator != null && _animator.HasAnimation("hold"))
                {
                    _animator.Play("hold");
                }
            }
        }

        public void MoveTo(Vector2 targetPosition, float duration, Func<float, float> easing)
        {
            _startPosition = _currentPosition;
            _targetPosition = targetPosition;
            _moveDuration = duration;
            _moveEasing = easing;
            _moveTimer = 0f;
            _isMoving = true;
        }

        public void RotateTo(float targetRotation, float duration, Func<float, float> easing)
        {
            _startRotation = _currentRotation;
            _targetRotation = targetRotation;
            _rotateDuration = duration;
            _rotateEasing = easing;
            _rotateTimer = 0f;
            _isRotating = true;
        }

        public void ScaleTo(float targetScale, float duration, Func<float, float> easing)
        {
            _startScale = _currentScale;
            _targetScale = targetScale;
            _scaleDuration = duration;
            _scaleEasing = easing;
            _scaleTimer = 0f;
            _isScaling = true;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_animator == null) return;

            Vector2 finalPosition = _currentPosition;
            if (!_isMoving)
            {
                finalPosition += SwayAnimation.Offset;
            }

            _animator.Draw(spriteBatch, finalPosition, Color.White, _currentRotation, _currentScale);
        }
    }
}

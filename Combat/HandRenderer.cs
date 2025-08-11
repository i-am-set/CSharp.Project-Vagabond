using Microsoft.Xna.Framework;
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
        /// <summary>
        /// The internal state of the hand's animation controller.
        /// </summary>
        private enum HandAnimationState
        {
            IdleLoop,
            IdleTwitch,
            Holding
        }

        private readonly PlayerHand _playerHand;
        private readonly SpriteManager _spriteManager;
        private readonly Random _random = new Random();

        // --- TUNING CONSTANTS ---
        private const int HAND_WIDTH = 128;
        private const int HAND_HEIGHT = 256;
        private const int IDLE_POS_Y_OFFSET = 10; // Vertical offset from the bottom of the screen
        private const int SELECTED_Y_OFFSET = 20; // How far the hand moves down when an action is selected
        private const int INVALID_DROP_Y_OFFSET = 40; // How far the hand moves down when it's an invalid drop target
        private const int IDLE_POS_X_OFFSET = 180; // Horizontal offset from the center
        private const float SLIDE_ANIMATION_DURATION = 0.6f; // Duration for sliding in/out
        private const float FOCUS_ANIMATION_DURATION = 0.5f; // Duration for focus movement
        private const float IDLE_SWAY_SPEED_X = 0.8f;
        private const float IDLE_SWAY_SPEED_Y = 0.6f;
        private const float IDLE_SWAY_AMOUNT = 1.5f;
        private const float HOVER_Y_LIFT = -10f; // How far the hand moves up when it's a drop target
        private const float PULSE_SPEED = 8f;
        private const float MIN_IDLE_TWITCH_DELAY_SECONDS = 2.0f; // Minimum time before a random idle animation plays.
        private const float MAX_IDLE_TWITCH_DELAY_SECONDS = 6.0f; // Maximum time before a random idle animation plays.

        // Animation state
        private SimpleAnimator _animator;
        private List<string> _idleTwitchAnimations = new List<string>();
        private float _idleTwitchTimer;
        private HandAnimationState _currentState = HandAnimationState.IdleLoop;

        private Vector2 _idlePosition;
        private Vector2 _offscreenPosition;

        private Vector2 _currentPosition;
        private Vector2 _targetPosition;
        private Vector2 _startPosition;
        private float _animationTimer;
        private float _currentAnimationDuration;
        private bool _isAnimating;
        public OrganicSwayAnimation SwayAnimation { get; }
        private float _pulseTimer = 0f;

        /// <summary>
        /// When true, the hand will render a highlight to indicate it's a valid drop target.
        /// </summary>
        public bool IsPotentialDropTarget { get; set; }

        /// <summary>
        /// When true, the hand will animate downwards to indicate it's an invalid drop target.
        /// </summary>
        public bool IsInvalidDropTarget { get; set; }

        /// <summary>
        /// The current screen bounds of the hand renderer.
        /// </summary>
        public Rectangle Bounds => new Rectangle((int)_currentPosition.X, (int)_currentPosition.Y, HAND_WIDTH, HAND_HEIGHT);

        /// <summary>
        /// The calculated visual center of the hand, useful for animation targets.
        /// </summary>
        public Vector2 VisualCenter
        {
            get
            {
                // The visual center is where the hand "holds" things.
                // This is an estimate based on the sprite's appearance.
                float visualCenterY = Bounds.Y + Bounds.Height * 0.4f;
                return new Vector2(Bounds.Center.X, visualCenterY);
            }
        }


        public HandRenderer(PlayerHand playerHand)
        {
            _playerHand = playerHand;
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            SwayAnimation = new OrganicSwayAnimation(IDLE_SWAY_SPEED_X, IDLE_SWAY_SPEED_Y, IDLE_SWAY_AMOUNT, IDLE_SWAY_AMOUNT);
        }

        public void LoadContent()
        {
            var core = ServiceLocator.Get<Core>();
            var textureFactory = ServiceLocator.Get<TextureFactory>();
            string handTypeString = _playerHand.Hand == HandType.Left ? "left" : "right";
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
                            frameData.Duration / 1000.0f // Convert ms to seconds
                        ));
                    }

                    // An animation is looping if its direction is "forward" in Aseprite,
                    // UNLESS it's an idle twitch animation, which we always force to be a one-shot.
                    bool isAsepriteLooping = tag.Direction.Equals("forward", StringComparison.OrdinalIgnoreCase);
                    bool isIdleTwitch = tag.Name.StartsWith("idle_", StringComparison.OrdinalIgnoreCase) && !tag.Name.Equals("idle_loop", StringComparison.OrdinalIgnoreCase);
                    bool isLooping = isAsepriteLooping && !isIdleTwitch;

                    cycles[tag.Name] = new AnimationCycle(tag.Name, isLooping, frames.ToArray());
                }

                var spriteSheet = new SimpleSpriteSheet(texture, cycles);
                _animator = new SimpleAnimator(spriteSheet);

                // Discover all available "twitch" animations defined in the spritesheet.
                _idleTwitchAnimations = cycles.Keys
                    .Where(key => key.StartsWith("idle_", StringComparison.OrdinalIgnoreCase) && !key.Equals("idle_loop", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Start the default animation loop.
                _animator.Play("idle_loop");
                _currentState = HandAnimationState.IdleLoop;
                ResetIdleTwitchTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Could not load and parse hand spritesheet from '{assetName}.png' and '{jsonPath}': {ex.Message}");
                _animator = null; // Ensure animator is null on failure
            }
        }

        /// <summary>
        /// Called when the combat scene is entered to trigger the intro animation.
        /// </summary>
        public void EnterScene()
        {
            // Set a temporary, invalid offscreen position. The first Update call will calculate the correct positions
            // and trigger the animation from the correct offscreen location.
            _currentPosition = new Vector2(0, Global.VIRTUAL_HEIGHT * 2);
            _targetPosition = _currentPosition;
            _isAnimating = false;
        }

        private void StartAnimation(Vector2 newTarget, float duration)
        {
            if (_targetPosition != newTarget)
            {
                _startPosition = _currentPosition;
                _targetPosition = newTarget;
                _animationTimer = 0f;
                _isAnimating = true;
                _currentAnimationDuration = duration;
            }
        }

        /// <summary>
        /// Updates the hand's animation state.
        /// </summary>
        public void Update(GameTime gameTime, CombatManager combatManager, CombatInputHandler inputHandler)
        {
            // --- Layout Calculation (every frame for robustness against resolution changes) ---
            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();
            float windowBottomVirtualY = actualScreenVirtualBounds.Bottom;

            float yPos = windowBottomVirtualY - HAND_HEIGHT + IDLE_POS_Y_OFFSET;
            float xPos;
            float screenCenterX = actualScreenVirtualBounds.Center.X;
            float centeringShift = HAND_WIDTH / 4f;

            if (_playerHand.Hand == HandType.Left)
            {
                xPos = screenCenterX - HAND_WIDTH - (IDLE_POS_X_OFFSET / 2f) - centeringShift;
            }
            else // Right Hand
            {
                xPos = screenCenterX + (IDLE_POS_X_OFFSET / 2f) - centeringShift;
            }

            _idlePosition = new Vector2(xPos, yPos);
            _offscreenPosition = new Vector2(xPos, windowBottomVirtualY + 5);

            // If this is the first update (detected by the bogus start position), snap to offscreen and start animation.
            if (_targetPosition.Y > Global.VIRTUAL_HEIGHT)
            {
                _currentPosition = _offscreenPosition;
                StartAnimation(_idlePosition, SLIDE_ANIMATION_DURATION);
            }
            // --- End Layout Calculation ---

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _pulseTimer += deltaTime;

            bool isActionSelected = !string.IsNullOrEmpty(_playerHand.SelectedActionId);
            bool isAnyCardBeingDragged = inputHandler.DraggedCard != null;
            Vector2 desiredPosition = _idlePosition;

            if (isAnyCardBeingDragged)
            {
                // A drag is in progress. Determine this hand's state during the drag.
                if (isActionSelected)
                {
                    // This hand is occupied, so it's an invalid target and should move down.
                    desiredPosition.Y += INVALID_DROP_Y_OFFSET;
                }
                else // This hand is empty
                {
                    // It's a potential target, so check if the input handler has marked it as such.
                    if (IsPotentialDropTarget)
                    {
                        desiredPosition.Y += HOVER_Y_LIFT;
                    }
                    // If it's not the potential target (mouse is elsewhere), it just stays at idle.
                }
            }
            else
            {
                // No drag is in progress. Apply standard passive/selected states.
                if (isActionSelected)
                {
                    desiredPosition.Y += SELECTED_Y_OFFSET;
                }
                // If not selected, it just stays at the idle position.
            }

            StartAnimation(desiredPosition, FOCUS_ANIMATION_DURATION);

            if (_isAnimating)
            {
                _animationTimer += deltaTime;
                float progress = Math.Min(1f, _animationTimer / _currentAnimationDuration);
                float easedProgress = Easing.EaseOutCubic(progress);

                _currentPosition = Vector2.Lerp(_startPosition, _targetPosition, easedProgress);

                if (progress >= 1f)
                {
                    _isAnimating = false;
                }
            }

            // --- Animator State Machine ---
            UpdateAnimationState(isActionSelected, deltaTime);
            _animator?.Update(gameTime);
        }

        /// <summary>
        /// Manages the state transitions for the hand's keyframed animations.
        /// </summary>
        private void UpdateAnimationState(bool isActionSelected, float deltaTime)
        {
            if (_animator == null) return;

            // --- State: Holding ---
            if (isActionSelected)
            {
                if (_currentState != HandAnimationState.Holding)
                {
                    _animator.Play("hold");
                    _currentState = HandAnimationState.Holding;
                }
            }
            // --- State: Idle (Looping or Twitching) ---
            else
            {
                // Transition from Holding back to Idle
                if (_currentState == HandAnimationState.Holding)
                {
                    _animator.Play("idle_loop");
                    _currentState = HandAnimationState.IdleLoop;
                    ResetIdleTwitchTimer();
                }

                // If a one-shot twitch animation finishes, go back to the main loop
                if (_currentState == HandAnimationState.IdleTwitch && _animator.IsAnimationFinished)
                {
                    _animator.Play("idle_loop");
                    _currentState = HandAnimationState.IdleLoop;
                    ResetIdleTwitchTimer();
                }

                // If in the main loop, check if it's time for a random twitch
                if (_currentState == HandAnimationState.IdleLoop)
                {
                    _idleTwitchTimer -= deltaTime;
                    if (_idleTwitchTimer <= 0 && _idleTwitchAnimations.Any())
                    {
                        string randomAnimation = _idleTwitchAnimations[_random.Next(_idleTwitchAnimations.Count)];
                        _animator.Play(randomAnimation);
                        _currentState = HandAnimationState.IdleTwitch;
                    }
                }
            }
        }

        /// <summary>
        /// Resets the timer for the next random idle animation to a new random value.
        /// </summary>
        private void ResetIdleTwitchTimer()
        {
            _idleTwitchTimer = _random.NextSingle() * (MAX_IDLE_TWITCH_DELAY_SECONDS - MIN_IDLE_TWITCH_DELAY_SECONDS) + MIN_IDLE_TWITCH_DELAY_SECONDS;
        }

        /// <summary>
        /// Draws the hand in its current state and position.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_animator == null)
            {
                // Draw a fallback placeholder if animation loading failed
                var textureFactory = ServiceLocator.Get<TextureFactory>();
                var placeholder = textureFactory.CreateColoredTexture(HAND_WIDTH, HAND_HEIGHT, Color.HotPink);
                spriteBatch.Draw(placeholder, _currentPosition, Color.White);
                return;
            }

            Vector2 finalPosition = _currentPosition;

            // Apply idle sway only when not doing a major animation and action is not selected
            if (!_isAnimating && string.IsNullOrEmpty(_playerHand.SelectedActionId))
            {
                finalPosition += SwayAnimation.Offset;
            }

            Color tint = Color.White;
            if (IsPotentialDropTarget)
            {
                float pulse = 0.75f + (float)Math.Sin(_pulseTimer * PULSE_SPEED) * 0.25f; // Varies between 0.5 and 1.0
                tint = Color.Lerp(Color.White, Color.Yellow, pulse);
            }

            // The animator is drawn at the final calculated position, which includes
            // both the base position animation and the procedural sway.
            _animator.Draw(spriteBatch, finalPosition, tint);
        }
    }
}
using Microsoft.Xna.Framework;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Editor;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// A controller responsible for playing back an AnimationTimeline for a combat action.
    /// It orchestrates visual effects (hand animations, VFX) and triggers the logical
    /// effects via the ActionResolver at the correct time.
    /// </summary>
    public class ActionAnimator
    {
        private readonly ActionResolver _actionResolver;
        private readonly IAnimationPlaybackContext _context;
        private readonly HandRenderer _leftHand;
        private readonly HandRenderer _rightHand;

        private CombatAction _currentAction;
        private AnimationTimeline _currentTimeline;
        private float _timer;
        private readonly HashSet<Keyframe> _triggeredKeyframes = new HashSet<Keyframe>();

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public float PlaybackTime => _timer;


        // State for the intro tween
        private bool _isDoingIntroTween;
        private float _introTweenTimer;
        private const float INTRO_TWEEN_DURATION = 0.8f;

        public ActionAnimator(IAnimationPlaybackContext context, ActionResolver resolver, HandRenderer leftHand, HandRenderer rightHand)
        {
            _context = context;
            _actionResolver = resolver;
            _leftHand = leftHand;
            _rightHand = rightHand;
        }

        /// <summary>
        /// Plays the animation timeline associated with the given combat action.
        /// </summary>
        /// <param name="action">The combat action to animate.</param>
        public void Play(CombatAction action)
        {
            // If there's no timeline, resolve the logic immediately and publish the completion event.
            // This handles basic attacks or actions without complex animations.
            if (action?.ActionData?.Timeline == null)
            {
                Debug.WriteLine($"    [ActionAnimator] Action '{action?.ActionData?.Name}' has no timeline. Resolving effects immediately.");
                if (action != null && _actionResolver != null)
                {
                    // In the editor, resolver can be null.
                    _actionResolver.Resolve(action, (_context as CombatScene)?.GetAllCombatEntities() ?? new List<CombatEntity>());
                }
                EventBus.Publish(new GameEvents.ActionAnimationComplete());
                return;
            }

            _currentAction = action;
            _currentTimeline = action.ActionData.Timeline;
            _timer = 0f;
            _triggeredKeyframes.Clear();
            IsPlaying = true;
            IsPaused = false;

            _isDoingIntroTween = false; // Reset state

            var gameState = ServiceLocator.Get<GameState>();
            if (gameState != null && _currentAction.CasterEntityId == gameState.PlayerEntityId)
            {
                _isDoingIntroTween = true;
                _introTweenTimer = 0f;

                // Command hands to move from their current (off-screen) position to idle.
                if (_context.AnimationAnchors.TryGetValue("LeftHandIdle", out var leftIdle))
                {
                    _leftHand.MoveTo(leftIdle, INTRO_TWEEN_DURATION, Easing.EaseOutQuint);
                }
                if (_context.AnimationAnchors.TryGetValue("RightHandIdle", out var rightIdle))
                {
                    _rightHand.MoveTo(rightIdle, INTRO_TWEEN_DURATION, Easing.EaseOutQuint);
                }
            }

            Debug.WriteLine($"    [ActionAnimator] Playing timeline for '{_currentAction.ActionData.Name}' (Duration: {_currentTimeline.Duration}s)");
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        public void Update(GameTime gameTime)
        {
            if (!IsPlaying || IsPaused) return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Handle the initial tween from off-screen to idle
            if (_isDoingIntroTween)
            {
                _introTweenTimer += deltaTime;
                if (_introTweenTimer >= INTRO_TWEEN_DURATION)
                {
                    _isDoingIntroTween = false;
                    // The main timeline timer starts NOW.
                    _timer = 0f;
                }
                return; // Don't process the main timeline yet
            }

            _timer += deltaTime;

            // Process keyframes
            foreach (var track in _currentTimeline.Tracks)
            {
                foreach (var keyframe in track.Keyframes)
                {
                    if (keyframe.State == KeyframeState.Deleted) continue;

                    // Check if it's time to trigger and if it hasn't been triggered yet
                    if (_timer >= keyframe.Time * _currentTimeline.Duration && !_triggeredKeyframes.Contains(keyframe))
                    {
                        TriggerKeyframe(track, keyframe);
                        _triggeredKeyframes.Add(keyframe);
                    }
                }
            }

            // Check for timeline completion
            if (_timer >= _currentTimeline.Duration)
            {
                OnTimelineFinished(true);
            }
        }

        /// <summary>
        /// Instantly moves the animation playback to a specific time.
        /// This is a performant method for timeline scrubbing that snaps to the state defined
        /// by the most recent keyframes, rather than simulating the time in-between.
        /// </summary>
        public void Seek(float newTime)
        {
            if (_currentTimeline == null) return;

            IsPlaying = true;
            IsPaused = true;
            _timer = newTime;

            // For each hand, find the state it should be in by looking at the last keyframe for each property.
            SeekHandState(_leftHand, "LeftHand");
            SeekHandState(_rightHand, "RightHand");
        }

        private void SeekHandState(HandRenderer hand, string trackName)
        {
            var track = _currentTimeline.Tracks.FirstOrDefault(t => t.Target == trackName);
            if (track == null) return;

            float timeRatio = _timer / _currentTimeline.Duration;

            // Find the last keyframe for each property type that should have already occurred.
            var lastMove = track.Keyframes.LastOrDefault(k => k.Type == "MoveTo" && k.Time <= timeRatio && k.State != KeyframeState.Deleted);
            var lastRotate = track.Keyframes.LastOrDefault(k => k.Type == "RotateTo" && k.Time <= timeRatio && k.State != KeyframeState.Deleted);
            var lastScale = track.Keyframes.LastOrDefault(k => k.Type == "ScaleTo" && k.Time <= timeRatio && k.State != KeyframeState.Deleted);

            Vector2 finalPos = _context.AnimationAnchors.TryGetValue("LeftHandIdle", out var idlePos) ? idlePos : Vector2.Zero;
            if (lastMove != null && _context.AnimationAnchors.TryGetValue(lastMove.Position, out var anchorPos))
            {
                finalPos = anchorPos;
            }

            float finalRot = 0f;
            if (lastRotate != null)
            {
                finalRot = MathHelper.ToRadians(lastRotate.Rotation);
            }

            float finalScale = 1f;
            if (lastScale != null)
            {
                finalScale = lastScale.Scale;
            }

            hand.ForcePositionAndRotation(finalPos, finalRot);
            hand.ForceScale(finalScale);
        }


        private void TriggerKeyframe(AnimationTrack track, Keyframe keyframe)
        {
            // --- Caster-Aware Animation Logic ---
            // Check if the keyframe targets a player-specific element. If so, ensure the player is the caster.
            var gameState = ServiceLocator.Get<GameState>();
            bool isPlayerCasting = gameState == null || _currentAction.CasterEntityId == gameState.PlayerEntityId;

            if (track.Target == "LeftHand" || track.Target == "RightHand")
            {
                if (!isPlayerCasting)
                {
                    // This is an AI action trying to animate the player's hands. Skip it.
                    return;
                }
            }

            HandRenderer targetHand = GetTargetHand(track.Target);
            var easingFunc = Easing.GetEasingFunction(keyframe.Easing);

            switch (keyframe.Type)
            {
                case "PlayAnimation":
                    targetHand?.PlayAnimation(keyframe.AnimationName);
                    break;

                case "TriggerEffects":
                    if (_actionResolver != null && _context is CombatScene combatScene)
                    {
                        _actionResolver.Resolve(_currentAction, combatScene.GetAllCombatEntities());
                    }
                    else
                    {
                        Debug.WriteLine($"    [ActionAnimator] TriggerEffects fired at t={_timer:F2}s (No ActionResolver available in this context)");
                    }
                    break;

                case "MoveTo":
                    if (targetHand != null && _context.AnimationAnchors.TryGetValue(keyframe.Position, out var targetPos))
                    {
                        targetHand.MoveTo(targetPos, keyframe.Duration, easingFunc);
                    }
                    break;

                case "RotateTo":
                    targetHand?.RotateTo(MathHelper.ToRadians(keyframe.Rotation), keyframe.Duration, easingFunc);
                    break;

                case "ScaleTo":
                    targetHand?.ScaleTo(keyframe.Scale, keyframe.Duration, easingFunc);
                    break;

                case "Wait":
                    // This keyframe type does nothing but act as a marker in the timeline.
                    break;
            }
        }

        private HandRenderer GetTargetHand(string target)
        {
            return target switch
            {
                "LeftHand" => _leftHand,
                "RightHand" => _rightHand,
                _ => null
            };
        }

        public void Stop()
        {
            OnTimelineFinished(false);
        }

        private void OnTimelineFinished(bool publishEvent)
        {
            if (!IsPlaying) return;

            if (_currentAction != null)
                Debug.WriteLine($"    [ActionAnimator] Timeline for '{_currentAction.ActionData.Name}' finished.");

            IsPlaying = false;
            IsPaused = false;

            // Ensure hands return to their off-screen state after the animation is complete.
            var gameState = ServiceLocator.Get<GameState>();
            if (gameState != null && _currentAction != null && _currentAction.CasterEntityId == gameState.PlayerEntityId)
            {
                if (_context.AnimationAnchors.TryGetValue("LeftHandOffscreen", out var leftOffscreen))
                {
                    _leftHand.MoveTo(leftOffscreen, 0.4f, Easing.EaseOutQuint);
                }
                if (_context.AnimationAnchors.TryGetValue("RightHandOffscreen", out var rightOffscreen))
                {
                    _rightHand.MoveTo(rightOffscreen, 0.4f, Easing.EaseOutQuint);
                }
                _leftHand.RotateTo(0, 0.3f, Easing.EaseOutCubic);
                _rightHand.RotateTo(0, 0.3f, Easing.EaseOutCubic);
            }

            _currentAction = null;
            _currentTimeline = null;

            if (publishEvent)
            {
                // The animator is the authority on when the *visuals* are complete.
                EventBus.Publish(new GameEvents.ActionAnimationComplete());
            }
        }
    }
}
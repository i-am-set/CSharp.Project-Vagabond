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
        /// <summary>
        /// A private cache to hold pre-sorted lists of keyframes for a single track,
        /// optimizing lookups during timeline scrubbing.
        /// </summary>
        private class KeyframeCache
        {
            public readonly List<Keyframe> MoveKeyframes;
            public readonly List<Keyframe> RotateKeyframes;
            public readonly List<Keyframe> ScaleKeyframes;
            public readonly List<Keyframe> AnimKeyframes;

            public KeyframeCache(AnimationTrack track)
            {
                MoveKeyframes = track.Keyframes.Where(k => k.Type == "MoveTo" && k.State != KeyframeState.Deleted).OrderBy(k => k.Time).ToList();
                RotateKeyframes = track.Keyframes.Where(k => k.Type == "RotateTo" && k.State != KeyframeState.Deleted).OrderBy(k => k.Time).ToList();
                ScaleKeyframes = track.Keyframes.Where(k => k.Type == "ScaleTo" && k.State != KeyframeState.Deleted).OrderBy(k => k.Time).ToList();
                AnimKeyframes = track.Keyframes.Where(k => k.Type == "PlayAnimation" && k.State != KeyframeState.Deleted).OrderBy(k => k.Time).ToList();
            }
        }

        private readonly ActionResolver _actionResolver;
        private readonly IAnimationPlaybackContext _context;
        private readonly HandRenderer _leftHand;
        private readonly HandRenderer _rightHand;

        private CombatAction _currentAction;
        private AnimationTimeline _currentTimeline;
        private float _timer;
        private readonly HashSet<Keyframe> _triggeredKeyframes = new HashSet<Keyframe>();
        private readonly Dictionary<string, KeyframeCache> _trackCaches = new Dictionary<string, KeyframeCache>();


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

            // Pre-process and cache the keyframes for fast lookups during scrubbing.
            _trackCaches.Clear();
            foreach (var track in _currentTimeline.Tracks)
            {
                _trackCaches[track.Target] = new KeyframeCache(track);
            }

            _isDoingIntroTween = false; // Reset state

            var gameState = ServiceLocator.Get<GameState>();
            // The intro tween should only happen for the player in the actual combat scene, not the editor.
            if (gameState != null && _currentAction.CasterEntityId == gameState.PlayerEntityId && _context is CombatScene)
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
            if (_isDoingIntroTween)
            {
                float introDelta = (float)gameTime.ElapsedGameTime.TotalSeconds;
                _introTweenTimer += introDelta;
                if (_introTweenTimer >= INTRO_TWEEN_DURATION)
                {
                    _isDoingIntroTween = false;
                    _timer = 0f;
                }
                // Let the HandRenderer's internal tweening handle this part
                return;
            }

            if (!IsPlaying) return;

            if (!IsPaused)
            {
                float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
                _timer += deltaTime;

                // Process one-shot keyframes as the timer passes them
                foreach (var track in _currentTimeline.Tracks)
                {
                    foreach (var keyframe in track.Keyframes)
                    {
                        if (keyframe.State == KeyframeState.Deleted) continue;

                        // Only trigger non-continuous, one-shot keyframes here.
                        bool isOneShot = keyframe.Type == "PlayAnimation" || keyframe.Type == "TriggerEffects";
                        if (!isOneShot) continue;

                        if (_timer >= keyframe.Time * _currentTimeline.Duration && !_triggeredKeyframes.Contains(keyframe))
                        {
                            TriggerOneShotKeyframe(track, keyframe);
                            _triggeredKeyframes.Add(keyframe);
                        }
                    }
                }
            }

            // Continuously update transforms based on the timer, even when paused.
            UpdateHandTransforms();

            if (!IsPaused && _timer >= _currentTimeline.Duration)
            {
                OnTimelineFinished(true);
            }
        }

        /// <summary>
        /// Instantly moves the animation playback to a specific time, smoothly interpolating
        /// the state between keyframes.
        /// </summary>
        public void Seek(float newTime)
        {
            if (_currentTimeline == null) return;

            IsPlaying = true;
            IsPaused = true;
            _timer = newTime;

            // A single call to update the transforms is enough when seeking.
            UpdateHandTransforms();
        }

        private void UpdateHandTransforms()
        {
            if (_currentTimeline == null || _currentAction == null) return;

            // --- ARCHITECTURAL GUARD ---
            // Only update player hand transforms if the player is the one casting the action.
            var gameState = ServiceLocator.Get<GameState>();
            if (gameState != null && _currentAction.CasterEntityId != gameState.PlayerEntityId)
            {
                // This is an AI action with a timeline that targets hands. Do not animate the player's hands.
                // In the future, this is where AI-specific animation logic would go.
                return;
            }

            SetHandStateAtTime(_leftHand, "LeftHand");
            SetHandStateAtTime(_rightHand, "RightHand");
        }

        private void SetHandStateAtTime(HandRenderer hand, string trackName)
        {
            if (!_trackCaches.TryGetValue(trackName, out var cache) || _currentTimeline.Duration <= 0)
            {
                return;
            }

            // --- Get Idle Defaults ---
            string idleAnchorName = trackName == "LeftHand" ? "LeftHandIdle" : "RightHandIdle";
            _context.AnimationAnchors.TryGetValue(idleAnchorName, out var idlePos);
            float idleRot = 0f;
            float idleScale = 1f;

            // --- Calculate Final Values ---
            Vector2 finalPos = CalculateInterpolatedValue(cache.MoveKeyframes, key => _context.AnimationAnchors.TryGetValue(key.Position, out var pos) ? pos : idlePos, idlePos, Vector2.Lerp);
            float finalRot = CalculateInterpolatedValue(cache.RotateKeyframes, key => MathHelper.ToRadians(key.Rotation), idleRot, MathHelper.Lerp);
            float finalScale = CalculateInterpolatedValue(cache.ScaleKeyframes, key => key.Scale, idleScale, MathHelper.Lerp);
            string finalAnimation = GetCurrentAnimation(cache.AnimKeyframes);

            // --- Apply Final State ---
            hand.ForcePositionAndRotation(finalPos, finalRot);
            hand.ForceScale(finalScale);
            if (!string.IsNullOrEmpty(finalAnimation))
            {
                hand.PlayAnimation(finalAnimation);
            }
        }

        private T CalculateInterpolatedValue<T>(List<Keyframe> sortedKeyframes, Func<Keyframe, T> valueSelector, T idleValue, Func<T, T, float, T> lerpFunc)
        {
            if (!sortedKeyframes.Any()) return idleValue;

            // Find the keyframe that defines the END of the current animation segment.
            int nextKeyIndex = sortedKeyframes.FindIndex(k => k.Time * _currentTimeline.Duration >= _timer);

            if (nextKeyIndex == -1) // We are past the last keyframe, so hold its state.
            {
                return valueSelector(sortedKeyframes.Last());
            }

            Keyframe nextKey = sortedKeyframes[nextKeyIndex];
            Keyframe prevKey = (nextKeyIndex > 0) ? sortedKeyframes[nextKeyIndex - 1] : null;

            // If we are before the first keyframe, hold the idle state.
            if (prevKey == null)
            {
                return idleValue;
            }

            // We are between prevKey and nextKey.
            T startValue = valueSelector(prevKey);
            T endValue = valueSelector(nextKey);

            float segmentStartTime = prevKey.Time * _currentTimeline.Duration;
            float segmentEndTime = nextKey.Time * _currentTimeline.Duration;
            float segmentDuration = segmentEndTime - segmentStartTime;

            if (segmentDuration <= 0)
            {
                return endValue; // Instant snap if keyframes are at the same time.
            }

            float progress = (_timer - segmentStartTime) / segmentDuration;
            var easingFunc = Easing.GetEasingFunction(nextKey.Easing); // Easing is on the destination keyframe.
            return lerpFunc(startValue, endValue, easingFunc(progress));
        }

        /// <summary>
        /// Efficiently finds the index of the last keyframe in a sorted list that occurs at or before the given time ratio.
        /// </summary>
        private int FindLastIndexBefore(List<Keyframe> keyframes, float timeRatio)
        {
            int index = -1;
            for (int i = 0; i < keyframes.Count; i++)
            {
                if (keyframes[i].Time <= timeRatio)
                {
                    index = i;
                }
                else
                {
                    // Since the list is sorted, we can exit early.
                    break;
                }
            }
            return index;
        }

        private string GetCurrentAnimation(List<Keyframe> keyframes)
        {
            if (!keyframes.Any() || _currentTimeline.Duration == 0) return null;
            float timeRatio = _timer / _currentTimeline.Duration;
            int lastKeyIndex = FindLastIndexBefore(keyframes, timeRatio);
            return (lastKeyIndex != -1) ? keyframes[lastKeyIndex].AnimationName : null;
        }


        private void TriggerOneShotKeyframe(AnimationTrack track, Keyframe keyframe)
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
            _trackCaches.Clear();

            if (publishEvent)
            {
                // The animator is the authority on when the *visuals* are complete.
                EventBus.Publish(new GameEvents.ActionAnimationComplete());
            }
        }
    }
}
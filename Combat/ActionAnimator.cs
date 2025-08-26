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

        // The "state before time 0" for each hand, used as the start of the first tween.
        private Vector2 _leftHandStartState, _rightHandStartState;
        private float _leftHandStartRot, _rightHandStartRot;
        private float _leftHandStartScale, _rightHandStartScale;

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public float PlaybackTime => _timer;

        // State for the mandatory intro tween
        private bool _isDoingIntroTween;
        private float _totalPlaybackTimer;
        private const float INTRO_DURATION = 0.5f;
        private Vector2 _leftHandIntroStartPos, _leftHandIntroTargetPos;
        private float _leftHandIntroStartRot, _leftHandIntroTargetRot;
        private float _leftHandIntroStartScale, _leftHandIntroTargetScale;
        private Vector2 _rightHandIntroStartPos, _rightHandIntroTargetPos;
        private float _rightHandIntroStartRot, _rightHandIntroTargetRot;
        private float _rightHandIntroStartScale, _rightHandIntroTargetScale;


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
            if (action?.ActionData?.Timeline == null)
            {
                Debug.WriteLine($"    [ActionAnimator] Action '{action?.ActionData?.Name}' has no timeline. Resolving effects immediately.");
                if (action != null && _actionResolver != null)
                {
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
            _isDoingIntroTween = true;
            _totalPlaybackTimer = 0f;

            _trackCaches.Clear();
            foreach (var track in _currentTimeline.Tracks)
            {
                _trackCaches[track.Target] = new KeyframeCache(track);
            }

            // --- Setup for Intro Tween and Main Timeline ---

            // 1. Capture the hands' CURRENT state as the intro's START state.
            _leftHandIntroStartPos = _leftHand.CurrentPosition;
            _leftHandIntroStartRot = _leftHand.CurrentRotation;
            _leftHandIntroStartScale = _leftHand.CurrentScale;
            _rightHandIntroStartPos = _rightHand.CurrentPosition;
            _rightHandIntroStartRot = _rightHand.CurrentRotation;
            _rightHandIntroStartScale = _rightHand.CurrentScale;

            // 2. Determine the intro's TARGET state (the state at time 0 of the main timeline).
            // We do this by temporarily setting the main timer to 0 and calculating the state.
            float originalTimer = _timer;
            _timer = 0;
            // These calls calculate the state at t=0 and update the hands' properties internally.
            SetHandStateAtTime(_leftHand, "LeftHand");
            SetHandStateAtTime(_rightHand, "RightHand");
            // Store the results as the intro's target.
            _leftHandIntroTargetPos = _leftHand.CurrentPosition;
            _leftHandIntroTargetRot = _leftHand.CurrentRotation;
            _leftHandIntroTargetScale = _leftHand.CurrentScale;
            _rightHandIntroTargetPos = _rightHand.CurrentPosition;
            _rightHandIntroTargetRot = _rightHand.CurrentRotation;
            _rightHandIntroTargetScale = _rightHand.CurrentScale;
            _timer = originalTimer; // Restore timer

            // 3. Set the implicit "start state" for the main timeline to be the state at time 0.
            _leftHandStartState = _leftHandIntroTargetPos;
            _leftHandStartRot = _leftHandIntroTargetRot;
            _leftHandStartScale = _leftHandIntroTargetScale;
            _rightHandStartState = _rightHandIntroTargetPos;
            _rightHandStartRot = _rightHandIntroTargetRot;
            _rightHandStartScale = _rightHandIntroTargetScale;

            Debug.WriteLine($"    [ActionAnimator] Playing timeline for '{_currentAction.ActionData.Name}' (Duration: {_currentTimeline.Duration}s)");
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        public void Update(GameTime gameTime)
        {
            if (!IsPlaying) return;

            if (!IsPaused)
            {
                _totalPlaybackTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            if (_isDoingIntroTween)
            {
                float progress = Math.Clamp(_totalPlaybackTimer / INTRO_DURATION, 0f, 1f);
                UpdateIntroTween(progress);

                if (progress >= 1.0f)
                {
                    _isDoingIntroTween = false;
                }
            }
            else // Not in intro tween, so do main timeline playback
            {
                // The main timeline timer is the total time minus the intro duration.
                _timer = _totalPlaybackTimer - INTRO_DURATION;

                // Process one-shot keyframes
                foreach (var track in _currentTimeline.Tracks)
                {
                    foreach (var keyframe in track.Keyframes)
                    {
                        if (keyframe.State == KeyframeState.Deleted) continue;
                        bool isOneShot = keyframe.Type == "PlayAnimation" || keyframe.Type == "TriggerEffects";
                        if (!isOneShot) continue;

                        if (_timer >= keyframe.Time * _currentTimeline.Duration && !_triggeredKeyframes.Contains(keyframe))
                        {
                            TriggerOneShotKeyframe(track, keyframe);
                            _triggeredKeyframes.Add(keyframe);
                        }
                    }
                }

                // Update transforms
                UpdateHandTransforms();

                // Check for finish
                if (!IsPaused && _timer >= _currentTimeline.Duration)
                {
                    OnTimelineFinished(true);
                }
            }
        }

        private void UpdateIntroTween(float progress)
        {
            float easedProgress = Easing.EaseOutCubic(progress);

            _leftHand.ForcePositionAndRotation(
                Vector2.Lerp(_leftHandIntroStartPos, _leftHandIntroTargetPos, easedProgress),
                MathHelper.Lerp(_leftHandIntroStartRot, _leftHandIntroTargetRot, easedProgress)
            );
            _leftHand.ForceScale(MathHelper.Lerp(_leftHandIntroStartScale, _leftHandIntroTargetScale, easedProgress));

            _rightHand.ForcePositionAndRotation(
                Vector2.Lerp(_rightHandIntroStartPos, _rightHandIntroTargetPos, easedProgress),
                MathHelper.Lerp(_rightHandIntroStartRot, _rightHandIntroTargetRot, easedProgress)
            );
            _rightHand.ForceScale(MathHelper.Lerp(_rightHandIntroStartScale, _rightHandIntroTargetScale, easedProgress));
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
            _isDoingIntroTween = false; // Seeking bypasses the intro tween.
            _totalPlaybackTimer = newTime + INTRO_DURATION; // Set total timer so that _timer becomes newTime
            _timer = newTime;

            UpdateHandTransforms();
        }

        private void UpdateHandTransforms()
        {
            if (_currentTimeline == null || _currentAction == null) return;

            var gameState = ServiceLocator.Get<GameState>();
            if (gameState != null && _currentAction.CasterEntityId != gameState.PlayerEntityId)
            {
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

            bool isLeftHand = trackName == "LeftHand";
            Vector2 startPos = isLeftHand ? _leftHandStartState : _rightHandStartState;
            float startRot = isLeftHand ? _leftHandStartRot : _rightHandStartRot;
            float startScale = isLeftHand ? _leftHandStartScale : _rightHandStartScale;

            Vector2 finalPos = CalculateInterpolatedValue(cache.MoveKeyframes,
                key =>
                {
                    if (key.TargetX.HasValue && key.TargetY.HasValue)
                    {
                        return new Vector2(key.TargetX.Value, key.TargetY.Value);
                    }
                    if (!string.IsNullOrEmpty(key.Position) && _context.AnimationAnchors.TryGetValue(key.Position, out var pos))
                    {
                        return pos;
                    }
                    return startPos;
                },
                startPos,
                Vector2.Lerp);

            float finalRot = CalculateInterpolatedValue(cache.RotateKeyframes, key => MathHelper.ToRadians(key.Rotation), startRot, MathHelper.Lerp);
            float finalScale = CalculateInterpolatedValue(cache.ScaleKeyframes, key => key.Scale, startScale, MathHelper.Lerp);
            string finalAnimation = GetCurrentAnimation(cache.AnimKeyframes);

            hand.ForcePositionAndRotation(finalPos, finalRot);
            hand.ForceScale(finalScale);
            if (!string.IsNullOrEmpty(finalAnimation))
            {
                hand.PlayAnimation(finalAnimation);
            }
        }

        private T CalculateInterpolatedValue<T>(List<Keyframe> sortedKeyframes, Func<Keyframe, T> valueSelector, T startValue, Func<T, T, float, T> lerpFunc)
        {
            if (!sortedKeyframes.Any()) return startValue;

            int nextKeyIndex = sortedKeyframes.FindIndex(k => k.Time * _currentTimeline.Duration >= _timer);

            if (nextKeyIndex == -1)
            {
                return valueSelector(sortedKeyframes.Last());
            }

            Keyframe nextKey = sortedKeyframes[nextKeyIndex];
            Keyframe prevKey = (nextKeyIndex > 0) ? sortedKeyframes[nextKeyIndex - 1] : null;

            T segmentStartValue;
            float segmentStartTime;

            if (prevKey == null)
            {
                segmentStartValue = startValue;
                segmentStartTime = 0f;
            }
            else
            {
                segmentStartValue = valueSelector(prevKey);
                segmentStartTime = prevKey.Time * _currentTimeline.Duration;
            }

            T endValue = valueSelector(nextKey);
            float segmentEndTime = nextKey.Time * _currentTimeline.Duration;
            float segmentDuration = segmentEndTime - segmentStartTime;

            if (segmentDuration <= 0)
            {
                return endValue;
            }

            float progress = (_timer - segmentStartTime) / segmentDuration;
            var easingFunc = Easing.GetEasingFunction(nextKey.Easing);
            return lerpFunc(segmentStartValue, endValue, easingFunc(progress));
        }

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
            var gameState = ServiceLocator.Get<GameState>();
            bool isPlayerCasting = gameState == null || _currentAction.CasterEntityId == gameState.PlayerEntityId;

            if (track.Target == "LeftHand" || track.Target == "RightHand")
            {
                if (!isPlayerCasting)
                {
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
                EventBus.Publish(new GameEvents.ActionAnimationComplete());
            }
        }
    }
}
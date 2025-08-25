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
        /// Instantly moves the animation playback to a specific time, smoothly interpolating
        /// the state between keyframes.
        /// </summary>
        public void Seek(float newTime)
        {
            if (_currentTimeline == null) return;

            IsPlaying = true;
            IsPaused = true;
            _timer = newTime;

            // For each hand, find the state it should be in by interpolating between keyframes.
            SeekHandState(_leftHand, "LeftHand");
            SeekHandState(_rightHand, "RightHand");
        }

        private void SeekHandState(HandRenderer hand, string trackName)
        {
            var track = _currentTimeline.Tracks.FirstOrDefault(t => t.Target == trackName);
            if (track == null || _currentTimeline.Duration <= 0) return;

            // --- Get Idle Defaults ---
            string idleAnchorName = trackName == "LeftHand" ? "LeftHandIdle" : "RightHandIdle";
            _context.AnimationAnchors.TryGetValue(idleAnchorName, out var idlePos);
            float idleRot = 0f;
            float idleScale = 1f;

            // --- POSITION ---
            var moveKeyframes = track.Keyframes.Where(k => k.Type == "MoveTo" && k.State != KeyframeState.Deleted).OrderBy(k => k.Time).ToList();
            Vector2 finalPos;
            Keyframe activeMoveTween = moveKeyframes.FirstOrDefault(k => _timer >= k.Time * _currentTimeline.Duration && _timer < k.Time * _currentTimeline.Duration + k.Duration);
            if (activeMoveTween != null)
            {
                Keyframe keyBefore = moveKeyframes.LastOrDefault(k => k.Time < activeMoveTween.Time);
                Vector2 startValue = (keyBefore != null && _context.AnimationAnchors.TryGetValue(keyBefore.Position, out var pPos)) ? pPos : idlePos;
                _context.AnimationAnchors.TryGetValue(activeMoveTween.Position, out var endValue);
                float tweenStartTime = activeMoveTween.Time * _currentTimeline.Duration;
                float progress = (_timer - tweenStartTime) / activeMoveTween.Duration;
                var easingFunc = Easing.GetEasingFunction(activeMoveTween.Easing);
                finalPos = Vector2.Lerp(startValue, endValue, easingFunc(progress));
            }
            else
            {
                Keyframe lastKey = moveKeyframes.LastOrDefault(k => k.Time * _currentTimeline.Duration <= _timer);
                finalPos = (lastKey != null && _context.AnimationAnchors.TryGetValue(lastKey.Position, out var lPos)) ? lPos : idlePos;
            }

            // --- ROTATION ---
            var rotateKeyframes = track.Keyframes.Where(k => k.Type == "RotateTo" && k.State != KeyframeState.Deleted).OrderBy(k => k.Time).ToList();
            float finalRot;
            Keyframe activeRotateTween = rotateKeyframes.FirstOrDefault(k => _timer >= k.Time * _currentTimeline.Duration && _timer < k.Time * _currentTimeline.Duration + k.Duration);
            if (activeRotateTween != null)
            {
                Keyframe keyBefore = rotateKeyframes.LastOrDefault(k => k.Time < activeRotateTween.Time);
                float startValue = keyBefore != null ? MathHelper.ToRadians(keyBefore.Rotation) : idleRot;
                float endValue = MathHelper.ToRadians(activeRotateTween.Rotation);
                float tweenStartTime = activeRotateTween.Time * _currentTimeline.Duration;
                float progress = (_timer - tweenStartTime) / activeRotateTween.Duration;
                var easingFunc = Easing.GetEasingFunction(activeRotateTween.Easing);
                finalRot = MathHelper.Lerp(startValue, endValue, easingFunc(progress));
            }
            else
            {
                Keyframe lastKey = rotateKeyframes.LastOrDefault(k => k.Time * _currentTimeline.Duration <= _timer);
                finalRot = lastKey != null ? MathHelper.ToRadians(lastKey.Rotation) : idleRot;
            }

            // --- SCALE ---
            var scaleKeyframes = track.Keyframes.Where(k => k.Type == "ScaleTo" && k.State != KeyframeState.Deleted).OrderBy(k => k.Time).ToList();
            float finalScale;
            Keyframe activeScaleTween = scaleKeyframes.FirstOrDefault(k => _timer >= k.Time * _currentTimeline.Duration && _timer < k.Time * _currentTimeline.Duration + k.Duration);
            if (activeScaleTween != null)
            {
                Keyframe keyBefore = scaleKeyframes.LastOrDefault(k => k.Time < activeScaleTween.Time);
                float startValue = keyBefore != null ? keyBefore.Scale : idleScale;
                float endValue = activeScaleTween.Scale;
                float tweenStartTime = activeScaleTween.Time * _currentTimeline.Duration;
                float progress = (_timer - tweenStartTime) / activeScaleTween.Duration;
                var easingFunc = Easing.GetEasingFunction(activeScaleTween.Easing);
                finalScale = MathHelper.Lerp(startValue, endValue, easingFunc(progress));
            }
            else
            {
                Keyframe lastKey = scaleKeyframes.LastOrDefault(k => k.Time * _currentTimeline.Duration <= _timer);
                finalScale = lastKey != null ? lastKey.Scale : idleScale;
            }

            // --- ANIMATION ---
            var animKeyframes = track.Keyframes.Where(k => k.Type == "PlayAnimation" && k.State != KeyframeState.Deleted).OrderBy(k => k.Time).ToList();
            string finalAnimation = GetCurrentAnimation(animKeyframes);

            // --- APPLY FINAL STATE ---
            hand.ForcePositionAndRotation(finalPos, finalRot);
            hand.ForceScale(finalScale);
            if (!string.IsNullOrEmpty(finalAnimation))
            {
                hand.PlayAnimation(finalAnimation);
            }
        }

        private string GetCurrentAnimation(List<Keyframe> keyframes)
        {
            if (!keyframes.Any() || _currentTimeline.Duration == 0) return null;
            float timeRatio = _timer / _currentTimeline.Duration;
            Keyframe lastKey = keyframes.LastOrDefault(k => k.Time <= timeRatio);
            return lastKey?.AnimationName;
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
﻿using Microsoft.Xna.Framework;
using ProjectVagabond.Combat.UI;
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
        private readonly CombatScene _combatScene;
        private readonly HandRenderer _leftHand;
        private readonly HandRenderer _rightHand;

        private CombatAction _currentAction;
        private AnimationTimeline _currentTimeline;
        private float _timer;
        private readonly HashSet<Keyframe> _triggeredKeyframes = new HashSet<Keyframe>();
        private bool _isPlaying;

        // State for the intro tween
        private bool _isDoingIntroTween;
        private float _introTweenTimer;
        private const float INTRO_TWEEN_DURATION = 0.8f;

        public ActionAnimator(ActionResolver resolver, CombatScene scene, HandRenderer leftHand, HandRenderer rightHand)
        {
            _actionResolver = resolver;
            _combatScene = scene;
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
                if (action != null)
                {
                    _actionResolver.Resolve(action, _combatScene.GetAllCombatEntities());
                }
                EventBus.Publish(new GameEvents.ActionAnimationComplete());
                return;
            }

            _currentAction = action;
            _currentTimeline = action.ActionData.Timeline;
            _timer = 0f;
            _triggeredKeyframes.Clear();
            _isPlaying = true; // This now means "animator is active"

            _isDoingIntroTween = false; // Reset state

            var gameState = ServiceLocator.Get<GameState>();
            if (_currentAction.CasterEntityId == gameState.PlayerEntityId)
            {
                _isDoingIntroTween = true;
                _introTweenTimer = 0f;

                // Command hands to move from their current (off-screen) position to idle.
                if (_combatScene.AnimationAnchors.TryGetValue("LeftHandIdle", out var leftIdle))
                {
                    _leftHand.MoveTo(leftIdle, INTRO_TWEEN_DURATION, Easing.EaseOutQuint);
                }
                if (_combatScene.AnimationAnchors.TryGetValue("RightHandIdle", out var rightIdle))
                {
                    _rightHand.MoveTo(rightIdle, INTRO_TWEEN_DURATION, Easing.EaseOutQuint);
                }
            }

            Debug.WriteLine($"    [ActionAnimator] Playing timeline for '{_currentAction.ActionData.Name}' (Duration: {_currentTimeline.Duration}s)");
        }

        public void Update(GameTime gameTime)
        {
            if (!_isPlaying) return;

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
                Stop();
            }
        }

        private void TriggerKeyframe(AnimationTrack track, Keyframe keyframe)
        {
            // --- Caster-Aware Animation Logic ---
            // Check if the keyframe targets a player-specific element. If so, ensure the player is the caster.
            var gameState = ServiceLocator.Get<GameState>();
            bool isPlayerCasting = _currentAction.CasterEntityId == gameState.PlayerEntityId;

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
                    _actionResolver.Resolve(_currentAction, _combatScene.GetAllCombatEntities());
                    break;

                case "MoveTo":
                    if (targetHand != null && _combatScene.AnimationAnchors.TryGetValue(keyframe.Position, out var targetPos))
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

        private void Stop()
        {
            Debug.WriteLine($"    [ActionAnimator] Timeline for '{_currentAction.ActionData.Name}' finished.");
            _isPlaying = false;

            // Ensure hands return to their off-screen state after the animation is complete.
            var gameState = ServiceLocator.Get<GameState>();
            if (_currentAction.CasterEntityId == gameState.PlayerEntityId)
            {
                if (_combatScene.AnimationAnchors.TryGetValue("LeftHandOffscreen", out var leftOffscreen))
                {
                    _leftHand.MoveTo(leftOffscreen, 0.4f, Easing.EaseOutQuint);
                }
                if (_combatScene.AnimationAnchors.TryGetValue("RightHandOffscreen", out var rightOffscreen))
                {
                    _rightHand.MoveTo(rightOffscreen, 0.4f, Easing.EaseOutQuint);
                }
                _leftHand.RotateTo(0, 0.3f, Easing.EaseOutCubic);
                _rightHand.RotateTo(0, 0.3f, Easing.EaseOutCubic);
            }


            _currentAction = null;
            _currentTimeline = null;

            // The animator is the authority on when the *visuals* are complete.
            EventBus.Publish(new GameEvents.ActionAnimationComplete());
        }
    }
}

using Microsoft.Xna.Framework;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// A controller responsible for transitioning the player's hands between static Poses.
    /// It orchestrates hand tweens and manages associated particle effects.
    /// </summary>
    public class ActionAnimator
    {
        private class HoldEmitter
        {
            public ParticleEmitter Emitter;
            public ParticleAnchorType Anchor;
            public float ActivationTimer;
        }

        private readonly HandRenderer _leftHand;
        private readonly HandRenderer _rightHand;
        private readonly ParticleSystemManager _particleSystemManager;
        private readonly PoseManager _poseManager;
        private readonly List<HoldEmitter> _activeEmitters = new List<HoldEmitter>();

        private Action _onTransitionComplete;
        private System.Timers.Timer _completionTimer;

        public PoseData CurrentPose { get; private set; }

        // --- TUNING ---
        private const float PARTICLE_RAMP_UP_DURATION = 10f;

        public ActionAnimator(HandRenderer leftHand, HandRenderer rightHand)
        {
            _leftHand = leftHand;
            _rightHand = rightHand;
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _poseManager = ServiceLocator.Get<PoseManager>();
        }

        public void Update(GameTime gameTime)
        {
            // The HandRenderers manage their own tweening. This manager's primary
            // role is to initiate transitions and manage associated particles.
            UpdateHoldEmitters(gameTime);
        }

        /// <summary>
        /// Smoothly transitions the hands to a specified pose.
        /// </summary>
        /// <param name="action">The combat action being performed, used to check caster ID. Can be null for player-only animations like holding a card.</param>
        /// <param name="poseId">The ID of the pose to transition to.</param>
        /// <param name="duration">The duration of the tween.</param>
        /// <param name="onComplete">An optional callback to invoke when the transition finishes.</param>
        public void TransitionToPose(CombatAction action, string poseId, float duration, Action onComplete = null)
        {
            StopHoldEmitters();
            _completionTimer?.Stop();
            _completionTimer?.Dispose();
            _onTransitionComplete = onComplete;

            CurrentPose = _poseManager.GetPose(poseId);
            if (CurrentPose == null)
            {
                if (string.IsNullOrEmpty(poseId))
                {
                    Debug.WriteLine($"[ActionAnimator] [WARNING] A null or empty poseId was provided. This may indicate a missing 'holdPoseId' or 'castPoseId' in an ActionData file. Defaulting to idle.");
                }
                else
                {
                    Debug.WriteLine($"[ActionAnimator] [WARNING] Pose '{poseId}' not found. Returning to idle.");
                }
                ReturnToIdle(duration, onComplete);
                return;
            }

            // Apply the pose to the hands
            ApplyHandState(_leftHand, CurrentPose.LeftHand, duration);
            ApplyHandState(_rightHand, CurrentPose.RightHand, duration);

            // Initialize particle effects for the new pose, but only for the player.
            var gameState = ServiceLocator.Get<GameState>();
            if (action == null || (gameState != null && action.CasterEntityId == gameState.PlayerEntityId))
            {
                InitializeHoldEmitters(CurrentPose);
            }

            // A simple mechanism to trigger the callback after the duration.
            // A more robust system might use tweening library callbacks.
            if (onComplete != null)
            {
                if (duration <= 0)
                {
                    _onTransitionComplete?.Invoke();
                }
                else
                {
                    _completionTimer = new System.Timers.Timer(duration * 1000);
                    _completionTimer.Elapsed += (sender, e) =>
                    {
                        _onTransitionComplete?.Invoke();
                        _completionTimer.Stop();
                        _completionTimer.Dispose();
                        _completionTimer = null;
                    };
                    _completionTimer.AutoReset = false;
                    _completionTimer.Start();
                }
            }
        }

        /// <summary>
        /// Returns the hands to their default idle pose.
        /// </summary>
        public void ReturnToIdle(float duration, Action onComplete = null)
        {
            var idlePose = _poseManager.GetPose("idle");
            if (idlePose == null)
            {
                Debug.WriteLine("[ActionAnimator] [CRITICAL] 'idle.json' pose not found!");
                // Fallback to a hardcoded idle position if the file is missing
                var anchors = AnimationAnchorCalculator.CalculateAnchors(false, out _);
                _leftHand.MoveTo(anchors["LeftHandIdle"], duration, Easing.EaseOutCubic);
                _rightHand.MoveTo(anchors["RightHandIdle"], duration, Easing.EaseOutCubic);
                _leftHand.RotateTo(0, duration, Easing.EaseOutCubic);
                _rightHand.RotateTo(0, duration, Easing.EaseOutCubic);
                _leftHand.ScaleTo(1f, duration, Easing.EaseOutCubic);
                _rightHand.ScaleTo(1f, duration, Easing.EaseOutCubic);
            }
            else
            {
                TransitionToPose(null, "idle", duration, onComplete);
            }
        }

        private void ApplyHandState(HandRenderer hand, HandState state, float duration)
        {
            if (state == null) return;
            hand.MoveTo(state.Position, duration, Easing.EaseOutCubic);
            hand.RotateTo(MathHelper.ToRadians(state.Rotation), duration, Easing.EaseOutCubic);
            hand.ScaleTo(state.Scale, duration, Easing.EaseOutCubic);
            if (!string.IsNullOrEmpty(state.AnimationName))
            {
                hand.PlayAnimation(state.AnimationName);
            }
        }

        private void InitializeHoldEmitters(PoseData pose)
        {
            StopHoldEmitters();
            if (pose.ParticleAnchor == ParticleAnchorType.Nowhere || string.IsNullOrEmpty(pose.ParticleEffectName))
            {
                return;
            }

            var settingsList = ParticleEffectRegistry.CreateEffect(pose.ParticleEffectName);
            if (settingsList == null)
            {
                Debug.WriteLine($"[ActionAnimator] [WARNING] Could not find particle effect named '{pose.ParticleEffectName}'.");
                return;
            }

            Action<ParticleAnchorType> createForAnchor = (anchorPoint) =>
            {
                foreach (var settings in settingsList)
                {
                    var emitter = _particleSystemManager.CreateEmitter(settings);
                    emitter.IsActive = false; // Emitter starts inactive
                    emitter.EmissionStrength = 0f; // Emitter starts with zero strength
                    _activeEmitters.Add(new HoldEmitter { Emitter = emitter, Anchor = anchorPoint, ActivationTimer = 0f });
                }
            };

            if (pose.ParticleAnchor == ParticleAnchorType.LeftHand || pose.ParticleAnchor == ParticleAnchorType.BothHands)
                createForAnchor(ParticleAnchorType.LeftHand);
            if (pose.ParticleAnchor == ParticleAnchorType.RightHand || pose.ParticleAnchor == ParticleAnchorType.BothHands)
                createForAnchor(ParticleAnchorType.RightHand);
            if (pose.ParticleAnchor == ParticleAnchorType.BetweenHands)
                createForAnchor(ParticleAnchorType.BetweenHands);
        }

        private void UpdateHoldEmitters(GameTime gameTime)
        {
            if (!_activeEmitters.Any()) return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var holdEmitter in _activeEmitters)
            {
                // Update position
                Vector2 newPos;
                switch (holdEmitter.Anchor)
                {
                    case ParticleAnchorType.LeftHand:
                        newPos = _leftHand.GetPalmPosition();
                        break;
                    case ParticleAnchorType.RightHand:
                        newPos = _rightHand.GetPalmPosition();
                        break;
                    case ParticleAnchorType.BetweenHands:
                        newPos = Vector2.Lerp(_leftHand.GetPalmPosition(), _rightHand.GetPalmPosition(), 0.5f);
                        break;
                    default:
                        newPos = Vector2.Zero;
                        break;
                }
                holdEmitter.Emitter.Position = newPos;

                // Once positioned, activate the emitter so it can start updating.
                if (!holdEmitter.Emitter.IsActive)
                {
                    holdEmitter.Emitter.IsActive = true;
                }

                // Ramp up emission strength
                if (holdEmitter.ActivationTimer < PARTICLE_RAMP_UP_DURATION)
                {
                    holdEmitter.ActivationTimer += deltaTime;
                    float progress = Math.Clamp(holdEmitter.ActivationTimer / PARTICLE_RAMP_UP_DURATION, 0f, 1f);
                    holdEmitter.Emitter.EmissionStrength = Easing.EaseOutCubic(progress);
                }
                else
                {
                    holdEmitter.Emitter.EmissionStrength = 1f;
                }
            }
        }

        public void StopHoldEmitters()
        {
            foreach (var holdEmitter in _activeEmitters)
            {
                _particleSystemManager.DestroyEmitter(holdEmitter.Emitter);
            }
            _activeEmitters.Clear();
        }
    }
}
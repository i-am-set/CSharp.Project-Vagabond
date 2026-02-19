using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public class BattleAnimationManager
    {
        private const float INDICATOR_COOLDOWN = 0.1f;
        private const float INDICATOR_MAX_ROTATION_SPEED = 0.25f;

        public class AlphaAnimationState { public string CombatantID; public float StartAlpha; public float TargetAlpha; public float Timer; public const float Duration = 0.1f; }

        public Func<BattleCombatant, Vector2> GetCombatantPosition { get; set; }

        public class HudEntryAnimationState
        {
            public string CombatantID;
            public float Timer;
            public const float DURATION = 0.5f;
        }
        private readonly List<HudEntryAnimationState> _activeHudEntryAnimations = new List<HudEntryAnimationState>();

        public class DeathAnimationState
        {
            public string CombatantID;
            public float Timer;
            public enum Phase { FlashWhite1, FlashGray, FlashWhite2, FadeOut }
            public Phase CurrentPhase;

            public const float FLASH_DURATION = 0.05f;
            public const float FADE_DURATION = 0.15f;
        }

        public class SpawnAnimationState
        {
            public string CombatantID;
            public float Timer;
            public enum Phase { Flash, FadeIn }
            public Phase CurrentPhase;

            public const float FLASH_DURATION = 0.2f;
            public const float FLASH_INTERVAL = 0.05f;
            public const float FADE_DURATION = 0.3f;
            public const float DROP_HEIGHT = 10f;
        }

        public class AttackChargeAnimationState
        {
            public enum Phase { Windup, Lunge }
            public Phase CurrentPhase = Phase.Windup;

            public string CombatantID;
            public bool IsPlayer;

            public Vector2 Scale = Vector2.One;
            public Vector2 Offset = Vector2.Zero;
        }
        private readonly List<AttackChargeAnimationState> _activeAttackCharges = new List<AttackChargeAnimationState>();

        public class IntroFadeAnimationState
        {
            public string CombatantID;
            public float Timer;
            public const float FADE_DURATION = 1.5f;
        }
        private readonly List<IntroFadeAnimationState> _activeIntroFadeAnimations = new List<IntroFadeAnimationState>();

        public class FloorIntroAnimationState
        {
            public string ID;
            public float Timer;
            public const float DURATION = 1.5f;
        }
        private readonly List<FloorIntroAnimationState> _activeFloorIntroAnimations = new List<FloorIntroAnimationState>();

        public class FloorOutroAnimationState
        {
            public string ID;
            public float Timer;
            public const float DURATION = 0.3f;
        }
        private readonly List<FloorOutroAnimationState> _activeFloorOutroAnimations = new List<FloorOutroAnimationState>();

        public class SwitchOutAnimationState
        {
            public string CombatantID;
            public bool IsEnemy;
            public float Timer;

            public enum Phase { Silhouetting, Lifting }
            public Phase CurrentPhase;
            public float SilhouetteTimer;
            public float LiftTimer;

            public const float SILHOUETTE_DURATION = 0.25f;
            public const float LIFT_DURATION = 0.25f;
            public const float LIFT_HEIGHT = 150f;

            public const float DURATION = BattleConstants.SWITCH_ANIMATION_DURATION;
            public const float SIMPLE_LIFT_HEIGHT = BattleConstants.SWITCH_VERTICAL_OFFSET;
        }

        public class SwitchInAnimationState
        {
            public string CombatantID;
            public float Timer;
            public const float DURATION = BattleConstants.SWITCH_ANIMATION_DURATION;
            public const float DROP_HEIGHT = BattleConstants.SWITCH_VERTICAL_OFFSET;
        }

        public class HitFlashAnimationState
        {
            public string CombatantID;
            public float Timer;
            public int FlashesRemaining;
            public bool IsCurrentlyWhite;
            public Vector2 ShakeOffset;
            public float ShakeTimer;

            public const int TOTAL_FLASHES = 4;
            public const float FLASH_ON_DURATION = 0.03f;
            public const float FLASH_OFF_DURATION = 0.03f;
            public const float TOTAL_FLASH_CYCLE_DURATION = FLASH_ON_DURATION + FLASH_OFF_DURATION;

            public const float SHAKE_LEFT_DURATION = 0.03f;
            public const float SHAKE_RIGHT_DURATION = 0.03f;
            public const float SHAKE_SETTLE_DURATION = 0.03f;
            public const float TOTAL_SHAKE_DURATION = SHAKE_LEFT_DURATION + SHAKE_RIGHT_DURATION + SHAKE_SETTLE_DURATION;
            public const float SHAKE_MAGNITUDE = 2f;
        }
        public class HealBounceAnimationState { public string CombatantID; public float Timer; public const float Duration = 0.2f; public const float Height = 5f; }
        public class HealFlashAnimationState { public string CombatantID; public float Timer; public const float Duration = 0.3f; }
        public class PoisonEffectAnimationState { public string CombatantID; public float Timer; public const float Duration = 1.0f; }

        public class ResourceBarAnimationState
        {
            public enum BarResourceType { HP }
            public enum BarAnimationType { Loss, Recovery }
            public enum LossPhase { Preview, FlashBlack, FlashWhite, Shrink }
            public enum RecoveryPhase { Hang, Fade }

            public string CombatantID;
            public BarResourceType ResourceType;
            public BarAnimationType AnimationType;
            public LossPhase CurrentLossPhase;
            public RecoveryPhase CurrentRecoveryPhase;

            public float ValueBefore;
            public float ValueAfter;

            public float Timer;

            public const float PREVIEW_DURATION = 0.6f;
            public const float FLASH_BLACK_DURATION = 0.05f;
            public const float FLASH_WHITE_DURATION = 0.05f;
            public const float SHRINK_DURATION = 0.25f;
        }

        public class AbilityIndicatorState
        {
            public enum AnimationPhase { EasingIn, Flashing, Holding, EasingOut }
            public AnimationPhase Phase;
            public string CombatantID;
            public string OriginalText;
            public string Text;
            public int Count;
            public Vector2 InitialPosition;

            public Vector2 CurrentPosition;
            public Vector2 Velocity;

            public float Rotation;
            public float RotationSpeed;

            public float Timer;
            public float ShakeTimer;

            public const float SHAKE_DURATION = 0.3f;
            public const float SHAKE_MAGNITUDE = 4.0f;
            public const float SHAKE_FREQUENCY = 15f;

            public const float EASE_IN_DURATION = 0.1f;
            public const float FLASH_DURATION = 0.1f;
            public const float HOLD_DURATION = 1.0f;
            public const float EASE_OUT_DURATION = 0.2f;
            public const float TOTAL_DURATION = EASE_IN_DURATION + FLASH_DURATION + HOLD_DURATION + EASE_OUT_DURATION;
        }

        private struct PendingAbilityIndicator
        {
            public string CombatantID;
            public string Text;
            public Vector2 StartPosition;
        }

        private readonly Queue<PendingAbilityIndicator> _pendingAbilityQueue = new Queue<PendingAbilityIndicator>();
        private float _abilitySpawnTimer = 0f;

        private const float ABILITY_SPAWN_INTERVAL = 0.4f;
        private const float ABILITY_FLOAT_SPEED_INITIAL = 15f;
        private const float ABILITY_FLOAT_DRAG = 1.5f;
        private const float ABILITY_DRIFT_RANGE = 10f;
        private const float ABILITY_ROTATION_SPEED_MAX = 0.15f;
        private const float ABILITY_ROTATION_DRAG = 2.0f;

        public class DamageIndicatorState
        {
            public enum IndicatorType { Text, Number, HealNumber, EmphasizedNumber, Effectiveness, StatChange, Protected, Failed }
            public IndicatorType Type;
            public string CombatantID;
            public string PrimaryText;
            public string? SecondaryText;
            public string? TertiaryText;
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector2 InitialPosition;
            public Color PrimaryColor;
            public Color? SecondaryColor;
            public Color? TertiaryColor;
            public float Timer;
            public const float DURATION = 1.8f;
            public const float RISE_DISTANCE = 3f;

            // Physics
            public float Rotation;
            public float RotationVelocity;
        }

        public class HitstopVisualState
        {
            public string CombatantID;
            public bool IsCrit;
        }
        private readonly List<HitstopVisualState> _activeHitstopVisuals = new List<HitstopVisualState>();

        public class ImpactFlashState
        {
            public float Timer;
            public float Duration;
            public Color Color;
            public List<string> TargetCombatantIDs = new List<string>();
        }
        private ImpactFlashState? _impactFlashState;

        private readonly List<AlphaAnimationState> _activeAlphaAnimations = new List<AlphaAnimationState>();
        private readonly List<DeathAnimationState> _activeDeathAnimations = new List<DeathAnimationState>();
        private readonly List<SpawnAnimationState> _activeSpawnAnimations = new List<SpawnAnimationState>();
        private readonly List<SwitchOutAnimationState> _activeSwitchOutAnimations = new List<SwitchOutAnimationState>();
        private readonly List<SwitchInAnimationState> _activeSwitchInAnimations = new List<SwitchInAnimationState>();
        private readonly List<HitFlashAnimationState> _activeHitFlashAnimations = new List<HitFlashAnimationState>();
        private readonly List<HealBounceAnimationState> _activeHealBounceAnimations = new List<HealBounceAnimationState>();
        private readonly List<HealFlashAnimationState> _activeHealFlashAnimations = new List<HealFlashAnimationState>();
        private readonly List<PoisonEffectAnimationState> _activePoisonEffectAnimations = new List<PoisonEffectAnimationState>();
        private readonly List<DamageIndicatorState> _activeDamageIndicators = new List<DamageIndicatorState>();
        private readonly List<ResourceBarAnimationState> _activeBarAnimations = new List<ResourceBarAnimationState>();

        private readonly List<AbilityIndicatorState> _activeAbilityIndicators = new List<AbilityIndicatorState>();

        private readonly Queue<Action> _pendingTextIndicators = new Queue<Action>();
        private float _indicatorCooldownTimer = 0f;


        private readonly Random _random = new Random();
        private readonly Global _global;

        private const int DIVIDER_Y = 123;

        public bool IsBlockingAnimation =>
            _activeAlphaAnimations.Any() ||
            _activeDeathAnimations.Any() ||
            _activeSpawnAnimations.Any() ||
            _activeSwitchOutAnimations.Any() ||
            _activeSwitchInAnimations.Any() ||
            _activeIntroFadeAnimations.Any() ||
            _activeFloorIntroAnimations.Any() ||
            _activeFloorOutroAnimations.Any(a => a.Timer < FloorOutroAnimationState.DURATION) ||
            _activeAttackCharges.Any();

        public bool IsAnimating => IsBlockingAnimation;

        public bool IsVisuallyBusy =>
            IsBlockingAnimation ||
            _activeDamageIndicators.Any() ||
            _activeAbilityIndicators.Any();

        public BattleAnimationManager()
        {
            _global = ServiceLocator.Get<Global>();
            EventBus.Subscribe<GameEvents.BattleActionExecuted>(OnActionExecuted);
        }

        private void OnActionExecuted(GameEvents.BattleActionExecuted e)
        {
            // Target Animations (Hit Flash / Shake)
            if (e.Targets != null)
            {
                foreach (var target in e.Targets)
                {
                    StartHitFlashAnimation(target.CombatantID);
                }
            }
        }

        public void Reset()
        {
            ForceClearAll();
        }

        public void ForceClearAll()
        {
            _activeAlphaAnimations.Clear();
            _activeDeathAnimations.Clear();
            _activeSpawnAnimations.Clear();
            _activeSwitchOutAnimations.Clear();
            _activeSwitchInAnimations.Clear();
            _activeHitFlashAnimations.Clear();
            _activeHealBounceAnimations.Clear();
            _activeHealFlashAnimations.Clear();
            _activePoisonEffectAnimations.Clear();
            _activeDamageIndicators.Clear();
            _activeAbilityIndicators.Clear();
            _activeBarAnimations.Clear();
            _pendingTextIndicators.Clear();
            _activeHitstopVisuals.Clear();
            _activeIntroFadeAnimations.Clear();
            _activeFloorIntroAnimations.Clear();
            _activeFloorOutroAnimations.Clear();
            _activeAttackCharges.Clear();
            _activeHudEntryAnimations.Clear();
            _impactFlashState = null;
            _indicatorCooldownTimer = 0f;
            _pendingAbilityQueue.Clear();
            _abilitySpawnTimer = 0f;
        }

        public void CompleteBlockingAnimations(IEnumerable<BattleCombatant> combatants)
        {
            foreach (var c in combatants)
            {
                c.VisualHP = c.Stats.CurrentHP;
            }
            _activeBarAnimations.Clear();

            foreach (var anim in _activeAlphaAnimations)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    combatant.VisualAlpha = anim.TargetAlpha;
                }
            }
            _activeAlphaAnimations.Clear();

            foreach (var anim in _activeIntroFadeAnimations)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    combatant.VisualAlpha = 1.0f;
                }
            }
            _activeIntroFadeAnimations.Clear();
            _activeSwitchInAnimations.Clear();
            _activeSwitchOutAnimations.Clear();
            _activeFloorIntroAnimations.Clear();
            _activeFloorOutroAnimations.Clear();
            _activeAttackCharges.Clear();

            foreach (var anim in _activeDeathAnimations)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    combatant.VisualAlpha = 0f;
                }
            }
            _activeDeathAnimations.Clear();

            foreach (var anim in _activeHudEntryAnimations)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    combatant.HudVisualAlpha = 1.0f;
                }
            }
            _activeHudEntryAnimations.Clear();

            _activeSpawnAnimations.Clear();
        }

        public void StartWindup(string combatantId, bool isPlayer)
        {
            _activeAttackCharges.RemoveAll(a => a.CombatantID == combatantId);
            _activeAttackCharges.Add(new AttackChargeAnimationState
            {
                CombatantID = combatantId,
                IsPlayer = isPlayer,
                CurrentPhase = AttackChargeAnimationState.Phase.Windup
            });
        }

        public void TriggerLunge(string combatantId)
        {
            var anim = _activeAttackCharges.FirstOrDefault(a => a.CombatantID == combatantId);
            if (anim != null)
            {
                anim.CurrentPhase = AttackChargeAnimationState.Phase.Lunge;

                // Snap to lunge pose immediately on trigger
                float dir = anim.IsPlayer ? 1f : -1f;
                anim.Offset = new Vector2(20f * dir, 0f);
                anim.Scale = new Vector2(1.4f, 0.6f);
            }
        }

        public void ReleaseAttackCharge(string combatantId, float timeToImpact)
        {
            // No-op or logic if needed for timer-based resets
            // Since we use explicit TriggerLunge now, this can be empty or redirect
            TriggerLunge(combatantId);
        }

        public void TriggerInstantAttack(string combatantId, bool isPlayer)
        {
            // Fallback for instant attacks without windup
            StartWindup(combatantId, isPlayer);
            TriggerLunge(combatantId);
        }

        public AttackChargeAnimationState GetAttackChargeState(string combatantId)
        {
            return _activeAttackCharges.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public void StartHitstopVisuals(string combatantId, bool isCrit)
        {
            _activeHitstopVisuals.RemoveAll(v => v.CombatantID == combatantId);
            _activeHitstopVisuals.Add(new HitstopVisualState { CombatantID = combatantId, IsCrit = isCrit });
        }

        public HitstopVisualState GetHitstopVisualState(string combatantId)
        {
            return _activeHitstopVisuals.FirstOrDefault(v => v.CombatantID == combatantId);
        }

        public void ClearHitstopVisuals()
        {
            _activeHitstopVisuals.Clear();
        }

        public void TriggerImpactFlash(Color color, float duration, List<string> targetIds)
        {
            _impactFlashState = new ImpactFlashState
            {
                Color = color,
                Duration = duration,
                Timer = duration,
                TargetCombatantIDs = targetIds
            };
        }

        public ImpactFlashState? GetImpactFlashState()
        {
            return _impactFlashState;
        }

        public void StartHealthLossAnimation(string combatantId, float hpBefore, float hpAfter)
        {
            _activeBarAnimations.RemoveAll(a => a.CombatantID == combatantId && a.ResourceType == ResourceBarAnimationState.BarResourceType.HP);
            _activeBarAnimations.Add(new ResourceBarAnimationState
            {
                CombatantID = combatantId,
                ResourceType = ResourceBarAnimationState.BarResourceType.HP,
                AnimationType = ResourceBarAnimationState.BarAnimationType.Loss,
                CurrentLossPhase = ResourceBarAnimationState.LossPhase.Preview,
                ValueBefore = hpBefore,
                ValueAfter = hpAfter,
                Timer = 0f
            });
        }

        public void StartHealthRecoveryAnimation(string combatantId, float hpBefore, float hpAfter)
        {
            _activeBarAnimations.RemoveAll(a => a.CombatantID == combatantId && a.ResourceType == ResourceBarAnimationState.BarResourceType.HP);
            _activeBarAnimations.Add(new ResourceBarAnimationState
            {
                CombatantID = combatantId,
                ResourceType = ResourceBarAnimationState.BarResourceType.HP,
                AnimationType = ResourceBarAnimationState.BarAnimationType.Recovery,
                CurrentRecoveryPhase = ResourceBarAnimationState.RecoveryPhase.Hang,
                ValueBefore = hpBefore,
                ValueAfter = hpAfter,
                Timer = 0f
            });
        }

        public void StartAlphaAnimation(string combatantId, float alphaBefore, float alphaAfter)
        {
            _activeAlphaAnimations.Add(new AlphaAnimationState
            {
                CombatantID = combatantId,
                StartAlpha = alphaBefore,
                TargetAlpha = alphaAfter,
                Timer = 0f
            });
        }

        public void StartHudEntryAnimation(string combatantId)
        {
            _activeHudEntryAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeHudEntryAnimations.Add(new HudEntryAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        public void StartDeathAnimation(string combatantId)
        {
            // Safety: Clear any pending attack charges or hit flashes for this unit
            // This prevents softlocks if a unit dies mid-attack (e.g. recoil)
            _activeAttackCharges.RemoveAll(a => a.CombatantID == combatantId);
            _activeHitFlashAnimations.RemoveAll(a => a.CombatantID == combatantId);

            _activeDeathAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeDeathAnimations.Add(new DeathAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f,
                CurrentPhase = DeathAnimationState.Phase.FlashWhite1,
            });
        }

        public bool IsDeathAnimating(string combatantId)
        {
            return _activeDeathAnimations.Any(a => a.CombatantID == combatantId);
        }

        public void StartSpawnAnimation(string combatantId)
        {
            _activeSpawnAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeSpawnAnimations.Add(new SpawnAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f,
                CurrentPhase = SpawnAnimationState.Phase.Flash
            });
        }

        public void StartIntroFadeAnimation(string combatantId)
        {
            _activeIntroFadeAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeIntroFadeAnimations.Add(new IntroFadeAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        public IntroFadeAnimationState GetIntroFadeAnimationState(string combatantId)
        {
            return _activeIntroFadeAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public void StartFloorIntroAnimation(string id)
        {
            _activeFloorIntroAnimations.RemoveAll(a => a.ID == id);
            _activeFloorIntroAnimations.Add(new FloorIntroAnimationState
            {
                ID = id,
                Timer = 0f
            });
        }

        public FloorIntroAnimationState GetFloorIntroAnimationState(string id)
        {
            return _activeFloorIntroAnimations.FirstOrDefault(a => a.ID == id);
        }

        public void StartFloorOutroAnimation(string id)
        {
            _activeFloorOutroAnimations.RemoveAll(a => a.ID == id);
            _activeFloorOutroAnimations.Add(new FloorOutroAnimationState
            {
                ID = id,
                Timer = 0f
            });
        }

        public FloorOutroAnimationState GetFloorOutroAnimationState(string id)
        {
            return _activeFloorOutroAnimations.FirstOrDefault(a => a.ID == id);
        }

        public bool IsFloorAnimatingOut(string id)
        {
            return _activeFloorOutroAnimations.Any(a => a.ID == id && a.Timer < FloorOutroAnimationState.DURATION);
        }

        public void StartSwitchOutAnimation(string combatantId, bool isEnemy)
        {
            // Safety: Clear attack charges for switching unit
            _activeAttackCharges.RemoveAll(a => a.CombatantID == combatantId);

            _activeSwitchOutAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeSwitchOutAnimations.Add(new SwitchOutAnimationState
            {
                CombatantID = combatantId,
                IsEnemy = isEnemy,
                Timer = 0f,
                CurrentPhase = SwitchOutAnimationState.Phase.Silhouetting,
                SilhouetteTimer = 0f,
                LiftTimer = 0f
            });
        }

        public void StartSwitchInAnimation(string combatantId)
        {
            _activeSwitchInAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeSwitchInAnimations.Add(new SwitchInAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        public void StartHitFlashAnimation(string combatantId)
        {
            _activeHitFlashAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeHitFlashAnimations.Add(new HitFlashAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f,
                FlashesRemaining = HitFlashAnimationState.TOTAL_FLASHES,
                IsCurrentlyWhite = true,
                ShakeTimer = 0f,
                ShakeOffset = Vector2.Zero
            });
        }

        public void StartHealBounceAnimation(string combatantId)
        {
            _activeHealBounceAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeHealBounceAnimations.Add(new HealBounceAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        public void StartHealFlashAnimation(string combatantId)
        {
            _activeHealFlashAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeHealFlashAnimations.Add(new HealFlashAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        public void StartPoisonEffectAnimation(string combatantId)
        {
            _activePoisonEffectAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activePoisonEffectAnimations.Add(new PoisonEffectAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        public void StartAbilityIndicator(string combatantId, string abilityName, Vector2 startPosition)
        {
            string text = abilityName.ToUpper();

            var existingIndicator = _activeAbilityIndicators.FirstOrDefault(ind => ind.CombatantID == combatantId && ind.OriginalText == text);

            if (existingIndicator != null)
            {
                existingIndicator.Count++;
                existingIndicator.Text = $"{existingIndicator.OriginalText} x{existingIndicator.Count}";
                existingIndicator.Timer = 0f;
                existingIndicator.Phase = AbilityIndicatorState.AnimationPhase.EasingIn;
                existingIndicator.ShakeTimer = AbilityIndicatorState.SHAKE_DURATION;
                existingIndicator.CurrentPosition = startPosition;
                existingIndicator.InitialPosition = startPosition;
                return;
            }

            var indicator = new AbilityIndicatorState
            {
                CombatantID = combatantId,
                OriginalText = text,
                Text = text,
                Count = 1,
                Timer = 0f,
                Phase = AbilityIndicatorState.AnimationPhase.EasingIn,
                ShakeTimer = AbilityIndicatorState.SHAKE_DURATION,
                InitialPosition = startPosition,
                CurrentPosition = startPosition,
                Velocity = new Vector2(
                    (float)(_random.NextDouble() * ABILITY_DRIFT_RANGE * 2 - ABILITY_DRIFT_RANGE),
                    -ABILITY_FLOAT_SPEED_INITIAL
                ),
                Rotation = 0f,
                RotationSpeed = (float)(_random.NextDouble() * ABILITY_ROTATION_SPEED_MAX * 2 - ABILITY_ROTATION_SPEED_MAX)
            };

            _activeAbilityIndicators.Add(indicator);
        }

        private float GetRandomIndicatorRotation()
        {
            return (float)(_random.NextDouble() * (INDICATOR_MAX_ROTATION_SPEED * 2) - INDICATOR_MAX_ROTATION_SPEED);
        }

        public void StartDamageIndicator(string combatantId, string text, Vector2 startPosition, Color color)
        {
            _pendingTextIndicators.Enqueue(() =>
            {
                _activeDamageIndicators.Add(new DamageIndicatorState
                {
                    Type = DamageIndicatorState.IndicatorType.Text,
                    CombatantID = combatantId,
                    PrimaryText = text,
                    Position = startPosition,
                    InitialPosition = startPosition,
                    PrimaryColor = color,
                    Timer = 0f,
                    Rotation = 0f,
                    RotationVelocity = GetRandomIndicatorRotation()
                });
            });
        }

        public void StartProtectedIndicator(string combatantId, Vector2 startPosition)
        {
            _pendingTextIndicators.Enqueue(() =>
            {
                _activeDamageIndicators.Add(new DamageIndicatorState
                {
                    Type = DamageIndicatorState.IndicatorType.Protected,
                    CombatantID = combatantId,
                    PrimaryText = "PROTECTED",
                    Position = startPosition,
                    InitialPosition = startPosition,
                    Timer = 0f,
                    Rotation = 0f,
                    RotationVelocity = GetRandomIndicatorRotation()
                });
            });
        }

        public void StartFailedIndicator(string combatantId, Vector2 startPosition)
        {
            _pendingTextIndicators.Enqueue(() =>
            {
                _activeDamageIndicators.Add(new DamageIndicatorState
                {
                    Type = DamageIndicatorState.IndicatorType.Failed,
                    CombatantID = combatantId,
                    PrimaryText = "FAILED",
                    Position = startPosition,
                    InitialPosition = startPosition,
                    Timer = 0f,
                    Rotation = 0f,
                    RotationVelocity = GetRandomIndicatorRotation()
                });
            });
        }

        public void StartEffectivenessIndicator(string combatantId, string text, Vector2 startPosition)
        {
            _pendingTextIndicators.Enqueue(() =>
            {
                _activeDamageIndicators.Add(new DamageIndicatorState
                {
                    Type = DamageIndicatorState.IndicatorType.Effectiveness,
                    CombatantID = combatantId,
                    PrimaryText = text,
                    Position = startPosition,
                    InitialPosition = startPosition,
                    Timer = 0f,
                    Rotation = 0f,
                    RotationVelocity = GetRandomIndicatorRotation()
                });
            });
        }

        public void StartDamageNumberIndicator(string combatantId, int damageAmount, Vector2 startPosition)
        {
            _activeDamageIndicators.Add(new DamageIndicatorState
            {
                Type = DamageIndicatorState.IndicatorType.Number,
                CombatantID = combatantId,
                PrimaryText = damageAmount.ToString(),
                Position = startPosition,
                InitialPosition = startPosition,
                Velocity = new Vector2((float)(_random.NextDouble() * 60 - 30), -110f),
                Timer = 0f,
                Rotation = 0f,
                RotationVelocity = GetRandomIndicatorRotation()
            });
        }

        public void StartEmphasizedDamageNumberIndicator(string combatantId, int damageAmount, Vector2 startPosition)
        {
            _activeDamageIndicators.Add(new DamageIndicatorState
            {
                Type = DamageIndicatorState.IndicatorType.EmphasizedNumber,
                CombatantID = combatantId,
                PrimaryText = damageAmount.ToString(),
                Position = startPosition,
                InitialPosition = startPosition,
                Velocity = new Vector2((float)(_random.NextDouble() * 80 - 40), -150f),
                Timer = 0f,
                Rotation = 0f,
                RotationVelocity = GetRandomIndicatorRotation()
            });
        }

        public void StartHealNumberIndicator(string combatantId, int healAmount, Vector2 startPosition)
        {
            _activeDamageIndicators.Add(new DamageIndicatorState
            {
                Type = DamageIndicatorState.IndicatorType.HealNumber,
                CombatantID = combatantId,
                PrimaryText = healAmount.ToString(),
                Position = startPosition,
                InitialPosition = startPosition,
                Velocity = new Vector2((float)(_random.NextDouble() * 60 - 30), -110f),
                Timer = 0f,
                Rotation = 0f,
                RotationVelocity = GetRandomIndicatorRotation()
            });
        }

        public void StartStatStageIndicator(string combatantId, string prefixText, string statText, string suffixText, Color prefixColor, Color statColor, Color suffixColor, Vector2 startPosition)
        {
            _pendingTextIndicators.Enqueue(() =>
            {
                _activeDamageIndicators.Add(new DamageIndicatorState
                {
                    Type = DamageIndicatorState.IndicatorType.StatChange,
                    CombatantID = combatantId,
                    PrimaryText = prefixText,
                    SecondaryText = statText,
                    TertiaryText = suffixText,
                    Position = startPosition,
                    InitialPosition = startPosition,
                    PrimaryColor = prefixColor,
                    SecondaryColor = statColor,
                    TertiaryColor = suffixColor,
                    Timer = 0f,
                    Rotation = 0f,
                    RotationVelocity = GetRandomIndicatorRotation()
                });
            });
        }

        public void SkipAllHealthAnimations(IEnumerable<BattleCombatant> combatants)
        {
            foreach (var c in combatants)
            {
                c.VisualHP = c.Stats.CurrentHP;
            }
        }

        public void SkipAllBarAnimations()
        {
            _activeBarAnimations.Clear();
        }

        public HitFlashAnimationState GetHitFlashState(string combatantId)
        {
            return _activeHitFlashAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public HealBounceAnimationState GetHealBounceAnimationState(string combatantId)
        {
            return _activeHealBounceAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public HealFlashAnimationState GetHealFlashAnimationState(string combatantId)
        {
            return _activeHealFlashAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public PoisonEffectAnimationState GetPoisonEffectAnimationState(string combatantId)
        {
            return _activePoisonEffectAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public SpawnAnimationState GetSpawnAnimationState(string combatantId)
        {
            return _activeSpawnAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public SwitchOutAnimationState GetSwitchOutAnimationState(string combatantId)
        {
            return _activeSwitchOutAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public SwitchInAnimationState GetSwitchInAnimationState(string combatantId)
        {
            return _activeSwitchInAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public ResourceBarAnimationState? GetResourceBarAnimation(string combatantId, ResourceBarAnimationState.BarResourceType type)
        {
            return _activeBarAnimations.FirstOrDefault(a => a.CombatantID == combatantId && a.ResourceType == type);
        }

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants, float timeScale = 1.0f)
        {
            double scaledSeconds = gameTime.ElapsedGameTime.TotalSeconds * timeScale;
            var scaledGameTime = new GameTime(gameTime.TotalGameTime, TimeSpan.FromSeconds(scaledSeconds));

            UpdateIndicatorQueue(scaledGameTime);
            UpdateHealthAnimations(scaledGameTime, combatants);
            UpdateAlphaAnimations(scaledGameTime, combatants);
            UpdateDeathAnimations(scaledGameTime, combatants);
            UpdateSpawnAnimations(scaledGameTime, combatants);
            UpdateIntroFadeAnimations(scaledGameTime, combatants);
            UpdateFloorIntroAnimations(scaledGameTime);
            UpdateFloorOutroAnimations(scaledGameTime);
            UpdateSwitchAnimations(scaledGameTime, combatants);
            UpdateHitFlashAnimations(scaledGameTime);
            UpdateHealAnimations(scaledGameTime);
            UpdatePoisonEffectAnimations(scaledGameTime);
            UpdateDamageIndicators(scaledGameTime);
            UpdateAbilityIndicators(scaledGameTime);
            UpdateBarAnimations(scaledGameTime);
            UpdateImpactFlash(scaledGameTime);
            UpdateAttackCharges(scaledGameTime, combatants);
            UpdateHudEntryAnimations(scaledGameTime, combatants);
        }

        private void UpdateHudEntryAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeHudEntryAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHudEntryAnimations[i];
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeHudEntryAnimations.RemoveAt(i);
                    continue;
                }

                anim.Timer += dt;
                float progress = Math.Clamp(anim.Timer / HudEntryAnimationState.DURATION, 0f, 1f);
                combatant.HudVisualAlpha = Easing.EaseOutQuad(progress);

                if (progress >= 1.0f)
                {
                    _activeHudEntryAnimations.RemoveAt(i);
                }
            }
        }

        private void UpdateAttackCharges(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeAttackCharges.Count - 1; i >= 0; i--)
            {
                var anim = _activeAttackCharges[i];

                // Safety: If combatant is gone or dead, remove animation
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null || combatant.IsDefeated || combatant.Stats.CurrentHP <= 0)
                {
                    _activeAttackCharges.RemoveAt(i);
                    continue;
                }

                float dir = anim.IsPlayer ? 1f : -1f;

                if (anim.CurrentPhase == AttackChargeAnimationState.Phase.Windup)
                {
                    // Lerp towards windup pose: Pulled back (-10) and Squashed (0.8, 1.2)
                    Vector2 targetOffset = new Vector2(-10f * dir, 0f);
                    Vector2 targetScale = new Vector2(0.8f, 1.2f);

                    float smooth = 1f - MathF.Exp(-10f * dt);
                    anim.Offset = Vector2.Lerp(anim.Offset, targetOffset, smooth);
                    anim.Scale = Vector2.Lerp(anim.Scale, targetScale, smooth);
                }
                else // Lunge
                {
                    // Decay towards neutral pose: (0, 0) and (1, 1)
                    float recoverySpeed = 10f;
                    float smooth = 1f - MathF.Exp(-recoverySpeed * dt);

                    anim.Offset = Vector2.Lerp(anim.Offset, Vector2.Zero, smooth);
                    anim.Scale = Vector2.Lerp(anim.Scale, Vector2.One, smooth);

                    // Remove when close enough to neutral
                    if (anim.Offset.LengthSquared() < 1f && Math.Abs(anim.Scale.X - 1f) < 0.01f)
                    {
                        _activeAttackCharges.RemoveAt(i);
                    }
                }
            }
        }

        private void UpdateImpactFlash(GameTime gameTime)
        {
            if (_impactFlashState != null)
            {
                _impactFlashState.Timer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_impactFlashState.Timer <= 0)
                {
                    _impactFlashState = null;
                }
            }
        }

        private void UpdateIndicatorQueue(GameTime gameTime)
        {
            if (_indicatorCooldownTimer > 0)
            {
                _indicatorCooldownTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            if (_indicatorCooldownTimer <= 0f && _pendingTextIndicators.Any())
            {
                var createAction = _pendingTextIndicators.Dequeue();
                createAction.Invoke();
                _indicatorCooldownTimer = INDICATOR_COOLDOWN;
            }
        }

        private void UpdateBarAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeBarAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeBarAnimations[i];
                anim.Timer += deltaTime;

                if (anim.AnimationType == ResourceBarAnimationState.BarAnimationType.Loss)
                {
                    switch (anim.CurrentLossPhase)
                    {
                        case ResourceBarAnimationState.LossPhase.Preview:
                            if (anim.Timer >= ResourceBarAnimationState.PREVIEW_DURATION)
                            {
                                anim.Timer = 0;
                                anim.CurrentLossPhase = ResourceBarAnimationState.LossPhase.FlashBlack;
                            }
                            break;
                        case ResourceBarAnimationState.LossPhase.FlashBlack:
                            if (anim.Timer >= ResourceBarAnimationState.FLASH_BLACK_DURATION)
                            {
                                anim.Timer = 0;
                                anim.CurrentLossPhase = ResourceBarAnimationState.LossPhase.FlashWhite;
                            }
                            break;
                        case ResourceBarAnimationState.LossPhase.FlashWhite:
                            if (anim.Timer >= ResourceBarAnimationState.FLASH_WHITE_DURATION)
                            {
                                anim.Timer = 0;
                                anim.CurrentLossPhase = ResourceBarAnimationState.LossPhase.Shrink;
                            }
                            break;
                        case ResourceBarAnimationState.LossPhase.Shrink:
                            if (anim.Timer >= ResourceBarAnimationState.SHRINK_DURATION)
                            {
                                _activeBarAnimations.RemoveAt(i);
                            }
                            break;
                    }
                }
                else // Recovery
                {
                    if (anim.CurrentRecoveryPhase == ResourceBarAnimationState.RecoveryPhase.Hang)
                    {
                        if (anim.Timer >= _global.HealOverlayHangDuration)
                        {
                            anim.Timer = 0;
                            anim.CurrentRecoveryPhase = ResourceBarAnimationState.RecoveryPhase.Fade;
                        }
                    }
                    else if (anim.CurrentRecoveryPhase == ResourceBarAnimationState.RecoveryPhase.Fade)
                    {
                        if (anim.Timer >= _global.HealOverlayFadeDuration)
                        {
                            _activeBarAnimations.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private void UpdateHealthAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            const float LERP_SPEED = 10f; // Tuned for "responsive but smooth"

            foreach (var combatant in combatants)
            {
                if (Math.Abs(combatant.VisualHP - combatant.Stats.CurrentHP) > 0.01f)
                {
                    combatant.VisualHP = MathHelper.Lerp(combatant.VisualHP, combatant.Stats.CurrentHP, 1f - MathF.Exp(-LERP_SPEED * dt));
                }
                else
                {
                    combatant.VisualHP = combatant.Stats.CurrentHP;
                }
            }
        }

        private void UpdateAlphaAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            for (int i = _activeAlphaAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeAlphaAnimations[i];
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeAlphaAnimations.RemoveAt(i);
                    continue;
                }

                anim.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (anim.Timer >= AlphaAnimationState.Duration)
                {
                    combatant.VisualAlpha = anim.TargetAlpha;
                    _activeAlphaAnimations.RemoveAt(i);
                }
                else
                {
                    float progress = anim.Timer / AlphaAnimationState.Duration;
                    combatant.VisualAlpha = MathHelper.Lerp(anim.StartAlpha, anim.TargetAlpha, Easing.EaseOutQuart(progress));
                }
            }
        }

        private void UpdateDeathAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeDeathAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeDeathAnimations[i];
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeDeathAnimations.RemoveAt(i);
                    continue;
                }

                combatant.VisualSilhouetteAmount = 1.0f;
                anim.Timer += deltaTime;

                switch (anim.CurrentPhase)
                {
                    case DeathAnimationState.Phase.FlashWhite1:
                        combatant.VisualSilhouetteColorOverride = Color.White;
                        if (anim.Timer >= DeathAnimationState.FLASH_DURATION)
                        {
                            anim.Timer = 0f;
                            anim.CurrentPhase = DeathAnimationState.Phase.FlashGray;
                        }
                        break;
                    case DeathAnimationState.Phase.FlashGray:
                        combatant.VisualSilhouetteColorOverride = ServiceLocator.Get<Global>().Palette_DarkShadow;
                        if (anim.Timer >= DeathAnimationState.FLASH_DURATION)
                        {
                            anim.Timer = 0f;
                            anim.CurrentPhase = DeathAnimationState.Phase.FlashWhite2;
                        }
                        break;
                    case DeathAnimationState.Phase.FlashWhite2:
                        combatant.VisualSilhouetteColorOverride = Color.White;
                        if (anim.Timer >= DeathAnimationState.FLASH_DURATION)
                        {
                            anim.Timer = 0f;
                            anim.CurrentPhase = DeathAnimationState.Phase.FadeOut;
                        }
                        break;
                    case DeathAnimationState.Phase.FadeOut:
                        combatant.VisualSilhouetteColorOverride = Color.White;
                        float progress = Math.Clamp(anim.Timer / DeathAnimationState.FADE_DURATION, 0f, 1f);
                        combatant.VisualAlpha = 1.0f - progress;

                        if (anim.Timer >= DeathAnimationState.FADE_DURATION)
                        {
                            combatant.VisualAlpha = 0f;
                            _activeDeathAnimations.RemoveAt(i);
                        }
                        break;
                }
            }
        }

        private void UpdateSpawnAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeSpawnAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeSpawnAnimations[i];
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeSpawnAnimations.RemoveAt(i);
                    continue;
                }

                anim.Timer += deltaTime;

                if (anim.CurrentPhase == SpawnAnimationState.Phase.Flash)
                {
                    int flashCycle = (int)(anim.Timer / SpawnAnimationState.FLASH_INTERVAL);
                    bool isVisible = flashCycle % 2 == 0;
                    combatant.VisualAlpha = 0f;
                    combatant.VisualSilhouetteAmount = 1.0f;
                    combatant.VisualSilhouetteColorOverride = isVisible ? Color.White : Color.Transparent;

                    if (anim.Timer >= SpawnAnimationState.FLASH_DURATION)
                    {
                        anim.Timer = 0f;
                        anim.CurrentPhase = SpawnAnimationState.Phase.FadeIn;
                    }
                }
                else if (anim.CurrentPhase == SpawnAnimationState.Phase.FadeIn)
                {
                    float progress = Math.Clamp(anim.Timer / SpawnAnimationState.FADE_DURATION, 0f, 1f);
                    float easedProgress = Easing.EaseOutQuad(progress);
                    combatant.VisualAlpha = easedProgress;
                    combatant.VisualSilhouetteAmount = 0f;
                    combatant.VisualSilhouetteColorOverride = null;

                    if (anim.Timer >= SpawnAnimationState.FADE_DURATION)
                    {
                        combatant.VisualAlpha = 1.0f;
                        _activeSpawnAnimations.RemoveAt(i);
                    }
                }
            }
        }

        private void UpdateIntroFadeAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeIntroFadeAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeIntroFadeAnimations[i];
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeIntroFadeAnimations.RemoveAt(i);
                    continue;
                }

                anim.Timer += deltaTime;
                float progress = Math.Clamp(anim.Timer / IntroFadeAnimationState.FADE_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutQuad(progress);

                combatant.VisualAlpha = easedProgress;
            }
        }

        private void UpdateFloorIntroAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeFloorIntroAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeFloorIntroAnimations[i];
                anim.Timer += deltaTime;
            }
        }

        private void UpdateFloorOutroAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeFloorOutroAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeFloorOutroAnimations[i];
                anim.Timer += deltaTime;
            }
        }

        private void UpdateSwitchAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeSwitchOutAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeSwitchOutAnimations[i];
                if (anim.IsEnemy)
                {
                    if (anim.CurrentPhase == SwitchOutAnimationState.Phase.Silhouetting)
                    {
                        anim.SilhouetteTimer += deltaTime;
                        if (anim.SilhouetteTimer >= SwitchOutAnimationState.SILHOUETTE_DURATION)
                        {
                            anim.CurrentPhase = SwitchOutAnimationState.Phase.Lifting;
                        }
                    }
                    else if (anim.CurrentPhase == SwitchOutAnimationState.Phase.Lifting)
                    {
                        anim.LiftTimer += deltaTime;
                        if (anim.LiftTimer >= SwitchOutAnimationState.LIFT_DURATION)
                        {
                            _activeSwitchOutAnimations.RemoveAt(i);
                        }
                    }
                }
                else
                {
                    anim.Timer += deltaTime;
                    if (anim.Timer >= SwitchOutAnimationState.DURATION)
                    {
                        _activeSwitchOutAnimations.RemoveAt(i);
                    }
                }
            }
            for (int i = _activeSwitchInAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeSwitchInAnimations[i];
                anim.Timer += deltaTime;
                if (anim.Timer >= SwitchInAnimationState.DURATION)
                {
                    _activeSwitchInAnimations.RemoveAt(i);
                }
            }
        }

        private void UpdateHitFlashAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeHitFlashAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHitFlashAnimations[i];
                anim.Timer += deltaTime;
                anim.ShakeTimer += deltaTime;

                if (anim.ShakeTimer < HitFlashAnimationState.TOTAL_SHAKE_DURATION)
                {
                    if (anim.ShakeTimer < HitFlashAnimationState.SHAKE_LEFT_DURATION)
                    {
                        float progress = anim.ShakeTimer / HitFlashAnimationState.SHAKE_LEFT_DURATION;
                        anim.ShakeOffset.X = MathHelper.Lerp(0, -HitFlashAnimationState.SHAKE_MAGNITUDE, Easing.EaseOutQuad(progress));
                    }
                    else if (anim.ShakeTimer < HitFlashAnimationState.SHAKE_LEFT_DURATION + HitFlashAnimationState.SHAKE_RIGHT_DURATION)
                    {
                        float phaseTimer = anim.ShakeTimer - HitFlashAnimationState.SHAKE_LEFT_DURATION;
                        float progress = phaseTimer / HitFlashAnimationState.SHAKE_RIGHT_DURATION;
                        anim.ShakeOffset.X = MathHelper.Lerp(-HitFlashAnimationState.SHAKE_MAGNITUDE, HitFlashAnimationState.SHAKE_MAGNITUDE, Easing.EaseInOutQuad(progress));
                    }
                    else
                    {
                        float phaseTimer = anim.ShakeTimer - (HitFlashAnimationState.SHAKE_LEFT_DURATION + HitFlashAnimationState.SHAKE_RIGHT_DURATION);
                        float progress = phaseTimer / HitFlashAnimationState.SHAKE_SETTLE_DURATION;
                        anim.ShakeOffset.X = MathHelper.Lerp(HitFlashAnimationState.SHAKE_MAGNITUDE, 0, Easing.EaseInQuad(progress));
                    }
                }
                else
                {
                    anim.ShakeOffset = Vector2.Zero;
                }

                if (anim.Timer >= HitFlashAnimationState.TOTAL_FLASH_CYCLE_DURATION)
                {
                    anim.Timer -= HitFlashAnimationState.TOTAL_FLASH_CYCLE_DURATION;
                    anim.FlashesRemaining--;
                    if (anim.FlashesRemaining <= 0)
                    {
                        _activeHitFlashAnimations.RemoveAt(i);
                        continue;
                    }
                }
                anim.IsCurrentlyWhite = anim.Timer < HitFlashAnimationState.FLASH_ON_DURATION;
            }
        }

        private void UpdateHealAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeHealBounceAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHealBounceAnimations[i];
                anim.Timer += deltaTime;
                if (anim.Timer >= HealBounceAnimationState.Duration)
                {
                    _activeHealBounceAnimations.RemoveAt(i);
                }
            }
            for (int i = _activeHealFlashAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHealFlashAnimations[i];
                anim.Timer += deltaTime;
                if (anim.Timer >= HealFlashAnimationState.Duration)
                {
                    _activeHealFlashAnimations.RemoveAt(i);
                }
            }
        }

        private void UpdatePoisonEffectAnimations(GameTime gameTime)
        {
            for (int i = _activePoisonEffectAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activePoisonEffectAnimations[i];
                anim.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (anim.Timer >= PoisonEffectAnimationState.Duration)
                {
                    _activePoisonEffectAnimations.RemoveAt(i);
                }
            }
        }

        private void UpdateDamageIndicators(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            const float gravity = 500f;
            const float floorY = 10f;

            for (int i = _activeDamageIndicators.Count - 1; i >= 0; i--)
            {
                var indicator = _activeDamageIndicators[i];
                indicator.Timer += deltaTime;

                // Physics Update: Rotation
                indicator.Rotation += indicator.RotationVelocity * deltaTime;
                // Heavy Damping on rotation
                indicator.RotationVelocity *= MathF.Pow(0.1f, deltaTime);

                if (indicator.Timer >= DamageIndicatorState.DURATION)
                {
                    _activeDamageIndicators.RemoveAt(i);
                    continue;
                }

                if (indicator.Type == DamageIndicatorState.IndicatorType.Number ||
                    indicator.Type == DamageIndicatorState.IndicatorType.HealNumber ||
                    indicator.Type == DamageIndicatorState.IndicatorType.EmphasizedNumber)
                {
                    indicator.Velocity.Y += gravity * deltaTime;
                    indicator.Position += indicator.Velocity * deltaTime;

                    float relativeY = indicator.Position.Y - indicator.InitialPosition.Y;

                    if (relativeY > floorY)
                    {
                        indicator.Position.Y = indicator.InitialPosition.Y + floorY;

                        indicator.Rotation = 0f;
                        indicator.RotationVelocity = 0f;

                        if (indicator.Velocity.Y > 0)
                        {
                            indicator.Velocity.Y = -indicator.Velocity.Y * 0.5f;
                            indicator.Velocity.X *= 0.8f;
                        }
                    }
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Effectiveness)
                {
                    float progress = indicator.Timer / DamageIndicatorState.DURATION;
                    float yOffset = Easing.EaseOutQuad(progress) * DamageIndicatorState.RISE_DISTANCE;
                    indicator.Position = indicator.InitialPosition + new Vector2(0, yOffset);
                }
                else
                {
                    float progress = indicator.Timer / DamageIndicatorState.DURATION;
                    // Reduced RISE_DISTANCE makes text indicators stay closer to the sprite
                    float yOffset = -Easing.EaseOutQuad(progress) * DamageIndicatorState.RISE_DISTANCE;
                    indicator.Position = indicator.InitialPosition + new Vector2(0, yOffset);
                }
            }
        }

        private void UpdateAbilityIndicators(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_abilitySpawnTimer > 0)
            {
                _abilitySpawnTimer -= deltaTime;
            }

            if (_abilitySpawnTimer <= 0 && _pendingAbilityQueue.Count > 0)
            {
                var pending = _pendingAbilityQueue.Dequeue();
                var indicator = new AbilityIndicatorState
                {
                    CombatantID = pending.CombatantID,
                    OriginalText = pending.Text,
                    Text = pending.Text,
                    Count = 1,
                    Timer = 0f,
                    Phase = AbilityIndicatorState.AnimationPhase.EasingIn,
                    ShakeTimer = AbilityIndicatorState.SHAKE_DURATION,
                    InitialPosition = pending.StartPosition,
                    CurrentPosition = pending.StartPosition,
                    Velocity = new Vector2(
                        (float)(_random.NextDouble() * ABILITY_DRIFT_RANGE * 2 - ABILITY_DRIFT_RANGE),
                        -ABILITY_FLOAT_SPEED_INITIAL
                    ),
                    Rotation = 0f,
                    RotationSpeed = (float)(_random.NextDouble() * ABILITY_ROTATION_SPEED_MAX * 2 - ABILITY_ROTATION_SPEED_MAX)
                };
                _activeAbilityIndicators.Add(indicator);
                _abilitySpawnTimer = ABILITY_SPAWN_INTERVAL;
            }

            for (int i = _activeAbilityIndicators.Count - 1; i >= 0; i--)
            {
                var indicator = _activeAbilityIndicators[i];
                indicator.Timer += deltaTime;
                if (indicator.ShakeTimer > 0)
                {
                    indicator.ShakeTimer -= deltaTime;
                }

                if (indicator.Timer >= AbilityIndicatorState.TOTAL_DURATION)
                {
                    _activeAbilityIndicators.RemoveAt(i);
                    continue;
                }

                indicator.CurrentPosition += indicator.Velocity * deltaTime;
                float velocityDamping = 1.0f - MathF.Exp(-ABILITY_FLOAT_DRAG * deltaTime);
                indicator.Velocity = Vector2.Lerp(indicator.Velocity, Vector2.Zero, velocityDamping);
                indicator.Rotation += indicator.RotationSpeed * deltaTime;
                float rotationDamping = 1.0f - MathF.Exp(-ABILITY_ROTATION_DRAG * deltaTime);
                indicator.RotationSpeed = MathHelper.Lerp(indicator.RotationSpeed, 0f, rotationDamping);

                if (indicator.Phase == AbilityIndicatorState.AnimationPhase.EasingIn && indicator.Timer >= AbilityIndicatorState.EASE_IN_DURATION)
                {
                    indicator.Phase = AbilityIndicatorState.AnimationPhase.Flashing;
                }
                else if (indicator.Phase == AbilityIndicatorState.AnimationPhase.Flashing && indicator.Timer >= AbilityIndicatorState.EASE_IN_DURATION + AbilityIndicatorState.FLASH_DURATION)
                {
                    indicator.Phase = AbilityIndicatorState.AnimationPhase.Holding;
                }
                else if (indicator.Phase == AbilityIndicatorState.AnimationPhase.Holding && indicator.Timer >= AbilityIndicatorState.EASE_IN_DURATION + AbilityIndicatorState.FLASH_DURATION + AbilityIndicatorState.HOLD_DURATION)
                {
                    indicator.Phase = AbilityIndicatorState.AnimationPhase.EasingOut;
                }
            }
        }

        public void DrawDamageIndicators(SpriteBatch spriteBatch, BitmapFont font)
        {
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Text);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Effectiveness);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.StatChange);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Protected);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Failed);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Number);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.HealNumber);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.EmphasizedNumber);
        }

        private void DrawIndicatorsOfType(SpriteBatch spriteBatch, BitmapFont font, DamageIndicatorState.IndicatorType typeToDraw)
        {
            var defaultFont = ServiceLocator.Get<BitmapFont>();
            const int screenPadding = 2;

            foreach (var indicator in _activeDamageIndicators)
            {
                if (indicator.Type != typeToDraw) continue;

                // Scale Logic: Shrink in the last 25%
                float progress = indicator.Timer / DamageIndicatorState.DURATION;
                float scale = 1.0f;
                float shrinkStart = 0.75f;
                if (progress > shrinkStart)
                {
                    float shrinkProgress = (progress - shrinkStart) / (1.0f - shrinkStart);
                    scale = 1.0f - Easing.EaseInCubic(shrinkProgress);
                }

                Color drawColor;
                BitmapFont activeFont = font;

                if (indicator.Type == DamageIndicatorState.IndicatorType.EmphasizedNumber)
                {
                    activeFont = defaultFont;
                    const float flashInterval = 0.05f;
                    bool useRed = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useRed ? _global.DamageIndicatorColor : Color.White;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Number)
                {
                    const float flashInterval = 0.1f;
                    bool useRed = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useRed ? _global.DamageIndicatorColor : _global.Palette_Rust;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.HealNumber)
                {
                    const float flashInterval = 0.1f;
                    bool useLightGreen = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useLightGreen ? _global.HealIndicatorColor : _global.Palette_Leaf;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Effectiveness)
                {
                    const float flashInterval = 0.2f;
                    bool useAltColor = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    switch (indicator.PrimaryText)
                    {
                        case "EFFECTIVE": drawColor = useAltColor ? _global.EffectiveIndicatorColor : _global.Palette_DarkSun; break;
                        case "RESISTED": drawColor = useAltColor ? _global.ResistedIndicatorColor : _global.Palette_Sun; break;
                        case "IMMUNE": drawColor = useAltColor ? _global.ImmuneIndicatorColor : _global.Palette_Shadow; break;
                        default: drawColor = Color.White; break;
                    }
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Protected)
                {
                    const float flashInterval = 0.1f;
                    bool useCyan = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useCyan ? _global.ProtectedIndicatorColor : Color.White;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Failed)
                {
                    const float flashInterval = 0.1f;
                    bool useRed = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useRed ? _global.FailedIndicatorColor : _global.Palette_Rust;
                }
                else
                {
                    drawColor = indicator.PrimaryColor;
                    if (indicator.PrimaryText == "GRAZE")
                    {
                        const float flashInterval = 0.2f;
                        bool useYellow = (int)(indicator.Timer / flashInterval) % 2 == 0;
                        drawColor = useYellow ? _global.Palette_DarkSun : _global.Palette_Sky;
                    }
                }

                if (indicator.Type == DamageIndicatorState.IndicatorType.StatChange)
                {
                    const float flashInterval = 0.2f;
                    bool useAltColor = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    Color prefixColor = useAltColor ? _global.Palette_DarkSun : indicator.PrimaryColor;
                    Color statColor = useAltColor ? _global.Palette_DarkSun : indicator.SecondaryColor.Value;
                    Color suffixColor = useAltColor ? _global.Palette_DarkSun : indicator.TertiaryColor.Value;

                    string prefixText = indicator.PrimaryText ?? "";
                    string statText = indicator.SecondaryText ?? "";
                    string suffixText = indicator.TertiaryText ?? "";

                    Vector2 prefixSize = activeFont.MeasureString(prefixText);
                    Vector2 statSize = activeFont.MeasureString(statText);
                    Vector2 suffixSize = activeFont.MeasureString(suffixText);

                    float totalWidth = prefixSize.X + statSize.X + suffixSize.X;
                    Vector2 basePosition = indicator.Position - new Vector2(totalWidth / 2f, statSize.Y);

                    // Origin for rotation
                    Vector2 groupOrigin = new Vector2(totalWidth / 2f, statSize.Y / 2f);
                    Vector2 drawCenter = indicator.Position;

                    float left = basePosition.X;
                    float right = basePosition.X + totalWidth;
                    if (left < screenPadding) drawCenter.X += (screenPadding - left);
                    if (right > Global.VIRTUAL_WIDTH - screenPadding) drawCenter.X -= (right - (Global.VIRTUAL_WIDTH - screenPadding));

                    Vector2 currentPos = drawCenter - groupOrigin;

                    spriteBatch.DrawStringOutlinedSnapped(activeFont, prefixText, currentPos, prefixColor, _global.Palette_Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    currentPos.X += prefixSize.X * scale; // Approximate spacing
                    spriteBatch.DrawStringOutlinedSnapped(activeFont, statText, currentPos, statColor, _global.Palette_Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    currentPos.X += statSize.X * scale;
                    spriteBatch.DrawStringOutlinedSnapped(activeFont, suffixText, currentPos, suffixColor, _global.Palette_Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
                else
                {
                    Vector2 textSize = activeFont.MeasureString(indicator.PrimaryText);
                    Vector2 origin = textSize / 2f;
                    Vector2 drawPos = indicator.Position;

                    float left = drawPos.X - origin.X;
                    float right = drawPos.X + origin.X;
                    if (left < screenPadding) drawPos.X += (screenPadding - left);
                    if (right > Global.VIRTUAL_WIDTH - screenPadding) drawPos.X -= (right - (Global.VIRTUAL_WIDTH - screenPadding));

                    if (indicator.Type == DamageIndicatorState.IndicatorType.EmphasizedNumber)
                    {
                        TextAnimator.DrawTextWithEffectOutlined(spriteBatch, activeFont, indicator.PrimaryText, drawPos - origin, drawColor, _global.Palette_Black, TextEffectType.Shake, indicator.Timer, new Vector2(scale), null, indicator.Rotation);
                    }
                    else
                    {
                        spriteBatch.DrawStringSquareOutlinedSnapped(activeFont, indicator.PrimaryText, drawPos, drawColor, _global.Palette_Black, indicator.Rotation, origin, scale, SpriteEffects.None, 0f);
                    }
                }
            }
        }

        public void DrawAbilityIndicators(SpriteBatch spriteBatch, BitmapFont font)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            foreach (var indicator in _activeAbilityIndicators)
            {
                float alpha = 1.0f;
                if (indicator.Phase == AbilityIndicatorState.AnimationPhase.EasingIn)
                {
                    alpha = Easing.EaseOutQuint(indicator.Timer / AbilityIndicatorState.EASE_IN_DURATION);
                }
                else if (indicator.Phase == AbilityIndicatorState.AnimationPhase.EasingOut)
                {
                    float easeOutStart = AbilityIndicatorState.EASE_IN_DURATION + AbilityIndicatorState.FLASH_DURATION + AbilityIndicatorState.HOLD_DURATION;
                    float fadeOutProgress = (indicator.Timer - easeOutStart) / AbilityIndicatorState.EASE_OUT_DURATION;
                    alpha = 1.0f - Easing.EaseInQuad(fadeOutProgress);
                }

                const float PULSE_SPEED = 15f;
                float pulse = (MathF.Sin(indicator.Timer * PULSE_SPEED) + 1f) / 2f;
                Color textColor = Color.Lerp(_global.Palette_DarkSun, _global.Palette_Sun, pulse);
                Color outlineColor = _global.Palette_Black;
                float finalDrawAlpha = alpha;

                Vector2 shakeOffset = Vector2.Zero;
                if (indicator.ShakeTimer > 0)
                {
                    float progress = 1.0f - (indicator.ShakeTimer / AbilityIndicatorState.SHAKE_DURATION);
                    float magnitude = AbilityIndicatorState.SHAKE_MAGNITUDE * (1.0f - Easing.EaseOutQuad(progress));
                    shakeOffset.X = MathF.Sin(indicator.Timer * AbilityIndicatorState.SHAKE_FREQUENCY) * magnitude;
                }

                Vector2 textSize = tertiaryFont.MeasureString(indicator.Text);
                var textPosition = new Vector2(
                    (int)(indicator.CurrentPosition.X - textSize.X / 2f + shakeOffset.X),
                    (int)(indicator.CurrentPosition.Y - textSize.Y / 2f + shakeOffset.Y)
                );

                Vector2 origin = textSize / 2f;
                Vector2 drawPos = indicator.CurrentPosition + shakeOffset;

                TextAnimator.DrawTextWithEffectSquareOutlined(spriteBatch, tertiaryFont, indicator.Text, drawPos - origin, textColor * finalDrawAlpha, outlineColor * finalDrawAlpha, TextEffectType.DriftWave, indicator.Timer, null, indicator.Rotation);
            }
        }
    }
}
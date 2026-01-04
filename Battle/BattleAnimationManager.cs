using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Particles;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Manages all visual feedback animations during a battle, such as health bars, hit flashes, and damage indicators.
    /// </summary>
    public class BattleAnimationManager
    {
        // --- Tuning ---
        private const float HEALTH_ANIMATION_DURATION = 0.25f; // Duration of the health bar drain animation in seconds.
        private const float INDICATOR_COOLDOWN = 1.0f; // Minimum time each text indicator is displayed before the next one can start.

        // Internal animation state structs
        public class HealthAnimationState { public string CombatantID; public float StartHP; public float TargetHP; public float Timer; }
        public class AlphaAnimationState { public string CombatantID; public float StartAlpha; public float TargetAlpha; public float Timer; public const float Duration = 0.167f; }

        public class DeathAnimationState
        {
            public string CombatantID;
            public float Timer;
            public enum Phase { FlashWhite1, FlashGray, FlashWhite2, FadeOut }
            public Phase CurrentPhase;

            // Data for Coin Spawning
            public Vector2 CenterPosition;
            public float GroundY;
            public bool CoinsSpawned;

            // Tuning
            public const float FLASH_DURATION = 0.1f;
            public const float FADE_DURATION = 0.25f;
        }

        public class SpawnAnimationState
        {
            public string CombatantID;
            public float Timer;
            public enum Phase { Flash, FadeIn }
            public Phase CurrentPhase;

            // Tuning
            public const float FLASH_DURATION = 0.4f; // Total time for flashing
            public const float FLASH_INTERVAL = 0.1f; // Time per flash toggle
            public const float FADE_DURATION = 0.6f; // Slightly slower for a floaty feel
            public const float DROP_HEIGHT = 10f; // Reduced height for a subtle float down
        }

        // --- NEW: Intro Slide Animation ---
        public class IntroSlideAnimationState
        {
            public string CombatantID;
            public bool IsEnemy; // Flag to determine behavior

            public enum Phase { Sliding, Waiting, Revealing }
            public Phase CurrentPhase;

            // Timers
            public float SlideTimer;
            public float WaitTimer;
            public float RevealTimer;

            // Offsets
            public Vector2 StartOffset;
            public Vector2 CurrentOffset;

            // Tuning
            public const float SLIDE_DURATION = 0.5f;
            public const float WAIT_DURATION = 0.5f; // 0.5s delay for enemies
            public const float REVEAL_DURATION = 0.5f; // Fade out silhouette
        }
        private readonly List<IntroSlideAnimationState> _activeIntroSlideAnimations = new List<IntroSlideAnimationState>();

        // --- NEW: Floor Intro Animation ---
        public class FloorIntroAnimationState
        {
            public string ID; // "floor_0", "floor_1", "floor_center"
            public float Timer;
            public const float DURATION = 0.5f;
        }
        private readonly List<FloorIntroAnimationState> _activeFloorIntroAnimations = new List<FloorIntroAnimationState>();

        // --- NEW: Floor Outro Animation ---
        public class FloorOutroAnimationState
        {
            public string ID; // "floor_0", "floor_1"
            public float Timer;
            public const float DURATION = 0.5f;
        }
        private readonly List<FloorOutroAnimationState> _activeFloorOutroAnimations = new List<FloorOutroAnimationState>();

        public class SwitchOutAnimationState
        {
            public string CombatantID;
            public bool IsEnemy;
            public float Timer;

            // For Enemy Sequence
            public enum Phase { Silhouetting, Lifting }
            public Phase CurrentPhase;
            public float SilhouetteTimer;
            public float LiftTimer;

            public const float SILHOUETTE_DURATION = 0.5f;
            public const float LIFT_DURATION = 0.5f;
            public const float LIFT_HEIGHT = 150f; // Match INTRO_SLIDE_DISTANCE

            // For Player (Legacy/Simple)
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

            // Flash Tuning
            public const int TOTAL_FLASHES = 4;
            public const float FLASH_ON_DURATION = 0.05f;
            public const float FLASH_OFF_DURATION = 0.05f;
            public const float TOTAL_FLASH_CYCLE_DURATION = FLASH_ON_DURATION + FLASH_OFF_DURATION;

            // Shake Tuning
            public const float SHAKE_LEFT_DURATION = 0.05f;
            public const float SHAKE_RIGHT_DURATION = 0.05f;
            public const float SHAKE_SETTLE_DURATION = 0.05f;
            public const float TOTAL_SHAKE_DURATION = SHAKE_LEFT_DURATION + SHAKE_RIGHT_DURATION + SHAKE_SETTLE_DURATION;
            public const float SHAKE_MAGNITUDE = 2f;
        }
        public class HealBounceAnimationState { public string CombatantID; public float Timer; public const float Duration = 0.3f; public const float Height = 5f; }
        public class HealFlashAnimationState { public string CombatantID; public float Timer; public const float Duration = 0.5f; }
        public class PoisonEffectAnimationState { public string CombatantID; public float Timer; public const float Duration = 1.5f; }

        // Made public so BattleRenderer can read the state
        public class ResourceBarAnimationState
        {
            public enum BarResourceType { HP, Mana }
            public enum BarAnimationType { Loss, Recovery }
            public enum LossPhase { Preview, FlashBlack, FlashWhite, Shrink }
            public enum RecoveryPhase { Hang, Fade } // Added RecoveryPhase

            public string CombatantID;
            public BarResourceType ResourceType;
            public BarAnimationType AnimationType;
            public LossPhase CurrentLossPhase;
            public RecoveryPhase CurrentRecoveryPhase; // Added CurrentRecoveryPhase

            public float ValueBefore; // e.g. 100 HP
            public float ValueAfter;  // e.g. 80 HP

            public float Timer;

            // Loss Animation Tuning
            public const float PREVIEW_DURATION = 0.6f;
            public const float FLASH_BLACK_DURATION = 0.05f;
            public const float FLASH_WHITE_DURATION = 0.05f;
            public const float SHRINK_DURATION = 0.6f;
        }
        public class AbilityIndicatorState
        {
            public enum AnimationPhase { EasingIn, Flashing, Holding, EasingOut }
            public AnimationPhase Phase;
            public string OriginalText;
            public string Text;
            public int Count;
            public Vector2 InitialPosition; // Off-screen bottom
            public Vector2 TargetPosition;  // On-screen "hang" position
            public Vector2 CurrentPosition;
            public float Timer;
            public float ShakeTimer;
            public const float SHAKE_DURATION = 0.5f;
            public const float SHAKE_MAGNITUDE = 4.0f;
            public const float SHAKE_FREQUENCY = 15f;
            public const float EASE_IN_DURATION = 0.1f;
            public const float FLASH_DURATION = 0.15f;
            public const float HOLD_DURATION = 1.0f;
            public const float EASE_OUT_DURATION = 0.4f;
            public const float TOTAL_DURATION = EASE_IN_DURATION + FLASH_DURATION + HOLD_DURATION + EASE_OUT_DURATION;
        }
        public class DamageIndicatorState
        {
            public enum IndicatorType { Text, Number, HealNumber, EmphasizedNumber, Effectiveness, StatChange, Protected, Failed } // Added Failed
            public IndicatorType Type;
            public string CombatantID;
            public string PrimaryText;
            public string? SecondaryText;
            public string? TertiaryText;
            public Vector2 Position; // Current position
            public Vector2 Velocity; // For physics-based indicators
            public Vector2 InitialPosition; // For simple animation paths
            public Color PrimaryColor;
            public Color? SecondaryColor;
            public Color? TertiaryColor;
            public float Timer;
            public const float DURATION = 1.75f;
            public const float RISE_DISTANCE = 5f;
        }

        // --- COIN PARTICLE SYSTEM ---
        public class CoinParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float TargetGroundY; // Specific landing height for this coin
            public bool IsResting;
            public float Timer; // For despawn/flicker
            public float Delay; // Staggered start delay

            // Visuals
            public float FlipTimer; // Controls the spin
            public float FlipSpeed; // How fast it spins

            // Magnetization State
            public bool IsMagnetizing;
            public Vector2 MagnetTarget;
            public float MagnetSpeed;
            public float MagnetAcceleration;

            // --- NEW: Pre-calculated Target ---
            public Vector2? PreCalculatedTarget;
            public string TargetCombatantID; // ID of the combatant who will "catch" this coin
        }
        private readonly List<CoinParticle> _activeCoins = new List<CoinParticle>();

        // --- COIN TUNING VARIABLES ---
        private const float COIN_GRAVITY = 900f;
        private const float COIN_BOUNCE_FACTOR = 0.5f;
        private const float COIN_LIFETIME = 0.5f; // Time to wait before magnetizing
        private const float COIN_INDIVIDUAL_DISPENSE_DELAY = 1.75f; // Increased stagger to prevent clumping

        // Spawning Physics
        private const float COIN_VELOCITY_X_RANGE = 120f; // Increased from 45f to spread coins wider
        private const float COIN_VELOCITY_Y_MIN = -200f; // Upward force min
        private const float COIN_VELOCITY_Y_MAX = -100f; // Upward force max

        // Magnetization Physics
        private const float COIN_MAGNET_ACCEL_MIN = 1500f;
        private const float COIN_MAGNET_ACCEL_MAX = 2000f;
        private const float COIN_MAGNET_KILL_DIST_SQ = 100f; // 10px squared

        // Floor Layout
        private const float COIN_GROUND_OFFSET_Y = 42f; // Lift the baseline up 16px
        private const float COIN_GROUND_DEPTH_HEIGHT = 14f; // The vertical spread of the floor (3D effect)

        // --- COIN CATCH ANIMATION ---
        public class CoinCatchAnimationState
        {
            public string CombatantID;
            public float Timer;
            public float CurrentRotation; // Rotation in radians
            public const float DURATION = 0.15f; // Very quick pop
            public const float ROTATION_DECAY_SPEED = 10f; // How fast rotation returns to 0
        }
        private readonly List<CoinCatchAnimationState> _activeCoinCatchAnimations = new List<CoinCatchAnimationState>();
        private const float COIN_CATCH_ROTATION_STRENGTH = 0.1f; // Tunable max rotation (radians)

        // --- HITSTOP VISUAL STATE ---
        public class HitstopVisualState
        {
            public string CombatantID;
            public bool IsCrit;
        }
        private readonly List<HitstopVisualState> _activeHitstopVisuals = new List<HitstopVisualState>();

        private readonly List<HealthAnimationState> _activeHealthAnimations = new List<HealthAnimationState>();
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
        private readonly List<AbilityIndicatorState> _activeAbilityIndicators = new List<AbilityIndicatorState>();
        private readonly List<ResourceBarAnimationState> _activeBarAnimations = new List<ResourceBarAnimationState>();

        // Text Indicator Queue
        private readonly Queue<Action> _pendingTextIndicators = new Queue<Action>();
        private float _indicatorCooldownTimer = 0f;


        private readonly Random _random = new Random();
        private readonly Global _global;

        // Layout Constants mirrored from BattleRenderer for pixel-perfect alignment
        private const int DIVIDER_Y = 123;

        /// <summary>
        /// Returns true if any BLOCKING animation is currently playing.
        /// This excludes cosmetic effects like coins, damage numbers, and hit flashes,
        /// allowing the game logic to proceed while these play in the background.
        /// </summary>
        public bool IsAnimating =>
            _activeHealthAnimations.Any() ||
            _activeAlphaAnimations.Any() ||
            _activeDeathAnimations.Any() ||
            _activeSpawnAnimations.Any() ||
            _activeSwitchOutAnimations.Any() ||
            _activeSwitchInAnimations.Any() ||
            _activeBarAnimations.Any() ||
            _activeIntroSlideAnimations.Any() ||
            _activeFloorIntroAnimations.Any() ||
            _activeFloorOutroAnimations.Any();

        /// <summary>
        /// Returns true if ANY visual effect is active, including non-blocking ones like coins and damage numbers.
        /// Used to delay the end of battle until the screen is clean.
        /// </summary>
        public bool IsVisuallyBusy =>
            IsAnimating ||
            _activeCoins.Any() ||
            _activeCoinCatchAnimations.Any() ||
            _activeDamageIndicators.Any() ||
            _activeAbilityIndicators.Any();

        public BattleAnimationManager()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Reset()
        {
            ForceClearAll();
        }

        public void ForceClearAll()
        {
            _activeHealthAnimations.Clear();
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
            _activeCoins.Clear();
            _pendingTextIndicators.Clear();
            _activeHitstopVisuals.Clear();
            _activeCoinCatchAnimations.Clear();
            _activeIntroSlideAnimations.Clear();
            _activeFloorIntroAnimations.Clear();
            _activeFloorOutroAnimations.Clear();
            _indicatorCooldownTimer = 0f;
        }

        /// <summary>
        /// Instantly completes all "blocking" animations (health bars, movement, etc.)
        /// so the game logic can proceed immediately.
        /// Does NOT clear non-blocking visuals like damage numbers or particles.
        /// </summary>
        public void CompleteBlockingAnimations(IEnumerable<BattleCombatant> combatants)
        {
            // 1. Snap Health/Mana Bars
            foreach (var anim in _activeHealthAnimations)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    combatant.VisualHP = anim.TargetHP;
                }
            }
            _activeHealthAnimations.Clear();
            _activeBarAnimations.Clear();

            // 2. Snap Alpha/Visibility
            foreach (var anim in _activeAlphaAnimations)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    combatant.VisualAlpha = anim.TargetAlpha;
                }
            }
            _activeAlphaAnimations.Clear();

            // 3. Snap Intro/Switch Animations
            foreach (var anim in _activeIntroSlideAnimations)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    combatant.VisualAlpha = 1.0f;
                }
            }
            _activeIntroSlideAnimations.Clear();
            _activeSwitchInAnimations.Clear();
            _activeSwitchOutAnimations.Clear();
            _activeFloorIntroAnimations.Clear();
            _activeFloorOutroAnimations.Clear();

            // 4. Handle Death Animations: Ensure coins spawn if skipped
            foreach (var anim in _activeDeathAnimations)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    if (!anim.CoinsSpawned && !combatant.IsPlayerControlled)
                    {
                        // Logic copied from UpdateDeathAnimations to ensure coins spawn
                        var players = combatants.Where(c => c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
                        SpawnCoins(anim.CenterPosition, 50, anim.GroundY, players);
                    }
                    // Ensure alpha is 0 (dead)
                    combatant.VisualAlpha = 0f;
                }
            }
            _activeDeathAnimations.Clear();

            // 5. Clear other blocking states
            _activeSpawnAnimations.Clear();

            // Note: We do NOT clear DamageIndicators, AbilityIndicators, Coins, or HitFlashes.
            // These are "fire and forget" visuals that don't block game logic.
            // Clearing them looks jarring; letting them fade out naturally while the next turn starts looks better.
        }

        public void StartHitstopVisuals(string combatantId, bool isCrit)
        {
            // Clear any existing hitstop visual for this combatant
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

        public void StartHealthAnimation(string combatantId, int hpBefore, int hpAfter)
        {
            // Remove any existing health animation for this combatant to ensure the new one takes precedence.
            // This is crucial for multi-hit moves, where each hit should restart the animation.
            _activeHealthAnimations.RemoveAll(a => a.CombatantID == combatantId);

            _activeHealthAnimations.Add(new HealthAnimationState
            {
                CombatantID = combatantId,
                StartHP = hpBefore,
                TargetHP = hpAfter,
                Timer = 0f
            });
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

        public void StartManaLossAnimation(string combatantId, float manaBefore, float manaAfter)
        {
            _activeBarAnimations.RemoveAll(a => a.CombatantID == combatantId && a.ResourceType == ResourceBarAnimationState.BarResourceType.Mana);
            _activeBarAnimations.Add(new ResourceBarAnimationState
            {
                CombatantID = combatantId,
                ResourceType = ResourceBarAnimationState.BarResourceType.Mana,
                AnimationType = ResourceBarAnimationState.BarAnimationType.Loss,
                CurrentLossPhase = ResourceBarAnimationState.LossPhase.Preview,
                ValueBefore = manaBefore,
                ValueAfter = manaAfter,
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

        public void StartManaRecoveryAnimation(string combatantId, float manaBefore, float manaAfter)
        {
            _activeBarAnimations.RemoveAll(a => a.CombatantID == combatantId && a.ResourceType == ResourceBarAnimationState.BarResourceType.Mana);
            _activeBarAnimations.Add(new ResourceBarAnimationState
            {
                CombatantID = combatantId,
                ResourceType = ResourceBarAnimationState.BarResourceType.Mana,
                AnimationType = ResourceBarAnimationState.BarAnimationType.Recovery,
                CurrentRecoveryPhase = ResourceBarAnimationState.RecoveryPhase.Hang,
                ValueBefore = manaBefore,
                ValueAfter = manaAfter,
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

        public void StartDeathAnimation(string combatantId, Vector2 centerPos, float groundY)
        {
            _activeDeathAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeDeathAnimations.Add(new DeathAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f,
                CurrentPhase = DeathAnimationState.Phase.FlashWhite1,
                CenterPosition = centerPos,
                GroundY = groundY,
                CoinsSpawned = false
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

        public void StartIntroSlideAnimation(string combatantId, Vector2 startOffset, bool isEnemy)
        {
            _activeIntroSlideAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeIntroSlideAnimations.Add(new IntroSlideAnimationState
            {
                CombatantID = combatantId,
                IsEnemy = isEnemy,
                CurrentPhase = IntroSlideAnimationState.Phase.Sliding,
                SlideTimer = 0f,
                WaitTimer = 0f,
                RevealTimer = 0f,
                StartOffset = startOffset,
                CurrentOffset = startOffset
            });
        }

        public IntroSlideAnimationState GetIntroSlideAnimationState(string combatantId)
        {
            return _activeIntroSlideAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
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
            return _activeFloorOutroAnimations.Any(a => a.ID == id);
        }

        public void StartSwitchOutAnimation(string combatantId, bool isEnemy)
        {
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

        public void StartCoinCatchAnimation(string combatantId)
        {
            // Reset timer if already exists to keep it popping
            var existing = _activeCoinCatchAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
            if (existing != null)
            {
                existing.Timer = 0f;
                // Add a new random rotation kick to the existing one
                float kick = (float)(_random.NextDouble() * (COIN_CATCH_ROTATION_STRENGTH * 2) - COIN_CATCH_ROTATION_STRENGTH);
                existing.CurrentRotation += kick;
            }
            else
            {
                float initialRotation = (float)(_random.NextDouble() * (COIN_CATCH_ROTATION_STRENGTH * 2) - COIN_CATCH_ROTATION_STRENGTH);
                _activeCoinCatchAnimations.Add(new CoinCatchAnimationState
                {
                    CombatantID = combatantId,
                    Timer = 0f,
                    CurrentRotation = initialRotation
                });
            }
        }

        public CoinCatchAnimationState GetCoinCatchAnimationState(string combatantId)
        {
            return _activeCoinCatchAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public void StartAbilityIndicator(string abilityName)
        {
            var font = ServiceLocator.Get<Core>().SecondaryFont;
            string text = abilityName.ToUpper();

            // Check if an indicator for this ability already exists.
            var existingIndicator = _activeAbilityIndicators.FirstOrDefault(ind => ind.OriginalText == text);
            if (existingIndicator != null)
            {
                existingIndicator.Count++;
                existingIndicator.Text = $"{existingIndicator.OriginalText} x{existingIndicator.Count}";
                existingIndicator.Timer = 0f; // Reset timer to restart animation/duration
                existingIndicator.Phase = AbilityIndicatorState.AnimationPhase.EasingIn;
                existingIndicator.ShakeTimer = AbilityIndicatorState.SHAKE_DURATION;
                return;
            }

            Vector2 textSize = font.MeasureString(text);
            const int paddingY = 1;
            int boxHeight = (int)textSize.Y + paddingY * 2;

            var indicator = new AbilityIndicatorState
            {
                OriginalText = text,
                Text = text,
                Count = 1,
                Timer = 0f,
                Phase = AbilityIndicatorState.AnimationPhase.EasingIn,
                ShakeTimer = AbilityIndicatorState.SHAKE_DURATION
            };
            indicator.InitialPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT + boxHeight);
            indicator.CurrentPosition = indicator.InitialPosition;

            _activeAbilityIndicators.Add(indicator);
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
                    Timer = 0f
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
                    Timer = 0f
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
                    Timer = 0f
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
                    Timer = 0f
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
                Velocity = new Vector2((float)(_random.NextDouble() * 30 - 15), (float)(_random.NextDouble() * -20 - 20)), // Slower, floatier arc
                Timer = 0f
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
                Velocity = new Vector2((float)(_random.NextDouble() * 30 - 15), (float)(_random.NextDouble() * -20 - 40)), // Higher initial upward velocity
                Timer = 0f
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
                Velocity = new Vector2((float)(_random.NextDouble() * 30 - 15), (float)(_random.NextDouble() * -20 - 20)),
                Timer = 0f
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
                    Timer = 0f
                });
            });
        }

        public void SkipAllHealthAnimations(IEnumerable<BattleCombatant> combatants)
        {
            foreach (var anim in _activeHealthAnimations)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    combatant.VisualHP = anim.TargetHP;
                }
            }
            _activeHealthAnimations.Clear();
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

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            UpdateIndicatorQueue(gameTime);
            UpdateHealthAnimations(gameTime, combatants);
            UpdateAlphaAnimations(gameTime, combatants);
            UpdateDeathAnimations(gameTime, combatants);
            UpdateSpawnAnimations(gameTime, combatants);
            UpdateIntroSlideAnimations(gameTime, combatants);
            UpdateFloorIntroAnimations(gameTime);
            UpdateFloorOutroAnimations(gameTime);
            UpdateSwitchAnimations(gameTime, combatants);
            UpdateHitFlashAnimations(gameTime);
            UpdateHealAnimations(gameTime);
            UpdatePoisonEffectAnimations(gameTime);
            UpdateDamageIndicators(gameTime);
            UpdateAbilityIndicators(gameTime);
            UpdateBarAnimations(gameTime);
            UpdateCoins(gameTime, combatants);
            UpdateCoinCatchAnimations(gameTime);
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
            for (int i = _activeHealthAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHealthAnimations[i];
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeHealthAnimations.RemoveAt(i);
                    continue;
                }

                anim.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (anim.Timer >= HEALTH_ANIMATION_DURATION)
                {
                    combatant.VisualHP = anim.TargetHP;
                    _activeHealthAnimations.RemoveAt(i);
                }
                else
                {
                    float progress = anim.Timer / HEALTH_ANIMATION_DURATION;
                    combatant.VisualHP = MathHelper.Lerp(anim.StartHP, anim.TargetHP, Easing.EaseOutQuart(progress));
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
                    combatant.VisualAlpha = MathHelper.Lerp(anim.StartAlpha, anim.TargetAlpha, Easing.EaseOutQuad(progress));
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

                // Ensure silhouette is fully active
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
                        combatant.VisualSilhouetteColorOverride = ServiceLocator.Get<Global>().Palette_DarkGray;
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

                            // Trigger Coin Spawn here
                            if (!anim.CoinsSpawned && !combatant.IsPlayerControlled)
                            {
                                // Pass the list of active players to SpawnCoins
                                var players = combatants.Where(c => c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
                                SpawnCoins(anim.CenterPosition, 50, anim.GroundY, players);
                                anim.CoinsSpawned = true;
                            }
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

        private void SpawnCoins(Vector2 origin, int amount, float referenceGroundY, List<BattleCombatant> players)
        {
            for (int i = 0; i < amount; i++)
            {
                // Calculate a random depth offset to create a 3D floor effect
                float randomDepth = (float)(_random.NextDouble() * COIN_GROUND_DEPTH_HEIGHT) - (COIN_GROUND_DEPTH_HEIGHT / 2f);

                // Apply the global offset (lift) and the random depth
                float targetGroundY = (referenceGroundY - COIN_GROUND_OFFSET_Y) + randomDepth;

                // --- ROUND ROBIN TARGET ASSIGNMENT ---
                Vector2? targetPos = null;
                string targetId = null;

                if (players.Any())
                {
                    // Distribute coins evenly among players
                    var targetPlayer = players[i % players.Count];
                    targetId = targetPlayer.CombatantID;

                    // Calculate visual center for the target player
                    // Hardcoded logic based on BattleRenderer layout to avoid dependency cycle
                    // Slot 0: Left, Slot 1: Right
                    float spriteCenterX = (targetPlayer.BattleSlot == 1)
                        ? (Global.VIRTUAL_WIDTH * 0.75f)
                        : (Global.VIRTUAL_WIDTH * 0.25f);

                    // Use fixed Y for player heart center
                    float heartCenterY = 96f;

                    targetPos = new Vector2(spriteCenterX, heartCenterY);
                }

                var coin = new CoinParticle
                {
                    Position = origin + new Vector2((float)(_random.NextDouble() * 20 - 10), 0), // Add jitter to start pos
                    Velocity = new Vector2(
                        (float)(_random.NextDouble() * (COIN_VELOCITY_X_RANGE * 2) - COIN_VELOCITY_X_RANGE), // Spread X
                        (float)(_random.NextDouble() * (COIN_VELOCITY_Y_MAX - COIN_VELOCITY_Y_MIN) + COIN_VELOCITY_Y_MIN) // Burst Up Y
                    ),
                    TargetGroundY = targetGroundY,
                    IsResting = false,
                    Timer = 0f,
                    Delay = (float)(_random.NextDouble() * COIN_INDIVIDUAL_DISPENSE_DELAY),
                    IsMagnetizing = false,
                    MagnetSpeed = 0f,
                    MagnetAcceleration = (float)(_random.NextDouble() * (COIN_MAGNET_ACCEL_MAX - COIN_MAGNET_ACCEL_MIN) + COIN_MAGNET_ACCEL_MIN),
                    FlipTimer = (float)(_random.NextDouble() * MathHelper.TwoPi),
                    FlipSpeed = 10f + (float)(_random.NextDouble() * 10f),
                    PreCalculatedTarget = targetPos, // Assign the specific target
                    TargetCombatantID = targetId
                };
                _activeCoins.Add(coin);
            }
        }

        private void UpdateCoins(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            for (int i = _activeCoins.Count - 1; i >= 0; i--)
            {
                var coin = _activeCoins[i];

                // Handle Delay
                if (coin.Delay > 0)
                {
                    coin.Delay -= dt;
                    continue; // Skip update until delay is over
                }

                // Update Flip Animation
                coin.FlipTimer += dt * coin.FlipSpeed;

                if (coin.IsMagnetizing)
                {
                    // Magnetization Logic
                    Vector2 direction = coin.MagnetTarget - coin.Position;
                    float distanceSq = direction.LengthSquared();

                    if (distanceSq < COIN_MAGNET_KILL_DIST_SQ)
                    {
                        // Coin Collected
                        if (!string.IsNullOrEmpty(coin.TargetCombatantID))
                        {
                            StartCoinCatchAnimation(coin.TargetCombatantID);
                        }
                        _activeCoins.RemoveAt(i);
                        continue;
                    }

                    direction.Normalize();
                    coin.MagnetSpeed += coin.MagnetAcceleration * dt;
                    coin.Position += direction * coin.MagnetSpeed * dt;
                }
                else if (!coin.IsResting)
                {
                    // Falling/Bouncing Logic
                    coin.Velocity.Y += COIN_GRAVITY * dt;
                    coin.Position += coin.Velocity * dt;

                    if (coin.Position.Y >= coin.TargetGroundY)
                    {
                        coin.Position.Y = coin.TargetGroundY;
                        // Bounce
                        if (Math.Abs(coin.Velocity.Y) > 50f)
                        {
                            coin.Velocity.Y = -coin.Velocity.Y * COIN_BOUNCE_FACTOR;
                            coin.Velocity.X *= 0.8f; // Friction
                        }
                        else
                        {
                            coin.IsResting = true;
                            coin.Velocity = Vector2.Zero;
                        }
                    }
                }
                else
                {
                    // Resting Logic -> Transition to Magnetize
                    coin.Timer += dt;
                    if (coin.Timer >= COIN_LIFETIME)
                    {
                        // Use the pre-calculated target if available
                        if (coin.PreCalculatedTarget.HasValue)
                        {
                            coin.IsMagnetizing = true;
                            coin.MagnetTarget = coin.PreCalculatedTarget.Value;
                        }
                        else
                        {
                            // No target (e.g. all players dead?), just fade out
                            _activeCoins.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private void UpdateCoinCatchAnimations(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeCoinCatchAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeCoinCatchAnimations[i];
                anim.Timer += dt;

                // Decay rotation
                anim.CurrentRotation = MathHelper.Lerp(anim.CurrentRotation, 0f, dt * CoinCatchAnimationState.ROTATION_DECAY_SPEED);

                if (anim.Timer >= CoinCatchAnimationState.DURATION)
                {
                    _activeCoinCatchAnimations.RemoveAt(i);
                }
            }
        }

        public void DrawCoins(SpriteBatch spriteBatch)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            foreach (var coin in _activeCoins)
            {
                // Don't draw if still delayed
                if (coin.Delay > 0) continue;

                // Calculate Shimmer Color
                // Sin wave from -1 to 1. Map to 0 to 1.
                float shimmer = (MathF.Sin(coin.FlipTimer) + 1f) / 2f;
                Color coinColor = Color.Lerp(_global.Palette_Yellow, _global.Palette_Orange, shimmer);

                // Calculate Flip Scale (X-axis)
                // Cos wave from -1 to 1. Abs to get 0 to 1 (flipping appearance).
                float flipScale = MathF.Abs(MathF.Cos(coin.FlipTimer));

                // Draw a small rectangle (3x3) scaled horizontally
                // Origin at center (1.5, 1.5)
                Vector2 origin = new Vector2(1f, 1f);
                Vector2 scale = new Vector2(1.0f, 1.0f);

                // Use a 3x3 source rect from the pixel texture (it's 1x1, so we just scale it up)
                // Actually, DrawSnapped takes a destination rect or position + scale.
                // Let's use position + scale.
                // Base size 3x3.
                Vector2 baseSize = new Vector2(1f, 1f);
                Vector2 finalScale = baseSize * scale;

                // Since DrawSnapped rounds positions, we need to be careful with small scales.
                // Let's use the overload that takes scale.
                // Note: DrawSnapped rounds the position. We want the visual center to be the coin position.
                spriteBatch.DrawSnapped(pixel, coin.Position, null, coinColor, 0f, new Vector2(0.5f, 0.5f), finalScale, SpriteEffects.None, 0f);
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
                    // Flash logic: Toggle silhouette on/off
                    int flashCycle = (int)(anim.Timer / SpawnAnimationState.FLASH_INTERVAL);
                    bool isVisible = flashCycle % 2 == 0;

                    combatant.VisualAlpha = 0f; // Hide normal sprite
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
                    // Fade Phase: Fade in, drop down
                    float progress = Math.Clamp(anim.Timer / SpawnAnimationState.FADE_DURATION, 0f, 1f);
                    float easedProgress = Easing.EaseOutQuad(progress);

                    combatant.VisualAlpha = easedProgress;
                    combatant.VisualSilhouetteAmount = 0f; // Disable silhouette
                    combatant.VisualSilhouetteColorOverride = null;

                    if (anim.Timer >= SpawnAnimationState.FADE_DURATION)
                    {
                        combatant.VisualAlpha = 1.0f;
                        _activeSpawnAnimations.RemoveAt(i);
                    }
                }
            }
        }

        private void UpdateIntroSlideAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeIntroSlideAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeIntroSlideAnimations[i];
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeIntroSlideAnimations.RemoveAt(i);
                    continue;
                }

                if (!anim.IsEnemy)
                {
                    // --- PLAYER LOGIC (Simple Slide) ---
                    anim.SlideTimer += deltaTime; // Changed from Timer
                    float progress = Math.Clamp(anim.SlideTimer / IntroSlideAnimationState.SLIDE_DURATION, 0f, 1f); // Changed from Timer
                    float easedProgress = Easing.EaseOutCubic(progress);

                    anim.CurrentOffset = Vector2.Lerp(anim.StartOffset, Vector2.Zero, easedProgress);
                    combatant.VisualAlpha = easedProgress; // Fade in

                    if (progress >= 1.0f)
                    {
                        combatant.VisualAlpha = 1.0f;
                        _activeIntroSlideAnimations.RemoveAt(i);
                    }
                }
                else
                {
                    // --- ENEMY LOGIC (Slide -> Wait -> Reveal) ---
                    switch (anim.CurrentPhase)
                    {
                        case IntroSlideAnimationState.Phase.Sliding:
                            anim.SlideTimer += deltaTime;
                            float slideProgress = Math.Clamp(anim.SlideTimer / IntroSlideAnimationState.SLIDE_DURATION, 0f, 1f);
                            float easedSlide = Easing.EaseOutCubic(slideProgress);

                            anim.CurrentOffset = Vector2.Lerp(anim.StartOffset, Vector2.Zero, easedSlide);

                            // Fade in alpha, but keep silhouette active (handled in Renderer)
                            combatant.VisualAlpha = easedSlide;

                            if (slideProgress >= 1.0f)
                            {
                                anim.CurrentPhase = IntroSlideAnimationState.Phase.Waiting;
                            }
                            break;

                        case IntroSlideAnimationState.Phase.Waiting:
                            anim.WaitTimer += deltaTime;
                            if (anim.WaitTimer >= IntroSlideAnimationState.WAIT_DURATION)
                            {
                                anim.CurrentPhase = IntroSlideAnimationState.Phase.Revealing;
                            }
                            break;

                        case IntroSlideAnimationState.Phase.Revealing:
                            anim.RevealTimer += deltaTime;
                            float revealProgress = Math.Clamp(anim.RevealTimer / IntroSlideAnimationState.REVEAL_DURATION, 0f, 1f);

                            // Renderer will use this timer to fade the silhouette overlay

                            if (revealProgress >= 1.0f)
                            {
                                _activeIntroSlideAnimations.RemoveAt(i);
                            }
                            break;
                    }
                }
            }
        }

        private void UpdateFloorIntroAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeFloorIntroAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeFloorIntroAnimations[i];
                anim.Timer += deltaTime;
                if (anim.Timer >= FloorIntroAnimationState.DURATION)
                {
                    _activeFloorIntroAnimations.RemoveAt(i);
                }
            }
        }

        private void UpdateFloorOutroAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeFloorOutroAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeFloorOutroAnimations[i];
                anim.Timer += deltaTime;
                if (anim.Timer >= FloorOutroAnimationState.DURATION)
                {
                    _activeFloorOutroAnimations.RemoveAt(i);
                }
            }
        }

        private void UpdateSwitchAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Switch Out
            for (int i = _activeSwitchOutAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeSwitchOutAnimations[i];

                if (anim.IsEnemy)
                {
                    // --- ENEMY SWITCH OUT (Multi-Phase) ---
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
                    // --- PLAYER SWITCH OUT (Legacy) ---
                    anim.Timer += deltaTime;
                    if (anim.Timer >= SwitchOutAnimationState.DURATION)
                    {
                        _activeSwitchOutAnimations.RemoveAt(i);
                    }
                }
            }

            // Switch In
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

                // --- Shake Logic (one-shot at the beginning) ---
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

                // --- Flash Logic (repeating) ---
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
            for (int i = _activeDamageIndicators.Count - 1; i >= 0; i--)
            {
                var indicator = _activeDamageIndicators[i];
                indicator.Timer += deltaTime;
                if (indicator.Timer >= DamageIndicatorState.DURATION)
                {
                    _activeDamageIndicators.RemoveAt(i);
                    continue;
                }

                // Apply physics or simple animation based on the indicator type
                if (indicator.Type == DamageIndicatorState.IndicatorType.Number || indicator.Type == DamageIndicatorState.IndicatorType.HealNumber || indicator.Type == DamageIndicatorState.IndicatorType.EmphasizedNumber)
                {
                    const float gravity = 80f; // Reduced gravity for a floatier effect
                    indicator.Velocity.Y += gravity * deltaTime;
                    indicator.Position += indicator.Velocity * deltaTime;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Effectiveness)
                {
                    float progress = indicator.Timer / DamageIndicatorState.DURATION;
                    float yOffset = Easing.EaseOutQuad(progress) * DamageIndicatorState.RISE_DISTANCE; // Move down
                    indicator.Position = indicator.InitialPosition + new Vector2(0, yOffset);
                }
                else // Text indicators like GRAZE, StatChange, Protected, Failed
                {
                    float progress = indicator.Timer / DamageIndicatorState.DURATION;
                    float yOffset = -Easing.EaseOutQuad(progress) * DamageIndicatorState.RISE_DISTANCE;
                    indicator.Position = indicator.InitialPosition + new Vector2(0, yOffset);
                }
            }
        }

        private void UpdateAbilityIndicators(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var font = ServiceLocator.Get<Core>().SecondaryFont;
            const int paddingY = 1;
            const int gap = 2;

            // Recalculate all target positions every frame for smooth stacking
            float yStackOffset = Global.VIRTUAL_HEIGHT;
            for (int i = 0; i < _activeAbilityIndicators.Count; i++)
            {
                var indicator = _activeAbilityIndicators[i];
                Vector2 textSize = font.MeasureString(indicator.Text);
                int boxHeight = (int)textSize.Y + paddingY * 2;
                yStackOffset -= boxHeight;
                indicator.TargetPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, yStackOffset);
                yStackOffset -= gap;
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

                // Smoothly move towards the (potentially changing) target position
                indicator.CurrentPosition = Vector2.Lerp(indicator.CurrentPosition, indicator.TargetPosition, 10f * deltaTime);

                // Update animation phase based on timer
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
            // Two-pass rendering: text first, then numbers on top.
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Text);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Effectiveness);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.StatChange);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Protected);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Failed); // Draw Failed text
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

                float progress = indicator.Timer / DamageIndicatorState.DURATION;

                float alpha = 1.0f;
                if (progress > 0.5f)
                {
                    float fadeProgress = (progress - 0.5f) * 2f;
                    alpha = 1.0f - Easing.EaseInQuad(fadeProgress);
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
                    drawColor = useRed ? _global.DamageIndicatorColor : _global.Palette_Red;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.HealNumber)
                {
                    const float flashInterval = 0.1f;
                    bool useLightGreen = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useLightGreen ? _global.HealIndicatorColor : _global.Palette_DarkGreen;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Effectiveness)
                {
                    const float flashInterval = 0.2f;
                    bool useAltColor = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    switch (indicator.PrimaryText)
                    {
                        case "EFFECTIVE":
                            drawColor = useAltColor ? _global.EffectiveIndicatorColor : _global.Palette_Yellow;
                            break;
                        case "RESISTED":
                            drawColor = useAltColor ? _global.ResistedIndicatorColor : _global.Palette_White;
                            break;
                        case "IMMUNE":
                            drawColor = useAltColor ? _global.ImmuneIndicatorColor : _global.Palette_LightGray;
                            break;
                        default:
                            drawColor = Color.White;
                            break;
                    }
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Protected)
                {
                    const float flashInterval = 0.1f; // Fast flash
                    bool useCyan = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useCyan ? _global.ProtectedIndicatorColor : Color.White;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Failed)
                {
                    const float flashInterval = 0.1f; // Fast flash
                    bool useRed = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useRed ? _global.FailedIndicatorColor : _global.Palette_Red;
                }
                else // Text indicator (Graze, StatChange)
                {
                    drawColor = indicator.PrimaryColor;
                    if (indicator.PrimaryText == "GRAZE")
                    {
                        const float flashInterval = 0.2f;
                        bool useYellow = (int)(indicator.Timer / flashInterval) % 2 == 0;
                        drawColor = useYellow ? _global.Palette_Yellow : _global.Palette_LightBlue;
                    }
                }

                if (indicator.Type == DamageIndicatorState.IndicatorType.StatChange)
                {
                    const float flashInterval = 0.2f;
                    bool useAltColor = (int)(indicator.Timer / flashInterval) % 2 == 0;

                    Color prefixColor = useAltColor ? _global.Palette_Yellow : indicator.PrimaryColor;
                    Color statColor = useAltColor ? _global.Palette_Yellow : indicator.SecondaryColor.Value;
                    Color suffixColor = useAltColor ? _global.Palette_Yellow : indicator.TertiaryColor.Value;

                    string prefixText = indicator.PrimaryText ?? "";
                    string statText = indicator.SecondaryText ?? "";
                    string suffixText = indicator.TertiaryText ?? "";

                    Vector2 prefixSize = activeFont.MeasureString(prefixText);
                    Vector2 statSize = activeFont.MeasureString(statText);
                    Vector2 suffixSize = activeFont.MeasureString(suffixText);

                    float totalWidth = prefixSize.X + statSize.X + suffixSize.X;
                    Vector2 basePosition = indicator.Position - new Vector2(totalWidth / 2f, statSize.Y);

                    // Adjust position to stay on screen
                    float left = basePosition.X;
                    float right = basePosition.X + totalWidth;
                    if (left < screenPadding) basePosition.X += (screenPadding - left);
                    if (right > Global.VIRTUAL_WIDTH - screenPadding) basePosition.X -= (right - (Global.VIRTUAL_WIDTH - screenPadding));

                    Vector2 currentPos = basePosition;
                    spriteBatch.DrawStringOutlinedSnapped(activeFont, prefixText, currentPos, prefixColor * alpha, _global.Palette_Black * alpha);
                    currentPos.X += prefixSize.X;
                    spriteBatch.DrawStringOutlinedSnapped(activeFont, statText, currentPos, statColor * alpha, _global.Palette_Black * alpha);
                    currentPos.X += statSize.X;
                    spriteBatch.DrawStringOutlinedSnapped(activeFont, suffixText, currentPos, suffixColor * alpha, _global.Palette_Black * alpha);
                }
                else
                {
                    Vector2 textSize = activeFont.MeasureString(indicator.PrimaryText);
                    Vector2 textPosition = indicator.Position - new Vector2(textSize.X / 2f, textSize.Y);

                    // Adjust position to stay on screen
                    float left = textPosition.X;
                    float right = textPosition.X + textSize.X;
                    if (left < screenPadding) textPosition.X += (screenPadding - left);
                    if (right > Global.VIRTUAL_WIDTH - screenPadding) textPosition.X -= (right - (Global.VIRTUAL_WIDTH - screenPadding));

                    spriteBatch.DrawStringOutlinedSnapped(activeFont, indicator.PrimaryText, textPosition, drawColor * alpha, _global.Palette_Black * alpha);
                }
            }
        }

        public void DrawAbilityIndicators(SpriteBatch spriteBatch, BitmapFont font)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            foreach (var indicator in _activeAbilityIndicators)
            {
                // --- Alpha Calculation ---
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

                // --- Pulsing Flash Logic ---
                const float PULSE_SPEED = 15f;
                float pulse = (MathF.Sin(indicator.Timer * PULSE_SPEED) + 1f) / 2f; // Oscillates between 0.0 and 1.0

                // --- Color Determination ---
                Color pulseTargetColor;
                if (indicator.Count >= 5)
                {
                    pulseTargetColor = _global.Palette_Pink;
                }
                else if (indicator.Count == 4)
                {
                    pulseTargetColor = _global.Palette_Red;
                }
                else if (indicator.Count == 3)
                {
                    pulseTargetColor = _global.Palette_Orange;
                }
                else if (indicator.Count == 2)
                {
                    pulseTargetColor = _global.Palette_Yellow;
                }
                else // Count is 1
                {
                    pulseTargetColor = _global.Palette_Gray;
                }

                Color bgColor = Color.Lerp(_global.TerminalBg, pulseTargetColor, pulse);
                Color outlineColor = Color.Black;
                Color textColor = Color.White;

                // --- Final Transparency ---
                float finalDrawAlpha = alpha;

                // --- Shake Calculation ---
                Vector2 shakeOffset = Vector2.Zero;
                if (indicator.ShakeTimer > 0)
                {
                    float progress = 1.0f - (indicator.ShakeTimer / AbilityIndicatorState.SHAKE_DURATION);
                    float magnitude = AbilityIndicatorState.SHAKE_MAGNITUDE * (1.0f - Easing.EaseOutQuad(progress));
                    shakeOffset.X = MathF.Sin(indicator.Timer * AbilityIndicatorState.SHAKE_FREQUENCY) * magnitude;
                }

                // --- Layout Calculation ---
                Vector2 textSize = secondaryFont.MeasureString(indicator.Text);
                const int paddingX = 8;
                const int paddingY = 1;

                const int maxChars = 20;
                float maxWidth = secondaryFont.MeasureString(new string('W', maxChars)).Width;
                int boxWidth = (int)maxWidth + paddingX * 2;
                int boxHeight = (int)textSize.Y + paddingY * 2;

                var boxRect = new Rectangle(
                    (int)(indicator.CurrentPosition.X - boxWidth / 2f + shakeOffset.X),
                    (int)(indicator.CurrentPosition.Y - boxHeight + shakeOffset.Y),
                    boxWidth,
                    boxHeight
                );

                var textPosition = new Vector2(
                    boxRect.X + (boxWidth - textSize.X) / 2f,
                    boxRect.Y + paddingY - 1
                );

                // --- Drawing ---
                spriteBatch.DrawSnapped(pixel, boxRect, bgColor * 0.8f * finalDrawAlpha);
                spriteBatch.DrawLineSnapped(new Vector2(boxRect.Left, boxRect.Top), new Vector2(boxRect.Right, boxRect.Top), _global.Palette_White * finalDrawAlpha);
                spriteBatch.DrawLineSnapped(new Vector2(boxRect.Left, boxRect.Bottom), new Vector2(boxRect.Right, boxRect.Bottom), _global.Palette_White * finalDrawAlpha);
                spriteBatch.DrawLineSnapped(new Vector2(boxRect.Left, boxRect.Top), new Vector2(boxRect.Left, boxRect.Bottom), _global.Palette_White * finalDrawAlpha);
                spriteBatch.DrawLineSnapped(new Vector2(boxRect.Right, boxRect.Top), new Vector2(boxRect.Right, boxRect.Bottom), _global.Palette_White * finalDrawAlpha);

                spriteBatch.DrawStringOutlinedSnapped(secondaryFont, indicator.Text, textPosition, textColor * finalDrawAlpha, outlineColor * finalDrawAlpha);
            }
        }
    }
}

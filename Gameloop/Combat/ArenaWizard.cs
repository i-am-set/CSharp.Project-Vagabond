using Microsoft.Xna.Framework;
using ProjectVagabond.Animations;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public enum WizardState
    {
        Moving,
        Telegraphing,
        Casting,
        Recovering,
        Dead
    }

    public sealed class FloatingText : IPoolable
    {
        public bool IsPooled { get; set; }
        public int Number;
        public bool IsHealing;
        public bool IsCrit;
        public float Timer;
        public float Duration;
        public Vector2 LocalOffset;

        public void Reset()
        {
            Number = 0;
            IsHealing = false;
            IsCrit = false;
            Timer = 0f;
            Duration = 0f;
            LocalOffset = Vector2.Zero;
        }

        public void ReturnToPool()
        {
            Pool<FloatingText>.Return(this);
        }
    }

    #region Base Stats
    /// <summary>
    /// Contains the core RPG stats and identity information for the wizard.
    /// </summary>
    public class BaseStats
    {
        public string Name;
        public int PortraitIndex;
        public bool IsPlayer;
        public int HP;
        public int MaxHP;
        public int CurrentHP;
        public int Power;
        public int Tenacity;
        public int Agility;
        public int Rating;
        public float Speed;
    }
    #endregion

    #region Combat State
    /// <summary>
    /// Manages the combat-related state, including positioning, active spells, and action queues.
    /// </summary>
    public class CombatState
    {
        public WizardState State = WizardState.Moving;
        public float StateTimer;
        public Vector2 Position;
        public Vector2 TargetPosition;
        public Vector2 PreviousPosition;
        public bool IsFacingRight;

        public float InvincibilityDuration = 0.4f;
        public float InvincibilityTimer;

        public List<MoveDefinition> Moves = new List<MoveDefinition>();
        public ActiveSpellData EquippedActiveSpell;
        public float ActiveSpellCooldownTimer;
        public float WardTimer;
        public float WardHitTimer;
        public float TeleportTimer;
        public bool IsTeleporting;
        public Vector2 TeleportTargetPos;
        public bool IsSuspended => IsTeleporting;

        public float TimeSinceDeath;

        public MoveDefinition QueuedMove;
        public ArenaWizard QueuedTargetWizard;
        public Vector2 QueuedTargetPos;
        public Vector2 QueuedDirection;
        public ActiveAttack CurrentActiveAttack;
        public float ActionTimer;

        public Vector2 KnockbackStartPos;
        public Vector2 KnockbackTargetPos;
        public float KnockbackTimer;
        public float KnockbackDuration;

        public Queue<Action> SuspendedActions = new Queue<Action>();
    }
    #endregion

    #region UI State
    /// <summary>
    /// Holds all visual and UI-related state, such as HUD layout cache and animation timers.
    /// </summary>
    public class UIState
    {
        public float HopTimer;
        public float HudShakeTimer;
        public float FloatingHeartWaveTimer;
        public float FloatingHeartWaveInterval;
        public float HudHeartWaveTimer;
        public float HudHeartWaveInterval;
        public bool IsHovered;

        public float HealthBarLingerDuration = 2.5f;
        public float HealthBarVisibilityTimer;
        public float HealthBarAlpha;

        public float DeadBodyFadeDuration = 16.0f;
        public float DeadBodyMinAlpha = 0.0f;

        public Vector2 HudNameSize;
        public Vector2 HudNamePos;
        public Vector2 HudHeartStartPos;
        public bool HudIsLeft;

        public float[] HeartFlashTimers;
        public int[] HeartFlashFrame;

        public string ActiveMoveText;
        public float MoveTextTimer;
        public float MoveTextDuration;
        public bool IsMoveCanceled;

        public List<FloatingText> FloatingTexts = new List<FloatingText>();
    }
    #endregion

    #region Metrics
    /// <summary>
    /// Tracks performance metrics during a match for payout calculation.
    /// </summary>
    public class MatchMetrics
    {
        public int DamageDealt;
        public int Kills;
        public int DamageBlocked;
        public float TimeSurvived;
        public int Placement;
    }
    #endregion

    public class WizardData
    {
        public BaseStats Stats = new BaseStats();
        public CombatState Combat = new CombatState();
        public UIState UI = new UIState();
        public MatchMetrics Metrics = new MatchMetrics();
    }

    public class ArenaWizard
    {
        public WizardData Data { get; private set; } = new WizardData();
        public WizardController Controller { get; private set; }
        public WizardAIController AIController { get; set; }

        public ArenaWizard()
        {
            Controller = new WizardController(this);
        }
    }
}
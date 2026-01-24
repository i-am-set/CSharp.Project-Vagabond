using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Systems;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

namespace ProjectVagabond
{
    /// <summary>
    /// A central place to define event argument classes for the EventBus.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>
        /// Published when the accumulated round log changes.
        /// </summary>
        public struct RoundLogUpdate
        {
            public string LogText { get; set; }
        }

        /// <summary>
        /// Published when a message should be added to the main terminal output.
        /// </summary>
        public struct TerminalMessagePublished
        {
            public string Message { get; set; }
            public Microsoft.Xna.Framework.Color? BaseColor { get; set; }
        }

        /// <summary>
        /// Published when a high-priority, short-duration message should be displayed to the user.
        /// </summary>
        public struct AlertPublished
        {
            public string Message { get; set; }
        }

        /// <summary>
        /// Published by the physics system when two dice colliders make contact.
        /// An event is published for each dynamic body involved in the collision.
        /// </summary>
        public struct DiceCollisionOccurred
        {
            public System.Numerics.Vector3 WorldPosition;
            public BepuPhysics.BodyHandle BodyHandle;
            public bool IsSparking;
        }

        /// <summary>
        /// Published when the screen resolution or UI theme changes.
        /// </summary>
        public struct UIThemeOrResolutionChanged { }

        /// <summary>
        /// Published at the start of an action's resolution, before any effects are calculated.
        /// </summary>
        public struct ActionDeclared
        {
            public BattleCombatant Actor { get; set; }
            public MoveData? Move { get; set; }
            public BattleCombatant? Target { get; set; }
            public QueuedActionType Type { get; set; }
        }

        /// <summary>
        /// Published when a single action in a battle is resolved.
        /// </summary>
        public struct BattleActionExecuted
        {
            public BattleCombatant Actor { get; set; }
            public MoveData ChosenMove { get; set; }
            public List<BattleCombatant> Targets { get; set; }
            public List<DamageCalculator.DamageResult> DamageResults { get; set; }
        }

        /// <summary>
        /// Published after all hits of a multi-hit move have been resolved.
        /// </summary>
        public struct MultiHitActionCompleted
        {
            public BattleCombatant Actor { get; set; }
            public MoveData ChosenMove { get; set; }
            public int HitCount { get; set; }
            public int CriticalHitCount { get; set; }
        }

        public enum AcquisitionType { Add, Remove }

        public struct PlayerMoveAdded
        {
            public string MoveID { get; set; }
            public AcquisitionType Type { get; set; }
        }

        public struct PlayerRelicAdded
        {
            public string RelicID { get; set; }
            public AcquisitionType Type { get; set; }
        }

        public struct CombatantDefeated
        {
            public BattleCombatant DefeatedCombatant { get; set; }
        }

        public struct SecondaryEffectComplete { }

        public struct StatusEffectTriggered
        {
            public BattleCombatant Combatant { get; set; }
            public StatusEffectType EffectType { get; set; }
            public int Damage { get; set; }
            public int Healing { get; set; }
        }

        public struct StatusEffectRemoved
        {
            public BattleCombatant Combatant { get; set; }
            public StatusEffectType EffectType { get; set; }
        }

        public struct ActionFailed
        {
            public BattleCombatant Actor { get; set; }
            public string Reason { get; set; }
            public string MoveName { get; set; }
        }

        public struct CombatantChargingAction
        {
            public BattleCombatant Actor { get; set; }
            public string MoveName { get; set; }
        }

        public struct CombatantHealed
        {
            public BattleCombatant Actor { get; set; }
            public BattleCombatant Target { get; set; }
            public int HealAmount { get; set; }
            public int VisualHPBefore { get; set; }
        }

        public struct CombatantManaRestored
        {
            public BattleCombatant Target { get; set; }
            public int AmountRestored { get; set; }
            public float ManaBefore { get; set; }
            public float ManaAfter { get; set; }
        }

        public struct CombatantManaConsumed
        {
            public BattleCombatant Actor { get; set; }
            public float ManaBefore { get; set; }
            public float ManaAfter { get; set; }
        }

        public struct CombatantRecoiled
        {
            public BattleCombatant Actor { get; set; }
            public int RecoilDamage { get; set; }
            public RelicData? SourceAbility { get; set; }
        }

        /// <summary>
        /// Published when a passive ability (Relic or Intrinsic) triggers.
        /// Used for visual feedback.
        /// </summary>
        public struct AbilityActivated
        {
            public BattleCombatant Combatant { get; set; }
            public IAbility Ability { get; set; }
            public string? NarrationText { get; set; }
        }

        public struct CombatantStatStageChanged
        {
            public BattleCombatant Target { get; set; }
            public OffensiveStatType Stat { get; set; }
            public int Amount { get; set; }
        }

        public struct MoveAnimationTriggered
        {
            public BattleCombatant Actor { get; set; }
            public MoveData Move { get; set; }
            public List<BattleCombatant> Targets { get; set; }
            public Dictionary<BattleCombatant, bool> GrazeStatus { get; set; }
        }

        public struct MoveImpactOccurred
        {
            public MoveData Move { get; set; }
        }

        public struct MoveAnimationCompleted { }

        public struct NextEnemyApproaches { }

        public struct CombatantSpawned
        {
            public BattleCombatant Combatant { get; set; }
        }

        public struct CombatantSwitchingOut
        {
            public BattleCombatant Combatant { get; set; }
        }

        public struct MoveFailed
        {
            public BattleCombatant Actor { get; set; }
        }

        public struct DisengageTriggered
        {
            public BattleCombatant Actor { get; set; }
        }

        public struct ForcedSwitchRequested
        {
            public BattleCombatant Actor { get; set; }
        }

        public struct SwitchSequenceInitiated
        {
            public BattleCombatant OutgoingCombatant { get; set; }
            public BattleCombatant IncomingCombatant { get; set; }
        }
    }
}
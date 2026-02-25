using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public static class GameEvents
    {
        public struct RoundLogUpdate
        {
            public string LogText { get; set; }
        }

        public struct TerminalMessagePublished
        {
            public string Message { get; set; }
            public Microsoft.Xna.Framework.Color? BaseColor { get; set; }
        }

        public struct AlertPublished
        {
            public string Message { get; set; }
        }

        public struct UIThemeOrResolutionChanged { }

        public struct ActionDeclared
        {
            public BattleCombatant Actor { get; set; }
            public MoveData? Move { get; set; }
            public BattleCombatant? Target { get; set; }
            public QueuedActionType Type { get; set; }
        }

        public struct BattleActionExecuted
        {
            public BattleCombatant Actor { get; set; }
            public MoveData ChosenMove { get; set; }
            public List<BattleCombatant> Targets { get; set; }
            public List<DamageCalculator.DamageResult> DamageResults { get; set; }
        }

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

        public struct CombatantDefeated
        {
            public BattleCombatant DefeatedCombatant { get; set; }
        }

        public struct CombatantVisualDeath
        {
            public BattleCombatant Victim { get; set; }
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
        }

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

        public struct PlayMoveAnimation
        {
            public MoveData Move { get; set; }
            public List<BattleCombatant> Targets { get; set; }
            public Dictionary<BattleCombatant, bool> GrazeStatus { get; set; }
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
            public int? SlotIndex { get; set; }
        }

        public struct SwitchSequenceInitiated
        {
            public BattleCombatant OutgoingCombatant { get; set; }
            public BattleCombatant IncomingCombatant { get; set; }
        }

        public struct GuardChanged
        {
            public BattleCombatant Combatant { get; set; }
            public int NewValue { get; set; }
        }

        public struct GuardBroken
        {
            public BattleCombatant Combatant { get; set; }
        }

        public struct RequestImpactSync
        {
            public MoveData Move { get; set; }
            public List<BattleCombatant> Targets { get; set; }
            public float DefaultTimeToImpact { get; set; }
            public Dictionary<BattleCombatant, bool> GrazeStatus { get; set; }
            public BattleCombatant Actor { get; set; }
        }

        public struct TriggerImpact { }

        public struct SpawnProjectile
        {
            public BattleCombatant Actor { get; set; }
            public BattleCombatant Target { get; set; }
            public Action OnImpact { get; set; }
            public Color Color { get; set; }
        }

        public class CheckActionPriorityEvent : GameEvent
        {
            public BattleCombatant Actor { get; }
            public MoveData Move { get; }
            public int Priority { get; set; }
            public CheckActionPriorityEvent(BattleCombatant actor, MoveData move, int basePriority)
            {
                Actor = actor;
                Move = move;
                Priority = basePriority;
            }
        }

        public class CombatantEnteredEvent : GameEvent
        {
            public BattleCombatant Combatant { get; }
            public CombatantEnteredEvent(BattleCombatant combatant) => Combatant = combatant;
        }
    }
}
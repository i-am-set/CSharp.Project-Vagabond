using ProjectVagabond;
using ProjectVagabond.Battle;
using System.Collections.Generic;

MoveData.cs
GameEvents.cs
```csharp
using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// A central place to define event argument classes for the EventBus.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>
        /// Published when a message should be added to the main terminal output.
        /// </summary>
        public struct TerminalMessagePublished
        {
            public string Message { get; set; }
            public Microsoft.Xna.Framework.Color? BaseColor { get; set; }
        }

        /// <summary>
        /// Published when the player's action queue is modified.
        /// </summary>
        public struct ActionQueueChanged
        {
            // This event can be expanded with more data if needed,
            // but for now, its existence is enough to signal a refresh.
        }

        /// <summary>
        /// Published when the player entity completes a move to a new tile.
        /// </summary>
        public struct PlayerMoved
        {
            public Microsoft.Xna.Framework.Vector2 NewPosition { get; set; }
        }

        /// <summary>
        /// Published by the physics system when two dice colliders make contact.
        /// An event is published for each dynamic body involved in the collision.
        /// </summary>
        public struct DiceCollisionOccurred
        {
            /// <summary>
            /// The position of the collision in 3D world space.
            /// </summary>
            public System.Numerics.Vector3 WorldPosition;

            /// <summary>
            /// The handle of the dynamic body (the die) this event pertains to.
            /// </summary>
            public BepuPhysics.BodyHandle BodyHandle;

            /// <summary>
            /// True if the collision was fast enough to generate a spark effect.
            /// </summary>
            public bool IsSparking;
        }

        /// <summary>
        /// Published when the screen resolution or UI theme changes, signaling
        /// UI elements to recalculate their layouts.
        /// </summary>
        public struct UIThemeOrResolutionChanged
        {
        }

        /// <summary>
        /// Published when the player entity completes a single action from its queue.
        /// This signals to other systems, like AI, that they can take their turn.
        /// </summary>
        public struct PlayerActionExecuted
        {
            public IAction Action { get; set; }
        }

        /// <summary>
        /// Published when a single action in a battle is resolved, providing detailed results for narration and animation.
        /// </summary>
        public struct BattleActionExecuted
        {
            public BattleCombatant Actor { get; set; }
            public MoveData ChosenMove { get; set; }
            public ConsumableItemData UsedItem { get; set; }
            public List<BattleCombatant> Targets { get; set; }
            public List<DamageCalculator.DamageResult> DamageResults { get; set; }
        }

        /// <summary>
        /// Published when a consumable item is used in battle.
        /// </summary>
        public struct BattleItemUsed
        {
            public BattleCombatant Actor { get; set; }
            public ConsumableItemData UsedItem { get; set; }
            public List<BattleCombatant> Targets { get; set; }
            public List<int> HealAmounts { get; set; }
        }


        /// <summary>
        /// Defines the type of change for a player's move set.
        /// </summary>
        public enum MoveSetChangeType
        {
            Learn,
            Forget
        }

        /// <summary>
        /// Published to request a change to the player's set of available combat moves.
        /// </summary>
        public struct PlayerMoveSetChanged
        {
            public string MoveID { get; set; }
            public MoveSetChangeType ChangeType { get; set; }
        }

        /// <summary>
        /// Published when a combatant's HP drops to 0 or below, signaling the start of their defeat sequence.
        /// </summary>
        public struct CombatantDefeated
        {
            public BattleCombatant DefeatedCombatant { get; set; }
        }

        /// <summary>
        /// Published by the SecondaryEffectSystem after it has finished processing all effects for an action.
        /// This signals the BattleManager to proceed with its state machine.
        /// </summary>
        public struct SecondaryEffectComplete
        {
        }

        /// <summary>
        /// Published when a status effect triggers a passive event, like dealing damage or healing.
        /// </summary>
        public struct StatusEffectTriggered
        {
            public BattleCombatant Combatant { get; set; }
            public StatusEffectType EffectType { get; set; }
            public int Damage { get; set; }
            public int Healing { get; set; }
        }

        /// <summary>
        /// Published when a status effect is removed from a combatant.
        /// </summary>
        public struct StatusEffectRemoved
        {
            public BattleCombatant Combatant { get; set; }
            public StatusEffectType EffectType { get; set; }
        }

        /// <summary>
        /// Published when a combatant's action fails due to a status effect or other reason.
        /// </summary>
        public struct ActionFailed
        {
            public BattleCombatant Actor { get; set; }
            public string Reason { get; set; } // e.g., "silenced", "confused"
        }

        /// <summary>
        /// Published when a combatant begins charging a move.
        /// </summary>
        public struct CombatantChargingAction
        {
            public BattleCombatant Actor { get; set; }
            public string MoveName { get; set; }
        }
    }
}
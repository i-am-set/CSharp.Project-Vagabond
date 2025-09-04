using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Manages the entire turn-based flow of a battle, acting as the central state machine.
    /// </summary>
    public class BattleManager
    {
        /// <summary>
        /// Defines the distinct phases of a battle turn.
        /// </summary>
        public enum BattlePhase
        {
            StartOfTurn,
            ActionSelection,
            ActionResolution,
            EndOfTurn,
            BattleOver
        }

        private readonly List<BattleCombatant> _playerCombatants;
        private readonly List<BattleCombatant> _enemyCombatants;
        private readonly List<BattleCombatant> _allCombatants;
        private List<QueuedAction> _actionQueue;
        private BattlePhase _currentPhase;
        private int _turnNumber;
        private bool _playerActionSubmitted;

        public BattlePhase CurrentPhase => _currentPhase;
        public IEnumerable<BattleCombatant> AllCombatants => _allCombatants;

        /// <summary>
        /// Initializes a new instance of the BattleManager class and starts the battle.
        /// </summary>
        /// <param name="playerParty">A list of combatants controlled by the player.</param>
        /// <param name="enemyParty">A list of combatants controlled by the AI.</param>
        public BattleManager(List<BattleCombatant> playerParty, List<BattleCombatant> enemyParty)
        {
            _playerCombatants = playerParty;
            _enemyCombatants = enemyParty;
            _allCombatants = new List<BattleCombatant>();
            _allCombatants.AddRange(_playerCombatants);
            _allCombatants.AddRange(_enemyCombatants);

            _actionQueue = new List<QueuedAction>();
            _turnNumber = 1;
            _currentPhase = BattlePhase.StartOfTurn;
        }

        /// <summary>
        /// Sets the player's chosen action for the current turn.
        /// </summary>
        public void SetPlayerAction(QueuedAction action)
        {
            if (_currentPhase != BattlePhase.ActionSelection || _playerActionSubmitted) return;

            _actionQueue.Add(action);
            _playerActionSubmitted = true;
        }

        /// <summary>
        /// Advances the battle state by one step. This should be called in the game's update loop.
        /// </summary>
        public void Update()
        {
            if (_currentPhase == BattlePhase.BattleOver)
            {
                return;
            }

            switch (_currentPhase)
            {
                case BattlePhase.StartOfTurn:
                    HandleStartOfTurn();
                    break;
                case BattlePhase.ActionSelection:
                    HandleActionSelection();
                    break;
                case BattlePhase.ActionResolution:
                    HandleActionResolution();
                    break;
                case BattlePhase.EndOfTurn:
                    HandleEndOfTurn();
                    break;
            }
        }

        /// <summary>
        /// Handles start-of-turn effects like status effect duration countdowns.
        /// </summary>
        private void HandleStartOfTurn()
        {
            EventBus.Publish(new GameEvents.BattleLogMessagePublished { Message = $"--- Turn {_turnNumber} Start ---" });

            // 1. Environment Resolution (Placeholder)

            // 2. Duration Countdown
            foreach (var combatant in _allCombatants)
            {
                if (combatant.IsDefeated) continue;

                var effectsToRemove = new List<StatusEffectInstance>();
                foreach (var effect in combatant.ActiveStatusEffects)
                {
                    effect.DurationInTurns--;
                    if (effect.DurationInTurns <= 0)
                    {
                        effectsToRemove.Add(effect);
                    }
                }
                // Remove expired effects
                combatant.ActiveStatusEffects.RemoveAll(e => effectsToRemove.Contains(e));
            }

            _currentPhase = BattlePhase.ActionSelection;
            _playerActionSubmitted = false; // Ready to receive player input
        }

        /// <summary>
        /// Gathers actions from all combatants and sorts them into an execution queue.
        /// </summary>
        private void HandleActionSelection()
        {
            // Wait for the player's action to be submitted by the BattleScene.
            if (!_playerActionSubmitted)
            {
                return;
            }

            // AI Actions
            var activePlayers = _playerCombatants.Where(c => !c.IsDefeated).ToList();
            var activeEnemies = _enemyCombatants.Where(c => !c.IsDefeated).ToList();
            foreach (var enemy in activeEnemies)
            {
                var target = activePlayers.FirstOrDefault();
                if (target != null && enemy.AvailableMoves.Any())
                {
                    var move = enemy.AvailableMoves.First();
                    _actionQueue.Add(new QueuedAction
                    {
                        Actor = enemy,
                        Target = target,
                        ChosenMove = move,
                        Priority = move.Priority,
                        ActorAgility = enemy.Stats.Agility
                    });
                }
            }

            // Build & Sort Action Queue
            _actionQueue = _actionQueue
                .OrderByDescending(a => a.Priority)
                .ThenByDescending(a => a.ActorAgility)
                .ToList();
            // A random roll would be the final tie-breaker here.

            _currentPhase = BattlePhase.ActionResolution;
        }

        /// <summary>
        /// Processes one action from the queue at a time until the queue is empty.
        /// </summary>
        private void HandleActionResolution()
        {
            if (!_actionQueue.Any())
            {
                _currentPhase = BattlePhase.EndOfTurn;
                return;
            }

            var action = _actionQueue[0];

            // Pre-computation (Attacker)
            if (action.Actor.HasStatusEffect(StatusEffectType.Stun))
            {
                EventBus.Publish(new GameEvents.BattleLogMessagePublished { Message = $"{action.Actor.Name} is stunned and cannot move!" });
                action.Actor.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Stun);
                _actionQueue.RemoveAt(0);
                return; // End this step of resolution
            }
            // DoT/HoT effects would be resolved here.

            // Execute Action
            var result = DamageCalculator.CalculateDamage(action.Actor, action.Target, action.ChosenMove);
            action.Target.ApplyDamage(result.DamageAmount);

            // Logging via EventBus
            string logMessage = $"{action.Actor.Name} uses {action.ChosenMove.MoveName} on {action.Target.Name} for {result.DamageAmount} damage.";
            if (result.WasGraze) logMessage += " (Graze)";
            if (result.WasCritical) logMessage += " (Critical Hit!)";
            EventBus.Publish(new GameEvents.BattleLogMessagePublished { Message = logMessage });
            EventBus.Publish(new GameEvents.BattleLogMessagePublished { Message = $"{action.Target.Name} HP: {action.Target.Stats.CurrentHP}/{action.Target.Stats.MaxHP}" });


            _actionQueue.RemoveAt(0);
        }

        /// <summary>
        /// Checks for victory or defeat conditions at the end of the turn.
        /// </summary>
        private void HandleEndOfTurn()
        {
            EventBus.Publish(new GameEvents.BattleLogMessagePublished { Message = $"--- Turn {_turnNumber} End ---" });

            // Victory/Defeat Check
            if (_enemyCombatants.All(c => c.IsDefeated))
            {
                EventBus.Publish(new GameEvents.BattleLogMessagePublished { Message = "Player Wins!" });
                _currentPhase = BattlePhase.BattleOver;
                return;
            }

            if (_playerCombatants.All(c => c.IsDefeated))
            {
                EventBus.Publish(new GameEvents.BattleLogMessagePublished { Message = "Player Loses!" });
                _currentPhase = BattlePhase.BattleOver;
                return;
            }

            // Loop back to the start of the next turn
            _turnNumber++;
            _currentPhase = BattlePhase.StartOfTurn;
        }
    }
}
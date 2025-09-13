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
            CheckForDefeat,
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
        public bool CanAdvance { get; set; } = true;

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

            // Initialize the player's deck manager
            var gameState = ServiceLocator.Get<GameState>();
            foreach (var player in _playerCombatants)
            {
                player.DeckManager = new CombatDeckManager();
                var knownMoves = gameState.PlayerState.SpellbookPages.Where(p => !string.IsNullOrEmpty(p)).ToList();
                player.DeckManager.Initialize(knownMoves);
            }

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

            if (!CanAdvance)
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
                case BattlePhase.CheckForDefeat:
                    HandleCheckForDefeat();
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
            // 1. Player draws to fill hand
            foreach (var player in _playerCombatants)
            {
                if (!player.IsDefeated)
                {
                    player.DeckManager?.DrawToFillHand();
                }
            }

            // 2. Environment Resolution (Placeholder)

            // 3. Duration Countdown
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
                if (target != null)
                {
                    MoveData move;
                    if (enemy.AvailableMoves.Any())
                    {
                        move = enemy.AvailableMoves.First();
                    }
                    else
                    {
                        // Fallback to Stall if no moves are available
                        if (!BattleDataCache.Moves.TryGetValue("Stall", out move))
                        {
                            Debug.WriteLine($"[BattleManager] [FATAL] Could not find 'Stall' move in BattleDataCache.");
                            continue; // Skip this enemy's turn if Stall is missing
                        }
                    }

                    _actionQueue.Add(new QueuedAction
                    {
                        Actor = enemy,
                        Target = target, // Stall still needs a target for the queue, even if it does nothing to it
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
            _actionQueue.RemoveAt(0);

            // Pre-computation (Attacker)
            if (action.Actor.HasStatusEffect(StatusEffectType.Stun))
            {
                EventBus.Publish(new GameEvents.BattleActionExecuted
                {
                    Actor = action.Actor,
                    ChosenMove = action.ChosenMove,
                    Targets = new List<BattleCombatant>(),
                    DamageResults = new List<DamageCalculator.DamageResult>()
                });
                action.Actor.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Stun);
                CanAdvance = false;
                return; // End this step of resolution
            }
            // DoT/HoT effects would be resolved here.

            // If the actor is the player, move the used card to the discard pile.
            if (action.Actor.IsPlayerControlled)
            {
                action.Actor.DeckManager?.CastMove(action.ChosenMove);
            }

            // Resolve targets based on move's TargetType
            var targets = ResolveTargets(action);
            var damageResults = new List<DamageCalculator.DamageResult>();

            if (!targets.Any())
            {
                // Handle moves with TargetType.None
                EventBus.Publish(new GameEvents.BattleActionExecuted
                {
                    Actor = action.Actor,
                    ChosenMove = action.ChosenMove,
                    Targets = targets,
                    DamageResults = damageResults
                });
            }
            else
            {
                float multiTargetModifier = (targets.Count > 1) ? BattleConstants.MULTI_TARGET_MODIFIER : 1.0f;

                foreach (var target in targets)
                {
                    var result = DamageCalculator.CalculateDamage(action.Actor, target, action.ChosenMove, multiTargetModifier);
                    target.ApplyDamage(result.DamageAmount);
                    damageResults.Add(result);
                }

                EventBus.Publish(new GameEvents.BattleActionExecuted
                {
                    Actor = action.Actor,
                    ChosenMove = action.ChosenMove,
                    Targets = targets,
                    DamageResults = damageResults
                });
            }

            _currentPhase = BattlePhase.CheckForDefeat;
            CanAdvance = false; // Pause the manager until the scene says it's okay.
        }

        private List<BattleCombatant> ResolveTargets(QueuedAction action)
        {
            var move = action.ChosenMove;
            var actor = action.Actor;
            var specifiedTarget = action.Target;
            var activeEnemies = _enemyCombatants.Where(c => !c.IsDefeated).ToList();
            var activePlayers = _playerCombatants.Where(c => !c.IsDefeated).ToList();

            switch (move.Target)
            {
                case TargetType.Single:
                    return specifiedTarget != null && !specifiedTarget.IsDefeated ? new List<BattleCombatant> { specifiedTarget } : new List<BattleCombatant>();
                case TargetType.Every:
                    return actor.IsPlayerControlled ? activeEnemies : activePlayers;
                case TargetType.SingleAll:
                    return specifiedTarget != null && !specifiedTarget.IsDefeated ? new List<BattleCombatant> { specifiedTarget } : new List<BattleCombatant>();
                case TargetType.EveryAll:
                    return _allCombatants.Where(c => !c.IsDefeated).ToList();
                case TargetType.Self:
                    return new List<BattleCombatant> { actor };
                case TargetType.None:
                default:
                    return new List<BattleCombatant>();
            }
        }


        /// <summary>
        /// Processes any defeated combatants, allowing for animations and narration to play out.
        /// </summary>
        private void HandleCheckForDefeat()
        {
            // First, check if we are waiting on a combatant's death sequence to finish.
            // CanAdvance being true means the scene is done with the animation/narration.
            var dyingCombatant = _allCombatants.FirstOrDefault(c => c.IsDying);
            if (dyingCombatant != null)
            {
                // The scene has signaled that the animation/narration is complete.
                // Finalize the defeat.
                dyingCombatant.IsDying = false;
                dyingCombatant.IsRemovalProcessed = true;

                // After a death sequence completes, immediately check for game-over conditions.
                if (_playerCombatants.All(c => c.IsDefeated))
                {
                    _currentPhase = BattlePhase.BattleOver;
                    return; // End the turn immediately.
                }
            }

            // Now, check for any newly defeated combatants that haven't been processed yet.
            var newlyDefeated = _allCombatants.FirstOrDefault(c => c.IsDefeated && !c.IsDying && !c.IsRemovalProcessed);
            if (newlyDefeated != null)
            {
                newlyDefeated.IsDying = true;
                EventBus.Publish(new GameEvents.CombatantDefeated { DefeatedCombatant = newlyDefeated });
                CanAdvance = false; // Pause until the scene finishes the death sequence.
                return;
            }

            // If no one is dying or newly defeated, we can proceed.
            if (_actionQueue.Any())
            {
                _currentPhase = BattlePhase.ActionResolution;
            }
            else
            {
                _currentPhase = BattlePhase.EndOfTurn;
            }
        }


        /// <summary>
        /// Checks for victory or defeat conditions at the end of the turn.
        /// </summary>
        private void HandleEndOfTurn()
        {
            // Victory/Defeat Check
            if (_enemyCombatants.All(c => c.IsDefeated) || _playerCombatants.All(c => c.IsDefeated))
            {
                _currentPhase = BattlePhase.BattleOver;
                return;
            }

            // Loop back to the start of the next turn
            _turnNumber++;
            _currentPhase = BattlePhase.StartOfTurn;
        }
    }
}
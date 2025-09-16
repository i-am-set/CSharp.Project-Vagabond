using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;

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
            SecondaryEffectResolution,
            CheckForDefeat,
            EndOfTurn,
            BattleOver
        }

        private readonly List<BattleCombatant> _playerCombatants;
        private readonly List<BattleCombatant> _enemyCombatants;
        private readonly List<BattleCombatant> _allCombatants;
        private List<QueuedAction> _actionQueue;
        private QueuedAction _currentActionForEffects;
        private List<DamageCalculator.DamageResult> _currentActionDamageResults;
        private List<BattleCombatant> _currentActionFinalTargets;
        private BattlePhase _currentPhase;
        private int _turnNumber;
        private bool _playerActionSubmitted;
        private static readonly Random _random = new Random();

        // State for multi-hit moves
        private QueuedAction _currentMultiHitAction;
        private int _multiHitCountRemaining;
        private int _totalHitsForNarration;
        private List<DamageCalculator.DamageResult> _multiHitAggregatedDamageResults;
        private List<BattleCombatant> _multiHitAggregatedFinalTargets;

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

            EventBus.Subscribe<GameEvents.SecondaryEffectComplete>(OnSecondaryEffectComplete);
        }

        private void OnSecondaryEffectComplete(GameEvents.SecondaryEffectComplete e)
        {
            if (_currentPhase == BattlePhase.SecondaryEffectResolution)
            {
                _currentPhase = BattlePhase.CheckForDefeat;
            }
        }

        /// <summary>
        /// Sets the player's chosen action for the current turn.
        /// </summary>
        public void SetPlayerAction(QueuedAction action)
        {
            if (_currentPhase != BattlePhase.ActionSelection || _playerActionSubmitted) return;

            // Handle pre-action effects that might prevent the action from being queued normally.
            if (HandlePreActionEffects(action))
            {
                _playerActionSubmitted = true;
                return; // The effect handled the action (e.g., charging).
            }

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
                case BattlePhase.SecondaryEffectResolution:
                    HandleSecondaryEffectResolution();
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
        /// Handles start-of-turn effects.
        /// </summary>
        private void HandleStartOfTurn()
        {
            var priorityActions = new List<QueuedAction>();

            foreach (var combatant in _allCombatants)
            {
                if (combatant.IsDefeated) continue;

                // Handle charging moves
                if (combatant.ChargingAction != null)
                {
                    combatant.ChargingAction.TurnsRemaining--;
                    if (combatant.ChargingAction.TurnsRemaining <= 0)
                    {
                        priorityActions.Add(combatant.ChargingAction.Action);
                        combatant.ChargingAction = null;
                    }
                }

                // Handle delayed attacks
                if (combatant.DelayedActions.Any())
                {
                    var readyActions = new List<DelayedAction>();
                    foreach (var delayed in combatant.DelayedActions)
                    {
                        delayed.TurnsRemaining--;
                        if (delayed.TurnsRemaining <= 0)
                        {
                            readyActions.Add(delayed);
                        }
                    }
                    foreach (var ready in readyActions)
                    {
                        priorityActions.Add(ready.Action);
                        // This is tricky with a Queue. It's safer to rebuild it.
                    }
                    if (readyActions.Any())
                    {
                        var remaining = combatant.DelayedActions.Where(d => !readyActions.Contains(d)).ToList();
                        combatant.DelayedActions = new Queue<DelayedAction>(remaining);
                    }
                }
            }

            _actionQueue.InsertRange(0, priorityActions);

            // Player draws to fill hand
            foreach (var player in _playerCombatants)
            {
                if (!player.IsDefeated)
                {
                    player.DeckManager?.DrawToFillHand();
                }
            }

            _currentPhase = BattlePhase.ActionSelection;
            _playerActionSubmitted = false;
        }

        /// <summary>
        /// Gathers actions from all combatants and sorts them into an execution queue.
        /// </summary>
        private void HandleActionSelection()
        {
            if (!_playerActionSubmitted) return;

            // AI Actions
            var activePlayers = _playerCombatants.Where(c => !c.IsDefeated).ToList();
            var activeEnemies = _enemyCombatants.Where(c => !c.IsDefeated).ToList();
            foreach (var enemy in activeEnemies)
            {
                if (enemy.ChargingAction != null) continue; // Skip if charging

                var target = activePlayers.FirstOrDefault();
                if (target != null)
                {
                    var possibleMoves = enemy.AvailableMoves;
                    if (enemy.HasStatusEffect(StatusEffectType.Silence))
                    {
                        possibleMoves = possibleMoves.Where(m => m.MoveType != MoveType.Spell).ToList();
                    }

                    MoveData move = possibleMoves.Any() ? possibleMoves.First() : BattleDataCache.Moves["Stall"];

                    var action = new QueuedAction { Actor = enemy, Target = target, ChosenMove = move, Priority = move.Priority, ActorAgility = enemy.GetEffectiveAgility() };

                    if (!HandlePreActionEffects(action))
                    {
                        _actionQueue.Add(action);
                    }
                }
            }

            _actionQueue = _actionQueue.OrderByDescending(a => a.Priority).ThenByDescending(a => a.ActorAgility).ToList();
            _currentPhase = BattlePhase.ActionResolution;
        }

        /// <summary>
        /// Processes one action from the queue at a time until the queue is empty.
        /// This method acts as a state machine for handling single-hit and multi-hit moves.
        /// </summary>
        private void HandleActionResolution()
        {
            // If we are in the middle of a multi-hit move, process the next hit.
            if (_currentMultiHitAction != null)
            {
                ProcessNextHit();
                return;
            }

            // If not, process a new action from the queue.
            if (!_actionQueue.Any())
            {
                _currentPhase = BattlePhase.EndOfTurn;
                return;
            }

            var action = _actionQueue[0];

            // --- Pre-action checks ---
            if (action.Actor.IsDefeated)
            {
                _actionQueue.RemoveAt(0); // Remove the invalid action
                _currentPhase = BattlePhase.CheckForDefeat;
                return;
            }
            if (action.Actor.HasStatusEffect(StatusEffectType.Stun))
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "stunned" });
                action.Actor.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Stun);
                _actionQueue.RemoveAt(0);
                _currentPhase = BattlePhase.CheckForDefeat;
                CanAdvance = false;
                return;
            }
            // ... other pre-action checks would go here ...

            // Announce the action
            EventBus.Publish(new GameEvents.ActionDeclared { Actor = action.Actor, Move = action.ChosenMove, Item = action.ChosenItem });

            if (action.ChosenItem != null)
            {
                _actionQueue.RemoveAt(0); // Consume the action
                ProcessItemAction(action);
                _currentPhase = BattlePhase.SecondaryEffectResolution;
                CanAdvance = false;
            }
            else if (action.ChosenMove != null)
            {
                // Check if this is the start of a new multi-hit move
                if (action.ChosenMove.Effects.TryGetValue("MultiHit", out var multiHitValue) && EffectParser.TryParseIntArray(multiHitValue, out int[] hitParams) && hitParams.Length == 2)
                {
                    _currentMultiHitAction = action;
                    _totalHitsForNarration = _random.Next(hitParams[0], hitParams[1] + 1);
                    _multiHitCountRemaining = _totalHitsForNarration;
                    _multiHitAggregatedDamageResults = new List<DamageCalculator.DamageResult>();
                    _multiHitAggregatedFinalTargets = new List<BattleCombatant>();

                    // Process the very first hit
                    ProcessNextHit();
                }
                else // It's a normal, single-hit move
                {
                    _actionQueue.RemoveAt(0); // Consume the action
                    ProcessSingleHitMove(action);
                    _currentPhase = BattlePhase.SecondaryEffectResolution;
                    CanAdvance = false;
                }
            }
        }

        private void HandleSecondaryEffectResolution()
        {
            SecondaryEffectSystem.ProcessEffects(_currentActionForEffects, _currentActionFinalTargets, _currentActionDamageResults);
            _currentActionForEffects = null;
            _currentActionDamageResults = null;
            _currentActionFinalTargets = null;
        }

        private void ProcessNextHit()
        {
            if (_multiHitCountRemaining > 0)
            {
                _multiHitCountRemaining--;

                var action = _currentMultiHitAction;
                var targetsForThisHit = ResolveTargets(action, isMultiHit: true);

                if (!targetsForThisHit.Any())
                {
                    // If no valid targets remain (e.g., all defeated), skip to the next hit immediately.
                    ProcessNextHit();
                    return;
                }

                var damageResultsForThisHit = new List<DamageCalculator.DamageResult>();
                float multiTargetModifier = (targetsForThisHit.Count > 1) ? BattleConstants.MULTI_TARGET_MODIFIER : 1.0f;

                foreach (var target in targetsForThisHit)
                {
                    var result = DamageCalculator.CalculateDamage(action.Actor, target, action.ChosenMove, multiTargetModifier);
                    target.ApplyDamage(result.DamageAmount);
                    damageResultsForThisHit.Add(result);
                    _multiHitAggregatedFinalTargets.Add(target);
                }
                _multiHitAggregatedDamageResults.AddRange(damageResultsForThisHit);

                EventBus.Publish(new GameEvents.BattleActionExecuted
                {
                    Actor = action.Actor,
                    ChosenMove = action.ChosenMove,
                    Targets = targetsForThisHit,
                    DamageResults = damageResultsForThisHit
                });

                CanAdvance = false; // Wait for this hit's animation
            }
            else // All hits are done
            {
                EventBus.Publish(new GameEvents.MultiHitActionCompleted
                {
                    Actor = _currentMultiHitAction.Actor,
                    ChosenMove = _currentMultiHitAction.ChosenMove,
                    HitCount = _totalHitsForNarration
                });

                _currentActionForEffects = _currentMultiHitAction;
                _currentActionDamageResults = _multiHitAggregatedDamageResults;
                _currentActionFinalTargets = _multiHitAggregatedFinalTargets;

                _actionQueue.Remove(_currentMultiHitAction);
                _currentMultiHitAction = null;
                _multiHitAggregatedDamageResults = null;
                _multiHitAggregatedFinalTargets = null;

                _currentPhase = BattlePhase.SecondaryEffectResolution;
                CanAdvance = false;
            }
        }

        private void ProcessSingleHitMove(QueuedAction action)
        {
            if (action.Actor.IsPlayerControlled)
            {
                action.Actor.DeckManager?.CastMove(action.ChosenMove);
            }

            if (action.ChosenMove.Effects.ContainsKey("RampUp"))
            {
                if (!action.Actor.RampingMoveCounters.ContainsKey(action.ChosenMove.MoveID))
                {
                    action.Actor.RampingMoveCounters[action.ChosenMove.MoveID] = 0;
                }
                action.Actor.RampingMoveCounters[action.ChosenMove.MoveID]++;
            }

            var damageResults = new List<DamageCalculator.DamageResult>();
            var finalTargets = ResolveTargets(action);

            if (finalTargets.Any())
            {
                float multiTargetModifier = (finalTargets.Count > 1) ? BattleConstants.MULTI_TARGET_MODIFIER : 1.0f;
                foreach (var target in finalTargets)
                {
                    var result = DamageCalculator.CalculateDamage(action.Actor, target, action.ChosenMove, multiTargetModifier);
                    target.ApplyDamage(result.DamageAmount);
                    damageResults.Add(result);
                }
            }

            EventBus.Publish(new GameEvents.BattleActionExecuted
            {
                Actor = action.Actor,
                ChosenMove = action.ChosenMove,
                Targets = finalTargets,
                DamageResults = damageResults
            });

            _currentActionForEffects = action;
            _currentActionDamageResults = damageResults;
            _currentActionFinalTargets = finalTargets;
        }

        private void ProcessItemAction(QueuedAction action)
        {
            var gameState = ServiceLocator.Get<GameState>();
            if (!gameState.ConsumeItem(action.ChosenItem.ItemID))
            {
                Debug.WriteLine($"[BattleManager] [ERROR] Failed to consume item '{action.ChosenItem.ItemID}'.");
                return;
            }

            var targets = ResolveTargets(action);
            var damageResults = new List<DamageCalculator.DamageResult>();

            switch (action.ChosenItem.Type)
            {
                case ConsumableType.Heal:
                    foreach (var target in targets)
                    {
                        int hpBefore = (int)target.VisualHP;
                        target.ApplyHealing(action.ChosenItem.PrimaryValue);
                        EventBus.Publish(new GameEvents.CombatantHealed
                        {
                            Actor = action.Actor,
                            Target = target,
                            HealAmount = action.ChosenItem.PrimaryValue,
                            VisualHPBefore = hpBefore
                        });
                    }
                    break;
                case ConsumableType.Attack:
                    if (!string.IsNullOrEmpty(action.ChosenItem.MoveID) && BattleDataCache.Moves.TryGetValue(action.ChosenItem.MoveID, out var moveData))
                    {
                        float multiTargetModifier = (targets.Count > 1) ? BattleConstants.MULTI_TARGET_MODIFIER : 1.0f;
                        foreach (var target in targets)
                        {
                            var result = DamageCalculator.CalculateDamage(action.Actor, target, moveData, multiTargetModifier);
                            target.ApplyDamage(result.DamageAmount);
                            damageResults.Add(result);
                        }
                        EventBus.Publish(new GameEvents.BattleActionExecuted { Actor = action.Actor, ChosenMove = moveData, UsedItem = action.ChosenItem, Targets = targets, DamageResults = damageResults });
                    }
                    break;
            }
            _currentActionForEffects = action;
            _currentActionDamageResults = damageResults;
            _currentActionFinalTargets = targets;
        }

        private List<BattleCombatant> ResolveTargets(QueuedAction action, bool isMultiHit = false)
        {
            var targetType = action.ChosenMove?.Target ?? action.ChosenItem?.Target ?? TargetType.None;
            var actor = action.Actor;
            var specifiedTarget = action.Target;
            var activeEnemies = _enemyCombatants.Where(c => !c.IsDefeated).ToList();
            var activePlayers = _playerCombatants.Where(c => !c.IsDefeated).ToList();

            if (isMultiHit && (targetType == TargetType.Every || targetType == TargetType.EveryAll))
            {
                var possibleTargets = (targetType == TargetType.Every)
                    ? (actor.IsPlayerControlled ? activeEnemies : activePlayers)
                    : _allCombatants.Where(c => !c.IsDefeated).ToList();
                if (possibleTargets.Any())
                {
                    return new List<BattleCombatant> { possibleTargets[_random.Next(possibleTargets.Count)] };
                }
                return new List<BattleCombatant>();
            }

            switch (targetType)
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

        private void HandleCheckForDefeat()
        {
            var dyingCombatant = _allCombatants.FirstOrDefault(c => c.IsDying);
            if (dyingCombatant != null)
            {
                dyingCombatant.IsDying = false;
                dyingCombatant.IsRemovalProcessed = true;
                if (_playerCombatants.All(c => c.IsDefeated))
                {
                    _currentPhase = BattlePhase.BattleOver;
                    return;
                }
            }

            var newlyDefeated = _allCombatants.FirstOrDefault(c => c.IsDefeated && !c.IsDying && !c.IsRemovalProcessed);
            if (newlyDefeated != null)
            {
                newlyDefeated.IsDying = true;
                EventBus.Publish(new GameEvents.CombatantDefeated { DefeatedCombatant = newlyDefeated });
                CanAdvance = false;
                return;
            }

            if (_actionQueue.Any() || _currentMultiHitAction != null)
            {
                _currentPhase = BattlePhase.ActionResolution;
            }
            else
            {
                _currentPhase = BattlePhase.EndOfTurn;
            }
        }

        private void HandleEndOfTurn()
        {
            foreach (var combatant in _allCombatants)
            {
                if (combatant.IsDefeated) continue;
                var effectsToRemove = new List<StatusEffectInstance>();
                foreach (var effect in combatant.ActiveStatusEffects)
                {
                    effect.DurationInTurns--;
                    if (effect.EffectType == StatusEffectType.Poison)
                    {
                        int poisonDamage = Math.Max(1, combatant.Stats.MaxHP / 16);
                        combatant.ApplyDamage(poisonDamage);
                        EventBus.Publish(new GameEvents.StatusEffectTriggered { Combatant = combatant, EffectType = StatusEffectType.Poison, Damage = poisonDamage });
                    }
                    if (effect.DurationInTurns <= 0)
                    {
                        effectsToRemove.Add(effect);
                    }
                }
                foreach (var expiredEffect in effectsToRemove)
                {
                    combatant.ActiveStatusEffects.Remove(expiredEffect);
                    EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = combatant, EffectType = expiredEffect.EffectType });
                }
            }

            if (_enemyCombatants.All(c => c.IsDefeated) || _playerCombatants.All(c => c.IsDefeated))
            {
                _currentPhase = BattlePhase.BattleOver;
                return;
            }

            _turnNumber++;
            _currentPhase = BattlePhase.StartOfTurn;
        }

        private bool HandlePreActionEffects(QueuedAction action)
        {
            var move = action.ChosenMove;
            if (move == null) return false;

            if (move.Effects.TryGetValue("Charge", out var chargeValue) && EffectParser.TryParseInt(chargeValue, out int chargeTurns))
            {
                action.Actor.ChargingAction = new DelayedAction { Action = action, TurnsRemaining = chargeTurns };
                EventBus.Publish(new GameEvents.CombatantChargingAction { Actor = action.Actor, MoveName = move.MoveName });
                return true;
            }

            if (move.Effects.TryGetValue("DelayedAttack", out var delayValue) && EffectParser.TryParseInt(delayValue, out int delayTurns))
            {
                action.Actor.DelayedActions.Enqueue(new DelayedAction { Action = action, TurnsRemaining = delayTurns });
                return true;
            }

            if (move.Effects.TryGetValue("HPCost", out var hpCostValue) && EffectParser.TryParseFloat(hpCostValue, out float hpCostPercent))
            {
                int cost = (int)(action.Actor.Stats.MaxHP * (hpCostPercent / 100f));
                action.Actor.ApplyDamage(cost);
            }

            if (move.Effects.TryGetValue("Gamble", out var gambleValue) && EffectParser.TryParseFloatArray(gambleValue, out float[] gambleParams) && gambleParams.Length >= 1)
            {
                float chance = gambleParams[0];
                if (_random.Next(1, 101) > chance)
                {
                    // Gamble failed. For now, just fail the action.
                    EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "bad luck" });
                    return true;
                }
            }

            return false; // Action should be queued normally
        }
    }
}

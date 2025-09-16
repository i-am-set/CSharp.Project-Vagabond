using ProjectVagabond.Battle;
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
        private BattlePhase _currentPhase;
        private int _turnNumber;
        private bool _playerActionSubmitted;
        private static readonly Random _random = new Random();

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

            // Handle Confusion
            if (action.Actor.HasStatusEffect(StatusEffectType.Confuse) && _random.NextDouble() < 0.25)
            {
                var availableMoves = action.Actor.AvailableMoves.Where(m => m != action.ChosenMove).ToList();
                if (availableMoves.Any())
                {
                    var originalMove = action.ChosenMove;
                    var newMove = availableMoves[_random.Next(availableMoves.Count)];
                    action.ChosenMove = newMove;
                    action.Priority = newMove.Priority;

                    bool originalNeedsTarget = originalMove.Target == TargetType.Single || originalMove.Target == TargetType.SingleAll;
                    bool newNeedsTarget = newMove.Target == TargetType.Single || newMove.Target == TargetType.SingleAll;

                    if (newNeedsTarget && !originalNeedsTarget)
                    {
                        var allPossibleTargets = _allCombatants.Where(c => !c.IsDefeated).ToList();
                        if (allPossibleTargets.Any())
                        {
                            action.Target = allPossibleTargets[_random.Next(allPossibleTargets.Count)];
                        }
                    }
                    EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "confused" });
                }
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
            // 1. Player draws to fill hand
            foreach (var player in _playerCombatants)
            {
                if (!player.IsDefeated)
                {
                    player.DeckManager?.DrawToFillHand();
                }
            }

            // 2. Environment Resolution (Placeholder)

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
                    var possibleMoves = enemy.AvailableMoves;
                    if (enemy.HasStatusEffect(StatusEffectType.Silence))
                    {
                        possibleMoves = possibleMoves.Where(m => m.MoveType != MoveType.Spell).ToList();
                    }

                    MoveData move;
                    if (possibleMoves.Any())
                    {
                        move = possibleMoves.First(); // Simple AI: always pick the first valid move
                    }
                    else
                    {
                        if (!BattleDataCache.Moves.TryGetValue("Stall", out move))
                        {
                            Debug.WriteLine($"[BattleManager] [FATAL] Could not find 'Stall' move in BattleDataCache.");
                            continue;
                        }
                    }

                    var action = new QueuedAction
                    {
                        Actor = enemy,
                        Target = target,
                        ChosenMove = move,
                        Priority = move.Priority,
                        ActorAgility = enemy.GetEffectiveAgility()
                    };

                    // Handle AI Confusion
                    if (enemy.HasStatusEffect(StatusEffectType.Confuse) && _random.NextDouble() < 0.25)
                    {
                        var availableMoves = enemy.AvailableMoves.Where(m => m != action.ChosenMove).ToList();
                        if (availableMoves.Any())
                        {
                            action.ChosenMove = availableMoves[_random.Next(availableMoves.Count)];
                            action.Priority = action.ChosenMove.Priority;
                            EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "confused" });
                        }
                    }

                    _actionQueue.Add(action);
                }
            }

            // Build & Sort Action Queue using effective agility
            _actionQueue = _actionQueue
                .OrderByDescending(a => a.Priority)
                .ThenByDescending(a => a.Actor.GetEffectiveAgility())
                .ToList();

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
            _currentActionForEffects = action;
            _currentActionDamageResults = new List<DamageCalculator.DamageResult>(); // Reset damage results

            // Liveness Check: Skip the action if the actor was defeated by a prior action in the same turn.
            if (action.Actor.IsDefeated)
            {
                _currentPhase = BattlePhase.SecondaryEffectResolution;
                return;
            }

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
                _currentPhase = BattlePhase.SecondaryEffectResolution;
                return;
            }

            if (action.Actor.HasStatusEffect(StatusEffectType.Silence) && action.ChosenMove?.MoveType == MoveType.Spell)
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "silenced" });
                CanAdvance = false;
                _currentPhase = BattlePhase.SecondaryEffectResolution;
                return;
            }

            if (action.Actor.HasStatusEffect(StatusEffectType.Burn) && action.ChosenMove?.MakesContact == true)
            {
                int burnDamage = Math.Max(1, action.Actor.Stats.MaxHP / 16);
                action.Actor.ApplyDamage(burnDamage);
                EventBus.Publish(new GameEvents.StatusEffectTriggered { Combatant = action.Actor, EffectType = StatusEffectType.Burn, Damage = burnDamage });
            }

            if (action.ChosenItem != null)
            {
                ProcessItemAction(action);
            }
            else if (action.ChosenMove != null)
            {
                ProcessMoveAction(action);
            }

            _currentPhase = BattlePhase.SecondaryEffectResolution;
            CanAdvance = false; // Pause the manager until the scene says it's okay.
        }

        private void HandleSecondaryEffectResolution()
        {
            // This phase is entered, and we immediately process the effects.
            // The system will publish an event when it's done.
            // The BattleManager will then wait for that event to transition state.
            SecondaryEffectSystem.ProcessEffects(_currentActionForEffects, _currentActionDamageResults);
            _currentActionForEffects = null; // Clear it after passing it to the system
            _currentActionDamageResults = null;
        }

        private void ProcessMoveAction(QueuedAction action)
        {
            // If the actor is the player, move the used card to the discard pile.
            if (action.Actor.IsPlayerControlled)
            {
                action.Actor.DeckManager?.CastMove(action.ChosenMove);
            }

            var targets = ResolveTargets(action);
            var damageResults = new List<DamageCalculator.DamageResult>();

            if (!targets.Any())
            {
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

                    // Post-damage status effect interactions
                    if (target.HasStatusEffect(StatusEffectType.Freeze) && action.ChosenMove?.ImpactType == ImpactType.Physical)
                    {
                        target.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Freeze);
                        EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = target, EffectType = StatusEffectType.Freeze });
                    }
                    if (target.HasStatusEffect(StatusEffectType.Freeze) && action.ChosenMove?.OffensiveElementIDs.Contains(2) == true) // 2 = Fire
                    {
                        var freezeEffect = target.ActiveStatusEffects.FirstOrDefault(e => e.EffectType == StatusEffectType.Freeze);
                        if (freezeEffect != null) freezeEffect.DurationInTurns--;
                    }
                }
                EventBus.Publish(new GameEvents.BattleActionExecuted
                {
                    Actor = action.Actor,
                    ChosenMove = action.ChosenMove,
                    Targets = targets,
                    DamageResults = damageResults
                });
            }

            _currentActionDamageResults = damageResults;

            if (action.Actor.HasStatusEffect(StatusEffectType.Freeze) && action.ChosenMove?.OffensiveElementIDs.Contains(2) == true) // 2 = Fire
            {
                var freezeEffect = action.Actor.ActiveStatusEffects.FirstOrDefault(e => e.EffectType == StatusEffectType.Freeze);
                if (freezeEffect != null) freezeEffect.DurationInTurns--;
            }
        }

        private void ProcessItemAction(QueuedAction action)
        {
            var gameState = ServiceLocator.Get<GameState>();
            if (!gameState.ConsumeItem(action.ChosenItem.ItemID))
            {
                Debug.WriteLine($"[BattleManager] [ERROR] Failed to consume item '{action.ChosenItem.ItemID}'. It may have been removed from inventory unexpectedly.");
                return; // Skip action if item can't be consumed.
            }

            var targets = ResolveTargets(action);
            var healAmounts = new List<int>();
            var damageResults = new List<DamageCalculator.DamageResult>();

            switch (action.ChosenItem.Type)
            {
                case ConsumableType.Heal:
                    foreach (var target in targets)
                    {
                        target.ApplyHealing(action.ChosenItem.PrimaryValue);
                        healAmounts.Add(action.ChosenItem.PrimaryValue);
                    }
                    EventBus.Publish(new GameEvents.BattleItemUsed
                    {
                        Actor = action.Actor,
                        UsedItem = action.ChosenItem,
                        Targets = targets,
                        HealAmounts = healAmounts
                    });
                    break;

                case ConsumableType.Buff:
                    // Placeholder for buff logic
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
                        EventBus.Publish(new GameEvents.BattleActionExecuted
                        {
                            Actor = action.Actor,
                            ChosenMove = moveData,
                            UsedItem = action.ChosenItem, // Pass the item for correct narration
                            Targets = targets,
                            DamageResults = damageResults
                        });
                    }
                    break;
            }
            _currentActionDamageResults = damageResults;
        }

        private List<BattleCombatant> ResolveTargets(QueuedAction action)
        {
            var targetType = action.ChosenMove?.Target ?? action.ChosenItem?.Target ?? TargetType.None;
            var actor = action.Actor;
            var specifiedTarget = action.Target;
            var activeEnemies = _enemyCombatants.Where(c => !c.IsDefeated).ToList();
            var activePlayers = _playerCombatants.Where(c => !c.IsDefeated).ToList();

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
        /// Checks for victory or defeat conditions and handles end-of-turn status effects.
        /// </summary>
        private void HandleEndOfTurn()
        {
            // Handle status effect duration countdowns and passive effects
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
                // Remove expired effects
                foreach (var expiredEffect in effectsToRemove)
                {
                    combatant.ActiveStatusEffects.Remove(expiredEffect);
                    EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = combatant, EffectType = expiredEffect.EffectType });
                }
            }

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
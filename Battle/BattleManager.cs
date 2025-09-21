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
        public int RoundNumber { get; private set; }
        private bool _playerActionSubmitted;
        private static readonly Random _random = new Random();

        // State for multi-hit moves
        private QueuedAction _currentMultiHitAction;
        private int _multiHitCountRemaining;
        private int _totalHitsForNarration;
        private List<DamageCalculator.DamageResult> _multiHitAggregatedDamageResults;
        private List<BattleCombatant> _multiHitAggregatedFinalTargets;

        // State for action resolution flow
        private QueuedAction _actionToExecute;
        private bool _endOfTurnEffectsProcessed;

        public BattlePhase CurrentPhase => _currentPhase;
        public IEnumerable<BattleCombatant> AllCombatants => _allCombatants;
        public bool CanAdvance { get; set; } = true;
        public bool IsPlayerTurnSkipped { get; private set; }
        public bool IsProcessingMultiHit => _currentMultiHitAction != null;


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
            RoundNumber = 1;
            _currentPhase = BattlePhase.StartOfTurn;
            _endOfTurnEffectsProcessed = false;

            EventBus.Subscribe<GameEvents.SecondaryEffectComplete>(OnSecondaryEffectComplete);

            // Process on-enter abilities
            HandleOnEnterAbilities();
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
            IsPlayerTurnSkipped = false;
            _endOfTurnEffectsProcessed = false;
            var startOfTurnActions = new List<QueuedAction>();

            foreach (var combatant in _allCombatants)
            {
                if (combatant.IsDefeated) continue;

                // Handle charging moves
                if (combatant.ChargingAction != null)
                {
                    combatant.ChargingAction.TurnsRemaining--;
                    if (combatant.ChargingAction.TurnsRemaining <= 0)
                    {
                        // Charge is complete, queue the action
                        startOfTurnActions.Add(combatant.ChargingAction.Action);
                        combatant.ChargingAction = null;
                        if (combatant.IsPlayerControlled)
                        {
                            IsPlayerTurnSkipped = true;
                        }
                    }
                    else
                    {
                        // Still charging, player turn will be skipped for selection.
                        if (combatant.IsPlayerControlled)
                        {
                            IsPlayerTurnSkipped = true;
                        }
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
                        startOfTurnActions.Add(ready.Action);
                        // This is tricky with a Queue. It's safer to rebuild it.
                    }
                    if (readyActions.Any())
                    {
                        var remaining = combatant.DelayedActions.Where(d => !readyActions.Contains(d)).ToList();
                        combatant.DelayedActions = new Queue<DelayedAction>(remaining);
                    }
                }
            }

            _actionQueue.InsertRange(0, startOfTurnActions);

            // Player draws to fill hand
            foreach (var player in _playerCombatants)
            {
                if (!player.IsDefeated)
                {
                    player.DeckManager?.DrawToFillHand();
                }
            }

            _currentPhase = BattlePhase.ActionSelection;
            _playerActionSubmitted = IsPlayerTurnSkipped;
        }

        /// <summary>
        /// Gathers actions from all combatants and sorts them into an execution queue.
        /// </summary>
        private void HandleActionSelection()
        {
            if (!_playerActionSubmitted) return;

            // Inject placeholder "Charging" actions for any combatant that is currently charging.
            foreach (var combatant in _allCombatants)
            {
                if (combatant.ChargingAction != null)
                {
                    var chargingAction = new QueuedAction
                    {
                        Actor = combatant,
                        ChosenMove = combatant.ChargingAction.Action.ChosenMove,
                        ActorAgility = combatant.GetEffectiveAgility(),
                        Priority = 100, // High priority to show charging message before other actions of same agility
                        Type = QueuedActionType.Charging
                    };
                    _actionQueue.Add(chargingAction);
                }
            }

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

                    var action = CreateActionFromMove(enemy, move, target);

                    if (!HandlePreActionEffects(action))
                    {
                        _actionQueue.Add(action);
                    }
                }
            }

            _actionQueue = _actionQueue.OrderByDescending(a => a.Priority).ThenByDescending(a => a.ActorAgility).ToList();

            // Set the flag for the last action in the round
            var lastAction = _actionQueue.LastOrDefault(a => a.Type == QueuedActionType.Move || a.Type == QueuedActionType.Item);
            if (lastAction != null)
            {
                lastAction.IsLastActionInRound = true;
            }

            _currentPhase = BattlePhase.ActionResolution;
        }

        /// <summary>
        /// Processes one action from the queue at a time until the queue is empty.
        /// This method acts as a state machine for handling single-hit and multi-hit moves.
        /// </summary>
        private void HandleActionResolution()
        {
            // State 1: We are in the middle of a multi-hit move.
            if (_currentMultiHitAction != null)
            {
                ProcessHit();
                return;
            }

            // State 2: We have an action that has been announced and is ready for execution.
            if (_actionToExecute != null)
            {
                var action = _actionToExecute;
                _actionToExecute = null; // Consume it.

                // This is where the action's effects begin.
                if (action.ChosenItem != null)
                {
                    ProcessItemAction(action);
                    _currentPhase = BattlePhase.SecondaryEffectResolution;
                    CanAdvance = false;
                }
                else if (action.ChosenMove != null)
                {
                    // This will either fully process a single-hit move, or set up the multi-hit state machine.
                    ProcessMoveAction(action);
                }
                return;
            }

            // State 3: We need to start a new action from the queue.
            if (!_actionQueue.Any())
            {
                _currentPhase = BattlePhase.EndOfTurn;
                return;
            }

            var nextAction = _actionQueue[0];
            _actionQueue.RemoveAt(0);

            // Handle special types that don't need narration/execution split
            if (nextAction.Type == QueuedActionType.Charging)
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = nextAction.Actor, Reason = $"charging {nextAction.ChosenMove.MoveName}" });
                CanAdvance = false;
                return; // Done with this action, next Update will process next in queue
            }

            // --- Pre-action checks ---
            if (nextAction.Actor.IsDefeated)
            {
                return; // Actor was defeated before their turn, just skip.
            }
            if (nextAction.Actor.HasStatusEffect(StatusEffectType.Stun))
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = nextAction.Actor, Reason = "stunned" });
                nextAction.Actor.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Stun);
                CanAdvance = false;
                return;
            }

            // Handle pre-resolution effects like HP cost or gamble
            if (!ProcessPreResolutionEffects(nextAction))
            {
                CanAdvance = false;
                return; // Effect failed (e.g. gamble), so stop the action.
            }

            // If the action is valid, announce it and wait.
            _actionToExecute = nextAction;
            EventBus.Publish(new GameEvents.ActionDeclared { Actor = _actionToExecute.Actor, Move = _actionToExecute.ChosenMove, Item = _actionToExecute.ChosenItem });
            CanAdvance = false;
        }

        private void HandleSecondaryEffectResolution()
        {
            SecondaryEffectSystem.ProcessEffects(_currentActionForEffects, _currentActionFinalTargets, _currentActionDamageResults);
            _currentActionForEffects = null;
            _currentActionDamageResults = null;
            _currentActionFinalTargets = null;
        }

        private void ProcessMoveAction(QueuedAction action)
        {
            if (action.Actor.IsPlayerControlled)
            {
                action.Actor.DeckManager?.CastMove(action.ChosenMove);
            }

            if (action.ChosenMove.Effects.TryGetValue("MultiHit", out var multiHitValue) && EffectParser.TryParseIntArray(multiHitValue, out int[] hitParams) && hitParams.Length == 2)
            {
                _currentMultiHitAction = action;
                _totalHitsForNarration = _random.Next(hitParams[0], hitParams[1] + 1);
                _multiHitCountRemaining = _totalHitsForNarration;
                _multiHitAggregatedDamageResults = new List<DamageCalculator.DamageResult>();
                _multiHitAggregatedFinalTargets = new List<BattleCombatant>();
            }
            else
            {
                // It's a single-hit move, set it up for the hit processor
                _currentMultiHitAction = action;
                _multiHitCountRemaining = 1;
                _multiHitAggregatedDamageResults = new List<DamageCalculator.DamageResult>();
                _multiHitAggregatedFinalTargets = new List<BattleCombatant>();
            }
            // The phase remains ActionResolution. The next Update() will call ProcessHit().
            ProcessHit();
        }

        private void ProcessHit()
        {
            if (_multiHitCountRemaining > 0)
            {
                _multiHitCountRemaining--;

                var action = _currentMultiHitAction;
                var targetsForThisHit = ResolveTargets(action, isMultiHit: _totalHitsForNarration > 1);

                if (targetsForThisHit.Any())
                {
                    var damageResultsForThisHit = new List<DamageCalculator.DamageResult>();
                    float multiTargetModifier = (targetsForThisHit.Count > 1) ? BattleConstants.MULTI_TARGET_MODIFIER : 1.0f;

                    foreach (var target in targetsForThisHit)
                    {
                        var moveInstance = HandlePreDamageEffects(action.ChosenMove, target);
                        var result = DamageCalculator.CalculateDamage(action, target, moveInstance, multiTargetModifier);
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
                }

                CanAdvance = false; // Wait for this hit's animation
            }

            // Check if all hits are done (for both single and multi-hit moves)
            if (_multiHitCountRemaining <= 0)
            {
                var actor = _currentMultiHitAction.Actor;
                var move = _currentMultiHitAction.ChosenMove;

                // Set the "first attack" flag after the damage calculation for the first move is complete.
                if (!actor.HasUsedFirstAttack)
                {
                    actor.HasUsedFirstAttack = true;
                }

                // Handle Spellweaver activation
                if (move.MoveType == MoveType.Action)
                {
                    foreach (var ability in actor.ActiveAbilities)
                    {
                        if (ability.Effects.ContainsKey("Spellweaver"))
                        {
                            actor.IsSpellweaverActive = true;
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = actor, Ability = ability, NarrationText = $"{actor.Name}'s {ability.AbilityName} is now active!" });
                        }
                    }
                }
                else if (move.MoveType == MoveType.Spell)
                {
                    // Consume Spellweaver if it was active
                    if (actor.IsSpellweaverActive)
                    {
                        actor.IsSpellweaverActive = false;
                    }
                }

                // Consume Momentum if it was active and the move dealt damage
                if (actor.IsMomentumActive && move.Power > 0)
                {
                    actor.IsMomentumActive = false;
                }


                if (_totalHitsForNarration > 1)
                {
                    int criticalHitCount = _multiHitAggregatedDamageResults.Count(r => r.WasCritical);
                    EventBus.Publish(new GameEvents.MultiHitActionCompleted
                    {
                        Actor = _currentMultiHitAction.Actor,
                        ChosenMove = _currentMultiHitAction.ChosenMove,
                        HitCount = _totalHitsForNarration,
                        CriticalHitCount = criticalHitCount
                    });
                }

                _currentActionForEffects = _currentMultiHitAction;
                _currentActionDamageResults = _multiHitAggregatedDamageResults;
                _currentActionFinalTargets = _multiHitAggregatedFinalTargets;

                _currentMultiHitAction = null;
                _multiHitAggregatedDamageResults = null;
                _multiHitAggregatedFinalTargets = null;

                _currentPhase = BattlePhase.SecondaryEffectResolution;
                CanAdvance = false;
            }
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
                            var moveInstance = HandlePreDamageEffects(moveData, target);
                            var result = DamageCalculator.CalculateDamage(action, target, moveInstance, multiTargetModifier);
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

            if (_actionQueue.Any() || _currentMultiHitAction != null || _actionToExecute != null)
            {
                _currentPhase = BattlePhase.ActionResolution;
            }
            else if (!_endOfTurnEffectsProcessed)
            {
                _currentPhase = BattlePhase.EndOfTurn;
            }
            else
            {
                RoundNumber++;
                _currentPhase = BattlePhase.StartOfTurn;
            }
        }

        private void HandleEndOfTurn()
        {
            _endOfTurnEffectsProcessed = true;

            foreach (var combatant in _allCombatants)
            {
                if (combatant.IsDefeated) continue;

                // Handle end-of-turn abilities
                foreach (var ability in combatant.ActiveAbilities)
                {
                    // Regeneration
                    if (ability.Effects.TryGetValue("RegenEndOfTurn", out var regenValue) && EffectParser.TryParseFloat(regenValue, out float healPercent))
                    {
                        int hpBefore = (int)combatant.VisualHP;
                        int healAmount = (int)(combatant.Stats.MaxHP * (healPercent / 100f));
                        if (healAmount > 0)
                        {
                            combatant.ApplyHealing(healAmount);
                            EventBus.Publish(new GameEvents.CombatantHealed
                            {
                                Actor = combatant,
                                Target = combatant,
                                HealAmount = healAmount,
                                VisualHPBefore = hpBefore
                            });
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = combatant, Ability = ability });
                        }
                    }

                    // Toxic Aura
                    if (ability.Effects.TryGetValue("AuraApplyStatusEndOfTurn", out var auraValue))
                    {
                        if (EffectParser.TryParseStatusEffectParams(auraValue, out var type, out int chance, out int duration))
                        {
                            if (_random.Next(1, 101) <= chance)
                            {
                                var potentialTargets = combatant.IsPlayerControlled ? _enemyCombatants : _playerCombatants;
                                var validTargets = potentialTargets.Where(c => !c.IsDefeated && !c.HasStatusEffect(type)).ToList();
                                if (validTargets.Any())
                                {
                                    var target = validTargets[_random.Next(validTargets.Count)];
                                    target.AddStatusEffect(new StatusEffectInstance(type, duration));
                                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = combatant, Ability = ability, NarrationText = $"{combatant.Name}'s {ability.AbilityName} afflicted {target.Name}!" });
                                }
                            }
                        }
                    }
                }

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

                // Handle Escalation at the very end of the turn's effects
                foreach (var ability in combatant.ActiveAbilities)
                {
                    if (ability.Effects.TryGetValue("Escalation", out var escalationValue) && EffectParser.TryParseIntArray(escalationValue, out int[] p) && p.Length == 2)
                    {
                        int maxStacks = p[1];
                        if (combatant.EscalationStacks < maxStacks)
                        {
                            combatant.EscalationStacks++;
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = combatant, Ability = ability });
                        }
                    }
                }
            }

            _currentPhase = BattlePhase.CheckForDefeat;
        }

        private void HandleOnEnterAbilities()
        {
            foreach (var combatant in _allCombatants)
            {
                foreach (var ability in combatant.ActiveAbilities)
                {
                    // Intimidate
                    if (ability.Effects.TryGetValue("IntimidateOnEnter", out var value) && EffectParser.TryParseInt(value, out int duration))
                    {
                        var opponents = combatant.IsPlayerControlled ? _enemyCombatants : _playerCombatants;
                        foreach (var opponent in opponents)
                        {
                            if (!opponent.IsDefeated)
                            {
                                opponent.AddStatusEffect(new StatusEffectInstance(StatusEffectType.StrengthDown, duration));
                            }
                        }
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = combatant, Ability = ability, NarrationText = $"{combatant.Name}'s Intimidate lowered the opponents' Strength!" });
                    }
                }
            }
        }

        public QueuedAction CreateActionFromMove(BattleCombatant actor, MoveData move, BattleCombatant target)
        {
            MoveData moveInstance = move;
            int priority = move.Priority;

            if (!actor.HasUsedFirstAttack)
            {
                foreach (var ability in actor.ActiveAbilities)
                {
                    if (ability.Effects.TryGetValue("AmbushPredator", out var value) && EffectParser.TryParseIntArray(value, out int[] p) && p.Length == 2)
                    {
                        priority += p[0];

                        moveInstance = move.Clone();
                        float powerModifier = 1.0f + (p[1] / 100f);
                        moveInstance.Power = (int)(moveInstance.Power * powerModifier);

                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = actor, Ability = ability });
                        break;
                    }
                }
            }

            return new QueuedAction
            {
                Actor = actor,
                Target = target,
                ChosenMove = moveInstance,
                Priority = priority,
                ActorAgility = actor.GetEffectiveAgility(),
                Type = QueuedActionType.Move
            };
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

            return false; // Action should be queued normally
        }

        private bool ProcessPreResolutionEffects(QueuedAction action)
        {
            var move = action.ChosenMove;
            if (move == null) return true;

            if (move.Effects.TryGetValue("HPCost", out var hpCostValue) && EffectParser.TryParseFloat(hpCostValue, out float hpCostPercent))
            {
                int cost = (int)(action.Actor.Stats.MaxHP * (hpCostPercent / 100f));
                action.Actor.ApplyDamage(cost);
            }

            foreach (var ability in action.Actor.ActiveAbilities)
            {
                if (ability.Effects.TryGetValue("Bloodletter", out var bloodletterValue) && EffectParser.TryParseFloatArray(bloodletterValue, out float[] p) && p.Length == 2)
                {
                    // Void element ID is 9
                    if (move.MoveType == MoveType.Spell && move.OffensiveElementIDs.Contains(9))
                    {
                        int cost = (int)(action.Actor.Stats.MaxHP * (p[0] / 100f));
                        action.Actor.ApplyDamage(cost);
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = action.Actor, Ability = ability, NarrationText = $"{action.Actor.Name} pays the price for {ability.AbilityName}!" });
                    }
                }
            }

            if (move.Effects.TryGetValue("Gamble", out var gambleValue) && EffectParser.TryParseFloatArray(gambleValue, out float[] gambleParams) && gambleParams.Length >= 1)
            {
                float chance = gambleParams[0];
                if (_random.Next(1, 101) > chance)
                {
                    EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "bad luck" });
                    return false; // Gamble failed, stop the action.
                }
            }

            return true; // Action can proceed.
        }

        private MoveData HandlePreDamageEffects(MoveData originalMove, BattleCombatant target)
        {
            var moveInstance = originalMove; // Start with the original

            if (moveInstance.Effects.TryGetValue("DetonateStatus", out var detonateValue))
            {
                var parts = detonateValue.Split(',');
                if (parts.Length == 2 &&
                    Enum.TryParse<StatusEffectType>(parts[0].Trim(), true, out var statusTypeToDetonate) &&
                    EffectParser.TryParseFloat(parts[1].Trim(), out float multiplier))
                {
                    if (target.HasStatusEffect(statusTypeToDetonate))
                    {
                        // Create a temporary copy of the move to modify its power for this one calculation
                        moveInstance = originalMove.Clone();
                        moveInstance.Power = (int)(moveInstance.Power * multiplier);
                        target.ActiveStatusEffects.RemoveAll(e => e.EffectType == statusTypeToDetonate);
                        EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = target, EffectType = statusTypeToDetonate });
                    }
                }
            }

            // Add other pre-damage effects here

            return moveInstance;
        }
    }
}
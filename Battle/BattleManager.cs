using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle
{
    public class BattleManager
    {
        public enum BattlePhase
        {
            StartOfTurn,
            ActionSelection,
            ActionResolution,
            AnimatingMove,
            SecondaryEffectResolution,
            CheckForDefeat,
            EndOfTurn,
            BattleOver
        }

        private readonly List<BattleCombatant> _playerCombatants;
        private readonly List<BattleCombatant> _enemyCombatants;
        private readonly List<BattleCombatant> _allCombatants;
        private List<QueuedAction> _actionQueue;
        private QueuedAction? _currentActionForEffects;
        private List<DamageCalculator.DamageResult> _currentActionDamageResults;
        private List<BattleCombatant> _currentActionFinalTargets;
        private BattlePhase _currentPhase;
        public int RoundNumber { get; private set; }
        private bool _playerActionSubmitted;
        private static readonly Random _random = new Random();

        private QueuedAction? _currentMultiHitAction;
        private int _multiHitCountRemaining;
        private int _totalHitsForNarration;
        private List<DamageCalculator.DamageResult> _multiHitAggregatedDamageResults;
        private List<BattleCombatant> _multiHitAggregatedFinalTargets;

        private QueuedAction? _actionToExecute;
        private QueuedAction? _actionPendingAnimation;
        private bool _endOfTurnEffectsProcessed;

        public BattlePhase CurrentPhase => _currentPhase;
        public IEnumerable<BattleCombatant> AllCombatants => _allCombatants;
        public bool CanAdvance { get; set; } = true;
        public bool IsPlayerTurnSkipped { get; private set; }
        public bool IsProcessingMultiHit => _currentMultiHitAction != null;


        public BattleManager(List<BattleCombatant> playerParty, List<BattleCombatant> enemyParty)
        {
            _playerCombatants = playerParty;
            _enemyCombatants = enemyParty;
            _allCombatants = new List<BattleCombatant>();
            _allCombatants.AddRange(_playerCombatants);
            _allCombatants.AddRange(_enemyCombatants);

            _actionQueue = new List<QueuedAction>();
            RoundNumber = 1;
            _currentPhase = BattlePhase.StartOfTurn;
            _endOfTurnEffectsProcessed = false;

            EventBus.Subscribe<GameEvents.SecondaryEffectComplete>(OnSecondaryEffectComplete);
            EventBus.Subscribe<GameEvents.MoveAnimationCompleted>(OnMoveAnimationCompleted);

            HandleOnEnterAbilities();
        }

        /// <summary>
        /// Emergency method called by the Watchdog to force the battle state forward if stuck.
        /// </summary>
        public void ForceAdvance()
        {
            // Clear any pending internal state that might be blocking progress
            _actionToExecute = null;
            _actionPendingAnimation = null;
            _currentMultiHitAction = null;

            // If we were stuck animating or resolving, move to checking defeat to clean up the turn
            if (_currentPhase == BattlePhase.AnimatingMove ||
                _currentPhase == BattlePhase.ActionResolution ||
                _currentPhase == BattlePhase.SecondaryEffectResolution)
            {
                _currentPhase = BattlePhase.CheckForDefeat;
            }
            // If we were stuck in check for defeat or end of turn, force a new round
            else if (_currentPhase == BattlePhase.CheckForDefeat || _currentPhase == BattlePhase.EndOfTurn)
            {
                RoundNumber++;
                _currentPhase = BattlePhase.StartOfTurn;
            }

            CanAdvance = true;
        }

        private void OnSecondaryEffectComplete(GameEvents.SecondaryEffectComplete e)
        {
            if (_currentPhase == BattlePhase.SecondaryEffectResolution)
            {
                _currentPhase = BattlePhase.CheckForDefeat;
            }
        }

        private void OnMoveAnimationCompleted(GameEvents.MoveAnimationCompleted e)
        {
            // Only process this event if we are actually waiting for an animation.
            // This prevents double-firing from the "Skip Animation" logic in BattleScene.
            if (_currentPhase == BattlePhase.AnimatingMove && _actionPendingAnimation != null)
            {
                ProcessMoveAction(_actionPendingAnimation);
                _actionPendingAnimation = null;
            }
        }

        public void SetPlayerAction(QueuedAction action)
        {
            if (_currentPhase != BattlePhase.ActionSelection || _playerActionSubmitted) return;

            if (HandlePreActionEffects(action))
            {
                _playerActionSubmitted = true;
                return;
            }

            _actionQueue.Add(action);
            _playerActionSubmitted = true;
        }

        public void Update()
        {
            if (_currentPhase == BattlePhase.BattleOver) return;
            if (!CanAdvance) return;

            switch (_currentPhase)
            {
                case BattlePhase.StartOfTurn: HandleStartOfTurn(); break;
                case BattlePhase.ActionSelection: HandleActionSelection(); break;
                case BattlePhase.ActionResolution: HandleActionResolution(); break;
                case BattlePhase.AnimatingMove: break; // Waiting for event
                case BattlePhase.SecondaryEffectResolution: HandleSecondaryEffectResolution(); break;
                case BattlePhase.CheckForDefeat: HandleCheckForDefeat(); break;
                case BattlePhase.EndOfTurn: HandleEndOfTurn(); break;
            }
        }

        private void HandleStartOfTurn()
        {
            IsPlayerTurnSkipped = false;
            _endOfTurnEffectsProcessed = false;
            var startOfTurnActions = new List<QueuedAction>();

            foreach (var combatant in _allCombatants)
            {
                if (combatant.IsDefeated) continue;

                if (combatant.ChargingAction != null)
                {
                    combatant.ChargingAction.TurnsRemaining--;
                    if (combatant.ChargingAction.TurnsRemaining <= 0)
                    {
                        startOfTurnActions.Add(combatant.ChargingAction.Action);
                        combatant.ChargingAction = null;
                        if (combatant.IsPlayerControlled) IsPlayerTurnSkipped = true;
                    }
                    else
                    {
                        if (combatant.IsPlayerControlled) IsPlayerTurnSkipped = true;
                    }
                }

                if (combatant.DelayedActions.Any())
                {
                    var readyActions = new List<DelayedAction>();
                    foreach (var delayed in combatant.DelayedActions)
                    {
                        delayed.TurnsRemaining--;
                        if (delayed.TurnsRemaining <= 0) readyActions.Add(delayed);
                    }
                    foreach (var ready in readyActions) startOfTurnActions.Add(ready.Action);

                    if (readyActions.Any())
                    {
                        var remaining = combatant.DelayedActions.Where(d => !readyActions.Contains(d)).ToList();
                        combatant.DelayedActions = new Queue<DelayedAction>(remaining);
                    }
                }
            }

            _actionQueue.InsertRange(0, startOfTurnActions);
            _currentPhase = BattlePhase.ActionSelection;
            _playerActionSubmitted = IsPlayerTurnSkipped;
        }

        private void HandleActionSelection()
        {
            if (!_playerActionSubmitted) return;

            foreach (var combatant in _allCombatants)
            {
                if (combatant.ChargingAction != null)
                {
                    var chargingAction = new QueuedAction
                    {
                        Actor = combatant,
                        ChosenMove = combatant.ChargingAction.Action.ChosenMove,
                        ActorAgility = combatant.GetEffectiveAgility(),
                        Priority = 100,
                        Type = QueuedActionType.Charging
                    };
                    _actionQueue.Add(chargingAction);
                }
            }

            var activePlayers = _playerCombatants.Where(c => !c.IsDefeated).ToList();
            var activeEnemies = _enemyCombatants.Where(c => !c.IsDefeated).ToList();
            foreach (var enemy in activeEnemies)
            {
                if (enemy.ChargingAction != null) continue;

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

            var lastAction = _actionQueue.LastOrDefault(a => a.Type == QueuedActionType.Move || a.Type == QueuedActionType.Item);
            if (lastAction != null) lastAction.IsLastActionInRound = true;

            _currentPhase = BattlePhase.ActionResolution;
        }

        private void HandleActionResolution()
        {
            if (_currentMultiHitAction != null || _actionToExecute != null) return;

            if (!_actionQueue.Any())
            {
                _currentPhase = BattlePhase.EndOfTurn;
                return;
            }

            var nextAction = _actionQueue[0];
            _actionQueue.RemoveAt(0);

            if (nextAction.Type == QueuedActionType.Charging)
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = nextAction.Actor, Reason = $"charging {nextAction.ChosenMove.MoveName}" });
                CanAdvance = false;
                return;
            }

            if (nextAction.Actor.IsDefeated) return;

            if (nextAction.Actor.HasStatusEffect(StatusEffectType.Stun))
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = nextAction.Actor, Reason = "stunned" });
                nextAction.Actor.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Stun);
                CanAdvance = false;
                return;
            }

            _actionToExecute = nextAction;
            EventBus.Publish(new GameEvents.ActionDeclared { Actor = _actionToExecute.Actor, Move = _actionToExecute.ChosenMove, Item = _actionToExecute.ChosenItem });
            CanAdvance = false;
        }

        public void ExecuteDeclaredAction()
        {
            if (_actionToExecute == null) return;

            var action = _actionToExecute;
            _actionToExecute = null;

            if (!ProcessPreResolutionEffects(action))
            {
                CanAdvance = false;
                return;
            }

            if (action.ChosenItem != null)
            {
                ProcessItemAction(action);
                _currentPhase = BattlePhase.SecondaryEffectResolution;
                CanAdvance = false;
            }
            else if (action.ChosenMove != null)
            {
                // Check if animation exists. If not, skip straight to processing.
                if (!string.IsNullOrEmpty(action.ChosenMove.AnimationSpriteSheet))
                {
                    _actionPendingAnimation = action;
                    _currentPhase = BattlePhase.AnimatingMove;
                    var targets = ResolveTargets(action);

                    MoveData animMove = action.ChosenMove;

                    if (!action.Actor.IsPlayerControlled && action.ChosenMove.Target != TargetType.Self)
                    {
                        animMove = action.ChosenMove.Clone();
                        animMove.IsAnimationCentralized = true;
                    }

                    EventBus.Publish(new GameEvents.MoveAnimationTriggered { Move = animMove, Targets = targets });
                }
                else
                {
                    ProcessMoveAction(action);
                }
            }
        }

        private void HandleSecondaryEffectResolution()
        {
            SecondaryEffectSystem.ProcessSecondaryEffects(_currentActionForEffects, _currentActionFinalTargets, _currentActionDamageResults);
            _currentActionForEffects = null;
            _currentActionDamageResults = null;
            _currentActionFinalTargets = null;
        }

        private void ProcessMoveAction(QueuedAction action)
        {
            if (action.Actor.Stats.CurrentMana < action.ChosenMove.ManaCost)
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "not enough mana" });
                CanAdvance = false;
                _currentPhase = BattlePhase.CheckForDefeat;
                return;
            }

            float manaBefore = action.Actor.Stats.CurrentMana;
            action.Actor.Stats.CurrentMana -= action.ChosenMove.ManaCost;
            float manaAfter = action.Actor.Stats.CurrentMana;

            if (manaBefore != manaAfter)
            {
                EventBus.Publish(new GameEvents.CombatantManaConsumed
                {
                    Actor = action.Actor,
                    ManaBefore = manaBefore,
                    ManaAfter = manaAfter
                });
            }

            if (action.Actor.IsPlayerControlled && action.SpellbookEntry != null)
            {
                action.SpellbookEntry.TimesUsed++;
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
                _currentMultiHitAction = action;
                _multiHitCountRemaining = 1;
                _multiHitAggregatedDamageResults = new List<DamageCalculator.DamageResult>();
                _multiHitAggregatedFinalTargets = new List<BattleCombatant>();
            }
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
                        SecondaryEffectSystem.ProcessPrimaryEffects(action, target);
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

                CanAdvance = false;
            }

            if (_multiHitCountRemaining <= 0)
            {
                var actor = _currentMultiHitAction.Actor;
                var move = _currentMultiHitAction.ChosenMove;

                if (!actor.HasUsedFirstAttack)
                {
                    actor.HasUsedFirstAttack = true;
                }

                if (move.MoveType == MoveType.Action)
                {
                    foreach (var relic in actor.ActiveRelics)
                    {
                        if (relic.Effects.ContainsKey("Spellweaver"))
                        {
                            actor.IsSpellweaverActive = true;
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = actor, Ability = relic, NarrationText = $"{actor.Name}'s {relic.AbilityName} is now active!" });
                        }
                    }
                }
                else if (move.MoveType == MoveType.Spell)
                {
                    if (actor.IsSpellweaverActive)
                    {
                        actor.IsSpellweaverActive = false;
                    }
                }

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
            if (!gameState.PlayerState.RemoveConsumable(action.ChosenItem.ItemID))
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
            var finishedDying = _allCombatants.Where(c => c.IsDying).ToList();
            foreach (var combatant in finishedDying)
            {
                combatant.IsDying = false;
                combatant.IsRemovalProcessed = true;
            }

            var newlyDefeated = _allCombatants.FirstOrDefault(c => c.IsDefeated && !c.IsDying && !c.IsRemovalProcessed);
            if (newlyDefeated != null)
            {
                newlyDefeated.IsDying = true;
                EventBus.Publish(new GameEvents.CombatantDefeated { DefeatedCombatant = newlyDefeated });
                CanAdvance = false;
                return;
            }

            if (_playerCombatants.All(c => c.IsDefeated))
            {
                _currentPhase = BattlePhase.BattleOver;
                return;
            }

            if (_enemyCombatants.All(c => c.IsDefeated))
            {
                _currentPhase = BattlePhase.BattleOver;
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

                foreach (var relic in combatant.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("RegenEndOfTurn", out var regenValue) && EffectParser.TryParseFloat(regenValue, out float healPercent))
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
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = combatant, Ability = relic });
                        }
                    }

                    if (relic.Effects.TryGetValue("AuraApplyStatusEndOfTurn", out var auraValue))
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
                                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = combatant, Ability = relic, NarrationText = $"{combatant.Name}'s {relic.AbilityName} afflicted {target.Name}!" });
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

                foreach (var relic in combatant.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("Escalation", out var escalationValue) && EffectParser.TryParseIntArray(escalationValue, out int[] p) && p.Length == 2)
                    {
                        int maxStacks = p[1];
                        if (combatant.EscalationStacks < maxStacks)
                        {
                            combatant.EscalationStacks++;
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = combatant, Ability = relic });
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
                foreach (var relic in combatant.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("IntimidateOnEnter", out var value) && EffectParser.TryParseStatStageAbilityParams(value, out var stat, out var amount))
                    {
                        var opponents = combatant.IsPlayerControlled ? _enemyCombatants : _playerCombatants;
                        bool anyAffected = false;
                        foreach (var opponent in opponents)
                        {
                            if (!opponent.IsDefeated)
                            {
                                var (success, _) = opponent.ModifyStatStage(stat, amount);
                                if (success)
                                {
                                    anyAffected = true;
                                    EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = opponent, Stat = stat, Amount = amount });
                                }
                            }
                        }
                        if (anyAffected)
                        {
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = combatant, Ability = relic, NarrationText = $"{combatant.Name}'s Intimidate lowered the opponents' {stat}!" });
                        }
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
                foreach (var relic in actor.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("AmbushPredator", out var value) && EffectParser.TryParseIntArray(value, out int[] p) && p.Length == 2)
                    {
                        priority += p[0];

                        moveInstance = move.Clone();
                        float powerModifier = 1.0f + (p[1] / 100f);
                        moveInstance.Power = (int)(moveInstance.Power * powerModifier);

                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = actor, Ability = relic });
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

            return false;
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

            foreach (var relic in action.Actor.ActiveRelics)
            {
                if (relic.Effects.TryGetValue("Bloodletter", out var bloodletterValue) && EffectParser.TryParseFloatArray(bloodletterValue, out float[] p) && p.Length == 2)
                {
                    if (move.MoveType == MoveType.Spell && move.OffensiveElementIDs.Contains(9))
                    {
                        int cost = (int)(action.Actor.Stats.MaxHP * (p[0] / 100f));
                        action.Actor.ApplyDamage(cost);
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = action.Actor, Ability = relic, NarrationText = $"{action.Actor.Name} pays the price for {relic.AbilityName}!" });
                    }
                }
            }

            if (move.Effects.TryGetValue("Gamble", out var gambleValue) && EffectParser.TryParseFloatArray(gambleValue, out float[] gambleParams) && gambleParams.Length >= 1)
            {
                float chance = gambleParams[0];
                if (_random.Next(1, 101) > chance)
                {
                    EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "bad luck" });
                    return false;
                }
            }

            return true;
        }

        private MoveData HandlePreDamageEffects(MoveData originalMove, BattleCombatant target)
        {
            var moveInstance = originalMove;

            if (moveInstance.Effects.TryGetValue("DetonateStatus", out var detonateValue))
            {
                var parts = detonateValue.Split(',');
                if (parts.Length == 2 &&
                    Enum.TryParse<StatusEffectType>(parts[0].Trim(), true, out var statusTypeToDetonate) &&
                    EffectParser.TryParseFloat(parts[1].Trim(), out float multiplier))
                {
                    if (target.HasStatusEffect(statusTypeToDetonate))
                    {
                        moveInstance = originalMove.Clone();
                        moveInstance.Power = (int)(moveInstance.Power * multiplier);
                        target.ActiveStatusEffects.RemoveAll(e => e.EffectType == statusTypeToDetonate);
                        EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = target, EffectType = statusTypeToDetonate });
                    }
                }
            }

            return moveInstance;
        }
    }
}

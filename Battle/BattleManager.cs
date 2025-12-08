using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle
{
    public class BattleManager
    {
        public enum BattlePhase
        {
            StartOfTurn,
            ActionSelection_Slot1, // Select action for first active teammate
            ActionSelection_Slot2, // Select action for second active teammate
            ActionResolution,
            AnimatingMove,
            SecondaryEffectResolution,
            CheckForDefeat,
            EndOfTurn,
            Reinforcement, // NEW: Phase for bringing in benched enemies
            BattleOver
        }

        private readonly List<BattleCombatant> _playerParty;
        private readonly List<BattleCombatant> _enemyParty;
        private readonly List<BattleCombatant> _allCombatants;

        private List<QueuedAction> _actionQueue;
        private QueuedAction? _currentActionForEffects;
        private List<DamageCalculator.DamageResult> _currentActionDamageResults;
        private List<BattleCombatant> _currentActionFinalTargets;
        private BattlePhase _currentPhase;
        public int RoundNumber { get; private set; }
        private static readonly Random _random = new Random();

        // Multi-Hit State
        private QueuedAction? _currentMultiHitAction;
        private int _multiHitCountRemaining;
        private int _totalHitsForNarration;
        private int _actualHitsDelivered;
        private bool _multiHitIsCritical;
        private List<DamageCalculator.DamageResult> _multiHitAggregatedDamageResults;
        private List<BattleCombatant> _multiHitAggregatedFinalTargets;

        private QueuedAction? _actionToExecute;
        private QueuedAction? _actionPendingAnimation;
        private bool _endOfTurnEffectsProcessed;

        // VGC State
        private QueuedAction? _pendingSlot1Action; // Holds the action for slot 1 while selecting slot 2
        public BattleCombatant? CurrentActingCombatant { get; private set; }

        // Reinforcement State
        private int _reinforcementSlotIndex = 0;
        private bool _reinforcementAnnounced = false;

        public BattlePhase CurrentPhase => _currentPhase;
        public IEnumerable<BattleCombatant> AllCombatants => _allCombatants;
        public bool CanAdvance { get; set; } = true;
        public bool IsProcessingMultiHit => _currentMultiHitAction != null;

        public BattleManager(List<BattleCombatant> playerParty, List<BattleCombatant> enemyParty)
        {
            _playerParty = playerParty;
            _enemyParty = enemyParty;
            _allCombatants = new List<BattleCombatant>();
            _allCombatants.AddRange(_playerParty);
            _allCombatants.AddRange(_enemyParty);

            // Initialize Slots
            InitializeSlots(_playerParty);
            InitializeSlots(_enemyParty);

            _actionQueue = new List<QueuedAction>();
            RoundNumber = 1;
            _currentPhase = BattlePhase.StartOfTurn;
            _endOfTurnEffectsProcessed = false;

            EventBus.Subscribe<GameEvents.SecondaryEffectComplete>(OnSecondaryEffectComplete);
            EventBus.Subscribe<GameEvents.MoveAnimationCompleted>(OnMoveAnimationCompleted);

            HandleOnEnterAbilities();
        }

        private void InitializeSlots(List<BattleCombatant> party)
        {
            for (int i = 0; i < party.Count; i++)
            {
                party[i].BattleSlot = i; // 0, 1 are active. 2, 3 are bench.
            }
        }

        public void ForceAdvance()
        {
            _actionToExecute = null;
            _actionPendingAnimation = null;
            _currentMultiHitAction = null;

            if (_currentPhase == BattlePhase.AnimatingMove ||
                _currentPhase == BattlePhase.ActionResolution ||
                _currentPhase == BattlePhase.SecondaryEffectResolution)
            {
                _currentPhase = BattlePhase.CheckForDefeat;
            }
            else if (_currentPhase == BattlePhase.CheckForDefeat || _currentPhase == BattlePhase.EndOfTurn)
            {
                RoundNumber++;
                _currentPhase = BattlePhase.StartOfTurn;
            }
            else if (_currentPhase == BattlePhase.Reinforcement)
            {
                // If stuck in reinforcement, skip to start of turn
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
            if (_currentPhase == BattlePhase.AnimatingMove && _actionPendingAnimation != null)
            {
                ProcessMoveAction(_actionPendingAnimation);
                _actionPendingAnimation = null;
            }
        }

        // --- ACTION SELECTION LOGIC ---

        public void SubmitAction(QueuedAction action)
        {
            if (_currentPhase == BattlePhase.ActionSelection_Slot1)
            {
                _pendingSlot1Action = action;

                // Check if there is a second active player combatant
                var slot2 = _playerParty.FirstOrDefault(c => c.BattleSlot == 1 && !c.IsDefeated);
                if (slot2 != null && slot2.ChargingAction == null)
                {
                    _currentPhase = BattlePhase.ActionSelection_Slot2;
                    CurrentActingCombatant = slot2;
                }
                else
                {
                    // Only 1 active, add the action and proceed
                    AddActionToQueue(action);
                    FinalizeTurnSelection();
                }
            }
            else if (_currentPhase == BattlePhase.ActionSelection_Slot2)
            {
                // Add both actions to queue
                if (_pendingSlot1Action != null) AddActionToQueue(_pendingSlot1Action);
                AddActionToQueue(action);
                FinalizeTurnSelection();
            }
        }

        public void CancelSlot2Selection()
        {
            if (_currentPhase == BattlePhase.ActionSelection_Slot2)
            {
                _pendingSlot1Action = null;
                // Find Slot 0 player
                var slot1 = _playerParty.FirstOrDefault(c => c.BattleSlot == 0 && !c.IsDefeated);
                if (slot1 != null)
                {
                    CurrentActingCombatant = slot1;
                    _currentPhase = BattlePhase.ActionSelection_Slot1;
                }
            }
        }

        private void AddActionToQueue(QueuedAction action)
        {
            if (HandlePreActionEffects(action)) return; // If charging/delayed, don't add to immediate queue
            _actionQueue.Add(action);
        }

        private void FinalizeTurnSelection()
        {
            _pendingSlot1Action = null;
            CurrentActingCombatant = null;

            // Generate Enemy Actions
            var activeEnemies = _enemyParty.Where(c => c.IsActiveOnField && !c.IsDefeated).ToList();
            var activePlayers = _playerParty.Where(c => c.IsActiveOnField && !c.IsDefeated).ToList();

            foreach (var enemy in activeEnemies)
            {
                if (enemy.ChargingAction != null) continue;

                // Simple AI: Target random active player
                var target = activePlayers.Any() ? activePlayers[_random.Next(activePlayers.Count)] : null;

                if (target != null)
                {
                    var possibleMoves = enemy.AvailableMoves;
                    if (enemy.HasStatusEffect(StatusEffectType.Silence))
                        possibleMoves = possibleMoves.Where(m => m.MoveType != MoveType.Spell).ToList();

                    MoveData move = possibleMoves.Any() ? possibleMoves.First() : BattleDataCache.Moves["Stall"];
                    var action = CreateActionFromMove(enemy, move, target);

                    if (!HandlePreActionEffects(action))
                    {
                        _actionQueue.Add(action);
                    }
                }
            }

            // Sort Queue
            _actionQueue = _actionQueue.OrderByDescending(a => a.Priority).ThenByDescending(a => a.ActorAgility).ToList();
            var lastAction = _actionQueue.LastOrDefault(a => a.Type == QueuedActionType.Move || a.Type == QueuedActionType.Item);
            if (lastAction != null) lastAction.IsLastActionInRound = true;

            _currentPhase = BattlePhase.ActionResolution;
        }

        public List<BattleCombatant> GetReservedBenchMembers()
        {
            var reserved = new List<BattleCombatant>();
            if (_pendingSlot1Action != null && _pendingSlot1Action.Type == QueuedActionType.Switch && _pendingSlot1Action.Target != null)
            {
                reserved.Add(_pendingSlot1Action.Target);
            }
            return reserved;
        }

        public void Update()
        {
            if (_currentPhase == BattlePhase.BattleOver) return;
            if (!CanAdvance) return;

            if (_currentMultiHitAction != null)
            {
                ProcessHit();
                return;
            }

            switch (_currentPhase)
            {
                case BattlePhase.StartOfTurn: HandleStartOfTurn(); break;
                case BattlePhase.ActionSelection_Slot1: break; // Waiting for UI
                case BattlePhase.ActionSelection_Slot2: break; // Waiting for UI
                case BattlePhase.ActionResolution: HandleActionResolution(); break;
                case BattlePhase.AnimatingMove: break;
                case BattlePhase.SecondaryEffectResolution: HandleSecondaryEffectResolution(); break;
                case BattlePhase.CheckForDefeat: HandleCheckForDefeat(); break;
                case BattlePhase.EndOfTurn: HandleEndOfTurn(); break;
                case BattlePhase.Reinforcement: HandleReinforcements(); break;
            }
        }

        private void HandleStartOfTurn()
        {
            _endOfTurnEffectsProcessed = false;
            var startOfTurnActions = new List<QueuedAction>();

            foreach (var combatant in _allCombatants)
            {
                if (combatant.IsDefeated || !combatant.IsActiveOnField) continue;

                if (combatant.ChargingAction != null)
                {
                    combatant.ChargingAction.TurnsRemaining--;
                    if (combatant.ChargingAction.TurnsRemaining <= 0)
                    {
                        startOfTurnActions.Add(combatant.ChargingAction.Action);
                        combatant.ChargingAction = null;
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

                    // Cleanup
                    var remaining = combatant.DelayedActions.Where(d => !readyActions.Contains(d)).ToList();
                    combatant.DelayedActions = new Queue<DelayedAction>(remaining);
                }
            }

            _actionQueue.InsertRange(0, startOfTurnActions);

            // Determine if Slot 1 needs input
            var slot1 = _playerParty.FirstOrDefault(c => c.BattleSlot == 0 && !c.IsDefeated);
            if (slot1 != null && slot1.ChargingAction == null)
            {
                _currentPhase = BattlePhase.ActionSelection_Slot1;
                CurrentActingCombatant = slot1;
            }
            else
            {
                // If Slot 1 is dead or charging, check Slot 2
                var slot2 = _playerParty.FirstOrDefault(c => c.BattleSlot == 1 && !c.IsDefeated);
                if (slot2 != null && slot2.ChargingAction == null)
                {
                    _currentPhase = BattlePhase.ActionSelection_Slot2;
                    CurrentActingCombatant = slot2;
                }
                else
                {
                    // No input needed, go straight to resolution
                    FinalizeTurnSelection();
                }
            }
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

            // Skip if actor died or switched out before acting
            if (nextAction.Actor.IsDefeated || !nextAction.Actor.IsActiveOnField) return;

            if (nextAction.Type == QueuedActionType.Charging)
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = nextAction.Actor, Reason = $"charging [cAction]{nextAction.ChosenMove.MoveName}[/]" });
                CanAdvance = false;
                return;
            }

            if (nextAction.Actor.HasStatusEffect(StatusEffectType.Stun))
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = nextAction.Actor, Reason = "stunned" });
                nextAction.Actor.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Stun);
                CanAdvance = false;
                return;
            }

            _actionToExecute = nextAction;
            EventBus.Publish(new GameEvents.ActionDeclared
            {
                Actor = _actionToExecute.Actor,
                Move = _actionToExecute.ChosenMove,
                Item = _actionToExecute.ChosenItem,
                Target = _actionToExecute.Target,
                Type = _actionToExecute.Type
            });
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

            if (action.Type == QueuedActionType.Switch)
            {
                ProcessSwitchAction(action);
                _currentPhase = BattlePhase.CheckForDefeat; // Check if switch triggered anything
                CanAdvance = false;
            }
            else if (action.ChosenItem != null)
            {
                ProcessItemAction(action);
                _currentPhase = BattlePhase.SecondaryEffectResolution;
                CanAdvance = false;
            }
            else if (action.ChosenMove != null)
            {
                ProcessMoveAction(action);
            }
        }

        private void ProcessSwitchAction(QueuedAction action)
        {
            var actor = action.Actor;
            var target = action.Target; // The benched unit

            if (target == null) return;

            int oldSlot = actor.BattleSlot;
            int newSlot = target.BattleSlot;

            actor.BattleSlot = newSlot;
            target.BattleSlot = oldSlot;

            // Trigger OnEnter abilities for the new unit
            HandleOnEnterAbilities(target);
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
                EventBus.Publish(new GameEvents.CombatantManaConsumed { Actor = action.Actor, ManaBefore = manaBefore, ManaAfter = manaAfter });
            }

            if (action.Actor.IsPlayerControlled && action.SpellbookEntry != null)
            {
                action.SpellbookEntry.TimesUsed++;
            }

            // Crit Calc
            _multiHitIsCritical = false;
            double critChance = BattleConstants.CRITICAL_HIT_CHANCE;
            foreach (var relic in action.Actor.ActiveRelics)
            {
                if (relic.Effects.TryGetValue("CritChanceBonus", out var value) && EffectParser.TryParseFloat(value, out float bonus))
                {
                    critChance += bonus / 100.0;
                }
            }
            if (_random.NextDouble() < critChance) _multiHitIsCritical = true;

            _actualHitsDelivered = 0;

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
                _totalHitsForNarration = 1;
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
                    _actualHitsDelivered++;
                    var damageResultsForThisHit = new List<DamageCalculator.DamageResult>();
                    float multiTargetModifier = (targetsForThisHit.Count > 1) ? BattleConstants.MULTI_TARGET_MODIFIER : 1.0f;

                    foreach (var target in targetsForThisHit)
                    {
                        var moveInstance = HandlePreDamageEffects(action.ChosenMove, target);
                        var result = DamageCalculator.CalculateDamage(action, target, moveInstance, multiTargetModifier, _multiHitIsCritical);
                        target.ApplyDamage(result.DamageAmount);
                        SecondaryEffectSystem.ProcessPrimaryEffects(action, target);
                        damageResultsForThisHit.Add(result);
                        _multiHitAggregatedFinalTargets.Add(target);
                    }
                    _multiHitAggregatedDamageResults.AddRange(damageResultsForThisHit);

                    if (!string.IsNullOrEmpty(action.ChosenMove.AnimationSpriteSheet))
                    {
                        MoveData animMove = action.ChosenMove;
                        EventBus.Publish(new GameEvents.MoveAnimationTriggered { Move = animMove, Targets = targetsForThisHit });
                    }

                    EventBus.Publish(new GameEvents.BattleActionExecuted
                    {
                        Actor = action.Actor,
                        ChosenMove = action.ChosenMove,
                        Targets = targetsForThisHit,
                        DamageResults = damageResultsForThisHit
                    });

                    if (targetsForThisHit.All(t => t.IsDefeated)) _multiHitCountRemaining = 0;
                }
                else
                {
                    _multiHitCountRemaining = 0;
                }
                CanAdvance = false;
            }

            if (_multiHitCountRemaining <= 0)
            {
                var actor = _currentMultiHitAction.Actor;
                var move = _currentMultiHitAction.ChosenMove;

                if (!actor.HasUsedFirstAttack) actor.HasUsedFirstAttack = true;

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
                    if (actor.IsSpellweaverActive) actor.IsSpellweaverActive = false;
                }

                if (actor.IsMomentumActive && move.Power > 0) actor.IsMomentumActive = false;

                if (_totalHitsForNarration > 1)
                {
                    int criticalHitCount = _multiHitAggregatedDamageResults.Count(r => r.WasCritical);
                    EventBus.Publish(new GameEvents.MultiHitActionCompleted
                    {
                        Actor = _currentMultiHitAction.Actor,
                        ChosenMove = _currentMultiHitAction.ChosenMove,
                        HitCount = _actualHitsDelivered,
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
            if (!gameState.PlayerState.RemoveConsumable(action.ChosenItem.ItemID)) return;

            var targets = ResolveTargets(action);
            var damageResults = new List<DamageCalculator.DamageResult>();

            switch (action.ChosenItem.Type)
            {
                case ConsumableType.Heal:
                    foreach (var target in targets)
                    {
                        int hpBefore = (int)target.VisualHP;
                        target.ApplyHealing(action.ChosenItem.PrimaryValue);
                        EventBus.Publish(new GameEvents.CombatantHealed { Actor = action.Actor, Target = target, HealAmount = action.ChosenItem.PrimaryValue, VisualHPBefore = hpBefore });
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

            // Identify Teams
            var activeEnemies = _enemyParty.Where(c => c.IsActiveOnField && !c.IsDefeated).ToList();
            var activePlayers = _playerParty.Where(c => c.IsActiveOnField && !c.IsDefeated).ToList();
            var allActive = _allCombatants.Where(c => c.IsActiveOnField && !c.IsDefeated).ToList();

            // Determine "Opponents" and "Allies" relative to the actor
            var opponents = actor.IsPlayerControlled ? activeEnemies : activePlayers;
            var allies = actor.IsPlayerControlled ? activePlayers : activeEnemies;
            var ally = allies.FirstOrDefault(c => c != actor); // The OTHER active team member

            // --- RANDOM TARGET LOGIC (For Multi-Hit or Random Moves) ---
            if (isMultiHit || targetType == TargetType.RandomBoth || targetType == TargetType.RandomEvery || targetType == TargetType.RandomAll)
            {
                List<BattleCombatant> pool = new List<BattleCombatant>();

                if (targetType == TargetType.RandomBoth || (isMultiHit && targetType == TargetType.Both))
                {
                    pool.AddRange(opponents);
                }
                else if (targetType == TargetType.RandomEvery || (isMultiHit && targetType == TargetType.Every))
                {
                    pool.AddRange(opponents);
                    if (ally != null) pool.Add(ally);
                }
                else if (targetType == TargetType.RandomAll || (isMultiHit && targetType == TargetType.All))
                {
                    pool.AddRange(allActive);
                }
                // Fallback for standard multi-hit on single target moves
                else if (isMultiHit && (targetType == TargetType.Single || targetType == TargetType.SingleTeam || targetType == TargetType.SingleAll))
                {
                    if (specifiedTarget != null && specifiedTarget.IsActiveOnField && !specifiedTarget.IsDefeated)
                        return new List<BattleCombatant> { specifiedTarget };
                    // If target dead, pick random opponent
                    if (opponents.Any()) return new List<BattleCombatant> { opponents[_random.Next(opponents.Count)] };
                    return new List<BattleCombatant>();
                }

                if (pool.Any())
                {
                    return new List<BattleCombatant> { pool[_random.Next(pool.Count)] };
                }
                return new List<BattleCombatant>();
            }

            // --- STANDARD TARGET LOGIC ---
            switch (targetType)
            {
                case TargetType.Single:
                    // Target ANYONE except SELF.
                    // If specified target is valid, use it.
                    if (specifiedTarget != null && specifiedTarget.IsActiveOnField && !specifiedTarget.IsDefeated && specifiedTarget != actor)
                        return new List<BattleCombatant> { specifiedTarget };

                    // Fallback: Default to first opponent
                    if (opponents.Any()) return new List<BattleCombatant> { opponents[0] };
                    return new List<BattleCombatant>();

                case TargetType.SingleAll:
                    // Target ANYONE including SELF.
                    if (specifiedTarget != null && specifiedTarget.IsActiveOnField && !specifiedTarget.IsDefeated)
                        return new List<BattleCombatant> { specifiedTarget };
                    // Fallback: Self
                    return new List<BattleCombatant> { actor };

                case TargetType.Both:
                    // Both Enemies
                    return opponents;

                case TargetType.Every:
                    // Both Enemies + Ally
                    var everyTargets = new List<BattleCombatant>(opponents);
                    if (ally != null) everyTargets.Add(ally);
                    return everyTargets;

                case TargetType.All:
                    // Everyone
                    return allActive;

                case TargetType.Self:
                    return new List<BattleCombatant> { actor };

                case TargetType.Team:
                    // Self + Ally
                    var teamTargets = new List<BattleCombatant> { actor };
                    if (ally != null) teamTargets.Add(ally);
                    return teamTargets;

                case TargetType.Ally:
                    // Only Ally
                    return ally != null ? new List<BattleCombatant> { ally } : new List<BattleCombatant>();

                case TargetType.SingleTeam:
                    // Specific target (Self or Ally)
                    if (specifiedTarget != null && specifiedTarget.IsActiveOnField && !specifiedTarget.IsDefeated)
                        return new List<BattleCombatant> { specifiedTarget };
                    return new List<BattleCombatant>();

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

                // Mark slot as empty (move to void)
                // We do NOT fill the slot here anymore. That happens in Reinforcement phase.
                combatant.BattleSlot = -1;
            }

            var newlyDefeated = _allCombatants.FirstOrDefault(c => c.IsDefeated && !c.IsDying && !c.IsRemovalProcessed);
            if (newlyDefeated != null)
            {
                newlyDefeated.IsDying = true;
                EventBus.Publish(new GameEvents.CombatantDefeated { DefeatedCombatant = newlyDefeated });
                CanAdvance = false;
                return;
            }

            if (_playerParty.All(c => c.IsDefeated))
            {
                _currentPhase = BattlePhase.BattleOver;
                return;
            }

            if (_enemyParty.All(c => c.IsDefeated))
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
                // Instead of going to StartOfTurn, we go to Reinforcement to fill empty slots
                _currentPhase = BattlePhase.Reinforcement;
                _reinforcementSlotIndex = 0;
                _reinforcementAnnounced = false;
            }
        }

        private void HandleEndOfTurn()
        {
            _endOfTurnEffectsProcessed = true;

            foreach (var combatant in _allCombatants)
            {
                if (combatant.IsDefeated || !combatant.IsActiveOnField) continue;

                // Regen
                foreach (var relic in combatant.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("RegenEndOfTurn", out var regenValue) && EffectParser.TryParseFloat(regenValue, out float healPercent))
                    {
                        int hpBefore = (int)combatant.VisualHP;
                        int healAmount = (int)(combatant.Stats.MaxHP * (healPercent / 100f));
                        if (healAmount > 0)
                        {
                            combatant.ApplyHealing(healAmount);
                            EventBus.Publish(new GameEvents.CombatantHealed { Actor = combatant, Target = combatant, HealAmount = healAmount, VisualHPBefore = hpBefore });
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = combatant, Ability = relic });
                        }
                    }
                }

                // Status Effects
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
                    if (effect.DurationInTurns <= 0) effectsToRemove.Add(effect);
                }
                foreach (var expiredEffect in effectsToRemove)
                {
                    combatant.ActiveStatusEffects.Remove(expiredEffect);
                    EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = combatant, EffectType = expiredEffect.EffectType });
                }
            }

            _currentPhase = BattlePhase.CheckForDefeat;
        }

        private void HandleReinforcements()
        {
            // Check slots 0 and 1 sequentially
            if (_reinforcementSlotIndex > 1)
            {
                RoundNumber++;
                _currentPhase = BattlePhase.StartOfTurn;
                return;
            }

            // Check if the current slot is empty
            bool isSlotOccupied = _enemyParty.Any(c => c.BattleSlot == _reinforcementSlotIndex && !c.IsDefeated);

            if (!isSlotOccupied)
            {
                // Check if there is a benched enemy available
                // Benched enemies have slot >= 2 or -1 if they were just moved to void
                var benchedEnemy = _enemyParty.FirstOrDefault(c => c.BattleSlot >= 2 && !c.IsDefeated);

                if (benchedEnemy != null)
                {
                    if (!_reinforcementAnnounced)
                    {
                        // Step 1: Announce via blocking narration
                        EventBus.Publish(new GameEvents.NextEnemyApproaches());
                        _reinforcementAnnounced = true;
                        // We return here. The BattleScene will see the UI is busy (displaying text)
                        // and pause the BattleManager updates until the user clicks.
                        return;
                    }
                    else
                    {
                        // Step 2: Spawn (Only happens after UI clears and Update resumes)
                        benchedEnemy.BattleSlot = _reinforcementSlotIndex;
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{benchedEnemy.Name} enters the battle!" });
                        EventBus.Publish(new GameEvents.CombatantSpawned { Combatant = benchedEnemy });
                        HandleOnEnterAbilities(benchedEnemy);

                        // Reset for next slot
                        _reinforcementAnnounced = false;
                        _reinforcementSlotIndex++;
                        return;
                    }
                }
            }

            // If slot is occupied OR no benched enemies, move to next slot
            _reinforcementSlotIndex++;
            _reinforcementAnnounced = false;
        }

        private void HandleOnEnterAbilities(BattleCombatant specificCombatant = null)
        {
            var targets = specificCombatant != null ? new List<BattleCombatant> { specificCombatant } : _allCombatants;

            foreach (var combatant in targets)
            {
                if (!combatant.IsActiveOnField) continue;

                foreach (var relic in combatant.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("IntimidateOnEnter", out var value) && EffectParser.TryParseStatStageAbilityParams(value, out var stat, out var amount))
                    {
                        var opponents = combatant.IsPlayerControlled ? _enemyParty : _playerParty;
                        bool anyAffected = false;
                        foreach (var opponent in opponents)
                        {
                            if (!opponent.IsDefeated && opponent.IsActiveOnField)
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
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = combatant, Ability = relic, NarrationText = $"{combatant.Name}'s {relic.AbilityName} lowered the opponents' {stat}!" });
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
                string typeTag = move.MoveType == MoveType.Spell ? "cSpell" : "cAction";
                EventBus.Publish(new GameEvents.CombatantChargingAction { Actor = action.Actor, MoveName = $"[{typeTag}]{move.MoveName}[/]" });
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
                if (parts.Length == 2 && Enum.TryParse<StatusEffectType>(parts[0].Trim(), true, out var statusTypeToDetonate) && EffectParser.TryParseFloat(parts[1].Trim(), out float multiplier))
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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities; // New Namespace
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
            ActionSelection_Slot1,
            ActionSelection_Slot2,
            ActionResolution,
            AnimatingMove,
            SecondaryEffectResolution,
            CheckForDefeat,
            EndOfTurn,
            Reinforcement,
            BattleOver
        }

        private readonly List<BattleCombatant> _playerParty;
        private readonly List<BattleCombatant> _enemyParty;
        private readonly List<BattleCombatant> _allCombatants;

        private readonly List<BattleCombatant> _cachedActivePlayers = new List<BattleCombatant>();
        private readonly List<BattleCombatant> _cachedActiveEnemies = new List<BattleCombatant>();
        private readonly List<BattleCombatant> _cachedAllActive = new List<BattleCombatant>();

        private List<QueuedAction> _actionQueue;
        private QueuedAction? _currentActionForEffects;
        private List<DamageCalculator.DamageResult> _currentActionDamageResults;
        private List<BattleCombatant> _currentActionFinalTargets;
        private BattlePhase _currentPhase;
        public int RoundNumber { get; private set; }
        private static readonly Random _random = new Random();

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

        private QueuedAction? _pendingSlot1Action;
        public BattleCombatant? CurrentActingCombatant { get; private set; }

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

            InitializeSlots(_playerParty);
            InitializeSlots(_enemyParty);
            RefreshCombatantCaches();

            _actionQueue = new List<QueuedAction>();
            RoundNumber = 1;
            _currentPhase = BattlePhase.StartOfTurn;
            _endOfTurnEffectsProcessed = false;

            EventBus.Subscribe<GameEvents.SecondaryEffectComplete>(OnSecondaryEffectComplete);
            EventBus.Subscribe<GameEvents.MoveAnimationCompleted>(OnMoveAnimationCompleted);

            // Trigger Battle Start Effects
            foreach (var combatant in _cachedAllActive)
            {
                foreach (var ability in combatant.BattleLifecycleEffects)
                {
                    ability.OnBattleStart(combatant);
                    ability.OnCombatantEnter(combatant);
                }
            }
        }

        private void InitializeSlots(List<BattleCombatant> party)
        {
            for (int i = 0; i < party.Count; i++)
            {
                party[i].BattleSlot = i;
            }
        }

        private void RefreshCombatantCaches()
        {
            _cachedActivePlayers.Clear();
            _cachedActiveEnemies.Clear();
            _cachedAllActive.Clear();

            foreach (var c in _playerParty)
            {
                if (c.IsActiveOnField && !c.IsDefeated)
                {
                    _cachedActivePlayers.Add(c);
                    _cachedAllActive.Add(c);
                }
            }

            foreach (var c in _enemyParty)
            {
                if (c.IsActiveOnField && !c.IsDefeated)
                {
                    _cachedActiveEnemies.Add(c);
                    _cachedAllActive.Add(c);
                }
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

        public void SubmitAction(QueuedAction action)
        {
            if (_currentPhase == BattlePhase.ActionSelection_Slot1)
            {
                _pendingSlot1Action = action;
                var slot2 = _cachedActivePlayers.FirstOrDefault(c => c.BattleSlot == 1);
                if (slot2 != null && slot2.ChargingAction == null)
                {
                    _currentPhase = BattlePhase.ActionSelection_Slot2;
                    CurrentActingCombatant = slot2;
                }
                else
                {
                    AddActionToQueue(action);
                    FinalizeTurnSelection();
                }
            }
            else if (_currentPhase == BattlePhase.ActionSelection_Slot2)
            {
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
                var slot1 = _cachedActivePlayers.FirstOrDefault(c => c.BattleSlot == 0);
                if (slot1 != null)
                {
                    CurrentActingCombatant = slot1;
                    _currentPhase = BattlePhase.ActionSelection_Slot1;
                }
            }
        }

        private void AddActionToQueue(QueuedAction action)
        {
            if (HandlePreActionEffects(action)) return;
            _actionQueue.Add(action);
        }

        private void FinalizeTurnSelection()
        {
            _pendingSlot1Action = null;
            CurrentActingCombatant = null;

            // --- AI LOGIC INTEGRATION ---
            foreach (var enemy in _cachedActiveEnemies)
            {
                if (enemy.ChargingAction != null) continue;

                // Use the new EnemyAI system to determine the best action
                var action = EnemyAI.DetermineBestAction(enemy, _allCombatants);

                if (!HandlePreActionEffects(action))
                {
                    _actionQueue.Add(action);
                }
            }

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
                case BattlePhase.ActionSelection_Slot1: break;
                case BattlePhase.ActionSelection_Slot2: break;
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

            foreach (var combatant in _cachedAllActive)
            {
                foreach (var ability in combatant.TurnLifecycleEffects)
                {
                    ability.OnTurnStart(combatant);
                }

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
                    var remaining = combatant.DelayedActions.Where(d => !readyActions.Contains(d)).ToList();
                    combatant.DelayedActions = new Queue<DelayedAction>(remaining);
                }
            }

            _actionQueue.InsertRange(0, startOfTurnActions);

            var slot1 = _cachedActivePlayers.FirstOrDefault(c => c.BattleSlot == 0);
            if (slot1 != null && slot1.ChargingAction == null)
            {
                _currentPhase = BattlePhase.ActionSelection_Slot1;
                CurrentActingCombatant = slot1;
            }
            else
            {
                var slot2 = _cachedActivePlayers.FirstOrDefault(c => c.BattleSlot == 1);
                if (slot2 != null && slot2.ChargingAction == null)
                {
                    _currentPhase = BattlePhase.ActionSelection_Slot2;
                    CurrentActingCombatant = slot2;
                }
                else
                {
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
                // Stun is temporary, duration decremented at end of turn
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
                _currentPhase = BattlePhase.CheckForDefeat;
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
            var target = action.Target;

            if (target == null) return;

            int oldSlot = actor.BattleSlot;
            int newSlot = target.BattleSlot;

            actor.BattleSlot = newSlot;
            target.BattleSlot = oldSlot;

            RefreshCombatantCaches();
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
            // Silence Check
            if (action.ChosenMove.MoveType == MoveType.Spell && action.Actor.HasStatusEffect(StatusEffectType.Silence))
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "silenced" });
                CanAdvance = false;
                _currentPhase = BattlePhase.CheckForDefeat;
                return;
            }

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

            _multiHitIsCritical = false;
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
                        var result = DamageCalculator.CalculateDamage(action, target, moveInstance, multiTargetModifier);
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

                    // Trigger OnKill Effects
                    if (targetsForThisHit.All(t => t.IsDefeated))
                    {
                        var ctx = new CombatContext { Actor = action.Actor, Move = action.ChosenMove };
                        foreach (var effect in action.Actor.OnKillEffects)
                        {
                            effect.OnKill(ctx);
                        }
                    }

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

                if (!actor.HasUsedFirstAttack) actor.HasUsedFirstAttack = true;

                // Trigger OnActionComplete Effects
                foreach (var effect in actor.OnActionCompleteEffects)
                {
                    effect.OnActionComplete(_currentMultiHitAction, actor);
                }

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

            var validCandidates = TargetingHelper.GetValidTargets(actor, targetType, _allCombatants);

            if (isMultiHit || targetType == TargetType.RandomBoth || targetType == TargetType.RandomEvery || targetType == TargetType.RandomAll)
            {
                if (isMultiHit && (targetType == TargetType.Single || targetType == TargetType.SingleTeam || targetType == TargetType.SingleAll))
                {
                    if (specifiedTarget != null && specifiedTarget.IsActiveOnField && !specifiedTarget.IsDefeated)
                        return new List<BattleCombatant> { specifiedTarget };

                    if (validCandidates.Any()) return new List<BattleCombatant> { validCandidates[_random.Next(validCandidates.Count)] };
                    return new List<BattleCombatant>();
                }

                if (validCandidates.Any())
                {
                    return new List<BattleCombatant> { validCandidates[_random.Next(validCandidates.Count)] };
                }
                return new List<BattleCombatant>();
            }

            if (specifiedTarget != null && validCandidates.Contains(specifiedTarget))
            {
                return new List<BattleCombatant> { specifiedTarget };
            }

            if (targetType == TargetType.All || targetType == TargetType.Both || targetType == TargetType.Every || targetType == TargetType.Team || targetType == TargetType.Ally)
            {
                return validCandidates;
            }

            if (validCandidates.Any())
            {
                return new List<BattleCombatant> { validCandidates[0] };
            }

            return new List<BattleCombatant>();
        }

        private void HandleCheckForDefeat()
        {
            var finishedDying = _allCombatants.Where(c => c.IsDying).ToList();
            foreach (var combatant in finishedDying)
            {
                combatant.IsDying = false;
                combatant.IsRemovalProcessed = true;
                combatant.BattleSlot = -1;
            }

            if (finishedDying.Any())
            {
                RefreshCombatantCaches();
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
                _currentPhase = BattlePhase.Reinforcement;
                _reinforcementSlotIndex = 0;
                _reinforcementAnnounced = false;
            }
        }

        private void HandleEndOfTurn()
        {
            _endOfTurnEffectsProcessed = true;

            foreach (var combatant in _cachedAllActive)
            {
                foreach (var ability in combatant.TurnLifecycleEffects)
                {
                    ability.OnTurnEnd(combatant);
                }

                var effectsToRemove = new List<StatusEffectInstance>();
                foreach (var effect in combatant.ActiveStatusEffects)
                {
                    // Only decrement duration for temporary effects
                    if (!effect.IsPermanent)
                    {
                        effect.DurationInTurns--;
                    }

                    // Poison Logic: Exponential Damage
                    if (effect.EffectType == StatusEffectType.Poison)
                    {
                        // Damage = Base * 2^Turns
                        int safeTurnCount = Math.Min(effect.PoisonTurnCount, 30);
                        long rawDamage = (long)Global.Instance.PoisonBaseDamage * (long)Math.Pow(2, safeTurnCount);

                        int poisonDamage = (int)Math.Min(rawDamage, int.MaxValue);

                        combatant.ApplyDamage(poisonDamage);
                        EventBus.Publish(new GameEvents.StatusEffectTriggered { Combatant = combatant, EffectType = StatusEffectType.Poison, Damage = poisonDamage });

                        effect.PoisonTurnCount++;
                    }

                    // Regen Logic: % Max HP Heal
                    if (effect.EffectType == StatusEffectType.Regen)
                    {
                        int healAmount = (int)(combatant.Stats.MaxHP * Global.Instance.RegenPercent);
                        if (healAmount > 0)
                        {
                            int hpBefore = (int)combatant.VisualHP;
                            combatant.ApplyHealing(healAmount);
                            EventBus.Publish(new GameEvents.CombatantHealed { Actor = combatant, Target = combatant, HealAmount = healAmount, VisualHPBefore = hpBefore });
                        }
                    }

                    // Remove expired temporary effects
                    if (!effect.IsPermanent && effect.DurationInTurns <= 0)
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

            _currentPhase = BattlePhase.CheckForDefeat;
        }

        private void HandleReinforcements()
        {
            if (_reinforcementSlotIndex > 1)
            {
                RoundNumber++;
                _currentPhase = BattlePhase.StartOfTurn;
                return;
            }

            bool isSlotOccupied = _cachedActiveEnemies.Any(c => c.BattleSlot == _reinforcementSlotIndex);

            if (!isSlotOccupied)
            {
                var benchedEnemy = _enemyParty.FirstOrDefault(c => c.BattleSlot >= 2 && !c.IsDefeated);

                if (benchedEnemy != null)
                {
                    if (!_reinforcementAnnounced)
                    {
                        EventBus.Publish(new GameEvents.NextEnemyApproaches());
                        _reinforcementAnnounced = true;
                        return;
                    }
                    else
                    {
                        benchedEnemy.BattleSlot = _reinforcementSlotIndex;
                        RefreshCombatantCaches();

                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{benchedEnemy.Name} enters the battle!" });
                        EventBus.Publish(new GameEvents.CombatantSpawned { Combatant = benchedEnemy });
                        HandleOnEnterAbilities(benchedEnemy);

                        _reinforcementAnnounced = false;
                        _reinforcementSlotIndex++;
                        return;
                    }
                }
            }

            _reinforcementSlotIndex++;
            _reinforcementAnnounced = false;
        }

        private void HandleOnEnterAbilities(BattleCombatant specificCombatant = null)
        {
            var targets = specificCombatant != null ? new List<BattleCombatant> { specificCombatant } : _cachedAllActive;

            foreach (var combatant in targets)
            {
                if (!combatant.IsActiveOnField) continue;

                foreach (var ability in combatant.BattleLifecycleEffects)
                {
                    ability.OnCombatantEnter(combatant);
                }
            }
        }

        public QueuedAction CreateActionFromMove(BattleCombatant actor, MoveData move, BattleCombatant target)
        {
            var action = new QueuedAction
            {
                Actor = actor,
                Target = target,
                ChosenMove = move.Clone(),
                Priority = move.Priority,
                ActorAgility = actor.GetEffectiveAgility(),
                Type = QueuedActionType.Move
            };

            foreach (var mod in actor.ActionModifiers)
            {
                mod.ModifyAction(action, actor);
            }

            return action;
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

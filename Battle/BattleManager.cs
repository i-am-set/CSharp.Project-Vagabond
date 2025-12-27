using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
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
            BattleOver,
            ProcessingInteraction,
            WaitingForSwitchCompletion
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

        private QueuedAction? _actionToExecute;
        private QueuedAction? _actionPendingAnimation;
        private bool _endOfTurnEffectsProcessed;

        private QueuedAction? _pendingSlot1Action;
        public BattleCombatant? CurrentActingCombatant { get; private set; }

        private int _reinforcementSlotIndex = 0;
        private bool _reinforcementAnnounced = false;

        // Pending Damage State for Delayed Application
        private class PendingImpactData
        {
            public QueuedAction Action;
            public List<BattleCombatant> Targets;
            public List<DamageCalculator.DamageResult> Results;
        }
        private PendingImpactData _pendingImpact;

        // --- INTERRUPT SYSTEM ---
        private BattleInteraction _activeInteraction;

        public BattlePhase CurrentPhase => _currentPhase;
        public IEnumerable<BattleCombatant> AllCombatants => _allCombatants;
        public bool CanAdvance { get; set; } = true;
        public bool IsProcessingMultiHit => false;

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
            EventBus.Subscribe<GameEvents.MoveImpactOccurred>(OnMoveImpactOccurred);
            EventBus.Subscribe<GameEvents.DisengageTriggered>(OnDisengageTriggered);

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
            _pendingImpact = null;
            _activeInteraction = null;

            if (_currentPhase == BattlePhase.AnimatingMove ||
                _currentPhase == BattlePhase.ActionResolution ||
                _currentPhase == BattlePhase.SecondaryEffectResolution ||
                _currentPhase == BattlePhase.ProcessingInteraction ||
                _currentPhase == BattlePhase.WaitingForSwitchCompletion)
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
                if (_pendingImpact != null)
                {
                    ApplyPendingImpact();
                }

                ProcessMoveActionPostImpact(_actionPendingAnimation);
                _actionPendingAnimation = null;
            }
        }

        private void OnMoveImpactOccurred(GameEvents.MoveImpactOccurred e)
        {
            if (_pendingImpact != null)
            {
                ApplyPendingImpact();
            }
        }

        private void OnDisengageTriggered(GameEvents.DisengageTriggered e)
        {
            CanAdvance = false;
            _currentPhase = BattlePhase.ProcessingInteraction;

            _activeInteraction = new SwitchInteraction(e.Actor, (result) =>
            {
                if (result is BattleCombatant target)
                {
                    // Hand off control to the Scene Director
                    InitiateSwitchSequence(e.Actor, target);
                }
                else
                {
                    // Cancelled or failed
                    _currentPhase = BattlePhase.CheckForDefeat;
                    CanAdvance = true;
                }
                _activeInteraction = null;
            });

            _activeInteraction.Start(this);
        }

        public void SubmitInteractionResult(object result)
        {
            if (_activeInteraction != null)
            {
                _activeInteraction.Resolve(result);
            }
        }

        private void InitiateSwitchSequence(BattleCombatant actor, BattleCombatant incomingMember)
        {
            // Enter waiting state
            _currentPhase = BattlePhase.WaitingForSwitchCompletion;
            CanAdvance = false;

            // Signal the Scene Director to take over visuals
            EventBus.Publish(new GameEvents.SwitchSequenceInitiated
            {
                OutgoingCombatant = actor,
                IncomingCombatant = incomingMember
            });
        }

        /// <summary>
        /// Called by the Scene Director (BattleScene) when the "Out" animation is finished.
        /// Instantly swaps the data.
        /// </summary>
        public void PerformLogicalSwitch(BattleCombatant actor, BattleCombatant incomingMember)
        {
            if (actor == null || incomingMember == null) return;

            int oldSlot = actor.BattleSlot;
            int newSlot = incomingMember.BattleSlot;

            actor.BattleSlot = newSlot;
            incomingMember.BattleSlot = oldSlot;

            // Reset "First Turn" flag for the incoming member so moves like Counter work again
            incomingMember.HasUsedFirstAttack = false;

            RefreshCombatantCaches();

            // Retarget any pending actions
            foreach (var action in _actionQueue)
            {
                if (action.Target == actor)
                {
                    action.Target = incomingMember;
                }
            }

            // Trigger OnEnter abilities for the new guy
            HandleOnEnterAbilities(incomingMember);
        }

        /// <summary>
        /// Called by the Scene Director (BattleScene) when the "In" animation is finished.
        /// Resumes the battle flow.
        /// </summary>
        public void ResumeAfterSwitch()
        {
            _currentPhase = BattlePhase.CheckForDefeat;
            CanAdvance = true;
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

            foreach (var enemy in _cachedActiveEnemies)
            {
                if (enemy.ChargingAction != null) continue;
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

        public void Update(float deltaTime)
        {
            if (_currentPhase == BattlePhase.BattleOver) return;
            if (!CanAdvance && _currentPhase != BattlePhase.WaitingForSwitchCompletion) return;

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
                case BattlePhase.ProcessingInteraction: break;
                case BattlePhase.WaitingForSwitchCompletion: break; // Do nothing, wait for Director
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
            if (_actionToExecute != null) return;

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
                CanAdvance = false;
                return;
            }

            // --- DAZED CHECK ---
            if (nextAction.Actor.IsDazed)
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = nextAction.Actor, Reason = "dazed" });
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

            InitiateSwitchSequence(actor, target);
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

            // Reset Disengage flag before processing the move
            action.Actor.PendingDisengage = false;

            PrepareHit(action);
        }

        private void PrepareHit(QueuedAction action)
        {
            var targetsForThisHit = ResolveTargets(action);

            if (targetsForThisHit.Any())
            {
                var damageResultsForThisHit = new List<DamageCalculator.DamageResult>();
                float multiTargetModifier = (targetsForThisHit.Count > 1) ? BattleConstants.MULTI_TARGET_MODIFIER : 1.0f;

                foreach (var target in targetsForThisHit)
                {
                    var moveInstance = HandlePreDamageEffects(action.ChosenMove, target);
                    var result = DamageCalculator.CalculateDamage(action, target, moveInstance, multiTargetModifier);
                    damageResultsForThisHit.Add(result);
                }

                _pendingImpact = new PendingImpactData
                {
                    Action = action,
                    Targets = targetsForThisHit,
                    Results = damageResultsForThisHit
                };

                var normalTargets = new List<BattleCombatant>();
                var protectedTargets = new List<BattleCombatant>();

                foreach (var target in targetsForThisHit)
                {
                    if (target.HasStatusEffect(StatusEffectType.Protected))
                    {
                        protectedTargets.Add(target);
                    }
                    else
                    {
                        normalTargets.Add(target);
                    }
                }

                if (normalTargets.Any() && !string.IsNullOrEmpty(action.ChosenMove.AnimationSpriteSheet))
                {
                    MoveData animMove = action.ChosenMove;
                    EventBus.Publish(new GameEvents.MoveAnimationTriggered { Move = animMove, Targets = normalTargets });
                    _actionPendingAnimation = action;
                    _currentPhase = BattlePhase.AnimatingMove;
                    CanAdvance = false;
                }

                if (protectedTargets.Any())
                {
                    var protectMove = action.ChosenMove.Clone();
                    protectMove.AnimationSpriteSheet = "basic_protect";
                    protectMove.IsAnimationCentralized = false;

                    var global = ServiceLocator.Get<Global>();
                    protectMove.AnimationSpeed = global.ProtectAnimationSpeed;
                    protectMove.DamageFrameIndex = global.ProtectDamageFrameIndex;

                    EventBus.Publish(new GameEvents.MoveAnimationTriggered { Move = protectMove, Targets = protectedTargets });

                    if (_actionPendingAnimation == null)
                    {
                        _actionPendingAnimation = action;
                        _currentPhase = BattlePhase.AnimatingMove;
                        CanAdvance = false;
                    }
                }

                if (_actionPendingAnimation == null)
                {
                    ApplyPendingImpact();
                    ProcessMoveActionPostImpact(action);
                }
            }
            else
            {
                // --- TARGETING FAILED LOGIC ---
                EventBus.Publish(new GameEvents.MoveFailed { Actor = action.Actor });
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{action.Actor.Name}'s attack failed!" });

                _currentPhase = BattlePhase.CheckForDefeat;
                CanAdvance = false;
            }
        }

        private void ApplyPendingImpact()
        {
            if (_pendingImpact == null) return;

            var action = _pendingImpact.Action;
            var targets = _pendingImpact.Targets;
            var results = _pendingImpact.Results;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var result = results[i];

                // --- SHIELD BREAKER LOGIC ---
                var shieldBreaker = action.ChosenMove.Abilities.OfType<IShieldBreaker>().FirstOrDefault();
                bool isProtecting = target.HasStatusEffect(StatusEffectType.Protected);

                if (shieldBreaker != null)
                {
                    if (isProtecting)
                    {
                        // Break it
                        target.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Protected);
                        EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = target, EffectType = StatusEffectType.Protected });
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{action.Actor.Name} shattered the guard!" });

                        // Modify Damage
                        result.DamageAmount = (int)(result.DamageAmount * shieldBreaker.BreakDamageMultiplier);

                        // Update the result in the list so the event gets the right number
                        results[i] = result;

                        isProtecting = false; // Treat as not protecting for the rest of the logic
                    }
                    else if (shieldBreaker.FailsIfNoProtect)
                    {
                        // Fail the move
                        result.DamageAmount = 0;
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "But it failed!" });
                        EventBus.Publish(new GameEvents.MoveFailed { Actor = action.Actor });
                        results[i] = result;
                        continue; // Skip damage application
                    }
                }

                if (isProtecting)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{target.Name} [cStatus]protected[/] against the attack!"
                    });

                    result.WasProtected = true;
                    result.DamageAmount = 0;
                    results[i] = result;
                    continue;
                }

                target.ApplyDamage(result.DamageAmount);

                var ctx = new CombatContext
                {
                    Actor = action.Actor,
                    Target = target,
                    Move = action.ChosenMove,
                    BaseDamage = result.DamageAmount,
                    IsCritical = result.WasCritical,
                    IsGraze = result.WasGraze
                };

                foreach (var ability in action.ChosenMove.Abilities)
                {
                    if (ability is IOnHitEffect onHit)
                    {
                        onHit.OnHit(ctx, result.DamageAmount);
                    }
                }

                SecondaryEffectSystem.ProcessPrimaryEffects(action, target);
            }

            EventBus.Publish(new GameEvents.BattleActionExecuted
            {
                Actor = action.Actor,
                ChosenMove = action.ChosenMove,
                Targets = targets,
                DamageResults = results
            });

            _currentActionForEffects = action;
            _currentActionDamageResults = results;
            _currentActionFinalTargets = targets;

            _pendingImpact = null;
        }

        private void ProcessMoveActionPostImpact(QueuedAction action)
        {
            if (_currentActionFinalTargets != null && _currentActionFinalTargets.All(t => t.IsDefeated))
            {
                var ctx = new CombatContext { Actor = action.Actor, Move = action.ChosenMove };
                foreach (var effect in action.Actor.OnKillEffects)
                {
                    effect.OnKill(ctx);
                }
            }

            var actor = action.Actor;
            if (!actor.HasUsedFirstAttack) actor.HasUsedFirstAttack = true;

            foreach (var effect in actor.OnActionCompleteEffects)
            {
                effect.OnActionComplete(action, actor);
            }

            foreach (var ability in action.ChosenMove.Abilities)
            {
                if (ability is IOnActionComplete onComplete)
                {
                    onComplete.OnActionComplete(action, actor);
                }
            }

            if (_currentPhase != BattlePhase.ProcessingInteraction && _currentPhase != BattlePhase.WaitingForSwitchCompletion)
            {
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

        private List<BattleCombatant> ResolveTargets(QueuedAction action)
        {
            var targetType = action.ChosenMove?.Target ?? action.ChosenItem?.Target ?? TargetType.None;
            var actor = action.Actor;
            var specifiedTarget = action.Target;

            var validCandidates = TargetingHelper.GetValidTargets(actor, targetType, _allCombatants);

            if (targetType == TargetType.RandomBoth || targetType == TargetType.RandomEvery || targetType == TargetType.RandomAll)
            {
                if (validCandidates.Any())
                {
                    return new List<BattleCombatant> { validCandidates[_random.Next(validCandidates.Count)] };
                }
                return new List<BattleCombatant>();
            }

            if (targetType == TargetType.All || targetType == TargetType.Both || targetType == TargetType.Every || targetType == TargetType.Team || targetType == TargetType.Ally)
            {
                return validCandidates;
            }

            // --- SMART RETARGETING LOGIC ---
            if (specifiedTarget != null)
            {
                // Case A: Target is still valid
                if (validCandidates.Contains(specifiedTarget))
                {
                    return new List<BattleCombatant> { specifiedTarget };
                }

                // Case B: Target is invalid (Dead/Switched) - SMART RETARGETING
                // Determine original intent
                bool wasHostile = actor.IsPlayerControlled != specifiedTarget.IsPlayerControlled;

                if (wasHostile)
                {
                    // Look for other enemies
                    var alternativeEnemies = validCandidates
                        .Where(c => c.IsPlayerControlled != actor.IsPlayerControlled)
                        .ToList();

                    if (alternativeEnemies.Any())
                        return new List<BattleCombatant> { alternativeEnemies.First() };
                }
                else
                {
                    // Look for other allies (including self)
                    var alternativeAllies = validCandidates
                        .Where(c => c.IsPlayerControlled == actor.IsPlayerControlled)
                        .ToList();

                    if (alternativeAllies.Any())
                        return new List<BattleCombatant> { alternativeAllies.First() };
                }

                // If we are here, it means we couldn't find a valid target on the INTENDED side.
                // Return empty to cause action failure rather than hitting the wrong team.
                return new List<BattleCombatant>();
            }

            // Fallback (No target specified initially)
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

            if (_actionQueue.Any() || _actionToExecute != null)
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

                if (!combatant.UsedProtectThisTurn)
                {
                    combatant.ConsecutiveProtectUses = 0;
                }
                combatant.UsedProtectThisTurn = false;

                // Reset Dazed state at end of turn
                combatant.IsDazed = false;

                var effectsToRemove = new List<StatusEffectInstance>();
                foreach (var effect in combatant.ActiveStatusEffects)
                {
                    if (!effect.IsPermanent)
                    {
                        effect.DurationInTurns--;
                    }

                    if (effect.EffectType == StatusEffectType.Poison)
                    {
                        int safeTurnCount = Math.Min(effect.PoisonTurnCount, 30);
                        long rawDamage = (long)Global.Instance.PoisonBaseDamage * (long)Math.Pow(2, safeTurnCount);
                        int poisonDamage = (int)Math.Min(rawDamage, int.MaxValue);

                        combatant.ApplyDamage(poisonDamage);
                        EventBus.Publish(new GameEvents.StatusEffectTriggered { Combatant = combatant, EffectType = StatusEffectType.Poison, Damage = poisonDamage });

                        effect.PoisonTurnCount++;
                    }

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

            foreach (var ability in move.Abilities)
            {
                if (ability is IActionModifier am)
                {
                    am.ModifyAction(action, actor);
                }
            }

            return action;
        }

        private bool HandlePreActionEffects(QueuedAction action)
        {
            return false;
        }

        private bool ProcessPreResolutionEffects(QueuedAction action)
        {
            return true;
        }

        private MoveData HandlePreDamageEffects(MoveData originalMove, BattleCombatant target)
        {
            return originalMove;
        }
    }
}
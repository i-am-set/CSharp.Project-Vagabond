using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Systems;
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
            BattleStartIntro,
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

        public IReadOnlyList<QueuedAction> ActionQueue => _actionQueue;

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

        private class PendingImpactData
        {
            public QueuedAction Action;
            public List<BattleCombatant> Targets;
            public List<DamageCalculator.DamageResult> Results;
        }
        private PendingImpactData _pendingImpact;

        private BattleInteraction _activeInteraction;

        private int _multiHitRemaining = 0;
        private int _multiHitTotalExecuted = 0;
        private int _multiHitCrits = 0;
        public bool IsProcessingMultiHit => _multiHitRemaining > 0;

        public BattlePhase CurrentPhase => _currentPhase;
        public IEnumerable<BattleCombatant> AllCombatants => _allCombatants;
        public bool CanAdvance { get; set; } = true;

        private readonly BattleAnimationManager _animationManager;
        private readonly Global _global;

        public BattleManager(List<BattleCombatant> playerParty, List<BattleCombatant> enemyParty, BattleAnimationManager animationManager)
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

            _currentPhase = BattlePhase.BattleStartIntro;
            _endOfTurnEffectsProcessed = false;

            _animationManager = animationManager;
            _global = ServiceLocator.Get<Global>();

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
                    // Note: Lifecycle effects usually fire their own notifications if needed
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
            if (_currentPhase == BattlePhase.AnimatingMove && _pendingImpact != null)
            {
                ApplyPendingImpact();
                if (_actionPendingAnimation != null)
                {
                    ProcessMoveActionPostImpact(_actionPendingAnimation);
                }
            }

            _actionToExecute = null;
            _actionPendingAnimation = null;
            _pendingImpact = null;
            _activeInteraction = null;

            if (_currentPhase == BattlePhase.BattleStartIntro)
            {
                _currentPhase = BattlePhase.StartOfTurn;
            }
            else if (_currentPhase == BattlePhase.AnimatingMove ||
                _currentPhase == BattlePhase.ActionResolution ||
                _currentPhase == BattlePhase.SecondaryEffectResolution ||
                _currentPhase == BattlePhase.ProcessingInteraction ||
                _currentPhase == BattlePhase.WaitingForSwitchCompletion)
            {
                if (!IsProcessingMultiHit)
                {
                    _currentPhase = BattlePhase.CheckForDefeat;
                }
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
                var action = _actionPendingAnimation;

                if (_pendingImpact != null)
                {
                    ApplyPendingImpact();
                }

                _actionPendingAnimation = null;

                ProcessMoveActionPostImpact(action);
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
            Debug.WriteLine($"[BattleManager] Disengage triggered for {e.Actor.Name}. Starting interaction.");
            CanAdvance = false;
            _currentPhase = BattlePhase.ProcessingInteraction;

            _activeInteraction = new SwitchInteraction(e.Actor, (result) =>
            {
                if (result is BattleCombatant target && target != e.Actor)
                {
                    Debug.WriteLine($"[BattleManager] SwitchInteraction resolved. Target: {target.Name}. Initiating sequence.");
                    InitiateSwitchSequence(e.Actor, target);
                }
                else
                {
                    Debug.WriteLine($"[BattleManager] SwitchInteraction resolved with NULL or Invalid Target. Cancelling switch.");
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
            _currentPhase = BattlePhase.WaitingForSwitchCompletion;
            CanAdvance = false;

            EventBus.Publish(new GameEvents.SwitchSequenceInitiated
            {
                OutgoingCombatant = actor,
                IncomingCombatant = incomingMember
            });
        }

        public void PerformLogicalSwitch(BattleCombatant actor, BattleCombatant incomingMember)
        {
            if (actor == null || incomingMember == null || actor == incomingMember) return;

            incomingMember.IsDying = false;
            incomingMember.IsRemovalProcessed = false;

            int oldSlot = actor.BattleSlot;
            int newSlot = incomingMember.BattleSlot;

            actor.BattleSlot = newSlot;
            incomingMember.BattleSlot = oldSlot;

            incomingMember.HasUsedFirstAttack = false;

            RefreshCombatantCaches();

            foreach (var action in _actionQueue)
            {
                if (action.Target == actor)
                {
                    action.Target = incomingMember;
                }
            }

            HandleOnEnterAbilities(incomingMember);
        }

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
                case BattlePhase.BattleStartIntro: break;
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
                case BattlePhase.WaitingForSwitchCompletion: break;
            }
        }

        private void HandleStartOfTurn()
        {
            SanitizeBattlefield();

            _endOfTurnEffectsProcessed = false;
            var startOfTurnActions = new List<QueuedAction>();

            foreach (var combatant in _cachedAllActive)
            {
                foreach (var ability in combatant.TurnLifecycleEffects)
                {
                    ability.OnTurnStart(combatant);
                    // Removed automatic event firing. Abilities must fire their own events if they do something.
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

        private void SanitizeBattlefield()
        {
            bool changesMade = false;

            foreach (var combatant in _allCombatants)
            {
                if (combatant.IsActiveOnField && (combatant.IsDefeated || combatant.Stats.CurrentHP <= 0))
                {
                    combatant.BattleSlot = -1;
                    combatant.IsDying = false;
                    combatant.IsRemovalProcessed = true;
                    _actionQueue.RemoveAll(a => a.Actor == combatant);
                    changesMade = true;
                }
            }

            if (changesMade) RefreshCombatantCaches();

            for (int slot = 0; slot < 2; slot++)
            {
                if (!_cachedActiveEnemies.Any(c => c.BattleSlot == slot))
                {
                    var reinforcement = _enemyParty.FirstOrDefault(c => c.BattleSlot >= 2 && !c.IsDefeated);
                    if (reinforcement != null)
                    {
                        reinforcement.BattleSlot = slot;
                        changesMade = true;
                        EventBus.Publish(new GameEvents.CombatantSpawned { Combatant = reinforcement });
                        HandleOnEnterAbilities(reinforcement);
                    }
                }
            }

            for (int slot = 0; slot < 2; slot++)
            {
                if (!_cachedActivePlayers.Any(c => c.BattleSlot == slot))
                {
                    var reinforcement = _playerParty.FirstOrDefault(c => c.BattleSlot >= 2 && !c.IsDefeated);
                    if (reinforcement != null)
                    {
                        reinforcement.BattleSlot = slot;
                        changesMade = true;
                        EventBus.Publish(new GameEvents.CombatantSpawned { Combatant = reinforcement });
                        HandleOnEnterAbilities(reinforcement);
                    }
                }
            }

            if (changesMade) RefreshCombatantCaches();
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

            if (nextAction.Actor.IsDefeated || !nextAction.Actor.IsActiveOnField || nextAction.Actor.Stats.CurrentHP <= 0)
            {
                return;
            }

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

            action.Actor.PendingDisengage = false;

            var multiHit = action.ChosenMove.Abilities.OfType<MultiHitAbility>().FirstOrDefault();
            if (multiHit != null)
            {
                int hits = _random.Next(multiHit.MinHits, multiHit.MaxHits + 1);
                _multiHitTotalExecuted = 0;
                _multiHitRemaining = hits;
                _multiHitCrits = 0;
            }
            else
            {
                _multiHitTotalExecuted = 0;
                _multiHitRemaining = 1;
                _multiHitCrits = 0;
            }

            PrepareHit(action);
        }

        private void PrepareHit(QueuedAction action)
        {
            var targetsForThisHit = ResolveTargets(action);

            if (targetsForThisHit.Any())
            {
                var damageResultsForThisHit = new List<DamageCalculator.DamageResult>();
                float multiTargetModifier = (targetsForThisHit.Count > 1) ? BattleConstants.MULTI_TARGET_MODIFIER : 1.0f;
                var grazeStatus = new Dictionary<BattleCombatant, bool>();

                foreach (var target in targetsForThisHit)
                {
                    var moveInstance = HandlePreDamageEffects(action.ChosenMove, target);
                    var result = DamageCalculator.CalculateDamage(action, target, moveInstance, multiTargetModifier);
                    damageResultsForThisHit.Add(result);
                    grazeStatus[target] = result.WasGraze;
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
                    EventBus.Publish(new GameEvents.MoveAnimationTriggered { Move = animMove, Targets = normalTargets, GrazeStatus = grazeStatus });
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

                    EventBus.Publish(new GameEvents.MoveAnimationTriggered { Move = protectMove, Targets = protectedTargets, GrazeStatus = grazeStatus });

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

            var significantTargetIds = new List<string>();

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var result = results[i];

                var shieldBreaker = action.ChosenMove.Abilities.OfType<IShieldBreaker>().FirstOrDefault();
                bool isProtecting = target.HasStatusEffect(StatusEffectType.Protected);

                if (shieldBreaker != null)
                {
                    if (isProtecting)
                    {
                        target.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Protected);
                        EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = target, EffectType = StatusEffectType.Protected });
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{action.Actor.Name} shattered the guard!" });

                        result.DamageAmount = (int)(result.DamageAmount * shieldBreaker.BreakDamageMultiplier);
                        results[i] = result;
                        isProtecting = false;
                    }
                    else if (shieldBreaker.FailsIfNoProtect)
                    {
                        result.DamageAmount = 0;
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "But it failed!" });
                        EventBus.Publish(new GameEvents.MoveFailed { Actor = action.Actor });
                        results[i] = result;
                        continue;
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

                if (result.DamageAmount > 0 && result.DamageAmount > (target.Stats.MaxHP * 0.15f))
                {
                    significantTargetIds.Add(target.CombatantID);
                }

                var ctx = new CombatContext
                {
                    Actor = action.Actor,
                    Target = target,
                    Move = action.ChosenMove,
                    BaseDamage = result.DamageAmount,
                    IsCritical = result.WasCritical,
                    IsGraze = result.WasGraze
                };

                // 1. Trigger Actor's OnHit effects (Relics, etc.)
                foreach (var effect in action.Actor.OnHitEffects)
                {
                    effect.OnHit(ctx, result.DamageAmount);
                    // Removed automatic event firing. The ability must fire it if successful.
                }

                // 2. Trigger Target's OnDamaged effects (Relics, etc.)
                foreach (var effect in target.OnDamagedEffects)
                {
                    effect.OnDamaged(ctx, result.DamageAmount);
                    // Removed automatic event firing.
                }

                // 3. Trigger Move's OnHit effects
                foreach (var ability in action.ChosenMove.Abilities)
                {
                    if (ability is IOnHitEffect onHit)
                    {
                        onHit.OnHit(ctx, result.DamageAmount);
                        // Removed automatic event firing.
                    }
                }

                // 4. Process Accumulated Lifesteal
                if (ctx.AccumulatedLifestealPercent > 0 && result.DamageAmount > 0)
                {
                    int totalHeal = (int)(result.DamageAmount * (ctx.AccumulatedLifestealPercent / 100f));
                    if (totalHeal > 0)
                    {
                        // Check for Lifesteal Reactions (e.g. Caustic Blood) on the target
                        bool preventHealing = false;
                        foreach (var reaction in target.LifestealReactions)
                        {
                            if (reaction.OnLifestealReceived(action.Actor, totalHeal, target))
                            {
                                preventHealing = true;
                                // Reaction abilities should fire their own events
                                break; // Stop checking if one reaction blocks it
                            }
                        }

                        if (!preventHealing)
                        {
                            int hpBefore = (int)action.Actor.VisualHP;
                            action.Actor.ApplyHealing(totalHeal);
                            EventBus.Publish(new GameEvents.CombatantHealed
                            {
                                Actor = action.Actor,
                                Target = action.Actor,
                                HealAmount = totalHeal,
                                VisualHPBefore = hpBefore
                            });
                        }
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

            if (significantTargetIds.Any())
            {
                Color flashColor = action.Actor.IsPlayerControlled ? Color.White : _global.Palette_Red;
                _animationManager.TriggerImpactFlash(flashColor, 0.15f, significantTargetIds);
            }

            _currentActionForEffects = action;
            _currentActionDamageResults = results;
            _currentActionFinalTargets = targets;

            _pendingImpact = null;
        }

        private void ProcessMoveActionPostImpact(QueuedAction action)
        {
            if (_currentActionDamageResults != null)
            {
                foreach (var res in _currentActionDamageResults)
                {
                    if (res.WasCritical) _multiHitCrits++;
                }
            }

            _multiHitTotalExecuted++;
            _multiHitRemaining--;

            if (_multiHitRemaining > 0)
            {
                if (_currentActionFinalTargets != null && _currentActionFinalTargets.All(t => t.IsDefeated))
                {
                    _multiHitRemaining = 0;
                }
                else
                {
                    PrepareHit(action);
                    return;
                }
            }

            if (_currentActionFinalTargets != null && _currentActionFinalTargets.All(t => t.IsDefeated))
            {
                var ctx = new CombatContext { Actor = action.Actor, Move = action.ChosenMove };
                foreach (var effect in action.Actor.OnKillEffects)
                {
                    effect.OnKill(ctx);
                    // Removed automatic event firing.
                }
            }

            var actor = action.Actor;
            if (!actor.HasUsedFirstAttack) actor.HasUsedFirstAttack = true;

            foreach (var effect in actor.OnActionCompleteEffects)
            {
                effect.OnActionComplete(action, actor);
                // Removed automatic event firing.
            }

            foreach (var ability in action.ChosenMove.Abilities)
            {
                if (ability is IOnActionComplete onComplete)
                {
                    onComplete.OnActionComplete(action, actor);
                    // Removed automatic event firing.
                }
            }

            if (_multiHitTotalExecuted > 1)
            {
                EventBus.Publish(new GameEvents.MultiHitActionCompleted
                {
                    Actor = action.Actor,
                    ChosenMove = action.ChosenMove,
                    HitCount = _multiHitTotalExecuted,
                    CriticalHitCount = _multiHitCrits
                });
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

            if (specifiedTarget != null)
            {
                if (validCandidates.Contains(specifiedTarget))
                {
                    return new List<BattleCombatant> { specifiedTarget };
                }

                bool wasHostile = actor.IsPlayerControlled != specifiedTarget.IsPlayerControlled;

                if (wasHostile)
                {
                    var alternativeEnemies = validCandidates
                        .Where(c => c.IsPlayerControlled != actor.IsPlayerControlled)
                        .ToList();

                    if (alternativeEnemies.Any())
                        return new List<BattleCombatant> { alternativeEnemies.First() };
                }
                else
                {
                    var alternativeAllies = validCandidates
                        .Where(c => c.IsPlayerControlled == actor.IsPlayerControlled)
                        .ToList();

                    if (alternativeAllies.Any())
                        return new List<BattleCombatant> { alternativeAllies.First() };
                }

                return new List<BattleCombatant>();
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

                _actionQueue.RemoveAll(a => a.Actor == combatant);
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
                    // Removed automatic event firing.
                }

                if (!combatant.UsedProtectThisTurn)
                {
                    combatant.ConsecutiveProtectUses = 0;
                }
                combatant.UsedProtectThisTurn = false;

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

                    if (effect.EffectType == StatusEffectType.Bleeding)
                    {
                        int bleedDamage = Math.Max(1, (int)(combatant.Stats.MaxHP * 0.1f));
                        combatant.ApplyDamage(bleedDamage);
                        EventBus.Publish(new GameEvents.StatusEffectTriggered { Combatant = combatant, EffectType = StatusEffectType.Bleeding, Damage = bleedDamage });
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
                    // Removed automatic event firing.
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
﻿
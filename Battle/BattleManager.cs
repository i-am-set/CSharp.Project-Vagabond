using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
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
using static ProjectVagabond.GameEvents;

namespace ProjectVagabond.Battle
{
    public class BattleManager
    {
        public enum BattlePhase
        {
            BattleStartIntro,
            BattleStartEffects,
            StartOfRound,
            ActionSelection,
            ActionResolution,
            EndOfRound,
            BattleOver,
            ProcessingInteraction,
            WaitingForSwitchCompletion
        }

        private enum EndOfRoundStage
        {
            VisualWait,
            ProcessEffects,
            CheckDeaths,
            CheckWinLoss,
            ProcessReinforcements,
            PrepareNextRound
        }

        private struct PendingImpactData
        {
            public QueuedAction Action;
            public List<BattleCombatant> Targets;
            public List<DamageCalculator.DamageResult> Results;
            public List<BattleCombatant> ProtectedTargets;
            public List<BattleCombatant> NormalTargets;
        }

        private PendingImpactData? _pendingImpactData = null;
        private bool _isWaitingForImpact = false;

        private readonly List<BattleCombatant> _playerParty;
        private readonly List<BattleCombatant> _enemyParty;
        private readonly List<BattleCombatant> _allCombatants;
        private readonly List<BattleCombatant> _cachedActivePlayers = new List<BattleCombatant>();
        private readonly List<BattleCombatant> _cachedActiveEnemies = new List<BattleCombatant>();
        private readonly List<BattleCombatant> _cachedAllActive = new List<BattleCombatant>();

        private List<QueuedAction> _actionQueue;
        private readonly Dictionary<int, QueuedAction> _pendingPlayerActions = new Dictionary<int, QueuedAction>();

        public IReadOnlyList<QueuedAction> ActionQueue => _actionQueue;

        private QueuedAction? _currentActionForEffects;
        private List<DamageCalculator.DamageResult> _currentActionDamageResults;
        private List<BattleCombatant> _currentActionFinalTargets;
        private BattlePhase _currentPhase;
        private EndOfRoundStage _endOfRoundStage;

        public int RoundNumber { get; private set; }
        private static readonly Random _random = new Random();

        private QueuedAction? _actionToExecute;

        // --- PACING STATE ---
        private float _turnPacingTimer = 0f;
        private const float ACTION_DELAY = 0.5f;

        public BattleCombatant? CurrentActingCombatant { get; private set; }

        private int _reinforcementSlotIndex = 0;
        private bool _reinforcementAnnounced = false;
        private bool _waitingForReinforcementSelection = false;

        private readonly Dictionary<string, string> _lastDefeatedNames = new Dictionary<string, string>();

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
        private readonly StringBuilder _roundLog = new StringBuilder();

        // Context for Ability System
        private readonly BattleContext _battleContext;

        // Watchdog for animation softlocks
        private float _phaseWatchdogTimer = 0f;

        // --- Startup Stagger Logic ---
        private readonly Queue<Action> _startupEffectQueue = new Queue<Action>();
        private float _startupEffectTimer = 0f;
        private const float STARTUP_EFFECT_DELAY = 0.5f;

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
            _endOfRoundStage = EndOfRoundStage.VisualWait;

            _animationManager = animationManager;
            _global = ServiceLocator.Get<Global>();

            _battleContext = new BattleContext();

            EventBus.Subscribe<GameEvents.DisengageTriggered>(OnDisengageTriggered);
            EventBus.Subscribe<GameEvents.TriggerImpact>(OnTriggerImpact);
        }

        public void StartBattle()
        {
        }

        public void TriggerBattleStartEvents()
        {
            _battleContext.ResetMultipliers();

            var battleStartEvent = new BattleStartedEvent(_allCombatants);

            foreach (var combatant in _cachedAllActive)
            {
                _battleContext.Actor = combatant;
                combatant.NotifyAbilities(battleStartEvent, _battleContext);
            }

            if (_startupEffectQueue.Any())
            {
                _currentPhase = BattlePhase.BattleStartEffects;
                _startupEffectTimer = 0f;
                CanAdvance = false;
            }
            else
            {
                _currentPhase = BattlePhase.StartOfRound;
                CanAdvance = true;
            }
        }

        public void EnqueueStartupEvent(Action action)
        {
            _startupEffectQueue.Enqueue(action);
        }

        private void InitializeSlots(List<BattleCombatant> party)
        {
            for (int i = 0; i < party.Count; i++) party[i].BattleSlot = i;
        }

        private void RefreshCombatantCaches()
        {
            _cachedActivePlayers.Clear();
            _cachedActiveEnemies.Clear();
            _cachedAllActive.Clear();

            foreach (var c in _playerParty) if (c.IsActiveOnField && !c.IsDefeated) { _cachedActivePlayers.Add(c); _cachedAllActive.Add(c); }
            foreach (var c in _enemyParty) if (c.IsActiveOnField && !c.IsDefeated) { _cachedActiveEnemies.Add(c); _cachedAllActive.Add(c); }
        }

        public void RequestNextPhase()
        {
            if (_currentPhase == BattlePhase.EndOfRound) HandleEndOfRound();
            CanAdvance = true;
        }

        public void ForceAdvance()
        {
            _actionToExecute = null;
            _activeInteraction = null;

            _isWaitingForImpact = false;
            _pendingImpactData = null;

            if (_currentPhase == BattlePhase.BattleStartIntro) _currentPhase = BattlePhase.StartOfRound;
            else if (_currentPhase == BattlePhase.ActionResolution || _currentPhase == BattlePhase.ProcessingInteraction || _currentPhase == BattlePhase.WaitingForSwitchCompletion)
            {
                if (!IsProcessingMultiHit)
                {
                    // Force skip to end of round to prevent stuck states
                    _actionQueue.Clear();
                    _currentPhase = BattlePhase.EndOfRound;
                    _endOfRoundStage = EndOfRoundStage.VisualWait;
                }
            }
            else if (_currentPhase == BattlePhase.EndOfRound) { RoundNumber++; _currentPhase = BattlePhase.StartOfRound; }
            CanAdvance = true;
        }

        private void OnDisengageTriggered(GameEvents.DisengageTriggered e)
        {
            CanAdvance = false;
            _currentPhase = BattlePhase.ProcessingInteraction;
            _activeInteraction = new SwitchInteraction(e.Actor, (result) =>
            {
                if (result is BattleCombatant target && target != e.Actor) InitiateSwitchSequence(e.Actor, target);
                else
                {
                    // Cancelled switch, resume turn
                    _currentPhase = BattlePhase.ActionResolution;
                    CanAdvance = true;
                }
                _activeInteraction = null;
            });
            _activeInteraction.Start(this);
        }

        private void OnTriggerImpact(GameEvents.TriggerImpact e)
        {
            CommitImpact();
        }

        private void CommitImpact()
        {
            if (!_isWaitingForImpact || _pendingImpactData == null) return;

            var data = _pendingImpactData.Value;
            var action = data.Action;
            var targets = data.Targets;
            var results = data.Results;
            var normalTargets = data.NormalTargets;
            var protectedTargets = data.ProtectedTargets;

            _isWaitingForImpact = false;
            _pendingImpactData = null;
            _phaseWatchdogTimer = 0f;

            var significantTargetIds = new List<string>();

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var result = results[i];
                _battleContext.Target = target;

                if (result.WasProtected)
                {
                    AppendToCurrentLine(" PROTECTED!");
                    result.DamageAmount = 0;
                    results[i] = result;
                    continue;
                }

                target.ApplyDamage(result.DamageAmount);

                if (target.Stats.CurrentHP <= 0 && !target.IsDying)
                {
                    EventBus.Publish(new GameEvents.CombatantVisualDeath { Victim = target });
                }

                // --- GUARD BREAK LOGIC ---
                if (!result.WasGraze && result.DamageAmount > 0 && target.CurrentGuard > 0)
                {
                    target.CurrentGuard--;
                    EventBus.Publish(new GameEvents.GuardChanged { Combatant = target, NewValue = target.CurrentGuard });

                    if (target.CurrentGuard == 0)
                    {
                        EventBus.Publish(new GameEvents.GuardBroken { Combatant = target });
                        // REMOVED: target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Stun, 1));
                        AppendToCurrentLine(" [cStatus]GUARD BROKEN![/]");
                    }
                }

                if (result.DamageAmount > 0 && result.DamageAmount >= (target.Stats.MaxHP * 0.50f)) significantTargetIds.Add(target.CombatantID);
                if (result.WasCritical) AppendToCurrentLine(" [cCrit]CRITICAL HIT![/]");
                if (result.WasVulnerable) AppendToCurrentLine(" [cVulnerable]VULNERABLE![/]");

                var reactionEvt = new ReactionEvent(action.Actor, target, action, result);
                action.Actor.NotifyAbilities(reactionEvt, _battleContext);
                target.NotifyAbilities(reactionEvt, _battleContext);
                foreach (var ab in action.ChosenMove.Abilities) ab.OnEvent(reactionEvt, _battleContext);
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
                Color flashColor = action.Actor.IsPlayerControlled ? Color.White : _global.Palette_Rust;
                _animationManager.TriggerImpactFlash(flashColor, 0.5f, significantTargetIds);
            }

            foreach (var target in targets)
            {
                if (target.IsPlayerControlled)
                {
                    int dmg = results[targets.IndexOf(target)].DamageAmount;
                    if (dmg > 0)
                    {
                        float damageRatio = (float)dmg / target.Stats.MaxHP;
                        float intensity = Math.Min(1.0f + (damageRatio * 5.0f), 5.0f);
                        ServiceLocator.Get<HapticsManager>().TriggerImpactTwist(intensity, 0.25f);
                    }
                    break;
                }
            }

            _currentActionForEffects = action;
            _currentActionDamageResults = results;
            _currentActionFinalTargets = targets;

            ProcessMoveActionPostImpact(action);
        }

        public void SubmitInteractionResult(object result)
        {
            if (_activeInteraction != null) { _activeInteraction.Resolve(result); return; }
            if (_waitingForReinforcementSelection)
            {
                if (result is BattleCombatant reinforcement)
                {
                    bool isPlayerSlot = _reinforcementSlotIndex >= 2;
                    int slot = _reinforcementSlotIndex % 2;
                    reinforcement.BattleSlot = slot;
                    RefreshCombatantCaches();
                    string key = $"{(isPlayerSlot ? "Player" : "Enemy")}_{slot}";
                    string msg = $"{reinforcement.Name} enters the battle!";
                    if (_lastDefeatedNames.TryGetValue(key, out string deadName)) { msg = $"{reinforcement.Name} takes {deadName}'s place!"; _lastDefeatedNames.Remove(key); }
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
                    EventBus.Publish(new GameEvents.CombatantSpawned { Combatant = reinforcement });

                    var entryEvent = new GameEvents.CombatantEnteredEvent(reinforcement);
                    _battleContext.ResetMultipliers();
                    _battleContext.Actor = reinforcement;
                    reinforcement.NotifyAbilities(entryEvent, _battleContext);

                    _waitingForReinforcementSelection = false;
                    _reinforcementAnnounced = false;
                    _reinforcementSlotIndex++;
                    // Stay in ProcessReinforcements stage, loop will continue next update
                    CanAdvance = true;
                }
            }
        }

        private void InitiateSwitchSequence(BattleCombatant actor, BattleCombatant incomingMember)
        {
            _currentPhase = BattlePhase.WaitingForSwitchCompletion;
            CanAdvance = false;
            EventBus.Publish(new GameEvents.SwitchSequenceInitiated { OutgoingCombatant = actor, IncomingCombatant = incomingMember });
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
            foreach (var action in _actionQueue) if (action.Target == actor) action.Target = incomingMember;

            var entryEvent = new GameEvents.CombatantEnteredEvent(incomingMember);
            _battleContext.ResetMultipliers();
            _battleContext.Actor = incomingMember;
            incomingMember.NotifyAbilities(entryEvent, _battleContext);
        }

        public void ResumeAfterSwitch()
        {
            // Switch complete, resume action resolution
            _currentPhase = BattlePhase.ActionResolution;
            _turnPacingTimer = 0.5f;
            CanAdvance = true;
        }

        public void SubmitAction(int slotIndex, QueuedAction action)
        {
            if (_currentPhase != BattlePhase.ActionSelection) return;
            _pendingPlayerActions[slotIndex] = action;
            CheckIfTurnReady();
        }

        public void CancelAction(int slotIndex)
        {
            if (_currentPhase != BattlePhase.ActionSelection) return;
            if (_pendingPlayerActions.ContainsKey(slotIndex)) _pendingPlayerActions.Remove(slotIndex);
        }

        public bool IsActionPending(int slotIndex) => _pendingPlayerActions.ContainsKey(slotIndex);

        private void CheckIfTurnReady()
        {
            int activePlayerCount = 0;
            foreach (var player in _cachedActivePlayers)
            {
                // Removed !player.Tags.Has(GameplayTags.States.Stunned) check
                // Stunned players now require manual input (which will fail later)
                if (player.ChargingAction == null)
                {
                    activePlayerCount++;
                }
            }

            if (_pendingPlayerActions.Count >= activePlayerCount)
            {
                FinalizeTurnSelection();
            }
        }

        private void FinalizeTurnSelection()
        {
            CurrentActingCombatant = null;
            foreach (var kvp in _pendingPlayerActions) AddActionToQueue(kvp.Value);
            _pendingPlayerActions.Clear();

            foreach (var action in _actionQueue)
            {
                if (action.ChosenMove != null)
                {
                    var prioEvent = new CheckActionPriorityEvent(action.Actor, action.ChosenMove, action.Priority);
                    _battleContext.ResetMultipliers();
                    _battleContext.Actor = action.Actor;
                    action.Actor.NotifyAbilities(prioEvent, _battleContext);
                    action.Priority = prioEvent.Priority;
                }
            }

            _actionQueue = _actionQueue.OrderByDescending(a => a.Priority).ThenByDescending(a => a.ActorAgility).ToList();
            var lastAction = _actionQueue.LastOrDefault(a => a.Type == QueuedActionType.Move);
            if (lastAction != null) lastAction.IsLastActionInRound = true;

            _currentPhase = BattlePhase.ActionResolution;
            _turnPacingTimer = 0.25f; // Small initial delay before first action
        }

        private void AddActionToQueue(QueuedAction action)
        {
            _actionQueue.Add(action);
        }

        public List<BattleCombatant> GetReservedBenchMembers()
        {
            var reserved = new List<BattleCombatant>();
            foreach (var action in _pendingPlayerActions.Values)
            {
                if (action.Type == QueuedActionType.Switch && action.Target != null) reserved.Add(action.Target);
            }
            return reserved;
        }

        public void SkipPacing()
        {
            _turnPacingTimer = 0f;
        }

        public void Update(float deltaTime)
        {
            if (_isWaitingForImpact)
            {
                _phaseWatchdogTimer += deltaTime;
                if (_phaseWatchdogTimer > 4.0f)
                {
                    // Failsafe: Force commit if sync missed
                    CommitImpact();
                }
                return;
            }

            bool isWaitingForInput = _currentPhase == BattlePhase.ProcessingInteraction ||
                                     _currentPhase == BattlePhase.WaitingForSwitchCompletion ||
                                     _waitingForReinforcementSelection;

            if (!CanAdvance && !isWaitingForInput && _currentPhase != BattlePhase.BattleOver)
            {
                _phaseWatchdogTimer += deltaTime;
                if (_phaseWatchdogTimer > 4.0f)
                {
                    System.Diagnostics.Debug.WriteLine($"[BattleManager] WATCHDOG: Force advancing from {_currentPhase}");
                    ForceAdvance();
                    _phaseWatchdogTimer = 0f;
                }
            }
            else
            {
                _phaseWatchdogTimer = 0f;
            }

            if (_currentPhase == BattlePhase.BattleOver) return;

            switch (_currentPhase)
            {
                case BattlePhase.BattleStartIntro: break;
                case BattlePhase.BattleStartEffects: HandleBattleStartEffects(deltaTime); break;
                case BattlePhase.StartOfRound: HandleStartOfRound(); break;
                case BattlePhase.ActionSelection: break;
                case BattlePhase.ActionResolution:
                    if (_animationManager.IsBlockingAnimation) break;

                    _turnPacingTimer -= deltaTime;
                    if (_turnPacingTimer <= 0)
                    {
                        ProcessTurnLogic();
                    }
                    break;
                case BattlePhase.EndOfRound: HandleEndOfRound(); break;
                case BattlePhase.ProcessingInteraction: break;
                case BattlePhase.WaitingForSwitchCompletion: break;
            }
        }

        private void ProcessTurnLogic()
        {
            // 1. Check if queue is empty
            if (_actionQueue.Count == 0)
            {
                _currentPhase = BattlePhase.EndOfRound;
                _endOfRoundStage = EndOfRoundStage.VisualWait;
                return;
            }

            // 2. Dequeue next action
            var nextAction = _actionQueue[0];
            _actionQueue.RemoveAt(0);

            // 3. Validate Actor
            // If actor is defeated or not on field, skip turn entirely without modifying queue structure
            if (nextAction.Actor.IsDefeated || !nextAction.Actor.IsActiveOnField || nextAction.Actor.Stats.CurrentHP <= 0)
            {
                _turnPacingTimer = 0f; // Process next immediately
                return;
            }

            // 4. Handle Status States (Stun/Dazed)
            if (nextAction.Actor.Tags.Has(GameplayTags.States.Stunned))
            {
                EventBus.Publish(new GameEvents.ActionFailed { Actor = nextAction.Actor, Reason = "stunned" });
                _turnPacingTimer = ACTION_DELAY;
                return;
            }

            if (nextAction.Actor.Tags.Has(GameplayTags.States.Dazed))
            {
                nextAction.Actor.Tags.Remove(GameplayTags.States.Dazed);
                AppendToLog($"{nextAction.Actor.Name} was [cStatus]too dazed to move![/]");
                EventBus.Publish(new GameEvents.ActionFailed { Actor = nextAction.Actor, Reason = "dazed" });
                _turnPacingTimer = ACTION_DELAY;
                return;
            }

            if (nextAction.Type == QueuedActionType.Charging)
            {
                AppendToLog($"{nextAction.Actor.Name} IS CHARGING [cAction]{nextAction.ChosenMove.MoveName}[/].");
                _turnPacingTimer = ACTION_DELAY * 0.5f; // Faster pacing for charge messages
                return;
            }

            // 5. Validate Target (Retargeting Logic)
            if (nextAction.Target != null && (nextAction.Target.IsDefeated || nextAction.Target.Stats.CurrentHP <= 0))
            {
                // Try to find a new target in the same group (Enemy or Player)
                var newTarget = _allCombatants.FirstOrDefault(c =>
                    c.IsPlayerControlled == nextAction.Target.IsPlayerControlled &&
                    !c.IsDefeated &&
                    c.Stats.CurrentHP > 0 &&
                    c.IsActiveOnField);

                if (newTarget != null)
                {
                    nextAction.Target = newTarget;
                }
                else
                {
                    // No valid targets left
                    AppendToLog($"{nextAction.Actor.Name}'s action failed (No Target)!");
                    EventBus.Publish(new GameEvents.MoveFailed { Actor = nextAction.Actor });
                    _turnPacingTimer = ACTION_DELAY * 0.5f;
                    return;
                }
            }

            // 6. Execute Action
            _actionToExecute = nextAction;
            CurrentActingCombatant = nextAction.Actor;

            string moveName = nextAction.ChosenMove?.MoveName ?? "ACTION";
            string typeColor = nextAction.ChosenMove?.MoveType == MoveType.Spell ? "[cAlt]" : "[cAction]";
            AppendToLog($"{nextAction.Actor.Name} USED {typeColor}{moveName}[/].");

            if (_actionToExecute.ChosenMove != null)
            {
                _battleContext.ResetMultipliers();
                _battleContext.Actor = _actionToExecute.Actor;
                _battleContext.Target = _actionToExecute.Target;
                _battleContext.Move = _actionToExecute.ChosenMove;
                _battleContext.Action = _actionToExecute;

                var declEvent = new ActionDeclaredEvent(_actionToExecute.Actor, _actionToExecute.ChosenMove, _actionToExecute.Target);
                _actionToExecute.Actor.NotifyAbilities(declEvent, _battleContext);

                if (declEvent.IsHandled)
                {
                    _actionToExecute = null;
                    _turnPacingTimer = ACTION_DELAY;
                    return;
                }
            }

            EventBus.Publish(new GameEvents.ActionDeclared { Actor = _actionToExecute.Actor, Move = _actionToExecute.ChosenMove, Target = _actionToExecute.Target, Type = _actionToExecute.Type });

            ExecuteDeclaredAction();

            // 7. Reset Timer
            _turnPacingTimer = ACTION_DELAY;
        }

        private void HandleBattleStartEffects(float dt)
        {
            _startupEffectTimer -= dt;
            if (_startupEffectTimer <= 0)
            {
                if (_startupEffectQueue.Count > 0)
                {
                    var action = _startupEffectQueue.Dequeue();
                    action?.Invoke();
                    _startupEffectTimer = STARTUP_EFFECT_DELAY;
                }
                else
                {
                    _currentPhase = BattlePhase.StartOfRound;
                    CanAdvance = true;
                }
            }
        }

        private void HandleStartOfRound()
        {
            SanitizeBattlefield();

            if (!_cachedActiveEnemies.Any())
            {
                if (_enemyParty.All(c => c.IsDefeated)) { _currentPhase = BattlePhase.BattleOver; return; }
                else
                {
                    // Skip to EndOfRound to trigger reinforcements if enemies exist but aren't active
                    _currentPhase = BattlePhase.EndOfRound;
                    _endOfRoundStage = EndOfRoundStage.ProcessReinforcements;
                    _reinforcementSlotIndex = 0;
                    return;
                }
            }

            _roundLog.Clear();
            EventBus.Publish(new GameEvents.RoundLogUpdate { LogText = "" });
            _pendingPlayerActions.Clear();

            var startOfRoundActions = new List<QueuedAction>();
            foreach (var combatant in _cachedAllActive)
            {
                _battleContext.ResetMultipliers();
                _battleContext.Actor = combatant;
                _battleContext.Target = null;
                _battleContext.Move = null;

                var turnEvent = new TurnStartEvent(combatant);
                combatant.NotifyAbilities(turnEvent, _battleContext);

                if (turnEvent.IsHandled)
                {
                    continue;
                }

                bool isCharging = combatant.ChargingAction != null;

                if (isCharging)
                {
                    combatant.ChargingAction.TurnsRemaining--;
                    if (combatant.ChargingAction.TurnsRemaining <= 0) { startOfRoundActions.Add(combatant.ChargingAction.Action); combatant.ChargingAction = null; }
                }
                if (combatant.DelayedActions.Any())
                {
                    var readyActions = new List<DelayedAction>();
                    foreach (var delayed in combatant.DelayedActions) { delayed.TurnsRemaining--; if (delayed.TurnsRemaining <= 0) readyActions.Add(delayed); }
                    foreach (var ready in readyActions) startOfRoundActions.Add(ready.Action);
                    var remaining = combatant.DelayedActions.Where(d => !readyActions.Contains(d)).ToList();
                    combatant.DelayedActions = new Queue<DelayedAction>(remaining);
                }

                if (!combatant.IsPlayerControlled && !isCharging)
                {
                    // AI still needs to generate an action even if stunned, so it can fail properly in resolution
                    var aiAction = EnemyAI.DetermineBestAction(combatant, _allCombatants);
                    if (aiAction != null)
                    {
                        startOfRoundActions.Add(aiAction);
                    }
                }
            }
            _actionQueue.InsertRange(0, startOfRoundActions);

            // Removed !p.Tags.Has(GameplayTags.States.Stunned) check
            bool anyPlayerCanAct = _cachedActivePlayers.Any(p => p.ChargingAction == null);

            if (anyPlayerCanAct) _currentPhase = BattlePhase.ActionSelection;
            else FinalizeTurnSelection();
        }

        private void RecordDefeatedName(BattleCombatant combatant)
        {
            if (combatant.BattleSlot != -1)
            {
                string key = $"{(combatant.IsPlayerControlled ? "Player" : "Enemy")}_{combatant.BattleSlot}";
                _lastDefeatedNames[key] = combatant.Name;
            }
        }

        private void SanitizeBattlefield()
        {
            bool changesMade = false;
            foreach (var combatant in _allCombatants)
            {
                if (combatant.IsActiveOnField && (combatant.IsDefeated || combatant.Stats.CurrentHP <= 0))
                {
                    RecordDefeatedName(combatant);
                    combatant.BattleSlot = -1;
                    combatant.IsDying = false;
                    combatant.IsRemovalProcessed = true;
                    // We do NOT remove from _actionQueue here to preserve indices during resolution,
                    // but Sanitize is called at StartOfRound where queue is fresh anyway.
                    changesMade = true;
                }
            }
            if (changesMade) RefreshCombatantCaches();
        }

        private void AppendToLog(string text) { if (_roundLog.Length > 0) _roundLog.Append("\n"); _roundLog.Append(text); EventBus.Publish(new GameEvents.RoundLogUpdate { LogText = _roundLog.ToString() }); }
        private void AppendToCurrentLine(string text) { _roundLog.Append(text); EventBus.Publish(new GameEvents.RoundLogUpdate { LogText = _roundLog.ToString() }); }

        public void ExecuteDeclaredAction()
        {
            if (_actionToExecute == null) return;
            var action = _actionToExecute;
            _actionToExecute = null;
            if (action.Type == QueuedActionType.Switch) ProcessSwitchAction(action);
            else if (action.ChosenMove != null) ProcessMoveAction(action);
        }

        private void ProcessSwitchAction(QueuedAction action)
        {
            if (action.Target == null) return;
            InitiateSwitchSequence(action.Actor, action.Target);
        }

        private void ProcessMoveAction(QueuedAction action)
        {
            if (action.Actor.IsPlayerControlled && action.SpellbookEntry != null) action.SpellbookEntry.TimesUsed++;
            action.Actor.PendingDisengage = false;

            var multiHit = action.ChosenMove.Abilities.OfType<MultiHitAbility>().FirstOrDefault();
            if (multiHit != null) { int hits = _random.Next(multiHit.MinHits, multiHit.MaxHits + 1); _multiHitTotalExecuted = 0; _multiHitRemaining = hits; _multiHitCrits = 0; }
            else { _multiHitTotalExecuted = 0; _multiHitRemaining = 1; _multiHitCrits = 0; }

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

                _battleContext.ResetMultipliers();
                _battleContext.Actor = action.Actor;
                _battleContext.Move = action.ChosenMove;
                _battleContext.Action = action;

                foreach (var target in targetsForThisHit)
                {
                    _battleContext.Target = target;
                    var result = DamageCalculator.CalculateDamage(action, target, action.ChosenMove, multiTargetModifier, null, false, _battleContext);
                    damageResultsForThisHit.Add(result);
                    grazeStatus[target] = result.WasGraze;
                }

                var normalTargets = new List<BattleCombatant>();
                var protectedTargets = new List<BattleCombatant>();

                for (int i = 0; i < targetsForThisHit.Count; i++)
                {
                    var target = targetsForThisHit[i];
                    var result = damageResultsForThisHit[i];

                    if (result.WasProtected)
                    {
                        protectedTargets.Add(target);
                    }
                    else
                    {
                        normalTargets.Add(target);
                    }
                }

                // Store calculated data
                _pendingImpactData = new PendingImpactData
                {
                    Action = action,
                    Targets = targetsForThisHit,
                    Results = damageResultsForThisHit,
                    NormalTargets = normalTargets,
                    ProtectedTargets = protectedTargets
                };

                _isWaitingForImpact = true;
                _phaseWatchdogTimer = 0f;

                // Request Visual Sync
                EventBus.Publish(new GameEvents.RequestImpactSync
                {
                    Actor = action.Actor,
                    Move = action.ChosenMove,
                    Targets = targetsForThisHit,
                    GrazeStatus = grazeStatus,
                    DefaultTimeToImpact = 0.25f // Fallback timing
                });
            }
            else
            {
                AppendToCurrentLine(" FAILED!");
                EventBus.Publish(new GameEvents.MoveFailed { Actor = action.Actor });
            }
        }

        private void ProcessMoveActionPostImpact(QueuedAction action)
        {
            if (_currentActionDamageResults != null) foreach (var res in _currentActionDamageResults) if (res.WasCritical) _multiHitCrits++;
            _multiHitTotalExecuted++;
            _multiHitRemaining--;

            if (_multiHitRemaining > 0)
            {
                if (_currentActionFinalTargets != null && _currentActionFinalTargets.All(t => t.IsDefeated || t.Stats.CurrentHP <= 0)) _multiHitRemaining = 0;
                else { PrepareHit(action); return; }
            }

            if (_currentActionFinalTargets != null && _currentActionFinalTargets.All(t => t.IsDefeated || t.Stats.CurrentHP <= 0))
            {
                AppendToCurrentLine(" [cDefeat]DEFEATED![/]");
            }

            var actor = action.Actor;
            if (!actor.HasUsedFirstAttack) actor.HasUsedFirstAttack = true;

            bool isMultiHit = action.ChosenMove.Abilities.OfType<MultiHitAbility>().Any() || action.ChosenMove.Effects.ContainsKey("MultiHit");

            if (isMultiHit)
            {
                EventBus.Publish(new GameEvents.MultiHitActionCompleted { Actor = action.Actor, ChosenMove = action.ChosenMove, HitCount = _multiHitTotalExecuted, CriticalHitCount = _multiHitCrits });
            }

            _currentActionForEffects = null;
            _currentActionDamageResults = null;
            _currentActionFinalTargets = null;
        }

        private List<BattleCombatant> ResolveTargets(QueuedAction action)
        {
            var targetType = action.ChosenMove?.Target ?? TargetType.None;
            var actor = action.Actor;
            var specifiedTarget = action.Target;
            var validCandidates = TargetingHelper.GetValidTargets(actor, targetType, _allCombatants);

            // Filter out dead candidates immediately
            validCandidates = validCandidates.Where(c => !c.IsDefeated && c.Stats.CurrentHP > 0).ToList();

            if (targetType == TargetType.RandomBoth || targetType == TargetType.RandomEvery || targetType == TargetType.RandomAll)
            {
                if (validCandidates.Any()) return new List<BattleCombatant> { validCandidates[_random.Next(validCandidates.Count)] };
                return new List<BattleCombatant>();
            }

            if (targetType == TargetType.All || targetType == TargetType.Both || targetType == TargetType.Every || targetType == TargetType.Team || targetType == TargetType.Ally) return validCandidates;

            if (specifiedTarget != null)
            {
                if (validCandidates.Contains(specifiedTarget)) return new List<BattleCombatant> { specifiedTarget };
                bool wasHostile = actor.IsPlayerControlled != specifiedTarget.IsPlayerControlled;
                if (wasHostile)
                {
                    var alternativeEnemies = validCandidates.Where(c => c.IsPlayerControlled != actor.IsPlayerControlled).ToList();
                    if (alternativeEnemies.Any()) return new List<BattleCombatant> { alternativeEnemies.First() };
                }
                else
                {
                    var alternativeAllies = validCandidates.Where(c => c.IsPlayerControlled == actor.IsPlayerControlled).ToList();
                    if (alternativeAllies.Any()) return new List<BattleCombatant> { alternativeAllies.First() };
                }
                return new List<BattleCombatant>();
            }

            if (validCandidates.Any()) return new List<BattleCombatant> { validCandidates[0] };
            return new List<BattleCombatant>();
        }

        private void HandleEndOfRound()
        {
            // 1. Wait for Visuals
            if (_animationManager.IsBlockingAnimation) return;

            switch (_endOfRoundStage)
            {
                case EndOfRoundStage.VisualWait:
                    _endOfRoundStage = EndOfRoundStage.ProcessEffects;
                    break;

                case EndOfRoundStage.ProcessEffects:
                    ProcessEndOfRoundEffects();
                    _endOfRoundStage = EndOfRoundStage.CheckDeaths;
                    break;

                case EndOfRoundStage.CheckDeaths:
                    ProcessDeaths();
                    _endOfRoundStage = EndOfRoundStage.CheckWinLoss;
                    break;

                case EndOfRoundStage.CheckWinLoss:
                    if (CheckWinLoss()) return; // Battle Over
                    _endOfRoundStage = EndOfRoundStage.ProcessReinforcements;
                    _reinforcementSlotIndex = 0;
                    _reinforcementAnnounced = false;
                    break;

                case EndOfRoundStage.ProcessReinforcements:
                    HandleReinforcementsLogic();
                    break;

                case EndOfRoundStage.PrepareNextRound:
                    RoundNumber++;
                    _currentPhase = BattlePhase.StartOfRound;
                    _endOfRoundStage = EndOfRoundStage.VisualWait;
                    break;
            }
        }

        private void ProcessEndOfRoundEffects()
        {
            foreach (var combatant in _cachedAllActive)
            {
                _battleContext.ResetMultipliers();
                _battleContext.Actor = combatant;
                _battleContext.Target = null;
                _battleContext.Move = null;

                var turnEnd = new TurnEndEvent(combatant);
                combatant.NotifyAbilities(turnEnd, _battleContext);

                if (!combatant.UsedProtectThisTurn) combatant.ConsecutiveProtectUses = 0;
                combatant.UsedProtectThisTurn = false;

                combatant.Tags.Remove(GameplayTags.States.Dazed);
                combatant.Tags.Remove(GameplayTags.States.Stunned);

                var effectsToRemove = new List<StatusEffectInstance>();
                foreach (var effect in combatant.ActiveStatusEffects)
                {
                    if (!effect.IsPermanent && effect.EffectType != StatusEffectType.Stun)
                    {
                        effect.DurationInTurns--;
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
        }

        private void ProcessDeaths()
        {
            // Process ALL dead combatants in one pass
            var deadCombatants = _allCombatants.Where(c =>
                c.IsActiveOnField &&
                (c.IsDefeated || c.Stats.CurrentHP <= 0) &&
                !c.IsRemovalProcessed).ToList();

            foreach (var deadCombatant in deadCombatants)
            {
                RecordDefeatedName(deadCombatant);
                deadCombatant.Stats.CurrentHP = 0;
                deadCombatant.IsDying = true;
                deadCombatant.IsRemovalProcessed = true;
                deadCombatant.BattleSlot = -1;

                // Remove any pending actions for this dead unit
                _actionQueue.RemoveAll(a => a.Actor == deadCombatant);

                EventBus.Publish(new GameEvents.CombatantDefeated { DefeatedCombatant = deadCombatant });
            }

            if (deadCombatants.Any())
            {
                RefreshCombatantCaches();
            }
        }

        private bool CheckWinLoss()
        {
            if (_playerParty.All(c => c.IsDefeated)) { _currentPhase = BattlePhase.BattleOver; _actionQueue.Clear(); return true; }
            if (_enemyParty.All(c => c.IsDefeated)) { _currentPhase = BattlePhase.BattleOver; _actionQueue.Clear(); return true; }
            return false;
        }

        private void HandleReinforcementsLogic()
        {
            while (_reinforcementSlotIndex < 4)
            {
                int i = _reinforcementSlotIndex;
                bool isPlayerSlot = i >= 2;
                int slot = i % 2;

                var activeList = isPlayerSlot ? _cachedActivePlayers : _cachedActiveEnemies;
                var partyList = isPlayerSlot ? _playerParty : _enemyParty;

                bool isSlotOccupied = activeList.Any(c => c.BattleSlot == slot);

                if (!isSlotOccupied)
                {
                    var reinforcement = partyList.FirstOrDefault(c => c.BattleSlot >= 2 && !c.IsDefeated);

                    if (reinforcement != null)
                    {
                        if (!_reinforcementAnnounced)
                        {
                            if (!isPlayerSlot) EventBus.Publish(new GameEvents.NextEnemyApproaches());
                            else EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "An ally approaches!" });

                            _reinforcementAnnounced = true;
                            CanAdvance = false;
                            return; // Wait for animation/message
                        }

                        if (isPlayerSlot)
                        {
                            if (!_waitingForReinforcementSelection)
                            {
                                EventBus.Publish(new GameEvents.ForcedSwitchRequested { Actor = null, SlotIndex = slot });
                                _waitingForReinforcementSelection = true;
                                CanAdvance = false;
                            }
                            return; // Wait for player input
                        }
                        else
                        {
                            reinforcement.BattleSlot = slot;
                            RefreshCombatantCaches();

                            string key = $"Enemy_{slot}";
                            string msg = $"{reinforcement.Name} enters the battle!";
                            if (_lastDefeatedNames.TryGetValue(key, out string deadName))
                            {
                                msg = $"{reinforcement.Name} takes {deadName}'s place!";
                                _lastDefeatedNames.Remove(key);
                            }

                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
                            EventBus.Publish(new GameEvents.CombatantSpawned { Combatant = reinforcement });

                            var entryEvent = new GameEvents.CombatantEnteredEvent(reinforcement);
                            _battleContext.ResetMultipliers();
                            _battleContext.Actor = reinforcement;
                            reinforcement.NotifyAbilities(entryEvent, _battleContext);

                            _reinforcementAnnounced = false;

                            if (_startupEffectQueue.Any())
                            {
                                _currentPhase = BattlePhase.BattleStartEffects;
                                _startupEffectTimer = STARTUP_EFFECT_DELAY;
                            }

                            return;
                        }
                    }
                }

                _reinforcementSlotIndex++;
            }

            // All slots checked
            _endOfRoundStage = EndOfRoundStage.PrepareNextRound;
        }

        public (int Min, int Max) GetProjectedDamageRange(BattleCombatant actor, BattleCombatant target, MoveData move)
        {
            if (move.Power == 0) return (0, 0);

            float modifier = 1.0f;
            bool isMulti = move.Target == TargetType.All || move.Target == TargetType.Both || move.Target == TargetType.Every || move.Target == TargetType.Team;
            if (isMulti) modifier = BattleConstants.MULTI_TARGET_MODIFIER;

            var tempContext = new BattleContext
            {
                Actor = actor,
                Target = target,
                Move = move,
                IsSimulation = true
            };

            var dummyAction = new QueuedAction
            {
                Actor = actor,
                ChosenMove = move,
                Target = target,
                Type = QueuedActionType.Move
            };
            tempContext.Action = dummyAction;

            tempContext.SimulationVariance = VarianceMode.Min;
            var minResult = DamageCalculator.CalculateDamage(dummyAction, target, move, modifier, false, true, tempContext);

            tempContext.SimulationVariance = VarianceMode.Max;
            var maxResult = DamageCalculator.CalculateDamage(dummyAction, target, move, modifier, false, true, tempContext);

            return (minResult.DamageAmount, maxResult.DamageAmount);
        }
    }
}

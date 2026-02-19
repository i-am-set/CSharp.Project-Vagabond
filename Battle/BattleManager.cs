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
            StartOfTurn,
            ActionSelection,
            ActionResolution,
            CheckForDefeat, // Kept for explicit state transitions if needed, though logic is mostly integrated now
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
        private readonly Dictionary<int, QueuedAction> _pendingPlayerActions = new Dictionary<int, QueuedAction>();

        public IReadOnlyList<QueuedAction> ActionQueue => _actionQueue;

        private QueuedAction? _currentActionForEffects;
        private List<DamageCalculator.DamageResult> _currentActionDamageResults;
        private List<BattleCombatant> _currentActionFinalTargets;
        private BattlePhase _currentPhase;
        public int RoundNumber { get; private set; }
        private static readonly Random _random = new Random();

        private QueuedAction? _actionToExecute;
        private bool _endOfTurnEffectsProcessed;

        // --- PACING STATE ---
        private float _turnPacingTimer = 0f;
        private const float ACTION_DELAY = 1.0f;

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
            _endOfTurnEffectsProcessed = false;

            _animationManager = animationManager;
            _global = ServiceLocator.Get<Global>();

            _battleContext = new BattleContext();

            EventBus.Subscribe<GameEvents.SecondaryEffectComplete>(OnSecondaryEffectComplete);
            EventBus.Subscribe<GameEvents.DisengageTriggered>(OnDisengageTriggered);
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
                _currentPhase = BattlePhase.StartOfTurn;
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
            if (_currentPhase == BattlePhase.CheckForDefeat) HandleCheckForDefeat();
            else if (_currentPhase == BattlePhase.EndOfTurn) HandleEndOfTurn();
            else if (_currentPhase == BattlePhase.Reinforcement) HandleReinforcements();
            CanAdvance = true;
        }

        public void ForceAdvance()
        {
            _actionToExecute = null;
            _activeInteraction = null;

            if (_currentPhase == BattlePhase.BattleStartIntro) _currentPhase = BattlePhase.StartOfTurn;
            else if (_currentPhase == BattlePhase.ActionResolution || _currentPhase == BattlePhase.ProcessingInteraction || _currentPhase == BattlePhase.WaitingForSwitchCompletion)
            {
                if (!IsProcessingMultiHit) _currentPhase = BattlePhase.CheckForDefeat;
            }
            else if (_currentPhase == BattlePhase.CheckForDefeat || _currentPhase == BattlePhase.EndOfTurn) { RoundNumber++; _currentPhase = BattlePhase.StartOfTurn; }
            else if (_currentPhase == BattlePhase.Reinforcement) { RoundNumber++; _currentPhase = BattlePhase.StartOfTurn; }
            CanAdvance = true;
        }

        private void OnSecondaryEffectComplete(GameEvents.SecondaryEffectComplete e) { }

        private void OnDisengageTriggered(GameEvents.DisengageTriggered e)
        {
            CanAdvance = false;
            _currentPhase = BattlePhase.ProcessingInteraction;
            _activeInteraction = new SwitchInteraction(e.Actor, (result) =>
            {
                if (result is BattleCombatant target && target != e.Actor) InitiateSwitchSequence(e.Actor, target);
                else { _currentPhase = BattlePhase.CheckForDefeat; CanAdvance = true; }
                _activeInteraction = null;
            });
            _activeInteraction.Start(this);
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
                    _currentPhase = BattlePhase.Reinforcement;
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
            _currentPhase = BattlePhase.CheckForDefeat;
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
                if (player.ChargingAction == null && !player.Tags.Has(GameplayTags.States.Stunned))
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

        public void Update(float deltaTime)
        {
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
                case BattlePhase.StartOfTurn: HandleStartOfTurn(); break;
                case BattlePhase.ActionSelection: break;
                case BattlePhase.ActionResolution:
                    _turnPacingTimer -= deltaTime;
                    if (_turnPacingTimer <= 0)
                    {
                        ProcessTurnLogic();
                    }
                    break;
                case BattlePhase.CheckForDefeat: HandleCheckForDefeat(); break;
                case BattlePhase.EndOfTurn: HandleEndOfTurn(); break;
                case BattlePhase.Reinforcement: HandleReinforcements(); break;
                case BattlePhase.ProcessingInteraction: break;
                case BattlePhase.WaitingForSwitchCompletion: break;
            }
        }

        private void ProcessTurnLogic()
        {
            // 1. Clean up battlefield first
            bool battleOver = HandleCheckForDefeat();
            if (battleOver) return;

            // 2. Check if queue is empty
            if (_actionQueue.Count == 0)
            {
                _currentPhase = BattlePhase.EndOfTurn;
                return;
            }

            // 3. Dequeue next action
            var nextAction = _actionQueue[0];
            _actionQueue.RemoveAt(0);

            // 4. Validate Actor
            if (nextAction.Actor.IsDefeated || !nextAction.Actor.IsActiveOnField || nextAction.Actor.Stats.CurrentHP <= 0)
            {
                // Actor is dead/gone, skip immediately
                _turnPacingTimer = 0f;
                return;
            }

            // 5. Handle Status States (Stun/Dazed)
            if (nextAction.Actor.Tags.Has(GameplayTags.States.Stunned))
            {
                AppendToLog($"{nextAction.Actor.Name} IS STUNNED!");
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

            // 6. Validate Target (Retargeting Logic)
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

            // 7. Execute Action
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

            // 8. Reset Timer
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
                    _currentPhase = BattlePhase.StartOfTurn;
                    CanAdvance = true;
                }
            }
        }

        private void HandleStartOfTurn()
        {
            SanitizeBattlefield();
            if (!_cachedActiveEnemies.Any())
            {
                if (_enemyParty.All(c => c.IsDefeated)) { _currentPhase = BattlePhase.BattleOver; return; }
                else { _currentPhase = BattlePhase.Reinforcement; _reinforcementSlotIndex = 0; _reinforcementAnnounced = false; return; }
            }

            _roundLog.Clear();
            EventBus.Publish(new GameEvents.RoundLogUpdate { LogText = "" });
            _endOfTurnEffectsProcessed = false;
            _pendingPlayerActions.Clear();

            var startOfTurnActions = new List<QueuedAction>();
            foreach (var combatant in _cachedAllActive)
            {
                _battleContext.ResetMultipliers();
                _battleContext.Actor = combatant;
                _battleContext.Target = null;
                _battleContext.Move = null;

                var turnEvent = new TurnStartEvent(combatant);
                combatant.NotifyAbilities(turnEvent, _battleContext);

                if (turnEvent.IsHandled || combatant.Tags.Has(GameplayTags.States.Stunned))
                {
                    continue;
                }

                bool isCharging = combatant.ChargingAction != null;

                if (isCharging)
                {
                    combatant.ChargingAction.TurnsRemaining--;
                    if (combatant.ChargingAction.TurnsRemaining <= 0) { startOfTurnActions.Add(combatant.ChargingAction.Action); combatant.ChargingAction = null; }
                }
                if (combatant.DelayedActions.Any())
                {
                    var readyActions = new List<DelayedAction>();
                    foreach (var delayed in combatant.DelayedActions) { delayed.TurnsRemaining--; if (delayed.TurnsRemaining <= 0) readyActions.Add(delayed); }
                    foreach (var ready in readyActions) startOfTurnActions.Add(ready.Action);
                    var remaining = combatant.DelayedActions.Where(d => !readyActions.Contains(d)).ToList();
                    combatant.DelayedActions = new Queue<DelayedAction>(remaining);
                }

                if (!combatant.IsPlayerControlled && !isCharging)
                {
                    var aiAction = EnemyAI.DetermineBestAction(combatant, _allCombatants);
                    if (aiAction != null)
                    {
                        startOfTurnActions.Add(aiAction);
                    }
                }
            }
            _actionQueue.InsertRange(0, startOfTurnActions);

            bool anyPlayerCanAct = _cachedActivePlayers.Any(p => p.ChargingAction == null && !p.Tags.Has(GameplayTags.States.Stunned));

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
                    _actionQueue.RemoveAll(a => a.Actor == combatant);
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

                var significantTargetIds = new List<string>();
                var normalTargets = new List<BattleCombatant>();
                var protectedTargets = new List<BattleCombatant>();

                for (int i = 0; i < targetsForThisHit.Count; i++)
                {
                    var target = targetsForThisHit[i];
                    var result = damageResultsForThisHit[i];
                    _battleContext.Target = target;

                    if (result.WasProtected)
                    {
                        AppendToCurrentLine(" PROTECTED!");
                        result.DamageAmount = 0;
                        damageResultsForThisHit[i] = result;
                        protectedTargets.Add(target);
                        continue;
                    }
                    else
                    {
                        normalTargets.Add(target);
                    }

                    target.ApplyDamage(result.DamageAmount);

                    if (!result.WasGraze && result.DamageAmount > 0 && target.CurrentTenacity > 0)
                    {
                        target.CurrentTenacity--;
                        EventBus.Publish(new GameEvents.TenacityChanged { Combatant = target, NewValue = target.CurrentTenacity });

                        if (target.CurrentTenacity == 0)
                        {
                            EventBus.Publish(new GameEvents.TenacityBroken { Combatant = target });
                        }
                    }

                    if (result.DamageAmount > 0 && result.DamageAmount >= (target.Stats.MaxHP * 0.50f)) significantTargetIds.Add(target.CombatantID);
                    if (result.WasCritical) AppendToCurrentLine(" [cCrit]CRITICAL HIT![/]");
                    if (result.WasVulnerable) AppendToCurrentLine(" [cVulnerable]VULNERABLE![/]");

                    var reactionEvt = new ReactionEvent(action.Actor, target, action, result);
                    action.Actor.NotifyAbilities(reactionEvt, _battleContext);
                    target.NotifyAbilities(reactionEvt, _battleContext);
                    foreach (var ab in action.ChosenMove.Abilities) ab.OnEvent(reactionEvt, _battleContext);

                    SecondaryEffectSystem.ProcessPrimaryEffects(action, target);
                }

                EventBus.Publish(new GameEvents.BattleActionExecuted { Actor = action.Actor, ChosenMove = action.ChosenMove, Targets = targetsForThisHit, DamageResults = damageResultsForThisHit });

                if (significantTargetIds.Any())
                {
                    Color flashColor = action.Actor.IsPlayerControlled ? Color.White : _global.Palette_Rust;
                    _animationManager.TriggerImpactFlash(flashColor, 0.15f, significantTargetIds);
                }
                foreach (var target in targetsForThisHit)
                {
                    if (target.IsPlayerControlled)
                    {
                        int dmg = damageResultsForThisHit[targetsForThisHit.IndexOf(target)].DamageAmount;
                        if (dmg > 0)
                        {
                            float damageRatio = (float)dmg / target.Stats.MaxHP;
                            float intensity = Math.Min(1.0f + (damageRatio * 5.0f), 5.0f);
                            ServiceLocator.Get<HapticsManager>().TriggerImpactTwist(intensity, 0.25f);
                        }
                        break;
                    }
                }

                float timeToImpact = 0.15f;
                AnimationDefinition animDef = null;
                if (!string.IsNullOrEmpty(action.ChosenMove.AnimationId) && BattleDataCache.Animations.TryGetValue(action.ChosenMove.AnimationId, out animDef))
                {
                    float frameDuration = 1f / Math.Max(1f, animDef.FPS);
                    timeToImpact = animDef.ImpactFrameIndex * frameDuration;
                }
                if (timeToImpact < 0.25f) timeToImpact = 0.25f;
                _animationManager.ReleaseAttackCharge(action.Actor.CombatantID, timeToImpact);

                if (normalTargets.Any())
                {
                    EventBus.Publish(new GameEvents.PlayMoveAnimation { Move = action.ChosenMove, Targets = normalTargets, GrazeStatus = grazeStatus });
                }

                if (protectedTargets.Any())
                {
                    var protectMove = action.ChosenMove.Clone();
                    protectMove.AnimationId = "basic_protect";
                    protectMove.IsAnimationCentralized = false;
                    EventBus.Publish(new GameEvents.PlayMoveAnimation { Move = protectMove, Targets = protectedTargets, GrazeStatus = grazeStatus });
                }

                _currentActionForEffects = action;
                _currentActionDamageResults = damageResultsForThisHit;
                _currentActionFinalTargets = targetsForThisHit;

                ProcessMoveActionPostImpact(action);
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

            if (_multiHitTotalExecuted > 1) EventBus.Publish(new GameEvents.MultiHitActionCompleted { Actor = action.Actor, ChosenMove = action.ChosenMove, HitCount = _multiHitTotalExecuted, CriticalHitCount = _multiHitCrits });

            SecondaryEffectSystem.ProcessSecondaryEffects(action, _currentActionFinalTargets, _currentActionDamageResults);
            _currentActionForEffects = null;
            _currentActionDamageResults = null;
            _currentActionFinalTargets = null;
        }

        public void DrawStatChanges(SpriteBatch spriteBatch, BattleCombatant combatant, float startX, float startY, bool isRightAligned)
        {
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

        private bool HandleCheckForDefeat()
        {
            // 1. Identify dead combatants
            var deadCombatants = _allCombatants.Where(c => c.IsActiveOnField && (c.IsDefeated || c.Stats.CurrentHP <= 0) && !c.IsRemovalProcessed).ToList();

            foreach (var combatant in deadCombatants)
            {
                RecordDefeatedName(combatant);
                combatant.Stats.CurrentHP = 0;
                combatant.IsDying = true; // Visual flag for scene
                combatant.IsRemovalProcessed = true;
                combatant.BattleSlot = -1;
                _actionQueue.RemoveAll(a => a.Actor == combatant);
                EventBus.Publish(new GameEvents.CombatantDefeated { DefeatedCombatant = combatant });
            }

            if (deadCombatants.Any()) RefreshCombatantCaches();

            // 2. Check Game Over / Victory
            if (_playerParty.All(c => c.IsDefeated)) { _currentPhase = BattlePhase.BattleOver; _actionQueue.Clear(); return true; }
            if (_enemyParty.All(c => c.IsDefeated)) { _currentPhase = BattlePhase.BattleOver; _actionQueue.Clear(); return true; }

            return false;
        }

        private void HandleEndOfTurn()
        {
            _endOfTurnEffectsProcessed = true;

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

            // Check for defeat again after end of turn effects
            if (HandleCheckForDefeat()) return;

            // Proceed to reinforcements
            _currentPhase = BattlePhase.Reinforcement;
            _reinforcementSlotIndex = 0;
            _reinforcementAnnounced = false;
        }

        private void HandleReinforcements()
        {
            for (int i = 0; i < 4; i++)
            {
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
                            return;
                        }

                        if (isPlayerSlot)
                        {
                            EventBus.Publish(new GameEvents.ForcedSwitchRequested { Actor = null });
                            _currentPhase = BattlePhase.ProcessingInteraction;
                            _waitingForReinforcementSelection = true;
                            CanAdvance = false;
                            return;
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

                            CanAdvance = false;
                            return;
                        }
                    }
                }
            }

            _reinforcementAnnounced = false;
            RoundNumber++;
            _currentPhase = BattlePhase.StartOfTurn;
            CanAdvance = true;
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
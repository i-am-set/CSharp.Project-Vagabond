using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
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
            BattleStartIntro,
            StartOfTurn,
            ActionSelection,
            ActionResolution,
            PreActionAnimation,
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
        private readonly Dictionary<int, QueuedAction> _pendingPlayerActions = new Dictionary<int, QueuedAction>();

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

        public BattleCombatant? CurrentActingCombatant { get; private set; }

        private int _reinforcementSlotIndex = 0;
        private bool _reinforcementAnnounced = false;
        private bool _waitingForReinforcementSelection = false;

        private readonly Dictionary<string, string> _lastDefeatedNames = new Dictionary<string, string>();

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
        private readonly StringBuilder _roundLog = new StringBuilder();

        // Context for Ability System
        private readonly BattleContext _battleContext;

        // Watchdog for animation softlocks
        private float _phaseWatchdogTimer = 0f;

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

            // Initialize reusable context
            _battleContext = new BattleContext();

            EventBus.Subscribe<GameEvents.SecondaryEffectComplete>(OnSecondaryEffectComplete);
            EventBus.Subscribe<GameEvents.MoveAnimationCompleted>(OnMoveAnimationCompleted);
            EventBus.Subscribe<GameEvents.MoveImpactOccurred>(OnMoveImpactOccurred);
            EventBus.Subscribe<GameEvents.DisengageTriggered>(OnDisengageTriggered);

            // Notify Battle Start
            var battleStartEvent = new BattleStartedEvent(_allCombatants);

            // Configure context for Battle Start (Global event)
            _battleContext.ResetMultipliers();

            foreach (var combatant in _cachedAllActive)
            {
                _battleContext.Actor = combatant;
                combatant.NotifyAbilities(battleStartEvent, _battleContext);
            }
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
            if (_currentPhase == BattlePhase.SecondaryEffectResolution) _currentPhase = BattlePhase.CheckForDefeat;
            else if (_currentPhase == BattlePhase.CheckForDefeat) HandleCheckForDefeat();
            else if (_currentPhase == BattlePhase.EndOfTurn) HandleEndOfTurn();
            else if (_currentPhase == BattlePhase.Reinforcement) HandleReinforcements();
            else if (_currentPhase == BattlePhase.ActionResolution) HandleActionResolution();
            CanAdvance = true;
        }

        public void ForceAdvance()
        {
            if (_currentPhase == BattlePhase.AnimatingMove && _pendingImpact != null)
            {
                ApplyPendingImpact();
                if (_actionPendingAnimation != null) ProcessMoveActionPostImpact(_actionPendingAnimation);
            }

            _actionToExecute = null;
            _actionPendingAnimation = null;
            _pendingImpact = null;
            _activeInteraction = null;

            if (_currentPhase == BattlePhase.BattleStartIntro) _currentPhase = BattlePhase.StartOfTurn;
            else if (_currentPhase == BattlePhase.AnimatingMove || _currentPhase == BattlePhase.ActionResolution || _currentPhase == BattlePhase.SecondaryEffectResolution || _currentPhase == BattlePhase.ProcessingInteraction || _currentPhase == BattlePhase.WaitingForSwitchCompletion || _currentPhase == BattlePhase.PreActionAnimation)
            {
                if (!IsProcessingMultiHit) _currentPhase = BattlePhase.CheckForDefeat;
            }
            else if (_currentPhase == BattlePhase.CheckForDefeat || _currentPhase == BattlePhase.EndOfTurn) { RoundNumber++; _currentPhase = BattlePhase.StartOfTurn; }
            else if (_currentPhase == BattlePhase.Reinforcement) { RoundNumber++; _currentPhase = BattlePhase.StartOfTurn; }
            CanAdvance = true;
        }

        private void OnSecondaryEffectComplete(GameEvents.SecondaryEffectComplete e) { }

        private void OnMoveAnimationCompleted(GameEvents.MoveAnimationCompleted e)
        {
            if (_currentPhase == BattlePhase.AnimatingMove && _actionPendingAnimation != null)
            {
                var action = _actionPendingAnimation;
                if (_pendingImpact != null) ApplyPendingImpact();
                _actionPendingAnimation = null;
                ProcessMoveActionPostImpact(action);
            }
        }

        private void OnMoveImpactOccurred(GameEvents.MoveImpactOccurred e)
        {
            if (_pendingImpact != null) ApplyPendingImpact();
        }

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
                // Check if player can act (not charging, not stunned/dazed via tags)
                if (player.ChargingAction == null && !player.Tags.Has(GameplayTags.States.Stunned) && !player.Tags.Has(GameplayTags.States.Dazed))
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

            foreach (var enemy in _cachedActiveEnemies)
            {
                if (enemy.ChargingAction != null || enemy.Tags.Has(GameplayTags.States.Stunned) || enemy.Tags.Has(GameplayTags.States.Dazed)) continue;
                var action = EnemyAI.DetermineBestAction(enemy, _allCombatants);
                _actionQueue.Add(action);
            }

            _actionQueue = _actionQueue.OrderByDescending(a => a.Priority).ThenByDescending(a => a.ActorAgility).ToList();
            var lastAction = _actionQueue.LastOrDefault(a => a.Type == QueuedActionType.Move);
            if (lastAction != null) lastAction.IsLastActionInRound = true;

            _currentPhase = BattlePhase.ActionResolution;
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
            // Watchdog Logic
            if (_currentPhase == BattlePhase.AnimatingMove)
            {
                _phaseWatchdogTimer += deltaTime;
                if (_phaseWatchdogTimer > 4.0f)
                {
                    Debug.WriteLine("[BattleManager] WATCHDOG TRIGGERED: Animation phase timed out. Forcing advance.");
                    OnMoveAnimationCompleted(new GameEvents.MoveAnimationCompleted());
                    _phaseWatchdogTimer = 0f;
                }
            }
            else
            {
                _phaseWatchdogTimer = 0f;
            }

            if (_currentPhase == BattlePhase.BattleOver) return;
            if (!CanAdvance && _currentPhase != BattlePhase.WaitingForSwitchCompletion && _currentPhase != BattlePhase.PreActionAnimation) return;

            switch (_currentPhase)
            {
                case BattlePhase.BattleStartIntro: break;
                case BattlePhase.StartOfTurn: HandleStartOfTurn(); break;
                case BattlePhase.ActionSelection: break;
                case BattlePhase.ActionResolution: HandleActionResolution(); break;
                case BattlePhase.PreActionAnimation: HandlePreActionAnimation(); break;
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
                // Configure Context
                _battleContext.ResetMultipliers();
                _battleContext.Actor = combatant;
                _battleContext.Target = null;
                _battleContext.Move = null;

                // Publish TurnStartEvent
                var turnEvent = new TurnStartEvent(combatant);
                combatant.NotifyAbilities(turnEvent, _battleContext);

                // Check if turn is skipped (e.g. StunLogicAbility sets IsHandled = true)
                if (turnEvent.IsHandled || combatant.Tags.Has(GameplayTags.States.Stunned))
                {
                    continue;
                }

                if (combatant.ChargingAction != null)
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
            }
            _actionQueue.InsertRange(0, startOfTurnActions);

            // Check if any players can act
            bool anyPlayerCanAct = _cachedActivePlayers.Any(p => p.ChargingAction == null && !p.Tags.Has(GameplayTags.States.Stunned) && !p.Tags.Has(GameplayTags.States.Dazed));

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

        private void HandleActionResolution()
        {
            if (_actionToExecute != null) return;
            if (!_actionQueue.Any()) { _currentPhase = BattlePhase.EndOfTurn; return; }

            var nextAction = _actionQueue[0];
            _actionQueue.RemoveAt(0);

            if (nextAction.Actor.IsDefeated || !nextAction.Actor.IsActiveOnField || nextAction.Actor.Stats.CurrentHP <= 0) { HandleActionResolution(); return; }

            if (nextAction.Type == QueuedActionType.Charging) { AppendToLog($"{nextAction.Actor.Name} IS CHARGING [cAction]{nextAction.ChosenMove.MoveName}[/]."); HandleActionResolution(); return; }

            // Check tags for Stun/Daze (Redundant check, but safe)
            if (nextAction.Actor.Tags.Has(GameplayTags.States.Stunned)) { AppendToLog($"{nextAction.Actor.Name} IS STUNNED!"); HandleActionResolution(); return; }
            if (nextAction.Actor.Tags.Has(GameplayTags.States.Dazed)) { AppendToLog($"{nextAction.Actor.Name} IS DAZED!"); HandleActionResolution(); return; }

            _actionToExecute = nextAction;
            CurrentActingCombatant = nextAction.Actor;

            string moveName = nextAction.ChosenMove?.MoveName ?? "ACTION";
            string typeColor = nextAction.ChosenMove?.MoveType == MoveType.Spell ? "[cAlt]" : "[cAction]";
            AppendToLog($"{nextAction.Actor.Name} USED {typeColor}{moveName}[/].");

            // Publish ActionDeclaredEvent
            if (_actionToExecute.ChosenMove != null)
            {
                // Configure Context
                _battleContext.ResetMultipliers();
                _battleContext.Actor = _actionToExecute.Actor;
                _battleContext.Target = _actionToExecute.Target;
                _battleContext.Move = _actionToExecute.ChosenMove;
                _battleContext.Action = _actionToExecute;

                var declEvent = new ActionDeclaredEvent(_actionToExecute.Actor, _actionToExecute.ChosenMove, _actionToExecute.Target);
                _actionToExecute.Actor.NotifyAbilities(declEvent, _battleContext);

                if (declEvent.IsHandled)
                {
                    // Action cancelled (e.g. Silence/Provoke)
                    _actionToExecute = null;
                    HandleActionResolution();
                    return;
                }
            }

            EventBus.Publish(new GameEvents.ActionDeclared { Actor = _actionToExecute.Actor, Move = _actionToExecute.ChosenMove, Target = _actionToExecute.Target, Type = _actionToExecute.Type });

            if (_actionToExecute.Type == QueuedActionType.Move && _actionToExecute.ChosenMove != null)
            {
                _currentPhase = BattlePhase.PreActionAnimation;
                _animationManager.StartAttackCharge(_actionToExecute.Actor.CombatantID, _actionToExecute.Actor.IsPlayerControlled);
                CanAdvance = false;
            }
            else ExecuteDeclaredAction();
        }

        private void HandlePreActionAnimation()
        {
            if (_actionToExecute == null) return;
            var chargeState = _animationManager.GetAttackChargeState(_actionToExecute.Actor.CombatantID);
            if (chargeState == null || chargeState.Timer >= chargeState.WindupDuration) ExecuteDeclaredAction();
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

        private void HandleSecondaryEffectResolution()
        {
            SecondaryEffectSystem.ProcessSecondaryEffects(_currentActionForEffects, _currentActionFinalTargets, _currentActionDamageResults);
            _currentActionForEffects = null;
            _currentActionDamageResults = null;
            _currentActionFinalTargets = null;
            CanAdvance = false;
        }

        private void ProcessMoveAction(QueuedAction action)
        {
            // --- Resource Cost Logic ---
            int finalCost = action.ChosenMove.ManaCost;
            if (action.ChosenMove.Tags.Has(GameplayTags.Effects.ManaDump))
            {
                finalCost = action.Actor.Stats.CurrentMana;
            }

            if (action.Actor.Stats.CurrentMana < finalCost)
            {
                AppendToCurrentLine(" NO MANA!");
                EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "not enough mana" });
                CanAdvance = false;
                _currentPhase = BattlePhase.CheckForDefeat;
                return;
            }

            if (action.Actor.IsPlayerControlled && action.SpellbookEntry != null) action.SpellbookEntry.TimesUsed++;
            action.Actor.PendingDisengage = false;

            var multiHit = action.ChosenMove.Abilities.OfType<MultiHitAbility>().FirstOrDefault();
            if (multiHit != null) { int hits = _random.Next(multiHit.MinHits, multiHit.MaxHits + 1); _multiHitTotalExecuted = 0; _multiHitRemaining = hits; _multiHitCrits = 0; }
            else { _multiHitTotalExecuted = 0; _multiHitRemaining = 1; _multiHitCrits = 0; }

            // Execute Hit Logic
            PrepareHit(action);

            // Pay Cost
            if (finalCost > 0)
            {
                float manaBefore = action.Actor.Stats.CurrentMana;
                action.Actor.Stats.CurrentMana = Math.Max(0, action.Actor.Stats.CurrentMana - finalCost);
                float manaAfter = action.Actor.Stats.CurrentMana;
                if (manaBefore != manaAfter) EventBus.Publish(new GameEvents.CombatantManaConsumed { Actor = action.Actor, ManaBefore = manaBefore, ManaAfter = manaAfter });
            }
        }

        private void PrepareHit(QueuedAction action)
        {
            var targetsForThisHit = ResolveTargets(action);
            if (targetsForThisHit.Any())
            {
                var damageResultsForThisHit = new List<DamageCalculator.DamageResult>();
                float multiTargetModifier = (targetsForThisHit.Count > 1) ? BattleConstants.MULTI_TARGET_MODIFIER : 1.0f;
                var grazeStatus = new Dictionary<BattleCombatant, bool>();

                // Configure Context for Damage Calculation
                _battleContext.ResetMultipliers();
                _battleContext.Actor = action.Actor;
                _battleContext.Move = action.ChosenMove;
                _battleContext.Action = action;

                foreach (var target in targetsForThisHit)
                {
                    _battleContext.Target = target;
                    // Pass context to calculator
                    var result = DamageCalculator.CalculateDamage(action, target, action.ChosenMove, multiTargetModifier, null, false, _battleContext);
                    damageResultsForThisHit.Add(result);
                    grazeStatus[target] = result.WasGraze;
                }

                _pendingImpact = new PendingImpactData { Action = action, Targets = targetsForThisHit, Results = damageResultsForThisHit };

                float timeToImpact = 0.15f;
                if (!string.IsNullOrEmpty(action.ChosenMove.AnimationSpriteSheet))
                {
                    float frameDuration = (1f / 12f) / action.ChosenMove.AnimationSpeed;
                    timeToImpact = action.ChosenMove.DamageFrameIndex * frameDuration;
                }
                if (timeToImpact < 0.05f) timeToImpact = 0.05f;

                _animationManager.ReleaseAttackCharge(action.Actor.CombatantID, timeToImpact);

                var normalTargets = new List<BattleCombatant>();
                var protectedTargets = new List<BattleCombatant>();
                foreach (var target in targetsForThisHit)
                {
                    if (target.Tags.Has(GameplayTags.States.Protected)) protectedTargets.Add(target);
                    else normalTargets.Add(target);
                }

                bool animationTriggered = false;

                if (normalTargets.Any() && !string.IsNullOrEmpty(action.ChosenMove.AnimationSpriteSheet))
                {
                    // Set state BEFORE publishing, in case the animation finishes synchronously
                    _actionPendingAnimation = action;
                    _currentPhase = BattlePhase.AnimatingMove;
                    CanAdvance = false;
                    animationTriggered = true;
                    EventBus.Publish(new GameEvents.MoveAnimationTriggered { Move = action.ChosenMove, Targets = normalTargets, GrazeStatus = grazeStatus });
                }

                if (protectedTargets.Any())
                {
                    var protectMove = action.ChosenMove.Clone();
                    protectMove.AnimationSpriteSheet = "basic_protect";
                    protectMove.IsAnimationCentralized = false;
                    protectMove.AnimationSpeed = _global.ProtectAnimationSpeed;
                    protectMove.DamageFrameIndex = _global.ProtectDamageFrameIndex;

                    // Set state BEFORE publishing
                    if (_actionPendingAnimation == null)
                    {
                        _actionPendingAnimation = action;
                        _currentPhase = BattlePhase.AnimatingMove;
                        CanAdvance = false;
                    }
                    animationTriggered = true;
                    EventBus.Publish(new GameEvents.MoveAnimationTriggered { Move = protectMove, Targets = protectedTargets, GrazeStatus = grazeStatus });
                }

                // Only manually process impact if no animation was triggered (or if they all finished instantly and cleared the flag)
                // Note: If an animation finished instantly, OnMoveAnimationCompleted would have already called ProcessMoveActionPostImpact.
                // We check animationTriggered to ensure we don't double-call if the animation was attempted.
                if (!animationTriggered)
                {
                    Debug.WriteLine($"[BattleManager] INFO: No animation triggered for move '{action.ChosenMove.MoveName}'. Proceeding to impact immediately.");
                    ApplyPendingImpact();
                    ProcessMoveActionPostImpact(action);
                }
            }
            else
            {
                AppendToCurrentLine(" FAILED!");
                EventBus.Publish(new GameEvents.MoveFailed { Actor = action.Actor });
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

            // Configure Context
            _battleContext.ResetMultipliers();
            _battleContext.Actor = action.Actor;
            _battleContext.Move = action.ChosenMove;
            _battleContext.Action = action;

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
                if (!result.WasGraze && result.DamageAmount > 0 && target.CurrentTenacity > 0)
                {
                    target.CurrentTenacity--;
                    EventBus.Publish(new GameEvents.TenacityChanged { Combatant = target, NewValue = target.CurrentTenacity });
                    if (target.CurrentTenacity == 0) EventBus.Publish(new GameEvents.TenacityBroken { Combatant = target });
                }

                if (result.DamageAmount > 0 && result.DamageAmount >= (target.Stats.MaxHP * 0.50f)) significantTargetIds.Add(target.CombatantID);
                if (result.WasCritical) AppendToCurrentLine(" [cCrit]CRITICAL HIT![/]");
                if (result.WasVulnerable) AppendToCurrentLine(" [cVulnerable]VULNERABLE![/]");

                // Fire ReactionEvent for OnHit/OnDamaged logic
                var reactionEvt = new ReactionEvent(action.Actor, target, action, result);
                action.Actor.NotifyAbilities(reactionEvt, _battleContext);
                target.NotifyAbilities(reactionEvt, _battleContext);
                foreach (var ab in action.ChosenMove.Abilities) ab.OnEvent(reactionEvt, _battleContext);

                SecondaryEffectSystem.ProcessPrimaryEffects(action, target);
            }

            EventBus.Publish(new GameEvents.BattleActionExecuted { Actor = action.Actor, ChosenMove = action.ChosenMove, Targets = targets, DamageResults = results });
            if (significantTargetIds.Any())
            {
                Color flashColor = action.Actor.IsPlayerControlled ? Color.White : _global.Palette_Rust;
                _animationManager.TriggerImpactFlash(flashColor, 0.15f, significantTargetIds);
            }
            foreach (var target in targets)
            {
                if (target.IsPlayerControlled)
                {
                    float damageRatio = (float)results[targets.IndexOf(target)].DamageAmount / target.Stats.MaxHP;
                    float intensity = Math.Min(1.0f + (damageRatio * 5.0f), 5.0f);
                    ServiceLocator.Get<HapticsManager>().TriggerImpactTwist(intensity, 0.25f);
                    break;
                }
            }

            _currentActionForEffects = action;
            _currentActionDamageResults = results;
            _currentActionFinalTargets = targets;
            _pendingImpact = null;
        }

        private void ProcessMoveActionPostImpact(QueuedAction action)
        {
            if (_currentActionDamageResults != null) foreach (var res in _currentActionDamageResults) if (res.WasCritical) _multiHitCrits++;
            _multiHitTotalExecuted++;
            _multiHitRemaining--;

            if (_multiHitRemaining > 0)
            {
                if (_currentActionFinalTargets != null && _currentActionFinalTargets.All(t => t.IsDefeated)) _multiHitRemaining = 0;
                else { PrepareHit(action); return; }
            }

            if (_currentActionFinalTargets != null && _currentActionFinalTargets.All(t => t.IsDefeated))
            {
                AppendToCurrentLine(" [cDefeat]DEFEATED![/]");
            }

            var actor = action.Actor;
            if (!actor.HasUsedFirstAttack) actor.HasUsedFirstAttack = true;

            if (_multiHitTotalExecuted > 1) EventBus.Publish(new GameEvents.MultiHitActionCompleted { Actor = action.Actor, ChosenMove = action.ChosenMove, HitCount = _multiHitTotalExecuted, CriticalHitCount = _multiHitCrits });

            if (_currentPhase != BattlePhase.ProcessingInteraction && _currentPhase != BattlePhase.WaitingForSwitchCompletion)
            {
                _currentPhase = BattlePhase.SecondaryEffectResolution;
                CanAdvance = false;
            }
        }

        private List<BattleCombatant> ResolveTargets(QueuedAction action)
        {
            var targetType = action.ChosenMove?.Target ?? TargetType.None;
            var actor = action.Actor;
            var specifiedTarget = action.Target;
            var validCandidates = TargetingHelper.GetValidTargets(actor, targetType, _allCombatants);

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

        private void HandleCheckForDefeat()
        {
            // 1. Process finished animations (Only if animation is actually done)
            var finishedDying = _allCombatants.Where(c => c.IsDying && !_animationManager.IsDeathAnimating(c.CombatantID)).ToList();

            foreach (var combatant in finishedDying)
            {
                RecordDefeatedName(combatant);
                combatant.IsDying = false;
                combatant.IsRemovalProcessed = true;
                combatant.BattleSlot = -1;
                _actionQueue.RemoveAll(a => a.Actor == combatant);
            }
            if (finishedDying.Any()) RefreshCombatantCaches();

            // 2. Check for new deaths
            var newlyDefeated = _allCombatants.FirstOrDefault(c => c.IsDefeated && !c.IsDying && !c.IsRemovalProcessed);
            if (newlyDefeated != null)
            {
                newlyDefeated.IsDying = true;
                EventBus.Publish(new GameEvents.CombatantDefeated { DefeatedCombatant = newlyDefeated });
                CanAdvance = false;
                return;
            }

            // 3. Wait for current deaths to finish animating
            if (_allCombatants.Any(c => c.IsDying))
            {
                CanAdvance = false;
                return;
            }

            // 4. Check Game Over / Victory
            if (_playerParty.All(c => c.IsDefeated)) { _currentPhase = BattlePhase.BattleOver; _actionQueue.Clear(); return; }
            if (_enemyParty.All(c => c.IsDefeated)) { _currentPhase = BattlePhase.BattleOver; _actionQueue.Clear(); return; }

            // 5. Proceed
            if (_actionQueue.Any() || _actionToExecute != null) _currentPhase = BattlePhase.ActionResolution;
            else if (!_endOfTurnEffectsProcessed) _currentPhase = BattlePhase.EndOfTurn;
            else { _currentPhase = BattlePhase.Reinforcement; _reinforcementSlotIndex = 0; _reinforcementAnnounced = false; }
        }

        private void HandleEndOfTurn()
        {
            _endOfTurnEffectsProcessed = true;

            foreach (var combatant in _allCombatants)
            {
                if (!combatant.IsDefeated && combatant.Stats.CurrentMana < combatant.Stats.MaxMana)
                {
                    float manaBefore = combatant.Stats.CurrentMana;
                    combatant.Stats.CurrentMana++;
                    float manaAfter = combatant.Stats.CurrentMana;
                    EventBus.Publish(new GameEvents.CombatantManaRestored { Target = combatant, AmountRestored = 1, ManaBefore = manaBefore, ManaAfter = manaAfter });
                }
            }

            foreach (var combatant in _cachedAllActive)
            {
                // Configure Context
                _battleContext.ResetMultipliers();
                _battleContext.Actor = combatant;
                _battleContext.Target = null;
                _battleContext.Move = null;

                // Publish TurnEndEvent
                var turnEnd = new TurnEndEvent(combatant);
                combatant.NotifyAbilities(turnEnd, _battleContext);

                if (!combatant.UsedProtectThisTurn) combatant.ConsecutiveProtectUses = 0;
                combatant.UsedProtectThisTurn = false;

                // Clear turn-based tags
                combatant.Tags.Remove(GameplayTags.States.Dazed);
                combatant.Tags.Remove(GameplayTags.States.Stunned);

                var effectsToRemove = new List<StatusEffectInstance>();
                foreach (var effect in combatant.ActiveStatusEffects)
                {
                    if (!effect.IsPermanent)
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
            _currentPhase = BattlePhase.CheckForDefeat;
        }

        private void HandleReinforcements()
        {
            if (_reinforcementSlotIndex > 3) { RoundNumber++; _currentPhase = BattlePhase.StartOfTurn; return; }
            bool isPlayerSlot = _reinforcementSlotIndex >= 2;
            int slot = _reinforcementSlotIndex % 2;
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
                    else
                    {
                        if (isPlayerSlot)
                        {
                            EventBus.Publish(new GameEvents.ForcedSwitchRequested { Actor = null });
                            _currentPhase = BattlePhase.ProcessingInteraction;
                            _waitingForReinforcementSelection = true;
                            CanAdvance = false;
                            return;
                        }
                        reinforcement.BattleSlot = slot;
                        RefreshCombatantCaches();
                        string key = $"{(isPlayerSlot ? "Player" : "Enemy")}_{slot}";
                        string msg = $"{reinforcement.Name} enters the battle!";
                        if (_lastDefeatedNames.TryGetValue(key, out string deadName)) { msg = $"{reinforcement.Name} takes {deadName}'s place!"; _lastDefeatedNames.Remove(key); }
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
                        EventBus.Publish(new GameEvents.CombatantSpawned { Combatant = reinforcement });
                        _reinforcementAnnounced = false;
                        _reinforcementSlotIndex++;
                        CanAdvance = false;
                        return;
                    }
                }
            }
            _reinforcementSlotIndex++;
            _reinforcementAnnounced = false;
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

            // Calculate Minimum
            tempContext.SimulationVariance = VarianceMode.Min;
            var minResult = DamageCalculator.CalculateDamage(dummyAction, target, move, modifier, false, true, tempContext);

            // Calculate Maximum
            tempContext.SimulationVariance = VarianceMode.Max;
            var maxResult = DamageCalculator.CalculateDamage(dummyAction, target, move, modifier, false, true, tempContext);

            return (minResult.DamageAmount, maxResult.DamageAmount);
        }
    }
}

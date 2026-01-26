using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
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
using ProjectVagabond.Systems;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
            EventBus.Subscribe<GameEvents.AbilityActivated>(OnAbilityActivated);

            foreach (var combatant in _cachedAllActive)
            {
                var ctx = new CombatTriggerContext { Actor = combatant };
                combatant.NotifyAbilities(CombatEventType.BattleStart, ctx);
                combatant.NotifyAbilities(CombatEventType.CombatantEnter, ctx);
            }
        }

        private void OnAbilityActivated(GameEvents.AbilityActivated e) { }

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
            else if (_currentPhase == BattlePhase.EndOfTurn) { RoundNumber++; _currentPhase = BattlePhase.StartOfTurn; }
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
                    HandleOnEnterAbilities(reinforcement);
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
            HandleOnEnterAbilities(incomingMember);
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
                // Exclude players who are charging, stunned, or dazed from the requirement count
                if (player.ChargingAction == null && !player.HasStatusEffect(StatusEffectType.Stun) && !player.IsDazed)
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
                if (enemy.ChargingAction != null || enemy.HasStatusEffect(StatusEffectType.Stun) || enemy.IsDazed) continue;
                var action = EnemyAI.DetermineBestAction(enemy, _allCombatants);
                if (!HandlePreActionEffects(action)) _actionQueue.Add(action);
            }

            _actionQueue = _actionQueue.OrderByDescending(a => a.Priority).ThenByDescending(a => a.ActorAgility).ToList();
            var lastAction = _actionQueue.LastOrDefault(a => a.Type == QueuedActionType.Move);
            if (lastAction != null) lastAction.IsLastActionInRound = true;

            _currentPhase = BattlePhase.ActionResolution;
        }

        private void AddActionToQueue(QueuedAction action)
        {
            if (HandlePreActionEffects(action)) return;
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
                var ctx = new CombatTriggerContext { Actor = combatant };
                combatant.NotifyAbilities(CombatEventType.TurnStart, ctx);
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

            // Check if any players can act (Not Charging, Not Stunned, Not Dazed)
            bool anyPlayerCanAct = _cachedActivePlayers.Any(p => p.ChargingAction == null && !p.HasStatusEffect(StatusEffectType.Stun) && !p.IsDazed);

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
            if (nextAction.Actor.HasStatusEffect(StatusEffectType.Stun)) { AppendToLog($"{nextAction.Actor.Name} IS STUNNED!"); HandleActionResolution(); return; }
            if (nextAction.Actor.IsDazed) { AppendToLog($"{nextAction.Actor.Name} IS DAZED!"); HandleActionResolution(); return; }

            _actionToExecute = nextAction;
            CurrentActingCombatant = nextAction.Actor;

            string moveName = nextAction.ChosenMove?.MoveName ?? "ACTION";
            string typeColor = nextAction.ChosenMove?.MoveType == MoveType.Spell ? "[cSpell]" : "[cAction]";
            AppendToLog($"{nextAction.Actor.Name} USED {typeColor}{moveName}[/].");

            EventBus.Publish(new GameEvents.ActionDeclared { Actor = _actionToExecute.Actor, Move = _actionToExecute.ChosenMove, Target = _actionToExecute.Target, Type = _actionToExecute.Type });

            var ctx = new CombatTriggerContext { Actor = _actionToExecute.Actor, Move = _actionToExecute.ChosenMove, Action = _actionToExecute };
            _actionToExecute.Actor.NotifyAbilities(CombatEventType.ActionDeclared, ctx);
            if (_actionToExecute.ChosenMove != null) foreach (var ab in _actionToExecute.ChosenMove.Abilities) ab.OnCombatEvent(CombatEventType.ActionDeclared, ctx);

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
            if (!ProcessPreResolutionEffects(action)) { CanAdvance = false; return; }
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
            if (action.ChosenMove.MoveType == MoveType.Spell && action.Actor.HasStatusEffect(StatusEffectType.Silence))
            {
                AppendToCurrentLine(" SILENCED!");
                EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "silenced" });
                CanAdvance = false;
                _currentPhase = BattlePhase.CheckForDefeat;
                return;
            }

            if (action.Actor.Stats.CurrentMana < action.ChosenMove.ManaCost)
            {
                AppendToCurrentLine(" NO MANA!");
                EventBus.Publish(new GameEvents.ActionFailed { Actor = action.Actor, Reason = "not enough mana" });
                CanAdvance = false;
                _currentPhase = BattlePhase.CheckForDefeat;
                return;
            }

            float manaBefore = action.Actor.Stats.CurrentMana;
            action.Actor.Stats.CurrentMana -= action.ChosenMove.ManaCost;
            float manaAfter = action.Actor.Stats.CurrentMana;
            if (manaBefore != manaAfter) EventBus.Publish(new GameEvents.CombatantManaConsumed { Actor = action.Actor, ManaBefore = manaBefore, ManaAfter = manaAfter });

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

                foreach (var target in targetsForThisHit)
                {
                    var result = DamageCalculator.CalculateDamage(action, target, action.ChosenMove, multiTargetModifier);
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
                    if (target.HasStatusEffect(StatusEffectType.Protected)) protectedTargets.Add(target);
                    else normalTargets.Add(target);
                }

                if (normalTargets.Any() && !string.IsNullOrEmpty(action.ChosenMove.AnimationSpriteSheet))
                {
                    EventBus.Publish(new GameEvents.MoveAnimationTriggered { Move = action.ChosenMove, Targets = normalTargets, GrazeStatus = grazeStatus });
                    _actionPendingAnimation = action;
                    _currentPhase = BattlePhase.AnimatingMove;
                    CanAdvance = false;
                }

                if (protectedTargets.Any())
                {
                    var protectMove = action.ChosenMove.Clone();
                    protectMove.AnimationSpriteSheet = "basic_protect";
                    protectMove.IsAnimationCentralized = false;
                    protectMove.AnimationSpeed = _global.ProtectAnimationSpeed;
                    protectMove.DamageFrameIndex = _global.ProtectDamageFrameIndex;
                    EventBus.Publish(new GameEvents.MoveAnimationTriggered { Move = protectMove, Targets = protectedTargets, GrazeStatus = grazeStatus });
                    if (_actionPendingAnimation == null) { _actionPendingAnimation = action; _currentPhase = BattlePhase.AnimatingMove; CanAdvance = false; }
                }

                if (_actionPendingAnimation == null) { ApplyPendingImpact(); ProcessMoveActionPostImpact(action); }
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

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var result = results[i];
                var shieldBreaker = action.ChosenMove.Abilities.OfType<ShieldBreakerAbility>().FirstOrDefault();
                bool isProtecting = target.HasStatusEffect(StatusEffectType.Protected);

                if (shieldBreaker != null)
                {
                    var type = shieldBreaker.GetType();
                    float breakMult = (float)type.GetField("_breakMult", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shieldBreaker);
                    bool failsIfNoProtect = (bool)type.GetField("_failsIfNoProtect", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shieldBreaker);

                    if (isProtecting)
                    {
                        target.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Protected);
                        EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = target, EffectType = StatusEffectType.Protected });
                        AppendToCurrentLine(" GUARD BROKEN!");
                        result.DamageAmount = (int)(result.DamageAmount * breakMult);
                        results[i] = result;
                        isProtecting = false;
                    }
                    else if (failsIfNoProtect)
                    {
                        result.DamageAmount = 0;
                        AppendToCurrentLine(" FAILED!");
                        EventBus.Publish(new GameEvents.MoveFailed { Actor = action.Actor });
                        results[i] = result;
                        continue;
                    }
                }

                if (isProtecting)
                {
                    AppendToCurrentLine(" PROTECTED!");
                    result.WasProtected = true;
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

                var ctx = new CombatTriggerContext { Actor = action.Actor, Target = target, Move = action.ChosenMove, FinalDamage = result.DamageAmount, IsCritical = result.WasCritical, IsGraze = result.WasGraze };
                action.Actor.NotifyAbilities(CombatEventType.OnHit, ctx);
                target.NotifyAbilities(CombatEventType.OnDamaged, ctx);
                foreach (var ab in action.ChosenMove.Abilities) ab.OnCombatEvent(CombatEventType.OnHit, ctx);

                if (ctx.AccumulatedLifestealPercent > 0 && result.DamageAmount > 0)
                {
                    int totalHeal = (int)(result.DamageAmount * (ctx.AccumulatedLifestealPercent / 100f));
                    if (totalHeal > 0)
                    {
                        var lifestealCtx = new CombatTriggerContext { Actor = action.Actor, Target = target, FinalDamage = totalHeal };
                        target.NotifyAbilities(CombatEventType.OnLifesteal, lifestealCtx);
                        if (!lifestealCtx.IsCancelled)
                        {
                            int hpBefore = (int)action.Actor.VisualHP;
                            action.Actor.ApplyHealing(totalHeal);
                            EventBus.Publish(new GameEvents.CombatantHealed { Actor = action.Actor, Target = action.Actor, HealAmount = totalHeal, VisualHPBefore = hpBefore });
                        }
                    }
                }
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
                var ctx = new CombatTriggerContext { Actor = action.Actor, Move = action.ChosenMove };
                action.Actor.NotifyAbilities(CombatEventType.OnKill, ctx);
                AppendToCurrentLine(" [cDefeat]DEFEATED![/]");
            }

            var actor = action.Actor;
            if (!actor.HasUsedFirstAttack) actor.HasUsedFirstAttack = true;
            var completeCtx = new CombatTriggerContext { Actor = actor, Action = action };
            actor.NotifyAbilities(CombatEventType.ActionComplete, completeCtx);
            foreach (var ab in action.ChosenMove.Abilities) ab.OnCombatEvent(CombatEventType.ActionComplete, completeCtx);

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
            var finishedDying = _allCombatants.Where(c => c.IsDying).ToList();
            foreach (var combatant in finishedDying)
            {
                RecordDefeatedName(combatant);
                combatant.IsDying = false;
                combatant.IsRemovalProcessed = true;
                combatant.BattleSlot = -1;
                _actionQueue.RemoveAll(a => a.Actor == combatant);
            }
            if (finishedDying.Any()) RefreshCombatantCaches();

            var newlyDefeated = _allCombatants.FirstOrDefault(c => c.IsDefeated && !c.IsDying && !c.IsRemovalProcessed);
            if (newlyDefeated != null)
            {
                newlyDefeated.IsDying = true;
                EventBus.Publish(new GameEvents.CombatantDefeated { DefeatedCombatant = newlyDefeated });
                CanAdvance = false;
                return;
            }

            if (_playerParty.All(c => c.IsDefeated)) { _currentPhase = BattlePhase.BattleOver; _actionQueue.Clear(); return; }
            if (_enemyParty.All(c => c.IsDefeated)) { _currentPhase = BattlePhase.BattleOver; _actionQueue.Clear(); return; }

            if (_actionQueue.Any() || _actionToExecute != null) _currentPhase = BattlePhase.ActionResolution;
            else if (!_endOfTurnEffectsProcessed) _currentPhase = BattlePhase.EndOfTurn;
            else { _currentPhase = BattlePhase.Reinforcement; _reinforcementSlotIndex = 0; _reinforcementAnnounced = false; }
        }

        private void HandleEndOfTurn()
        {
            _endOfTurnEffectsProcessed = true;
            foreach (var combatant in _cachedAllActive)
            {
                var ctx = new CombatTriggerContext { Actor = combatant };
                combatant.NotifyAbilities(CombatEventType.TurnEnd, ctx);
                if (!combatant.UsedProtectThisTurn) combatant.ConsecutiveProtectUses = 0;
                combatant.UsedProtectThisTurn = false;
                combatant.IsDazed = false;

                var effectsToRemove = new List<StatusEffectInstance>();
                foreach (var effect in combatant.ActiveStatusEffects)
                {
                    if (!effect.IsPermanent) effect.DurationInTurns--;
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
                    if (!effect.IsPermanent && effect.DurationInTurns <= 0) effectsToRemove.Add(effect);
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
                        HandleOnEnterAbilities(reinforcement);
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

        private void HandleOnEnterAbilities(BattleCombatant specificCombatant = null)
        {
            var targets = specificCombatant != null ? new List<BattleCombatant> { specificCombatant } : _cachedAllActive;
            foreach (var combatant in targets)
            {
                if (!combatant.IsActiveOnField) continue;
                var ctx = new CombatTriggerContext { Actor = combatant };
                combatant.NotifyAbilities(CombatEventType.CombatantEnter, ctx);
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
            var ctx = new CombatTriggerContext { Actor = actor, Action = action };
            actor.NotifyAbilities(CombatEventType.ActionDeclared, ctx);
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.ActionDeclared, ctx);
            return action;
        }

        private bool HandlePreActionEffects(QueuedAction action) => false;
        private bool ProcessPreResolutionEffects(QueuedAction action) => true;
        private MoveData HandlePreDamageEffects(MoveData originalMove, BattleCombatant target) => originalMove;
    }
}

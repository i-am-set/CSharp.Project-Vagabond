using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Items;
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

namespace ProjectVagabond.Scenes
{
    public class BattleScene : GameScene
    {
        private const float MULTI_HIT_DELAY = 0.25f;
        private const float ACTION_EXECUTION_DELAY = 0.5f;
        private const float FIXED_COIN_GROUND_Y = 115f;
        private const int ENEMY_SLOT_Y_OFFSET = 12;
        private const float BATTLE_ENTRY_INITIAL_DELAY = 0.0f;

        private BattleManager _battleManager;
        private BattleUIManager _uiManager;
        private BattleRenderer _renderer;
        private BattleAnimationManager _animationManager;
        private MoveAnimationManager _moveAnimationManager;
        private BattleInputHandler _inputHandler;
        private AlertManager _alertManager;
        private BattleLogManager _battleLogManager;
        private ImageButton _settingsButton;
        private ComponentStore _componentStore;
        private SceneManager _sceneManager;
        private SpriteManager _spriteManager;
        private HapticsManager _hapticsManager;
        private TooltipManager _tooltipManager;
        private GameState _gameState;
        private readonly Global _global;
        private HitstopManager _hitstopManager;
        private ParticleSystemManager _particleSystemManager;
        private readonly TransitionManager _transitionManager;
        private readonly ProgressionManager _progressionManager;

        private List<int> _enemyEntityIds = new List<int>();
        private BattleManager.BattlePhase _previousBattlePhase;
        private bool _isBattleOver;
        private float _endOfBattleTimer;
        private const float END_OF_BATTLE_DELAY = 2.0f;
        private BattleCombatant _currentActor;
        private bool _isWaitingForMultiHitDelay = false;
        private float _multiHitDelayTimer = 0f;
        private readonly Queue<Action> _pendingAnimations = new Queue<Action>();
        private bool _rewardScreenShown = false;
        private readonly Random _random = new Random();

        private bool _isWaitingForActionExecution = false;
        private float _actionExecutionTimer = 0f;

        private float _watchdogTimer = 0f;
        private const float WATCHDOG_TIMEOUT = 4.0f;
        private BattleManager.BattlePhase _lastFramePhase;
        private bool _wasUiBusyLastFrame;
        private bool _wasAnimatingLastFrame;
        private bool _isFadingOutOnDeath = false;
        private float _deathFadeTimer = 0f;
        private readonly HashSet<string> _processedDeathAnimations = new HashSet<string>();

        private enum SwitchSequenceState { None, AnimatingOut, Swapping, AnimatingIn }
        private SwitchSequenceState _switchSequenceState = SwitchSequenceState.None;
        private float _switchSequenceTimer = 0f;
        private BattleCombatant _switchOutgoing;
        private BattleCombatant _switchIncoming;

        private enum IntroPhase { EnemyDrop, PlayerRise }
        private IntroPhase _currentIntroPhase;
        private Queue<BattleCombatant> _introSequenceQueue = new Queue<BattleCombatant>();
        private float _introTimer = 0f;
        private const float INTRO_STAGGER_DELAY = 0.3f;
        private const float INTRO_SLIDE_DISTANCE = 150f;

        private float _uiSlideTimer = 0f;
        private const float UI_SLIDE_DURATION = 0.5f;
        private const float UI_SLIDE_DISTANCE = 100f;

        private enum SettingsButtonState { Hidden, AnimatingIn, Visible }
        private SettingsButtonState _settingsButtonState = SettingsButtonState.Hidden;
        private float _settingsButtonAnimTimer = 0f;
        private const float SETTINGS_BUTTON_ANIM_DURATION = 0.6f;

        private enum RoundAnimState { Hidden, Entering, Idle, Pop, Hang, Settle }
        private RoundAnimState _roundAnimState = RoundAnimState.Hidden;
        private float _roundAnimTimer = 0f;
        private int _lastRoundNumber = 1;

        private const float ROUND_ANIM_ENTER_DURATION = 0.5f;
        private const float ROUND_ANIM_POP_DURATION = 0.1f;
        private const float ROUND_ANIM_HANG_DURATION = 0.25f;
        private const float ROUND_ANIM_SETTLE_DURATION = 0.25f;
        private const float ROUND_MAX_SCALE = 1.5f;
        private const float ROUND_SHAKE_MAGNITUDE = 0.25f;
        private const float ROUND_SHAKE_FREQUENCY = 20f;

        private bool _victorySequenceTriggered = false;
        private LootScreen _lootScreen;
        private bool _lootScreenHasShown = false;
        private bool _floorTransitionTriggered = false;
        private bool _didFlee = false;

        private static readonly Regex _randomWordRegex = new Regex(@"\b[\w\-\']+(?:\$[\w\-\']+)+\b", RegexOptions.Compiled);

        public BattleAnimationManager AnimationManager => _animationManager;

        public BattleScene()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            _hitstopManager = ServiceLocator.Get<HitstopManager>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _progressionManager = ServiceLocator.Get<ProgressionManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            base.Initialize();
            _uiManager = new BattleUIManager();
            _renderer = new BattleRenderer();
            _animationManager = new BattleAnimationManager();
            _moveAnimationManager = new MoveAnimationManager();
            _inputHandler = new BattleInputHandler();
            _alertManager = new AlertManager();
            _battleLogManager = new BattleLogManager();
            _lootScreen = new LootScreen();
        }

        public override void Enter()
        {
            base.Enter();
            _uiManager.Reset();
            _renderer.Reset();
            _animationManager.Reset();
            _moveAnimationManager.SkipAll();
            _alertManager.Reset();

            _battleLogManager.Reset();
            _battleLogManager.Subscribe();

            _inputHandler.Reset();
            _enemyEntityIds.Clear();
            _currentActor = null;
            _isBattleOver = false;
            _endOfBattleTimer = 0f;
            _isWaitingForMultiHitDelay = false;
            _multiHitDelayTimer = 0f;
            _pendingAnimations.Clear();
            _rewardScreenShown = false;
            _isWaitingForActionExecution = false;
            _actionExecutionTimer = 0f;
            _isFadingOutOnDeath = false;
            _deathFadeTimer = 0f;
            _processedDeathAnimations.Clear();
            _watchdogTimer = 0f;
            _switchSequenceState = SwitchSequenceState.None;
            _victorySequenceTriggered = false;
            _lootScreenHasShown = false;
            _floorTransitionTriggered = false;
            _didFlee = false;
            _lootScreen.Reset();

            SubscribeToEvents();
            InitializeSettingsButton();

            _settingsButtonState = SettingsButtonState.Hidden;
            _settingsButtonAnimTimer = 0f;

            _roundAnimState = RoundAnimState.Hidden;
            _roundAnimTimer = 0f;
            _lastRoundNumber = 1;

            if (!_componentStore.HasComponent<CombatantStatsComponent>(_gameState.PlayerEntityId))
            {
                Debug.WriteLine($"[BattleScene] [FATAL] Player entity {_gameState.PlayerEntityId} is missing stats! Aborting battle setup.");
                return;
            }

            SetupBattle();

            if (_battleManager != null)
            {
                _currentIntroPhase = IntroPhase.EnemyDrop;
                _introSequenceQueue.Clear();
                _introTimer = BATTLE_ENTRY_INITIAL_DELAY;
                _uiSlideTimer = 0f;

                var players = _battleManager.AllCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();
                foreach (var p in players) p.VisualAlpha = 0f;

                _uiManager.IntroOffset = new Vector2(0, UI_SLIDE_DISTANCE);

                var leader = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == 0);
                if (leader != null)
                {
                    _uiManager.ShowActionMenu(leader, _battleManager.AllCombatants.ToList());
                }

                var enemies = _battleManager.AllCombatants.Where(c => !c.IsPlayerControlled && c.IsActiveOnField).OrderBy(c => c.BattleSlot).ToList();
                foreach (var e in enemies)
                {
                    _introSequenceQueue.Enqueue(e);
                    e.VisualAlpha = 0f;
                }

                _renderer.SetCenteringState(enemies.Count == 1);

                if (enemies.Count == 1) _animationManager.StartFloorIntroAnimation("floor_center");
                else
                {
                    _animationManager.StartFloorIntroAnimation("floor_0");
                    _animationManager.StartFloorIntroAnimation("floor_1");
                }

                foreach (var combatant in _battleManager.AllCombatants)
                {
                    if (combatant.IsPlayerControlled) combatant.VisualHP = combatant.Stats.CurrentHP;

                    // --- NEW: Reset HUD Visibility ---
                    combatant.HudVisualAlpha = 0f;
                }
            }
        }

        public override void Exit()
        {
            base.Exit();
            if (BattleSetup.ReturnSceneState != GameSceneState.Split)
            {
                _progressionManager.ClearCurrentSplitMap();
            }
            _lootScreen.Reset();
            UnsubscribeFromEvents();
            _battleLogManager.Unsubscribe();
            CleanupEntities();
            CleanupPlayerState();
            ServiceLocator.Unregister<BattleManager>();
        }

        private void SubscribeToEvents()
        {
            EventBus.Subscribe<GameEvents.ActionDeclared>(OnActionDeclared);
            EventBus.Subscribe<GameEvents.BattleActionExecuted>(OnBattleActionExecuted);
            EventBus.Subscribe<GameEvents.CombatantDefeated>(OnCombatantDefeated);
            EventBus.Subscribe<GameEvents.ActionFailed>(OnActionFailed);
            EventBus.Subscribe<GameEvents.StatusEffectTriggered>(OnStatusEffectTriggered);
            EventBus.Subscribe<GameEvents.CombatantHealed>(OnCombatantHealed);
            EventBus.Subscribe<GameEvents.CombatantManaRestored>(OnCombatantManaRestored);
            EventBus.Subscribe<GameEvents.CombatantManaConsumed>(OnCombatantManaConsumed);
            EventBus.Subscribe<GameEvents.MultiHitActionCompleted>(OnMultiHitActionCompleted);
            EventBus.Subscribe<GameEvents.CombatantRecoiled>(OnCombatantRecoiled);
            EventBus.Subscribe<GameEvents.AbilityActivated>(OnAbilityActivated);
            EventBus.Subscribe<GameEvents.AlertPublished>(OnAlertPublished);
            EventBus.Subscribe<GameEvents.CombatantStatStageChanged>(OnCombatantStatStageChanged);
            EventBus.Subscribe<GameEvents.MoveAnimationTriggered>(OnMoveAnimationTriggered);
            EventBus.Subscribe<GameEvents.NextEnemyApproaches>(OnNextEnemyApproaches);
            EventBus.Subscribe<GameEvents.CombatantSpawned>(OnCombatantSpawned);
            EventBus.Subscribe<GameEvents.CombatantSwitchingOut>(OnCombatantSwitchingOut);
            EventBus.Subscribe<GameEvents.MoveFailed>(OnMoveFailed);
            EventBus.Subscribe<GameEvents.SwitchSequenceInitiated>(OnSwitchSequenceInitiated);

            _uiManager.OnMoveSelected += OnPlayerMoveSelected;
            _uiManager.OnSwitchActionSelected += OnPlayerSwitchSelected;
            _uiManager.OnForcedSwitchSelected += OnForcedSwitchSelected;
            _uiManager.OnFleeRequested += FleeBattle;
            _uiManager.OnTargetSelectedFromUI += OnTargetSelectedFromUI;
            _inputHandler.OnMoveTargetSelected += OnPlayerMoveTargetSelected;
            _inputHandler.OnBackRequested += () => _uiManager.GoBack();
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
        }

        private void UnsubscribeFromEvents()
        {
            EventBus.Unsubscribe<GameEvents.ActionDeclared>(OnActionDeclared);
            EventBus.Unsubscribe<GameEvents.BattleActionExecuted>(OnBattleActionExecuted);
            EventBus.Unsubscribe<GameEvents.CombatantDefeated>(OnCombatantDefeated);
            EventBus.Unsubscribe<GameEvents.ActionFailed>(OnActionFailed);
            EventBus.Unsubscribe<GameEvents.StatusEffectTriggered>(OnStatusEffectTriggered);
            EventBus.Unsubscribe<GameEvents.CombatantHealed>(OnCombatantHealed);
            EventBus.Unsubscribe<GameEvents.CombatantManaRestored>(OnCombatantManaRestored);
            EventBus.Unsubscribe<GameEvents.CombatantManaConsumed>(OnCombatantManaConsumed);
            EventBus.Unsubscribe<GameEvents.MultiHitActionCompleted>(OnMultiHitActionCompleted);
            EventBus.Unsubscribe<GameEvents.CombatantRecoiled>(OnCombatantRecoiled);
            EventBus.Unsubscribe<GameEvents.AbilityActivated>(OnAbilityActivated);
            EventBus.Unsubscribe<GameEvents.AlertPublished>(OnAlertPublished);
            EventBus.Unsubscribe<GameEvents.CombatantStatStageChanged>(OnCombatantStatStageChanged);
            EventBus.Unsubscribe<GameEvents.MoveAnimationTriggered>(OnMoveAnimationTriggered);
            EventBus.Unsubscribe<GameEvents.NextEnemyApproaches>(OnNextEnemyApproaches);
            EventBus.Unsubscribe<GameEvents.CombatantSpawned>(OnCombatantSpawned);
            EventBus.Unsubscribe<GameEvents.CombatantSwitchingOut>(OnCombatantSwitchingOut);
            EventBus.Unsubscribe<GameEvents.MoveFailed>(OnMoveFailed);
            EventBus.Unsubscribe<GameEvents.SwitchSequenceInitiated>(OnSwitchSequenceInitiated);

            _uiManager.OnMoveSelected -= OnPlayerMoveSelected;
            _uiManager.OnSwitchActionSelected -= OnPlayerSwitchSelected;
            _uiManager.OnForcedSwitchSelected -= OnForcedSwitchSelected;
            _uiManager.OnFleeRequested -= FleeBattle;
            _uiManager.OnTargetSelectedFromUI -= OnTargetSelectedFromUI;
            _inputHandler.OnMoveTargetSelected -= OnPlayerMoveTargetSelected;
            _inputHandler.OnBackRequested -= () => _uiManager.GoBack();
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
        }

        private void InitializeSettingsButton()
        {
            int buttonSize = 16;
            int offScreenX = Global.VIRTUAL_WIDTH + 20;

            if (_settingsButton == null)
            {
                var sheet = _spriteManager.SplitMapSettingsButton;
                var rects = _spriteManager.SplitMapSettingsButtonSourceRects;

                _settingsButton = new ImageButton(new Rectangle(offScreenX, 2, buttonSize, buttonSize), sheet, rects[0], rects[1], enableHoverSway: true)
                {
                    UseScreenCoordinates = false,
                    TriggerHapticOnHover = true
                };
            }

            _settingsButton.Bounds = new Rectangle(offScreenX, 2, buttonSize, buttonSize);
            _settingsButton.OnClick = null;
            _settingsButton.OnClick += () =>
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                OpenSettings();
            };
            _settingsButton.ResetAnimationState();
        }

        private void SetupBattle()
        {
            var gameState = ServiceLocator.Get<GameState>();
            var playerParty = new List<BattleCombatant>();
            int playerEntityId = gameState.PlayerEntityId;
            var leaderCombatant = BattleCombatantFactory.CreateFromEntity(playerEntityId, "player_leader");
            if (leaderCombatant != null)
            {
                leaderCombatant.Name = gameState.PlayerState.Leader.Name;
                leaderCombatant.BattleSlot = 0;
                playerParty.Add(leaderCombatant);
            }

            for (int i = 1; i < gameState.PlayerState.Party.Count; i++)
            {
                var member = gameState.PlayerState.Party[i];
                var memberCombatant = CreateCombatantFromPartyMember(member, $"player_ally_{i}");
                memberCombatant.BattleSlot = i;
                playerParty.Add(memberCombatant);
            }

            var enemyParty = new List<BattleCombatant>();
            var enemyArchetypesToSpawn = BattleSetup.EnemyArchetypes ?? new List<string>();
            int enemyCount = Math.Min(enemyArchetypesToSpawn.Count, 4);
            for (int i = 0; i < enemyCount; i++)
            {
                string archetypeId = enemyArchetypesToSpawn[i];
                if (string.IsNullOrEmpty(archetypeId)) continue;
                int newEnemyId = Spawner.Spawn(archetypeId, new Vector2(-1, -1));
                if (newEnemyId != -1)
                {
                    var enemyCombatant = BattleCombatantFactory.CreateFromEntity(newEnemyId, $"enemy_{i + 1}");
                    if (enemyCombatant != null)
                    {
                        enemyCombatant.IsPlayerControlled = false;
                        enemyCombatant.BattleSlot = enemyParty.Count;
                        enemyParty.Add(enemyCombatant);
                        if (newEnemyId != gameState.PlayerEntityId) _enemyEntityIds.Add(newEnemyId);
                    }
                }
            }
            BattleSetup.EnemyArchetypes = null;

            if (!playerParty.Any())
            {
                Debug.WriteLine("[BattleScene] [FATAL] Player party is empty. Aborting battle.");
                _battleManager = null;
                return;
            }

            _battleManager = new BattleManager(playerParty, enemyParty, _animationManager);
            ServiceLocator.Register<BattleManager>(_battleManager);
            _previousBattlePhase = _battleManager.CurrentPhase;
        }

        private BattleCombatant CreateCombatantFromPartyMember(PartyMember member, string id)
        {
            var combatant = new BattleCombatant
            {
                CombatantID = id,
                Name = member.Name,
                ArchetypeId = "player",
                IsPlayerControlled = true,
                Stats = new CombatantStats
                {
                    MaxHP = member.MaxHP,
                    CurrentHP = member.CurrentHP,
                    MaxMana = member.MaxMana,
                    CurrentMana = member.CurrentMana,
                    Strength = member.Strength,
                    Intelligence = member.Intelligence,
                    Tenacity = member.Tenacity,
                    Agility = member.Agility
                },
                DefaultStrikeMoveID = member.DefaultStrikeMoveID,
                Spells = member.Spells,
                PortraitIndex = member.PortraitIndex
            };

            combatant.Stats.MaxHP = _gameState.PlayerState.GetEffectiveStat(member, "MaxHP");
            combatant.Stats.MaxMana = _gameState.PlayerState.GetEffectiveStat(member, "MaxMana");
            combatant.Stats.Strength = _gameState.PlayerState.GetEffectiveStat(member, "Strength");
            combatant.Stats.Intelligence = _gameState.PlayerState.GetEffectiveStat(member, "Intelligence");
            combatant.Stats.Tenacity = _gameState.PlayerState.GetEffectiveStat(member, "Tenacity");
            combatant.Stats.Agility = _gameState.PlayerState.GetEffectiveStat(member, "Agility");

            combatant.Stats.CurrentHP = member.CurrentHP;
            combatant.Stats.CurrentMana = member.CurrentMana;
            combatant.VisualHP = combatant.Stats.CurrentHP;

            if (member.IntrinsicAbilities != null && member.IntrinsicAbilities.Count > 0)
            {
                var intrinsicAbilities = AbilityFactory.CreateAbilitiesFromData(member.IntrinsicAbilities, new Dictionary<string, int>());
                combatant.RegisterAbilities(intrinsicAbilities);
            }

            foreach (var relicId in _gameState.PlayerState.GlobalRelics)
            {
                if (BattleDataCache.Relics.TryGetValue(relicId, out var relicData))
                {
                    var relicAbilities = AbilityFactory.CreateAbilitiesFromData(relicData.Effects, relicData.StatModifiers);
                    combatant.RegisterAbilities(relicAbilities);
                    combatant.ActiveRelics.Add(relicData);
                }
            }

            var data = BattleDataCache.PartyMembers.Values.FirstOrDefault(p => p.Name == member.Name);
            if (data != null)
            {
                combatant.Gender = data.Gender;
                combatant.IsProperNoun = data.IsProperNoun;
            }

            return combatant;
        }

        private void CleanupEntities()
        {
            if (_enemyEntityIds.Any())
            {
                var entityManager = ServiceLocator.Get<EntityManager>();
                var gameState = ServiceLocator.Get<GameState>();
                int playerId = gameState.PlayerEntityId;

                foreach (var id in _enemyEntityIds)
                {
                    if (id == playerId) continue;
                    _componentStore.EntityDestroyed(id);
                    entityManager.DestroyEntity(id);
                }
                _enemyEntityIds.Clear();
            }
        }

        private void CleanupPlayerState()
        {
            if (_battleManager != null && _gameState.PlayerState != null)
            {
                foreach (var member in _gameState.PlayerState.Party)
                {
                    var combatant = _battleManager.AllCombatants.FirstOrDefault(c => c.Name == member.Name && c.IsPlayerControlled);
                    if (combatant != null)
                    {
                        member.CurrentHP = combatant.Stats.CurrentHP;
                        member.CurrentMana = combatant.Stats.CurrentMana;
                    }
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_battleManager == null)
            {
                base.Update(gameTime);
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_lootScreen != null && _lootScreen.IsActive)
            {
                _lootScreen.Update(gameTime);
                _animationManager.Update(gameTime, _battleManager.AllCombatants);
                if (!_lootScreen.IsActive) FinalizeVictory();
                return;
            }

            if (_isFadingOutOnDeath)
            {
                _deathFadeTimer += dt;
                if (_deathFadeTimer >= Global.UniversalSlowFadeDuration)
                {
                    _sceneManager.ChangeScene(GameSceneState.GameOver, TransitionType.None, TransitionType.None);
                }
                return;
            }

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.BattleStartIntro)
            {
                if (_transitionManager.IsTransitioning) return;

                if (_currentIntroPhase == IntroPhase.EnemyDrop)
                {
                    _introTimer -= dt;
                    if (_introTimer <= 0)
                    {
                        if (_introSequenceQueue.Count > 0)
                        {
                            var nextCombatant = _introSequenceQueue.Dequeue();
                            _animationManager.StartIntroSlideAnimation(nextCombatant.CombatantID, new Vector2(0, -INTRO_SLIDE_DISTANCE), true);
                            _introTimer = INTRO_STAGGER_DELAY;
                        }
                        else if (_introTimer <= -0.5f)
                        {
                            _currentIntroPhase = IntroPhase.PlayerRise;
                            StartPlayerIntro();
                        }
                    }
                }
                else if (_currentIntroPhase == IntroPhase.PlayerRise)
                {
                    _uiSlideTimer += dt;
                    float progress = Math.Clamp(_uiSlideTimer / UI_SLIDE_DURATION, 0f, 1f);
                    float eased = Easing.EaseOutCubic(progress);
                    _uiManager.IntroOffset = Vector2.Lerp(new Vector2(0, UI_SLIDE_DISTANCE), Vector2.Zero, eased);

                    if (progress >= 1.0f)
                    {
                        _battleManager.ForceAdvance();
                        _settingsButtonState = SettingsButtonState.AnimatingIn;
                        _roundAnimState = RoundAnimState.Pop;
                        _roundAnimTimer = 0f;

                        // --- NEW: Trigger HUD Entry Animation ---
                        foreach (var c in _battleManager.AllCombatants)
                        {
                            _animationManager.StartHudEntryAnimation(c.CombatantID);
                        }
                    }
                }

                _animationManager.Update(gameTime, _battleManager.AllCombatants);
                _renderer.Update(gameTime, _battleManager.AllCombatants, _animationManager, null);
                _uiManager.Update(gameTime, currentMouseState, currentKeyboardState, null);
                return;
            }

            if (_switchSequenceState != SwitchSequenceState.None)
            {
                _switchSequenceTimer -= dt;
                if (_switchSequenceState == SwitchSequenceState.AnimatingOut)
                {
                    if (_switchSequenceTimer <= 0)
                    {
                        _battleManager.PerformLogicalSwitch(_switchOutgoing, _switchIncoming);
                        _switchSequenceState = SwitchSequenceState.AnimatingIn;
                        bool isEnemy = !_switchIncoming.IsPlayerControlled;
                        float duration = BattleAnimationManager.IntroSlideAnimationState.SLIDE_DURATION;
                        if (isEnemy) duration += BattleAnimationManager.IntroSlideAnimationState.WAIT_DURATION + BattleAnimationManager.IntroSlideAnimationState.REVEAL_DURATION;
                        _switchSequenceTimer = duration;
                        Vector2 offset = isEnemy ? new Vector2(0, -INTRO_SLIDE_DISTANCE) : new Vector2(0, INTRO_SLIDE_DISTANCE);
                        _animationManager.StartIntroSlideAnimation(_switchIncoming.CombatantID, offset, isEnemy);
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{_switchIncoming.Name} steps in!" });
                    }
                }
                else if (_switchSequenceState == SwitchSequenceState.AnimatingIn)
                {
                    if (_switchSequenceTimer <= 0)
                    {
                        _switchSequenceState = SwitchSequenceState.None;
                        _battleManager.ResumeAfterSwitch();
                        if (_switchIncoming != null) _switchIncoming.VisualAlpha = 1.0f;
                        _switchOutgoing = null;
                        _switchIncoming = null;
                    }
                }
            }

            _animationManager.Update(gameTime, _battleManager.AllCombatants);
            _moveAnimationManager.Update(gameTime);
            _uiManager.Update(gameTime, currentMouseState, currentKeyboardState, _battleManager.CurrentActingCombatant);
            _inputHandler.Update(gameTime, _uiManager, _renderer);

            var activeCombatant = _battleManager.CurrentActingCombatant ?? _currentActor;
            _renderer.Update(gameTime, _battleManager.AllCombatants, _animationManager, activeCombatant);

            _alertManager.Update(gameTime);
            _tooltipManager.Update(gameTime);

            if (_settingsButton != null)
            {
                if (_settingsButtonState == SettingsButtonState.AnimatingIn)
                {
                    _settingsButtonAnimTimer += dt;
                    float progress = Math.Clamp(_settingsButtonAnimTimer / SETTINGS_BUTTON_ANIM_DURATION, 0f, 1f);
                    float eased = Easing.EaseOutBack(progress);
                    float startX = Global.VIRTUAL_WIDTH + 20;
                    float targetX = Global.VIRTUAL_WIDTH - 16 - 2;
                    float currentX = MathHelper.Lerp(startX, targetX, eased);
                    _settingsButton.Bounds = new Rectangle((int)currentX, 2, 16, 16);
                    if (progress >= 1.0f) _settingsButtonState = SettingsButtonState.Visible;
                }
                else if (_settingsButtonState == SettingsButtonState.Visible)
                {
                    _settingsButton.Bounds = new Rectangle(Global.VIRTUAL_WIDTH - 16 - 2, 2, 16, 16);
                }
                if (_settingsButtonState != SettingsButtonState.Hidden) _settingsButton.Update(currentMouseState);
            }

            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                if (_uiManager.SubMenuState == BattleSubMenuState.ActionMoves || _uiManager.SubMenuState == BattleSubMenuState.Switch)
                {
                    _uiManager.GoBack();
                }
                else if (_uiManager.UIState == BattleUIState.Targeting)
                {
                    _uiManager.GoBack();
                }
                else
                {
                    OpenSettings();
                }
            }

            if (_roundAnimState == RoundAnimState.Entering)
            {
                _roundAnimTimer += dt;
                if (_roundAnimTimer >= ROUND_ANIM_ENTER_DURATION) { _roundAnimState = RoundAnimState.Idle; _roundAnimTimer = 0f; }
            }
            else if (_roundAnimState == RoundAnimState.Idle)
            {
                if (_battleManager.RoundNumber > _lastRoundNumber) { _lastRoundNumber = _battleManager.RoundNumber; _roundAnimState = RoundAnimState.Pop; _roundAnimTimer = 0f; }
            }
            else if (_roundAnimState == RoundAnimState.Pop)
            {
                _roundAnimTimer += dt;
                if (_roundAnimTimer >= ROUND_ANIM_POP_DURATION) { _roundAnimState = RoundAnimState.Hang; _roundAnimTimer = 0f; }
            }
            else if (_roundAnimState == RoundAnimState.Hang)
            {
                _roundAnimTimer += dt;
                if (_roundAnimTimer >= ROUND_ANIM_HANG_DURATION) { _roundAnimState = RoundAnimState.Settle; _roundAnimTimer = 0f; }
            }
            else if (_roundAnimState == RoundAnimState.Settle)
            {
                _roundAnimTimer += dt;
                if (_roundAnimTimer >= ROUND_ANIM_SETTLE_DURATION) { _roundAnimState = RoundAnimState.Idle; _roundAnimTimer = 0f; }
            }

            if (_isBattleOver)
            {
                if (!_uiManager.IsBusy && !_animationManager.IsVisuallyBusy)
                {
                    if (_didFlee)
                    {
                        if (!_victorySequenceTriggered)
                        {
                            _victorySequenceTriggered = true;
                            var transition = _transitionManager.GetRandomTransition();
                            _sceneManager.ChangeScene(BattleSetup.ReturnSceneState, transition, transition);
                        }
                        base.Update(gameTime);
                        return;
                    }

                    var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
                    bool playerWon = player != null && !player.IsDefeated;

                    if (playerWon)
                    {
                        if (!_victorySequenceTriggered)
                        {
                            TriggerVictoryRestoration();
                            _victorySequenceTriggered = true;
                        }
                        else if (!_animationManager.IsBlockingAnimation)
                        {
                            if (!_lootScreenHasShown)
                            {
                                if (!_floorTransitionTriggered)
                                {
                                    _uiManager.ForceClearNarration();
                                    if (!SplitMapScene.WasMajorBattle)
                                    {
                                        _animationManager.StartFloorOutroAnimation("floor_0");
                                        _animationManager.StartFloorOutroAnimation("floor_1");
                                        _animationManager.StartFloorIntroAnimation("floor_center");
                                    }
                                    _renderer.ForceDrawCenterFloor = true;
                                    _floorTransitionTriggered = true;
                                }
                                else
                                {
                                    bool floorsBusy = _animationManager.IsFloorAnimatingOut("floor_0") || _animationManager.IsFloorAnimatingOut("floor_1") || _animationManager.GetFloorIntroAnimationState("floor_center") != null;
                                    if (!floorsBusy)
                                    {
                                        var lootManager = ServiceLocator.Get<LootManager>();
                                        var loot = lootManager.GenerateCombatLoot();
                                        _lootScreen.Show(loot);
                                        _lootScreenHasShown = true;
                                    }
                                }
                            }
                            else if (!_lootScreen.IsActive)
                            {
                                FinalizeVictory();
                            }
                        }
                    }
                    else
                    {
                        if (!_isFadingOutOnDeath)
                        {
                            string killer = "Unknown Causes";
                            if (_currentActor != null && !_currentActor.IsPlayerControlled) killer = _currentActor.Name;
                            else if (_currentActor != null && _currentActor.IsPlayerControlled) killer = "Recoil";
                            else killer = "Status Effects";
                            _gameState.LastRunKiller = killer;
                            _isFadingOutOnDeath = true;
                            _deathFadeTimer = 0f;
                        }
                    }
                }
                base.Update(gameTime);
                return;
            }

            bool clickDetected = UIInputManager.CanProcessMouseClick() && currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            if (clickDetected)
            {
                if (_uiManager.IsBusy && !_uiManager.IsWaitingForInput)
                {
                }
                else if (_uiManager.IsWaitingForInput)
                {
                }
                else if (_animationManager.IsBlockingAnimation || _moveAnimationManager.IsAnimating)
                {
                    _animationManager.CompleteBlockingAnimations(_battleManager.AllCombatants);
                    _moveAnimationManager.CompleteCurrentAnimation();
                    UIInputManager.ConsumeMouseClick();
                }
                else
                {
                    if (_battleManager.CurrentPhase == BattleManager.BattlePhase.CheckForDefeat ||
                        _battleManager.CurrentPhase == BattleManager.BattlePhase.EndOfTurn ||
                        _battleManager.CurrentPhase == BattleManager.BattlePhase.Reinforcement)
                    {
                        _battleManager.RequestNextPhase();
                        UIInputManager.ConsumeMouseClick();
                    }
                }
            }

            bool isUiBusy = _uiManager.IsBusy;
            bool isAnimBusy = _animationManager.IsBlockingAnimation;
            bool isMoveAnimBusy = _moveAnimationManager.IsAnimating;
            bool isPendingBusy = _pendingAnimations.Any();
            bool isSwitching = _switchSequenceState != SwitchSequenceState.None;

            if (!isUiBusy && !isAnimBusy && !isMoveAnimBusy && !isPendingBusy && !isSwitching)
            {
                _battleManager.RequestNextPhase();
            }

            if (!_uiManager.IsBusy && !_animationManager.IsBlockingAnimation && _pendingAnimations.Any())
            {
                var nextAnimation = _pendingAnimations.Dequeue();
                nextAnimation.Invoke();
            }

            bool stateChanged = _battleManager.CurrentPhase != _lastFramePhase || isUiBusy != _wasUiBusyLastFrame || isAnimBusy != _wasAnimatingLastFrame;
            if (!stateChanged && !_isBattleOver && !_uiManager.IsWaitingForInput) _watchdogTimer += dt; else _watchdogTimer = 0f;

            if (_watchdogTimer > WATCHDOG_TIMEOUT)
            {
                Debug.WriteLine($"[BATTLE WATCHDOG] Softlock detected! Force advancing state.");
                _uiManager.ForceClearNarration();
                _animationManager.ForceClearAll();
                _moveAnimationManager.SkipAll();
                _pendingAnimations.Clear();
                _isWaitingForMultiHitDelay = false;
                _switchSequenceState = SwitchSequenceState.None;
                _battleManager.ForceAdvance();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[warning]Combat stalled. Watchdog forced advance." });
                _watchdogTimer = 0f;
            }

            _lastFramePhase = _battleManager.CurrentPhase;
            _wasUiBusyLastFrame = isUiBusy;
            _wasAnimatingLastFrame = isAnimBusy;

            _battleManager.Update(dt);

            if (_isWaitingForActionExecution)
            {
                _actionExecutionTimer += dt;
                if (_actionExecutionTimer >= ACTION_EXECUTION_DELAY)
                {
                    _isWaitingForActionExecution = false;
                    _actionExecutionTimer = 0f;
                    _battleManager.ExecuteDeclaredAction();
                }
            }

            var currentPhase = _battleManager.CurrentPhase;
            if (currentPhase != _previousBattlePhase)
            {
                HandlePhaseChange(currentPhase);
                _previousBattlePhase = currentPhase;
            }

            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.BattleOver && !_isBattleOver)
            {
                _isBattleOver = true;
                _uiManager.HideAllMenus();
                var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
                bool playerWon = player != null && !player.IsDefeated;
            }

            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.AnimatingMove && !_moveAnimationManager.IsAnimating)
            {
                EventBus.Publish(new GameEvents.MoveAnimationCompleted());
            }

            base.Update(gameTime);
        }

        private void StartPlayerIntro()
        {
            var players = _battleManager.AllCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();
            foreach (var p in players) _animationManager.StartIntroSlideAnimation(p.CombatantID, new Vector2(0, INTRO_SLIDE_DISTANCE), false);
            _uiSlideTimer = 0f;
        }

        private void TriggerVictoryRestoration()
        {
            if (_battleManager != null)
            {
                bool anyRestored = false;
                foreach (var combatant in _battleManager.AllCombatants)
                {
                    if (combatant.IsPlayerControlled && !combatant.IsDefeated)
                    {
                        if (combatant.Stats.CurrentMana < combatant.Stats.MaxMana)
                        {
                            float oldMana = combatant.Stats.CurrentMana;
                            combatant.Stats.CurrentMana = combatant.Stats.MaxMana;
                            _animationManager.StartManaRecoveryAnimation(combatant.CombatantID, oldMana, combatant.Stats.MaxMana);
                            combatant.ManaBarVisibleTimer = 2.0f;
                            anyRestored = true;
                        }
                    }
                }
            }
        }

        private void FinalizeVictory()
        {
            SplitMapScene.PlayerWonLastBattle = true;
            DecrementTemporaryBuffs();
            var transition = _transitionManager.GetRandomTransition();
            _sceneManager.ChangeScene(BattleSetup.ReturnSceneState, transition, transition);
        }

        private void DecrementTemporaryBuffs()
        {
            var gameState = ServiceLocator.Get<GameState>();
            var buffsComp = _componentStore.GetComponent<TemporaryBuffsComponent>(gameState.PlayerEntityId);
            if (buffsComp == null) return;
            for (int i = buffsComp.Buffs.Count - 1; i >= 0; i--)
            {
                buffsComp.Buffs[i].RemainingBattles--;
                if (buffsComp.Buffs[i].RemainingBattles <= 0) buffsComp.Buffs.RemoveAt(i);
            }
        }

        private void HandlePhaseChange(BattleManager.BattlePhase newPhase)
        {
            if (newPhase == BattleManager.BattlePhase.EndOfTurn || newPhase == BattleManager.BattlePhase.BattleOver) _currentActor = null;
            if (newPhase == BattleManager.BattlePhase.ActionSelection_Slot1 || newPhase == BattleManager.BattlePhase.ActionSelection_Slot2)
            {
                var actingCombatant = _battleManager.CurrentActingCombatant;
                if (actingCombatant != null) _uiManager.ShowActionMenu(actingCombatant, _battleManager.AllCombatants.ToList());
            }
            else if (newPhase == BattleManager.BattlePhase.StartOfTurn) { }
            else _uiManager.HideAllMenus();
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_battleManager == null)
            {
                string errorText = "Battle failed to initialize.";
                Vector2 textSize = font.MeasureString(errorText);
                spriteBatch.DrawStringSnapped(font, errorText, new Vector2((Global.VIRTUAL_WIDTH - textSize.X) / 2, (Global.VIRTUAL_HEIGHT - textSize.Y) / 2), Color.Red);
                return;
            }

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            _battleLogManager.Draw(spriteBatch);

            if (_roundAnimState != RoundAnimState.Hidden)
            {
                string roundText = _battleManager.RoundNumber.ToString();
                Vector2 roundTextSize = font.MeasureString(roundText);
                Vector2 roundTextPosition = new Vector2(5, 5);
                Vector2 origin = roundTextSize / 2f;
                Vector2 drawPos = roundTextPosition + origin;
                float scale = 1.0f;
                float rotation = 0f;
                Color color = ServiceLocator.Get<Global>().Palette_DarkShadow;

                if (_roundAnimState == RoundAnimState.Entering)
                {
                    float progress = Math.Clamp(_roundAnimTimer / ROUND_ANIM_ENTER_DURATION, 0f, 1f);
                    scale = Easing.EaseOutBack(progress);
                }
                else if (_roundAnimState == RoundAnimState.Pop)
                {
                    float progress = Math.Clamp(_roundAnimTimer / ROUND_ANIM_POP_DURATION, 0f, 1f);
                    scale = MathHelper.Lerp(1.0f, ROUND_MAX_SCALE, Easing.EaseOutCubic(progress));
                    color = Color.Lerp(ServiceLocator.Get<Global>().Palette_DarkShadow, Color.White, progress);
                }
                else if (_roundAnimState == RoundAnimState.Hang)
                {
                    scale = ROUND_MAX_SCALE;
                    color = Color.White;
                    float progress = Math.Clamp(_roundAnimTimer / ROUND_ANIM_HANG_DURATION, 0f, 1f);
                    float decay = 1.0f - Easing.EaseOutQuad(progress);
                    rotation = MathF.Sin(_roundAnimTimer * ROUND_SHAKE_FREQUENCY) * ROUND_SHAKE_MAGNITUDE * decay;
                }
                else if (_roundAnimState == RoundAnimState.Settle)
                {
                    float progress = Math.Clamp(_roundAnimTimer / ROUND_ANIM_SETTLE_DURATION, 0f, 1f);
                    scale = MathHelper.Lerp(ROUND_MAX_SCALE, 1.0f, Easing.EaseInCubic(progress));
                    color = Color.Lerp(Color.White, ServiceLocator.Get<Global>().Palette_DarkShadow, progress);
                }
                spriteBatch.DrawStringSnapped(font, roundText, drawPos, color, rotation, origin, scale, SpriteEffects.None, 0f);
            }

            BattleCombatant renderContextActor = _currentActor;
            if (_battleManager != null && (_battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot1 || _battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot2))
            {
                renderContextActor = _battleManager.CurrentActingCombatant;
            }

            _renderer.Draw(spriteBatch, font, gameTime, _battleManager.AllCombatants, renderContextActor, _uiManager, _inputHandler, _animationManager, _uiManager.SharedPulseTimer, transform);

            bool isFlashing = _animationManager.GetImpactFlashState() != null;

            if (isFlashing)
            {
                var core = ServiceLocator.Get<Core>();
                core.RequestFullscreenOverlay((sb, uiMatrix) =>
                {
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, uiMatrix);
                    _moveAnimationManager.Draw(sb);
                    _uiManager.Draw(sb, font, gameTime, Matrix.Identity);
                    _animationManager.DrawDamageIndicators(sb, ServiceLocator.Get<Core>().SecondaryFont);
                    _animationManager.DrawAbilityIndicators(sb, ServiceLocator.Get<Core>().SecondaryFont);
                    sb.End();
                });
            }
            else
            {
                _moveAnimationManager.Draw(spriteBatch);
                _uiManager.Draw(spriteBatch, font, gameTime, transform);
                _animationManager.DrawDamageIndicators(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont);
                _animationManager.DrawAbilityIndicators(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont);
            }
        }

        public override void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_isFadingOutOnDeath)
            {
                float alpha = Math.Clamp(_deathFadeTimer / Global.UniversalSlowFadeDuration, 0f, 1f);
                var pixel = ServiceLocator.Get<Texture2D>();
                var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
                var screenBounds = new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
                spriteBatch.Draw(pixel, screenBounds, Color.Black * alpha);
            }
        }

        public override void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            _uiManager.DrawFullscreenDialogs(spriteBatch, font, gameTime, transform);

            if (_lootScreen != null && _lootScreen.IsActive)
            {
                _lootScreen.Draw(spriteBatch, font, gameTime, transform);
            }
        }

        public void TriggerFlee()
        {
            FleeBattle();
        }

        private void TriggerDeathAnimation(BattleCombatant target)
        {
            if (_processedDeathAnimations.Contains(target.CombatantID)) return;
            _processedDeathAnimations.Add(target.CombatantID);
            Vector2 centerPos = _renderer.GetCombatantVisualCenterPosition(target, _battleManager.AllCombatants);
            if (centerPos == Vector2.Zero)
            {
                const int enemyAreaPadding = 40;
                int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
                int slotWidth = availableWidth / 2;
                int slotIndex = target.BattleSlot;
                float slotCenterX = enemyAreaPadding + (slotIndex * slotWidth) + (slotWidth / 2);
                bool isMajor = _spriteManager.IsMajorEnemySprite(target.ArchetypeId);
                int spriteSize = isMajor ? 96 : 64;
                float spriteCenterY = ENEMY_SLOT_Y_OFFSET + (spriteSize / 2f);
                centerPos = new Vector2(slotCenterX, spriteCenterY);
            }
            Vector2 visualOffset = _spriteManager.GetVisualCenterOffset(target.ArchetypeId);
            centerPos += visualOffset;
            float groundY = FIXED_COIN_GROUND_Y;
            _animationManager.StartDeathAnimation(target.CombatantID, centerPos, groundY);
            target.VisualAlpha = 1.0f;
        }

        private void OnPlayerMoveSelected(MoveData move, MoveEntry entry, BattleCombatant target)
        {
            var player = _battleManager.CurrentActingCombatant;
            if (player != null)
            {
                var action = _battleManager.CreateActionFromMove(player, move, target);
                action.SpellbookEntry = entry;
                _battleManager.SubmitAction(action);
            }
        }

        private void OnPlayerMoveTargetSelected(MoveData move, MoveEntry entry, BattleCombatant target)
        {
            OnPlayerMoveSelected(move, entry, target);
        }

        private void OnTargetSelectedFromUI(BattleCombatant target)
        {
            if (_uiManager.UIState == BattleUIState.Targeting) OnPlayerMoveSelected(_uiManager.MoveForTargeting, _uiManager.SpellForTargeting, target);
        }

        private void OnPlayerSwitchSelected(BattleCombatant targetMember)
        {
            var player = _battleManager.CurrentActingCombatant;
            if (player == null) return;
            var action = new QueuedAction { Actor = player, Target = targetMember, Priority = 6, ActorAgility = player.Stats.Agility, Type = QueuedActionType.Switch };
            _battleManager.SubmitAction(action);
            _uiManager.HideAllMenus();
        }

        private void OnForcedSwitchSelected(BattleCombatant targetMember)
        {
            _battleManager.SubmitInteractionResult(targetMember);
            _uiManager.HideAllMenus();
        }

        private void OnActionDeclared(GameEvents.ActionDeclared e)
        {
            _isWaitingForActionExecution = true;
            _actionExecutionTimer = 0f;
            _currentActor = e.Actor;
        }

        private void OnMoveAnimationTriggered(GameEvents.MoveAnimationTriggered e)
        {
            _moveAnimationManager.StartAnimation(e.Move, e.Targets, _renderer, e.GrazeStatus);
        }

        private void OnMultiHitActionCompleted(GameEvents.MultiHitActionCompleted e)
        {
            _isWaitingForMultiHitDelay = false;
            _multiHitDelayTimer = 0f;
        }

        private void OnBattleActionExecuted(GameEvents.BattleActionExecuted e)
        {
            _currentActor = e.Actor;
            _renderer.TriggerAttackAnimation(e.Actor.CombatantID);
            bool isMultiHit = e.ChosenMove != null && e.ChosenMove.Effects.ContainsKey("MultiHit");
            if (isMultiHit) _isWaitingForMultiHitDelay = true;
            var grazedTargets = new List<BattleCombatant>();
            for (int i = 0; i < e.Targets.Count; i++)
            {
                if (e.DamageResults[i].WasGraze) grazedTargets.Add(e.Targets[i]);
            }

            for (int i = 0; i < e.Targets.Count; i++)
            {
                var target = e.Targets[i];
                var result = e.DamageResults[i];
                Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(target, _battleManager.AllCombatants);

                if (result.DamageAmount > 0)
                {
                    target.HealthBarVisibleTimer = 6.0f;
                    float damageRatio = Math.Clamp((float)result.DamageAmount / target.Stats.MaxHP, 0f, 1f);
                    const float BASE_JUICE_SCALAR = 3.0f;
                    float juiceIntensity = 1.0f + (damageRatio * BASE_JUICE_SCALAR);
                    if (result.WasCritical) juiceIntensity *= 1.5f;
                    juiceIntensity = Math.Min(juiceIntensity, 5.0f);
                    if (!result.WasGraze)
                    {
                        float baseFreeze = result.WasCritical ? _global.HitstopDuration_Crit : _global.HitstopDuration_Normal;
                        _hitstopManager.Trigger(baseFreeze * juiceIntensity);
                        _animationManager.StartHitstopVisuals(target.CombatantID, result.WasCritical);
                        if (target.IsPlayerControlled)
                        {
                            _core.TriggerFullscreenFlash(Color.White, 0.15f * juiceIntensity);
                            _core.TriggerScreenFlashSequence(_global.Palette_Rust);
                        }
                        else _core.TriggerFullscreenFlash(Color.White, 0.15f * juiceIntensity);
                        _hapticsManager.TriggerCompoundShake(0.25f * juiceIntensity);
                    }
                    Vector2 attackerPos = _renderer.GetCombatantVisualCenterPosition(e.Actor, _battleManager.AllCombatants);
                    Vector2 targetPos = _renderer.GetCombatantVisualCenterPosition(target, _battleManager.AllCombatants);
                    Vector2 direction = targetPos - attackerPos;
                    if (direction != Vector2.Zero) direction.Normalize(); else direction = new Vector2(1, 0);
                    float shakeMag = 10f * juiceIntensity;
                    float recoilMag = 20f * juiceIntensity;
                    _hapticsManager.TriggerDirectionalShake(direction, shakeMag, 0.2f * juiceIntensity);
                    _renderer.TriggerRecoil(target.CombatantID, direction, recoilMag);
                    var sparks = _particleSystemManager.CreateEmitter(ParticleEffects.CreateHitSparks(juiceIntensity));
                    sparks.Position = targetPos;
                    sparks.EmitBurst(sparks.Settings.BurstCount);
                    var ring = _particleSystemManager.CreateEmitter(ParticleEffects.CreateImpactRing(juiceIntensity));
                    ring.Position = targetPos;
                    ring.EmitBurst(1);
                    _animationManager.StartHealthLossAnimation(target.CombatantID, target.VisualHP, target.Stats.CurrentHP);
                    _animationManager.StartHealthAnimation(target.CombatantID, (int)target.VisualHP, target.Stats.CurrentHP);
                    if (target.HasStatusEffect(StatusEffectType.Burn)) _renderer.TriggerStatusIconHop(target.CombatantID, StatusEffectType.Burn);
                    if (target.Stats.CurrentHP <= 0) TriggerDeathAnimation(target);
                    int baselineDamage = DamageCalculator.CalculateBaselineDamage(e.Actor, target, e.ChosenMove);
                    if (result.WasCritical || (result.DamageAmount >= baselineDamage * 1.5f && baselineDamage > 0)) _animationManager.StartEmphasizedDamageNumberIndicator(target.CombatantID, result.DamageAmount, hudPosition);
                    else _animationManager.StartDamageNumberIndicator(target.CombatantID, result.DamageAmount, hudPosition);
                }
                if (result.WasGraze) _animationManager.StartDamageIndicator(target.CombatantID, "GRAZE", hudPosition, ServiceLocator.Get<Global>().GrazeIndicatorColor);
                if (result.WasCritical)
                {
                    _animationManager.StartDamageIndicator(target.CombatantID, "CRITICAL HIT", hudPosition, ServiceLocator.Get<Global>().CritcalHitIndicatorColor);
                }
                if (result.WasProtected) _animationManager.StartProtectedIndicator(target.CombatantID, hudPosition);
            }
        }

        private void OnCombatantHealed(GameEvents.CombatantHealed e)
        {
            e.Target.HealthBarVisibleTimer = 6.0f;
            Action playVisuals = () =>
            {
                Vector2 targetPos = _renderer.GetCombatantVisualCenterPosition(e.Target, _battleManager.AllCombatants);
                var healParticles = _particleSystemManager.CreateEmitter(ParticleEffects.CreateHealBurst());
                healParticles.Position = targetPos;
                healParticles.EmitBurst(healParticles.Settings.BurstCount);
                _animationManager.StartHealBounceAnimation(e.Target.CombatantID);
                _animationManager.StartHealFlashAnimation(e.Target.CombatantID);
                _animationManager.StartHealthRecoveryAnimation(e.Target.CombatantID, e.VisualHPBefore, e.Target.Stats.CurrentHP);
                _animationManager.StartHealthAnimation(e.Target.CombatantID, e.VisualHPBefore, e.Target.Stats.CurrentHP);
                Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(e.Target, _battleManager.AllCombatants);
                _animationManager.StartHealNumberIndicator(e.Target.CombatantID, e.HealAmount, hudPosition);
            };
            playVisuals();
        }

        private void OnCombatantManaRestored(GameEvents.CombatantManaRestored e)
        {
            e.Target.ManaBarVisibleTimer = 6.0f;
            _animationManager.StartManaRecoveryAnimation(e.Target.CombatantID, e.ManaBefore, e.ManaAfter);
        }

        private void OnCombatantManaConsumed(GameEvents.CombatantManaConsumed e)
        {
            e.Actor.ManaBarVisibleTimer = 6.0f;
            _animationManager.StartManaLossAnimation(e.Actor.CombatantID, e.ManaBefore, e.ManaAfter);
        }

        private void OnCombatantRecoiled(GameEvents.CombatantRecoiled e)
        {
            e.Actor.HealthBarVisibleTimer = 6.0f;
            if (e.Actor.IsPlayerControlled)
            {
                _core.TriggerScreenFlashSequence(_global.Palette_Rust);
                _hapticsManager.TriggerWobble(intensity: 10.0f, duration: 0.75f, frequency: 120f);
            }
            _animationManager.StartHealthLossAnimation(e.Actor.CombatantID, e.Actor.VisualHP, e.Actor.Stats.CurrentHP);
            _animationManager.StartHealthAnimation(e.Actor.CombatantID, (int)e.Actor.VisualHP, e.Actor.Stats.CurrentHP);
            if (!e.Actor.IsPlayerControlled) _animationManager.StartHitFlashAnimation(e.Actor.CombatantID);
            Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(e.Actor, _battleManager.AllCombatants);
            _animationManager.StartDamageNumberIndicator(e.Actor.CombatantID, e.RecoilDamage, hudPosition);
        }

        private void OnCombatantDefeated(GameEvents.CombatantDefeated e)
        {
            TriggerDeathAnimation(e.DefeatedCombatant);
            if (!e.DefeatedCombatant.IsPlayerControlled)
            {
                int coinAmount = e.DefeatedCombatant.CoinReward;
                _gameState.PlayerState.Coin += coinAmount;
            }
        }

        private void OnActionFailed(GameEvents.ActionFailed e)
        {
            _currentActor = e.Actor;
            if (e.Reason == "stunned") _renderer.TriggerStatusIconHop(e.Actor.CombatantID, StatusEffectType.Stun);
            else if (e.Reason == "silenced") _renderer.TriggerStatusIconHop(e.Actor.CombatantID, StatusEffectType.Silence);
        }

        private void OnStatusEffectTriggered(GameEvents.StatusEffectTriggered e)
        {
            if (e.Damage > 0)
            {
                e.Combatant.HealthBarVisibleTimer = 6.0f;
                if (e.Combatant.IsPlayerControlled)
                {
                    _core.TriggerScreenFlashSequence(_global.Palette_Rust);
                    _hapticsManager.TriggerWobble(intensity: 10.0f, duration: 0.75f, frequency: 120f);
                }
                _animationManager.StartHealthLossAnimation(e.Combatant.CombatantID, e.Combatant.VisualHP, e.Combatant.Stats.CurrentHP);
                _animationManager.StartHealthAnimation(e.Combatant.CombatantID, (int)e.Combatant.VisualHP, e.Combatant.Stats.CurrentHP);
                _renderer.TriggerStatusIconHop(e.Combatant.CombatantID, e.EffectType);
                if (e.Combatant.Stats.CurrentHP <= 0) TriggerDeathAnimation(e.Combatant);
                else
                {
                    if (e.EffectType == StatusEffectType.Poison) _animationManager.StartPoisonEffectAnimation(e.Combatant.CombatantID);
                    else if (e.EffectType == StatusEffectType.Burn && !e.Combatant.IsPlayerControlled) _animationManager.StartHitFlashAnimation(e.Combatant.CombatantID);
                }
                Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(e.Combatant, _battleManager.AllCombatants);
                _animationManager.StartDamageNumberIndicator(e.Combatant.CombatantID, e.Damage, hudPosition);
            }
        }

        private void OnAbilityActivated(GameEvents.AbilityActivated e)
        {
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[debug]Ability Activated: {e.Ability.Name} ({e.Combatant.Name})" });
            Vector2 hudPos = _renderer.GetCombatantHudCenterPosition(e.Combatant, _battleManager.AllCombatants);
            _animationManager.StartAbilityIndicator(e.Combatant.CombatantID, e.Ability.Name, hudPos);
        }

        private void OnAlertPublished(GameEvents.AlertPublished e)
        {
            _alertManager.StartAlert(e.Message);
        }

        private void OnCombatantStatStageChanged(GameEvents.CombatantStatStageChanged e)
        {
            string statText = e.Stat.ToString().ToUpper();
            string verb = e.Amount > 0 ? "UP" : "DOWN";
            int absAmount = Math.Abs(e.Amount);
            string prefixText = absAmount > 1 ? $"{absAmount}x " : "";
            string suffixText = $" {verb}";
            Color changeColor = e.Amount > 0 ? _global.Palette_Sky : _global.Palette_Rust;
            Color statColor = _global.Palette_Sun;
            Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(e.Target, _battleManager.AllCombatants);
            _animationManager.StartStatStageIndicator(e.Target.CombatantID, prefixText, statText, suffixText, changeColor, statColor, changeColor, hudPosition);
        }

        private void OnNextEnemyApproaches(GameEvents.NextEnemyApproaches e)
        {
        }

        private void OnCombatantSpawned(GameEvents.CombatantSpawned e)
        {
            if (_switchSequenceState != SwitchSequenceState.None) return;
            bool isEnemy = !e.Combatant.IsPlayerControlled;
            Vector2 offset = isEnemy ? new Vector2(0, -INTRO_SLIDE_DISTANCE) : new Vector2(0, INTRO_SLIDE_DISTANCE);
            _animationManager.StartIntroSlideAnimation(e.Combatant.CombatantID, offset, isEnemy);
        }

        private void OnCombatantSwitchingOut(GameEvents.CombatantSwitchingOut e)
        {
            bool isEnemy = !e.Combatant.IsPlayerControlled;
            _animationManager.StartSwitchOutAnimation(e.Combatant.CombatantID, isEnemy);
        }

        private void OnMoveFailed(GameEvents.MoveFailed e)
        {
            Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(e.Actor, _battleManager.AllCombatants);
            _animationManager.StartFailedIndicator(e.Actor.CombatantID, hudPosition);
        }

        private void OnSwitchSequenceInitiated(GameEvents.SwitchSequenceInitiated e)
        {
            _switchOutgoing = e.OutgoingCombatant;
            _switchIncoming = e.IncomingCombatant;
            _switchSequenceState = SwitchSequenceState.AnimatingOut;
            bool isEnemy = !_switchOutgoing.IsPlayerControlled;
            if (isEnemy) _switchSequenceTimer = BattleAnimationManager.SwitchOutAnimationState.SILHOUETTE_DURATION + BattleAnimationManager.SwitchOutAnimationState.LIFT_DURATION;
            else _switchSequenceTimer = BattleAnimationManager.SwitchOutAnimationState.DURATION;
            EventBus.Publish(new GameEvents.CombatantSwitchingOut { Combatant = _switchOutgoing });
        }

        private void FleeBattle()
        {
            _didFlee = true;
            _isBattleOver = true;
            _uiManager.HideAllMenus();
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
        }

        private void OpenSettings()
        {
            _sceneManager.ShowModal(GameSceneState.Settings);
        }
    }
}
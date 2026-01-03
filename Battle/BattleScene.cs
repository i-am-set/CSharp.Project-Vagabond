using Microsoft.Xna.Framework;
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
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

namespace ProjectVagabond.Scenes
{
    public class BattleScene : GameScene
    {
        private const float MULTI_HIT_DELAY = 0.25f;
        private const float ACTION_EXECUTION_DELAY = 0.5f; // Tunable delay before attack execution
        private const float FIXED_COIN_GROUND_Y = 115f;
        private const int ENEMY_SLOT_Y_OFFSET = 12;
        private BattleManager _battleManager;
        private BattleUIManager _uiManager;
        private BattleRenderer _renderer;
        private BattleAnimationManager _animationManager;
        private MoveAnimationManager _moveAnimationManager;
        private BattleInputHandler _inputHandler;
        private AlertManager _alertManager;
        private readonly ChoiceGenerator _choiceGenerator;
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

        // Action Execution Delay State
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

        // --- SWITCH SEQUENCE DIRECTOR STATE ---
        private enum SwitchSequenceState { None, AnimatingOut, Swapping, AnimatingIn }
        private SwitchSequenceState _switchSequenceState = SwitchSequenceState.None;
        private float _switchSequenceTimer = 0f;
        private BattleCombatant _switchOutgoing;
        private BattleCombatant _switchIncoming;

        // --- INTRO SEQUENCE STATE ---
        private enum IntroPhase { EnemyDrop, PlayerRise }
        private IntroPhase _currentIntroPhase;
        private Queue<BattleCombatant> _introSequenceQueue = new Queue<BattleCombatant>();
        private float _introTimer = 0f;
        private const float INTRO_STAGGER_DELAY = 0.3f;
        private const float INTRO_SLIDE_DISTANCE = 150f;

        // UI Slide State
        private float _uiSlideTimer = 0f;
        private const float UI_SLIDE_DURATION = 0.5f;
        private const float UI_SLIDE_DISTANCE = 100f; // Distance to slide up from bottom

        // --- SETTINGS BUTTON ANIMATION STATE ---
        private enum SettingsButtonState { Hidden, AnimatingIn, Visible }
        private SettingsButtonState _settingsButtonState = SettingsButtonState.Hidden;
        private float _settingsButtonAnimTimer = 0f;
        private const float SETTINGS_BUTTON_ANIM_DURATION = 0.6f;

        // --- ROUND NUMBER ANIMATION STATE ---
        private enum RoundAnimState { Hidden, Entering, Idle, Pop, Hang, Settle }
        private RoundAnimState _roundAnimState = RoundAnimState.Hidden;
        private float _roundAnimTimer = 0f;
        private int _lastRoundNumber = 1;

        private const float ROUND_ANIM_ENTER_DURATION = 0.5f;
        private const float ROUND_ANIM_POP_DURATION = 0.1f;
        private const float ROUND_ANIM_HANG_DURATION = 0.25f;
        private const float ROUND_ANIM_SETTLE_DURATION = 0.25f;
        private const float ROUND_MAX_SCALE = 1.5f;
        private const float ROUND_SHAKE_MAGNITUDE = 0.25f; // Radians
        private const float ROUND_SHAKE_FREQUENCY = 20f;

        // --- VICTORY SEQUENCE STATE ---
        private bool _victorySequenceTriggered = false;

        // --- REGEX FOR RANDOM WORD PARSING ---
        // Matches words separated by $, e.g. "WORD$WORD$WORD"
        // \b: Word boundary
        // [\w\-\']+: A word (alphanumeric, hyphens, apostrophes, underscores)
        // (?:\$[\w\-\']+)+: Non-capturing group for $ followed by another word, repeated 1+ times
        private static readonly Regex _randomWordRegex = new Regex(@"\b[\w\-\']+(?:\$[\w\-\']+)+\b", RegexOptions.Compiled);

        public BattleAnimationManager AnimationManager => _animationManager;

        public BattleScene()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _choiceGenerator = new ChoiceGenerator();
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            _hitstopManager = ServiceLocator.Get<HitstopManager>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
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
        }

        public override void Enter()
        {
            base.Enter();
            _uiManager.Reset();
            _renderer.Reset();
            _animationManager.Reset();
            _moveAnimationManager.SkipAll();
            _alertManager.Reset();
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
            SubscribeToEvents();
            InitializeSettingsButton();

            // Reset Settings Button Animation
            _settingsButtonState = SettingsButtonState.Hidden;
            _settingsButtonAnimTimer = 0f;

            // Reset Round Number Animation
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
                // --- INTRO SEQUENCE SETUP ---
                _currentIntroPhase = IntroPhase.EnemyDrop;
                _introSequenceQueue.Clear();
                _introTimer = 0f;
                _uiSlideTimer = 0f;

                // 1. Hide Players and UI initially
                var players = _battleManager.AllCombatants
                    .Where(c => c.IsPlayerControlled && c.IsActiveOnField)
                    .ToList();

                foreach (var p in players)
                {
                    p.VisualAlpha = 0f; // Hidden
                }

                // Set UI Border Offset (Push down off-screen)
                _uiManager.IntroOffset = new Vector2(0, UI_SLIDE_DISTANCE);

                var leader = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == 0);
                if (leader != null)
                {
                    _uiManager.ShowActionMenu(leader, _battleManager.AllCombatants.ToList());
                }

                // 2. Queue Enemies (Slot 0 then Slot 1) for animation
                var enemies = _battleManager.AllCombatants
                    .Where(c => !c.IsPlayerControlled && c.IsActiveOnField)
                    .OrderBy(c => c.BattleSlot)
                    .ToList();

                foreach (var e in enemies)
                {
                    _introSequenceQueue.Enqueue(e);
                    e.VisualAlpha = 0f; // Hidden initially
                }

                // --- TRIGGER FLOOR ANIMATIONS ---
                // Check enemy count to decide which floor intro to play
                if (enemies.Count == 1)
                {
                    // Single enemy: Use centered floor
                    _animationManager.StartFloorIntroAnimation("floor_center");
                }
                else
                {
                    // Multiple enemies: Use standard slots
                    _animationManager.StartFloorIntroAnimation("floor_0");
                    _animationManager.StartFloorIntroAnimation("floor_1");
                }

                // Sync stats for players
                foreach (var combatant in _battleManager.AllCombatants)
                {
                    if (combatant.IsPlayerControlled)
                    {
                        combatant.Stats.CurrentHP = combatant.Stats.MaxHP;
                        combatant.Stats.CurrentMana = combatant.Stats.MaxMana;
                        combatant.VisualHP = combatant.Stats.MaxHP; // Sync visual HP
                    }
                }
            }
        }

        public override void Exit()
        {
            base.Exit();
            UnsubscribeFromEvents();
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
            _uiManager.OnItemSelected += OnPlayerItemSelected;
            _uiManager.OnSwitchActionSelected += OnPlayerSwitchSelected;
            _uiManager.OnForcedSwitchSelected += OnForcedSwitchSelected;
            _uiManager.OnFleeRequested += FleeBattle;
            _uiManager.OnTargetSelectedFromUI += OnTargetSelectedFromUI;
            _inputHandler.OnMoveTargetSelected += OnPlayerMoveTargetSelected;
            _inputHandler.OnItemTargetSelected += OnPlayerItemSelected;
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
            _uiManager.OnItemSelected -= OnPlayerItemSelected;
            _uiManager.OnSwitchActionSelected -= OnPlayerSwitchSelected;
            _uiManager.OnForcedSwitchSelected -= OnForcedSwitchSelected;
            _uiManager.OnFleeRequested -= FleeBattle;
            _uiManager.OnTargetSelectedFromUI -= OnTargetSelectedFromUI;
            _inputHandler.OnMoveTargetSelected -= OnPlayerMoveTargetSelected;
            _inputHandler.OnItemTargetSelected -= OnPlayerItemSelected;
            _inputHandler.OnBackRequested -= () => _uiManager.GoBack();
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
        }

        private void InitializeSettingsButton()
        {
            int buttonSize = 16;
            int offScreenX = Global.VIRTUAL_WIDTH + 20; // Safe off-screen position

            if (_settingsButton == null)
            {
                var settingsIcon = _spriteManager.SettingsIconSprite;
                if (settingsIcon != null) buttonSize = Math.Max(settingsIcon.Width, settingsIcon.Height);

                // Initialize off-screen
                _settingsButton = new ImageButton(new Rectangle(offScreenX, 2, buttonSize, buttonSize), settingsIcon, enableHoverSway: true)
                {
                    UseScreenCoordinates = false
                };
            }

            // Always reset position to off-screen on initialization/re-entry
            _settingsButton.Bounds = new Rectangle(offScreenX, 2, buttonSize, buttonSize);

            _settingsButton.OnClick += OpenSettings;
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

                        if (newEnemyId != gameState.PlayerEntityId)
                        {
                            _enemyEntityIds.Add(newEnemyId);
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[BattleScene] [WARNING] Failed to create combatant for enemy archetype '{archetypeId}'. Skipping.");
                    }
                }
                else
                {
                    Debug.WriteLine($"[BattleScene] [WARNING] Failed to spawn enemy entity for archetype '{archetypeId}'. Skipping.");
                }
            }
            BattleSetup.EnemyArchetypes = null;

            if (!playerParty.Any())
            {
                Debug.WriteLine("[BattleScene] [FATAL] Player party is empty. Aborting battle.");
                _battleManager = null;
                return;
            }

            _battleManager = new BattleManager(playerParty, enemyParty);
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
                    Level = member.Level,
                    MaxHP = member.MaxHP,
                    CurrentHP = member.CurrentHP,
                    MaxMana = member.MaxMana,
                    CurrentMana = member.CurrentMana,
                    Strength = member.Strength,
                    Intelligence = member.Intelligence,
                    Tenacity = member.Tenacity,
                    Agility = member.Agility
                },
                WeaknessElementIDs = new List<int>(member.WeaknessElementIDs),
                ResistanceElementIDs = new List<int>(member.ResistanceElementIDs),
                DefaultStrikeMoveID = member.DefaultStrikeMoveID,
                Spells = member.Spells,
                PortraitIndex = member.PortraitIndex,
                EquippedWeaponId = member.EquippedWeaponId,
                EquippedArmorId = member.EquippedArmorId,
                EquippedRelicId = member.EquippedRelicId
            };

            combatant.Stats.MaxHP = _gameState.PlayerState.GetEffectiveStat(member, "MaxHP");
            combatant.Stats.MaxMana = _gameState.PlayerState.GetEffectiveStat(member, "MaxMana");
            combatant.Stats.Strength = _gameState.PlayerState.GetEffectiveStat(member, "Strength");
            combatant.Stats.Intelligence = _gameState.PlayerState.GetEffectiveStat(member, "Intelligence");
            combatant.Stats.Tenacity = _gameState.PlayerState.GetEffectiveStat(member, "Tenacity");
            combatant.Stats.Agility = _gameState.PlayerState.GetEffectiveStat(member, "Agility");
            combatant.VisualHP = combatant.Stats.CurrentHP;

            if (!string.IsNullOrEmpty(member.EquippedRelicId))
            {
                if (BattleDataCache.Relics.TryGetValue(member.EquippedRelicId, out var relicData))
                {
                    combatant.ActiveRelics.Add(relicData);
                }
            }

            // --- Populate Narration Data from PartyMemberData ---
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
                    if (id == playerId)
                    {
                        Debug.WriteLine($"[BattleScene] [WARNING] Attempted to cleanup Player Entity ID {id}. Skipping.");
                        continue;
                    }

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

            if (_isFadingOutOnDeath)
            {
                _deathFadeTimer += dt;
                if (_deathFadeTimer >= Global.UniversalSlowFadeDuration)
                {
                    _sceneManager.ChangeScene(GameSceneState.GameOver, TransitionType.Diamonds, TransitionType.Diamonds);
                }
                return;
            }

            // --- INTRO SEQUENCE LOGIC ---
            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.BattleStartIntro)
            {
                // Wait for transition to clear
                if (_transitionManager.IsTransitioning) return;

                if (_currentIntroPhase == IntroPhase.EnemyDrop)
                {
                    _introTimer -= dt;
                    if (_introTimer <= 0)
                    {
                        if (_introSequenceQueue.Count > 0)
                        {
                            var nextCombatant = _introSequenceQueue.Dequeue();

                            // Drop from top (negative Y offset relative to final position)
                            Vector2 startOffset = new Vector2(0, -INTRO_SLIDE_DISTANCE);

                            // Pass isEnemy = true for the new multi-phase animation
                            _animationManager.StartIntroSlideAnimation(nextCombatant.CombatantID, startOffset, true);
                            _introTimer = INTRO_STAGGER_DELAY;
                        }
                        else
                        {
                            // Queue empty. Wait for animations to finish.
                            if (_introTimer <= -0.5f) // Wait 0.5s after last spawn
                            {
                                _currentIntroPhase = IntroPhase.PlayerRise;
                                StartPlayerIntro();
                            }
                        }
                    }
                }
                else if (_currentIntroPhase == IntroPhase.PlayerRise)
                {
                    // Animate UI
                    _uiSlideTimer += dt;
                    float progress = Math.Clamp(_uiSlideTimer / UI_SLIDE_DURATION, 0f, 1f);
                    float eased = Easing.EaseOutCubic(progress);
                    _uiManager.IntroOffset = Vector2.Lerp(new Vector2(0, UI_SLIDE_DISTANCE), Vector2.Zero, eased);

                    if (progress >= 1.0f)
                    {
                        _battleManager.ForceAdvance(); // Move to StartOfTurn
                        _settingsButtonState = SettingsButtonState.AnimatingIn; // Trigger settings button animation
                        _roundAnimState = RoundAnimState.Pop; // Trigger round number animation
                        _roundAnimTimer = 0f;
                    }
                }

                // Update animations during intro
                _animationManager.Update(gameTime, _battleManager.AllCombatants);
                _renderer.Update(gameTime, _battleManager.AllCombatants, _animationManager, null);
                return; // Skip normal update loop
            }

            // --- SWITCH SEQUENCE DIRECTOR ---
            if (_switchSequenceState != SwitchSequenceState.None)
            {
                _switchSequenceTimer -= dt;

                if (_switchSequenceState == SwitchSequenceState.AnimatingOut)
                {
                    if (_switchSequenceTimer <= 0)
                    {
                        // Phase 1 Complete: Perform Logic Swap
                        _battleManager.PerformLogicalSwitch(_switchOutgoing, _switchIncoming);

                        // Start Phase 2: Animate In
                        _switchSequenceState = SwitchSequenceState.AnimatingIn;

                        bool isEnemy = !_switchIncoming.IsPlayerControlled;

                        // Calculate duration based on animation type
                        float duration = BattleAnimationManager.IntroSlideAnimationState.SLIDE_DURATION;
                        if (isEnemy)
                        {
                            duration += BattleAnimationManager.IntroSlideAnimationState.WAIT_DURATION +
                                        BattleAnimationManager.IntroSlideAnimationState.REVEAL_DURATION;
                        }
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
                        // Phase 2 Complete: Resume Battle
                        _switchSequenceState = SwitchSequenceState.None;
                        _battleManager.ResumeAfterSwitch();

                        if (_switchIncoming != null)
                        {
                            _switchIncoming.VisualAlpha = 1.0f;
                        }

                        _switchOutgoing = null;
                        _switchIncoming = null;
                    }
                }
            }

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

            _animationManager.Update(gameTime, _battleManager.AllCombatants);
            _moveAnimationManager.Update(gameTime);
            _uiManager.Update(gameTime, currentMouseState, currentKeyboardState, _battleManager.CurrentActingCombatant);
            _inputHandler.Update(gameTime, _uiManager, _renderer);

            var activeCombatant = _battleManager.CurrentActingCombatant ?? _currentActor;
            _renderer.Update(gameTime, _battleManager.AllCombatants, _animationManager, activeCombatant);

            _alertManager.Update(gameTime);
            _tooltipManager.Update(gameTime);

            // Update Settings Button Animation & Logic
            if (_settingsButton != null)
            {
                if (_settingsButtonState == SettingsButtonState.AnimatingIn)
                {
                    _settingsButtonAnimTimer += dt;
                    float progress = Math.Clamp(_settingsButtonAnimTimer / SETTINGS_BUTTON_ANIM_DURATION, 0f, 1f);
                    float eased = Easing.EaseOutBack(progress);

                    float startX = Global.VIRTUAL_WIDTH + 20;
                    float targetX = Global.VIRTUAL_WIDTH - 16 - 2; // 16 size, 2 padding

                    float currentX = MathHelper.Lerp(startX, targetX, eased);
                    _settingsButton.Bounds = new Rectangle((int)currentX, 2, 16, 16);

                    if (progress >= 1.0f) _settingsButtonState = SettingsButtonState.Visible;
                }
                else if (_settingsButtonState == SettingsButtonState.Visible)
                {
                    int buttonSize = 16;
                    int padding = 2;
                    int buttonX = Global.VIRTUAL_WIDTH - buttonSize - padding;
                    int buttonY = padding;
                    _settingsButton.Bounds = new Rectangle(buttonX, buttonY, buttonSize, buttonSize);
                }

                // Only update input if visible
                if (_settingsButtonState != SettingsButtonState.Hidden)
                {
                    _settingsButton.Update(currentMouseState);
                }
            }

            // Update Round Number Animation
            if (_roundAnimState == RoundAnimState.Entering)
            {
                _roundAnimTimer += dt;
                if (_roundAnimTimer >= ROUND_ANIM_ENTER_DURATION)
                {
                    _roundAnimState = RoundAnimState.Idle;
                    _roundAnimTimer = 0f;
                }
            }
            else if (_roundAnimState == RoundAnimState.Idle)
            {
                if (_battleManager.RoundNumber > _lastRoundNumber)
                {
                    _lastRoundNumber = _battleManager.RoundNumber;
                    _roundAnimState = RoundAnimState.Pop;
                    _roundAnimTimer = 0f;
                }
            }
            else if (_roundAnimState == RoundAnimState.Pop)
            {
                _roundAnimTimer += dt;
                if (_roundAnimTimer >= ROUND_ANIM_POP_DURATION)
                {
                    _roundAnimState = RoundAnimState.Hang;
                    _roundAnimTimer = 0f;
                }
            }
            else if (_roundAnimState == RoundAnimState.Hang)
            {
                _roundAnimTimer += dt;
                if (_roundAnimTimer >= ROUND_ANIM_HANG_DURATION)
                {
                    _roundAnimState = RoundAnimState.Settle;
                    _roundAnimTimer = 0f;
                }
            }
            else if (_roundAnimState == RoundAnimState.Settle)
            {
                _roundAnimTimer += dt;
                if (_roundAnimTimer >= ROUND_ANIM_SETTLE_DURATION)
                {
                    _roundAnimState = RoundAnimState.Idle;
                    _roundAnimTimer = 0f;
                }
            }

            if (_isBattleOver)
            {
                if (!_uiManager.IsBusy && !_animationManager.IsAnimating)
                {
                    var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
                    bool playerWon = player != null && !player.IsDefeated;

                    if (playerWon)
                    {
                        if (!_victorySequenceTriggered)
                        {
                            TriggerVictoryRestoration();
                            _victorySequenceTriggered = true;
                        }
                        // Check IsAnimating again because TriggerVictoryRestoration might have started animations
                        else if (!_animationManager.IsAnimating)
                        {
                            FinalizeVictory();
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

            bool isAnimating = _animationManager.IsAnimating || _moveAnimationManager.IsAnimating;
            bool clickDetected = UIInputManager.CanProcessMouseClick() &&
                                 currentMouseState.LeftButton == ButtonState.Released &&
                                 previousMouseState.LeftButton == ButtonState.Pressed;

            bool isAttackAnimationPlaying = _moveAnimationManager.IsAnimating || _battleManager.IsProcessingMultiHit;

            if (isAnimating && clickDetected && !isAttackAnimationPlaying)
            {
                _animationManager.SkipAllHealthAnimations(_battleManager.AllCombatants);
                _animationManager.SkipAllBarAnimations();
                UIInputManager.ConsumeMouseClick();
            }

            if (!_uiManager.IsBusy && !_animationManager.IsAnimating && _pendingAnimations.Any())
            {
                var nextAnimation = _pendingAnimations.Dequeue();
                nextAnimation.Invoke();
            }

            bool uiBusy = _uiManager.IsBusy;
            bool animBusy = _animationManager.IsAnimating;
            bool moveAnimBusy = _moveAnimationManager.IsAnimating;
            bool pendingBusy = _pendingAnimations.Any();
            bool isMultiHitActive = _battleManager.IsProcessingMultiHit;
            bool isSwitching = _switchSequenceState != SwitchSequenceState.None;

            bool canAdvance;

            if (isMultiHitActive)
            {
                canAdvance = !pendingBusy;
            }
            else
            {
                canAdvance = !uiBusy && !animBusy && !moveAnimBusy && !pendingBusy && !isSwitching;
            }

            if (_isWaitingForMultiHitDelay && canAdvance)
            {
                if (_multiHitDelayTimer <= 0) _multiHitDelayTimer = MULTI_HIT_DELAY;
                _multiHitDelayTimer -= dt;

                if (_multiHitDelayTimer > 0) canAdvance = false;
                else _multiHitDelayTimer = 0f;
            }

            bool stateChanged = _battleManager.CurrentPhase != _lastFramePhase ||
                                uiBusy != _wasUiBusyLastFrame ||
                                animBusy != _wasAnimatingLastFrame;

            if (!stateChanged && !canAdvance && !_isBattleOver && !_uiManager.IsWaitingForInput)
            {
                _watchdogTimer += dt;
            }
            else
            {
                _watchdogTimer = 0f;
            }

            if (_watchdogTimer > WATCHDOG_TIMEOUT)
            {
                Debug.WriteLine($"[BATTLE WATCHDOG] Softlock detected! Force advancing state.");
                _uiManager.ForceClearNarration();
                _animationManager.ForceClearAll();
                _moveAnimationManager.SkipAll();
                _pendingAnimations.Clear();
                _isWaitingForMultiHitDelay = false;
                _switchSequenceState = SwitchSequenceState.None; // Reset switch state
                _battleManager.ForceAdvance();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[warning]Combat stalled. Watchdog forced advance." });
                _watchdogTimer = 0f;
                canAdvance = true;
            }

            _lastFramePhase = _battleManager.CurrentPhase;
            _wasUiBusyLastFrame = uiBusy;
            _wasAnimatingLastFrame = animBusy;

            _battleManager.CanAdvance = canAdvance;
            _battleManager.Update(dt); // Pass delta time

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
                _uiManager.ShowNarration(player != null && player.IsDefeated ? "Player Loses!" : "Player Wins!");
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
            foreach (var p in players)
            {
                // Slide up from bottom
                // Pass isEnemy = false for simple slide
                _animationManager.StartIntroSlideAnimation(p.CombatantID, new Vector2(0, INTRO_SLIDE_DISTANCE), false);
            }
            _uiSlideTimer = 0f;
        }

        private void ShowRewardScreen()
        {
            var choiceMenu = _sceneManager.GetScene(GameSceneState.ChoiceMenu) as ChoiceMenuScene;
            if (choiceMenu == null)
            {
                FinalizeVictory();
                return;
            }

            var choices = new List<object>();
            const int numberOfChoices = 3;
            int gameStage = 1;

            if (SplitMapScene.WasMajorBattle)
            {
                var playerAbilities = _componentStore.GetComponent<PassiveAbilitiesComponent>(_gameState.PlayerEntityId)?.RelicIDs;
                var excludeIds = playerAbilities != null ? new HashSet<string>(playerAbilities) : null;
                choices.AddRange(_choiceGenerator.GenerateAbilityChoices(gameStage, numberOfChoices, excludeIds));
            }
            else
            {
                var playerSpells = _gameState.PlayerState?.Spells.Where(s => s != null).Select(p => p.MoveID);
                var excludeIds = playerSpells != null ? new HashSet<string>(playerSpells) : null;
                choices.AddRange(_choiceGenerator.GenerateSpellChoices(gameStage, numberOfChoices, excludeIds));
            }

            if (!choices.Any())
            {
                FinalizeVictory();
                return;
            }

            choiceMenu.Show(choices, FinalizeVictory);
            _sceneManager.ShowModal(GameSceneState.ChoiceMenu);
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

                            // Trigger the ghost fill animation
                            _animationManager.StartManaRecoveryAnimation(combatant.CombatantID, oldMana, combatant.Stats.MaxMana);

                            // Force the bar to stay visible
                            combatant.ManaBarVisibleTimer = 2.0f;
                            anyRestored = true;
                        }
                    }
                }

                if (anyRestored)
                {
                    // Optional: Add a small delay or sound here if needed, but the animation duration acts as a delay.
                }
            }
        }

        private void FinalizeVictory()
        {
            SplitMapScene.PlayerWonLastBattle = true;
            DecrementTemporaryBuffs();

            var transition = _transitionManager.GetRandomCombatTransition();
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
                if (buffsComp.Buffs[i].RemainingBattles <= 0)
                {
                    buffsComp.Buffs.RemoveAt(i);
                }
            }
        }

        private void HandlePhaseChange(BattleManager.BattlePhase newPhase)
        {
            if (newPhase == BattleManager.BattlePhase.EndOfTurn || newPhase == BattleManager.BattlePhase.BattleOver)
            {
                _currentActor = null;
            }

            if (newPhase == BattleManager.BattlePhase.ActionSelection_Slot1 || newPhase == BattleManager.BattlePhase.ActionSelection_Slot2)
            {
                var actingCombatant = _battleManager.CurrentActingCombatant;
                if (actingCombatant != null)
                {
                    _uiManager.ShowActionMenu(actingCombatant, _battleManager.AllCombatants.ToList());
                }
            }
            else if (newPhase == BattleManager.BattlePhase.StartOfTurn)
            {
                // Do nothing. Keep previous state (Visible) until ActionSelection triggers or something else happens.
                // This prevents the 1-frame flicker if BattleManager is blocked.
            }
            else
            {
                _uiManager.HideAllMenus();
            }
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            DrawSceneContent(spriteBatch, font, gameTime, transform);
            spriteBatch.End();

            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            _renderer.DrawOverlay(spriteBatch, font);
            _tooltipManager.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont);
            _animationManager.DrawAbilityIndicators(spriteBatch, font);
            _alertManager.Draw(spriteBatch);

            // Only draw settings button if visible
            if (_settingsButtonState != SettingsButtonState.Visible && _settingsButtonState != SettingsButtonState.Hidden)
            {
                // If animating, draw with current bounds
                _settingsButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }
            else if (_settingsButtonState == SettingsButtonState.Visible)
            {
                _settingsButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }

            spriteBatch.End();
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

            // --- DRAW ROUND NUMBER ---
            if (_roundAnimState != RoundAnimState.Hidden)
            {
                string roundText = _battleManager.RoundNumber.ToString();
                Vector2 roundTextSize = font.MeasureString(roundText);
                Vector2 roundTextPosition = new Vector2(5, 5);
                Vector2 origin = roundTextSize / 2f;
                Vector2 drawPos = roundTextPosition + origin; // Adjust for origin centering

                float scale = 1.0f;
                float rotation = 0f;
                Color color = ServiceLocator.Get<Global>().Palette_DarkGray;

                if (_roundAnimState == RoundAnimState.Entering)
                {
                    float progress = Math.Clamp(_roundAnimTimer / ROUND_ANIM_ENTER_DURATION, 0f, 1f);
                    scale = Easing.EaseOutBack(progress);
                }
                else if (_roundAnimState == RoundAnimState.Pop)
                {
                    float progress = Math.Clamp(_roundAnimTimer / ROUND_ANIM_POP_DURATION, 0f, 1f);
                    scale = MathHelper.Lerp(1.0f, ROUND_MAX_SCALE, Easing.EaseOutCubic(progress));
                    color = Color.Lerp(ServiceLocator.Get<Global>().Palette_DarkGray, Color.White, progress);
                }
                else if (_roundAnimState == RoundAnimState.Hang)
                {
                    scale = ROUND_MAX_SCALE;
                    color = Color.White;

                    // Violent Rotation with Decay
                    float progress = Math.Clamp(_roundAnimTimer / ROUND_ANIM_HANG_DURATION, 0f, 1f);
                    float decay = 1.0f - Easing.EaseOutQuad(progress);
                    rotation = MathF.Sin(_roundAnimTimer * ROUND_SHAKE_FREQUENCY) * ROUND_SHAKE_MAGNITUDE * decay;
                }
                else if (_roundAnimState == RoundAnimState.Settle)
                {
                    float progress = Math.Clamp(_roundAnimTimer / ROUND_ANIM_SETTLE_DURATION, 0f, 1f);
                    scale = MathHelper.Lerp(ROUND_MAX_SCALE, 1.0f, Easing.EaseInCubic(progress));
                    color = Color.Lerp(Color.White, ServiceLocator.Get<Global>().Palette_DarkGray, progress);
                }

                spriteBatch.DrawStringSnapped(font, roundText, drawPos, color, rotation, origin, scale, SpriteEffects.None, 0f);
            }

            BattleCombatant renderContextActor = _currentActor;
            if (_battleManager != null &&
               (_battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot1 ||
                _battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot2))
            {
                renderContextActor = _battleManager.CurrentActingCombatant;
            }

            _renderer.Update(gameTime, _battleManager.AllCombatants, _animationManager, renderContextActor); // Update renderer state before drawing
            _renderer.Draw(spriteBatch, font, gameTime, _battleManager.AllCombatants, renderContextActor, _uiManager, _inputHandler, _animationManager, _uiManager.SharedPulseTimer, transform);
            _moveAnimationManager.Draw(spriteBatch);
            _uiManager.Draw(spriteBatch, font, gameTime, transform);
            _animationManager.DrawDamageIndicators(spriteBatch, secondaryFont);
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

        #region Event Handlers
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
            if (_uiManager.UIState == BattleUIState.Targeting)
            {
                OnPlayerMoveSelected(_uiManager.MoveForTargeting, _uiManager.SpellForTargeting, target);
            }
            else if (_uiManager.UIState == BattleUIState.ItemTargeting)
            {
                OnPlayerItemSelected(_uiManager.ItemForTargeting, target);
            }
        }

        private void OnPlayerItemSelected(ConsumableItemData item, BattleCombatant target)
        {
            var player = _battleManager.CurrentActingCombatant;
            if (player == null) return;

            if (target == null)
            {
                var enemies = _battleManager.AllCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
                switch (item.Target)
                {
                    case TargetType.Self:
                    case TargetType.SingleTeam: target = player; break;
                    case TargetType.Single: if (enemies.Any()) target = enemies.First(); break;
                }
            }

            var action = new QueuedAction { Actor = player, ChosenItem = item, Target = target, Priority = item.Priority, ActorAgility = player.Stats.Agility, Type = QueuedActionType.Item };
            _battleManager.SubmitAction(action);
            _uiManager.HideAllMenus();
        }

        private void OnPlayerSwitchSelected(BattleCombatant targetMember)
        {
            var player = _battleManager.CurrentActingCombatant;
            if (player == null) return;

            var action = new QueuedAction
            {
                Actor = player,
                Target = targetMember,
                Priority = 6,
                ActorAgility = player.Stats.Agility,
                Type = QueuedActionType.Switch
            };
            _battleManager.SubmitAction(action);
            _uiManager.HideAllMenus();
        }

        private void OnForcedSwitchSelected(BattleCombatant targetMember)
        {
            // This is called when the player selects a replacement from the forced switch dialog
            _battleManager.SubmitInteractionResult(targetMember);
            _uiManager.HideAllMenus();
        }

        private void OnActionDeclared(GameEvents.ActionDeclared e)
        {
            _isWaitingForActionExecution = true;
            _actionExecutionTimer = 0f;
            _currentActor = e.Actor;

            if (e.Type == QueuedActionType.Switch)
            {
                _uiManager.ShowNarration($"{e.Actor.Name} switches to {e.Target?.Name}!");
            }
            else
            {
                string actionName = e.Item != null ? e.Item.ItemName : (e.Move != null ? e.Move.MoveName : "Unknown Action");
                string typeTag = "";
                if (e.Move != null)
                {
                    typeTag = e.Move.MoveType == MoveType.Spell ? "cSpell" : "cAction";
                }
                else if (e.Item != null)
                {
                    typeTag = "cItem";
                }

                // --- CUSTOM NARRATION LOGIC ---
                string message = "";
                string actionPhrase = e.Item != null ? null : e.Move?.ActionPhrase;

                if (!string.IsNullOrEmpty(actionPhrase))
                {
                    // Use custom phrase
                    message = ParseActionPhrase(actionPhrase, e.Actor, e.Target, e.Move?.SourceItemName ?? e.Item?.ItemName);
                }
                else
                {
                    // Fallback to default
                    message = $"{e.Actor.Name} USED\n[{typeTag}]{actionName}[/]!";

                    if (e.Move != null && e.Actor.IsPlayerControlled && e.Move.MoveID == e.Actor.DefaultStrikeMoveID)
                    {
                        var partyMember = _gameState.PlayerState.Party.FirstOrDefault(p => p.Name == e.Actor.Name);
                        if (partyMember != null && !string.IsNullOrEmpty(partyMember.EquippedWeaponId))
                        {
                            if (BattleDataCache.Weapons.TryGetValue(partyMember.EquippedWeaponId, out var weaponData))
                            {
                                message = $"{e.Actor.Name} USED\n[{typeTag}]{actionName}[/] WITH {weaponData.WeaponName.ToUpper()}!";
                            }
                        }
                    }
                }

                _uiManager.ShowNarration(message);
            }
        }

        private string GetSmartName(BattleCombatant combatant)
        {
            string name = combatant.Name.ToUpper();

            // Only apply numbering to enemies
            if (!combatant.IsPlayerControlled && _battleManager != null)
            {
                var activeEnemies = _battleManager.AllCombatants
                    .Where(c => !c.IsPlayerControlled && c.IsActiveOnField && !c.IsDefeated)
                    .ToList();

                // Check if there are multiple enemies with the exact same name
                if (activeEnemies.Count(c => c.Name == combatant.Name) > 1)
                {
                    // Append the number suffix wrapped in [small] tag
                    if (combatant.BattleSlot == 0) name = name + " [small][cPrefix]#1[/][/]";
                    else if (combatant.BattleSlot == 1) name = name + " [small][cPrefix]#2[/][/]";
                }
            }
            return name;
        }

        private string ParseActionPhrase(string phrase, BattleCombatant user, BattleCombatant? target, string? itemName)
        {
            // 1. Handle Random Variations FIRST
            string processedPhrase = _randomWordRegex.Replace(phrase, (match) =>
            {
                var options = match.Value.Split('$');
                return options[_random.Next(options.Length)].Replace('_', ' ');
            });

            var sb = new StringBuilder(processedPhrase);

            // User Name & Proper Noun
            string userProperNounPrefix = user.IsProperNoun ? "" : "THE ";
            sb.Replace("{IsUserProperNoun}", userProperNounPrefix);

            // Use Smart Name for User
            sb.Replace("{user}", GetSmartName(user));

            // --- CONTEXTUAL SLOT COLOR REPLACEMENT ---
            // 1. Replace [cSlot] for User
            string userColor = user.IsPlayerControlled ? "[cDefault]" : "[cEnemy]";
            sb.Replace("[cSlot]{user}", $"{userColor}{{user}}");

            // 2. Replace [cSlot] for Target
            if (target != null)
            {
                string targetColor = target.IsPlayerControlled ? "[cDefault]" : "[cEnemy]";
                sb.Replace("[cSlot]{target}", $"{targetColor}{{target}}");
            }
            else
            {
                // Cleanup if no target
                sb.Replace("[cSlot]{target}", "{target}");
            }

            // 3. Now proceed with Name replacements
            sb.Replace("{IsUserProperNoun}", userProperNounPrefix);
            sb.Replace("{user}", GetSmartName(user));

            // User Pronouns
            string userPronoun = "THEIR";
            string userReflexive = "THEMSELVES";

            switch (user.Gender)
            {
                case Gender.Male:
                    userPronoun = "HIS";
                    userReflexive = "HIMSELF";
                    break;
                case Gender.Female:
                    userPronoun = "HER";
                    userReflexive = "HERSELF";
                    break;
                case Gender.Thing:
                    userPronoun = "ITS";
                    userReflexive = "ITSELF";
                    break;
            }
            sb.Replace("{user_pronoun}", userPronoun);
            sb.Replace("{user_reflexive}", userReflexive);

            // Item Name
            sb.Replace("{item_name}", itemName != null ? itemName.ToUpper() : "HANDS");

            // Target Name & Pronouns
            string targetPronoun = "THEIR"; // Default

            if (target != null)
            {
                // Use Smart Name for Target as well for consistency
                sb.Replace("{target}", GetSmartName(target));

                string properNounPrefix = target.IsProperNoun ? "" : "THE ";
                sb.Replace("{IsTargetProperNoun}", properNounPrefix);

                // Determine Target Pronoun
                switch (target.Gender)
                {
                    case Gender.Male: targetPronoun = "HIS"; break;
                    case Gender.Female: targetPronoun = "HER"; break;
                    case Gender.Thing: targetPronoun = "ITS"; break;
                }
            }
            else
            {
                // Fallback for non-targeted moves
                sb.Replace("{target}", "THE AIR");
                sb.Replace("{IsTargetProperNoun}", "");
                targetPronoun = "ITS"; // "The Air's" -> Its
            }

            sb.Replace("{target_pronoun}", targetPronoun);

            return sb.ToString();
        }

        private void OnMoveAnimationTriggered(GameEvents.MoveAnimationTriggered e)
        {
            _moveAnimationManager.StartAnimation(e.Move, e.Targets, _renderer, e.GrazeStatus);
        }

        private void OnMultiHitActionCompleted(GameEvents.MultiHitActionCompleted e)
        {
            string timeStr = e.HitCount == 1 ? "TIME" : "TIMES";
            _uiManager.ShowNarration($"HIT {e.HitCount} {timeStr}!");

            if (e.CriticalHitCount > 0)
            {
                if (e.CriticalHitCount == 1) _uiManager.ShowNarration("Landed a\n[cCrit]CRITICAL HIT[/]!");
                else _uiManager.ShowNarration($"Landed {e.CriticalHitCount}\n[cCrit]CRITICAL HITS[/]!");
            }
            _isWaitingForMultiHitDelay = false;
            _multiHitDelayTimer = 0f;
        }

        private void OnBattleActionExecuted(GameEvents.BattleActionExecuted e)
        {
            _currentActor = e.Actor;
            _renderer.TriggerAttackAnimation(e.Actor.CombatantID);

            bool isMultiHit = e.ChosenMove != null && e.ChosenMove.Effects.ContainsKey("MultiHit");
            if (isMultiHit) _isWaitingForMultiHitDelay = true;

            // --- GRAZE NARRATION LOGIC ---
            var grazedTargets = new List<BattleCombatant>();
            for (int i = 0; i < e.Targets.Count; i++)
            {
                if (e.DamageResults[i].WasGraze)
                {
                    grazedTargets.Add(e.Targets[i]);
                }
            }

            if (grazedTargets.Any())
            {
                // Sort: Players first, then Enemies. Within group, Slot 0 then Slot 1.
                // IsPlayerControlled: true for players.
                // OrderByDescending(IsPlayerControlled) puts true (Players) before false (Enemies).
                // ThenBy(BattleSlot) puts 0 before 1.
                var sortedGrazes = grazedTargets
                    .OrderByDescending(c => c.IsPlayerControlled)
                    .ThenBy(c => c.BattleSlot)
                    .ToList();

                foreach (var target in sortedGrazes)
                {
                    string targetName = GetSmartName(target);
                    _uiManager.ShowNarration($"{targetName} WAS GRAZED.");
                }
            }

            for (int i = 0; i < e.Targets.Count; i++)
            {
                var target = e.Targets[i];
                var result = e.DamageResults[i];
                Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(target, _battleManager.AllCombatants);

                if (result.AttackerAbilitiesTriggered != null)
                    foreach (var ability in result.AttackerAbilitiesTriggered) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = e.Actor, Ability = ability });

                if (result.DefenderAbilitiesTriggered != null)
                    foreach (var ability in result.DefenderAbilitiesTriggered) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = target, Ability = ability });

                if (result.DamageAmount > 0)
                {
                    // Set visibility timer for health bar
                    target.HealthBarVisibleTimer = 6.0f;

                    // --- DYNAMIC JUICE CALCULATION ---
                    float damageRatio = Math.Clamp((float)result.DamageAmount / target.Stats.MaxHP, 0f, 1f);

                    // Base scalar: 3.0 means 100% damage = 3x juice. 10% damage = 1.3x juice.
                    const float BASE_JUICE_SCALAR = 3.0f;
                    float juiceIntensity = 1.0f + (damageRatio * BASE_JUICE_SCALAR);

                    if (result.WasCritical) juiceIntensity *= 1.5f;

                    // Cap to prevent game-breaking values on massive overkill
                    juiceIntensity = Math.Min(juiceIntensity, 5.0f);

                    // Apply Juice
                    if (!result.WasGraze)
                    {
                        float baseFreeze = result.WasCritical ? _global.HitstopDuration_Crit : _global.HitstopDuration_Normal;
                        _hitstopManager.Trigger(baseFreeze * juiceIntensity);

                        // Trigger Visual Pop (Flash/Scale)
                        _animationManager.StartHitstopVisuals(target.CombatantID, result.WasCritical);

                        // Screen Flash for EVERY hit (White for enemies, Red for players)
                        if (target.IsPlayerControlled)
                        {
                            _core.TriggerFullscreenFlash(Color.White, 0.15f * juiceIntensity);
                            _core.TriggerScreenFlashSequence(_global.Palette_Red);
                        }
                        else
                        {
                            // Use CRT shader flash for enemies too
                            _core.TriggerFullscreenFlash(Color.White, 0.15f * juiceIntensity);
                        }

                        _hapticsManager.TriggerCompoundShake(0.75f * juiceIntensity);
                    }

                    // Directional Shake & Recoil
                    // Calculate direction from attacker to target
                    Vector2 attackerPos = _renderer.GetCombatantVisualCenterPosition(e.Actor, _battleManager.AllCombatants);
                    Vector2 targetPos = _renderer.GetCombatantVisualCenterPosition(target, _battleManager.AllCombatants);
                    Vector2 direction = targetPos - attackerPos;
                    if (direction != Vector2.Zero) direction.Normalize();
                    else direction = new Vector2(1, 0); // Fallback

                    float shakeMag = 10f * juiceIntensity;
                    float recoilMag = 20f * juiceIntensity;

                    _hapticsManager.TriggerDirectionalShake(direction, shakeMag, 0.2f * juiceIntensity);
                    _renderer.TriggerRecoil(target.CombatantID, direction, recoilMag);

                    // Particles
                    var sparks = _particleSystemManager.CreateEmitter(ParticleEffects.CreateHitSparks(juiceIntensity));
                    sparks.Position = targetPos;
                    sparks.EmitBurst(sparks.Settings.BurstCount);

                    var ring = _particleSystemManager.CreateEmitter(ParticleEffects.CreateImpactRing(juiceIntensity));
                    ring.Position = targetPos;
                    ring.EmitBurst(1);

                    _animationManager.StartHealthLossAnimation(target.CombatantID, target.VisualHP, target.Stats.CurrentHP);
                    _animationManager.StartHealthAnimation(target.CombatantID, (int)target.VisualHP, target.Stats.CurrentHP);

                    // Trigger Burn Icon Hop if target is burned and takes damage
                    if (target.HasStatusEffect(StatusEffectType.Burn))
                    {
                        _renderer.TriggerStatusIconHop(target.CombatantID, StatusEffectType.Burn);
                    }

                    if (target.Stats.CurrentHP <= 0)
                    {
                        TriggerDeathAnimation(target);
                    }
                    else
                    {
                        // Removed StartHitFlashAnimation as we now use screen flash + recoil
                    }

                    int baselineDamage = DamageCalculator.CalculateBaselineDamage(e.Actor, target, e.ChosenMove);
                    if (result.WasCritical || (result.DamageAmount >= baselineDamage * 1.5f && baselineDamage > 0))
                        _animationManager.StartEmphasizedDamageNumberIndicator(target.CombatantID, result.DamageAmount, hudPosition);
                    else
                        _animationManager.StartDamageNumberIndicator(target.CombatantID, result.DamageAmount, hudPosition);
                }

                if (result.WasGraze) _animationManager.StartDamageIndicator(target.CombatantID, "GRAZE", hudPosition, ServiceLocator.Get<Global>().GrazeIndicatorColor);

                if (result.WasCritical)
                {
                    _animationManager.StartDamageIndicator(target.CombatantID, "CRITICAL HIT", hudPosition, ServiceLocator.Get<Global>().CritcalHitIndicatorColor);
                    if (!isMultiHit) _uiManager.ShowNarration($"A [cCrit]CRITICAL HIT[/] on {target.Name}!");
                }

                if (result.WasProtected)
                {
                    _animationManager.StartProtectedIndicator(target.CombatantID, hudPosition);
                }

                var font = ServiceLocator.Get<Core>().SecondaryFont;
                Vector2 effectivenessPosition = hudPosition + new Vector2(0, font.LineHeight / 2f + 10);
                switch (result.Effectiveness)
                {
                    case DamageCalculator.ElementalEffectiveness.Effective: _animationManager.StartEffectivenessIndicator(target.CombatantID, "EFFECTIVE", effectivenessPosition); break;
                    case DamageCalculator.ElementalEffectiveness.Resisted: _animationManager.StartEffectivenessIndicator(target.CombatantID, "RESISTED", effectivenessPosition); break;
                    case DamageCalculator.ElementalEffectiveness.Immune: _animationManager.StartEffectivenessIndicator(target.CombatantID, "IMMUNE", effectivenessPosition); break;
                }
            }
        }

        private void OnCombatantHealed(GameEvents.CombatantHealed e)
        {
            // Set visibility timer for health bar
            e.Target.HealthBarVisibleTimer = 6.0f;

            // Define the visual effects as an Action to be executed when the narration appears
            Action playVisuals = () =>
            {
                // 1. Trigger Particles
                Vector2 targetPos = _renderer.GetCombatantVisualCenterPosition(e.Target, _battleManager.AllCombatants);
                var healParticles = _particleSystemManager.CreateEmitter(ParticleEffects.CreateHealBurst());
                healParticles.Position = targetPos;
                healParticles.EmitBurst(healParticles.Settings.BurstCount);

                // 2. Trigger Sprite Bounce & Flash
                _animationManager.StartHealBounceAnimation(e.Target.CombatantID);
                _animationManager.StartHealFlashAnimation(e.Target.CombatantID);

                // 3. Trigger Health Bar "Ghost Fill" Animation
                _animationManager.StartHealthRecoveryAnimation(e.Target.CombatantID, e.VisualHPBefore, e.Target.Stats.CurrentHP);

                // 4. Standard Health Lerp
                _animationManager.StartHealthAnimation(e.Target.CombatantID, e.VisualHPBefore, e.Target.Stats.CurrentHP);

                // 5. Number Indicator
                Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(e.Target, _battleManager.AllCombatants);
                _animationManager.StartHealNumberIndicator(e.Target.CombatantID, e.HealAmount, hudPosition);
            };

            // Pass the action to the UI manager to be executed alongside the text
            _uiManager.ShowNarration($"{e.Target.Name} recovered\n{e.HealAmount} HP!", playVisuals);
        }

        private void OnCombatantManaRestored(GameEvents.CombatantManaRestored e)
        {
            // Set visibility timer for mana bar
            e.Target.ManaBarVisibleTimer = 6.0f;

            _uiManager.ShowNarration($"{e.Target.Name} restored\n{e.AmountRestored} Mana!");
            _animationManager.StartManaRecoveryAnimation(e.Target.CombatantID, e.ManaBefore, e.ManaAfter);
        }

        private void OnCombatantManaConsumed(GameEvents.CombatantManaConsumed e)
        {
            // Set visibility timer for mana bar
            e.Actor.ManaBarVisibleTimer = 6.0f;

            _animationManager.StartManaLossAnimation(e.Actor.CombatantID, e.ManaBefore, e.ManaAfter);
        }

        private void OnCombatantRecoiled(GameEvents.CombatantRecoiled e)
        {
            // Set visibility timer for health bar
            e.Actor.HealthBarVisibleTimer = 6.0f;

            if (e.Actor.IsPlayerControlled)
            {
                _core.TriggerScreenFlashSequence(_global.Palette_Red);
                _hapticsManager.TriggerWobble(intensity: 10.0f, duration: 0.75f, frequency: 120f);
            }

            if (e.SourceAbility != null) _uiManager.ShowNarration($"{e.Actor.Name} was hurt by\n{e.SourceAbility.AbilityName}!");
            else _uiManager.ShowNarration($"{e.Actor.Name} is damaged by recoil!");

            _animationManager.StartHealthLossAnimation(e.Actor.CombatantID, e.Actor.VisualHP, e.Actor.Stats.CurrentHP);
            _animationManager.StartHealthAnimation(e.Actor.CombatantID, (int)e.Actor.VisualHP, e.Actor.Stats.CurrentHP);
            if (!e.Actor.IsPlayerControlled) _animationManager.StartHitFlashAnimation(e.Actor.CombatantID);

            Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(e.Actor, _battleManager.AllCombatants);
            _animationManager.StartDamageNumberIndicator(e.Actor.CombatantID, e.RecoilDamage, hudPosition);
        }

        private void OnCombatantDefeated(GameEvents.CombatantDefeated e)
        {
            _uiManager.ShowNarration($"{e.DefeatedCombatant.Name} was [cDefeat]DEFEATED[/]!");
            TriggerDeathAnimation(e.DefeatedCombatant);
            if (!e.DefeatedCombatant.IsPlayerControlled)
            {
                int coinAmount = e.DefeatedCombatant.CoinReward;
                _gameState.PlayerState.Coin += coinAmount;
                _uiManager.ShowNarration($"Gained {coinAmount} Coins!");
            }
        }

        private void OnActionFailed(GameEvents.ActionFailed e)
        {
            _currentActor = e.Actor;
            if (e.Reason.StartsWith("charging")) _uiManager.ShowNarration($"{e.Actor.Name} is {e.Reason}!");
            else _uiManager.ShowNarration($"{e.Actor.Name} is {e.Reason} and cannot move!");

            // Trigger Status Icon Hop for Stun/Silence
            if (e.Reason == "stunned")
            {
                _renderer.TriggerStatusIconHop(e.Actor.CombatantID, StatusEffectType.Stun);
            }
            else if (e.Reason == "silenced")
            {
                _renderer.TriggerStatusIconHop(e.Actor.CombatantID, StatusEffectType.Silence);
            }
        }

        private void OnStatusEffectTriggered(GameEvents.StatusEffectTriggered e)
        {
            if (e.Damage > 0)
            {
                // Set visibility timer for health bar
                e.Combatant.HealthBarVisibleTimer = 6.0f;

                if (e.Combatant.IsPlayerControlled)
                {
                    _core.TriggerScreenFlashSequence(_global.Palette_Red);
                    _hapticsManager.TriggerWobble(intensity: 10.0f, duration: 0.75f, frequency: 120f);
                }

                string effectName = e.EffectType == StatusEffectType.Burn ? "the burn" : e.EffectType.ToString();
                _uiManager.ShowNarration($"{e.Combatant.Name} takes {e.Damage} damage from [cStatus]{effectName}[/]!");

                _animationManager.StartHealthLossAnimation(e.Combatant.CombatantID, e.Combatant.VisualHP, e.Combatant.Stats.CurrentHP);
                _animationManager.StartHealthAnimation(e.Combatant.CombatantID, (int)e.Combatant.VisualHP, e.Combatant.Stats.CurrentHP);

                // Trigger Status Icon Hop for Poison/Burn
                _renderer.TriggerStatusIconHop(e.Combatant.CombatantID, e.EffectType);

                if (e.Combatant.Stats.CurrentHP <= 0)
                {
                    TriggerDeathAnimation(e.Combatant);
                }
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
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[debug]Ability Activated: {e.Ability.AbilityName} ({e.Combatant.Name})" });
            _animationManager.StartAbilityIndicator(e.Ability.AbilityName);
            if (!string.IsNullOrEmpty(e.NarrationText)) _uiManager.ShowNarration(e.NarrationText);
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

            Color changeColor = e.Amount > 0 ? _global.Palette_LightBlue : _global.Palette_Red;
            Color statColor = e.Stat switch
            {
                OffensiveStatType.Strength => _global.StatColor_Strength,
                OffensiveStatType.Intelligence => _global.StatColor_Intelligence,
                OffensiveStatType.Tenacity => _global.StatColor_Tenacity,
                OffensiveStatType.Agility => _global.StatColor_Agility,
                _ => _global.Palette_White
            };

            Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(e.Target, _battleManager.AllCombatants);
            _animationManager.StartStatStageIndicator(e.Target.CombatantID, prefixText, statText, suffixText, changeColor, statColor, changeColor, hudPosition);
        }

        private void OnNextEnemyApproaches(GameEvents.NextEnemyApproaches e)
        {
            _uiManager.ShowNarration("Another [cEnemy]ENEMY[/] approaches...");
        }

        private void OnCombatantSpawned(GameEvents.CombatantSpawned e)
        {
            // If we are in the middle of a switch sequence, we handle the animation manually in Update
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
            // Start the visual sequence
            _switchOutgoing = e.OutgoingCombatant;
            _switchIncoming = e.IncomingCombatant;
            _switchSequenceState = SwitchSequenceState.AnimatingOut;

            // Determine duration based on who is switching
            bool isEnemy = !_switchOutgoing.IsPlayerControlled;
            if (isEnemy)
            {
                // Enemy: Silhouette + Lift
                _switchSequenceTimer = BattleAnimationManager.SwitchOutAnimationState.SILHOUETTE_DURATION +
                                       BattleAnimationManager.SwitchOutAnimationState.LIFT_DURATION;
            }
            else
            {
                // Player: Simple Lift
                _switchSequenceTimer = BattleAnimationManager.SwitchOutAnimationState.DURATION;
            }

            // Trigger the "Out" animation
            EventBus.Publish(new GameEvents.CombatantSwitchingOut { Combatant = _switchOutgoing });
        }

        private void FleeBattle()
        {
            _isBattleOver = true;
            _uiManager.HideAllMenus();
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
            _uiManager.ShowNarration(player != null ? $"{player.Name} [cEscape]ESCAPED[/]." : "Got away safely!");
        }

        private void OpenSettings()
        {
            _sceneManager.ShowModal(GameSceneState.Settings);
        }
        #endregion
    }
}

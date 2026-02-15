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
using System.Text.RegularExpressions;
using static ProjectVagabond.GameEvents;
using static System.Net.Mime.MediaTypeNames;

namespace ProjectVagabond.Scenes
{
    public class BattleScene : GameScene
    {
        private const float MULTI_HIT_DELAY = 0.2f; // Was 0.05f
        private const float ACTION_EXECUTION_DELAY = 0.0f;
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
        private Core _core;
        private GraphicsDevice _graphicsDevice;
        private Texture2D _pixel;
        private BattleCameraController _battleCam;

        private List<int> _enemyEntityIds = new List<int>();
        private BattleManager.BattlePhase _previousBattlePhase;
        private bool _isBattleOver;
        private float _endOfBattleTimer;
        private const float END_OF_BATTLE_DELAY = 2.0f;
        private BattleCombatant _currentActor;
        private bool _isWaitingForMultiHitDelay = false;
        private float _multiHitDelayTimer = 0f;
        private readonly Queue<Action> _pendingAnimations = new Queue<Action>();
        private readonly Random _random = new Random();

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
        private bool _floorTransitionTriggered = false;
        private bool _didFlee = false;
        private bool _isBattleLogHovered = false;

        public BattleAnimationManager AnimationManager => _animationManager;

        public BattleScene()
        {
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
            _core = ServiceLocator.Get<Core>();
            _graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            _pixel = ServiceLocator.Get<Texture2D>();

            _uiManager = new BattleUIManager();
            _renderer = new BattleRenderer();
            _animationManager = new BattleAnimationManager();
            _moveAnimationManager = new MoveAnimationManager();
            _inputHandler = new BattleInputHandler();
            _alertManager = new AlertManager();
            _battleLogManager = new BattleLogManager();
            _battleCam = new BattleCameraController();
        }

        public override void Enter()
        {
            base.Enter();
            _uiManager.Reset();
            _renderer.Reset();
            _animationManager.Reset();

            _animationManager.GetCombatantPosition = (c) =>
            {
                if (_battleManager == null) return Vector2.Zero;
                return _renderer.GetCombatantVisualCenterPosition(c, _battleManager.AllCombatants);
            };

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
            _isFadingOutOnDeath = false;
            _deathFadeTimer = 0f;
            _processedDeathAnimations.Clear();
            _watchdogTimer = 0f;
            _switchSequenceState = SwitchSequenceState.None;
            _victorySequenceTriggered = false;
            _floorTransitionTriggered = false;
            _didFlee = false;
            _isBattleLogHovered = false;

            SubscribeToEvents();

            _roundAnimState = RoundAnimState.Hidden;
            _roundAnimTimer = 0f;
            _lastRoundNumber = 1;

            if (_gameState.PlayerState == null || _gameState.PlayerState.Party.Count == 0)
            {
                Debug.WriteLine($"[BattleScene] [FATAL] PlayerState is invalid! Aborting battle setup.");
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

                _uiManager.IntroOffset = Vector2.Zero;

                var leader = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == 0);
                if (leader != null)
                {
                    _uiManager.ShowActionMenu(leader, _battleManager.AllCombatants.ToList());
                    _uiManager.HideButtonsForEntrance();
                }

                var enemies = _battleManager.AllCombatants.Where(c => !c.IsPlayerControlled && c.IsActiveOnField).OrderBy(c => c.BattleSlot).ToList();
                foreach (var e in enemies)
                {
                    _introSequenceQueue.Enqueue(e);
                    e.VisualAlpha = 0f;
                }

                // Always start floor 0 and 1 animations, regardless of enemy count.
                _animationManager.StartFloorIntroAnimation("floor_0");
                _animationManager.StartFloorIntroAnimation("floor_1");

                foreach (var combatant in _battleManager.AllCombatants)
                {
                    if (combatant.IsPlayerControlled) combatant.VisualHP = combatant.Stats.CurrentHP;
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
            UnsubscribeFromEvents();
            _battleLogManager.Unsubscribe();
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
            EventBus.Subscribe<GameEvents.TenacityChanged>(OnTenacityChanged);
            EventBus.Subscribe<GameEvents.TenacityBroken>(OnTenacityBroken);

            _uiManager.OnForcedSwitchSelected += OnForcedSwitchSelected;
            _uiManager.OnFleeRequested += FleeBattle;
            _inputHandler.OnBackRequested += () => _uiManager.GoBack();
        }

        private void UnsubscribeFromEvents()
        {
            EventBus.Unsubscribe<GameEvents.ActionDeclared>(OnActionDeclared);
            EventBus.Unsubscribe<GameEvents.BattleActionExecuted>(OnBattleActionExecuted);
            EventBus.Unsubscribe<GameEvents.CombatantDefeated>(OnCombatantDefeated);
            EventBus.Unsubscribe<GameEvents.ActionFailed>(OnActionFailed);
            EventBus.Unsubscribe<GameEvents.StatusEffectTriggered>(OnStatusEffectTriggered);
            EventBus.Unsubscribe<GameEvents.CombatantHealed>(OnCombatantHealed);
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
            EventBus.Unsubscribe<GameEvents.TenacityChanged>(OnTenacityChanged);
            EventBus.Unsubscribe<GameEvents.TenacityBroken>(OnTenacityBroken);

            _uiManager.OnForcedSwitchSelected -= OnForcedSwitchSelected;
            _uiManager.OnFleeRequested -= FleeBattle;
            _inputHandler.OnBackRequested -= () => _uiManager.GoBack();
        }

        private void SetupBattle()
        {
            var gameState = _gameState;
            var playerParty = new List<BattleCombatant>();

            var leaderMember = gameState.PlayerState.Leader;
            if (leaderMember != null)
            {
                var leaderCombatant = BattleCombatantFactory.CreatePlayer(leaderMember, "player_leader");
                leaderCombatant.BattleSlot = 0;
                playerParty.Add(leaderCombatant);
            }

            for (int i = 1; i < gameState.PlayerState.Party.Count; i++)
            {
                var member = gameState.PlayerState.Party[i];
                var memberCombatant = BattleCombatantFactory.CreatePlayer(member, $"player_ally_{i}");
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

                var enemyCombatant = BattleCombatantFactory.CreateEnemy(archetypeId, $"enemy_{i + 1}");
                if (enemyCombatant != null)
                {
                    enemyCombatant.BattleSlot = enemyParty.Count;
                    enemyParty.Add(enemyCombatant);
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
            _battleCam.Update(dt);

            _battleLogManager.Update(gameTime);

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

                    // _uiManager.IntroOffset = Vector2.Lerp(new Vector2(0, UI_SLIDE_DISTANCE), Vector2.Zero, eased);

                    if (progress >= 1.0f)
                    {
                        _battleManager.ForceAdvance();
                        _roundAnimState = RoundAnimState.Pop;
                        _roundAnimTimer = 0f;

                        foreach (var c in _battleManager.AllCombatants)
                        {
                            _animationManager.StartHudEntryAnimation(c.CombatantID);
                        }

                        // Trigger the button pop-in sequence
                        _uiManager.TriggerButtonEntrance();
                    }
                }

                _animationManager.Update(gameTime, _battleManager.AllCombatants);
                _renderer.Update(gameTime, _battleManager.AllCombatants, _animationManager, null);
                _uiManager.Update(gameTime, currentMouseState, currentKeyboardState, null, _renderer, isInputBlocked: true);
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

            // --- INPUT HANDLING REFACTOR ---
            // Update UI Manager and check if it captured input
            bool uiCapturedInput = _uiManager.Update(gameTime, currentMouseState, currentKeyboardState, _battleManager.CurrentActingCombatant, _renderer);

            // Only update World Input Handler if UI didn't capture input
            if (!uiCapturedInput)
            {
                _inputHandler.Update(gameTime, _uiManager, _renderer);
            }
            else
            {
                // If UI captured input, ensure World Input Handler resets its hover state
                _inputHandler.ResetHover(_uiManager);
            }

            var activeCombatant = _battleManager.CurrentActingCombatant ?? _currentActor;

            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection)
            {
                activeCombatant = null;
            }

            _renderer.Update(gameTime, _battleManager.AllCombatants, _animationManager, activeCombatant);

            _alertManager.Update(gameTime);
            _tooltipManager.Update(gameTime);

            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                if (_uiManager.UIState == BattleUIState.Targeting)
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
                            var transitionOut = _transitionManager.GetRandomTransition();
                            var transitionIn = _transitionManager.GetRandomTransition();
                            _sceneManager.ChangeScene(BattleSetup.ReturnSceneState, transitionOut, transitionIn);
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
                            if (!_floorTransitionTriggered)
                            {
                                _uiManager.ForceClearNarration();
                                if (!SplitMapScene.WasMajorBattle)
                                {
                                    _animationManager.StartFloorOutroAnimation("floor_0");
                                    _animationManager.StartFloorOutroAnimation("floor_1");
                                }
                                _floorTransitionTriggered = true;
                            }
                            else
                            {
                                bool floorsBusy = _animationManager.IsFloorAnimatingOut("floor_0") || _animationManager.IsFloorAnimatingOut("floor_1");
                                if (!floorsBusy)
                                {
                                    FinalizeVictory();
                                }
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
                string stallReport = $"[BATTLE WATCHDOG] STALL DETECTED (>{WATCHDOG_TIMEOUT}s) - RECOVERING\n" +
                                     $"Phase: {_battleManager.CurrentPhase}\n" +
                                     $"UI Busy: {_uiManager.IsBusy}\n" +
                                     $"Anim Busy: {_animationManager.IsBlockingAnimation}\n" +
                                     $"MoveAnim Busy: {_moveAnimationManager.IsAnimating}\n" +
                                     $"Pending Anims: {_pendingAnimations.Count}\n" +
                                     $"Switch State: {_switchSequenceState}\n" +
                                     $"MultiHit Wait: {_isWaitingForMultiHitDelay}";

                Debug.WriteLine(stallReport);

                _battleManager.ForceAdvance();
                _uiManager.Reset();
                _animationManager.Reset();
                _moveAnimationManager.SkipAll();
                _watchdogTimer = 0f;
                _isWaitingForMultiHitDelay = false;
                _pendingAnimations.Clear();
                _switchSequenceState = SwitchSequenceState.None;
            }

            _lastFramePhase = _battleManager.CurrentPhase;
            _wasUiBusyLastFrame = isUiBusy;
            _wasAnimatingLastFrame = isAnimBusy;

            if (_isWaitingForMultiHitDelay)
            {
                _multiHitDelayTimer += dt;
                if (_multiHitDelayTimer < MULTI_HIT_DELAY) return;
            }

            _battleManager.Update(dt);

            var currentPhase = _battleManager.CurrentPhase;
            if (currentPhase != _previousBattlePhase)
            {
                HandlePhaseChange(currentPhase);
                _previousBattlePhase = currentPhase;
            }

            // --- SAFETY: Ensure UI is visible in ActionSelection ---
            if (currentPhase == BattleManager.BattlePhase.ActionSelection && !_uiManager.IsActionMenuVisible)
            {
                var leader = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == 0);
                if (leader == null) leader = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && !c.IsDefeated);
                if (leader != null)
                {
                    _uiManager.ShowActionMenu(leader, _battleManager.AllCombatants.ToList());
                }
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
            // Nothing to restore
        }

        private void FinalizeVictory()
        {
            SplitMapScene.PlayerWonLastBattle = true;
            DecrementTemporaryBuffs();
            var transitionOut = _transitionManager.GetRandomTransition();
            var transitionIn = _transitionManager.GetRandomTransition();
            _sceneManager.ChangeScene(BattleSetup.ReturnSceneState, transitionOut, transitionIn);
        }

        private void DecrementTemporaryBuffs()
        {
            var gameState = _gameState;
            var leader = gameState.PlayerState.Leader;
            if (leader == null) return;

            for (int i = leader.ActiveBuffs.Count - 1; i >= 0; i--)
            {
                leader.ActiveBuffs[i].RemainingBattles--;
                if (leader.ActiveBuffs[i].RemainingBattles <= 0) leader.ActiveBuffs.RemoveAt(i);
            }
        }

        private void HandlePhaseChange(BattleManager.BattlePhase newPhase)
        {
            if (newPhase == BattleManager.BattlePhase.EndOfTurn ||
                newPhase == BattleManager.BattlePhase.BattleOver ||
                newPhase == BattleManager.BattlePhase.ActionSelection)
            {
                _currentActor = null;
                ResetCamera();
            }

            if (newPhase == BattleManager.BattlePhase.ActionSelection)
            {
                var leader = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == 0);
                if (leader == null) leader = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && !c.IsDefeated);

                if (leader != null)
                {
                    _uiManager.ShowActionMenu(leader, _battleManager.AllCombatants.ToList());
                }
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

            var secondaryFont = _core.SecondaryFont;

            // 1. End the batch started by GameScene
            spriteBatch.End();

            // 2. Calculate Camera Transform
            Matrix camMatrix = _battleCam.GetTransform();
            Matrix worldTransform = camMatrix * transform;

            // 3. Begin World Batch (Zoomed)
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                null,
                null,
                null,
                worldTransform
            );

            BattleCombatant renderContextActor = _currentActor;
            if (_battleManager != null && _battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection)
            {
                renderContextActor = null;
            }

            _renderer.Draw(spriteBatch, font, gameTime, _battleManager.AllCombatants, renderContextActor, _uiManager, _inputHandler, _animationManager, _uiManager.SharedPulseTimer, worldTransform);

            bool isFlashing = _animationManager.GetImpactFlashState() != null;

            if (!isFlashing)
            {
                _moveAnimationManager.Draw(spriteBatch);
                _animationManager.DrawDamageIndicators(spriteBatch, _core.SecondaryFont);
                _animationManager.DrawAbilityIndicators(spriteBatch, _core.SecondaryFont);
            }

            spriteBatch.End();

            _particleSystemManager.Draw(spriteBatch, worldTransform);

            // 4. Begin UI Batch (Fixed)
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                null,
                null,
                null,
                transform
            );

            if (_roundAnimState != RoundAnimState.Hidden)
            {
                string roundText = _battleManager.RoundNumber.ToString();
                Vector2 roundTextSize = font.MeasureString(roundText);

                // Positioned at (5, 8) as requested previously
                Vector2 roundTextPosition = new Vector2(5, 8);

                Vector2 origin = roundTextSize / 2f;
                Vector2 drawPos = roundTextPosition + origin;
                float scale = 1.0f;
                float rotation = 0f;
                Color color = _global.Palette_DarkShadow;

                if (_roundAnimState == RoundAnimState.Entering)
                {
                    float progress = Math.Clamp(_roundAnimTimer / ROUND_ANIM_ENTER_DURATION, 0f, 1f);
                    scale = Easing.EaseOutBack(progress);
                }
                else if (_roundAnimState == RoundAnimState.Pop)
                {
                    float progress = Math.Clamp(_roundAnimTimer / ROUND_ANIM_POP_DURATION, 0f, 1f);
                    scale = MathHelper.Lerp(1.0f, ROUND_MAX_SCALE, Easing.EaseOutCubic(progress));
                    color = Color.Lerp(_global.Palette_DarkShadow, Color.White, progress);
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
                    color = Color.Lerp(Color.White, _global.Palette_DarkShadow, progress);
                }
                spriteBatch.DrawStringSnapped(font, roundText, drawPos, color, rotation, origin, scale, SpriteEffects.None, 0f);
            }

            if (!isFlashing)
            {
                _uiManager.Draw(spriteBatch, font, gameTime, transform);
            }

            _battleLogManager.Draw(spriteBatch);

            spriteBatch.End();

            // 5. Handle Flashing Overlay
            if (isFlashing)
            {
                var core = _core;
                core.RequestFullscreenOverlay((sb, uiMatrix) =>
                {
                    // uiMatrix is the screen transform passed by Core
                    Matrix overlayWorldTransform = camMatrix * uiMatrix;

                    // World Space Elements on top of flash
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, overlayWorldTransform);
                    _moveAnimationManager.Draw(sb);
                    _animationManager.DrawDamageIndicators(sb, _core.SecondaryFont);
                    _animationManager.DrawAbilityIndicators(sb, _core.SecondaryFont);
                    sb.End();

                    // UI Space Elements on top of flash
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, uiMatrix);
                    _uiManager.Draw(sb, font, gameTime, Matrix.Identity);
                    sb.End();
                });
            }

            // 6. Re-open a dummy batch so GameScene.Draw doesn't crash on End()
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                null,
                null,
                null,
                transform
            );
        }

        public override void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_isFadingOutOnDeath)
            {
                float alpha = Math.Clamp(_deathFadeTimer / Global.UniversalSlowFadeDuration, 0f, 1f);
                var pixel = _pixel;
                var graphicsDevice = _graphicsDevice;
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
            _animationManager.StartDeathAnimation(target.CombatantID);
            target.VisualAlpha = 1.0f;
        }

        private void OnForcedSwitchSelected(BattleCombatant targetMember)
        {
            _battleManager.SubmitInteractionResult(targetMember);
            _uiManager.HideAllMenus();
        }

        private void OnActionDeclared(GameEvents.ActionDeclared e)
        {
            _currentActor = e.Actor;
        }

        private void OnMoveAnimationTriggered(GameEvents.MoveAnimationTriggered e)
        {
            // 1. Calculate the center of ALL targets
            Vector2 camTarget = GetCombatantCentroid(e.Targets);

            // 2. Fallback to actor if no targets (e.g. self-buff)
            if (camTarget == Vector2.Zero)
            {
                camTarget = _renderer.GetCombatantVisualCenterPosition(e.Actor, _battleManager.AllCombatants);
            }

            // 3. Set Focus immediately
            SetCameraFocus(camTarget, 1.01f);

            // Existing logic...
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

            Vector2 camTarget = GetCombatantCentroid(e.Targets);

            if (camTarget == Vector2.Zero)
            {
                camTarget = _renderer.GetCombatantVisualCenterPosition(e.Actor, _battleManager.AllCombatants);
            }

            SetCameraFocus(camTarget, 1.01f);

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
                Vector2 targetPos = _renderer.GetCombatantVisualCenterPosition(target, _battleManager.AllCombatants);

                // --- Status Move Visuals ---
                if (e.ChosenMove.ImpactType == ImpactType.Status && !result.WasGraze && !result.WasProtected)
                {
                    var statusParticles = _particleSystemManager.CreateEmitter(ParticleEffects.CreateStatusImpact());
                    statusParticles.Position = targetPos;
                    statusParticles.EmitBurst(statusParticles.Settings.BurstCount);

                    // Subtle wobble for feedback without heavy impact feel
                    _hapticsManager.TriggerWobble(2.0f, 0.2f, 15f);
                }

                if (result.DamageAmount > 0)
                {
                    target.HealthBarVisibleTimer = 6.0f;
                    float damageRatio = Math.Clamp((float)result.DamageAmount / target.Stats.MaxHP, 0f, 1f);

                    bool isHeavyHit = result.WasCritical || damageRatio > 0.25f || target.Stats.CurrentHP <= 0;

                    // Tuning: Reduced scalar from 3.0f to 1.0f and cap from 5.0f to 2.0f for snappier feel
                    const float BASE_JUICE_SCALAR = 1.0f;
                    float juiceIntensity = 1.0f + (damageRatio * BASE_JUICE_SCALAR);
                    if (result.WasCritical) juiceIntensity *= 1.2f;
                    juiceIntensity = Math.Min(juiceIntensity, 2.0f);

                    if (!result.WasGraze)
                    {
                        float baseFreeze = isHeavyHit ? _global.HitstopDuration_Crit : 0.05f;
                        _hitstopManager.Trigger(baseFreeze * juiceIntensity);
                        _animationManager.StartHitstopVisuals(target.CombatantID, result.WasCritical);

                        if (target.IsPlayerControlled && isHeavyHit)
                        {
                            _core.TriggerFullscreenFlash(Color.White, 0.15f * juiceIntensity);
                            _core.TriggerScreenFlashSequence(_global.Palette_Rust);
                        }

                        _hapticsManager.TriggerCompoundShake(0.25f * juiceIntensity);
                    }
                    Vector2 attackerPos = _renderer.GetCombatantVisualCenterPosition(e.Actor, _battleManager.AllCombatants);

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

                    if (result.WasVulnerable)
                    {
                        _animationManager.StartDamageNumberIndicator(target.CombatantID, result.DamageAmount, hudPosition);
                        _animationManager.StartDamageIndicator(target.CombatantID, "VULNERABLE", hudPosition + new Vector2(0, -10), _global.VulnerableDamageIndicatorColor);
                    }
                    else if (result.WasCritical || (result.DamageAmount >= baselineDamage * 1.5f && baselineDamage > 0))
                    {
                        _animationManager.StartEmphasizedDamageNumberIndicator(target.CombatantID, result.DamageAmount, hudPosition);
                    }
                    else
                    {
                        _animationManager.StartDamageNumberIndicator(target.CombatantID, result.DamageAmount, hudPosition);
                    }
                }
                if (result.WasGraze) _animationManager.StartDamageIndicator(target.CombatantID, "GRAZE", hudPosition, _global.GrazeIndicatorColor);
                if (result.WasCritical)
                {
                    _animationManager.StartDamageIndicator(target.CombatantID, "CRITICAL HIT", hudPosition, _global.CritcalHitIndicatorColor);
                }
                if (result.WasProtected) _animationManager.StartProtectedIndicator(target.CombatantID, hudPosition);
            }
        }

        private void OnTenacityChanged(GameEvents.TenacityChanged e)
        {
            Vector2 hudPos = _renderer.GetCombatantHudCenterPosition(e.Combatant, _battleManager.AllCombatants);
            Vector2 visualPos = _renderer.GetCombatantVisualCenterPosition(e.Combatant, _battleManager.AllCombatants);
            var sparks = _particleSystemManager.CreateEmitter(ParticleEffects.CreateSparks());
            sparks.Position = visualPos;
            sparks.Settings.StartColor = _global.Palette_Sky;
            sparks.Settings.EndColor = Color.White;
            sparks.EmitBurst(5);
        }

        private void OnTenacityBroken(GameEvents.TenacityBroken e)
        {
            Vector2 hudPos = _renderer.GetCombatantHudCenterPosition(e.Combatant, _battleManager.AllCombatants);
            _animationManager.StartDamageIndicator(e.Combatant.CombatantID, "TENACITY BROKEN", hudPos + new Vector2(0, -15), _global.TenacityBrokenIndicatorColor);
            _hapticsManager.TriggerShake(5f, 0.2f);
            _core.TriggerFullscreenFlash(_global.TenacityBrokenIndicatorColor, 0.1f);
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

        private void SetCameraFocus(Vector2 targetPos, float zoom)
        {
            if (targetPos == Vector2.Zero)
            {
                targetPos = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);
            }
            _battleCam.SetTarget(targetPos, zoom);
        }

        private void ResetCamera()
        {
            SetCameraFocus(new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f), 1.0f);
        }

        private Vector2 GetCombatantCentroid(IList<BattleCombatant> combatants)
        {
            if (combatants == null || combatants.Count == 0) return Vector2.Zero;

            Vector2 sum = Vector2.Zero;
            foreach (var c in combatants)
            {
                sum += _renderer.GetCombatantVisualCenterPosition(c, _battleManager.AllCombatants);
            }
            return sum / combatants.Count;
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    /// <summary>
    /// A scene dedicated to managing and rendering a turn-based battle.
    /// </summary>
    public class BattleScene : GameScene
    {
        private BattleManager _battleManager;
        private List<int> _enemyEntityIds = new List<int>();

        // UI Components
        private BattleNarrator _battleNarrator;
        private ActionMenu _actionMenu;
        private ItemMenu _itemMenu;
        private ImageButton _settingsButton;

        private ComponentStore _componentStore;
        private SceneManager _sceneManager;
        private SpriteManager _spriteManager;
        private Global _global;
        private HapticsManager _hapticsManager;

        // State Tracking
        private BattleManager.BattlePhase _previousBattlePhase;
        private bool _isBattleOver;
        private float _endOfBattleTimer;
        private const float END_OF_BATTLE_DELAY = 2.0f; // Seconds to wait before exiting
        private BattleCombatant _currentActor;

        // UI State
        private enum BattleUIState { Default, Targeting }
        private enum BattleSubMenuState { None, ActionRoot, ActionMoves, Item }
        private BattleUIState _uiState = BattleUIState.Default;
        private BattleSubMenuState _subMenuState = BattleSubMenuState.None;
        private MoveData _moveForTargeting;
        private List<TargetInfo> _currentTargets = new List<TargetInfo>();
        private int _hoveredTargetIndex = -1;
        private struct TargetInfo { public BattleCombatant Combatant; public Rectangle Bounds; }

        // Health & Alpha Animation
        private class HealthAnimationState
        {
            public string CombatantID;
            public float StartHP;
            public float TargetHP;
            public float Timer;
            public const float Duration = 1.0f;
        }
        private class AlphaAnimationState
        {
            public string CombatantID;
            public float StartAlpha;
            public float TargetAlpha;
            public float Timer;
            public const float Duration = 0.167f;
        }
        private class HitAnimationState
        {
            public string CombatantID;
            public float Timer;
            public const float Duration = 1.0f;
        }
        private readonly List<HealthAnimationState> _activeHealthAnimations = new List<HealthAnimationState>();
        private readonly List<AlphaAnimationState> _activeAlphaAnimations = new List<AlphaAnimationState>();
        private readonly List<HitAnimationState> _activeHitAnimations = new List<HitAnimationState>();
        private readonly Queue<Action> _narrationQueue = new Queue<Action>();
        private readonly Random _random = new Random();

        // Hover Highlight Animation
        private class HoverHighlightState
        {
            public MoveData CurrentMove;
            public List<BattleCombatant> Targets = new List<BattleCombatant>();
            public int CurrentTargetIndex = 0;
            public float Timer = 0f;

            public const float StartDelay = 0.5f;
            public const float FadeDuration = 0.6f;
            public const float HoldDuration = 0.0f;
        }
        private readonly HoverHighlightState _hoverHighlightState = new HoverHighlightState();


        // Layout Constants
        private const int DIVIDER_Y = 105;
        private const int MAX_ENEMIES = 5;
        private const float PLAYER_INDICATOR_BOB_SPEED = 1.5f;

        public BattleScene()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            // The battle scene animation can encompass the whole screen.
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            base.Initialize();
            var narratorBounds = new Rectangle(0, DIVIDER_Y, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - DIVIDER_Y);
            _battleNarrator = new BattleNarrator(narratorBounds);
            _actionMenu = new ActionMenu();
            _itemMenu = new ItemMenu();
        }

        public override void Enter()
        {
            base.Enter();

            _actionMenu.ResetAnimationState();
            _uiState = BattleUIState.Default;
            _subMenuState = BattleSubMenuState.None;
            _enemyEntityIds.Clear();
            _activeHealthAnimations.Clear();
            _activeAlphaAnimations.Clear();
            _activeHitAnimations.Clear();
            _narrationQueue.Clear();
            _currentActor = null;

            _isBattleOver = false;
            _endOfBattleTimer = 0f;

            EventBus.Subscribe<GameEvents.BattleActionExecuted>(OnBattleActionExecuted);
            EventBus.Subscribe<GameEvents.CombatantDefeated>(OnCombatantDefeated);
            _actionMenu.OnMoveSelected += OnPlayerMoveSelected;
            _actionMenu.OnItemMenuRequested += OnItemMenuRequested;
            _actionMenu.OnMovesMenuOpened += () => _subMenuState = BattleSubMenuState.ActionMoves;
            _actionMenu.OnMainMenuOpened += () => _subMenuState = BattleSubMenuState.ActionRoot;
            _actionMenu.OnFleeRequested += FleeBattle;
            _itemMenu.OnBack += OnItemMenuBack;

            if (_settingsButton == null)
            {
                var settingsIcon = _spriteManager.SettingsIconSprite;
                var buttonSize = 16;
                if (settingsIcon != null) buttonSize = Math.Max(settingsIcon.Width, settingsIcon.Height);
                _settingsButton = new ImageButton(new Rectangle(0, 0, buttonSize, buttonSize), settingsIcon)
                {
                    UseScreenCoordinates = true
                };
            }
            _settingsButton.OnClick += OpenSettings;
            _settingsButton.ResetAnimationState();

            var gameState = ServiceLocator.Get<GameState>();
            int playerEntityId = gameState.PlayerEntityId;
            var playerCombatant = BattleCombatantFactory.CreateFromEntity(playerEntityId, "player_1");
            var playerParty = new List<BattleCombatant> { playerCombatant };

            var enemyParty = new List<BattleCombatant>();
            var enemyArchetypesToSpawn = BattleSetup.EnemyArchetypes;

            if (enemyArchetypesToSpawn != null && enemyArchetypesToSpawn.Any())
            {
                int enemyCount = Math.Min(enemyArchetypesToSpawn.Count, MAX_ENEMIES);
                for (int i = 0; i < enemyCount; i++)
                {
                    string archetypeId = enemyArchetypesToSpawn[i];
                    int newEnemyId = Spawner.Spawn(archetypeId, new Vector2(-1, -1));
                    if (newEnemyId != -1)
                    {
                        var enemyCombatant = BattleCombatantFactory.CreateFromEntity(newEnemyId, $"enemy_{i + 1}");
                        if (enemyCombatant != null)
                        {
                            enemyParty.Add(enemyCombatant);
                            _enemyEntityIds.Add(newEnemyId);
                        }
                    }
                }
                BattleSetup.EnemyArchetypes = null; // Clear after use
            }
            else
            {
                // Fallback to a single default enemy if none are specified
                int defaultEnemyId = Spawner.Spawn("wanderer", new Vector2(-1, -1));
                if (defaultEnemyId != -1)
                {
                    var enemyCombatant = BattleCombatantFactory.CreateFromEntity(defaultEnemyId, "enemy_1");
                    if (enemyCombatant != null)
                    {
                        enemyParty.Add(enemyCombatant);
                        _enemyEntityIds.Add(defaultEnemyId);
                    }
                }
            }

            if (playerCombatant == null || !enemyParty.Any())
            {
                Debug.WriteLine("[BattleScene] [FATAL] Failed to create one or more combatants. Aborting.");
                _battleManager = null;
                return;
            }

            _battleManager = new BattleManager(playerParty, enemyParty);
            _previousBattlePhase = _battleManager.CurrentPhase;
        }

        public override void Exit()
        {
            base.Exit();
            EventBus.Unsubscribe<GameEvents.BattleActionExecuted>(OnBattleActionExecuted);
            EventBus.Unsubscribe<GameEvents.CombatantDefeated>(OnCombatantDefeated);
            _actionMenu.OnMoveSelected -= OnPlayerMoveSelected;
            _actionMenu.OnItemMenuRequested -= OnItemMenuRequested;
            _actionMenu.OnMovesMenuOpened -= () => _subMenuState = BattleSubMenuState.ActionMoves;
            _actionMenu.OnMainMenuOpened -= () => _subMenuState = BattleSubMenuState.ActionRoot;
            _actionMenu.OnFleeRequested -= FleeBattle;
            _itemMenu.OnBack -= OnItemMenuBack;
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;

            if (_enemyEntityIds.Any())
            {
                var entityManager = ServiceLocator.Get<EntityManager>();
                var componentStore = ServiceLocator.Get<ComponentStore>();
                foreach (var id in _enemyEntityIds)
                {
                    componentStore.EntityDestroyed(id);
                    entityManager.DestroyEntity(id);
                }
                _enemyEntityIds.Clear();
            }
        }

        private void OpenSettings()
        {
            _sceneManager.ShowModal(GameSceneState.Settings);
        }

        private void FleeBattle()
        {
            _isBattleOver = true;
            _actionMenu.Hide();
            _itemMenu.Hide();

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
            string fleeMessage = player != null ? $"{player.Name} escaped." : "Got away safely!";
            _narrationQueue.Enqueue(() => _battleNarrator.Show(fleeMessage, secondaryFont));
        }

        private void OnItemMenuRequested()
        {
            _subMenuState = BattleSubMenuState.Item;
            _actionMenu.Hide();
            _itemMenu.Show();
        }

        private void OnItemMenuBack()
        {
            _subMenuState = BattleSubMenuState.ActionRoot;
            _itemMenu.Hide();
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && !c.IsDefeated);
            if (player != null)
            {
                _actionMenu.Show(player, _battleManager.AllCombatants.ToList());
            }
        }

        private void OnBattleActionExecuted(GameEvents.BattleActionExecuted e)
        {
            _narrationQueue.Clear();
            _currentActor = e.Actor;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            if (e.Actor.HasStatusEffect(StatusEffectType.Stun))
            {
                _narrationQueue.Enqueue(() => _battleNarrator.Show($"{e.Actor.Name} is stunned and cannot move!", secondaryFont));
                return;
            }

            if (e.ChosenMove.Target == TargetType.None || !e.Targets.Any())
            {
                _narrationQueue.Enqueue(() => _battleNarrator.Show($"{e.Actor.Name} uses {e.ChosenMove.MoveName}!", secondaryFont));
                return;
            }

            // Single narration for the move itself
            string attackNarration = $"{e.Actor.Name} uses {e.ChosenMove.MoveName}!";
            _narrationQueue.Enqueue(() => _battleNarrator.Show(attackNarration, secondaryFont));

            // A single lambda to start all animations at once
            _narrationQueue.Enqueue(() =>
            {
                for (int i = 0; i < e.Targets.Count; i++)
                {
                    var target = e.Targets[i];
                    var result = e.DamageResults[i];

                    if (result.DamageAmount > 0)
                    {
                        if (target.IsPlayerControlled)
                        {
                            _core.TriggerFullscreenFlash(_global.Palette_Red, 0.15f);
                            _core.TriggerFullscreenGlitch(duration: 0.2f);
                            _hapticsManager.TriggerShake(magnitude: 2.0f, duration: 0.3f);
                        }
                        StartHealthAnimation(target.CombatantID, (int)target.VisualHP, target.Stats.CurrentHP);
                        StartHitAnimation(target.CombatantID);
                    }
                }
            });

            // Queue up individual narrations for results after the animations have started
            for (int i = 0; i < e.Targets.Count; i++)
            {
                var target = e.Targets[i];
                var result = e.DamageResults[i];

                if (result.WasCritical)
                {
                    _narrationQueue.Enqueue(() => _battleNarrator.Show($"A critical hit on {target.Name}!", secondaryFont));
                }
            }
        }

        private void OnCombatantDefeated(GameEvents.CombatantDefeated e)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            _narrationQueue.Enqueue(() => _battleNarrator.Show($"{e.DefeatedCombatant.Name} was defeated!", secondaryFont));
            StartAlphaAnimation(e.DefeatedCombatant.CombatantID, e.DefeatedCombatant.VisualAlpha, 0.1f);
        }

        private void StartHealthAnimation(string combatantId, int hpBefore, int hpAfter)
        {
            _activeHealthAnimations.Add(new HealthAnimationState
            {
                CombatantID = combatantId,
                StartHP = hpBefore,
                TargetHP = hpAfter,
                Timer = 0f
            });
        }

        private void StartAlphaAnimation(string combatantId, float alphaBefore, float alphaAfter)
        {
            _activeAlphaAnimations.Add(new AlphaAnimationState
            {
                CombatantID = combatantId,
                StartAlpha = alphaBefore,
                TargetAlpha = alphaAfter,
                Timer = 0f
            });
        }

        private void StartHitAnimation(string combatantId)
        {
            _activeHitAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeHitAnimations.Add(new HitAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        private void OnPlayerMoveSelected(MoveData move, BattleCombatant target)
        {
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
            if (player != null)
            {
                var action = new QueuedAction
                {
                    Actor = player,
                    Target = target,
                    ChosenMove = move,
                    Priority = move.Priority,
                    ActorAgility = player.Stats.Agility
                };
                _battleManager.SetPlayerAction(action);
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_battleManager == null)
            {
                base.Update(gameTime);
                return;
            }

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

            // --- Handle End of Battle State ---
            // If the battle is over (win, lose, or flee), we enter a simplified update loop.
            // This loop only processes animations and narration until they are complete, then starts a timer to exit the scene.
            if (_isBattleOver)
            {
                _battleNarrator.Update(gameTime);
                UpdateHealthAnimations(gameTime);
                UpdateAlphaAnimations(gameTime);
                UpdateHitAnimations(gameTime);
                _settingsButton?.Update(currentMouseState);

                bool animationsAndNarrationComplete = !_battleNarrator.IsBusy && !_activeHealthAnimations.Any() && !_activeAlphaAnimations.Any() && !_narrationQueue.Any();

                // If everything is finished, start the exit timer.
                if (animationsAndNarrationComplete)
                {
                    _endOfBattleTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_endOfBattleTimer >= END_OF_BATTLE_DELAY)
                    {
                        _sceneManager.ChangeScene(GameSceneState.TerminalMap);
                    }
                }
                else if (!_battleNarrator.IsBusy && _narrationQueue.Any())
                {
                    // If animations are done but there's more narration, process it.
                    var nextStep = _narrationQueue.Dequeue();
                    nextStep.Invoke();
                }

                base.Update(gameTime);
                return; // Exit here to prevent normal battle logic from running.
            }

            // --- Normal Battle Update Loop ---
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            _battleNarrator.Update(gameTime);
            UpdateHealthAnimations(gameTime);
            UpdateAlphaAnimations(gameTime);
            UpdateHitAnimations(gameTime);
            UpdateHoverHighlights(gameTime);

            if (_settingsButton != null)
            {
                float scale = _core.FinalScale;
                int buttonVirtualSize = 16;
                int buttonScreenSize = (int)(buttonVirtualSize * scale);

                var screenBounds = _core.GraphicsDevice.PresentationParameters.Bounds;
                const int padding = 5;

                int buttonX = screenBounds.Width - buttonScreenSize - padding;
                int buttonY = padding;

                _settingsButton.Bounds = new Rectangle(buttonX, buttonY, buttonScreenSize, buttonScreenSize);
            }
            _settingsButton?.Update(currentMouseState);

            // --- Animation Skip Logic ---
            bool skipRequested = (UIInputManager.CanProcessMouseClick() && currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed) ||
                                 (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState)) ||
                                 (KeyPressed(Keys.Space, currentKeyboardState, _previousKeyboardState));

            if (_activeHealthAnimations.Any() && skipRequested)
            {
                SkipAllHealthAnimations();
                if (UIInputManager.CanProcessMouseClick() && currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed)
                {
                    UIInputManager.ConsumeMouseClick();
                }
            }

            // Synchronize BattleScene's UI state with the ActionMenu's state
            if (_actionMenu.CurrentMenuState == ActionMenu.MenuState.Targeting)
            {
                _uiState = BattleUIState.Targeting;
                _moveForTargeting = _actionMenu.SelectedMove;
            }
            else
            {
                _uiState = BattleUIState.Default;
            }

            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection)
            {
                switch (_subMenuState)
                {
                    case BattleSubMenuState.ActionRoot:
                    case BattleSubMenuState.ActionMoves:
                        _actionMenu.Update(currentMouseState, gameTime);
                        break;
                    case BattleSubMenuState.Item:
                        _itemMenu.Update(currentMouseState, gameTime);
                        break;
                }

                if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
                {
                    if (_subMenuState == BattleSubMenuState.Item)
                    {
                        OnItemMenuBack();
                    }
                    else
                    {
                        _actionMenu.GoBack();
                    }
                }
            }

            if (_uiState == BattleUIState.Targeting)
            {
                _hoveredTargetIndex = -1;
                for (int i = 0; i < _currentTargets.Count; i++)
                {
                    if (_currentTargets[i].Bounds.Contains(virtualMousePos))
                    {
                        _hoveredTargetIndex = i;
                        break;
                    }
                }

                if (UIInputManager.CanProcessMouseClick() && currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed)
                {
                    if (_hoveredTargetIndex != -1)
                    {
                        var selectedTarget = _currentTargets[_hoveredTargetIndex].Combatant;
                        OnPlayerMoveSelected(_moveForTargeting, selectedTarget);
                        UIInputManager.ConsumeMouseClick();
                    }
                }
            }

            bool canProceed = !_battleNarrator.IsBusy && !_activeHealthAnimations.Any() && !_activeAlphaAnimations.Any();

            if (canProceed)
            {
                if (_narrationQueue.Any())
                {
                    var nextStep = _narrationQueue.Dequeue();
                    nextStep.Invoke();
                }
                else
                {
                    _battleManager.CanAdvance = true;
                }
            }
            else
            {
                _battleManager.CanAdvance = false;
            }

            _battleManager.Update();

            var currentPhase = _battleManager.CurrentPhase;
            if (currentPhase != _previousBattlePhase)
            {
                if (currentPhase == BattleManager.BattlePhase.EndOfTurn || currentPhase == BattleManager.BattlePhase.BattleOver)
                {
                    _currentActor = null;
                }

                if (currentPhase == BattleManager.BattlePhase.ActionSelection)
                {
                    _subMenuState = BattleSubMenuState.ActionRoot;
                    var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && !c.IsDefeated);
                    if (player != null)
                    {
                        _actionMenu.Show(player, _battleManager.AllCombatants.ToList());
                    }
                }
                else
                {
                    _subMenuState = BattleSubMenuState.None;
                    _actionMenu.Hide();
                    _actionMenu.SetState(ActionMenu.MenuState.Main);
                    _itemMenu.Hide();
                }
                _previousBattlePhase = currentPhase;
            }

            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.BattleOver && !_isBattleOver)
            {
                _isBattleOver = true;
                _actionMenu.Hide();
                _itemMenu.Hide();

                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
                if (player != null && player.IsDefeated)
                {
                    _narrationQueue.Enqueue(() => _battleNarrator.Show("Player Loses!", secondaryFont));
                }
                else
                {
                    _narrationQueue.Enqueue(() => _battleNarrator.Show("Player Wins!", secondaryFont));
                }
            }

            base.Update(gameTime);
        }

        private void SkipAllHealthAnimations()
        {
            foreach (var anim in _activeHealthAnimations)
            {
                var combatant = _battleManager.AllCombatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    combatant.VisualHP = anim.TargetHP;
                }
            }
            _activeHealthAnimations.Clear();
        }

        private void UpdateHealthAnimations(GameTime gameTime)
        {
            for (int i = _activeHealthAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHealthAnimations[i];
                var combatant = _battleManager.AllCombatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeHealthAnimations.RemoveAt(i);
                    continue;
                }

                anim.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (anim.Timer >= HealthAnimationState.Duration)
                {
                    combatant.VisualHP = anim.TargetHP;
                    _activeHealthAnimations.RemoveAt(i);
                }
                else
                {
                    float progress = anim.Timer / HealthAnimationState.Duration;
                    combatant.VisualHP = MathHelper.Lerp(anim.StartHP, anim.TargetHP, Easing.EaseOutQuart(progress));
                }
            }
        }

        private void UpdateAlphaAnimations(GameTime gameTime)
        {
            for (int i = _activeAlphaAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeAlphaAnimations[i];
                var combatant = _battleManager.AllCombatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeAlphaAnimations.RemoveAt(i);
                    continue;
                }

                anim.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (anim.Timer >= AlphaAnimationState.Duration)
                {
                    combatant.VisualAlpha = anim.TargetAlpha;
                    _activeAlphaAnimations.RemoveAt(i);
                }
                else
                {
                    float progress = anim.Timer / AlphaAnimationState.Duration;
                    combatant.VisualAlpha = MathHelper.Lerp(anim.StartAlpha, anim.TargetAlpha, Easing.EaseOutQuad(progress));
                }
            }
        }

        private void UpdateHitAnimations(GameTime gameTime)
        {
            for (int i = _activeHitAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHitAnimations[i];
                anim.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (anim.Timer >= HitAnimationState.Duration)
                {
                    _activeHitAnimations.RemoveAt(i);
                }
            }
        }

        private void UpdateHoverHighlights(GameTime gameTime)
        {
            var hoveredMove = _actionMenu.HoveredMove;

            if (hoveredMove != _hoverHighlightState.CurrentMove)
            {
                _hoverHighlightState.CurrentMove = hoveredMove;
                _hoverHighlightState.Targets.Clear();
                _hoverHighlightState.CurrentTargetIndex = 0;
                _hoverHighlightState.Timer = 0f;

                if (hoveredMove != null)
                {
                    var player = _battleManager.AllCombatants.First(c => c.IsPlayerControlled);
                    var enemies = _battleManager.AllCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();
                    var all = _battleManager.AllCombatants.Where(c => !c.IsDefeated).ToList();

                    switch (hoveredMove.Target)
                    {
                        case TargetType.Single: _hoverHighlightState.Targets.AddRange(enemies); break;
                        case TargetType.Every: _hoverHighlightState.Targets.AddRange(enemies); break;
                        case TargetType.Self: _hoverHighlightState.Targets.Add(player); break;
                        case TargetType.SingleAll: _hoverHighlightState.Targets.AddRange(all); break;
                        case TargetType.EveryAll: _hoverHighlightState.Targets.AddRange(all); break;
                    }
                }
            }

            if (_hoverHighlightState.CurrentMove != null && _hoverHighlightState.Targets.Any())
            {
                _hoverHighlightState.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_hoverHighlightState.Timer < HoverHighlightState.StartDelay)
                {
                    return; // Don't start the animation cycle yet
                }

                float effectiveTimer = _hoverHighlightState.Timer - HoverHighlightState.StartDelay;
                float cycleDuration = (HoverHighlightState.FadeDuration * 2) + HoverHighlightState.HoldDuration;

                switch (_hoverHighlightState.CurrentMove.Target)
                {
                    case TargetType.Single:
                    case TargetType.SingleAll:
                        if (effectiveTimer >= cycleDuration)
                        {
                            _hoverHighlightState.Timer = HoverHighlightState.StartDelay; // Reset timer to start of animation
                            _hoverHighlightState.CurrentTargetIndex = (_hoverHighlightState.CurrentTargetIndex + 1) % _hoverHighlightState.Targets.Count;
                        }
                        break;
                    case TargetType.Every:
                    case TargetType.EveryAll:
                    case TargetType.Self:
                        float simpleCycle = HoverHighlightState.FadeDuration * 2;
                        if (effectiveTimer >= simpleCycle)
                        {
                            _hoverHighlightState.Timer = HoverHighlightState.StartDelay; // Reset timer to start of animation
                        }
                        break;
                }
            }
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
            var pixel = ServiceLocator.Get<Texture2D>();
            var global = ServiceLocator.Get<Global>();

            _currentTargets.Clear();
            var enemies = _battleManager.AllCombatants.Where(c => !c.IsPlayerControlled).ToList();
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);

            // --- Draw Enemy HUDs ---
            if (enemies.Any())
            {
                const int enemyAreaPadding = 20;
                const int enemyHudY = 80;
                int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
                int slotWidth = availableWidth / enemies.Count;

                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    var centerPosition = new Vector2(enemyAreaPadding + (i * slotWidth) + (slotWidth / 2), enemyHudY);
                    DrawCombatantHud(spriteBatch, font, secondaryFont, enemy, centerPosition);
                    if (!enemy.IsDefeated)
                    {
                        _currentTargets.Add(new TargetInfo
                        {
                            Combatant = enemy,
                            Bounds = GetCombatantInteractionBounds(enemy, centerPosition, font, secondaryFont)
                        });
                    }
                }
            }

            // --- Draw Player HUD ---
            if (player != null)
            {
                const int playerHudY = DIVIDER_Y - 10;
                const int playerHudPaddingX = 10;
                float yOffset = 0;

                if (_battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection)
                {
                    yOffset = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * PLAYER_INDICATOR_BOB_SPEED * MathF.PI) > 0) ? -1f : 0f;
                }

                // Player Name on the left
                spriteBatch.DrawStringSnapped(font, player.Name, new Vector2(playerHudPaddingX, playerHudY - font.LineHeight + 7 + yOffset), Color.White);

                // Player HP on the right
                string hpLabel = "HP: ";
                string currentHp = ((int)Math.Round(player.VisualHP)).ToString();
                string separator = "/";
                string maxHp = player.Stats.MaxHP.ToString();
                string fullHpText = hpLabel + currentHp + separator + maxHp;
                Vector2 hpTextSize = secondaryFont.MeasureString(fullHpText);

                float hpStartX = Global.VIRTUAL_WIDTH - playerHudPaddingX - hpTextSize.X;
                var playerHitAnim = _activeHitAnimations.FirstOrDefault(a => a.CombatantID == player.CombatantID);
                DrawHpLine(spriteBatch, secondaryFont, player, new Vector2(hpStartX, playerHudY + yOffset), 1.0f, playerHitAnim);
            }

            // --- Draw UI Title ---
            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection)
            {
                string title = "";
                if (_subMenuState == BattleSubMenuState.ActionMoves) title = "ACTIONS";
                else if (_subMenuState == BattleSubMenuState.Item) title = "ITEMS";

                if (!string.IsNullOrEmpty(title))
                {
                    var titleSize = secondaryFont.MeasureString(title);
                    var titleY = DIVIDER_Y - 10 - font.LineHeight + 7;
                    var titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.Width) / 2, titleY);
                    spriteBatch.DrawStringSnapped(secondaryFont, title, titlePos, _global.Palette_LightGray);
                }
            }

            DrawHoverHighlights(spriteBatch, font, secondaryFont);
            DrawTurnIndicator(spriteBatch, font, gameTime);

            // --- Draw Targeting UI ---
            if (_uiState == BattleUIState.Targeting)
            {
                for (int i = 0; i < _currentTargets.Count; i++)
                {
                    Color boxColor = i == _hoveredTargetIndex ? Color.Red : Color.Yellow;
                    var bounds = _currentTargets[i].Bounds;
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Right, bounds.Top), boxColor);
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Bottom), new Vector2(bounds.Right, bounds.Bottom), boxColor);
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Left, bounds.Bottom), boxColor);
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Right, bounds.Top), new Vector2(bounds.Right, bounds.Bottom), boxColor);
                }
            }

            spriteBatch.DrawSnapped(pixel, new Rectangle(0, DIVIDER_Y, Global.VIRTUAL_WIDTH, 1), Color.White);

            if (_subMenuState == BattleSubMenuState.Item)
            {
                _itemMenu.Draw(spriteBatch, font, gameTime, transform);
            }
            else
            {
                _actionMenu.Draw(spriteBatch, font, gameTime, transform);
            }
            _battleNarrator.Draw(spriteBatch, secondaryFont, gameTime);

            if (global.ShowDebugOverlays && _battleManager != null)
            {
                var playerCombatant = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
                if (playerCombatant?.DeckManager != null)
                {
                    DrawDebugDeckInfo(spriteBatch, secondaryFont, playerCombatant.DeckManager);
                }
            }
        }

        public override void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            if (_settingsButton != null)
            {
                _settingsButton.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }

            spriteBatch.End();
        }

        private void DrawDebugDeckInfo(SpriteBatch spriteBatch, BitmapFont font, CombatDeckManager deckManager)
        {
            var global = ServiceLocator.Get<Global>();
            float yOffset = 5f;
            float xOffset = 5f;
            var drawPile = deckManager.DrawPile.ToList();
            var discardPile = deckManager.DiscardPile.ToList();

            // Draw Draw Pile (Queue)
            string drawHeader = $"DRAW PILE ({drawPile.Count})";
            spriteBatch.DrawStringSnapped(font, drawHeader, new Vector2(xOffset, yOffset), global.Palette_Yellow);
            yOffset += font.LineHeight;

            if (!drawPile.Any())
            {
                spriteBatch.DrawStringSnapped(font, "-- EMPTY --", new Vector2(xOffset, yOffset), global.Palette_Gray);
            }
            else
            {
                for (int i = 0; i < Math.Min(drawPile.Count, 10); i++)
                {
                    var move = drawPile[i];
                    Color color = i == 0 ? global.Palette_Red : global.Palette_BrightWhite;
                    spriteBatch.DrawStringSnapped(font, move.MoveName, new Vector2(xOffset, yOffset), color);
                    yOffset += font.LineHeight;
                }
            }

            // Draw Discard Pile
            yOffset = 5f;
            string discardHeader = $"DISCARD ({discardPile.Count})";
            var headerSize = font.MeasureString(discardHeader);
            xOffset = Global.VIRTUAL_WIDTH - headerSize.Width - 5f;

            spriteBatch.DrawStringSnapped(font, discardHeader, new Vector2(xOffset, yOffset), global.Palette_Yellow);
            yOffset += font.LineHeight;

            foreach (var move in discardPile)
            {
                var moveNameSize = font.MeasureString(move.MoveName);
                xOffset = Global.VIRTUAL_WIDTH - moveNameSize.Width - 5f;
                spriteBatch.DrawStringSnapped(font, move.MoveName, new Vector2(xOffset, yOffset), global.Palette_BrightWhite);
                yOffset += font.LineHeight;
            }
        }

        private Rectangle GetCombatantInteractionBounds(BattleCombatant combatant, Vector2 centerPosition, BitmapFont nameFont, BitmapFont statsFont)
        {
            const int spriteSize = 64;
            float spriteTop = centerPosition.Y - spriteSize - 10;

            Vector2 nameSize = nameFont.MeasureString(combatant.Name);
            float nameY = centerPosition.Y - 8;

            string hpLabel = "HP: ";
            string currentHp = ((int)Math.Round(combatant.VisualHP)).ToString();
            string separator = "/";
            string maxHp = combatant.Stats.MaxHP.ToString();
            string fullHpText = hpLabel + currentHp + separator + maxHp;
            Vector2 hpSize = statsFont.MeasureString(fullHpText);
            float hpY = centerPosition.Y + 2;
            float hpBottom = hpY + statsFont.LineHeight;

            float top = spriteTop;
            float bottom = hpBottom;
            float maxWidth = Math.Max(spriteSize, Math.Max(nameSize.X, hpSize.X));

            float left = centerPosition.X - maxWidth / 2;
            float width = maxWidth;
            float height = bottom - top;

            const int padding = 2;
            return new Rectangle(
                (int)left - padding,
                (int)top - padding,
                (int)width + padding * 2,
                (int)height + padding * 2
            );
        }

        private Rectangle GetPlayerInteractionBounds(BitmapFont nameFont, BitmapFont statsFont)
        {
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
            if (player == null) return Rectangle.Empty;

            const int playerHudY = DIVIDER_Y - 10;
            const int playerHudPaddingX = 10;

            Vector2 nameSize = nameFont.MeasureString(player.Name);
            Vector2 namePos = new Vector2(playerHudPaddingX, playerHudY - nameFont.LineHeight + 7);

            string hpLabel = "HP: ";
            string currentHp = ((int)Math.Round(player.VisualHP)).ToString();
            string separator = "/";
            string maxHp = player.Stats.MaxHP.ToString();
            string fullHpText = hpLabel + currentHp + separator + maxHp;
            Vector2 hpTextSize = statsFont.MeasureString(fullHpText);
            float hpStartX = Global.VIRTUAL_WIDTH - playerHudPaddingX - hpTextSize.X;

            int left = (int)namePos.X;
            int right = (int)(hpStartX + hpTextSize.X);
            int top = (int)Math.Min(namePos.Y, playerHudY);
            int bottom = (int)Math.Max(namePos.Y + nameSize.Y, playerHudY + hpTextSize.Y);

            const int padding = 2;
            return new Rectangle(left - padding, top - padding, (right - left) + padding * 2, (bottom - top) + padding * 2);
        }

        private void DrawCombatantHud(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, BattleCombatant combatant, Vector2 centerPosition)
        {
            var global = ServiceLocator.Get<Global>();
            var pixel = ServiceLocator.Get<Texture2D>();
            const int spriteSize = 64;
            var spriteRect = new Rectangle(
                (int)(centerPosition.X - spriteSize / 2),
                (int)(centerPosition.Y - spriteSize - 10),
                spriteSize,
                spriteSize
            );

            Color tintColor = Color.White * combatant.VisualAlpha;

            Texture2D enemySprite = _spriteManager.GetEnemySprite(combatant.ArchetypeId);
            if (enemySprite != null)
            {
                spriteBatch.DrawSnapped(enemySprite, spriteRect, tintColor);
            }
            else
            {
                spriteBatch.DrawSnapped(pixel, spriteRect, global.Palette_Pink * combatant.VisualAlpha);
            }

            Vector2 nameSize = nameFont.MeasureString(combatant.Name);
            Vector2 namePos = new Vector2(centerPosition.X - nameSize.X / 2, centerPosition.Y - 8);
            spriteBatch.DrawStringSnapped(nameFont, combatant.Name, namePos, tintColor);

            string hpLabel = "HP: ";
            string currentHp = ((int)Math.Round(combatant.VisualHP)).ToString();
            string separator = "/";
            string maxHp = combatant.Stats.MaxHP.ToString();
            string fullHpText = hpLabel + currentHp + separator + maxHp;
            Vector2 hpSize = statsFont.MeasureString(fullHpText);
            Vector2 hpPos = new Vector2(centerPosition.X - hpSize.X / 2, centerPosition.Y + 2);
            var hitAnim = _activeHitAnimations.FirstOrDefault(a => a.CombatantID == combatant.CombatantID);
            DrawHpLine(spriteBatch, statsFont, combatant, hpPos, combatant.VisualAlpha, hitAnim);
        }

        private void DrawHpLine(SpriteBatch spriteBatch, BitmapFont statsFont, BattleCombatant combatant, Vector2 position, float alpha = 1.0f, HitAnimationState hitAnim = null)
        {
            var global = ServiceLocator.Get<Global>();
            Color labelColor = global.Palette_LightGray * alpha;
            Color numberColor = Color.White * alpha;
            Vector2 drawPosition = position;

            if (hitAnim != null)
            {
                float progress = hitAnim.Timer / HitAnimationState.Duration;
                float easeOutProgress = Easing.EaseOutCubic(progress);

                // Shake effect
                float shakeMagnitude = 4.0f * (1.0f - easeOutProgress);
                drawPosition.X += (float)(_random.NextDouble() * 2 - 1) * shakeMagnitude;
                drawPosition.Y += (float)(_random.NextDouble() * 2 - 1) * shakeMagnitude;

                // Color flash effect
                Color flashColor = global.Palette_Red;
                labelColor = Color.Lerp(flashColor, global.Palette_LightGray, easeOutProgress) * alpha;
                numberColor = Color.Lerp(flashColor, Color.White, easeOutProgress) * alpha;
            }

            string hpLabel = "HP: ";
            string currentHp = ((int)Math.Round(combatant.VisualHP)).ToString();
            string separator = "/";
            string maxHp = combatant.Stats.MaxHP.ToString();

            spriteBatch.DrawStringSnapped(statsFont, hpLabel, drawPosition, labelColor);
            float currentX = drawPosition.X + statsFont.MeasureString(hpLabel).Width;

            spriteBatch.DrawStringSnapped(statsFont, currentHp, new Vector2(currentX, drawPosition.Y), numberColor);
            currentX += statsFont.MeasureString(currentHp).Width;

            spriteBatch.DrawStringSnapped(statsFont, separator, new Vector2(currentX, drawPosition.Y), labelColor);
            currentX += statsFont.MeasureString(separator).Width;

            spriteBatch.DrawStringSnapped(statsFont, maxHp, new Vector2(currentX, drawPosition.Y), numberColor);
        }

        private void DrawHoverHighlights(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont)
        {
            if (_hoverHighlightState.CurrentMove == null || !_hoverHighlightState.Targets.Any() || _hoverHighlightState.Timer < HoverHighlightState.StartDelay) return;

            float alpha = 0f;
            var move = _hoverHighlightState.CurrentMove;
            var state = _hoverHighlightState;
            float effectiveTimer = state.Timer - HoverHighlightState.StartDelay;

            switch (move.Target)
            {
                case TargetType.Single:
                case TargetType.SingleAll:
                    float cycleDuration = (HoverHighlightState.FadeDuration * 2) + HoverHighlightState.HoldDuration;
                    float timeInCycle = effectiveTimer % cycleDuration;

                    if (timeInCycle < HoverHighlightState.FadeDuration) // Fading in
                        alpha = timeInCycle / HoverHighlightState.FadeDuration;
                    else if (timeInCycle < HoverHighlightState.FadeDuration + HoverHighlightState.HoldDuration) // Holding
                        alpha = 1f;
                    else // Fading out
                        alpha = 1f - ((timeInCycle - HoverHighlightState.FadeDuration - HoverHighlightState.HoldDuration) / HoverHighlightState.FadeDuration);

                    var target = state.Targets[state.CurrentTargetIndex];
                    DrawHighlightForCombatant(spriteBatch, font, secondaryFont, target, alpha);
                    break;

                case TargetType.Every:
                case TargetType.EveryAll:
                case TargetType.Self:
                    float simpleCycle = HoverHighlightState.FadeDuration * 2;
                    float timeInSimpleCycle = effectiveTimer % simpleCycle;

                    if (timeInSimpleCycle < HoverHighlightState.FadeDuration) // Fading in
                        alpha = timeInSimpleCycle / HoverHighlightState.FadeDuration;
                    else // Fading out
                        alpha = 1f - ((timeInSimpleCycle - HoverHighlightState.FadeDuration) / HoverHighlightState.FadeDuration);

                    foreach (var t in state.Targets)
                    {
                        DrawHighlightForCombatant(spriteBatch, font, secondaryFont, t, alpha);
                    }
                    break;
            }
        }

        private void DrawHighlightForCombatant(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, BattleCombatant combatant, float alpha)
        {
            if (alpha <= 0) return;
            Color boxColor = Color.Yellow * (alpha * 0.3f);
            Rectangle bounds;

            if (combatant.IsPlayerControlled)
            {
                bounds = GetPlayerInteractionBounds(font, secondaryFont);
            }
            else
            {
                // This requires re-calculating the enemy's position, which is already done once per frame.
                // For a production game, this data should be cached to avoid redundant calculations.
                var enemies = _battleManager.AllCombatants.Where(c => !c.IsPlayerControlled).ToList();
                int enemyIndex = enemies.FindIndex(e => e.CombatantID == combatant.CombatantID);
                if (enemyIndex == -1) return;

                const int enemyAreaPadding = 20;
                const int enemyHudY = 80;
                int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
                int slotWidth = availableWidth / enemies.Count;
                var centerPosition = new Vector2(enemyAreaPadding + (enemyIndex * slotWidth) + (slotWidth / 2), enemyHudY);
                bounds = GetCombatantInteractionBounds(combatant, centerPosition, font, secondaryFont);
            }

            spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Right, bounds.Top), boxColor);
            spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Bottom), new Vector2(bounds.Right, bounds.Bottom), boxColor);
            spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Left, bounds.Bottom), boxColor);
            spriteBatch.DrawLineSnapped(new Vector2(bounds.Right, bounds.Top), new Vector2(bounds.Right, bounds.Bottom), boxColor);
        }

        private void DrawTurnIndicator(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_currentActor == null || _currentActor.IsDefeated) return;

            var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
            var arrowRects = _spriteManager.ArrowIconSourceRects;
            if (arrowSheet == null || arrowRects == null) return;

            float bobOffset = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 4f) > 0) ? 1f : 0f;

            if (_currentActor.IsPlayerControlled)
            {
                var player = _currentActor;
                var arrowRect = arrowRects[4]; // Right arrow

                const int playerHudY = DIVIDER_Y - 10;
                const int playerHudPaddingX = 10;

                Vector2 nameSize = font.MeasureString(player.Name);
                Vector2 namePos = new Vector2(playerHudPaddingX, playerHudY - font.LineHeight + 7);

                var arrowPos = new Vector2(
                    namePos.X - arrowRect.Width - 4 + bobOffset,
                    namePos.Y + (nameSize.Y - arrowRect.Height) / 2 - 1
                );

                spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, Color.White);
            }
            else // Enemy turn
            {
                var arrowRect = arrowRects[6]; // Down arrow
                var enemies = _battleManager.AllCombatants.Where(c => !c.IsPlayerControlled).ToList();
                int enemyIndex = enemies.FindIndex(e => e.CombatantID == _currentActor.CombatantID);
                if (enemyIndex == -1) return;

                const int enemyAreaPadding = 20;
                const int enemyHudY = 80;
                int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
                int slotWidth = availableWidth / enemies.Count;
                var centerPosition = new Vector2(enemyAreaPadding + (enemyIndex * slotWidth) + (slotWidth / 2), enemyHudY);

                const int spriteSize = 64;
                var spriteRect = new Rectangle(
                    (int)(centerPosition.X - spriteSize / 2),
                    (int)(centerPosition.Y - spriteSize - 10),
                    spriteSize,
                    spriteSize
                );

                var arrowPos = new Vector2(
                    spriteRect.Center.X - arrowRect.Width / 2,
                    spriteRect.Top - arrowRect.Height - 2 + bobOffset
                );

                spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, Color.White);
            }
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}
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
    /// Acts as a conductor for specialized manager classes.
    /// </summary>
    public class BattleScene : GameScene
    {
        // Core Battle Logic
        private BattleManager _battleManager;

        // Specialized Managers
        private BattleUIManager _uiManager;
        private BattleRenderer _renderer;
        private BattleAnimationManager _animationManager;
        private BattleInputHandler _inputHandler;

        // Scene-level UI & Services
        private ImageButton _settingsButton;
        private ComponentStore _componentStore;
        private SceneManager _sceneManager;
        private SpriteManager _spriteManager;
        private HapticsManager _hapticsManager;

        // State Tracking
        private List<int> _enemyEntityIds = new List<int>();
        private BattleManager.BattlePhase _previousBattlePhase;
        private bool _isBattleOver;
        private float _endOfBattleTimer;
        private const float END_OF_BATTLE_DELAY = 2.0f;
        private BattleCombatant _currentActor;

        public BattleAnimationManager AnimationManager => _animationManager;

        public BattleScene()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
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
            _inputHandler = new BattleInputHandler();
        }

        public override void Enter()
        {
            base.Enter();

            // Reset all managers
            _uiManager.Reset();
            _renderer.Reset();
            _animationManager.Reset();

            // Clear state
            _enemyEntityIds.Clear();
            _currentActor = null;
            _isBattleOver = false;
            _endOfBattleTimer = 0f;

            // Subscribe to events
            SubscribeToEvents();

            // Initialize scene-level UI
            InitializeSettingsButton();

            // Setup combatants and start the battle
            SetupBattle();
        }

        public override void Exit()
        {
            base.Exit();
            UnsubscribeFromEvents();
            CleanupEntities();
            ServiceLocator.Unregister<BattleManager>(); // Unregister on exit
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

            _uiManager.OnMoveSelected += OnPlayerMoveSelected;
            _uiManager.OnItemSelected += OnPlayerItemSelected;
            _uiManager.OnFleeRequested += FleeBattle;
            _inputHandler.OnMoveTargetSelected += OnPlayerMoveSelected;
            _inputHandler.OnItemTargetSelected += OnPlayerItemSelected;
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

            _uiManager.OnMoveSelected -= OnPlayerMoveSelected;
            _uiManager.OnItemSelected -= OnPlayerItemSelected;
            _uiManager.OnFleeRequested -= FleeBattle;
            _inputHandler.OnMoveTargetSelected -= OnPlayerMoveSelected;
            _inputHandler.OnItemTargetSelected -= OnPlayerItemSelected;
            _inputHandler.OnBackRequested -= () => _uiManager.GoBack();
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
        }

        private void InitializeSettingsButton()
        {
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
        }

        private void SetupBattle()
        {
            var gameState = ServiceLocator.Get<GameState>();
            int playerEntityId = gameState.PlayerEntityId;
            var playerCombatant = BattleCombatantFactory.CreateFromEntity(playerEntityId, "player_1");
            var playerParty = new List<BattleCombatant> { playerCombatant };

            var enemyParty = new List<BattleCombatant>();
            var enemyArchetypesToSpawn = BattleSetup.EnemyArchetypes ?? new List<string> { "wanderer" };

            int enemyCount = Math.Min(enemyArchetypesToSpawn.Count, 5);
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
            BattleSetup.EnemyArchetypes = null;

            if (playerCombatant == null || !enemyParty.Any())
            {
                Debug.WriteLine("[BattleScene] [FATAL] Failed to create one or more combatants. Aborting.");
                _battleManager = null;
                return;
            }

            _battleManager = new BattleManager(playerParty, enemyParty);
            ServiceLocator.Register<BattleManager>(_battleManager); // Register the instance
            _previousBattlePhase = _battleManager.CurrentPhase;
        }

        private void CleanupEntities()
        {
            if (_enemyEntityIds.Any())
            {
                var entityManager = ServiceLocator.Get<EntityManager>();
                foreach (var id in _enemyEntityIds)
                {
                    _componentStore.EntityDestroyed(id);
                    entityManager.DestroyEntity(id);
                }
                _enemyEntityIds.Clear();
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

            // Update managers
            _animationManager.Update(gameTime, _battleManager.AllCombatants);
            _uiManager.Update(gameTime, currentMouseState, currentKeyboardState);
            _inputHandler.Update(gameTime, _uiManager, _renderer);
            _renderer.Update(gameTime, _battleManager.AllCombatants);
            _settingsButton?.Update(currentMouseState);

            // Handle end of battle state
            if (_isBattleOver)
            {
                if (!_uiManager.IsBusy && !_animationManager.IsAnimating)
                {
                    _endOfBattleTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_endOfBattleTimer >= END_OF_BATTLE_DELAY)
                    {
                        _sceneManager.ChangeScene(GameSceneState.TerminalMap);
                    }
                }
                base.Update(gameTime);
                return;
            }

            // Animation Skip Logic
            if (_animationManager.IsAnimating && (UIInputManager.CanProcessMouseClick() && currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed))
            {
                _animationManager.SkipAllHealthAnimations(_battleManager.AllCombatants);
                UIInputManager.ConsumeMouseClick();
            }

            // Battle Manager Advancement Logic
            _battleManager.CanAdvance = !_uiManager.IsBusy && !_animationManager.IsAnimating;
            _battleManager.Update();

            // Handle Phase Changes
            var currentPhase = _battleManager.CurrentPhase;
            if (currentPhase != _previousBattlePhase)
            {
                HandlePhaseChange(currentPhase);
                _previousBattlePhase = currentPhase;
            }

            // Check for battle over condition
            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.BattleOver && !_isBattleOver)
            {
                _isBattleOver = true;
                _uiManager.HideAllMenus();
                var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
                _uiManager.ShowNarration(player != null && player.IsDefeated ? "Player Loses!" : "Player Wins!");
            }

            base.Update(gameTime);
        }

        private void HandlePhaseChange(BattleManager.BattlePhase newPhase)
        {
            if (newPhase == BattleManager.BattlePhase.EndOfTurn || newPhase == BattleManager.BattlePhase.BattleOver)
            {
                _currentActor = null;
            }

            if (newPhase == BattleManager.BattlePhase.ActionSelection)
            {
                if (!_battleManager.IsPlayerTurnSkipped)
                {
                    var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && !c.IsDefeated);
                    if (player != null)
                    {
                        _uiManager.ShowActionMenu(player, _battleManager.AllCombatants.ToList());
                    }
                }
            }
            else
            {
                _uiManager.HideAllMenus();
            }
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Draw the main scene content
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            DrawSceneContent(spriteBatch, font, gameTime, transform);
            spriteBatch.End();

            // Draw the overlay content (tooltips) on top, using the same transform
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            _renderer.DrawOverlay(spriteBatch, font);
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
            string roundText = _battleManager.RoundNumber.ToString();
            Vector2 roundTextPosition = new Vector2(5, 5);
            spriteBatch.DrawStringSnapped(font, roundText, roundTextPosition, ServiceLocator.Get<Global>().Palette_DarkGray);

            _renderer.Draw(spriteBatch, font, gameTime, _battleManager.AllCombatants, _currentActor, _uiManager, _inputHandler, _animationManager);
            _uiManager.Draw(spriteBatch, font, gameTime, transform);
            _animationManager.DrawDamageIndicators(spriteBatch, secondaryFont);
        }

        public override void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
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
                _settingsButton.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }
            spriteBatch.End();
        }

        public override void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // This is now empty, as the tooltip is part of the virtual scene space.
            // This method is for screen-space UI that should NOT be scaled.
        }

        #region Event Handlers
        private void OnPlayerMoveSelected(MoveData move, BattleCombatant target)
        {
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
            if (player != null)
            {
                var action = new QueuedAction { Actor = player, Target = target, ChosenMove = move, Priority = move.Priority, ActorAgility = player.Stats.Agility, Type = QueuedActionType.Move };
                _battleManager.SetPlayerAction(action);
            }
        }

        private void OnPlayerItemSelected(ConsumableItemData item, BattleCombatant target)
        {
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
            if (player == null) return;

            if (target == null)
            {
                var enemies = _battleManager.AllCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();
                switch (item.Target)
                {
                    case TargetType.Self:
                    case TargetType.SingleAll: target = player; break;
                    case TargetType.Single: if (enemies.Any()) target = enemies.First(); break;
                }
            }

            var action = new QueuedAction { Actor = player, ChosenItem = item, Target = target, Priority = item.Priority, ActorAgility = player.Stats.Agility, Type = QueuedActionType.Item };
            _battleManager.SetPlayerAction(action);
            _uiManager.HideAllMenus();
        }

        private void OnActionDeclared(GameEvents.ActionDeclared e)
        {
            _currentActor = e.Actor;
            string actionName = e.Item != null ? e.Item.ItemName : e.Move.MoveName;
            var targetType = e.Item?.Target ?? e.Move?.Target ?? TargetType.None;

            if (targetType != TargetType.None)
            {
                _uiManager.ShowNarration($"{e.Actor.Name} uses {actionName}!");
            }
        }

        private void OnMultiHitActionCompleted(GameEvents.MultiHitActionCompleted e)
        {
            _uiManager.ShowNarration($"Hit {e.HitCount} times!");
        }

        private void OnBattleActionExecuted(GameEvents.BattleActionExecuted e)
        {
            _currentActor = e.Actor;

            for (int i = 0; i < e.Targets.Count; i++)
            {
                var target = e.Targets[i];
                var result = e.DamageResults[i];
                Vector2 hudPosition = _renderer.GetCombatantHudCenterPosition(target, _battleManager.AllCombatants);

                if (result.DamageAmount > 0)
                {
                    if (target.IsPlayerControlled)
                    {
                        _core.TriggerFullscreenFlash(ServiceLocator.Get<Global>().Palette_Red, 0.15f);
                        _core.TriggerFullscreenGlitch(duration: 0.2f);
                        _hapticsManager.TriggerShake(magnitude: 2.0f, duration: 0.3f);
                    }
                    _animationManager.StartHealthAnimation(target.CombatantID, (int)target.VisualHP, target.Stats.CurrentHP);
                    _animationManager.StartHitAnimation(target.CombatantID);
                    _animationManager.StartDamageNumberIndicator(target.CombatantID, result.DamageAmount, hudPosition);
                }

                if (result.WasGraze)
                {
                    _animationManager.StartDamageIndicator(target.CombatantID, "GRAZE", hudPosition, ServiceLocator.Get<Global>().Palette_LightGray);
                }

                if (result.WasCritical)
                {
                    _uiManager.ShowNarration($"A critical hit on {target.Name}!");
                    _animationManager.StartDamageIndicator(target.CombatantID, "CRITICAL HIT", hudPosition, ServiceLocator.Get<Global>().Palette_Yellow);
                }
            }
        }

        private void OnCombatantHealed(GameEvents.CombatantHealed e)
        {
            _animationManager.StartHealthAnimation(e.Target.CombatantID, e.VisualHPBefore, e.Target.Stats.CurrentHP);
            _uiManager.ShowNarration($"{e.Target.Name} recovered {e.HealAmount} HP!");
        }

        private void OnCombatantDefeated(GameEvents.CombatantDefeated e)
        {
            _uiManager.ShowNarration($"{e.DefeatedCombatant.Name} was defeated!");
            _animationManager.StartAlphaAnimation(e.DefeatedCombatant.CombatantID, e.DefeatedCombatant.VisualAlpha, 0.1f);
        }

        private void OnActionFailed(GameEvents.ActionFailed e)
        {
            _currentActor = e.Actor;
            if (e.Reason.StartsWith("charging"))
            {
                _uiManager.ShowNarration($"{e.Actor.Name} is {e.Reason}!");
            }
            else
            {
                _uiManager.ShowNarration($"{e.Actor.Name} is {e.Reason} and cannot move!");
            }
        }

        private void OnStatusEffectTriggered(GameEvents.StatusEffectTriggered e)
        {
            if (e.Damage > 0)
            {
                _uiManager.ShowNarration($"{e.Combatant.Name} takes {e.Damage} damage from {e.EffectType}!");
                _animationManager.StartHealthAnimation(e.Combatant.CombatantID, (int)e.Combatant.VisualHP, e.Combatant.Stats.CurrentHP);
            }
        }

        private void FleeBattle()
        {
            _isBattleOver = true;
            _uiManager.HideAllMenus();
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
            _uiManager.ShowNarration(player != null ? $"{player.Name} escaped." : "Got away safely!");
        }

        private void OpenSettings()
        {
            _sceneManager.ShowModal(GameSceneState.Settings);
        }
        #endregion
    }
}
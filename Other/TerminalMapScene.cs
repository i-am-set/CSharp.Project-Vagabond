using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Scenes
{
    public class TerminalMapScene : GameScene
    {
        private readonly GameState _coreState;
        private readonly SceneManager _sceneManager;
        private readonly ClockRenderer _clockRenderer;
        private readonly SpriteManager _spriteManager;
        private readonly WorldClockManager _worldClockManager;
        private readonly InputHandler _inputHandler;
        private readonly MapInputHandler _mapInputHandler;
        private readonly MapRenderer _mapRenderer;
        private readonly StatsRenderer _statsRenderer;
        private readonly HapticsManager _hapticsManager;
        private readonly TerminalRenderer _terminalRenderer;
        private readonly ParticleSystemManager _particleSystemManager;
        private readonly DiceRollingSystem _diceRollingSystem;

        private WaitDialog _waitDialog;
        private ImageButton _settingsButton;
        private TurnOrderPanel _turnOrderPanel;
        private PlayerStatusPanel _playerStatusPanel;
        private EnemyDisplayPanel _enemyDisplayPanel;
        private CombatLogPanel _combatLogPanel;
        private ActionMenuPanel _actionMenuPanel;
        private PlayerCombatInputSystem _playerCombatInputSystem;
        private KeyboardState _previousKeyboardState;

        public TerminalMapScene()
        {
            _coreState = ServiceLocator.Get<GameState>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _clockRenderer = ServiceLocator.Get<ClockRenderer>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _inputHandler = ServiceLocator.Get<InputHandler>();
            _mapInputHandler = ServiceLocator.Get<MapInputHandler>();
            _mapRenderer = ServiceLocator.Get<MapRenderer>();
            _statsRenderer = ServiceLocator.Get<StatsRenderer>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _terminalRenderer = ServiceLocator.Get<TerminalRenderer>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _diceRollingSystem = ServiceLocator.Get<DiceRollingSystem>();

            EventBus.Subscribe<GameEvents.EntityTookDamage>(OnEntityTookDamage);
        }

        private void OnEntityTookDamage(GameEvents.EntityTookDamage e)
        {
            // If the player is the one taking damage, trigger a more intense shake.
            if (e.EntityId == _coreState.PlayerEntityId)
            {
                _hapticsManager.TriggerShake(16.0f, 0.3f);
            }
            else
            {
                // If an enemy takes damage, trigger the requested shake for attack feedback.
                _hapticsManager.TriggerShake(10, 0.3f);
            }
        }

        public override void Enter()
        {
            base.Enter();
            _core.IsMouseVisible = true;
            _waitDialog = new WaitDialog(this);
            _clockRenderer.OnClockClicked += ShowWaitDialog;
            _diceRollingSystem.OnRollCompleted += OnDiceRollCompleted;

            if (_settingsButton == null)
            {
                var settingsIcon = _spriteManager.SettingsIconSprite;
                var buttonSize = 16;
                if (settingsIcon != null) buttonSize = Math.Max(settingsIcon.Width, settingsIcon.Height);
                _settingsButton = new ImageButton(new Rectangle(5, 5, buttonSize, buttonSize), settingsIcon);
            }
            _settingsButton.OnClick += OpenSettings;

            // --- Combat UI Panel Initialization and Layout ---
            if (_playerStatusPanel == null)
            {
                // This layout is designed around the MapRenderer's fixed position.
                const int mapRightEdge = 35 + (Global.LOCAL_GRID_SIZE * Global.LOCAL_GRID_CELL_SIZE) + 10; // Map starts at 35, grid is 320, frame is 10
                const int bottomPanelY = 380;
                const int bottomPanelHeight = 140;

                // Enemy Display (Top Right)
                var enemyDisplayBounds = new Rectangle(mapRightEdge + 20, 50, Global.VIRTUAL_WIDTH - (mapRightEdge + 40), 120);
                _enemyDisplayPanel = new EnemyDisplayPanel(enemyDisplayBounds);

                // Turn Order (Right Side, below enemies)
                int turnOrderPanelWidth = 150;
                int maxTurnOrderItems = 10;
                var turnOrderPosition = new Vector2(enemyDisplayBounds.X, enemyDisplayBounds.Bottom + 10);
                _turnOrderPanel = new TurnOrderPanel(turnOrderPosition, turnOrderPanelWidth, maxTurnOrderItems);

                // Player Status (Bottom Left)
                var playerStatusBounds = new Rectangle(35, bottomPanelY, 250, bottomPanelHeight);
                _playerStatusPanel = new PlayerStatusPanel(playerStatusBounds);

                // Action Menu (Bottom Middle)
                var actionMenuBounds = new Rectangle(playerStatusBounds.Right + 10, bottomPanelY, 200, bottomPanelHeight);
                _actionMenuPanel = new ActionMenuPanel(actionMenuBounds);

                // Combat Log (Bottom Right)
                int combatLogX = actionMenuBounds.Right + 10;
                int combatLogWidth = Global.VIRTUAL_WIDTH - combatLogX - 35;
                var combatLogBounds = new Rectangle(combatLogX, bottomPanelY, combatLogWidth, bottomPanelHeight);
                _combatLogPanel = new CombatLogPanel(combatLogBounds);

                // Initialize the combat input system with the correct panels
                _playerCombatInputSystem = new PlayerCombatInputSystem(_actionMenuPanel, _turnOrderPanel, _enemyDisplayPanel, _mapRenderer);
            }
            _previousKeyboardState = Keyboard.GetState();
        }

        public override void Exit()
        {
            base.Exit();
            _clockRenderer.OnClockClicked -= ShowWaitDialog;
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
            EventBus.Unsubscribe<GameEvents.EntityTookDamage>(OnEntityTookDamage);
            _diceRollingSystem.OnRollCompleted -= OnDiceRollCompleted;
        }

        private void OnDiceRollCompleted(DiceRollResult result)
        {
            foreach (var groupResult in result.ResultsByGroup)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{groupResult.Key} dice : Sum = {groupResult.Value.Sum()}" });
            }

            
        }

        private void OpenSettings()
        {
            var settingsScene = _sceneManager.GetScene(GameSceneState.Settings) as SettingsScene;
            if (settingsScene != null) settingsScene.ReturnScene = GameSceneState.TerminalMap;
            _sceneManager.LastInputDevice = InputDevice.Mouse;
            _sceneManager.ChangeScene(GameSceneState.Settings);
        }

        private void ShowWaitDialog()
        {
            if (_waitDialog.IsActive || _worldClockManager.IsInterpolatingTime) return;
            _waitDialog.Show((hours, minutes, seconds) =>
            {
                double totalSeconds = (hours * 3600) + (minutes * 60) + seconds;
                if (totalSeconds > 0)
                {
                    _coreState.CancelExecutingActions();
                    // The real-world duration is proportional to the in-game duration, but capped.
                    float realDuration = Math.Clamp((float)totalSeconds * 0.1f, 0.5f, 5.0f);
                    _worldClockManager.PassTime(totalSeconds, realDuration, ActivityType.Waiting);
                }
            });
        }

        public override void Update(GameTime gameTime)
        {
            var currentKeyboardState = Keyboard.GetState();
            _diceRollingSystem.Update(gameTime);

            // Temporary input for testing dice rolls, changed to Tilde key
            if (currentKeyboardState.IsKeyDown(Keys.OemTilde) && !_previousKeyboardState.IsKeyDown(Keys.OemTilde))
            {
                var rollRequest = new List<DiceGroup>
                {
                    new DiceGroup
                    {
                        GroupId = "test_sum",
                        NumberOfDice = 40,
                        Tint = Color.Goldenrod,
                        ResultProcessing = DiceResultProcessing.Sum
                    },
                    new DiceGroup
                    {
                        GroupId = "test_individual",
                        NumberOfDice = 2,
                        Tint = Color.MediumPurple,
                        ResultProcessing = DiceResultProcessing.IndividualValues
                    }
                };
                _diceRollingSystem.Roll(rollRequest);
            }

            var font = ServiceLocator.Get<BitmapFont>();
            _waitDialog.Update(gameTime);
            if (_waitDialog.IsActive) return;

            // The input handler must run every frame to catch the unpause command.
            // It has its own internal logic to halt other inputs when paused.
            _inputHandler.HandleInput(gameTime);

            // UI elements that should remain interactive while paused are updated here.
            var currentMouseState = Mouse.GetState();
            _settingsButton?.Update(currentMouseState);
            _clockRenderer.Update(gameTime); // Updates the clock's buttons

            // If the game is paused, halt all other scene-specific updates.
            if (_coreState.IsPaused)
            {
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            if (_coreState.IsInCombat)
            {
                // These panels must always update during combat.
                _playerCombatInputSystem.ProcessInput();
                _actionMenuPanel.Update(gameTime, currentMouseState, font);
                _enemyDisplayPanel.Update(currentMouseState);

                // Block TurnOrderPanel during any focus state.
                if (_coreState.UIState != CombatUIState.SelectTarget && _coreState.UIState != CombatUIState.SelectMove)
                {
                    _turnOrderPanel.Update(gameTime, currentMouseState, font);
                }

                // Handle MapRenderer update separately. It should NOT update during target selection.
                if (_coreState.UIState != CombatUIState.SelectTarget)
                {
                    _mapRenderer.Update(gameTime, font);
                }
            }
            else
            {
                // Standard out-of-combat updates
                _mapInputHandler.Update(gameTime);
                _mapRenderer.Update(gameTime, font);
                _statsRenderer.Update(gameTime);
            }

            _hapticsManager.Update(gameTime);
            _worldClockManager.Update(gameTime);
            _previousKeyboardState = currentKeyboardState;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            var currentMouseState = Mouse.GetState();

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // --- Draw World/Map Content ---
            if (_coreState.CurrentMapView == MapView.Local)
            {
                _mapRenderer.DrawLocalMapBackground(spriteBatch, font, gameTime);

                spriteBatch.End();
                _particleSystemManager.Draw(spriteBatch);
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);

                _mapRenderer.DrawLocalMapEntities(spriteBatch, font, gameTime);
            }
            else // World Map
            {
                _mapRenderer.DrawMap(spriteBatch, font, gameTime);
            }

            // --- Draw UI Panels ---
            if (_coreState.IsInCombat)
            {
                _enemyDisplayPanel.Draw(spriteBatch, gameTime, currentMouseState);
                _turnOrderPanel.Draw(spriteBatch, font, gameTime);
                _playerStatusPanel.Draw(spriteBatch, font);
                _actionMenuPanel.Draw(spriteBatch, font, gameTime);
                _combatLogPanel.Draw(spriteBatch, font, gameTime);

                // Draw focus effect for target or move selection
                if (_coreState.UIState == CombatUIState.SelectTarget || _coreState.UIState == CombatUIState.SelectMove)
                {
                    var pixel = ServiceLocator.Get<Texture2D>();
                    var screenBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
                    spriteBatch.Draw(pixel, screenBounds, Color.Black * 0.6f);

                    // Redraw the important panels on top of the overlay
                    if (_coreState.UIState == CombatUIState.SelectTarget)
                    {
                        _enemyDisplayPanel.Draw(spriteBatch, gameTime, currentMouseState);
                    }
                    else // Must be SelectMove
                    {
                        // Redraw the map with entities to see where you're moving
                        _mapRenderer.DrawLocalMapBackground(spriteBatch, font, gameTime);
                        _mapRenderer.DrawLocalMapEntities(spriteBatch, font, gameTime);
                    }
                    _actionMenuPanel.Draw(spriteBatch, font, gameTime);
                }
            }
            else
            {
                _terminalRenderer.DrawTerminal(spriteBatch, font, gameTime);
                _statsRenderer.DrawStats(spriteBatch, font);
                _clockRenderer.DrawClock(spriteBatch, font, gameTime);
                _settingsButton?.Draw(spriteBatch, font, gameTime);
            }

            spriteBatch.End();

            _waitDialog.Draw(spriteBatch, font, gameTime);
        }
    }
}
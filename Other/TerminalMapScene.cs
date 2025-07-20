﻿﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;

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

        private WaitDialog _waitDialog;
        private ImageButton _settingsButton;
        private TurnOrderPanel _turnOrderPanel;
        private PlayerStatusPanel _playerStatusPanel;
        private EnemyDisplayPanel _enemyDisplayPanel;
        private CombatLogPanel _combatLogPanel;
        private ActionMenuPanel _actionMenuPanel;
        private PlayerCombatInputSystem _playerCombatInputSystem;

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
        }

        public override void Enter()
        {
            base.Enter();
            _core.IsMouseVisible = true;
            _waitDialog = new WaitDialog(this);
            _clockRenderer.OnClockClicked += ShowWaitDialog;

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
        }

        public override void Exit()
        {
            base.Exit();
            _clockRenderer.OnClockClicked -= ShowWaitDialog;
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
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
                    _worldClockManager.PassTime(totalSeconds);
                }
            });
        }

        public override void Update(GameTime gameTime)
        {
            var font = ServiceLocator.Get<BitmapFont>();
            _waitDialog.Update(gameTime);
            if (_waitDialog.IsActive) return;

            var currentMouseState = Mouse.GetState();

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
                _settingsButton?.Update(currentMouseState);
                _inputHandler.HandleInput(gameTime);
                _mapInputHandler.Update(gameTime);
                _mapRenderer.Update(gameTime, font);
                _statsRenderer.Update(gameTime);
                _clockRenderer.Update(gameTime);
            }

            _hapticsManager.Update(gameTime);
            _worldClockManager.Update(gameTime);
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            Matrix shakeMatrix = _hapticsManager.GetHapticsMatrix();
            var currentMouseState = Mouse.GetState();

            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: shakeMatrix);

            if (_coreState.IsInCombat)
            {
                // Draw the dedicated combat UI
                _mapRenderer.DrawMap(spriteBatch, font, gameTime);
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
                        _mapRenderer.DrawMap(spriteBatch, font, gameTime);
                    }
                    _actionMenuPanel.Draw(spriteBatch, font, gameTime);
                }
            }
            else
            {
                // Draw the standard out-of-combat UI
                _terminalRenderer.DrawTerminal(spriteBatch, font, gameTime);
                _mapRenderer.DrawMap(spriteBatch, font, gameTime);
                _statsRenderer.DrawStats(spriteBatch, font);
                _clockRenderer.DrawClock(spriteBatch, font, gameTime);
                _settingsButton?.Draw(spriteBatch, font, gameTime);
            }

            spriteBatch.End();

            _waitDialog.Draw(spriteBatch, font, gameTime);
        }
    }
}

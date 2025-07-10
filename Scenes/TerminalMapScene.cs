using Microsoft.Xna.Framework;
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
        private TargetInfoPanel _targetInfoPanel;
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

            if (_turnOrderPanel == null)
            {
                int turnOrderPanelWidth = 150;
                int maxTurnOrderItems = 16;
                var turnOrderPosition = new Vector2((Global.VIRTUAL_WIDTH - turnOrderPanelWidth - 10), Global.TERMINAL_Y - 23);
                _turnOrderPanel = new TurnOrderPanel(turnOrderPosition, turnOrderPanelWidth, maxTurnOrderItems);
            }

            if (_playerStatusPanel == null)
            {
                // Define bounds for the Player Status Panel
                int playerStatusPanelWidth = 250;
                int playerStatusPanelHeight = 100;
                int playerStatusPanelX = 20;
                int playerStatusPanelY = Global.VIRTUAL_HEIGHT - playerStatusPanelHeight - 20;
                _playerStatusPanel = new PlayerStatusPanel(new Rectangle(playerStatusPanelX, playerStatusPanelY, playerStatusPanelWidth, playerStatusPanelHeight));

                // Define bounds for the Action Menu Panel
                int actionMenuPanelWidth = 200;
                int actionMenuPanelHeight = 150;
                int actionMenuPanelX = playerStatusPanelX + playerStatusPanelWidth + 10;
                int actionMenuPanelY = Global.VIRTUAL_HEIGHT - actionMenuPanelHeight - 20;
                _actionMenuPanel = new ActionMenuPanel(new Rectangle(actionMenuPanelX, actionMenuPanelY, actionMenuPanelWidth, actionMenuPanelHeight));

                // Define bounds for the Target Info Panel
                int targetInfoPanelWidth = 250;
                int targetInfoPanelHeight = 100;
                int targetInfoPanelX = 375;
                int targetInfoPanelY = 50 + ((Global.DEFAULT_TERMINAL_HEIGHT / 2) + 20) + 10;
                _targetInfoPanel = new TargetInfoPanel(new Rectangle(targetInfoPanelX, targetInfoPanelY, targetInfoPanelWidth, targetInfoPanelHeight));

                _playerCombatInputSystem = new PlayerCombatInputSystem(_actionMenuPanel, _turnOrderPanel, _mapRenderer);
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
                if (hours > 0 || minutes > 0 || seconds > 0)
                {
                    _coreState.CancelExecutingActions();
                    _worldClockManager.PassTime(0, hours, minutes, seconds);
                }
            });
        }

        public override void Update(GameTime gameTime)
        {
            var font = ServiceLocator.Get<BitmapFont>();
            _waitDialog.Update(gameTime);
            if (_waitDialog.IsActive) return;

            var currentMouseState = Mouse.GetState();
            _settingsButton?.Update(currentMouseState);

            if (_coreState.IsInCombat)
            {
                _playerCombatInputSystem.ProcessInput();
                _actionMenuPanel.Update(gameTime, currentMouseState, font);
                _turnOrderPanel.Update(gameTime, currentMouseState, font);
            }
            else
            {
                _inputHandler.HandleInput(gameTime);
                _mapInputHandler.Update(gameTime);
            }

            _mapRenderer.Update(gameTime, font);
            _statsRenderer.Update(gameTime);
            _hapticsManager.Update(gameTime);
            _worldClockManager.Update(gameTime);
            _clockRenderer.Update(gameTime);
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            Matrix shakeMatrix = _hapticsManager.GetHapticsMatrix();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: shakeMatrix);

            _terminalRenderer.DrawTerminal(spriteBatch, font, gameTime);
            _mapRenderer.DrawMap(spriteBatch, font, gameTime);
            _statsRenderer.DrawStats(spriteBatch, font);
            _clockRenderer.DrawClock(spriteBatch, font, gameTime);

            if (_coreState.IsInCombat)
            {
                _turnOrderPanel.Draw(spriteBatch, font, gameTime);
                _playerStatusPanel.Draw(spriteBatch, font);
                _targetInfoPanel.Draw(spriteBatch, font);
                _actionMenuPanel.Draw(spriteBatch, font, gameTime);
            }

            _settingsButton?.Draw(spriteBatch, font, gameTime);
            spriteBatch.End();

            _waitDialog.Draw(spriteBatch, font, gameTime);
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.UI;
using System;

namespace ProjectVagabond.Scenes
{
    public class TerminalMapScene : GameScene
    {
        private WaitDialog _waitDialog;
        private ImageButton _settingsButton;
        private TurnOrderPanel _turnOrderPanel;
        private PlayerStatusPanel _playerStatusPanel;
        private TargetInfoPanel _targetInfoPanel;
        private ActionMenuPanel _actionMenuPanel;
        private PlayerCombatInputSystem _playerCombatInputSystem;

        public override void Enter()
        {
            base.Enter();
            Core.Instance.IsMouseVisible = true;
            _waitDialog = new WaitDialog(this);
            Core.CurrentClockRenderer.OnClockClicked += ShowWaitDialog;

            if (_settingsButton == null)
            {
                var settingsIcon = Core.CurrentSpriteManager.SettingsIconSprite;
                var buttonSize = 16;
                if (settingsIcon != null)
                {
                    buttonSize = Math.Max(settingsIcon.Width, settingsIcon.Height);
                }
                _settingsButton = new ImageButton(new Rectangle(5, 5, buttonSize, buttonSize), settingsIcon);
            }
            _settingsButton.OnClick += OpenSettings;

            if (_turnOrderPanel == null)
            {
                int turnOrderPanelWidth = 100;
                int maxTurnOrderItems = 16; // The panel will show a maximum of 5 names at a time
                var turnOrderPosition = new Vector2((Global.VIRTUAL_WIDTH - turnOrderPanelWidth) / 2, Global.TERMINAL_Y - 23);
                _turnOrderPanel = new TurnOrderPanel(turnOrderPosition, turnOrderPanelWidth, maxTurnOrderItems);
            }

            if (_playerStatusPanel == null)
            {
                // Define bounds for the Player Status Panel (bottom-left)
                int playerStatusPanelWidth = 250;
                int playerStatusPanelHeight = 100;
                int playerStatusPanelX = 20;
                int playerStatusPanelY = Global.VIRTUAL_HEIGHT - playerStatusPanelHeight - 20;
                _playerStatusPanel = new PlayerStatusPanel(new Rectangle(playerStatusPanelX, playerStatusPanelY, playerStatusPanelWidth, playerStatusPanelHeight));

                // Define bounds for the Action Menu Panel (next to player status)
                int actionMenuPanelWidth = 200;
                int actionMenuPanelHeight = 150;
                int actionMenuPanelX = playerStatusPanelX + playerStatusPanelWidth + 10;
                int actionMenuPanelY = Global.VIRTUAL_HEIGHT - actionMenuPanelHeight - 20;
                _actionMenuPanel = new ActionMenuPanel(new Rectangle(actionMenuPanelX, actionMenuPanelY, actionMenuPanelWidth, actionMenuPanelHeight));

                // Define bounds for the Target Info Panel (below the shrunken terminal)
                int targetInfoPanelWidth = 250;
                int targetInfoPanelHeight = 100;
                int targetInfoPanelX = 375; // Aligns with the terminal's X position
                int targetInfoPanelY = 50 + ((Global.DEFAULT_TERMINAL_HEIGHT / 2) + 20) + 10;
                _targetInfoPanel = new TargetInfoPanel(new Rectangle(targetInfoPanelX, targetInfoPanelY, targetInfoPanelWidth, targetInfoPanelHeight));

                _playerCombatInputSystem = new PlayerCombatInputSystem(_actionMenuPanel, _turnOrderPanel, Core.CurrentMapRenderer);
            }
        }

        public override void Exit()
        {
            base.Exit();
            Core.CurrentClockRenderer.OnClockClicked -= ShowWaitDialog;
            if (_settingsButton != null)
            {
                _settingsButton.OnClick -= OpenSettings;
            }
        }

        private void OpenSettings()
        {
            var settingsScene = Core.CurrentSceneManager.GetScene(GameSceneState.Settings) as SettingsScene;
            if (settingsScene != null)
            {
                settingsScene.ReturnScene = GameSceneState.TerminalMap;
            }
            Core.CurrentSceneManager.LastInputDevice = InputDevice.Mouse;
            Core.CurrentSceneManager.ChangeScene(GameSceneState.Settings);
        }

        private void ShowWaitDialog()
        {
            if (_waitDialog.IsActive || Core.CurrentWorldClockManager.IsInterpolatingTime)
            {
                return;
            }

            _waitDialog.Show((hours, minutes, seconds) =>
            {
                if (hours > 0 || minutes > 0 || seconds > 0)
                {
                    Core.CurrentGameState.CancelExecutingActions();
                    Core.CurrentWorldClockManager.PassTime(0, hours, minutes, seconds);
                }
            });
        }

        public override void Update(GameTime gameTime)
        {
            _waitDialog.Update(gameTime);
            if (_waitDialog.IsActive) return;

            var currentMouseState = Mouse.GetState();
            _settingsButton?.Update(currentMouseState);

            if (Core.CurrentGameState.IsInCombat)
            {
                _playerCombatInputSystem.ProcessInput();
                _actionMenuPanel.Update(gameTime, currentMouseState);
                _turnOrderPanel.Update(gameTime, currentMouseState);
            }
            else
            {
                Core.CurrentInputHandler.HandleInput(gameTime);
                Core.CurrentMapInputHandler.Update(gameTime);
            }

            Core.CurrentMapRenderer.Update(gameTime);
            Core.CurrentStatsRenderer.Update(gameTime);
            Core.CurrentHapticsManager.Update(gameTime);
            Core.CurrentWorldClockManager.Update(gameTime);
            Core.CurrentClockRenderer.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            Matrix shakeMatrix = Core.CurrentHapticsManager.GetHapticsMatrix();

            Global.Instance.CurrentSpriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: shakeMatrix);

            Core.CurrentTerminalRenderer.DrawTerminal(gameTime);
            Core.CurrentMapRenderer.DrawMap(gameTime);
            Core.CurrentStatsRenderer.DrawStats();
            Core.CurrentClockRenderer.DrawClock(Global.Instance.CurrentSpriteBatch, gameTime);
            _turnOrderPanel.Draw(Global.Instance.CurrentSpriteBatch, gameTime);

            // Draw Combat UI Panels
            _playerStatusPanel.Draw(Global.Instance.CurrentSpriteBatch);
            _targetInfoPanel.Draw(Global.Instance.CurrentSpriteBatch);
            _actionMenuPanel.Draw(Global.Instance.CurrentSpriteBatch, gameTime);

            _settingsButton?.Draw(Global.Instance.CurrentSpriteBatch, Global.Instance.DefaultFont, gameTime);

            Global.Instance.CurrentSpriteBatch.End();

            _waitDialog.Draw(gameTime);
        }
    }
}
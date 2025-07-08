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
                _turnOrderPanel = new TurnOrderPanel();
            }

            if (_playerStatusPanel == null)
            {
                // Define bounds for the panels
                int pspWidth = 250;
                int pspHeight = 100;
                int pspX = 20;
                int pspY = Global.VIRTUAL_HEIGHT - pspHeight - 20;
                _playerStatusPanel = new PlayerStatusPanel(new Rectangle(pspX, pspY, pspWidth, pspHeight));

                int ampWidth = 200;
                int ampHeight = 150;
                int ampX = pspX + pspWidth + 10;
                int ampY = Global.VIRTUAL_HEIGHT - ampHeight - 20;
                _actionMenuPanel = new ActionMenuPanel(new Rectangle(ampX, ampY, ampWidth, ampHeight));

                int tipWidth = 250;
                int tipHeight = 100;
                int tipX = 375;
                int tipY = 50 + ((Global.DEFAULT_TERMINAL_HEIGHT / 2) + 20) + 10; // Below the shrunken terminal
                _targetInfoPanel = new TargetInfoPanel(new Rectangle(tipX, tipY, tipWidth, tipHeight));

                _playerCombatInputSystem = new PlayerCombatInputSystem(_actionMenuPanel, Core.CurrentMapRenderer);
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

            _settingsButton?.Update(Mouse.GetState());

            if (Core.CurrentGameState.IsInCombat)
            {
                _playerCombatInputSystem.ProcessInput();
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
            _turnOrderPanel.Draw(Global.Instance.CurrentSpriteBatch);

            // Draw Combat UI Panels
            _playerStatusPanel.Draw(Global.Instance.CurrentSpriteBatch);
            _targetInfoPanel.Draw(Global.Instance.CurrentSpriteBatch);
            _actionMenuPanel.Draw(Global.Instance.CurrentSpriteBatch);

            _settingsButton?.Draw(Global.Instance.CurrentSpriteBatch, Global.Instance.DefaultFont, gameTime);

            Global.Instance.CurrentSpriteBatch.End();

            _waitDialog.Draw(gameTime);
        }
    }
}
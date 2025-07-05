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
                    Core.CurrentGameState.CancelPathExecution();
                    Core.CurrentWorldClockManager.PassTime(0, hours, minutes, seconds);
                }
            });
        }

        public override void Update(GameTime gameTime)
        {
            _waitDialog.Update(gameTime);

            if (_waitDialog.IsActive)
            {
                return;
            }

            _settingsButton?.Update(Mouse.GetState());
            Core.CurrentInputHandler.HandleInput(gameTime);
            Core.CurrentMapRenderer.Update(gameTime);
            Core.CurrentMapInputHandler.Update(gameTime);
            Core.CurrentGameState.UpdateMovement(gameTime);
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

            _settingsButton?.Draw(Global.Instance.CurrentSpriteBatch, Global.Instance.DefaultFont, gameTime);

            Global.Instance.CurrentSpriteBatch.End();

            _waitDialog.Draw(gameTime);
        }
    }
}
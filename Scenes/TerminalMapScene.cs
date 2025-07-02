using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.UI;

namespace ProjectVagabond.Scenes
{
    public class TerminalMapScene : GameScene
    {
        private WaitDialog _waitDialog;

        public override void Enter()
        {
            base.Enter();
            Core.Instance.IsMouseVisible = true;
            _waitDialog = new WaitDialog(this);
            Core.CurrentClockRenderer.OnClockClicked += ShowWaitDialog;
        }

        public override void Exit()
        {
            base.Exit();
            Core.CurrentClockRenderer.OnClockClicked -= ShowWaitDialog;
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

            Global.Instance.CurrentSpriteBatch.End();

            _waitDialog.Draw(gameTime);
        }
    }
}
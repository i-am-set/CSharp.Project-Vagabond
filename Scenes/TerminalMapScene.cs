using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond.Scenes
{
    public class TerminalMapScene : GameScene
    {
        public override void Update(GameTime gameTime)
        {
            Core.CurrentInputHandler.HandleInput(gameTime);
            Core.CurrentGameState.UpdateMovement(gameTime);
            Core.CurrentStatsRenderer.Update(gameTime);
            Core.CurrentHapticsManager.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            Matrix shakeMatrix = Core.CurrentHapticsManager.GetHapticsMatrix();

            Global.Instance.CurrentSpriteBatch.Begin(transformMatrix: shakeMatrix);

            Core.CurrentMapRenderer.DrawMap();
            Core.CurrentTerminalRenderer.DrawTerminal();
            Core.CurrentStatsRenderer.DrawStats();

            Global.Instance.CurrentSpriteBatch.End();
        }
    }
}
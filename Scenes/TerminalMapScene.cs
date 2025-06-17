﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond.Scenes
{
    public class TerminalMapScene : GameScene
    {
        public override void Enter()
        {
            base.Enter();
            Core.Instance.IsMouseVisible = true;
        }

        public override void Update(GameTime gameTime)
        {
            Core.CurrentInputHandler.HandleInput(gameTime);
            Core.CurrentMapRenderer.Update(gameTime);
            Core.CurrentGameState.UpdateMovement(gameTime);
            Core.CurrentStatsRenderer.Update(gameTime);
            Core.CurrentHapticsManager.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            Matrix shakeMatrix = Core.CurrentHapticsManager.GetHapticsMatrix();

            Global.Instance.CurrentSpriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: shakeMatrix);

            Core.CurrentTerminalRenderer.DrawTerminal();
            Core.CurrentMapRenderer.DrawMap();
            Core.CurrentStatsRenderer.DrawStats();

            Global.Instance.CurrentSpriteBatch.End();
        }
    }
}

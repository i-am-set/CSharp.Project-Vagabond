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
        }
    }
}

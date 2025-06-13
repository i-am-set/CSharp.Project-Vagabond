using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ProjectVagabond.Scenes
{
    public class CombatScene : GameScene
    {
        public override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Core.CurrentSceneManager.ChangeScene(GameSceneState.TerminalMap);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = Global.Instance.CurrentSpriteBatch;
            var font = Global.Instance.DefaultFont;
            int screenWidth = Global.Instance.CurrentGraphics.PreferredBackBufferWidth;
            int screenHeight = Global.Instance.CurrentGraphics.PreferredBackBufferHeight;

            spriteBatch.Begin();

            string text = "This is a placeholder combat screen.\nPress ESC to return.";
            Vector2 textSize = font.MeasureString(text);
            Vector2 textPos = new Vector2(screenWidth / 2 - textSize.X / 2, screenHeight / 2 - textSize.Y / 2);
            spriteBatch.DrawString(font, text, textPos, Color.Red);

            spriteBatch.End();
        }
    }
}
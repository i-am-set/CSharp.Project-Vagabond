using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ProjectVagabond.Scenes
{
    public class DialogueScene : GameScene
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

            Core.CurrentMapRenderer.DrawMap();
            Core.CurrentStatsRenderer.DrawStats();

            using (var pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1))
            {
                pixel.SetData(new[] { Color.White });

                Rectangle dialogueBox = new Rectangle(100, screenHeight - 250, screenWidth - 200, 200);
                spriteBatch.Draw(pixel, dialogueBox, Global.Instance.palette_Black * 0.8f);

                string text = "This is a placeholder dialogue screen.\nPress ESC to return.";
                Vector2 textSize = font.MeasureString(text);
                Vector2 textPos = new Vector2(dialogueBox.X + (dialogueBox.Width - textSize.X) / 2, dialogueBox.Y + (dialogueBox.Height - textSize.Y) / 2);
                spriteBatch.DrawString(font, text, textPos, Global.Instance.palette_BrightWhite);
            }

            spriteBatch.End();
        }
    }
}
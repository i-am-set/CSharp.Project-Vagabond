using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond.Scenes
{
    public class DialogueScene : GameScene
    {
        private readonly SceneManager _sceneManager;
        private readonly MapRenderer _mapRenderer;
        private readonly StatsRenderer _statsRenderer;
        private readonly Global _global;

        public DialogueScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _mapRenderer = ServiceLocator.Get<MapRenderer>();
            _statsRenderer = ServiceLocator.Get<StatsRenderer>();
            _global = ServiceLocator.Get<Global>();
        }

        protected override Rectangle GetAnimatedBounds()
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            int screenHeight = Global.VIRTUAL_HEIGHT;
            return new Rectangle(100, screenHeight - 250, screenWidth - 200, 200);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                _sceneManager.ChangeScene(GameSceneState.TerminalMap);
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            int screenHeight = Global.VIRTUAL_HEIGHT;
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            _mapRenderer.DrawMap(spriteBatch, font, gameTime);

            // Use the map's calculated bounds to position the stats correctly
            var mapBounds = _mapRenderer.MapScreenBounds;
            int leftColumnWidth = mapBounds.X;
            if (leftColumnWidth > 20)
            {
                _statsRenderer.DrawStats(spriteBatch, font, new Vector2(10, Global.MAP_TOP_PADDING), leftColumnWidth - 20);
            }

            Rectangle dialogueBox = GetAnimatedBounds();
            spriteBatch.Draw(pixel, dialogueBox, _global.Palette_Black * 0.8f);

            string text = "This is a placeholder dialogue screen.\nPress ESC to return.";
            Vector2 textSize = font.MeasureString(text);
            Vector2 textPos = new Vector2(dialogueBox.X + (dialogueBox.Width - textSize.X) / 2, dialogueBox.Y + (dialogueBox.Height - textSize.Y) / 2);
            spriteBatch.DrawString(font, text, textPos, _global.Palette_BrightWhite);
        }
    }
}
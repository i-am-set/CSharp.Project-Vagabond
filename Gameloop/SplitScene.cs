using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond.Scenes
{
    /// <summary>
    /// This scene has been deprecated and replaced by SplitMapScene.
    /// This file is kept to resolve compilation errors but can be removed from the project.
    /// The GameSceneState.Split now correctly points to the new SplitMapScene.
    /// </summary>
    [Obsolete("SplitScene is deprecated. Use SplitMapScene instead.")]
    public class SplitScene : GameScene
    {
        public SplitScene() { }

        public override Rectangle GetAnimatedBounds() => new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // This scene is deprecated and draws nothing.
        }
    }
}
using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// Enum to identify each distinct game scene.
    /// </summary>
    public enum GameSceneState
    {
        MainMenu,
        TerminalMap,
        Dialogue,
        Combat,
        Settings
    }

    /// <summary>
    /// Abstract base class for all game scenes.
    /// </summary>
    public abstract class GameScene
    {
        /// <summary>
        /// Called once when the scene is first added to the SceneManager.
        /// Use for one-time setup.
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// Called every time the scene becomes the active scene.
        /// Use for resetting state.
        /// </summary>
        public virtual void Enter() { }

        /// <summary>
        /// Called every time the scene is no longer the active scene.
        /// </summary>
        public virtual void Exit() { }

        /// <summary>
        /// Called every frame to update the scene's logic.
        /// </summary>
        public abstract void Update(GameTime gameTime);

        /// <summary>
        /// Called every frame to draw the scene.
        /// </summary>
        public abstract void Draw(GameTime gameTime);
    }
}
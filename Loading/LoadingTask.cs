using Microsoft.Xna.Framework;

namespace ProjectVagabond.Scenes
{
    public abstract class LoadingTask
    {
        public string Description { get; }
        public bool IsComplete { get; protected set; }

        protected LoadingTask(string description)
        {
            Description = description;
            IsComplete = false;
        }

        public abstract void Start();
        public abstract void Update(GameTime gameTime);
    }
}
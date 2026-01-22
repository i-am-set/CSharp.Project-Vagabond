using Microsoft.Xna.Framework;

namespace ProjectVagabond.Scenes
{
    /// <summary>
    /// A loading task that simply waits for a specified duration.
    /// Useful for pacing the loading screen so it doesn't flash by too quickly.
    /// </summary>
    public class DelayTask : LoadingTask
    {
        private readonly float _duration;
        private float _timer;

        public DelayTask(float durationInSeconds, string description = "") : base(description)
        {
            _duration = durationInSeconds;
        }

        public override void Start()
        {
            _timer = 0f;
            IsComplete = false;
        }

        public override void Update(GameTime gameTime)
        {
            if (IsComplete) return;

            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_timer >= _duration)
            {
                IsComplete = true;
            }
        }
    }
}
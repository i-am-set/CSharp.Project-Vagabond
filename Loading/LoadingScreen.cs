using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class LoadingScreen
    {
        private readonly Global _global;
        private readonly List<LoadingTask> _tasks = new List<LoadingTask>();
        private int _currentTaskIndex = -1;
        private float _totalProgress = 0f;
        private float _progressPerTask = 0f;

        private float _ellipsisTimer = 0f;
        private int _ellipsisCount = 0;

        public bool IsActive { get; private set; }

        // Progress bar animation
        private float _visualProgress = 0f;
        private float _progressAnimStart = 0f;
        private float _progressAnimEnd = 0f;
        private float _progressAnimTimer = 0f;
        private const float PROGRESS_ANIM_DURATION = 0.4f;

        // Hold after complete
        private bool _allTasksComplete = false;
        private float _holdTimer = 0f;
        private const float HOLD_DURATION = 1.0f;

        public LoadingScreen()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void AddTask(LoadingTask task)
        {
            _tasks.Add(task);
        }

        public void Start()
        {
            if (!_tasks.Any())
            {
                IsActive = false;
                return;
            }

            IsActive = true;
            _currentTaskIndex = 0;
            _totalProgress = 0f;
            _progressPerTask = 1.0f / _tasks.Count;

            _visualProgress = 0f;
            _progressAnimStart = 0f;
            _progressAnimEnd = 0f;
            _progressAnimTimer = PROGRESS_ANIM_DURATION; // Start as complete

            _allTasksComplete = false;
            _holdTimer = 0f;

            _tasks[_currentTaskIndex].Start();
        }

        public void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _ellipsisTimer += deltaTime;
            if (_ellipsisTimer > 0.5f)
            {
                _ellipsisTimer = 0f;
                _ellipsisCount = (_ellipsisCount + 1) % 4;
            }

            // Handle hold timer after all tasks are complete
            if (_allTasksComplete)
            {
                _holdTimer += deltaTime;
                if (_holdTimer >= HOLD_DURATION && _progressAnimTimer >= PROGRESS_ANIM_DURATION)
                {
                    IsActive = false;
                }
            }
            // Only process tasks if not all are complete
            else if (_currentTaskIndex < _tasks.Count)
            {
                var currentTask = _tasks[_currentTaskIndex];
                currentTask.Update(gameTime);

                if (currentTask.IsComplete)
                {
                    _totalProgress = (_currentTaskIndex + 1) * _progressPerTask;

                    // Start animation for the progress bar
                    _progressAnimStart = _visualProgress;
                    _progressAnimEnd = _totalProgress;
                    _progressAnimTimer = 0f;

                    _currentTaskIndex++;

                    if (_currentTaskIndex < _tasks.Count)
                    {
                        _tasks[_currentTaskIndex].Start();
                    }
                    else
                    {
                        _allTasksComplete = true;
                        _progressAnimEnd = 1.0f; // Ensure it animates to exactly 100%
                    }
                }
            }

            // Update progress bar animation
            if (_progressAnimTimer < PROGRESS_ANIM_DURATION)
            {
                _progressAnimTimer += deltaTime;
                float progress = MathHelper.Clamp(_progressAnimTimer / PROGRESS_ANIM_DURATION, 0f, 1f);
                _visualProgress = MathHelper.Lerp(_progressAnimStart, _progressAnimEnd, Easing.EaseOutCirc(progress));
            }
            else
            {
                _visualProgress = _progressAnimEnd;
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (!IsActive) return;

            // --- Loading Bar Style Parameters ---
            const int LOADING_BAR_SEGMENTS = 50;
            const int SEGMENT_WIDTH = 6;
            const int SEGMENT_GAP = 2;
            const int SEGMENT_HEIGHT = 6;
            const int BAR_HEIGHT = 10;
            const int horizontalPadding = 2;

            var pixel = ServiceLocator.Get<Texture2D>();
            var screenBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

            // Black background
            spriteBatch.Draw(pixel, screenBounds, _global.Palette_Black);

            // Loading text
            string loadingText = "Loading" + new string('.', _ellipsisCount);
            Vector2 textSize = font.MeasureString(loadingText);
            Vector2 textPosition = new Vector2(
                (Global.VIRTUAL_WIDTH - textSize.X) / 2,
                (Global.VIRTUAL_HEIGHT) - 45
            );
            spriteBatch.DrawString(font, loadingText, textPosition, _global.Palette_BrightWhite);

            // Loading bar layout
            int segmentsAreaWidth = LOADING_BAR_SEGMENTS * (SEGMENT_WIDTH + SEGMENT_GAP) - SEGMENT_GAP;
            int barWidth = segmentsAreaWidth + (horizontalPadding * 2);
            int barX = (Global.VIRTUAL_WIDTH - barWidth) / 2;
            int barY = (Global.VIRTUAL_HEIGHT - 20 - (BAR_HEIGHT / 2));
            var barBounds = new Rectangle(barX, barY, barWidth, BAR_HEIGHT);

            // Border
            var borderRect = new Rectangle(barBounds.X - 2, barBounds.Y - 2, barBounds.Width + 4, barBounds.Height + 4);
            spriteBatch.Draw(pixel, borderRect, _global.Palette_DarkGray);

            // Draw the stylized, segmented bar
            UIPrimitives.DrawSegmentedBar(
                spriteBatch,
                pixel,
                barBounds,
                _visualProgress,
                LOADING_BAR_SEGMENTS,
                _global.Palette_LightGreen,
                Color.Lerp(_global.Palette_DarkGray, _global.Palette_LightGreen, 0.3f),
                _global.Palette_DarkGray,
                SEGMENT_WIDTH,
                SEGMENT_GAP,
                SEGMENT_HEIGHT,
                horizontalPadding
            );
        }

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}
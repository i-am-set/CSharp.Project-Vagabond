#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public event Action? OnComplete;

        // Progress bar animation
        private float _visualProgress = 0f;
        private float _progressAnimStart = 0f;
        private float _progressAnimEnd = 0f;
        private float _progressAnimTimer = 0f;
        private const float PROGRESS_ANIM_DURATION = 0.2f; // Shorter duration for quick segment fills

        // Segment-based animation state
        private int _currentSegmentToFill = 0;
        private float _segmentFillTimer = 0f;
        private const float MIN_SEGMENT_FILL_DELAY = 0.01f; // Minimum time between each segment fill
        private const int LOADING_BAR_SEGMENTS = 50;

        // Hold after complete
        private bool _allTasksComplete = false;

        // Visual state
        private bool _silentMode = false;

        public LoadingScreen()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Clear()
        {
            _tasks.Clear();
            _currentTaskIndex = -1;
            _totalProgress = 0f;
            _progressPerTask = 0f;
            IsActive = false;
            _allTasksComplete = false;
            _silentMode = false;
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

            // If any task is a DelayTask, we enter silent mode (no bar or text).
            _silentMode = _tasks.Any(t => t is DelayTask);

            IsActive = true;
            _currentTaskIndex = 0;
            _totalProgress = 0f;
            _progressPerTask = 1.0f / _tasks.Count;

            _visualProgress = 0f;
            _progressAnimStart = 0f;
            _progressAnimEnd = 0f;
            _progressAnimTimer = PROGRESS_ANIM_DURATION;

            _currentSegmentToFill = 0;
            _segmentFillTimer = 0f;

            _allTasksComplete = false;

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

            // Handle completion
            if (_allTasksComplete)
            {
                // The loading screen is finished once all tasks are done AND the visual progress bar has caught up.
                if (_progressAnimTimer >= PROGRESS_ANIM_DURATION)
                {
                    IsActive = false;
                    OnComplete?.Invoke();
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
                    _currentTaskIndex++;

                    if (_currentTaskIndex < _tasks.Count)
                    {
                        _tasks[_currentTaskIndex].Start();
                    }
                    else
                    {
                        _allTasksComplete = true;
                        _totalProgress = 1.0f; // Ensure it reaches exactly 100%
                    }
                }
            }

            // --- New Segment-Based Animation Logic ---
            if (_currentSegmentToFill < LOADING_BAR_SEGMENTS)
            {
                _segmentFillTimer += deltaTime;

                if (_segmentFillTimer >= MIN_SEGMENT_FILL_DELAY)
                {
                    _segmentFillTimer -= MIN_SEGMENT_FILL_DELAY;

                    // Calculate the progress required to fill the *next* segment.
                    float requiredProgress = (_currentSegmentToFill + 1) / (float)LOADING_BAR_SEGMENTS;

                    // Check if the actual loading has progressed far enough to allow the next segment to fill.
                    if (_totalProgress >= requiredProgress)
                    {
                        _currentSegmentToFill++;

                        // Start the visual animation to the new target progress.
                        _progressAnimStart = _visualProgress;
                        _progressAnimEnd = (float)_currentSegmentToFill / LOADING_BAR_SEGMENTS;
                        _progressAnimTimer = 0f;
                    }
                }
            }

            // Update progress bar visual interpolation
            if (_progressAnimTimer < PROGRESS_ANIM_DURATION)
            {
                _progressAnimTimer += deltaTime;
                float progress = MathHelper.Clamp(_progressAnimTimer / PROGRESS_ANIM_DURATION, 0f, 1f);
                _visualProgress = MathHelper.Lerp(_progressAnimStart, _progressAnimEnd, Easing.EaseOutQuad(progress));
            }
            else
            {
                _visualProgress = _progressAnimEnd;
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (!IsActive || _silentMode) return;

            var pixel = ServiceLocator.Get<Texture2D>();

            // --- Bar Style Parameters ---
            const int BAR_WIDTH = 100;
            const int BAR_HEIGHT = 1;
            const int TEXT_PADDING_ABOVE_BAR = 5;

            // 1. Calculate positions in virtual space
            int barX = (Global.VIRTUAL_WIDTH - BAR_WIDTH) / 2;
            int barY = (Global.VIRTUAL_HEIGHT - BAR_HEIGHT) / 2;
            int filledWidth = (int)(BAR_WIDTH * _visualProgress);

            // 2. Draw the background and filled portion of the loading bar
            var barBgRect = new Rectangle(barX, barY, BAR_WIDTH, BAR_HEIGHT);
            var barFillRect = new Rectangle(barX, barY, filledWidth, BAR_HEIGHT);
            spriteBatch.DrawSnapped(pixel, barBgRect, _global.Palette_Black);
            spriteBatch.DrawSnapped(pixel, barFillRect, _global.Palette_DarkGray);

            // 3. Get the current loading text
            string loadingText;
            if (_allTasksComplete)
            {
                loadingText = "Loading" + new string('.', _ellipsisCount);
            }
            else
            {
                string taskDescription = "";
                if (_currentTaskIndex >= 0 && _currentTaskIndex < _tasks.Count)
                {
                    taskDescription = _tasks[_currentTaskIndex].Description;
                }

                if (!string.IsNullOrEmpty(taskDescription))
                {
                    loadingText = taskDescription + new string('.', _ellipsisCount);
                }
                else
                {
                    // This handles DelayTask or other tasks with no description
                    loadingText = "";
                }
            }

            // 4. Calculate and draw the loading text, if any
            if (!string.IsNullOrEmpty(loadingText))
            {
                loadingText = loadingText.ToUpper();
                Vector2 textSize = font.MeasureString(loadingText);
                Vector2 textPosition = new Vector2(
                    (Global.VIRTUAL_WIDTH - textSize.X) / 2,
                    barY - textSize.Y - TEXT_PADDING_ABOVE_BAR
                );
                spriteBatch.DrawStringSnapped(font, loadingText, textPosition, _global.Palette_DarkGray);
            }
        }

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }

    /// <summary>
    /// A generic, synchronous loading task that executes a given Action.
    /// </summary>
    public class GenericTask : LoadingTask
    {
        private readonly Action _loadAction;

        public GenericTask(string description, Action loadAction) : base(description)
        {
            _loadAction = loadAction;
        }

        public override void Start()
        {
            // The action is executed synchronously and completes immediately.
            try
            {
                _loadAction?.Invoke();
            }
            catch (Exception ex)
            {
                // Log the error to the debug console to help diagnose loading issues.
                Debug.WriteLine($"[ERROR] Loading task '{Description}' failed: {ex.Message}");
                // Re-throw the exception to crash the game, ensuring data integrity issues are not ignored.
                throw;
            }
            finally
            {
                IsComplete = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            // This task completes instantly in Start(), so Update does nothing.
        }
    }
}
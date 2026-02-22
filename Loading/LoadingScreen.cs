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
        private struct LogEntry
        {
            public string Text;
            public bool IsSuccess;
        }

        private readonly Global _global;
        private readonly List<LoadingTask> _tasks = new List<LoadingTask>();
        private int _currentTaskIndex = -1;

        // Ellipsis animation
        private float _ellipsisTimer = 0f;
        private int _ellipsisCount = 0;

        public bool IsActive { get; private set; }
        public event Action? OnComplete;

        // --- Message Log System ---
        private readonly List<LogEntry> _messageLog = new List<LogEntry>();
        private const int MAX_LOG_LINES = 100;
        private const int SCREEN_MARGIN = 20;

        private bool _silentMode = false;

        public LoadingScreen()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Clear()
        {
            _tasks.Clear();
            _messageLog.Clear();
            _currentTaskIndex = -1;
            IsActive = false;
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
                OnComplete?.Invoke();
                return;
            }

            _silentMode = _tasks.Any(t => t is DelayTask);

            IsActive = true;
            _currentTaskIndex = 0;

            // Start the first task immediately
            _tasks[_currentTaskIndex].Start();
        }

        private void AddToLog(string message, bool success = true)
        {
            _messageLog.Add(new LogEntry { Text = message, IsSuccess = success });
            if (_messageLog.Count > MAX_LOG_LINES)
            {
                _messageLog.RemoveAt(0);
            }
        }

        public void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Visual update only (does not affect load speed)
            _ellipsisTimer += deltaTime;
            if (_ellipsisTimer > 0.5f)
            {
                _ellipsisTimer = 0f;
                _ellipsisCount = (_ellipsisCount + 1) % 4;
            }

            // Process Tasks
            if (_currentTaskIndex < _tasks.Count)
            {
                var currentTask = _tasks[_currentTaskIndex];
                currentTask.Update(gameTime);

                if (currentTask.IsComplete)
                {
                    // 1. Log completion immediately
                    AddToLog(currentTask.Description);

                    // 2. Move to next task immediately (No artificial delay)
                    _currentTaskIndex++;

                    if (_currentTaskIndex < _tasks.Count)
                    {
                        _tasks[_currentTaskIndex].Start();
                    }
                    else
                    {
                        // 3. All tasks done. Finish immediately.
                        FinishLoading();
                    }
                }
            }
        }

        private void FinishLoading()
        {
            IsActive = false;
            OnComplete?.Invoke();
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (!IsActive || _silentMode) return;

            // --- Layout Parameters ---
            int bottomY = Global.VIRTUAL_HEIGHT - SCREEN_MARGIN;
            int leftX = SCREEN_MARGIN;

            string okText = "OK";
            float okWidth = font.MeasureString(okText).Width;
            float rightX = Global.VIRTUAL_WIDTH - SCREEN_MARGIN - okWidth;

            // 1. Determine Current Active Line
            string currentLineText = "";
            if (_currentTaskIndex >= 0 && _currentTaskIndex < _tasks.Count)
            {
                string taskDescription = _tasks[_currentTaskIndex].Description;
                if (!string.IsNullOrEmpty(taskDescription))
                {
                    currentLineText = taskDescription + new string('.', _ellipsisCount);
                }
            }

            // 2. Draw Current Line (Bottom-most)
            float currentY = bottomY - font.LineHeight;

            if (!string.IsNullOrEmpty(currentLineText))
            {
                spriteBatch.DrawStringSnapped(font, currentLineText.ToUpper(), new Vector2(leftX, currentY), _global.Palette_DarkShadow);
            }

            // 3. Draw History Log (Stacked above current line)
            float logY = currentY;

            for (int i = _messageLog.Count - 1; i >= 0; i--)
            {
                logY -= (font.LineHeight + 2); // Move up one line + gap

                // Optimization: Stop drawing if off-screen
                if (logY < -font.LineHeight) break;

                var entry = _messageLog[i];

                // Draw Description (Left)
                spriteBatch.DrawStringSnapped(font, entry.Text.ToUpper(), new Vector2(leftX, logY), _global.Palette_DarkShadow);

                // Draw OK (Right)
                spriteBatch.DrawStringSnapped(font, okText, new Vector2(rightX, logY), _global.Palette_DarkShadow);
            }
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
            try
            {
                _loadAction?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Loading task '{Description}' failed: {ex.Message}");
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
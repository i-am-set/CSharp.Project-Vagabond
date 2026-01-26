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

        // --- Finishing & Clearing State ---
        private bool _allTasksComplete = false; // Logic is done
        private bool _isFinishing = false;      // Flavor text phase
        private bool _isClearing = false;       // New phase: Clearing log lines
        private float _clearTimer = 0f;

        // Tuning: Speed of clearing lines
        private const float CLEAR_LINE_DELAY = 0.05f;

        // --- Text State Machine ---
        private bool _isShowingOk = false; // True if the current line has finished and is showing OK
        private float _okTimer = 0f;
        private const float REAL_TASK_OK_DELAY = 0.1f; // How long real tasks pause with OK before moving on

        // --- Flavor Text System ---
        private string _currentFlavorText = "";
        private float _flavorTimer = 0f;

        // Variable durations for the "fake" work
        private float _currentFlavorWorkDuration = 0.05f;
        private float _currentFlavorOkDuration = 0.05f;

        // Tuning: Speed of text cycling during the "Finishing" phase
        private const float FLAVOR_WORK_MIN = 0.02f;
        private const float FLAVOR_WORK_MAX = 0.35f;
        private const float FLAVOR_OK_MIN = 0.05f;
        private const float FLAVOR_OK_MAX = 0.35f;

        // --- Message Log System ---
        private readonly List<LogEntry> _messageLog = new List<LogEntry>();
        private const int MAX_LOG_LINES = 100;
        private const int SCREEN_MARGIN = 20; // Increased margin

        // Retro/Terminal style loading messages
        private readonly string[] _masterFlavorTexts = new[]
        {
            "ALLOCATING 64KB CONVENTIONAL MEMORY",
            "CHECKING VRAM INTEGRITY",
            "LOADING SPRITE TABLES",
            "INITIALIZING SOUND CHANNELS",
            "READING CONFIG.DAT",
            "ACCESSING DISK CACHE",
            "ZEROING BUFFERS",
            "VERIFYING CHECKSUM",
            "RANDOMIZING SEED",
            "INITIALIZING RANDOM TABLES",
            "LOADING PALETTE DATA",
            "MAPPING MEMORY ADDRESSES",
            "SYNCING V-BLANK INTERRUPTS",
            "UNPACKING DATA FILES",
            "PREPARING MEMORY MANAGER",
            "TESTING I/O PORTS",
            "LOADING FONT GLYPHS",
            "INITIALIZING ENTITY TABLES",
            "PRE-CACHING PHYSICS HULLS"
        };

        private List<string> _availableFlavorTexts = new List<string>();
        private readonly Random _rng = new Random();

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
            _allTasksComplete = false;
            _isFinishing = false;
            _isClearing = false;
            _clearTimer = 0f;
            _silentMode = false;
            _isShowingOk = false;
            _okTimer = 0f;

            _availableFlavorTexts.Clear();
            _availableFlavorTexts.AddRange(_masterFlavorTexts);
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

            _silentMode = _tasks.Any(t => t is DelayTask);

            IsActive = true;
            _currentTaskIndex = 0;

            _allTasksComplete = false;
            _isFinishing = false;
            _isClearing = false;
            _isShowingOk = false;

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

            _ellipsisTimer += deltaTime;
            if (_ellipsisTimer > 0.5f)
            {
                _ellipsisTimer = 0f;
                _ellipsisCount = (_ellipsisCount + 1) % 4;
            }

            // 1. Process Real Logic Tasks
            if (!_allTasksComplete && _currentTaskIndex < _tasks.Count)
            {
                var currentTask = _tasks[_currentTaskIndex];

                if (!_isShowingOk)
                {
                    // Phase A: Doing Work
                    currentTask.Update(gameTime);

                    if (currentTask.IsComplete)
                    {
                        // Work done, switch to showing OK
                        _isShowingOk = true;
                        _okTimer = 0f;
                    }
                }
                else
                {
                    // Phase B: Showing OK
                    _okTimer += deltaTime;
                    if (_okTimer >= REAL_TASK_OK_DELAY)
                    {
                        // Delay done, log it and move on
                        AddToLog(currentTask.Description);

                        _currentTaskIndex++;
                        _isShowingOk = false;

                        if (_currentTaskIndex < _tasks.Count)
                        {
                            _tasks[_currentTaskIndex].Start();
                        }
                        else
                        {
                            _allTasksComplete = true;
                            _isFinishing = true; // Start the flavor text phase
                            PickNextFlavorText();
                        }
                    }
                }
            }
            // 2. Handle "Finishing" Phase (Flavor text loop)
            else if (_isFinishing)
            {
                if (!_isShowingOk)
                {
                    // Phase A: Simulating Work
                    _flavorTimer += deltaTime;
                    if (_flavorTimer >= _currentFlavorWorkDuration)
                    {
                        _isShowingOk = true;
                        _okTimer = 0f;
                    }
                }
                else
                {
                    // Phase B: Showing OK
                    _okTimer += deltaTime;
                    if (_okTimer >= _currentFlavorOkDuration)
                    {
                        // Log previous
                        if (!string.IsNullOrEmpty(_currentFlavorText))
                        {
                            AddToLog(_currentFlavorText);
                        }

                        // Check if we should stop flavor text (e.g. ran out or arbitrary limit)
                        // For now, let's just do a few lines then stop.
                        // Or, since we removed the bar, we can just stop when we run out of flavor text 
                        // or after a fixed number of lines.
                        // Let's stop when we have enough lines to look cool (e.g. 5 flavor lines).
                        // But for now, let's just transition to clearing immediately after one batch 
                        // or keep it simple: Stop when _availableFlavorTexts is low or just random chance?
                        // Let's just do 3 flavor lines for effect.

                        if (_availableFlavorTexts.Count < _masterFlavorTexts.Length - 3)
                        {
                            _isFinishing = false;
                            _isClearing = true;
                        }
                        else
                        {
                            PickNextFlavorText();
                        }
                    }
                }
            }
            // 3. Handle "Clearing" Phase (Sequential Disappearance)
            else if (_isClearing)
            {
                _clearTimer += deltaTime;
                if (_clearTimer >= CLEAR_LINE_DELAY)
                {
                    _clearTimer = 0f;
                    if (_messageLog.Count > 0)
                    {
                        // Remove from top (index 0)
                        _messageLog.RemoveAt(0);
                    }
                    else
                    {
                        // Log is empty, we are done
                        IsActive = false;
                        OnComplete?.Invoke();
                    }
                }
            }
        }

        private void PickNextFlavorText()
        {
            _isShowingOk = false;
            _flavorTimer = 0f;
            _okTimer = 0f;

            // Randomize durations for "glitchy/processing" feel
            _currentFlavorWorkDuration = (float)(_rng.NextDouble() * (FLAVOR_WORK_MAX - FLAVOR_WORK_MIN) + FLAVOR_WORK_MIN);
            _currentFlavorOkDuration = (float)(_rng.NextDouble() * (FLAVOR_OK_MAX - FLAVOR_OK_MIN) + FLAVOR_OK_MIN);

            if (_availableFlavorTexts.Count == 0)
            {
                _availableFlavorTexts.AddRange(_masterFlavorTexts);
            }

            int index = _rng.Next(_availableFlavorTexts.Count);
            _currentFlavorText = _availableFlavorTexts[index];
            _availableFlavorTexts.RemoveAt(index);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (!IsActive || _silentMode) return;

            // --- Layout Parameters ---
            int bottomY = Global.VIRTUAL_HEIGHT - SCREEN_MARGIN;
            int leftX = SCREEN_MARGIN;

            // Calculate Right Align X for OK
            // We assume OK width is roughly constant, or measure it.
            string okText = "OK";
            float okWidth = font.MeasureString(okText).Width;
            float rightX = Global.VIRTUAL_WIDTH - SCREEN_MARGIN - okWidth;

            // 1. Determine Current Active Line (if not clearing)
            string currentLineText = "";

            if (!_isClearing)
            {
                if (_isFinishing)
                {
                    currentLineText = _currentFlavorText;
                }
                else if (!_allTasksComplete)
                {
                    string taskDescription = "";
                    if (_currentTaskIndex >= 0 && _currentTaskIndex < _tasks.Count)
                    {
                        taskDescription = _tasks[_currentTaskIndex].Description;
                    }

                    if (!string.IsNullOrEmpty(taskDescription))
                    {
                        currentLineText = taskDescription + new string('.', _ellipsisCount);
                    }
                }
            }

            // 2. Draw Current Line (Bottom-most)
            float currentY = bottomY - font.LineHeight;

            if (!string.IsNullOrEmpty(currentLineText))
            {
                // Draw Description (Left)
                spriteBatch.DrawStringSnapped(font, currentLineText.ToUpper(), new Vector2(leftX, currentY), _global.Palette_Black);

                // Draw OK (Right) if ready
                if (_isShowingOk)
                {
                    spriteBatch.DrawStringSnapped(font, okText, new Vector2(rightX, currentY), _global.Palette_Black);
                }
            }

            // 3. Draw History Log (Stacked above current line)
            // Iterate backwards to draw newest closest to the current line
            float logY = currentY;

            // If we are clearing, the "current line" is gone, so log starts at bottomY
            if (_isClearing)
            {
                logY = bottomY;
            }

            for (int i = _messageLog.Count - 1; i >= 0; i--)
            {
                logY -= (font.LineHeight + 2); // Move up one line + gap

                // Optimization: Stop drawing if off-screen
                if (logY < -font.LineHeight) break;

                var entry = _messageLog[i];

                // Draw Description (Left)
                spriteBatch.DrawStringSnapped(font, entry.Text.ToUpper(), new Vector2(leftX, logY), _global.Palette_Black);

                // Draw OK (Right)
                // Assuming all log entries are successes for now
                spriteBatch.DrawStringSnapped(font, okText, new Vector2(rightX, logY), _global.Palette_Black);
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
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
        private const float PROGRESS_ANIM_DURATION = 0.2f;

        // Segment-based animation state
        private int _currentSegmentToFill = 0;
        private float _segmentFillTimer = 0f;
        private const float MIN_SEGMENT_FILL_DELAY = 0.01f;
        private const int LOADING_BAR_SEGMENTS = 50;

        // --- Finishing & Holding State ---
        private bool _allTasksComplete = false; // Logic is done
        private bool _isFinishing = false;      // Bar is filling to 100%
        private bool _isHolding = false;        // Bar is full, waiting for delay
        private float _holdTimer = 0f;
        private const float HOLD_DURATION = 1.0f; // Wait 1 second at 100%

        // --- Text State Machine ---
        private bool _isShowingOk = false; // True if the current line has finished and is showing [OK]
        private float _okTimer = 0f;
        private const float REAL_TASK_OK_DELAY = 0.1f; // How long real tasks pause with [OK] before moving on

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
        private readonly List<string> _messageLog = new List<string>();
        private const int MAX_LOG_LINES = 100;
        private const int SCREEN_MARGIN = 10;

        // Retro/Terminal style loading messages
        private readonly string[] _masterFlavorTexts = new[]
        {
            "ALLOCATING 64KB CONVENTIONAL MEMORY...",
            "CHECKING VRAM INTEGRITY...",
            "LOADING SPRITE TABLES...",
            "INITIALIZING SOUND CHANNELS...",
            "READING CONFIG.DAT...",
            "ACCESSING DISK CACHE...",
            "ZEROING BUFFERS...",
            "VERIFYING CHECKSUM...",
            "RANDOMIZING SEED...",
            "INITIALIZING RANDOM TABLES...",
            "LOADING PALETTE DATA...",
            "MAPPING MEMORY ADDRESSES...",
            "SYNCING V-BLANK INTERRUPTS...",
            "UNPACKING DATA FILES...",
            "PREPARING MEMORY MANAGER...",
            "TESTING I/O PORTS...",
            "LOADING FONT GLYPHS...",
            "INITIALIZING ENTITY TABLES...",
            "PRE-CACHING PHYSICS HULLS..."
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
            _totalProgress = 0f;
            _progressPerTask = 0f;
            IsActive = false;
            _allTasksComplete = false;
            _isFinishing = false;
            _isHolding = false;
            _holdTimer = 0f;
            _silentMode = false;
            _visualProgress = 0f;
            _currentSegmentToFill = 0;
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
            _totalProgress = 0f;
            _progressPerTask = 1.0f / _tasks.Count;

            _visualProgress = 0f;
            _progressAnimStart = 0f;
            _progressAnimEnd = 0f;
            _progressAnimTimer = PROGRESS_ANIM_DURATION;

            _currentSegmentToFill = 0;
            _segmentFillTimer = 0f;

            _allTasksComplete = false;
            _isFinishing = false;
            _isHolding = false;
            _holdTimer = 0f;
            _isShowingOk = false;

            _tasks[_currentTaskIndex].Start();
        }

        private void AddToLog(string message)
        {
            _messageLog.Add(message);
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
                    // Phase B: Showing [OK]
                    _okTimer += deltaTime;
                    if (_okTimer >= REAL_TASK_OK_DELAY)
                    {
                        // Delay done, log it and move on
                        AddToLog($"{currentTask.Description} [OK]");

                        _totalProgress = (_currentTaskIndex + 1) * _progressPerTask;
                        _currentTaskIndex++;
                        _isShowingOk = false;

                        if (_currentTaskIndex < _tasks.Count)
                        {
                            _tasks[_currentTaskIndex].Start();
                        }
                        else
                        {
                            _allTasksComplete = true;
                            _isFinishing = true; // Start the visual fill-up phase
                            PickNextFlavorText();
                        }
                    }
                }
            }

            // 2. Handle "Finishing" Phase (Filling bar to 100% with flavor text)
            if (_isFinishing)
            {
                // Force target to 100%
                _totalProgress = 1.0f;

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
                    // Phase B: Showing [OK]
                    _okTimer += deltaTime;
                    if (_okTimer >= _currentFlavorOkDuration)
                    {
                        // Log previous and pick next
                        if (!string.IsNullOrEmpty(_currentFlavorText))
                        {
                            AddToLog($"{_currentFlavorText} [OK]");
                        }
                        PickNextFlavorText();
                    }
                }

                // Accelerate visual progress to catch up
                _visualProgress = MathHelper.Lerp(_visualProgress, 1.0f, deltaTime * 5.0f);

                // Snap to 1.0 if very close
                if (_visualProgress >= 0.99f)
                {
                    _visualProgress = 1.0f;
                    _isFinishing = false;
                    _isHolding = true;

                    // Log the final flavor text if it's hanging there
                    if (!string.IsNullOrEmpty(_currentFlavorText))
                    {
                        AddToLog($"{_currentFlavorText} [OK]");
                    }
                }
            }
            // 3. Handle "Holding" Phase (Wait 1 second at 100%)
            else if (_isHolding)
            {
                _holdTimer += deltaTime;
                if (_holdTimer >= HOLD_DURATION)
                {
                    IsActive = false;
                    OnComplete?.Invoke();
                }
            }
            // 4. Normal Task Progress Animation
            else
            {
                // Segment-Based Animation Logic
                if (_currentSegmentToFill < LOADING_BAR_SEGMENTS)
                {
                    _segmentFillTimer += deltaTime;

                    if (_segmentFillTimer >= MIN_SEGMENT_FILL_DELAY)
                    {
                        _segmentFillTimer -= MIN_SEGMENT_FILL_DELAY;

                        float requiredProgress = (_currentSegmentToFill + 1) / (float)LOADING_BAR_SEGMENTS;

                        if (_totalProgress >= requiredProgress)
                        {
                            _currentSegmentToFill++;
                            _progressAnimStart = _visualProgress;
                            _progressAnimEnd = (float)_currentSegmentToFill / LOADING_BAR_SEGMENTS;
                            _progressAnimTimer = 0f;
                        }
                    }
                }

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

            var pixel = ServiceLocator.Get<Texture2D>();

            // --- Bar Style Parameters ---
            const int BAR_WIDTH = 300;
            const int BAR_HEIGHT = 1;
            const int TEXT_PADDING_ABOVE_BAR = 4;

            // 1. Calculate positions in virtual space (Bottom Left)
            int barX = SCREEN_MARGIN;
            int barY = Global.VIRTUAL_HEIGHT - SCREEN_MARGIN - BAR_HEIGHT;
            int filledWidth = (int)(BAR_WIDTH * _visualProgress);

            // 2. Draw the background and filled portion of the loading bar
            var barBgRect = new Rectangle(barX, barY, BAR_WIDTH, BAR_HEIGHT);
            var barFillRect = new Rectangle(barX, barY, filledWidth, BAR_HEIGHT);
            spriteBatch.DrawSnapped(pixel, barBgRect, Color.Black);
            spriteBatch.DrawSnapped(pixel, barFillRect, _global.Palette_Black);

            // 3. Determine Text to Display
            string currentLineText = "";
            Color currentColor = _global.Palette_Black;

            if (_isHolding)
            {
                currentLineText = "FINALIZING ENVIRONMENT... [OK]";
            }
            else if (_isFinishing)
            {
                currentLineText = _currentFlavorText;
            }
            else
            {
                // Normal Task Description
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

            // Append [OK] if in the showing state
            if (_isShowingOk && !_isHolding)
            {
                currentLineText += " [OK]";
            }

            // 4. Draw Current Line (Just above bar)
            float currentTextY = barY - font.LineHeight - TEXT_PADDING_ABOVE_BAR;
            if (!string.IsNullOrEmpty(currentLineText))
            {
                currentLineText = currentLineText.ToUpper();
                Vector2 textPosition = new Vector2(barX, currentTextY);
                spriteBatch.DrawStringSnapped(font, currentLineText, textPosition, currentColor);
            }

            // 5. Draw History Log (Stacked above current line)
            // Iterate backwards to draw newest closest to the current line
            float logY = currentTextY;
            Color logColor = _global.Palette_Black; // All text is Sun color

            for (int i = _messageLog.Count - 1; i >= 0; i--)
            {
                logY -= (font.LineHeight + 1); // Move up one line + 1px gap

                // Optimization: Stop drawing if off-screen
                if (logY < -font.LineHeight) break;

                string logText = _messageLog[i].ToUpper();
                spriteBatch.DrawStringSnapped(font, logText, new Vector2(barX, logY), logColor);
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
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

        // --- Flavor Text System ---
        private string _currentFlavorText = "";
        private float _flavorTimer = 0f;
        private float _currentFlavorDuration = 0.05f; // Variable duration

        // Tuning: Speed of text cycling during the "Finishing" phase
        private const float FLAVOR_DURATION_MIN = 0.02f;
        private const float FLAVOR_DURATION_MAX = 0.12f;

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

        // Working list to ensure no repeats until exhausted
        private List<string> _availableFlavorTexts = new List<string>();
        private readonly Random _rng = new Random();

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
            _isFinishing = false;
            _isHolding = false;
            _holdTimer = 0f;
            _silentMode = false;
            _visualProgress = 0f;
            _currentSegmentToFill = 0;

            // Reset flavor text bag
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
            _isFinishing = false;
            _isHolding = false;
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

            // 1. Process Logic Tasks
            if (!_allTasksComplete && _currentTaskIndex < _tasks.Count)
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
                        _isFinishing = true; // Start the visual fill-up phase

                        // Pick initial flavor text immediately
                        PickNextFlavorText();
                    }
                }
            }

            // 2. Handle "Finishing" Phase (Filling bar to 100% with flavor text)
            if (_isFinishing)
            {
                // Force target to 100%
                _totalProgress = 1.0f;

                // Cycle flavor text with variable speed
                _flavorTimer += deltaTime;
                if (_flavorTimer >= _currentFlavorDuration)
                {
                    PickNextFlavorText();
                }

                // Accelerate visual progress to catch up
                // We use a simple lerp here instead of the segment logic to ensure it finishes smoothly
                _visualProgress = MathHelper.Lerp(_visualProgress, 1.0f, deltaTime * 5.0f);

                // Snap to 1.0 if very close
                if (_visualProgress >= 0.99f)
                {
                    _visualProgress = 1.0f;
                    _isFinishing = false;
                    _isHolding = true;
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
        }

        private void PickNextFlavorText()
        {
            _flavorTimer = 0f;

            // Randomize duration for "glitchy/processing" feel
            _currentFlavorDuration = (float)(_rng.NextDouble() * (FLAVOR_DURATION_MAX - FLAVOR_DURATION_MIN) + FLAVOR_DURATION_MIN);

            if (_availableFlavorTexts.Count == 0)
            {
                // Refill if empty (unlikely to happen in one load, but safe)
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
            spriteBatch.DrawSnapped(pixel, barBgRect, _global.Palette_DarkShadow);
            spriteBatch.DrawSnapped(pixel, barFillRect, _global.Palette_Sun);

            // 3. Determine Text to Display
            string loadingText = "";
            Color textColor = _global.Palette_Sun;

            if (_isHolding)
            {
                loadingText = "FINALIZING ENVIRONMENT...";
                textColor = _global.Palette_Sun; // Bright color for completion
            }
            else if (_isFinishing)
            {
                // Show rapid-fire flavor text
                loadingText = _currentFlavorText;
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
                    loadingText = taskDescription + new string('.', _ellipsisCount);
                }
            }

            // 4. Calculate and draw the loading text
            if (!string.IsNullOrEmpty(loadingText))
            {
                loadingText = loadingText.ToUpper();
                Vector2 textSize = font.MeasureString(loadingText);
                Vector2 textPosition = new Vector2(
                    (Global.VIRTUAL_WIDTH - textSize.X) / 2,
                    barY - textSize.Y - TEXT_PADDING_ABOVE_BAR
                );
                spriteBatch.DrawStringSnapped(font, loadingText, textPosition, textColor);
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
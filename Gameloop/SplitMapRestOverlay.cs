using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Dice;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

namespace ProjectVagabond.UI
{
    public class SplitMapRestOverlay
    {
        public bool IsOpen { get; private set; } = false;

        // FIX: Ensure IsNarrating is false if the menu isn't even open.
        public bool IsNarrating => IsOpen && _menuState == RestMenuState.Narrating;

        // Signals that the entire sequence (selection + narration) is finished
        public event Action? OnRestCompleted;
        public event Action? OnLeaveRequested; // For Skip

        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly Core _core;
        private readonly GameState _gameState;
        private readonly HapticsManager _hapticsManager;

        private Button _confirmButton;
        private Button _skipButton;
        private ConfirmationDialog _confirmationDialog;

        // Internal Narrator for results
        private readonly StoryNarrator _narrator;

        // State Machine
        private enum RestMenuState { Selection, Narrating }
        private RestMenuState _menuState = RestMenuState.Selection;

        // Layout Constants
        private const float WORLD_Y_OFFSET = 600f;
        private const int BUTTON_HEIGHT = 15;

        // Slot Layout
        private readonly Rectangle[] _partyMemberPanelAreas = new Rectangle[4];
        private const int PANEL_WIDTH = 76;
        private const int PANEL_HEIGHT = 132;

        // Action Buttons
        private enum RestAction { Rest, Train, Search, Guard }
        private readonly Dictionary<int, RestAction> _selectedActions = new Dictionary<int, RestAction>();
        private readonly List<Button> _actionButtons = new List<Button>();

        // --- TUNING: Logic ---
        private const float HEAL_PERCENT_REST = 0.75f;
        private const float HEAL_PERCENT_TRAIN = 0.0f;
        private const float HEAL_PERCENT_SEARCH = 0.0f;
        private const float HEAL_PERCENT_GUARD = 0.0f;

        private const float GUARD_HEAL_MULTIPLIER = 2.0f;

        // Search Tuning
        private const int SEARCH_CHANCE_UNGUARDED = 50;
        private const int SEARCH_CHANCE_GUARDED = 90;

        // Train Tuning
        private const int TRAIN_AMOUNT_UNGUARDED = 1;
        private const int TRAIN_AMOUNT_GUARDED_MAJOR = 2;
        private const int TRAIN_AMOUNT_GUARDED_MINOR = 1;

        // --- TUNING: Colors ---
        private readonly Color COLOR_DESC_REST_NORMAL;
        private readonly Color COLOR_DESC_REST_GUARDED;
        private readonly Color COLOR_DESC_TRAIN_NORMAL;
        private readonly Color COLOR_DESC_TRAIN_GUARDED;
        private readonly Color COLOR_DESC_SEARCH_NORMAL;
        private readonly Color COLOR_DESC_SEARCH_GUARDED;
        private readonly Color COLOR_DESC_GUARD;

        // --- TUNING: Visuals ---
        private Color OVERLAY_COLOR_A = Color.Yellow;
        private Color OVERLAY_COLOR_B = Color.White;
        private const float OVERLAY_PULSE_SPEED = 8.0f;
        private const float HEAL_ANIMATION_SPEED = 5.0f; // Speed of lerp

        // --- TUNING: Text Pulse ---
        private const float TEXT_PULSE_SPEED = 4.0f;
        private const float TEXT_OPACITY_MIN = 0.75f;
        private const float TEXT_OPACITY_MAX = 1.0f;

        // --- TUNING: Sleep Particles ---
        private const float SLEEP_PARTICLE_SPEED = 9f;                 // Pixels per second moving up
        private const float SLEEP_PARTICLE_LIFETIME = 2.0f;             // How long a "Z" lasts
        private const float SLEEP_PARTICLE_SPAWN_INTERVAL_BASE = 1.0f;  // Base time between spawns
        private const float SLEEP_PARTICLE_SPAWN_INTERVAL_VARIANCE = 0.2f; // Random variance added to base
        private const float SLEEP_PARTICLE_SWAY_AMOUNT = 5f;            // Horizontal sway distance
        private const float SLEEP_PARTICLE_SWAY_SPEED = 3f;             // Speed of the sway
        private const float SLEEP_PARTICLE_WIND_SPEED = -5f;            // Horizontal drift speed (Negative = Left, Positive = Right)
        private const float SLEEP_PARTICLE_FADE_START_PERCENT = 0.7f;   // When to start fading out (0.0 to 1.0)

        // Spawn Position relative to the CENTER of the 32x32 portrait
        private const float SLEEP_PARTICLE_OFFSET_X = -12f;
        private const float SLEEP_PARTICLE_OFFSET_Y = -8f; // Negative is Up

        private readonly Color SLEEP_PARTICLE_COLOR;
        private readonly Color SLEEP_PARTICLE_OUTLINE_COLOR;

        private static readonly Random _rng = new Random();

        // Animation State for Health Bars
        private Dictionary<int, float> _visualHP = new Dictionary<int, float>();
        private Dictionary<int, float> _targetHP = new Dictionary<int, float>();
        private bool _isAnimatingHeal = false;
        private float _overlayPulseTimer = 0f;
        private float _textPulseTimer = 0f;

        // Hop Animation Controllers (One per slot)
        private readonly SpriteHopAnimationController[] _hopControllers = new SpriteHopAnimationController[4];

        // Portrait Animation Timers (One per slot) - Controls the Normal/Alt sprite toggle
        private readonly float[] _portraitAnimTimers = new float[4];

        // Sleep Particles
        private class SleepParticle
        {
            public Vector2 Position;
            public float Timer;
            public float MaxTime;
            public float SwayPhase;
            public float Speed;
            public int MemberIndex; // Track which member this particle belongs to
        }
        private readonly List<SleepParticle> _sleepParticles = new List<SleepParticle>();
        private readonly float[] _sleepSpawnTimers = new float[4];

        // --- SEQUENTIAL EXECUTION STATE ---
        private class RestSequenceStep
        {
            public int MemberIndex; // -1 for global/system messages
            public string Message;
            public Action Effect;
        }
        private readonly Queue<RestSequenceStep> _sequenceQueue = new Queue<RestSequenceStep>();
        private int _currentSpotlightIndex = -1; // -1 means no spotlight (or all visible)

        public SplitMapRestOverlay(GameScene parentScene)
        {
            _core = ServiceLocator.Get<Core>();
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();

            // Initialize Hop Controllers
            for (int i = 0; i < 4; i++)
            {
                _hopControllers[i] = new SpriteHopAnimationController();
            }

            // Initialize Tunable Colors
            COLOR_DESC_REST_NORMAL = _global.Palette_Leaf;
            COLOR_DESC_REST_GUARDED = Color.Lime;
            COLOR_DESC_TRAIN_NORMAL = _global.Palette_Shadow;
            COLOR_DESC_TRAIN_GUARDED = Color.Magenta;
            COLOR_DESC_SEARCH_NORMAL = _global.Palette_Sky;
            COLOR_DESC_SEARCH_GUARDED = Color.Aqua;
            COLOR_DESC_GUARD = _global.Palette_Shadow;

            // Initialize Sleep Particle Colors
            SLEEP_PARTICLE_COLOR = _global.Palette_Sun;
            SLEEP_PARTICLE_OUTLINE_COLOR = _global.Palette_Black;

            _confirmationDialog = new ConfirmationDialog(parentScene);

            // Initialize Narrator
            var narratorBounds = new Rectangle(0, Global.VIRTUAL_HEIGHT - 50, Global.VIRTUAL_WIDTH, 50);
            _narrator = new StoryNarrator(narratorBounds);

            // Hook up the sequence advancer instead of immediate completion
            _narrator.OnFinished += AdvanceSequence;

            _confirmButton = new Button(Rectangle.Empty, "CONFIRM", font: _core.SecondaryFont)
            {
                CustomDefaultTextColor = _global.Palette_Sun,
                CustomHoverTextColor = _global.Palette_Rust,
                UseScreenCoordinates = true
            };
            // Direct execution for Confirm, no dialog
            _confirmButton.OnClick += ExecuteRest;

            // Skip button uses Tertiary font
            _skipButton = new Button(Rectangle.Empty, "SKIP", font: _core.TertiaryFont)
            {
                CustomDefaultTextColor = _global.Palette_DarkShadow,
                CustomHoverTextColor = _global.ButtonHoverColor,
                UseScreenCoordinates = true
            };
            // Skip still requires confirmation
            _skipButton.OnClick += RequestSkipRest;
        }

        public void Show()
        {
            IsOpen = true;
            _menuState = RestMenuState.Selection;
            _narrator.Clear();
            InitializeActions();
            RebuildLayout();
            _sleepParticles.Clear();
            for (int i = 0; i < _sleepSpawnTimers.Length; i++) _sleepSpawnTimers[i] = 0f;
            for (int i = 0; i < _portraitAnimTimers.Length; i++) _portraitAnimTimers[i] = 0f; // Reset animation timers

            // Initialize Visual HP
            _visualHP.Clear();
            _targetHP.Clear();
            _isAnimatingHeal = false;
            _overlayPulseTimer = 0f;
            _textPulseTimer = 0f;
            _currentSpotlightIndex = -1;

            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                var member = _gameState.PlayerState.Party[i];
                _visualHP[i] = member.CurrentHP;
                _targetHP[i] = member.CurrentHP;
            }
        }

        public void Hide()
        {
            IsOpen = false;
            _menuState = RestMenuState.Selection; // FIX: Reset state so it doesn't get stuck
            _confirmationDialog.Hide();
            _narrator.Clear();
        }

        private void InitializeActions()
        {
            _selectedActions.Clear();
            int partyCount = _gameState.PlayerState.Party.Count;
            for (int i = 0; i < partyCount; i++)
            {
                _selectedActions[i] = RestAction.Rest; // Default to Rest
            }
        }

        private void RebuildLayout()
        {
            int centerX = Global.VIRTUAL_WIDTH / 2;
            int screenBottom = (int)WORLD_Y_OFFSET + Global.VIRTUAL_HEIGHT;

            int margin = 3;
            int buttonY = screenBottom - BUTTON_HEIGHT - margin;

            // Confirm Button (Centered)
            var font = _core.SecondaryFont;
            var confirmSize = font.MeasureString("CONFIRM");
            int confirmWidth = (int)confirmSize.Width + 16;
            _confirmButton.Bounds = new Rectangle((Global.VIRTUAL_WIDTH - confirmWidth) / 2, buttonY, confirmWidth, BUTTON_HEIGHT);

            // Skip Button (Bottom Right)
            var skipFont = _core.TertiaryFont; // Using Tertiary Font
            var skipSize = skipFont.MeasureString("SKIP");
            int skipWidth = (int)skipSize.Width + 16;
            _skipButton.Bounds = new Rectangle(Global.VIRTUAL_WIDTH - skipWidth - 10, buttonY, skipWidth, BUTTON_HEIGHT);

            // Panel Areas - Always 4 slots centered
            int totalPanelWidth = (4 * PANEL_WIDTH);
            int startX = (Global.VIRTUAL_WIDTH - totalPanelWidth) / 2;

            _actionButtons.Clear();

            for (int i = 0; i < 4; i++)
            {
                _partyMemberPanelAreas[i] = new Rectangle(
                    startX + (i * PANEL_WIDTH),
                    (int)WORLD_Y_OFFSET + 40, // Push down a bit
                    PANEL_WIDTH,
                    PANEL_HEIGHT
                );

                // Only create buttons for occupied slots
                if (i < _gameState.PlayerState.Party.Count)
                {
                    CreateActionButtonsForMember(i, _partyMemberPanelAreas[i]);
                }
            }
        }

        private void CreateActionButtonsForMember(int memberIndex, Rectangle panelRect)
        {
            int buttonWidth = 50;
            int buttonHeight = 10;
            int spacing = 1;
            // Anchor to bottom of panel
            int startY = panelRect.Bottom - (4 * (buttonHeight + spacing)) - 5 - 9;
            int centerX = panelRect.Center.X;

            // Helper to create toggle buttons
            void AddBtn(string text, RestAction action)
            {
                var btn = new ToggleButton(
                    new Rectangle(centerX - buttonWidth / 2, startY, buttonWidth, buttonHeight),
                    text,
                    font: _core.SecondaryFont,
                    customToggledTextColor: _global.Palette_DarkSun,
                    customDefaultTextColor: _global.Palette_Shadow
                )
                {
                    UseScreenCoordinates = true,
                    AlignLeft = true, // Align text to left
                    TextRenderOffset = new Vector2(8, -1), // Shift past icon (8px + 0px gap), adjust Y
                    DisableInputWhenSelected = true // Prevent clicking/hovering when already selected
                };

                // Guard Logic: If party size is 1, disable the Guard button but still show it.
                if (action == RestAction.Guard && _gameState.PlayerState.Party.Count <= 1)
                {
                    btn.IsEnabled = false;
                }

                btn.OnClick += () => SetAction(memberIndex, action);
                _actionButtons.Add(btn);
                startY += buttonHeight + spacing;
            }

            AddBtn("REST", RestAction.Rest);
            AddBtn("TRAIN", RestAction.Train);
            AddBtn("SEARCH", RestAction.Search);
            AddBtn("GUARD", RestAction.Guard);
        }

        private void SetAction(int memberIndex, RestAction action)
        {
            if (action == RestAction.Guard)
            {
                // Exclusive Logic: If setting Guard, unguard everyone else
                foreach (var key in _selectedActions.Keys.ToList())
                {
                    if (_selectedActions[key] == RestAction.Guard)
                    {
                        _selectedActions[key] = RestAction.Rest;
                    }
                }
            }
            else if (_selectedActions[memberIndex] == RestAction.Guard)
            {
                // If we were guarding and switched off, that's fine.
            }

            _selectedActions[memberIndex] = action;

            // Trigger the sprite hop animation for this member
            if (memberIndex >= 0 && memberIndex < _hopControllers.Length)
            {
                _hopControllers[memberIndex].Trigger();
            }

            // Reset portrait animation timer to sync with the hop
            if (memberIndex >= 0 && memberIndex < _portraitAnimTimers.Length)
            {
                _portraitAnimTimers[memberIndex] = 0f;
            }
        }

        private void RequestSkipRest()
        {
            _confirmationDialog.Show(
                "Skip resting entirely?",
                new List<Tuple<string, Action>>
                {
                    Tuple.Create("SKIP", new Action(() => { OnLeaveRequested?.Invoke(); _confirmationDialog.Hide(); })),
                    Tuple.Create("[chighlight]CANCEL", new Action(() => _confirmationDialog.Hide()))
                }
            );
        }

        private void ExecuteRest()
        {
            // Switch to Narrating state immediately
            _menuState = RestMenuState.Narrating;
            _sequenceQueue.Clear();
            _currentSpotlightIndex = -1;

            bool guardActive = _selectedActions.Values.Any(a => a == RestAction.Guard);
            var allRelics = BattleDataCache.Relics.Keys.ToList();

            string guardName = "";
            // Find Guard Name
            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                if (_selectedActions[i] == RestAction.Guard)
                {
                    guardName = _gameState.PlayerState.Party[i].Name.ToUpper();
                    break;
                }
            }

            // 1. Queue Guard(s) First
            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                if (_selectedActions[i] == RestAction.Guard)
                {
                    var member = _gameState.PlayerState.Party[i];
                    int idx = i; // Capture for lambda
                    _sequenceQueue.Enqueue(new RestSequenceStep
                    {
                        MemberIndex = idx,
                        Message = $"{member.Name} stood guard while the party rested.\n[cmodifier]+MODIFIER[/]",
                        Effect = () => { _targetHP[idx] = member.CurrentHP; } // No HP change
                    });
                }
            }

            // 2. Queue Everyone Else (Left to Right)
            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                if (_selectedActions[i] == RestAction.Guard) continue; // Skip guards

                var member = _gameState.PlayerState.Party[i];
                var action = _selectedActions[i];
                int idx = i; // Capture for lambda

                // Sync visual start point
                _visualHP[i] = member.CurrentHP;

                // Calculate multiplier for this member
                float multiplier = guardActive ? GUARD_HEAL_MULTIPLIER : 1.0f;

                switch (action)
                {
                    case RestAction.Rest:
                        {
                            int oldHP = member.CurrentHP;
                            int healAmount = (int)(member.MaxHP * HEAL_PERCENT_REST * multiplier);
                            int newHP = Math.Min(member.MaxHP, member.CurrentHP + healAmount);

                            int actualHealed = newHP - oldHP;
                            int percentDisplay = (int)((float)actualHealed / member.MaxHP * 100f);

                            string msg;
                            if (guardActive) msg = $"THANKS TO {guardName}, {member.Name} RECOVERED WELL!\n";
                            else msg = $"{member.Name} RESTED.\n";
                            msg += $"[chealth]+{percentDisplay}% HP[/]";

                            _sequenceQueue.Enqueue(new RestSequenceStep
                            {
                                MemberIndex = idx,
                                Message = msg,
                                Effect = () =>
                                {
                                    member.CurrentHP = newHP;
                                    _targetHP[idx] = newHP;
                                    _isAnimatingHeal = true; // Trigger animation for this step
                                }
                            });
                            break;
                        }

                    case RestAction.Train:
                        {
                            string[] stats = { "Strength", "Intelligence", "Tenacity", "Agility" };
                            string msg = "";
                            Action effectAction;

                            if (guardActive)
                            {
                                // Guarded: +2 to one, +1 to another
                                int idx1 = _rng.Next(4);
                                int idx2;
                                do { idx2 = _rng.Next(4); } while (idx2 == idx1);

                                msg = $"THANKS TO {guardName}, {member.Name} FOCUSED!\n";
                                msg += $"{GetStatTag(stats[idx1])}+{TRAIN_AMOUNT_GUARDED_MAJOR} {stats[idx1].ToUpper()}[/]  {GetStatTag(stats[idx2])}+{TRAIN_AMOUNT_GUARDED_MINOR} {stats[idx2].ToUpper()}[/]";

                                effectAction = () =>
                                {
                                    ApplyStatBoost(member, stats[idx1], TRAIN_AMOUNT_GUARDED_MAJOR);
                                    ApplyStatBoost(member, stats[idx2], TRAIN_AMOUNT_GUARDED_MINOR);
                                    _targetHP[idx] = member.CurrentHP;
                                };
                            }
                            else
                            {
                                // Unguarded: +1 to one
                                int idx1 = _rng.Next(4);
                                msg = $"{member.Name} TRAINED.\n";
                                msg += $"{GetStatTag(stats[idx1])}+{TRAIN_AMOUNT_UNGUARDED} {stats[idx1].ToUpper()}[/]";

                                effectAction = () =>
                                {
                                    ApplyStatBoost(member, stats[idx1], TRAIN_AMOUNT_UNGUARDED);
                                    _targetHP[idx] = member.CurrentHP;
                                };
                            }

                            _sequenceQueue.Enqueue(new RestSequenceStep
                            {
                                MemberIndex = idx,
                                Message = msg,
                                Effect = effectAction
                            });
                            break;
                        }

                    case RestAction.Search:
                        {
                            int chance = guardActive ? SEARCH_CHANCE_GUARDED : SEARCH_CHANCE_UNGUARDED;
                            string msg;
                            Action effectAction;

                            if (guardActive) msg = $"THANKS TO {guardName}, {member.Name} LOOKED THOROUGHLY!\n";
                            else msg = $"{member.Name} SEARCHED.\nThey looked around cautiously.\n";

                            if (_rng.Next(0, 100) < chance)
                            {
                                if (allRelics.Any())
                                {
                                    string relicId = allRelics[_rng.Next(allRelics.Count)];
                                    var relic = BattleDataCache.Relics[relicId];
                                    // REMOVED: Rarity tag logic
                                    msg += $"Found [pop][cItem]{relic.RelicName}[/][/]!";

                                    effectAction = () =>
                                    {
                                        _gameState.PlayerState.AddRelic(relicId);
                                        _targetHP[idx] = member.CurrentHP;
                                    };
                                }
                                else
                                {
                                    msg += "[cdull]Found nothing (Empty DB).[/]";
                                    effectAction = () => { _targetHP[idx] = member.CurrentHP; };
                                }
                            }
                            else
                            {
                                msg += "[cdull]Found nothing.[/]";
                                effectAction = () => { _targetHP[idx] = member.CurrentHP; };
                            }

                            _sequenceQueue.Enqueue(new RestSequenceStep
                            {
                                MemberIndex = idx,
                                Message = msg,
                                Effect = effectAction
                            });
                            break;
                        }
                }
            }

            // Add final completion message
            _sequenceQueue.Enqueue(new RestSequenceStep
            {
                MemberIndex = -1, // Global spotlight (or none)
                Message = "[wave]REST COMPLETE![/]",
                Effect = () => { _currentSpotlightIndex = -1; }
            });

            // Start the sequence
            ProcessNextSequenceStep();
        }

        private void ProcessNextSequenceStep()
        {
            if (_sequenceQueue.Count > 0)
            {
                var step = _sequenceQueue.Dequeue();

                // Set spotlight
                _currentSpotlightIndex = step.MemberIndex;

                // Execute logic (apply stats/heals)
                step.Effect?.Invoke();

                // Show text
                _narrator.Show(step.Message);
            }
            else
            {
                // Sequence finished
                OnRestCompleted?.Invoke();
            }
        }

        private void AdvanceSequence()
        {
            // This is called when the narrator finishes a message (user clicked/pressed space)
            // We check if there are more steps in the queue.
            if (_sequenceQueue.Count > 0)
            {
                ProcessNextSequenceStep();
            }
            else
            {
                // If queue is empty, we are done.
                // The last step was "REST COMPLETE!", so now we exit.
                OnRestCompleted?.Invoke();
            }
        }

        private string GetStatTag(string statName)
        {
            // Use property names from Global.cs so StoryNarrator reflection can find them
            return statName switch
            {
                "Strength" => "[StatColor_Strength]",
                "Intelligence" => "[StatColor_Intelligence]",
                "Tenacity" => "[StatColor_Tenacity]",
                "Agility" => "[StatColor_Agility]",
                _ => "[Palette_Sun]"
            };
        }

        private void ApplyStatBoost(PartyMember member, string stat, int amount)
        {
            switch (stat)
            {
                case "Strength": member.Strength += amount; break;
                case "Intelligence": member.Intelligence += amount; break;
                case "Tenacity": member.Tenacity += amount; break;
                case "Agility": member.Agility += amount; break;
            }
        }

        public void Update(GameTime gameTime, MouseState mouseState, Matrix cameraTransform)
        {
            if (!IsOpen) return;

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Update(gameTime);
                return; // Block other input
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update Hop Controllers
            foreach (var controller in _hopControllers)
            {
                controller.Update(gameTime);
            }

            // Update Portrait Animation Timers
            for (int i = 0; i < 4; i++)
            {
                _portraitAnimTimers[i] += dt;
            }

            // Update Pulse
            _overlayPulseTimer += dt * OVERLAY_PULSE_SPEED;

            // Update Text Pulse
            _textPulseTimer += dt * TEXT_PULSE_SPEED;

            // Update Heal Animation
            if (_isAnimatingHeal)
            {
                bool allDone = true;
                // Only animate the currently spotlighted member if one is selected
                // Or animate all if no spotlight (fallback)
                int startIndex = _currentSpotlightIndex != -1 ? _currentSpotlightIndex : 0;
                int endIndex = _currentSpotlightIndex != -1 ? _currentSpotlightIndex + 1 : _gameState.PlayerState.Party.Count;

                for (int i = startIndex; i < endIndex; i++)
                {
                    if (i >= _gameState.PlayerState.Party.Count) continue;

                    float current = _visualHP[i];
                    float target = _targetHP[i];

                    if (Math.Abs(target - current) > 0.1f)
                    {
                        _visualHP[i] = MathHelper.Lerp(_visualHP[i], target, HEAL_ANIMATION_SPEED * dt);
                        if (Math.Abs(target - _visualHP[i]) < 0.5f) _visualHP[i] = target;
                        allDone = false;
                    }
                }
                if (allDone) _isAnimatingHeal = false;
            }
            else if (_menuState == RestMenuState.Selection)
            {
                // Keep visual HP synced in selection mode
                for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
                {
                    _visualHP[i] = _gameState.PlayerState.Party[i].CurrentHP;
                }
            }

            // Update Sleep Particles (Always update to keep them moving)
            UpdateParticles(dt);

            // If narrating, only update the narrator and block all other input
            if (_menuState == RestMenuState.Narrating)
            {
                _narrator.Update(gameTime);
                return;
            }

            // Transform mouse to world space
            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));

            // Fake mouse state for world space buttons
            var worldMouseState = new MouseState((int)mouseInWorldSpace.X, (int)mouseInWorldSpace.Y, mouseState.ScrollWheelValue, mouseState.LeftButton, mouseState.MiddleButton, mouseState.RightButton, mouseState.XButton1, mouseState.XButton2);

            // Update Action Buttons
            for (int i = 0; i < _actionButtons.Count; i++)
            {
                var btn = _actionButtons[i];
                btn.Update(worldMouseState);
            }

            // Sync Toggle States
            int btnIndex = 0;
            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                // Rest
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Rest;
                // Train
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Train;
                // Search
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Search;
                // Guard (Conditional)
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Guard;
            }

            _confirmButton.Update(worldMouseState);
            _skipButton.Update(worldMouseState);
        }

        private void UpdateParticles(float dt)
        {
            // Update existing particles
            for (int i = _sleepParticles.Count - 1; i >= 0; i--)
            {
                var p = _sleepParticles[i];

                // Check if the member is still resting. If not, remove the particle immediately.
                if (_selectedActions.TryGetValue(p.MemberIndex, out var action) && action != RestAction.Rest)
                {
                    _sleepParticles.RemoveAt(i);
                    continue;
                }

                p.Timer += dt;
                if (p.Timer >= p.MaxTime)
                {
                    _sleepParticles.RemoveAt(i);
                    continue;
                }

                // Move Up
                p.Position.Y -= p.Speed * dt;

                // Sway + Wind
                float swayOffset = MathF.Sin(p.Timer * SLEEP_PARTICLE_SWAY_SPEED + p.SwayPhase) * SLEEP_PARTICLE_SWAY_AMOUNT;
                float windOffset = SLEEP_PARTICLE_WIND_SPEED;

                p.Position.X += (swayOffset + windOffset) * dt;
            }

            // Spawn new particles
            for (int i = 0; i < 4; i++)
            {
                if (i >= _gameState.PlayerState.Party.Count) continue;

                if (_selectedActions.TryGetValue(i, out var action) && action == RestAction.Rest)
                {
                    _sleepSpawnTimers[i] -= dt;
                    if (_sleepSpawnTimers[i] <= 0)
                    {
                        _sleepSpawnTimers[i] = SLEEP_PARTICLE_SPAWN_INTERVAL_BASE + (float)(_rng.NextDouble() * SLEEP_PARTICLE_SPAWN_INTERVAL_VARIANCE);
                        SpawnSleepParticle(i);
                    }
                }
                else
                {
                    _sleepSpawnTimers[i] = 0; // Reset so it spawns immediately when switched to Rest
                }
            }
        }

        private void SpawnSleepParticle(int memberIndex)
        {
            var panelRect = _partyMemberPanelAreas[memberIndex];
            var defaultFont = ServiceLocator.Get<BitmapFont>();
            float nameHeight = defaultFont.LineHeight;

            // Calculate portrait top-right position
            // Portrait is drawn at: centerX - 16, currentY
            // currentY = panelRect.Y + 4 + nameHeight - 2

            float portraitY = panelRect.Y + 4 + nameHeight - 2;
            float centerX = panelRect.Center.X;

            // Calculate center of the 32x32 portrait
            Vector2 portraitCenter = new Vector2(centerX, portraitY + 16);

            // Apply tunable offset from center
            Vector2 spawnPos = portraitCenter + new Vector2(SLEEP_PARTICLE_OFFSET_X, SLEEP_PARTICLE_OFFSET_Y);

            _sleepParticles.Add(new SleepParticle
            {
                Position = spawnPos,
                Timer = 0f,
                MaxTime = SLEEP_PARTICLE_LIFETIME,
                SwayPhase = (float)(_rng.NextDouble() * Math.PI * 2),
                Speed = SLEEP_PARTICLE_SPEED,
                MemberIndex = memberIndex // Track owner
            });
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsOpen) return;

            var pixelTex = ServiceLocator.Get<Texture2D>();
            var secondaryFont = _core.SecondaryFont;
            var defaultFont = ServiceLocator.Get<BitmapFont>();
            var tertiaryFont = _core.TertiaryFont;

            // Draw Background
            var bgRectDraw = new Rectangle(0, (int)WORLD_Y_OFFSET, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            spriteBatch.DrawSnapped(pixelTex, bgRectDraw, _global.GameBg);

            // Draw Border
            if (_spriteManager.RestBorderMain != null)
            {
                spriteBatch.DrawSnapped(_spriteManager.RestBorderMain, new Vector2(0, WORLD_Y_OFFSET), Color.White);
            }

            // Title
            string title = "REST";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, WORLD_Y_OFFSET + 10);
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_Sun);

            // Check if anyone is guarding to calculate potential heal multiplier
            bool guardActive = _selectedActions.Values.Any(a => a == RestAction.Guard);

            // Draw Party Panels
            for (int i = 0; i < 4; i++)
            {
                var bounds = _partyMemberPanelAreas[i];
                bool isOccupied = i < _gameState.PlayerState.Party.Count;
                var member = isOccupied ? _gameState.PlayerState.Party[i] : null;

                // --- SPOTLIGHT LOGIC ---
                // If narrating, and this is NOT the spotlighted member, dim it.
                // If spotlight index is -1 (e.g. "Rest Complete"), don't dim anyone (or dim everyone? usually none).
                // Let's say if index != -1, we dim everyone else.
                bool isDimmed = false;
                if (_menuState == RestMenuState.Narrating && _currentSpotlightIndex != -1)
                {
                    if (i != _currentSpotlightIndex)
                    {
                        isDimmed = true;
                    }
                }

                int centerX = bounds.Center.X;
                int currentY = bounds.Y + 4;

                // 1. Name (Calculated here, drawn later to be on top)
                string name = isOccupied ? member!.Name.ToUpper() : "EMPTY";
                Color nameColor = isOccupied ? _global.Palette_Sun : _global.Palette_DarkShadow;

                var nameSize = defaultFont.MeasureString(name);
                Vector2 namePos = new Vector2(centerX - nameSize.Width / 2, currentY);

                // Advance Y for background drawing
                currentY += (int)nameSize.Height - 2;

                // 2. Portrait
                if (isOccupied && _spriteManager.PlayerMasterSpriteSheet != null)
                {
                    int portraitIndex = member!.PortraitIndex;
                    PlayerSpriteType type;

                    // Check if resting
                    if (_selectedActions.TryGetValue(i, out var action) && action == RestAction.Rest)
                    {
                        // Sleeping: Use sleep sprite, no animation
                        type = PlayerSpriteType.Sleep;
                    }
                    else
                    {
                        // Awake: Toggle animation
                        float animSpeed = 1f;
                        // Use local timer for independent animation
                        int frame = (int)(_portraitAnimTimers[i] * animSpeed) % 2;
                        type = frame == 0 ? PlayerSpriteType.Normal : PlayerSpriteType.Alt;
                    }

                    var sourceRect = _spriteManager.GetPlayerSourceRect(portraitIndex, type);

                    // Apply Hop Offset
                    float hopOffset = _hopControllers[i].GetOffset(true); // True = Invert (Up)

                    // --- NEW BOBBING LOGIC ---
                    float bobSpeed = 2.5f; // Default Idle Speed
                    float bobAmp = 0.5f;   // Default Idle Amplitude

                    // Stagger the bob based on index to prevent unison movement
                    float phase = i * 0.7f; // FIXED: Use 'i' instead of 'index'
                    float bob = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * bobSpeed + phase) * bobAmp;
                    hopOffset += bob;

                    // Use Vector2 for smooth sub-pixel rendering
                    Vector2 portraitPos = new Vector2(centerX - 16, currentY + hopOffset);

                    spriteBatch.DrawSnapped(_spriteManager.PlayerMasterSpriteSheet, portraitPos, sourceRect, Color.White);
                }

                // Draw Name NOW (On top of background/shadow)
                spriteBatch.DrawStringSnapped(defaultFont, name, namePos, nameColor);

                currentY += 32 + 2 - 6;

                // 3. Health Bar
                if (_spriteManager.InventoryPlayerHealthBarEmpty != null)
                {
                    int barX = centerX - (_spriteManager.InventoryPlayerHealthBarEmpty.Width / 2);
                    spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarEmpty, new Vector2(barX, currentY), Color.White);

                    if (isOccupied && _spriteManager.InventoryPlayerHealthBarFull != null)
                    {
                        // Use _visualHP for the red bar
                        float currentVisualHP = _visualHP.ContainsKey(i) ? _visualHP[i] : member!.CurrentHP;
                        float hpPercent = currentVisualHP / Math.Max(1, member!.MaxHP);
                        int visibleWidth = (int)(_spriteManager.InventoryPlayerHealthBarFull.Width * hpPercent);
                        var srcRect = new Rectangle(0, 0, visibleWidth, _spriteManager.InventoryPlayerHealthBarFull.Height);
                        spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, currentY), srcRect, Color.White);

                        // --- NEW: Draw Healing Preview Overlay ---
                        // Determine target HP (either projected or actual target if animating)
                        float targetHP = currentVisualHP;
                        bool showOverlay = false;

                        if (_isAnimatingHeal)
                        {
                            targetHP = _targetHP[i];
                            showOverlay = targetHP > currentVisualHP;
                        }
                        else if (_selectedActions.TryGetValue(i, out var healAction) && healAction == RestAction.Rest)
                        {
                            float multiplier = (guardActive && healAction != RestAction.Guard) ? GUARD_HEAL_MULTIPLIER : 1.0f;
                            int healAmount = (int)(member.MaxHP * HEAL_PERCENT_REST * multiplier);
                            targetHP = Math.Min(member.MaxHP, member.CurrentHP + healAmount);
                            showOverlay = targetHP > member.CurrentHP;
                        }

                        if (showOverlay && _spriteManager.InventoryPlayerHealthBarOverlay != null)
                        {
                            int fullWidth = _spriteManager.InventoryPlayerHealthBarFull.Width;
                            float currentPercent = currentVisualHP / member.MaxHP;
                            float projectedPercent = targetHP / member.MaxHP;

                            int startPixel = (int)(fullWidth * currentPercent);
                            int endPixel = (int)(fullWidth * projectedPercent);
                            int overlayWidth = endPixel - startPixel;

                            if (overlayWidth > 0)
                            {
                                var srcOverlay = new Rectangle(startPixel, 0, overlayWidth, 7);

                                // Pulse Color
                                float t = (MathF.Sin(_overlayPulseTimer) + 1f) / 2f;
                                Color overlayColor = Color.Lerp(OVERLAY_COLOR_A, OVERLAY_COLOR_B, t);

                                spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarOverlay, new Vector2(barX + 1 + startPixel, currentY), srcOverlay, overlayColor);
                            }
                        }
                    }

                    string hpValText = isOccupied ? $"{member!.CurrentHP}/{member.MaxHP}" : "0/0";
                    string hpSuffix = " HP";

                    var valSize = secondaryFont.MeasureString(hpValText);
                    var suffixSize = secondaryFont.MeasureString(hpSuffix);

                    float hpTextX = centerX - ((valSize.Width + suffixSize.Width) / 2f);
                    float hpTextY = currentY + 7;

                    Color hpValColor = isOccupied ? _global.Palette_Sun : _global.Palette_DarkShadow;
                    spriteBatch.DrawStringSnapped(secondaryFont, hpValText, new Vector2(hpTextX, hpTextY), hpValColor);
                    spriteBatch.DrawStringSnapped(secondaryFont, hpSuffix, new Vector2(hpTextX + valSize.Width, hpTextY), _global.Palette_Shadow);

                    // --- NEW: Draw Action Description ---
                    if (isOccupied && _selectedActions.TryGetValue(i, out var descAction))
                    {
                        string descText = "";
                        Color descColor = _global.Palette_Sun;

                        // Determine multiplier (Guard doesn't buff itself)
                        float multiplier = (guardActive && descAction != RestAction.Guard) ? GUARD_HEAL_MULTIPLIER : 1.0f;

                        switch (descAction)
                        {
                            case RestAction.Rest:
                                int finalPercent = (int)(HEAL_PERCENT_REST * multiplier * 100);
                                descText = $"+{finalPercent}% HP";
                                if (guardActive)
                                {
                                    descColor = COLOR_DESC_REST_GUARDED;
                                }
                                else
                                {
                                    descColor = COLOR_DESC_REST_NORMAL;
                                }
                                break;

                            case RestAction.Train:
                                if (guardActive)
                                {
                                    descText = $"+{TRAIN_AMOUNT_GUARDED_MAJOR} STAT\n+{TRAIN_AMOUNT_GUARDED_MINOR} STAT";
                                    descColor = COLOR_DESC_TRAIN_GUARDED;
                                }
                                else
                                {
                                    descText = $"+{TRAIN_AMOUNT_UNGUARDED} STAT";
                                    descColor = COLOR_DESC_TRAIN_NORMAL;
                                }
                                break;

                            case RestAction.Search:
                                int chance = guardActive ? SEARCH_CHANCE_GUARDED : SEARCH_CHANCE_UNGUARDED;
                                descText = $"{chance}% RELIC";
                                descColor = guardActive ? COLOR_DESC_SEARCH_GUARDED : COLOR_DESC_SEARCH_NORMAL;
                                break;

                            case RestAction.Guard:
                                descText = "+MODIFIER";
                                descColor = COLOR_DESC_GUARD;
                                break;
                        }

                        // Split by newline
                        var lines = descText.Split('\n');

                        // --- VERTICAL CENTERING LOGIC ---
                        // Calculate the area between the HP text and the buttons
                        // Button layout constants from CreateActionButtonsForMember:
                        // buttonHeight = 10, spacing = 1, 4 buttons.
                        // startY = panelRect.Bottom - (4 * 11) - 5 - 9;
                        float buttonsTopY = bounds.Bottom - (4 * 11) - 14;

                        float textTopBoundary = hpTextY + secondaryFont.LineHeight + 2; // +2 padding from HP text
                        float availableHeight = buttonsTopY - textTopBoundary;

                        // Calculate total text height
                        // LineHeight + 1 pixel spacing per line
                        float totalTextHeight = lines.Length * (secondaryFont.LineHeight + 1) - 1;

                        // Center it
                        float startDescY = textTopBoundary + (availableHeight - totalTextHeight) / 2f;

                        // Clamp to top if text exceeds space (prevents overlap with stats)
                        if (startDescY < textTopBoundary) startDescY = textTopBoundary;

                        float descY = startDescY;

                        // --- PULSE OPACITY LOGIC ---
                        float pulseOpacity = MathHelper.Lerp(TEXT_OPACITY_MIN, TEXT_OPACITY_MAX, (MathF.Sin(_textPulseTimer * TEXT_PULSE_SPEED) + 1f) / 2f);
                        Color finalDescColor = descColor * pulseOpacity;

                        foreach (var line in lines)
                        {
                            var lineSize = secondaryFont.MeasureString(line);
                            float lineX = centerX - (lineSize.Width / 2f);

                            // --- ANIMATION: Bob Logic ---
                            // REMOVED: User requested static text.
                            float bobOffset = 0f;

                            // IMPORTANT: Round the base Y first, then add the integer bob offset.
                            // This prevents sub-pixel centering from eating the 1-pixel animation.
                            float finalY = MathF.Round(descY) + bobOffset;

                            spriteBatch.DrawStringSnapped(secondaryFont, line, new Vector2(lineX, finalY), finalDescColor);
                            descY += secondaryFont.LineHeight + 1; // Spacing between lines
                        }
                    }

                    currentY += 8 + (int)valSize.Height + 4 - 3;
                }

                // Draw Sleep Particles for this member
                foreach (var p in _sleepParticles)
                {
                    if (p.MemberIndex == i)
                    {
                        float alpha = 1.0f;
                        if (p.Timer > p.MaxTime * SLEEP_PARTICLE_FADE_START_PERCENT)
                        {
                            float fadeDuration = p.MaxTime * (1.0f - SLEEP_PARTICLE_FADE_START_PERCENT);
                            float timeInFade = p.Timer - (p.MaxTime * SLEEP_PARTICLE_FADE_START_PERCENT);
                            alpha = 1.0f - (timeInFade / fadeDuration);
                        }
                        // Use Square Outline for Zs
                        spriteBatch.DrawStringSquareOutlinedSnapped(secondaryFont, "Z", p.Position, SLEEP_PARTICLE_COLOR * alpha, SLEEP_PARTICLE_OUTLINE_COLOR * alpha);
                    }
                }

                // --- APPLY DIMMER IF NEEDED ---
                if (isDimmed)
                {
                    // Draw a semi-transparent black box over the entire panel area
                    spriteBatch.DrawSnapped(pixelTex, bounds, Color.Black * 0.7f);
                }
            }

            // Draw Action Buttons
            for (int i = 0; i < _actionButtons.Count; i++)
            {
                var btn = (ToggleButton)_actionButtons[i];
                btn.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);

                // Calculate Bob Offset (Match ToggleButton logic)
                float bobOffset = 0f;
                if (btn.IsSelected)
                {
                    float speed = 5f;
                    float val = MathF.Round((MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * speed) + 1f) * 0.5f);
                    bobOffset = -val;
                }

                // Draw Icon
                int actionIndex = i % 4; // 0=Rest, 1=Train, 2=Search, 3=Guard
                int stateIndex = 0; // Idle
                if (btn.IsSelected) stateIndex = 2; // Selected
                else if (btn.IsHovered) stateIndex = 1; // Hover

                var iconRect = _spriteManager.GetRestActionIconRect(actionIndex, stateIndex);

                // Calculate position: Left aligned in button, centered vertically
                // Button Height is 10. Icon is 8. Y offset = 1.
                // Apply bobOffset to Y
                Vector2 iconPos = new Vector2(btn.Bounds.X + 1, btn.Bounds.Y + 1 + bobOffset);

                spriteBatch.DrawSnapped(_spriteManager.RestActionIconsSpriteSheet, iconPos, iconRect, Color.White);
            }

            _confirmButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            _skipButton.Draw(spriteBatch, tertiaryFont, gameTime, Matrix.Identity); // Use Tertiary Font

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                foreach (var rect in _partyMemberPanelAreas)
                {
                    spriteBatch.DrawSnapped(pixelTex, rect, Color.Blue * 0.2f);
                }
                foreach (var btn in _actionButtons)
                {
                    spriteBatch.DrawSnapped(pixelTex, btn.Bounds, Color.Green * 0.5f);
                }
            }

            // --- NARRATION OVERLAY ---
            // If narrating, draw a dimmer and the narrator box on top of everything
            if (_menuState == RestMenuState.Narrating)
            {
                // Dimmer
                // spriteBatch.DrawSnapped(pixelTex, bgRectDraw, Color.Black * 0.7f); // REMOVED DIMMER

                // Narrator
                _narrator.Draw(spriteBatch, secondaryFont, gameTime);
            }
        }

        public void DrawDialogOverlay(SpriteBatch spriteBatch)
        {
            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.DrawOverlay(spriteBatch);
            }
        }

        public void DrawDialogContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_confirmationDialog.IsActive)
            {
                // Draw in screen space (Matrix.Identity)
                _confirmationDialog.DrawContent(spriteBatch, font, gameTime, Matrix.Identity);
            }

            // Draw Narrator if active
            if (_menuState == RestMenuState.Narrating)
            {
                _narrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);
            }
        }
    }
}
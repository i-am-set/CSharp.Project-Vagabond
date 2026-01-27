using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;

using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Systems;
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


namespace ProjectVagabond.UI
{
    public class SplitMapRestOverlay
    {
        public bool IsOpen { get; private set; } = false;

        public bool IsNarrating => IsOpen && _menuState == RestMenuState.Narrating;

        public event Action? OnRestCompleted;
        public event Action? OnLeaveRequested;

        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly Core _core;
        private readonly GameState _gameState;
        private readonly HapticsManager _hapticsManager;

        private Button _confirmButton;
        private Button _skipButton;
        private ConfirmationDialog _confirmationDialog;

        private readonly StoryNarrator _narrator;

        private enum RestMenuState { Selection, Narrating }
        private RestMenuState _menuState = RestMenuState.Selection;

        private const float WORLD_Y_OFFSET = 600f;
        private const int BUTTON_HEIGHT = 15;

        private readonly Rectangle[] _partyMemberPanelAreas = new Rectangle[4];
        private const int PANEL_WIDTH = 76;
        private const int PANEL_HEIGHT = 132;

        private enum RestAction { Rest, Train, Search, Guard }
        private readonly Dictionary<int, RestAction> _selectedActions = new Dictionary<int, RestAction>();
        private readonly List<Button> _actionButtons = new List<Button>();

        private const float HEAL_PERCENT_REST = 0.75f;
        private const float GUARD_HEAL_MULTIPLIER = 2.0f;

        private const int SEARCH_CHANCE_UNGUARDED = 50;
        private const int SEARCH_CHANCE_GUARDED = 90;

        private const int TRAIN_AMOUNT_UNGUARDED = 1;
        private const int TRAIN_AMOUNT_GUARDED_MAJOR = 1;
        private const int TRAIN_AMOUNT_GUARDED_MINOR = 1;

        private readonly Color COLOR_DESC_REST_NORMAL;
        private readonly Color COLOR_DESC_REST_GUARDED;
        private readonly Color COLOR_DESC_TRAIN_NORMAL;
        private readonly Color COLOR_DESC_TRAIN_GUARDED;
        private readonly Color COLOR_DESC_SEARCH_NORMAL;
        private readonly Color COLOR_DESC_SEARCH_GUARDED;
        private readonly Color COLOR_DESC_GUARD;

        private Color OVERLAY_COLOR_A = Color.Yellow;
        private Color OVERLAY_COLOR_B = Color.White;
        private const float OVERLAY_PULSE_SPEED = 8.0f;
        private const float HEAL_ANIMATION_SPEED = 5.0f;

        private const float TEXT_PULSE_SPEED = 4.0f;
        private const float TEXT_OPACITY_MIN = 0.75f;
        private const float TEXT_OPACITY_MAX = 1.0f;

        private const float SLEEP_PARTICLE_SPEED = 9f;
        private const float SLEEP_PARTICLE_LIFETIME = 2.0f;
        private const float SLEEP_PARTICLE_SPAWN_INTERVAL_BASE = 1.0f;
        private const float SLEEP_PARTICLE_SPAWN_INTERVAL_VARIANCE = 0.2f;
        private const float SLEEP_PARTICLE_SWAY_AMOUNT = 5f;
        private const float SLEEP_PARTICLE_SWAY_SPEED = 3f;
        private const float SLEEP_PARTICLE_WIND_SPEED = -5f;
        private const float SLEEP_PARTICLE_FADE_START_PERCENT = 0.7f;

        private const float SLEEP_PARTICLE_OFFSET_X = -12f;
        private const float SLEEP_PARTICLE_OFFSET_Y = -8f;

        private readonly Color SLEEP_PARTICLE_COLOR;
        private readonly Color SLEEP_PARTICLE_OUTLINE_COLOR;

        private static readonly Random _rng = new Random();

        private Dictionary<int, float> _visualHP = new Dictionary<int, float>();
        private Dictionary<int, float> _targetHP = new Dictionary<int, float>();
        private bool _isAnimatingHeal = false;
        private float _overlayPulseTimer = 0f;
        private float _textPulseTimer = 0f;

        private readonly SpriteHopAnimationController[] _hopControllers = new SpriteHopAnimationController[4];
        private readonly float[] _portraitAnimTimers = new float[4];

        private class SleepParticle
        {
            public Vector2 Position;
            public float Timer;
            public float MaxTime;
            public float SwayPhase;
            public float Speed;
            public int MemberIndex;
        }
        private readonly List<SleepParticle> _sleepParticles = new List<SleepParticle>();
        private readonly float[] _sleepSpawnTimers = new float[4];

        private class RestSequenceStep
        {
            public int MemberIndex;
            public string Message;
            public Action Effect;
        }
        private readonly Queue<RestSequenceStep> _sequenceQueue = new Queue<RestSequenceStep>();
        private int _currentSpotlightIndex = -1;

        public SplitMapRestOverlay(GameScene parentScene)
        {
            _core = ServiceLocator.Get<Core>();
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();

            for (int i = 0; i < 4; i++)
            {
                _hopControllers[i] = new SpriteHopAnimationController();
            }

            COLOR_DESC_REST_NORMAL = _global.Palette_Leaf;
            COLOR_DESC_REST_GUARDED = Color.Lime;
            COLOR_DESC_TRAIN_NORMAL = _global.Palette_Shadow;
            COLOR_DESC_TRAIN_GUARDED = Color.Magenta;
            COLOR_DESC_SEARCH_NORMAL = _global.Palette_Sky;
            COLOR_DESC_SEARCH_GUARDED = Color.Aqua;
            COLOR_DESC_GUARD = _global.Palette_Shadow;

            SLEEP_PARTICLE_COLOR = _global.Palette_Sun;
            SLEEP_PARTICLE_OUTLINE_COLOR = _global.Palette_Black;

            _confirmationDialog = new ConfirmationDialog(parentScene);

            var narratorBounds = new Rectangle(0, Global.VIRTUAL_HEIGHT - 50, Global.VIRTUAL_WIDTH, 50);
            _narrator = new StoryNarrator(narratorBounds);
            _narrator.OnFinished += AdvanceSequence;

            _confirmButton = new Button(Rectangle.Empty, "CONFIRM", font: _core.SecondaryFont)
            {
                CustomDefaultTextColor = _global.Palette_Sun,
                CustomHoverTextColor = _global.Palette_Rust,
                UseScreenCoordinates = true
            };
            _confirmButton.OnClick += ExecuteRest;

            _skipButton = new Button(Rectangle.Empty, "SKIP", font: _core.TertiaryFont)
            {
                CustomDefaultTextColor = _global.Palette_DarkShadow,
                CustomHoverTextColor = _global.ButtonHoverColor,
                UseScreenCoordinates = true
            };
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
            for (int i = 0; i < _portraitAnimTimers.Length; i++) _portraitAnimTimers[i] = 0f;

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
            _menuState = RestMenuState.Selection;
            _confirmationDialog.Hide();
            _narrator.Clear();
        }

        private void InitializeActions()
        {
            _selectedActions.Clear();
            int partyCount = _gameState.PlayerState.Party.Count;
            for (int i = 0; i < partyCount; i++)
            {
                _selectedActions[i] = RestAction.Rest;
            }
        }

        private void RebuildLayout()
        {
            int centerX = Global.VIRTUAL_WIDTH / 2;
            int screenBottom = (int)WORLD_Y_OFFSET + Global.VIRTUAL_HEIGHT;

            int margin = 3;
            int buttonY = screenBottom - BUTTON_HEIGHT - margin;

            var font = _core.SecondaryFont;
            var confirmSize = font.MeasureString("CONFIRM");
            int confirmWidth = (int)confirmSize.Width + 16;
            _confirmButton.Bounds = new Rectangle((Global.VIRTUAL_WIDTH - confirmWidth) / 2, buttonY, confirmWidth, BUTTON_HEIGHT);

            var skipFont = _core.TertiaryFont;
            var skipSize = skipFont.MeasureString("SKIP");
            int skipWidth = (int)skipSize.Width + 16;
            _skipButton.Bounds = new Rectangle(Global.VIRTUAL_WIDTH - skipWidth - 10, buttonY, skipWidth, BUTTON_HEIGHT);

            int totalPanelWidth = (4 * PANEL_WIDTH);
            int startX = (Global.VIRTUAL_WIDTH - totalPanelWidth) / 2;

            _actionButtons.Clear();

            for (int i = 0; i < 4; i++)
            {
                _partyMemberPanelAreas[i] = new Rectangle(
                    startX + (i * PANEL_WIDTH),
                    (int)WORLD_Y_OFFSET + 40,
                    PANEL_WIDTH,
                    PANEL_HEIGHT
                );

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
            int startY = panelRect.Bottom - (4 * (buttonHeight + spacing)) - 5 - 9;
            int centerX = panelRect.Center.X;

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
                    AlignLeft = true,
                    TextRenderOffset = new Vector2(8, -1),
                    DisableInputWhenSelected = true
                };

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
                foreach (var key in _selectedActions.Keys.ToList())
                {
                    if (_selectedActions[key] == RestAction.Guard)
                    {
                        _selectedActions[key] = RestAction.Rest;
                    }
                }
            }

            _selectedActions[memberIndex] = action;

            if (memberIndex >= 0 && memberIndex < _hopControllers.Length)
            {
                _hopControllers[memberIndex].Trigger();
            }

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
            _menuState = RestMenuState.Narrating;
            _sequenceQueue.Clear();
            _currentSpotlightIndex = -1;

            bool guardActive = _selectedActions.Values.Any(a => a == RestAction.Guard);

            string guardName = "";
            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                if (_selectedActions[i] == RestAction.Guard)
                {
                    guardName = _gameState.PlayerState.Party[i].Name.ToUpper();
                    break;
                }
            }

            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                if (_selectedActions[i] == RestAction.Guard)
                {
                    var member = _gameState.PlayerState.Party[i];
                    int idx = i;
                    _sequenceQueue.Enqueue(new RestSequenceStep
                    {
                        MemberIndex = idx,
                        Message = $"{member.Name} stood guard while the party rested.\n[cmodifier]+MODIFIER[/]",
                        Effect = () => { _targetHP[idx] = member.CurrentHP; }
                    });
                }
            }

            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                if (_selectedActions[i] == RestAction.Guard) continue;

                var member = _gameState.PlayerState.Party[i];
                var action = _selectedActions[i];
                int idx = i;

                _visualHP[i] = member.CurrentHP;

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
                                    _isAnimatingHeal = true;
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
                                int goldAmount = 50;
                                msg += $"Found [palette_darksun]{goldAmount} Gold[/]!";

                                effectAction = () =>
                                {
                                    _gameState.PlayerState.Coin += goldAmount;
                                    _targetHP[idx] = member.CurrentHP;
                                };
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

            _sequenceQueue.Enqueue(new RestSequenceStep
            {
                MemberIndex = -1,
                Message = "[wave]REST COMPLETE![/]",
                Effect = () => { _currentSpotlightIndex = -1; }
            });

            ProcessNextSequenceStep();
        }

        private void ProcessNextSequenceStep()
        {
            if (_sequenceQueue.Count > 0)
            {
                var step = _sequenceQueue.Dequeue();
                _currentSpotlightIndex = step.MemberIndex;
                step.Effect?.Invoke();
                _narrator.Show(step.Message);
            }
            else
            {
                OnRestCompleted?.Invoke();
            }
        }

        private void AdvanceSequence()
        {
            if (_sequenceQueue.Count > 0)
            {
                ProcessNextSequenceStep();
            }
            else
            {
                OnRestCompleted?.Invoke();
            }
        }

        private string GetStatTag(string statName)
        {
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
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var controller in _hopControllers)
            {
                controller.Update(gameTime);
            }

            for (int i = 0; i < 4; i++)
            {
                _portraitAnimTimers[i] += dt;
            }

            _overlayPulseTimer += dt * OVERLAY_PULSE_SPEED;
            _textPulseTimer += dt * TEXT_PULSE_SPEED;

            if (_isAnimatingHeal)
            {
                bool allDone = true;
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
                for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
                {
                    _visualHP[i] = _gameState.PlayerState.Party[i].CurrentHP;
                }
            }

            UpdateParticles(dt);

            if (_menuState == RestMenuState.Narrating)
            {
                _narrator.Update(gameTime);
                return;
            }

            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));
            var worldMouseState = new MouseState((int)mouseInWorldSpace.X, (int)mouseInWorldSpace.Y, mouseState.ScrollWheelValue, mouseState.LeftButton, mouseState.MiddleButton, mouseState.RightButton, mouseState.XButton1, mouseState.XButton2);

            for (int i = 0; i < _actionButtons.Count; i++)
            {
                var btn = _actionButtons[i];
                btn.Update(worldMouseState);
            }

            int btnIndex = 0;
            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Rest;
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Train;
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Search;
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Guard;
            }

            _confirmButton.Update(worldMouseState);
            _skipButton.Update(worldMouseState);
        }

        private void UpdateParticles(float dt)
        {
            for (int i = _sleepParticles.Count - 1; i >= 0; i--)
            {
                var p = _sleepParticles[i];

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

                p.Position.Y -= p.Speed * dt;
                float swayOffset = MathF.Sin(p.Timer * SLEEP_PARTICLE_SWAY_SPEED + p.SwayPhase) * SLEEP_PARTICLE_SWAY_AMOUNT;
                float windOffset = SLEEP_PARTICLE_WIND_SPEED;
                p.Position.X += (swayOffset + windOffset) * dt;
            }

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
                    _sleepSpawnTimers[i] = 0;
                }
            }
        }

        private void SpawnSleepParticle(int memberIndex)
        {
            var panelRect = _partyMemberPanelAreas[memberIndex];
            var defaultFont = ServiceLocator.Get<BitmapFont>();
            float nameHeight = defaultFont.LineHeight;

            float portraitY = panelRect.Y + 4 + nameHeight - 2;
            float centerX = panelRect.Center.X;
            Vector2 portraitCenter = new Vector2(centerX, portraitY + 16);
            Vector2 spawnPos = portraitCenter + new Vector2(SLEEP_PARTICLE_OFFSET_X, SLEEP_PARTICLE_OFFSET_Y);

            _sleepParticles.Add(new SleepParticle
            {
                Position = spawnPos,
                Timer = 0f,
                MaxTime = SLEEP_PARTICLE_LIFETIME,
                SwayPhase = (float)(_rng.NextDouble() * Math.PI * 2),
                Speed = SLEEP_PARTICLE_SPEED,
                MemberIndex = memberIndex
            });
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsOpen) return;

            var pixelTex = ServiceLocator.Get<Texture2D>();
            var secondaryFont = _core.SecondaryFont;
            var defaultFont = ServiceLocator.Get<BitmapFont>();
            var tertiaryFont = _core.TertiaryFont;

            var bgRectDraw = new Rectangle(0, (int)WORLD_Y_OFFSET, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            spriteBatch.DrawSnapped(pixelTex, bgRectDraw, _global.GameBg);

            if (_spriteManager.RestBorderMain != null)
            {
                spriteBatch.DrawSnapped(_spriteManager.RestBorderMain, new Vector2(0, WORLD_Y_OFFSET), Color.White);
            }

            string title = "REST";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, WORLD_Y_OFFSET + 10);
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_Sun);

            bool guardActive = _selectedActions.Values.Any(a => a == RestAction.Guard);

            for (int i = 0; i < 4; i++)
            {
                var bounds = _partyMemberPanelAreas[i];
                bool isOccupied = i < _gameState.PlayerState.Party.Count;
                var member = isOccupied ? _gameState.PlayerState.Party[i] : null;

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

                string name = isOccupied ? member!.Name.ToUpper() : "EMPTY";
                Color nameColor = isOccupied ? _global.Palette_Sun : _global.Palette_DarkShadow;

                var nameSize = defaultFont.MeasureString(name);
                Vector2 namePos = new Vector2(centerX - nameSize.Width / 2, currentY);

                currentY += (int)nameSize.Height - 2;

                if (isOccupied && _spriteManager.PlayerMasterSpriteSheet != null)
                {
                    int portraitIndex = member!.PortraitIndex;
                    PlayerSpriteType type;

                    if (_selectedActions.TryGetValue(i, out var action) && action == RestAction.Rest)
                    {
                        type = PlayerSpriteType.Sleep;
                    }
                    else
                    {
                        float animSpeed = 1f;
                        int frame = (int)(_portraitAnimTimers[i] * animSpeed) % 2;
                        type = frame == 0 ? PlayerSpriteType.Normal : PlayerSpriteType.Alt;
                    }

                    var sourceRect = _spriteManager.GetPlayerSourceRect(portraitIndex, type);
                    float hopOffset = _hopControllers[i].GetOffset(true);

                    float bobSpeed = 2.5f;
                    float bobAmp = 0.5f;
                    float phase = i * 0.7f;
                    float bob = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * bobSpeed + phase) * bobAmp;
                    hopOffset += bob;

                    Vector2 portraitPos = new Vector2(centerX - 16, currentY + hopOffset);
                    spriteBatch.DrawSnapped(_spriteManager.PlayerMasterSpriteSheet, portraitPos, sourceRect, Color.White);
                }

                spriteBatch.DrawStringSnapped(defaultFont, name, namePos, nameColor);

                currentY += 32 + 2 - 6;

                if (_spriteManager.InventoryPlayerHealthBarEmpty != null)
                {
                    int barX = centerX - (_spriteManager.InventoryPlayerHealthBarEmpty.Width / 2);
                    spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarEmpty, new Vector2(barX, currentY), Color.White);

                    if (isOccupied && _spriteManager.InventoryPlayerHealthBarFull != null)
                    {
                        float currentVisualHP = _visualHP.ContainsKey(i) ? _visualHP[i] : member!.CurrentHP;
                        float hpPercent = currentVisualHP / Math.Max(1, member!.MaxHP);
                        int visibleWidth = (int)(_spriteManager.InventoryPlayerHealthBarFull.Width * hpPercent);
                        var srcRect = new Rectangle(0, 0, visibleWidth, _spriteManager.InventoryPlayerHealthBarFull.Height);
                        spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, currentY), srcRect, Color.White);

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

                    if (isOccupied && _selectedActions.TryGetValue(i, out var descAction))
                    {
                        string descText = "";
                        Color descColor = _global.Palette_Sun;
                        float multiplier = (guardActive && descAction != RestAction.Guard) ? GUARD_HEAL_MULTIPLIER : 1.0f;

                        switch (descAction)
                        {
                            case RestAction.Rest:
                                int finalPercent = (int)(HEAL_PERCENT_REST * multiplier * 100);
                                descText = $"+{finalPercent}% HP";
                                descColor = guardActive ? COLOR_DESC_REST_GUARDED : COLOR_DESC_REST_NORMAL;
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

                        var lines = descText.Split('\n');
                        float buttonsTopY = bounds.Bottom - (4 * 11) - 14;
                        float textTopBoundary = hpTextY + secondaryFont.LineHeight + 2;
                        float availableHeight = buttonsTopY - textTopBoundary;
                        float totalTextHeight = lines.Length * (secondaryFont.LineHeight + 1) - 1;
                        float startDescY = textTopBoundary + (availableHeight - totalTextHeight) / 2f;
                        if (startDescY < textTopBoundary) startDescY = textTopBoundary;
                        float descY = startDescY;

                        float pulseOpacity = MathHelper.Lerp(TEXT_OPACITY_MIN, TEXT_OPACITY_MAX, (MathF.Sin(_textPulseTimer * TEXT_PULSE_SPEED) + 1f) / 2f);
                        Color finalDescColor = descColor * pulseOpacity;

                        foreach (var line in lines)
                        {
                            var lineSize = secondaryFont.MeasureString(line);
                            float lineX = centerX - (lineSize.Width / 2f);
                            float finalY = MathF.Round(descY);
                            spriteBatch.DrawStringSnapped(secondaryFont, line, new Vector2(lineX, finalY), finalDescColor);
                            descY += secondaryFont.LineHeight + 1;
                        }
                    }

                    currentY += 8 + (int)valSize.Height + 4 - 3;
                }

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
                        spriteBatch.DrawStringSquareOutlinedSnapped(secondaryFont, "Z", p.Position, SLEEP_PARTICLE_COLOR * alpha, SLEEP_PARTICLE_OUTLINE_COLOR * alpha);
                    }
                }

                if (isDimmed)
                {
                    spriteBatch.DrawSnapped(pixelTex, bounds, Color.Black * 0.7f);
                }
            }

            for (int i = 0; i < _actionButtons.Count; i++)
            {
                var btn = (ToggleButton)_actionButtons[i];
                btn.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);

                float bobOffset = 0f;
                if (btn.IsSelected)
                {
                    float speed = 5f;
                    float val = MathF.Round((MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * speed) + 1f) * 0.5f);
                    bobOffset = -val;
                }

                int actionIndex = i % 4;
                int stateIndex = 0;
                if (btn.IsSelected) stateIndex = 2;
                else if (btn.IsHovered) stateIndex = 1;

                var iconRect = _spriteManager.GetRestActionIconRect(actionIndex, stateIndex);
                Vector2 iconPos = new Vector2(btn.Bounds.X + 1, btn.Bounds.Y + 1 + bobOffset);
                spriteBatch.DrawSnapped(_spriteManager.RestActionIconsSpriteSheet, iconPos, iconRect, Color.White);
            }

            _confirmButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            _skipButton.Draw(spriteBatch, tertiaryFont, gameTime, Matrix.Identity);

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

            if (_menuState == RestMenuState.Narrating)
            {
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
                _confirmationDialog.DrawContent(spriteBatch, font, gameTime, Matrix.Identity);
            }

            if (_menuState == RestMenuState.Narrating)
            {
                _narrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);
            }
        }
    }
}

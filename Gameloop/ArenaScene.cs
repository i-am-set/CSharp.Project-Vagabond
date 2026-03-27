#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Animations;
using ProjectVagabond.Battle;
using ProjectVagabond.Particles;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class ArenaScene : GameScene
    {
        private enum ArenaState
        {
            Betting,
            Fighting,
            MatchOver,
            Paused
        }

        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly GameState _gameState;
        private readonly InputManager _inputManager;
        private readonly SceneManager _sceneManager;
        private readonly TransitionManager _transitionManager;
        private readonly Texture2D _pixel;
        private readonly Core _core;
        private readonly ParticleSystemManager _particleSystemManager;
        private readonly TextureFactory _textureFactory;
        private readonly CursorManager _cursorManager;

        private readonly List<ArenaWizard> _wizards = new List<ArenaWizard>();
        private readonly List<ArenaWizard> _wizardsFixedOrder = new List<ArenaWizard>();
        private readonly List<ActiveAttack> _activeAttacks = new List<ActiveAttack>();
        private readonly List<ArenaWizard> _queryResults = new List<ArenaWizard>();
        private readonly Random _random = new Random();

        private readonly TicketManager _ticketManager = new TicketManager();
        private readonly WizardRenderer _wizardRenderer = new WizardRenderer();

        private ArenaState _arenaState;
        private float _phaseTimer = 0f;
        private int _lastCountdownSecond = 0;
        private bool _hasPlayedBetSound = false;

        private float _hitstopTimer = 0f;

        private PlinkAnimator _plinkBetCountdown = null!;
        private PlinkAnimator _plinkFight = null!;

        private MouseState _lastMouseState;

        private Rectangle _arenaBounds;
        public Rectangle ArenaBounds => _arenaBounds;
        private Vector2 _arenaCenter;

        private float _matchOverTimer = 0f;
        private string _matchResultText = "";
        private Color _matchResultColor = Color.White;

        private ArenaWizard? _hoveredHudWizard;
        private ArenaWizard? _previousHoveredHudWizard;

        private const int MULT_DEFAULT_MIN_STEP = 9; // Index 9 is Palette_Rust
        private const float ARENA_FILL_OPACITY = 1f; // Tunable window effect

        private readonly Dictionary<ArenaWizard, int> _probSteps = new Dictionary<ArenaWizard, int>();
        private readonly Dictionary<ArenaWizard, PlinkAnimator> _probPlinks = new Dictionary<ArenaWizard, PlinkAnimator>();
        private readonly Dictionary<ArenaWizard, float> _winProbabilities = new Dictionary<ArenaWizard, float>();

        private BattleContext _battleContext;

        public IReadOnlyList<ArenaWizard> Wizards => _wizards;
        public IReadOnlyList<ActiveAttack> ActiveAttacks => _activeAttacks;

        public bool IsOvertime { get; private set; }
        private float _matchTimer;
        private int _lastMatchSecond;
        private PlinkAnimator _plinkTimerText = null!;
        private PlinkAnimator _plinkSuddenDeath = null!;
        private float _suddenDeathTimer;

        private readonly HashSet<ArenaWizard> _printedTickets = new HashSet<ArenaWizard>();

        // --- Pause Menu State ---
        private readonly List<Button> _pauseButtons = new List<Button>();
        private readonly NavigationGroup _pauseNavGroup = new NavigationGroup(wrapNavigation: true);
        private ArenaState _prePauseState;
        private readonly ConfirmationDialog _confirmationDialog;

        // --- Betting State ---
        private Button _skipButton = null!;
        private readonly NavigationGroup _bettingNavGroup = new NavigationGroup(wrapNavigation: true);

        public ArenaScene()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _pixel = ServiceLocator.Get<Texture2D>();
            _core = ServiceLocator.Get<Core>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _textureFactory = ServiceLocator.Get<TextureFactory>();
            _cursorManager = ServiceLocator.Get<CursorManager>();
            _confirmationDialog = new ConfirmationDialog(this);
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public Vector2 GetRandomArenaPoint()
        {
            float margin = 12f;
            Vector2 pt;
            Vector2 clamped;
            do
            {
                float x = _arenaBounds.Left + margin + (float)_random.NextDouble() * (_arenaBounds.Width - margin * 2);
                float y = _arenaBounds.Top + margin + (float)_random.NextDouble() * (_arenaBounds.Height - margin * 2);
                pt = new Vector2(x, y);
                clamped = ClampToArena(pt, margin);
            } while (pt != clamped);

            return pt;
        }

        public List<ArenaWizard> GetWizardsInCircle(Vector2 center, float radius)
        {
            _queryResults.Clear();
            foreach (var w in _wizards)
            {
                if (w.Data.Combat.State != WizardState.Dead && CollisionMath.RectangleIntersectsCircle(w.Controller.GetHitbox(_spriteManager), center, radius))
                {
                    _queryResults.Add(w);
                }
            }
            return _queryResults;
        }

        public List<ArenaWizard> GetWizardsInOBB(Vector2 origin, Vector2 direction, float width, float length)
        {
            _queryResults.Clear();
            foreach (var w in _wizards)
            {
                if (w.Data.Combat.State != WizardState.Dead && CollisionMath.AABBIntersectsOBB(w.Controller.GetHitbox(_spriteManager), origin, direction, width, length))
                {
                    _queryResults.Add(w);
                }
            }
            return _queryResults;
        }

        public void DebugPrintTicket()
        {
            _ticketManager.DebugPrintTicket();
        }

        public void SpawnAttack(ActiveAttack attack)
        {
            attack.DeliveryInstance.Start(attack);
            _activeAttacks.Add(attack);
        }

        private void InitializePauseMenu()
        {
            _pauseButtons.Clear();
            _pauseNavGroup.Clear();

            var font = _core.SecondaryFont;
            int startY = Global.VIRTUAL_HEIGHT / 2 - 25;
            int spacing = 14;

            Button CreatePauseButton(string text, int y)
            {
                Vector2 size = font.MeasureString(text);
                int w = (int)size.X + 8;
                int h = (int)size.Y + 4;

                // Shift bounds down by 1, text up by 1 to effectively move the border down relative to the text.
                var btn = new Button(new Rectangle(Global.VIRTUAL_WIDTH / 2 - w / 2, y + 1, w, h), text, font: font)
                {
                    DrawBorderOnHover = true,
                    HoverAnimation = HoverAnimationType.Hop,
                    TextRenderOffset = new Vector2(0, -1)
                };
                return btn;
            }

            var resumeBtn = CreatePauseButton("RESUME", startY);
            resumeBtn.OnClick += () => {
                _arenaState = _prePauseState;
                _particleSystemManager.IsPaused = false;
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().ResumeGameAudio();
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;atk=0.02;sus=0.05;dec=0.2;freq=880;slide=-440;vol=0.2");
            };

            var settingsBtn = CreatePauseButton("SETTINGS", startY + spacing);
            settingsBtn.OnClick += () => { _sceneManager.ShowModal(GameSceneState.Settings); };

            var mainMenuBtn = CreatePauseButton("MAIN MENU", startY + spacing * 2);
            mainMenuBtn.OnClick += () => {
                _confirmationDialog.Show("Return to Main Menu?\n\n[cemphasis]Current match progress will be lost.[/]", new List<Tuple<string, Action>> {
                    Tuple.Create("YES", new Action(() => {
                        _particleSystemManager.IsPaused = false;
                        _particleSystemManager.ClearAllEmitters();
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().ResumeGameAudio();
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().StopAll();
                        PoolManager.ClearAll();
                        _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff);
                        _confirmationDialog.Hide();
                    })),
                    Tuple.Create("[chighlight]NO", new Action(() => {
                        _confirmationDialog.Hide();
                    }))
                });
            };

            var desktopBtn = CreatePauseButton("EXIT TO DESKTOP", startY + spacing * 3);
            desktopBtn.OnClick += () => {
                _confirmationDialog.Show("Exit to Desktop?\n\n[cemphasis]Current match progress will be lost.[/]", new List<Tuple<string, Action>> {
                    Tuple.Create("YES", new Action(() => {
                        _core.ExitApplication();
                    })),
                    Tuple.Create("[chighlight]NO", new Action(() => {
                        _confirmationDialog.Hide();
                    }))
                });
            };

            _pauseButtons.Add(resumeBtn);
            _pauseButtons.Add(settingsBtn);
            _pauseButtons.Add(mainMenuBtn);
            _pauseButtons.Add(desktopBtn);

            foreach (var b in _pauseButtons) _pauseNavGroup.Add(b);
        }

        public override void Enter()
        {
            base.Enter();
            _wizards.Clear();
            _wizardsFixedOrder.Clear();
            _activeAttacks.Clear();
            _ticketManager.Clear();
            _probSteps.Clear();
            _probPlinks.Clear();
            _winProbabilities.Clear();
            _printedTickets.Clear();
            _previousHoveredHudWizard = null;

            InitializePauseMenu();

            ServiceLocator.Get<GeometricBackgroundManager>().Show(0.5f);

            _battleContext = new BattleContext
            {
                Arena = this,
                Global = _global,
                SpriteManager = _spriteManager,
                Core = _core,
                ParticleSystemManager = _particleSystemManager,
                TextureFactory = _textureFactory,
                Pixel = _pixel
            };

            _arenaState = ArenaState.Betting;
            _phaseTimer = 15.0f;
            _lastCountdownSecond = 15;
            _hasPlayedBetSound = false;

            var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();
            audio.PlayMusic("music_battle", 1.0f);
            audio.SetCurrentMusicStemVolume(0, 0.0f, instant: true); // Normal muted instantly
            audio.SetCurrentMusicStemVolume(1, 1.0f, instant: true); // Muffled active instantly

            IsOvertime = false;
            _matchTimer = 120f;
            _lastMatchSecond = 120;
            _plinkTimerText = new PlinkAnimator();
            _plinkSuddenDeath = new PlinkAnimator();
            _suddenDeathTimer = 0f;

            _plinkBetCountdown = new PlinkAnimator();
            _plinkFight = new PlinkAnimator();

            _plinkBetCountdown.Start(0f, 0.3f);

            _matchOverTimer = 0f;
            _matchResultText = "";

            _arenaBounds = new Rectangle(8, 45, Global.VIRTUAL_WIDTH - 16, Global.VIRTUAL_HEIGHT - 48);
            _arenaCenter = new Vector2(_arenaBounds.Center.X, _arenaBounds.Center.Y);
            _ticketManager.DispenseTargetX = Global.VIRTUAL_WIDTH / 2f;

            // Initialize Skip Button
            var tertFont = _core.TertiaryFont;
            Vector2 skipSize = tertFont.MeasureString("SKIP");
            int skipW = (int)skipSize.X + 8;
            int skipH = (int)skipSize.Y + 4;
            int skipY = Global.VIRTUAL_HEIGHT - 10 - skipH; // Exactly 10px above bottom

            _skipButton = new Button(new Rectangle((int)(_arenaCenter.X - skipW / 2f), skipY, skipW, skipH), "SKIP", font: tertFont)
            {
                DrawBorderOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                CustomDefaultTextColor = _global.Palette_DarkestPale // Stealthy until hovered
            };
            _skipButton.OnClick += () => {
                if (_arenaState == ArenaState.Betting)
                {
                    if (_phaseTimer > 3.0f)
                    {
                        _phaseTimer = 3.0f; // Skip to the 3-second countdown
                        ServiceLocator.Get<HapticsManager>().TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                    }
                }
            };
            _bettingNavGroup.Clear();
            _bettingNavGroup.Add(_skipButton);

            var selectedIds = _gameState.SelectedRoster;
            int count = selectedIds.Count;

            float spawnRadius = Math.Min(_arenaBounds.Width, _arenaBounds.Height) / 3f;

            for (int i = 0; i < count; i++)
            {
                string id = selectedIds[i];
                if (!GameDataCache.WizardCats.TryGetValue(id, out var data)) continue;
                if (!_gameState.RunWizards.TryGetValue(id, out var rolledStats)) continue;

                float angle = (i / (float)count) * MathHelper.TwoPi;
                Vector2 spawnPos = _arenaCenter + new Vector2(MathF.Cos(angle) * spawnRadius, MathF.Sin(angle) * spawnRadius);

                var wizard = new ArenaWizard();
                bool isPlayer = (id == _gameState.PlayerControlledId);
                wizard.Controller.Initialize(data, rolledStats, spawnPos, isPlayer);
                _wizards.Add(wizard);
            }

            _wizardsFixedOrder.AddRange(_wizards);

            int totalRating = _wizards.Sum(w => w.Data.Stats.Rating);
            foreach (var w in _wizards)
            {
                if (totalRating > 0 && w.Data.Stats.Rating > 0)
                {
                    _winProbabilities[w] = (float)w.Data.Stats.Rating / totalRating;
                }
                else
                {
                    _winProbabilities[w] = 0f;
                }

                _probSteps[w] = GetProbabilityStep(_winProbabilities[w]);
                _probPlinks[w] = new PlinkAnimator { MaxScale = 1.5f, PlinkTriggerThreshold = 0f };
            }

            CalculateHUDLayout();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);
            float dt = (float)effectiveGameTime.ElapsedGameTime.TotalSeconds;

            if (_transitionManager.IsTransitioning) return;

            if (_arenaState == ArenaState.Paused)
            {
                if (_sceneManager.IsModalActive) return; // Don't update pause menu if settings is open

                if (_confirmationDialog.IsActive)
                {
                    _confirmationDialog.Update(gameTime);
                    return;
                }

                var pMouseState = _inputManager.GetEffectiveMouseState();
                foreach (var btn in _pauseButtons) btn.Update(pMouseState);

                if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
                    _pauseNavGroup.DeselectAll();
                else
                    _pauseNavGroup.UpdateInput(_inputManager);

                if (_inputManager.Back)
                {
                    _arenaState = _prePauseState;
                    _particleSystemManager.IsPaused = false;
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().ResumeGameAudio();
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;atk=0.02;sus=0.05;dec=0.2;freq=880;slide=-440;vol=0.2");
                }
                return;
            }

            _plinkBetCountdown?.Update(effectiveGameTime, _arenaCenter);
            _plinkFight?.Update(effectiveGameTime, _arenaCenter);
            _plinkTimerText?.Update(effectiveGameTime, new Vector2(_arenaCenter.X, 12));
            _plinkSuddenDeath?.Update(effectiveGameTime, new Vector2(_arenaCenter.X, Global.VIRTUAL_HEIGHT / 2f - 20));

            foreach (var w in _wizards)
            {
                if (_probPlinks.TryGetValue(w, out var plink) && plink.IsActive)
                {
                    plink.Update(effectiveGameTime, Vector2.Zero);
                }
            }

            var mouseState = _inputManager.GetEffectiveMouseState();
            Vector2 virtualMousePos = Core.TransformMouse(mouseState.Position);
            bool isClicking = mouseState.LeftButton == ButtonState.Pressed;
            bool justClicked = isClicking && _lastMouseState.LeftButton == ButtonState.Released;

            int aliveCount = _wizards.Count(w => w.Data.Combat.State != WizardState.Dead);

            _hoveredHudWizard = null;
            bool canHover = _arenaState == ArenaState.Betting || (_arenaState == ArenaState.Fighting && aliveCount > 1);

            if (canHover)
            {
                float colWidth = Global.VIRTUAL_WIDTH / (float)_wizardsFixedOrder.Count;
                for (int i = 0; i < _wizardsFixedOrder.Count; i++)
                {
                    var w = _wizardsFixedOrder[i];
                    if (w.Data.Combat.State == WizardState.Dead) continue;

                    Rectangle hudRect = new Rectangle((int)(colWidth * i), 0, (int)colWidth, 40);

                    if (hudRect.Contains(virtualMousePos))
                    {
                        _hoveredHudWizard = w;
                    }
                }
            }

            if (_hoveredHudWizard != null && _hoveredHudWizard != _previousHoveredHudWizard)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_hover");
            }
            _previousHoveredHudWizard = _hoveredHudWizard;

            _ticketManager.Update(dt, virtualMousePos, justClicked, isClicking, _inputManager);

            if (_ticketManager.IsHoveringTicket || _ticketManager.IsDraggingTicket)
            {
                _cursorManager.SetState(_ticketManager.IsDraggingTicket ? CursorState.Dragging : CursorState.HoverDraggable);
            }

            if (_arenaState == ArenaState.Betting)
            {
                _phaseTimer -= dt;

                _skipButton.Update(mouseState);
                if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
                    _bettingNavGroup.DeselectAll();
                else
                    _bettingNavGroup.UpdateInput(_inputManager);

                if (!_hasPlayedBetSound && _phaseTimer <= 29.0f)
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlaySfx("sfx_voice_place_your_bets");
                    _hasPlayedBetSound = true;
                }

                int currentSecond = (int)Math.Ceiling(_phaseTimer);
                if (currentSecond != _lastCountdownSecond && currentSecond > 0)
                {
                    _lastCountdownSecond = currentSecond;
                    _plinkBetCountdown.Start(0f, 0.2f);

                    if (currentSecond <= 3)
                    {
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;atk=0.01;sus=0.05;dec=0.1;freq=600;vol=0.15|wave=2;atk=0.01;sus=0.05;dec=0.2;freq=150;vol=0.25");
                        ServiceLocator.Get<HapticsManager>().TriggerZoomPulse(1.01f, 0.1f);
                    }
                    else if (currentSecond <= 10)
                    {
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;atk=0.01;sus=0.05;dec=0.1;freq=600;vol=0.05");
                    }
                }

                if (_phaseTimer <= 0)
                {
                    _arenaState = ArenaState.Fighting;
                    _phaseTimer = 0f;
                    _plinkFight.Start(0f, 0.3f);
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlaySfx("voice_fight");
                    ServiceLocator.Get<HapticsManager>().TriggerZoomPulse(1.03f, 0.05f);

                    var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();
                    audio.SetCurrentMusicStemVolume(0, 1.0f, fadeSpeed: 0.3f); // Normal active
                    audio.SetCurrentMusicStemVolume(1, 0.0f, fadeSpeed: 0.3f); // Muffled muted
                }
            }

            if (_arenaState == ArenaState.Fighting || _arenaState == ArenaState.MatchOver)
            {
                if (_hitstopTimer > 0)
                {
                    _hitstopTimer -= dt;
                    foreach (var wizard in _wizards)
                    {
                        wizard.Data.UI.IsHovered = wizard.Data.Combat.State != WizardState.Dead && wizard.Controller.GetHitbox(_spriteManager).Contains(virtualMousePos);
                    }
                }
                else
                {
                    _phaseTimer += dt;

                    if (_arenaState == ArenaState.Fighting)
                    {
                        if (_phaseTimer >= 1.0f && !IsOvertime)
                        {
                            _matchTimer -= dt;
                            int currentSec = (int)Math.Ceiling(_matchTimer);
                            if (currentSec != _lastMatchSecond && currentSec >= 0)
                            {
                                _lastMatchSecond = currentSec;
                                if (currentSec == 60 || currentSec == 30 || currentSec <= 10)
                                {
                                    _plinkTimerText.Start(0f, 0.3f);
                                }
                            }

                            if (_matchTimer <= 0)
                            {
                                IsOvertime = true;
                                _suddenDeathTimer = 1.0f;
                                _plinkSuddenDeath.Start(0f, 0.3f);
                                _plinkTimerText.Start(0f, 0.3f);

                                foreach (var w in _wizards)
                                {
                                    if (w.Data.Combat.State != WizardState.Dead)
                                    {
                                        w.Data.Stats.CurrentHP = 1;
                                        if (w.Data.Combat.QueuedMove != null && w.Data.Combat.QueuedMove.Effects.Any(e => e is HealEffect))
                                        {
                                            w.Data.Combat.State = WizardState.Recovering;
                                            w.Data.Combat.StateTimer = 0.25f;
                                            w.Data.Combat.QueuedMove = null;
                                        }
                                    }
                                }

                                foreach (var attack in _activeAttacks)
                                {
                                    if (attack.Move.Effects.Any(e => e is HealEffect))
                                    {
                                        attack.IsCanceled = true;
                                    }
                                }
                            }
                        }

                        if (_suddenDeathTimer > 0)
                        {
                            _suddenDeathTimer -= dt;
                        }
                    }

                    if (_inputManager.ActiveSpellTriggered && _arenaState == ArenaState.Fighting)
                    {
                        var player = _wizards.FirstOrDefault(w => w.Data.Stats.IsPlayer);
                        if (player != null) player.Controller.TriggerActiveSpell(_battleContext);
                    }

                    foreach (var wizard in _wizards)
                    {
                        wizard.Data.UI.IsHovered = wizard.Data.Combat.State != WizardState.Dead && wizard.Controller.GetHitbox(_spriteManager).Contains(virtualMousePos);
                        wizard.Controller.Update(dt, _battleContext);
                    }

                    for (int i = 0; i < _wizards.Count; i++)
                    {
                        var w1 = _wizards[i];
                        if (w1.Data.Combat.State == WizardState.Dead) continue;

                        for (int j = i + 1; j < _wizards.Count; j++)
                        {
                            var w2 = _wizards[j];
                            if (w2.Data.Combat.State == WizardState.Dead) continue;

                            var box1 = w1.Controller.GetHitbox(_spriteManager);
                            var box2 = w2.Controller.GetHitbox(_spriteManager);

                            if (box1.Intersects(box2))
                            {
                                Vector2 center1 = new Vector2(box1.Center.X, box1.Center.Y);
                                Vector2 center2 = new Vector2(box2.Center.X, box2.Center.Y);

                                Vector2 pushDir = center1 - center2;
                                if (pushDir.LengthSquared() == 0)
                                {
                                    pushDir = new Vector2((float)_random.NextDouble() - 0.5f, (float)_random.NextDouble() - 0.5f);
                                    if (pushDir.LengthSquared() == 0) pushDir = new Vector2(1, 0);
                                }
                                pushDir.Normalize();

                                if (w1.Data.Combat.State == WizardState.Moving)
                                {
                                    w1.Data.Combat.TargetPosition = ClampToArena(w1.Data.Combat.Position + pushDir * 50f, 12f);
                                }
                                if (w2.Data.Combat.State == WizardState.Moving)
                                {
                                    w2.Data.Combat.TargetPosition = ClampToArena(w2.Data.Combat.Position - pushDir * 50f, 12f);
                                }
                            }
                        }
                    }

                    for (int i = _activeAttacks.Count - 1; i >= 0; i--)
                    {
                        var attack = _activeAttacks[i];

                        if (attack.TargetWizard != null && attack.TargetWizard.Data.Combat.IsSuspended)
                        {
                            attack.TargetWizard = null;
                        }

                        attack.Update(dt, _battleContext);

                        if (attack.IsFinished)
                        {
                            _activeAttacks.RemoveAt(i);
                            attack.ReturnToPool();
                        }
                    }

                    if (_arenaState == ArenaState.Fighting)
                    {
                        UpdateDynamicOdds();

                        int currentAliveCount = _wizards.Count(w => w.Data.Combat.State != WizardState.Dead);

                        foreach (var w in _wizards)
                        {
                            if (w.Data.Combat.State == WizardState.Dead && w.Data.Metrics.Placement == 0)
                            {
                                w.Data.Metrics.Placement = currentAliveCount + 1;
                                w.Data.Metrics.TimeSurvived = _phaseTimer;

                                if (!_printedTickets.Contains(w))
                                {
                                    _printedTickets.Add(w);
                                    _ticketManager.PrintTicket(_wizards.IndexOf(w) + 1, w.Data.Metrics.Placement);
                                }
                            }
                        }

                        if (currentAliveCount <= 1)
                        {
                            _arenaState = ArenaState.MatchOver;
                            _matchOverTimer = 4.0f;

                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().StopMusic(2.0f);
                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlaySfx("sfx_win");

                            var winner = _wizards.FirstOrDefault(w => w.Data.Combat.State != WizardState.Dead);
                            if (winner != null)
                            {
                                winner.Data.Metrics.Placement = 1;
                                winner.Data.Metrics.TimeSurvived = _phaseTimer;

                                _matchResultText = $"{winner.Data.Stats.Name.ToUpper()} WINS!";

                                if (!_printedTickets.Contains(winner))
                                {
                                    _printedTickets.Add(winner);
                                    _ticketManager.PrintTicket(_wizards.IndexOf(winner) + 1, 1);
                                }
                            }
                            else
                            {
                                _matchResultText = "DRAW";
                                _matchResultColor = _global.Palette_Gray;
                            }

                            _gameState.LastMatchWizards = _wizards.ToList();
                        }
                    }
                    else if (_arenaState == ArenaState.MatchOver)
                    {
                        _matchOverTimer -= dt;
                        if (_matchOverTimer <= 0f && !_transitionManager.IsTransitioning)
                        {
                            _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff);
                        }
                    }
                }
            }

            if (_inputManager.Back)
            {
                _prePauseState = _arenaState;
                _arenaState = ArenaState.Paused;
                _pauseNavGroup.SelectFirst();
                _particleSystemManager.IsPaused = true;
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PauseGameAudio();
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;atk=0.02;sus=0.05;dec=0.2;freq=440;slide=440;vol=0.2");
            }

            _lastMouseState = mouseState;
        }

        private void CalculateHUDLayout()
        {
            var secondaryFont = _core.SecondaryFont;
            int totalWizards = _wizardsFixedOrder.Count;
            if (totalWizards == 0) return;

            float colWidth = Global.VIRTUAL_WIDTH / (float)totalWizards;

            for (int i = 0; i < totalWizards; i++)
            {
                var w = _wizardsFixedOrder[i];
                w.Data.UI.HudIsLeft = true;

                float centerX = colWidth * i + colWidth / 2f;
                w.Data.UI.HudNameSize = secondaryFont.MeasureString(w.Data.Stats.Name.ToUpper());

                w.Data.UI.HudNamePos = new Vector2(centerX - w.Data.UI.HudNameSize.X / 2f, 15);

                int maxHearts = (w.Data.Stats.MaxHP + 2) / 3;
                int heartWidth = 5;
                int heartSpacing = 1;
                int totalHeartsWidth = maxHearts * heartWidth + (maxHearts - 1) * heartSpacing;

                w.Data.UI.HudHeartStartPos = new Vector2(centerX - totalHeartsWidth / 2f, 15 + w.Data.UI.HudNameSize.Y + 2);
            }
        }

        public override void Exit()
        {
            base.Exit();
            ServiceLocator.Get<GeometricBackgroundManager>().Hide();
            PoolManager.ClearAll();
        }

        public Vector2 ClampToArena(Vector2 point, float margin = 4f)
        {
            float L = _arenaBounds.Left + margin;
            float R = _arenaBounds.Right - margin;
            float T = _arenaBounds.Top + margin;
            float B = _arenaBounds.Bottom - margin;
            float bev = 24f; // Bevel size

            // 1. Clamp to AABB
            point.X = Math.Clamp(point.X, L, R);
            point.Y = Math.Clamp(point.Y, T, B);

            // 2. Clamp to Bevels
            // Top-Left
            float dx = point.X - L;
            float dy = point.Y - T;
            if (dx + dy < bev)
            {
                float delta = bev - (dx + dy);
                point.X += delta / 2f;
                point.Y += delta / 2f;
            }

            // Top-Right
            dx = R - point.X;
            dy = point.Y - T;
            if (dx + dy < bev)
            {
                float delta = bev - (dx + dy);
                point.X -= delta / 2f;
                point.Y += delta / 2f;
            }

            // Bottom-Left
            dx = point.X - L;
            dy = B - point.Y;
            if (dx + dy < bev)
            {
                float delta = bev - (dx + dy);
                point.X += delta / 2f;
                point.Y -= delta / 2f;
            }

            // Bottom-Right
            dx = R - point.X;
            dy = B - point.Y;
            if (dx + dy < bev)
            {
                float delta = bev - (dx + dy);
                point.X -= delta / 2f;
                point.Y -= delta / 2f;
            }

            return point;
        }

        private int GetProbabilityStep(float prob)
        {
            return Math.Clamp((int)Math.Round(prob * 12.0f), 0, 12);
        }

        private void UpdateDynamicOdds()
        {
            float totalDynamicRating = 0f;

            foreach (var w in _wizards)
            {
                if (w.Data.Combat.State != WizardState.Dead && w.Data.Stats.CurrentHP > 0)
                {
                    totalDynamicRating += w.Data.Stats.Rating * ((float)w.Data.Stats.CurrentHP / w.Data.Stats.MaxHP);
                }
            }

            foreach (var w in _wizards)
            {
                if (w.Data.Combat.State == WizardState.Dead || w.Data.Stats.CurrentHP <= 0)
                {
                    _winProbabilities[w] = 0f;
                }
                else if (totalDynamicRating > 0)
                {
                    float dynamicRating = w.Data.Stats.Rating * ((float)w.Data.Stats.CurrentHP / w.Data.Stats.MaxHP);
                    _winProbabilities[w] = dynamicRating / totalDynamicRating;
                }

                int step = GetProbabilityStep(_winProbabilities[w]);

                if (_probSteps.TryGetValue(w, out int currentStep) && currentStep != step)
                {
                    if (currentStep != -1)
                    {
                        _probPlinks[w].Start(0f, 0.3f);
                    }
                    _probSteps[w] = step;
                }
            }
        }

        private Color GetMultiplierColor(int step)
        {
            switch (step)
            {
                case 0: return _global.Palette_Black;
                case 1: return _global.Palette_DarkShadow;
                case 2: return _global.Palette_DarkestPale;
                case 3: return _global.Palette_DarkPale;
                case 4: return _global.Palette_Pale;
                case 5: return _global.Palette_LightPale;
                case 6: return _global.Palette_Sun;
                case 7: return _global.Palette_DarkSun;
                case 8: return _global.Palette_Fruit;
                case 9: return _global.Palette_Rust;
                case 10: return _global.Palette_DarkRust;
                case 11: return _global.Palette_Sea;
                case 12: return _global.Palette_Sky;
                default: return _global.Palette_Sun;
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

            spriteBatch.End();

            // Draw Background Particles (Geometry)
            _particleSystemManager.Draw(spriteBatch, transform, 0);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            int L = _arenaBounds.Left - 1;
            int R = _arenaBounds.Right;
            int T = _arenaBounds.Top - 1;
            int B = _arenaBounds.Bottom;
            int bev = 24;

            // --- Arena Fill (Window Effect) ---
            Color fillColor = _global.Palette_Off * ARENA_FILL_OPACITY;

            // Central vertical strip
            spriteBatch.Draw(_pixel, new Rectangle(L + bev, T, R - L - bev * 2, B - T), fillColor);
            // Left middle strip
            spriteBatch.Draw(_pixel, new Rectangle(L, T + bev, bev, B - T - bev * 2), fillColor);
            // Right middle strip
            spriteBatch.Draw(_pixel, new Rectangle(R - bev, T + bev, bev, B - T - bev * 2), fillColor);

            // Corner triangles
            for (int y = 0; y < bev; y++)
            {
                int width = y;
                spriteBatch.Draw(_pixel, new Rectangle(L + bev - width, T + y, width, 1), fillColor); // TL
                spriteBatch.Draw(_pixel, new Rectangle(R - bev, T + y, width, 1), fillColor); // TR
                spriteBatch.Draw(_pixel, new Rectangle(L + bev - width, B - 1 - y, width, 1), fillColor); // BL
                spriteBatch.Draw(_pixel, new Rectangle(R - bev, B - 1 - y, width, 1), fillColor); // BR
            }
            // ----------------------------------

            // Top
            spriteBatch.Draw(_pixel, new Rectangle(L + bev, T, R - L - bev * 2, 1), _global.Palette_Black);
            // Bottom
            spriteBatch.Draw(_pixel, new Rectangle(L + bev, B, R - L - bev * 2, 1), _global.Palette_Black);
            // Left
            spriteBatch.Draw(_pixel, new Rectangle(L, T + bev, 1, B - T - bev * 2), _global.Palette_Black);
            // Right
            spriteBatch.Draw(_pixel, new Rectangle(R, T + bev, 1, B - T - bev * 2), _global.Palette_Black);

            // Diagonals
            spriteBatch.DrawBresenhamLineSnapped(_pixel, new Vector2(L, T + bev), new Vector2(L + bev, T), _global.Palette_Black);
            spriteBatch.DrawBresenhamLineSnapped(_pixel, new Vector2(R - bev, T), new Vector2(R, T + bev), _global.Palette_Black);
            spriteBatch.DrawBresenhamLineSnapped(_pixel, new Vector2(L, B - bev), new Vector2(L + bev, B), _global.Palette_Black);
            spriteBatch.DrawBresenhamLineSnapped(_pixel, new Vector2(R - bev, B), new Vector2(R, B - bev), _global.Palette_Black);

            foreach (var attack in _activeAttacks)
            {
                attack.DeliveryInstance.Draw(spriteBatch, attack);
            }

            _wizards.Sort((a, b) => a.Data.Combat.Position.Y.CompareTo(b.Data.Combat.Position.Y));

            foreach (var wizard in _wizards)
            {
                if (wizard.Data.Combat.State == WizardState.Dead) _wizardRenderer.DrawWizard(wizard, spriteBatch, _spriteManager, _global);
            }

            foreach (var wizard in _wizards)
            {
                if (wizard.Data.Combat.State != WizardState.Dead) _wizardRenderer.DrawWizard(wizard, spriteBatch, _spriteManager, _global);
            }

            foreach (var attack in _activeAttacks)
            {
                attack.Animation?.Draw(spriteBatch, attack);
            }

            spriteBatch.End();

            // Draw Foreground Particles (Explosions, Sparks, etc.)
            _particleSystemManager.Draw(spriteBatch, transform, 1);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            if (_hoveredHudWizard != null && _hoveredHudWizard.Data.Combat.State != WizardState.Dead)
            {
                Vector2 mousePos = Core.TransformMouse(_inputManager.GetEffectiveMouseState().Position);
                var points = SpriteBatchExtensions.GetBresenhamLinePoints(mousePos, _hoveredHudWizard.Data.Combat.Position);
                int offset = (int)(effectiveGameTime.TotalGameTime.TotalSeconds * 30) % 6;
                for (int i = offset; i < points.Count; i += 6)
                {
                    spriteBatch.Draw(_pixel, new Vector2(points[i].X, points[i].Y), _global.Palette_Fruit);
                }
            }

            foreach (var wizard in _wizards)
            {
                _wizardRenderer.DrawUI(wizard, spriteBatch, _spriteManager, effectiveGameTime);
                _wizardRenderer.DrawDebug(wizard, spriteBatch, _battleContext);
            }

            DrawTopHUD(spriteBatch);

            var mainFont = _core.DefaultFont;
            var tertFont = _core.TertiaryFont;

            _ticketManager.Draw(spriteBatch, _spriteManager, _global, mainFont, tertFont);

            if ((_arenaState == ArenaState.Fighting && _phaseTimer >= 1.0f) || _arenaState == ArenaState.MatchOver)
            {
                string timerText = IsOvertime ? "OVERTIME" : _lastMatchSecond.ToString();
                Color timerColor = IsOvertime ? _global.Palette_Rust : (_lastMatchSecond <= 3 ? _global.Palette_Rust : _global.Palette_DarkPale);

                Vector2 tSize = mainFont.MeasureString(timerText);
                Vector2 tPos = new Vector2(MathF.Round(_arenaCenter.X), 48 + MathF.Round(tSize.Y / 2f));
                Vector2 tOrigin = new Vector2(MathF.Round(tSize.X / 2f), MathF.Round(tSize.Y / 2f));

                float tScale = _plinkTimerText.IsActive ? _plinkTimerText.Scale : 1f;
                float tRot = _plinkTimerText.IsActive ? _plinkTimerText.Rotation : 0f;

                spriteBatch.DrawStringOutlinedSnapped(mainFont, timerText, tPos, timerColor, _global.Palette_Off, tRot, tOrigin, tScale, SpriteEffects.None, 0f);
            }

            if (_suddenDeathTimer > 0)
            {
                string sdText = "SUDDEN DEATH";
                Vector2 sdSize = mainFont.MeasureString(sdText);
                Vector2 sdPos = new Vector2(MathF.Round(_arenaCenter.X), MathF.Round(Global.VIRTUAL_HEIGHT / 2f - 20));
                Vector2 sdOrigin = new Vector2(MathF.Round(sdSize.X / 2f), MathF.Round(sdSize.Y / 2f));

                float sdScale = _plinkSuddenDeath.IsActive ? _plinkSuddenDeath.Scale : 1f;
                float sdRot = _plinkSuddenDeath.IsActive ? _plinkSuddenDeath.Rotation : 0f;
                float sdAlpha = Math.Clamp(_suddenDeathTimer, 0f, 1f);

                spriteBatch.DrawStringOutlinedSnapped(mainFont, sdText, sdPos, _global.Palette_Rust * sdAlpha, _global.Palette_Off * sdAlpha, sdRot, sdOrigin, sdScale, SpriteEffects.None, 0f);
            }

            if (_arenaState == ArenaState.Betting)
            {
                int currentSecond = (int)Math.Ceiling(_phaseTimer);
                if (currentSecond > 0)
                {
                    float countScale = _plinkBetCountdown.IsActive ? _plinkBetCountdown.Scale : 1f;
                    float countRot = _plinkBetCountdown.IsActive ? _plinkBetCountdown.Rotation : 0f;

                    Vector2 betPos;

                    if (currentSecond <= 3 && _spriteManager.CountdownNumbersSpriteSheet != null)
                    {
                        var sheet = _spriteManager.CountdownNumbersSpriteSheet;
                        int frameIndex = 3 - currentSecond;
                        Rectangle sourceRect = new Rectangle(frameIndex * 32, 0, 32, 32);
                        Vector2 origin = new Vector2(16, 16);
                        Vector2 pos = new Vector2(MathF.Round(_arenaCenter.X), MathF.Round(_arenaCenter.Y));

                        float timeSinceTick = currentSecond - _phaseTimer;
                        float popProgress = Math.Clamp(timeSinceTick / 0.35f, 0f, 1f);
                        float popScale = Easing.EaseOutBackBig(popProgress);
                        float popRot = (1f - popProgress) * 0.3f * (currentSecond % 2 == 0 ? 1 : -1);

                        float finalScale = popScale;
                        float finalRot = countRot + popRot;

                        spriteBatch.DrawSnapped(sheet, pos, sourceRect, _global.Palette_Rust, finalRot, origin, finalScale, SpriteEffects.None, 0f);

                        betPos = new Vector2(MathF.Round(_arenaCenter.X), MathF.Round(pos.Y - 16 - 10));
                    }
                    else
                    {
                        string countText = currentSecond.ToString();
                        Vector2 countSize = mainFont.MeasureString(countText);
                        Vector2 countPos = new Vector2(MathF.Round(_arenaCenter.X - countSize.X / 2f), MathF.Round(_arenaCenter.Y - countSize.Y / 2f));
                        Vector2 countOrigin = new Vector2(MathF.Round(countSize.X / 2f), MathF.Round(countSize.Y / 2f));
                        Color countColor = _global.Palette_Sun;

                        spriteBatch.DrawStringOutlinedSnapped(mainFont, countText, countPos + countOrigin, countColor, _global.Palette_Off, countRot, countOrigin, countScale, SpriteEffects.None, 0f);

                        betPos = new Vector2(MathF.Round(_arenaCenter.X), MathF.Round(countPos.Y - 10));
                    }

                    string betText = "PLACE YOUR BETS";
                    Vector2 betSize = mainFont.MeasureString(betText);
                    betPos.X -= betSize.X / 2f;
                    betPos.Y -= betSize.Y;

                    TextAnimator.DrawTextWithEffectOutlined(
                        spriteBatch,
                        mainFont,
                        betText,
                        betPos,
                        _global.Palette_White,
                        _global.Palette_Off,
                        TextEffectType.RainbowWave,
                        (float)effectiveGameTime.TotalGameTime.TotalSeconds,
                        Vector2.One,
                        null,
                        0f
                    );
                }

                _skipButton.Draw(spriteBatch, tertFont, effectiveGameTime, transform);
            }
            else if (_arenaState == ArenaState.Fighting && _phaseTimer < 1.0f)
            {
                string text = "FIGHT!";
                Vector2 size = mainFont.MeasureString(text);
                Vector2 pos = new Vector2(MathF.Round(_arenaCenter.X - size.X / 2f), MathF.Round(_arenaCenter.Y - size.Y / 2f));
                Vector2 origin = new Vector2(MathF.Round(size.X / 2f), MathF.Round(size.Y / 2f));
                float scale = _plinkFight.IsActive ? _plinkFight.Scale : 1f;
                float rot = _plinkFight.IsActive ? _plinkFight.Rotation : 0f;
                float alpha = 1.0f - _phaseTimer;

                spriteBatch.DrawStringOutlinedSnapped(mainFont, text, pos + origin, _global.Palette_Sun * alpha, _global.Palette_Off * alpha, rot, origin, scale, SpriteEffects.None, 0f);
            }
            else if (_arenaState == ArenaState.MatchOver)
            {
                string text = _matchResultText;
                Color textColor = _matchResultColor;

                if (text.EndsWith("WINS!"))
                {
                    float flash = (float)(Math.Sin(effectiveGameTime.TotalGameTime.TotalSeconds * 15f) + 1f) / 2f;
                    textColor = Color.Lerp(_global.Palette_Sky, _global.Palette_Sun, flash);
                }

                Vector2 size = mainFont.MeasureString(text);
                Vector2 pos = new Vector2(MathF.Round(_arenaCenter.X - size.X / 2f), MathF.Round(_arenaCenter.Y - size.Y / 2f - 10));

                spriteBatch.DrawStringOutlinedSnapped(mainFont, text, pos, textColor, _global.Palette_Off);
            }

            if (_arenaState == ArenaState.Paused)
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black * 0.7f);

                string pauseText = "PAUSED";
                Vector2 pSize = mainFont.MeasureString(pauseText);
                spriteBatch.DrawStringOutlinedSnapped(mainFont, pauseText, new Vector2(Global.VIRTUAL_WIDTH / 2f - pSize.X / 2f, 30), _global.Palette_Sun, _global.Palette_Off);

                var secondaryFont = _core.SecondaryFont;
                foreach (var btn in _pauseButtons)
                {
                    btn.Draw(spriteBatch, secondaryFont, effectiveGameTime, transform);
                }

                if (_confirmationDialog.IsActive)
                {
                    _confirmationDialog.DrawContent(spriteBatch, font, gameTime, transform);
                }
            }
        }

        private string GetOrdinalSuffix(int number)
        {
            int mod100 = number % 100;
            if (mod100 >= 11 && mod100 <= 13) return "TH";
            switch (number % 10)
            {
                case 1: return "ST";
                case 2: return "ND";
                case 3: return "RD";
                default: return "TH";
            }
        }

        private void DrawTopHUD(SpriteBatch spriteBatch)
        {
            var defaultFont = _core.DefaultFont;
            var secondaryFont = _core.SecondaryFont;
            var tertiaryFont = _core.TertiaryFont;
            var sheet = _spriteManager.HealthHeartsSpriteSheet;
            if (sheet == null || _wizardsFixedOrder.Count == 0) return;

            int heartWidth = 5;
            int heartSpacing = 1;

            int aliveCount = _wizardsFixedOrder.Count(wiz => wiz.Data.Combat.State != WizardState.Dead);
            bool showProbabilities = _arenaState == ArenaState.Betting || (_arenaState == ArenaState.Fighting && aliveCount > 1);

            float colWidth = Global.VIRTUAL_WIDTH / (float)_wizardsFixedOrder.Count;

            for (int i = 0; i < _wizardsFixedOrder.Count; i++)
            {
                var w = _wizardsFixedOrder[i];
                float centerX = colWidth * i + colWidth / 2f;

                float shakeX = 0f;
                float shakeY = 0f;
                if (w.Data.UI.HudShakeTimer > 0)
                {
                    float shakeMag = (w.Data.UI.HudShakeTimer / 0.4f) * 3f;
                    shakeX = (float)(_random.NextDouble() * 2 - 1) * shakeMag;
                    shakeY = (float)(_random.NextDouble() * 2 - 1) * shakeMag;
                }

                Rectangle hudRect = new Rectangle((int)(colWidth * i), 0, (int)colWidth, 40);
                if (_hoveredHudWizard == w)
                {
                    Color bgColor = _global.Palette_DarkShadow * 0.5f;
                    spriteBatch.Draw(_pixel, new Rectangle(hudRect.X + 1, hudRect.Y, hudRect.Width - 2, hudRect.Height), bgColor);
                    spriteBatch.Draw(_pixel, new Rectangle(hudRect.X, hudRect.Y + 1, 1, hudRect.Height - 2), bgColor);
                    spriteBatch.Draw(_pixel, new Rectangle(hudRect.Right - 1, hudRect.Y + 1, 1, hudRect.Height - 2), bgColor);
                }

                Color baseNameColor = w.Data.Stats.IsPlayer ? _global.Palette_DarkPale : _global.Palette_DarkestPale;
                Color nameColor = baseNameColor;

                if (w.Data.Combat.State == WizardState.Dead)
                {
                    float fadeProgress = Math.Clamp(w.Data.Combat.TimeSinceDeath / 0.5f, 0f, 1f);
                    nameColor = Color.Lerp(baseNameColor, _global.Palette_Black, fadeProgress);
                }

                Vector2 finalNamePos = new Vector2(MathF.Round(w.Data.UI.HudNamePos.X + shakeX), MathF.Round(w.Data.UI.HudNamePos.Y + shakeY));
                spriteBatch.DrawStringOutlinedSnapped(secondaryFont, w.Data.Stats.Name.ToUpper(), finalNamePos, nameColor, _global.Palette_Off);

                float showSpellAlpha = 1f;
                float showPlacementAlpha = 0f;

                if (w.Data.Combat.State == WizardState.Dead)
                {
                    float fadeProgress = Math.Clamp(w.Data.Combat.TimeSinceDeath / 0.5f, 0f, 1f);
                    showSpellAlpha = 1f - fadeProgress;
                    showPlacementAlpha = fadeProgress;
                }
                else if (w.Data.Metrics.Placement == 1 && _arenaState == ArenaState.MatchOver)
                {
                    float fadeProgress = Math.Clamp((4.0f - _matchOverTimer) / 0.5f, 0f, 1f);
                    showSpellAlpha = 1f - fadeProgress;
                    showPlacementAlpha = fadeProgress;
                }

                if (w.Data.Combat.EquippedActiveSpell != null && showSpellAlpha > 0f)
                {
                    var spellSheet = _spriteManager.ActiveSpellsSpriteSheet;
                    if (spellSheet != null)
                    {
                        int frame = w.Data.Combat.EquippedActiveSpell.SpriteFrame;
                        Rectangle sourceRect = new Rectangle(frame * 9, 0, 9, 9);

                        float cd = w.Data.Combat.ActiveSpellCooldownTimer;
                        float opacity = (cd > 0 ? 0.15f : 1.0f) * showSpellAlpha;

                        Vector2 spellPos = new Vector2(MathF.Round(centerX - 4.5f + shakeX), MathF.Round(4f + shakeY));

                        // 11x11 Backing
                        spriteBatch.Draw(_pixel, new Rectangle((int)spellPos.X - 1, (int)spellPos.Y - 1, 11, 11), _global.Palette_Off * showSpellAlpha);

                        spriteBatch.DrawSnapped(spellSheet, spellPos, sourceRect, Color.White * opacity);

                        if (cd > 0)
                        {
                            string cdText = MathF.Ceiling(cd).ToString();
                            Vector2 cdSize = secondaryFont.MeasureString(cdText);
                            Vector2 cdPos = spellPos + new Vector2(4.5f, 4.5f) - new Vector2(MathF.Round(cdSize.X / 2f), MathF.Round(cdSize.Y / 2f));

                            spriteBatch.DrawStringSquareOutlinedSnapped(secondaryFont, cdText, cdPos, _global.Palette_Sun * showSpellAlpha, _global.Palette_Black * showSpellAlpha);
                        }
                    }
                }

                float heartsY = w.Data.UI.HudHeartStartPos.Y + shakeY;
                if (w.Data.Combat.State == WizardState.Dead)
                {
                    float fadeProgress = Math.Clamp(w.Data.Combat.TimeSinceDeath / 0.5f, 0f, 1f);
                    int currentLineWidth = (int)(w.Data.UI.HudNameSize.X * fadeProgress);
                    if (currentLineWidth > 0)
                    {
                        int lineY = (int)MathF.Round(finalNamePos.Y + w.Data.UI.HudNameSize.Y / 2f);
                        spriteBatch.Draw(_pixel, new Rectangle((int)finalNamePos.X, lineY, currentLineWidth, 1), _global.Palette_Black);
                    }
                }
                else
                {
                    int maxHearts = (w.Data.Stats.MaxHP + 2) / 3;
                    for (int h = 0; h < maxHearts; h++)
                    {
                        int heartVal = Math.Clamp(w.Data.Stats.CurrentHP - h * 3, 0, 3);
                        int frameIndex = 3;
                        if (heartVal == 3) frameIndex = 0;
                        else if (heartVal == 2) frameIndex = 1;
                        else if (heartVal == 1) frameIndex = 2;

                        int flashFrame = w.Controller.GetHeartFlashFrame(h);
                        if (flashFrame != -1) frameIndex = flashFrame;

                        var sourceRect = new Rectangle(frameIndex * heartWidth, 0, heartWidth, 5);

                        int yOffset = 0;
                        if (w.Data.Stats.CurrentHP > 0)
                        {
                            float localWaveTime = w.Data.UI.HudHeartWaveTimer - w.Data.UI.HudHeartWaveInterval - (h * 0.08f);
                            if (localWaveTime > 0 && localWaveTime < 0.15f)
                            {
                                yOffset = -1;
                            }
                        }

                        Vector2 finalHeartPos = new Vector2(MathF.Round(w.Data.UI.HudHeartStartPos.X + h * (heartWidth + heartSpacing) + shakeX), MathF.Round(heartsY) + yOffset);

                        // Outline
                        spriteBatch.DrawSnapped(sheet, finalHeartPos + new Vector2(-1, 0), sourceRect, _global.Palette_Off);
                        spriteBatch.DrawSnapped(sheet, finalHeartPos + new Vector2(1, 0), sourceRect, _global.Palette_Off);
                        spriteBatch.DrawSnapped(sheet, finalHeartPos + new Vector2(0, -1), sourceRect, _global.Palette_Off);
                        spriteBatch.DrawSnapped(sheet, finalHeartPos + new Vector2(0, 1), sourceRect, _global.Palette_Off);

                        // Main
                        spriteBatch.DrawSnapped(sheet, finalHeartPos, sourceRect, Color.White);
                    }
                }

                float probY = heartsY + 10f;
                if (w.Data.Combat.State != WizardState.Dead && showProbabilities)
                {
                    int step = _probSteps.TryGetValue(w, out var s) ? s : 0;
                    Color probColor = GetMultiplierColor(step);
                    BitmapFont probFont = secondaryFont;

                    if (step < 4) probFont = tertiaryFont;
                    else if (step >= MULT_DEFAULT_MIN_STEP) probFont = defaultFont;

                    float prob = _winProbabilities.TryGetValue(w, out var p) ? p : 0f;
                    string probText = $"{(prob * 100f):F0}%";

                    var plink = _probPlinks[w];
                    float pScale = plink.IsActive ? plink.Scale : 1f;
                    float pRot = plink.IsActive ? plink.Rotation : 0f;

                    if (plink.IsActive && plink.FlashTint.HasValue)
                    {
                        float flashAmount = plink.FlashTint.Value.A / 255f;
                        probColor = Color.Lerp(probColor, Color.White, flashAmount);
                    }

                    Vector2 probSize = probFont.MeasureString(probText);
                    Vector2 pivot = new Vector2(MathF.Round(probSize.X / 2f), MathF.Round(probSize.Y / 2f));
                    Vector2 probOrigin = new Vector2(MathF.Round(pivot.X), MathF.Round(pivot.Y));

                    Vector2 drawPos = new Vector2(MathF.Round(centerX + shakeX), MathF.Round(probY + pivot.Y));

                    spriteBatch.DrawStringOutlinedSnapped(probFont, probText, drawPos, probColor, _global.Palette_Off, pRot, probOrigin, pScale, SpriteEffects.None, 0f);
                }

                if (showPlacementAlpha > 0f && w.Data.Metrics.Placement > 0)
                {
                    string numText = w.Data.Metrics.Placement.ToString();
                    string sufText = GetOrdinalSuffix(w.Data.Metrics.Placement);

                    Vector2 numSize = defaultFont.MeasureString(numText);
                    Vector2 sufSize = secondaryFont.MeasureString(sufText);

                    float totalWidth = numSize.X + sufSize.X;
                    float startX = centerX - totalWidth / 2f;

                    float numY = probY;
                    float sufY = probY + (numSize.Y - sufSize.Y);

                    Color placementColor = w.Data.Metrics.Placement == 1 ? _global.Palette_Sun : _global.Palette_Black;
                    placementColor *= showPlacementAlpha;
                    Color outlineColor = _global.Palette_Off * showPlacementAlpha;

                    spriteBatch.DrawStringOutlinedSnapped(defaultFont, numText, new Vector2(MathF.Round(startX), MathF.Round(numY)), placementColor, outlineColor);
                    spriteBatch.DrawStringOutlinedSnapped(tertiaryFont, sufText, new Vector2(MathF.Round(startX + numSize.X) + 1, MathF.Round(sufY)), placementColor, outlineColor);
                }
            }
        }

        public void TriggerHitstop(float duration)
        {
            _hitstopTimer = Math.Max(_hitstopTimer, duration);
        }
    }
}
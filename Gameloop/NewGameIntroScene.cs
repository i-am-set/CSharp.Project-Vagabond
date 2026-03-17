#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Animations;
using ProjectVagabond.Battle;
using ProjectVagabond.Deliveries;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class NewGameIntroScene : GameScene
    {
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly InputManager _inputManager;
        private readonly SceneManager _sceneManager;
        private readonly TransitionManager _transitionManager;
        private readonly HapticsManager _hapticsManager;
        private readonly ParticleSystemManager _particleSystemManager;
        private readonly Random _random = new Random();

        // Carousel State
        private List<string> _characterIds = new();
        private Dictionary<string, (float Timer, float TargetRotation)> _selectedWizards = new();

        private float _virtualIndex = 0f;
        private int _targetVirtualIndex = 0;
        private const float CAROUSEL_SLIDE_SPEED = 15f;

        // Drag State
        private bool _isMouseDownOnCarousel = false;
        private bool _isDraggingCarousel = false;
        private Vector2 _dragStartPos;
        private float _dragStartVirtualIndex;
        private float _carouselVelocity = 0f;
        private float _carouselFriction = 2.5f;
        private int _lastHapticIndex = 0;

        // Auto-Select State
        private enum AutoSelectState { Spinning, Pausing }
        private bool _isAutoSelecting = false;
        private AutoSelectState _autoSelectState;
        private List<string> _autoSelectQueue = new List<string>();
        private float _spinStartVirtualIndex;
        private float _spinTargetVirtualIndex;
        private float _spinDuration;
        private float _spinTimer;
        private float _pauseDuration;
        private float _timePerSelection;
        private const float TOTAL_AUTO_SELECT_DURATION = 2.0f;

        // UI Elements
        private Button _leftArrow = null!;
        private Button _rightArrow = null!;
        private Button _btnMode1v1 = null!;
        private Button _btnMode4ffa = null!;
        private Button _btnMode6ffa = null!;
        private Button _btnMode8ffa = null!;
        private Button _startButton = null!;
        private readonly NavigationGroup _navigationGroup;

        private int _randomCount = 6;

        // Intro Text
        private const string INTRO_LINE_1 = "CHOOSE A";
        private const string INTRO_LINE_2 = "WIZARD CAT";

        // --- Plink Animation State ---
        private bool _isPlinkingIn = true;
        private PlinkAnimator _plinkTitle1 = null!;
        private PlinkAnimator _plinkTitle2 = null!;
        private PlinkAnimator _plinkStats = null!;
        private PlinkAnimator[] _plinkCarousel = new PlinkAnimator[7];
        private List<PlinkAnimator> _allPlinks = new List<PlinkAnimator>();

        private Queue<Action> _plinkQueue = new Queue<Action>();
        private float _plinkTimer = 0f;
        private const float PLINK_STAGGER = 0.05f;

        // Idle Animation State
        private float _idleTimer = 0f;
        private float _titleWaveTimer = 0f;
        private float _introHeartWaveTimer = 0f;
        private float _introHeartWaveInterval = 3f;

        // Arrow Simulation State
        private float _leftArrowSimTimer = 0f;
        private float _rightArrowSimTimer = 0f;
        private const float ARROW_SIM_DURATION = 0.15f;

        // Scroll State
        private int _lastScrollWheelValue;
        private int _scrollAccumulator;
        private MouseState _previousMouseState;

        // Layout Constants
        private const int BASE_CENTER_Y = 50;

        public NewGameIntroScene()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _navigationGroup = new NavigationGroup(wrapNavigation: true);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Enter()
        {
            base.Enter();
            InitializeData();
            InitializeUI();

            _selectedWizards.Clear();
            _isAutoSelecting = false;
            _titleWaveTimer = 0f;
            _idleTimer = 0f;
            _leftArrowSimTimer = 0f;
            _rightArrowSimTimer = 0f;
            _introHeartWaveTimer = 0f;
            _introHeartWaveInterval = 2f + (float)_random.NextDouble() * 4f;

            _previousMouseState = _inputManager.GetEffectiveMouseState();
            _lastScrollWheelValue = _previousMouseState.ScrollWheelValue;
            _scrollAccumulator = 0;

            _isMouseDownOnCarousel = false;
            _isDraggingCarousel = false;
            _carouselVelocity = 0f;
            _lastHapticIndex = _targetVirtualIndex;

            _navigationGroup.DeselectAll();

            _isPlinkingIn = true;
            _allPlinks.Clear();
            _plinkQueue.Clear();

            _plinkTitle1 = new PlinkAnimator(); _allPlinks.Add(_plinkTitle1);
            _plinkTitle2 = new PlinkAnimator(); _allPlinks.Add(_plinkTitle2);
            _plinkStats = new PlinkAnimator(); _allPlinks.Add(_plinkStats);

            for (int i = 0; i < 7; i++)
            {
                _plinkCarousel[i] = new PlinkAnimator();
                _allPlinks.Add(_plinkCarousel[i]);
            }

            foreach (var p in _allPlinks) p.Start(9999f, 0.25f);

            _leftArrow.SetHiddenForEntrance();
            _rightArrow.SetHiddenForEntrance();
            _btnMode1v1.SetHiddenForEntrance();
            _btnMode4ffa.SetHiddenForEntrance();
            _btnMode6ffa.SetHiddenForEntrance();
            _btnMode8ffa.SetHiddenForEntrance();
            _startButton.SetHiddenForEntrance();

            _allPlinks.Add(_leftArrow.Plink);
            _allPlinks.Add(_rightArrow.Plink);
            _allPlinks.Add(_btnMode1v1.Plink);
            _allPlinks.Add(_btnMode4ffa.Plink);
            _allPlinks.Add(_btnMode6ffa.Plink);
            _allPlinks.Add(_btnMode8ffa.Plink);
            _allPlinks.Add(_startButton.Plink);

            var randomActions = new List<Action>
            {
                () => _plinkTitle1.Start(0f, 0.25f),
                () => _plinkTitle2.Start(0f, 0.25f),
                () => _plinkStats.Start(0f, 0.25f)
            };

            for (int i = 0; i < 7; i++)
            {
                int index = i;
                randomActions.Add(() => _plinkCarousel[index].Start(0f, 0.25f));
            }

            randomActions = randomActions.OrderBy(x => _random.Next()).ToList();

            foreach (var a in randomActions) _plinkQueue.Enqueue(a);

            _plinkQueue.Enqueue(() => _leftArrow.PlayEntrance(0f));
            _plinkQueue.Enqueue(() => _rightArrow.PlayEntrance(0f));
            _plinkQueue.Enqueue(() => _btnMode1v1.PlayEntrance(0f));
            _plinkQueue.Enqueue(() => _btnMode4ffa.PlayEntrance(0f));
            _plinkQueue.Enqueue(() => _btnMode6ffa.PlayEntrance(0f));
            _plinkQueue.Enqueue(() => _btnMode8ffa.PlayEntrance(0f));
            _plinkQueue.Enqueue(() => _startButton.PlayEntrance(0f));

            _plinkTimer = 0f;
        }

        private void InitializeData()
        {
            _characterIds = GameDataCache.WizardCats.Keys.ToList();
            _characterIds.Sort((a, b) =>
            {
                if (int.TryParse(a, out int idA) && int.TryParse(b, out int idB))
                    return idA.CompareTo(idB);
                return string.Compare(a, b, StringComparison.Ordinal);
            });

            int oakleyIndex = _characterIds.IndexOf("0");
            _targetVirtualIndex = oakleyIndex != -1 ? oakleyIndex : 0;
            _virtualIndex = _targetVirtualIndex;
        }

        private void InitializeUI()
        {
            _navigationGroup.Clear();
            var core = ServiceLocator.Get<Core>();
            var secondaryFont = core.SecondaryFont;

            int centerX = Global.VIRTUAL_WIDTH / 2;
            int centerY = BASE_CENTER_Y;

            int arrowY = centerY;
            int halfWidth = Global.VIRTUAL_WIDTH / 2;
            int buttonHeight = 128;
            int buttonY = arrowY - (buttonHeight / 2);

            float leftButtonCenterX = halfWidth / 2f;
            float leftTextTargetX = centerX - 24;
            float leftOffset = leftTextTargetX - leftButtonCenterX;

            _leftArrow = new Button(
                new Rectangle(0, buttonY, halfWidth, buttonHeight),
                "<",
                font: secondaryFont
            )
            {
                TriggerHapticOnHover = true,
                EnableHoverSway = true,
                HoverAnimation = HoverAnimationType.None,
                EnableTextWave = false,
                TextRenderOffset = new Vector2(leftOffset, 0)
            };

            float rightButtonCenterX = halfWidth + (halfWidth / 2f);
            float rightTextTargetX = centerX + 24;
            float rightOffset = rightTextTargetX - rightButtonCenterX;

            _rightArrow = new Button(
                new Rectangle(halfWidth, buttonY, halfWidth, buttonHeight),
                ">",
                font: secondaryFont
            )
            {
                TriggerHapticOnHover = true,
                EnableHoverSway = true,
                HoverAnimation = HoverAnimationType.None,
                EnableTextWave = false,
                TextRenderOffset = new Vector2(rightOffset, 0)
            };

            int btnW = 50;
            int btnH = 16;
            int gridStartX = centerX - 51;
            int gridStartY = 132;
            int gridSpacingX = 52;
            int gridSpacingY = 18;

            _btnMode1v1 = new Button(new Rectangle(gridStartX, gridStartY, btnW, btnH), "1v1", font: secondaryFont)
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                CustomDefaultTextColor = _global.Palette_Off,
                CustomHoverTextColor = _global.Palette_Off,
                CustomSelectedTextColor = _global.Palette_Off,
                CustomDisabledTextColor = _global.Palette_Off
            };
            _btnMode1v1.OnClick += () => SetMode(2);
            _navigationGroup.Add(_btnMode1v1);

            _btnMode4ffa = new Button(new Rectangle(gridStartX + gridSpacingX, gridStartY, btnW, btnH), "4 FFA", font: secondaryFont)
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                CustomDefaultTextColor = _global.Palette_Off,
                CustomHoverTextColor = _global.Palette_Off,
                CustomSelectedTextColor = _global.Palette_Off,
                CustomDisabledTextColor = _global.Palette_Off
            };
            _btnMode4ffa.OnClick += () => SetMode(4);
            _navigationGroup.Add(_btnMode4ffa);

            _btnMode6ffa = new Button(new Rectangle(gridStartX, gridStartY + gridSpacingY, btnW, btnH), "6 FFA", font: secondaryFont)
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                CustomDefaultTextColor = _global.Palette_Off,
                CustomHoverTextColor = _global.Palette_Off,
                CustomSelectedTextColor = _global.Palette_Off,
                CustomDisabledTextColor = _global.Palette_Off
            };
            _btnMode6ffa.OnClick += () => SetMode(6);
            _navigationGroup.Add(_btnMode6ffa);

            _btnMode8ffa = new Button(new Rectangle(gridStartX + gridSpacingX, gridStartY + gridSpacingY, btnW, btnH), "8 FFA", font: secondaryFont)
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                CustomDefaultTextColor = _global.Palette_Off,
                CustomHoverTextColor = _global.Palette_Off,
                CustomSelectedTextColor = _global.Palette_Off,
                CustomDisabledTextColor = _global.Palette_Off
            };
            _btnMode8ffa.OnClick += () => SetMode(8);
            _navigationGroup.Add(_btnMode8ffa);

            string startText = "START";
            Vector2 startSize = core.DefaultFont.MeasureString(startText);
            int startW = (int)startSize.X + 10;
            int startH = (int)startSize.Y + 6;
            int startButtonY = 146;

            _startButton = new Button(
                new Rectangle(Global.VIRTUAL_WIDTH - startW - 20, startButtonY, startW, startH),
                startText,
                font: core.DefaultFont
            )
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop
            };
            _startButton.OnClick += StartGame;
            _navigationGroup.Add(_startButton);
        }

        private void SetMode(int count)
        {
            if (_isPlinkingIn || _isAutoSelecting) return;
            if (_randomCount != count)
            {
                _randomCount = count;
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

                if (count == 2) _btnMode1v1.Plink.Start(0f, 0.15f);
                else if (count == 4) _btnMode4ffa.Plink.Start(0f, 0.15f);
                else if (count == 6) _btnMode6ffa.Plink.Start(0f, 0.15f);
                else if (count == 8) _btnMode8ffa.Plink.Start(0f, 0.15f);
            }
        }

        private void CycleCharacter(int direction)
        {
            if (_isPlinkingIn || _isAutoSelecting) return;

            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
            {
                if (direction == -1) _leftArrowSimTimer = ARROW_SIM_DURATION;
                else _rightArrowSimTimer = ARROW_SIM_DURATION;
            }

            _targetVirtualIndex += direction;
        }

        private void StartGame()
        {
            if (_isPlinkingIn || _transitionManager.IsTransitioning || _isAutoSelecting) return;
            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

            var shuffled = _characterIds.OrderBy(x => _random.Next()).ToList();
            _autoSelectQueue = shuffled.Take(_randomCount).ToList();
            _selectedWizards.Clear();

            _timePerSelection = TOTAL_AUTO_SELECT_DURATION / _randomCount;

            _isAutoSelecting = true;
            SetupNextSpin();
        }

        private void SetupNextSpin()
        {
            if (_autoSelectQueue.Count == 0)
            {
                StartGameActual();
                return;
            }

            string targetId = _autoSelectQueue[0];
            int targetIndex = _characterIds.IndexOf(targetId);
            int count = _characterIds.Count;

            int currentIndex = ((int)MathF.Floor(_virtualIndex + 0.5f) % count + count) % count;
            int diff = targetIndex - currentIndex;

            if (diff > count / 2) diff -= count;
            else if (diff < -count / 2) diff += count;

            if (diff == 0) diff = count;

            _spinStartVirtualIndex = _virtualIndex;
            _spinTargetVirtualIndex = MathF.Floor(_virtualIndex + 0.5f) + diff;
            _spinDuration = _timePerSelection * 0.7f;
            _pauseDuration = _timePerSelection * 0.3f;
            _spinTimer = 0f;
            _autoSelectState = AutoSelectState.Spinning;
        }

        private void StartGameActual()
        {
            var core = ServiceLocator.Get<Core>();
            var gameState = ServiceLocator.Get<GameState>();

            var loadingTasks = new List<LoadingTask>
            {
                new GenericTask("Initializing arena...", () =>
                {
                    gameState.InitializeWorld(_selectedWizards.Keys.ToList());
                })
            };

            core.SetGameLoaded(true);

            var transitionOut = _transitionManager.GetRandomTransition();
            var transitionIn = _transitionManager.GetRandomTransition();
            _sceneManager.ChangeScene(GameSceneState.Arena, transitionOut, transitionIn, 0f, loadingTasks);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);
            float dt = (float)effectiveGameTime.ElapsedGameTime.TotalSeconds;

            if (_transitionManager.IsTransitioning) return;

            _idleTimer += dt;
            _titleWaveTimer += dt;

            foreach (var key in _selectedWizards.Keys.ToList())
            {
                var data = _selectedWizards[key];
                _selectedWizards[key] = (data.Timer + dt, data.TargetRotation);
            }

            _introHeartWaveTimer += dt;
            if (_introHeartWaveTimer > _introHeartWaveInterval + 1.0f)
            {
                _introHeartWaveTimer = 0f;
                _introHeartWaveInterval = 2f + (float)_random.NextDouble() * 4f;
            }

            if (_leftArrowSimTimer > 0f)
            {
                _leftArrowSimTimer -= dt;
                if (_leftArrowSimTimer < 0f) _leftArrowSimTimer = 0f;
            }

            if (_rightArrowSimTimer > 0f)
            {
                _rightArrowSimTimer -= dt;
                if (_rightArrowSimTimer < 0f) _rightArrowSimTimer = 0f;
            }

            var currentMouseState = _inputManager.GetEffectiveMouseState();
            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            if (_isPlinkingIn)
            {
                _plinkTimer -= dt;
                while (_plinkTimer <= 0f && _plinkQueue.Count > 0)
                {
                    _plinkQueue.Dequeue().Invoke();
                    _plinkTimer += PLINK_STAGGER;
                }

                int centerX = Global.VIRTUAL_WIDTH / 2;
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

                _plinkTitle1.Update(effectiveGameTime, new Vector2(centerX, 14));
                _plinkTitle2.Update(effectiveGameTime, new Vector2(centerX, 14 + secondaryFont.LineHeight + 2));
                _plinkStats.Update(effectiveGameTime, new Vector2(centerX, BASE_CENTER_Y + 70));

                int count = _characterIds.Count;
                float carouselSlideOffset = MathF.Floor(_virtualIndex + 0.5f) - _virtualIndex;

                for (int i = 0; i < 7; i++)
                {
                    int offset = i - 3;
                    float visualOffset = offset + carouselSlideOffset;
                    float xOffset = MathF.Sin(visualOffset * 0.5f) * 100f;
                    float xPos = centerX + xOffset;
                    float curveY = MathF.Pow(Math.Abs(visualOffset), 1.5f) * 2.0f;
                    float baseYPos = BASE_CENTER_Y - curveY;
                    _plinkCarousel[i].Update(effectiveGameTime, new Vector2(xPos, baseYPos));
                }

                if (_plinkQueue.Count == 0 && !_allPlinks.Any(p => p.IsActive))
                {
                    _isPlinkingIn = false;
                }
            }
            else
            {
                if (!_isAutoSelecting)
                {
                    _btnMode1v1.HoverAnimation = _randomCount == 2 ? HoverAnimationType.None : HoverAnimationType.Hop;
                    _btnMode4ffa.HoverAnimation = _randomCount == 4 ? HoverAnimationType.None : HoverAnimationType.Hop;
                    _btnMode6ffa.HoverAnimation = _randomCount == 6 ? HoverAnimationType.None : HoverAnimationType.Hop;
                    _btnMode8ffa.HoverAnimation = _randomCount == 8 ? HoverAnimationType.None : HoverAnimationType.Hop;

                    _btnMode1v1.Update(currentMouseState);
                    _btnMode4ffa.Update(currentMouseState);
                    _btnMode6ffa.Update(currentMouseState);
                    _btnMode8ffa.Update(currentMouseState);
                    _startButton.Update(currentMouseState);

                    bool hoverCount = _btnMode1v1.IsHovered || _btnMode4ffa.IsHovered || _btnMode6ffa.IsHovered || _btnMode8ffa.IsHovered;

                    if (!hoverCount && !_startButton.IsHovered)
                    {
                        _leftArrow.Update(currentMouseState);
                        _rightArrow.Update(currentMouseState);
                    }
                    else
                    {
                        _leftArrow.IsHovered = false;
                        _rightArrow.IsHovered = false;
                    }

                    bool hoverCarousel = _leftArrow.IsHovered || _rightArrow.IsHovered;

                    int currentScroll = currentMouseState.ScrollWheelValue;
                    int scrollDelta = currentScroll - _lastScrollWheelValue;
                    _lastScrollWheelValue = currentScroll;

                    if (scrollDelta != 0)
                    {
                        _scrollAccumulator += scrollDelta;
                        const int SCROLL_THRESHOLD = 120;

                        if (_scrollAccumulator >= SCROLL_THRESHOLD)
                        {
                            if (hoverCarousel) CycleCharacter(1);
                            else if (hoverCount)
                            {
                                int[] modes = { 2, 4, 6, 8 };
                                int idx = Array.IndexOf(modes, _randomCount);
                                idx = (idx + 1) % 4;
                                SetMode(modes[idx]);
                            }
                            _scrollAccumulator = 0;
                        }
                        else if (_scrollAccumulator <= -SCROLL_THRESHOLD)
                        {
                            if (hoverCarousel) CycleCharacter(-1);
                            else if (hoverCount)
                            {
                                int[] modes = { 2, 4, 6, 8 };
                                int idx = Array.IndexOf(modes, _randomCount);
                                idx = (idx - 1 + 4) % 4;
                                SetMode(modes[idx]);
                            }
                            _scrollAccumulator = 0;
                        }
                    }

                    bool justPressed = currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
                    bool justReleased = currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
                    bool isPressed = currentMouseState.LeftButton == ButtonState.Pressed;

                    if (justPressed && hoverCarousel)
                    {
                        _isMouseDownOnCarousel = true;
                        _isDraggingCarousel = false;
                        _dragStartPos = virtualMousePos;
                    }

                    if (isPressed && _isMouseDownOnCarousel)
                    {
                        float deltaX = _dragStartPos.X - virtualMousePos.X;
                        if (!_isDraggingCarousel && Math.Abs(deltaX) > 2f)
                        {
                            _isDraggingCarousel = true;
                            _dragStartPos = virtualMousePos;
                            _dragStartVirtualIndex = _virtualIndex;
                            _carouselVelocity = 0f;
                        }

                        if (_isDraggingCarousel)
                        {
                            float prevIndex = _virtualIndex;
                            float activeDeltaX = _dragStartPos.X - virtualMousePos.X;
                            _virtualIndex = _dragStartVirtualIndex + (activeDeltaX / 40f);
                            if (dt > 0) _carouselVelocity = (_virtualIndex - prevIndex) / dt;
                            _targetVirtualIndex = (int)MathF.Round(_virtualIndex);
                        }
                    }

                    if (justReleased && _isMouseDownOnCarousel)
                    {
                        _isMouseDownOnCarousel = false;
                        if (!_isDraggingCarousel)
                        {
                            if (_leftArrow.IsHovered) CycleCharacter(-1);
                            else if (_rightArrow.IsHovered) CycleCharacter(1);
                        }
                        _isDraggingCarousel = false;
                    }

                    if (!_isDraggingCarousel)
                    {
                        if (Math.Abs(_carouselVelocity) > 0.1f)
                        {
                            _virtualIndex += _carouselVelocity * dt;
                            _carouselVelocity *= MathF.Max(0f, 1f - _carouselFriction * dt);
                            _targetVirtualIndex = (int)MathF.Round(_virtualIndex);
                        }
                        else
                        {
                            _carouselVelocity = 0f;
                            _virtualIndex = MathHelper.Lerp(_virtualIndex, _targetVirtualIndex, dt * CAROUSEL_SLIDE_SPEED);
                        }
                    }

                    int currentIndex = (int)MathF.Round(_virtualIndex);
                    if (currentIndex != _lastHapticIndex)
                    {
                        _lastHapticIndex = currentIndex;
                        _hapticsManager.TriggerUICompoundShake(_global.HoverHapticStrength);
                    }

                    if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
                    {
                        _navigationGroup.DeselectAll();
                    }
                    else
                    {
                        if (!_btnMode1v1.IsSelected && !_btnMode4ffa.IsSelected && !_btnMode6ffa.IsSelected && !_btnMode8ffa.IsSelected && !_startButton.IsSelected)
                        {
                            _navigationGroup.Select(0);
                        }

                        if (_inputManager.NavigateLeft) CycleCharacter(-1);
                        else if (_inputManager.NavigateRight) CycleCharacter(1);

                        _navigationGroup.UpdateInput(_inputManager);
                    }

                    if (_inputManager.Back)
                    {
                        _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.None, TransitionType.None);
                    }
                }
                else
                {
                    _lastScrollWheelValue = currentMouseState.ScrollWheelValue;

                    if (_autoSelectState == AutoSelectState.Spinning)
                    {
                        _spinTimer += dt;
                        float p = Math.Clamp(_spinTimer / _spinDuration, 0f, 1f);
                        _virtualIndex = MathHelper.Lerp(_spinStartVirtualIndex, _spinTargetVirtualIndex, Easing.EaseInOutCubic(p));

                        int currentIndex = (int)MathF.Round(_virtualIndex);
                        if (currentIndex != _lastHapticIndex)
                        {
                            _lastHapticIndex = currentIndex;
                            _hapticsManager.TriggerUICompoundShake(_global.HoverHapticStrength);
                        }

                        if (p >= 1f)
                        {
                            _virtualIndex = _spinTargetVirtualIndex;
                            float sign = _random.Next(2) == 0 ? 1f : -1f;
                            float rotRad = MathHelper.ToRadians(_random.Next(4, 11)) * sign;
                            _selectedWizards[_autoSelectQueue[0]] = (0f, rotRad);
                            _autoSelectQueue.RemoveAt(0);
                            _autoSelectState = AutoSelectState.Pausing;
                            _spinTimer = 0f;
                            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength * 2f);

                            var emitter = _particleSystemManager.CreateEmitter(ParticleEffects.CreateUIPlink());
                            emitter.Position = new Vector2(Global.VIRTUAL_WIDTH / 2f, BASE_CENTER_Y);
                            emitter.EmitBurst(15);
                        }
                    }
                    else if (_autoSelectState == AutoSelectState.Pausing)
                    {
                        _spinTimer += dt;
                        if (_spinTimer >= _pauseDuration)
                        {
                            SetupNextSpin();
                        }
                    }
                }
            }

            _previousMouseState = currentMouseState;
        }

        private void DrawModeButtonBackground(SpriteBatch spriteBatch, Button btn, int modeValue, Texture2D pixel)
        {
            float scale = btn.Plink.IsActive ? btn.Plink.Scale : 1f;
            float rotation = btn.Plink.IsActive ? btn.Plink.Rotation : 0f;
            if (scale < 0.01f) return;

            float yOffset = 0f;
            Color bgColor;

            if (_randomCount == modeValue)
            {
                yOffset = 0f;
                bgColor = _global.Palette_Sun;
            }
            else
            {
                yOffset = btn.HoverAnimator.CurrentOffset;
                if (btn.IsPressed) bgColor = _global.Palette_Fruit;
                else if (btn.IsHovered) bgColor = _global.ButtonHoverColor;
                else bgColor = _global.Palette_DarkestPale;
            }

            Vector2 center = new Vector2(btn.Bounds.Center.X, btn.Bounds.Center.Y + yOffset);

            int w = btn.Bounds.Width;
            int h = btn.Bounds.Height;

            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), bgColor, rotation, new Vector2(0.5f, 0.5f), new Vector2(w - 4, h) * scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), bgColor, rotation, new Vector2(0.5f, 0.5f), new Vector2(w - 2, h - 2) * scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), bgColor, rotation, new Vector2(0.5f, 0.5f), new Vector2(w, h - 4) * scale, SpriteEffects.None, 0f);
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);

            var core = ServiceLocator.Get<Core>();
            var secondaryFont = core.SecondaryFont;
            var tertiaryFont = core.TertiaryFont;
            Matrix staticTransform = Matrix.CreateScale(core.FinalScale, core.FinalScale, 1.0f) *
                                     Matrix.CreateTranslation(core.FinalRenderRectangle.X, core.FinalRenderRectangle.Y, 0);

            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, null, null, null, staticTransform);
            spriteBatch.Draw(_spriteManager.EmptySprite, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, staticTransform);

            float titleY = 14f;

            float t1Scale = _isPlinkingIn ? _plinkTitle1.Scale : 1f;
            float t1Rot = _isPlinkingIn ? _plinkTitle1.Rotation : 0f;
            if (t1Scale > 0.01f)
            {
                Vector2 size1 = secondaryFont.MeasureString(INTRO_LINE_1);
                var pos1 = new Vector2(MathF.Round((Global.VIRTUAL_WIDTH - size1.X) / 2f), MathF.Round(titleY - 2));
                TextAnimator.DrawTextWithEffect(spriteBatch, secondaryFont, INTRO_LINE_1, pos1, _global.Palette_DarkPale, TextEffectType.None, 0f, new Vector2(t1Scale), null, t1Rot);

                if (_isPlinkingIn && _plinkTitle1.FlashTint.HasValue)
                {
                    Rectangle bounds = new Rectangle((int)pos1.X, (int)pos1.Y, (int)size1.X, (int)size1.Y);
                    spriteBatch.DrawSnapped(_spriteManager.EmptySprite, bounds, _plinkTitle1.FlashTint.Value);
                }
            }

            float t2Scale = _isPlinkingIn ? _plinkTitle2.Scale : 1f;
            float t2Rot = _isPlinkingIn ? _plinkTitle2.Rotation : 0f;
            if (t2Scale > 0.01f)
            {
                Vector2 size2 = font.MeasureString(INTRO_LINE_2);
                var pos2 = new Vector2(MathF.Round((Global.VIRTUAL_WIDTH - size2.X) / 2f), MathF.Round(titleY + secondaryFont.LineHeight + 2));
                TextAnimator.DrawTextWithEffect(spriteBatch, font, INTRO_LINE_2, pos2, _global.Palette_White, TextEffectType.RainbowWave, _titleWaveTimer, new Vector2(t2Scale), null, t2Rot);

                if (_isPlinkingIn && _plinkTitle2.FlashTint.HasValue)
                {
                    Rectangle bounds = new Rectangle((int)pos2.X, (int)pos2.Y, (int)size2.X, (int)size2.Y);
                    spriteBatch.DrawSnapped(_spriteManager.EmptySprite, bounds, _plinkTitle2.FlashTint.Value);
                }
            }

            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
            if (_characterIds.Count > 0)
            {
                DrawCarousel(spriteBatch, font, tertiaryFont);
            }
            spriteBatch.End();

            float leftY = 0f;
            Color leftColor = (_leftArrow.IsHovered || _leftArrow.IsSelected) ? _global.ButtonHoverColor : _global.GameTextColor;

            if (_leftArrowSimTimer > 0f)
            {
                float progress = 1f - (_leftArrowSimTimer / ARROW_SIM_DURATION);
                if (progress < 0.5f) { leftY = -1f; leftColor = _global.ButtonHoverColor; }
                else { leftY = 1f; leftColor = _global.Palette_Fruit; }
            }
            else if (_leftArrow.IsHovered && _inputManager.GetEffectiveMouseState().LeftButton == ButtonState.Pressed)
            {
                leftY = 1f;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateTranslation(0, leftY, 0) * staticTransform);
            _leftArrow.Draw(spriteBatch, font, effectiveGameTime, Matrix.Identity, false, null, null, leftColor);
            spriteBatch.End();

            float rightY = 0f;
            Color rightColor = (_rightArrow.IsHovered || _rightArrow.IsSelected) ? _global.ButtonHoverColor : _global.GameTextColor;

            if (_rightArrowSimTimer > 0f)
            {
                float progress = 1f - (_rightArrowSimTimer / ARROW_SIM_DURATION);
                if (progress < 0.5f) { rightY = -1f; rightColor = _global.ButtonHoverColor; }
                else { rightY = 1f; rightColor = _global.Palette_Fruit; }
            }
            else if (_rightArrow.IsHovered && _inputManager.GetEffectiveMouseState().LeftButton == ButtonState.Pressed)
            {
                rightY = 1f;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateTranslation(0, rightY, 0) * staticTransform);
            _rightArrow.Draw(spriteBatch, font, effectiveGameTime, Matrix.Identity, false, null, null, rightColor);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, staticTransform);

            var pixel = ServiceLocator.Get<Texture2D>();
            DrawModeButtonBackground(spriteBatch, _btnMode1v1, 2, pixel);
            DrawModeButtonBackground(spriteBatch, _btnMode4ffa, 4, pixel);
            DrawModeButtonBackground(spriteBatch, _btnMode6ffa, 6, pixel);
            DrawModeButtonBackground(spriteBatch, _btnMode8ffa, 8, pixel);

            _btnMode1v1.Draw(spriteBatch, secondaryFont, effectiveGameTime, Matrix.Identity);
            _btnMode4ffa.Draw(spriteBatch, secondaryFont, effectiveGameTime, Matrix.Identity);
            _btnMode6ffa.Draw(spriteBatch, secondaryFont, effectiveGameTime, Matrix.Identity);
            _btnMode8ffa.Draw(spriteBatch, secondaryFont, effectiveGameTime, Matrix.Identity);

            Color startColor = (_startButton.IsHovered || _startButton.IsSelected) ? _global.ButtonHoverColor : _global.GameTextColor;
            _startButton.Draw(spriteBatch, core.DefaultFont, effectiveGameTime, Matrix.Identity, false, null, null, startColor);

            spriteBatch.End();

            float sScale = _isPlinkingIn ? _plinkStats.Scale : 1f;
            float sRot = _isPlinkingIn ? _plinkStats.Rotation : 0f;

            if (sScale > 0.01f)
            {
                Vector2 statsCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, BASE_CENTER_Y + 70);
                Matrix statsMatrix = Matrix.CreateTranslation(-statsCenter.X, -statsCenter.Y, 0) *
                                     Matrix.CreateScale(sScale) *
                                     Matrix.CreateRotationZ(sRot) *
                                     Matrix.CreateTranslation(statsCenter.X, statsCenter.Y, 0);

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, statsMatrix * staticTransform);
                DrawStats(spriteBatch, secondaryFont, tertiaryFont);

                if (_isPlinkingIn && _plinkStats.FlashTint.HasValue)
                {
                    Rectangle flashRect = new Rectangle((int)statsCenter.X - 70, (int)statsCenter.Y - 30, 140, 60);
                    spriteBatch.DrawSnapped(_spriteManager.EmptySprite, flashRect, _plinkStats.FlashTint.Value);
                }

                spriteBatch.End();
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
        }

        private Color GetStatColor(int value)
        {
            if (value >= 8) return _global.StatColor_High;
            if (value >= 4) return _global.StatColor_Average;
            return _global.StatColor_Low;
        }

        private void DrawStats(SpriteBatch spriteBatch, BitmapFont secondaryFont, BitmapFont tertiaryFont)
        {
            if (_characterIds.Count == 0) return;
            int count = _characterIds.Count;
            int centerIndex = ((int)MathF.Floor(_virtualIndex + 0.5f) % count + count) % count;
            string charId = _characterIds[centerIndex];
            if (!GameDataCache.WizardCats.TryGetValue(charId, out var data)) return;

            int centerX = Global.VIRTUAL_WIDTH / 2;
            int startY = BASE_CENTER_Y + 38;
            int currentY = startY;

            var heartSheet = _spriteManager.HealthHeartsSpriteSheet;
            if (heartSheet != null)
            {
                int hpStat = data.HP;
                int heartWidth = 5;
                int heartSpacing = 1;
                int totalHeartsWidth = hpStat * heartWidth + (hpStat - 1) * heartSpacing;
                int heartsStartX = centerX - (totalHeartsWidth / 2);

                for (int i = 0; i < hpStat; i++)
                {
                    int yOffset = 0;
                    float localWaveTime = _introHeartWaveTimer - _introHeartWaveInterval - (i * 0.08f);
                    if (localWaveTime > 0 && localWaveTime < 0.15f)
                    {
                        yOffset = -1;
                    }

                    var sourceRect = new Rectangle(0, 0, heartWidth, 5);
                    spriteBatch.DrawSnapped(heartSheet, new Vector2(heartsStartX + i * (heartWidth + heartSpacing), currentY + yOffset), sourceRect, Color.White);
                }
                currentY += 5 + 1;
            }

            string[] labels = { "POW", "TEN", "AGI" };
            int[] values = { data.Power, data.Tenacity, data.Agility };

            int statBlockX = centerX - 30;
            float standardLabelWidth = secondaryFont.MeasureString("POW").Width;
            float areaStartX = statBlockX + standardLabelWidth + 4;

            for (int i = 0; i < labels.Length; i++)
            {
                float labelWidth = secondaryFont.MeasureString(labels[i]).Width;
                float labelX = MathF.Round(statBlockX + (standardLabelWidth - labelWidth));

                spriteBatch.DrawStringSnapped(secondaryFont, labels[i], new Vector2(labelX, currentY), _global.Palette_DarkestPale);

                int val = values[i];
                string textVal;
                Color textColor;

                if (val <= 1)
                {
                    textVal = "VERY LOW";
                    textColor = _global.Palette_Rust;
                }
                else if (val <= 3)
                {
                    textVal = "LOW";
                    textColor = _global.Palette_DarkestPale;
                }
                else if (val <= 5)
                {
                    textVal = "AVERAGE";
                    textColor = _global.Palette_DarkPale;
                }
                else if (val <= 7)
                {
                    textVal = "AVERAGE";
                    textColor = _global.Palette_DarkPale;
                }
                else if (val <= 9)
                {
                    textVal = "HIGH";
                    textColor = _global.Palette_Pale;
                }
                else
                {
                    textVal = "VERY HIGH";
                    textColor = _global.Palette_Leaf;
                }

                float textWidth = tertiaryFont.MeasureString(textVal).Width;
                float textX = MathF.Round(areaStartX + (40f - textWidth) / 2f);
                float textY = currentY + MathF.Ceiling((secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f);

                spriteBatch.DrawStringSnapped(tertiaryFont, textVal, new Vector2(textX, textY), textColor);

                currentY += secondaryFont.LineHeight + 1;
            }

            currentY += 4;
        }

        private void DrawCarousel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont tertiaryFont)
        {
            int centerX = Global.VIRTUAL_WIDTH / 2;
            int centerY = BASE_CENTER_Y;
            var sheet = _spriteManager.PlayerMasterSpriteSheet;
            var silhouette = _spriteManager.PlayerMasterSpriteSheetSilhouette;
            int count = _characterIds.Count;

            int[] drawOrder = { -3, 3, -2, 2, -1, 1, 0 };

            const float SPREAD_FACTOR = 0.5f;
            const float RADIUS = 100f;

            int centerIndex = ((int)MathF.Floor(_virtualIndex + 0.5f) % count + count) % count;
            float carouselSlideOffset = MathF.Floor(_virtualIndex + 0.5f) - _virtualIndex;

            foreach (int offset in drawOrder)
            {
                int plinkIndex = offset + 3;
                var plink = _plinkCarousel[plinkIndex];

                float pScale = _isPlinkingIn ? plink.Scale : 1f;
                float pRot = _isPlinkingIn ? plink.Rotation : 0f;

                if (_isPlinkingIn && pScale < 0.01f) continue;

                int charIndex = (centerIndex + offset) % count;
                if (charIndex < 0) charIndex += count;

                string charId = _characterIds[charIndex];
                bool isCenter = (offset == 0);

                float finalOpacity = isCenter ? 1.0f : 0.6f;
                if (Math.Abs(offset) >= 2) finalOpacity = 0.3f;

                float visualOffset = offset + carouselSlideOffset;

                float xOffset = MathF.Sin(visualOffset * SPREAD_FACTOR) * RADIUS;
                float xPos = centerX + xOffset;

                int spriteIndex = int.Parse(charId);

                PlayerSpriteType spriteType;
                if (Math.Abs(offset) >= 3) spriteType = PlayerSpriteType.Portrait5x5;
                else if (Math.Abs(offset) >= 1) spriteType = PlayerSpriteType.Portrait8x8;
                else spriteType = PlayerSpriteType.Normal;

                float curveY = MathF.Pow(Math.Abs(visualOffset), 1.5f) * 2.0f;
                float baseYPos = centerY - curveY;
                float headYPos = baseYPos;

                if (isCenter)
                {
                    float bob = MathF.Sin(_idleTimer * 4f);
                    headYPos += (bob > 0 ? -1f : 0f);
                    if (bob > 0) spriteType = PlayerSpriteType.Alt;
                }

                Vector2 origin = new Vector2(16, 16);
                Vector2 bodyPosition = new Vector2(MathF.Round(xPos), MathF.Round(baseYPos));
                Vector2 headPosition = new Vector2(MathF.Round(xPos), MathF.Round(headYPos));

                float selectionScaleMult = 1f;
                float selectionRotOffset = 0f;
                float selectionFlashAlpha = 0f;

                if (_selectedWizards.TryGetValue(charId, out var selData))
                {
                    float selTimer = selData.Timer;
                    float targetRot = selData.TargetRotation;

                    float flashCycle = selTimer % 1.5f;
                    float flashIn = 0.03f;
                    float flashOut = 0.3f;
                    if (flashCycle < flashIn)
                        selectionFlashAlpha = Easing.EaseOutCubic(flashCycle / flashIn) * 0.8f;
                    else if (flashCycle < flashIn + flashOut)
                        selectionFlashAlpha = (1f - Easing.EaseOutCubic((flashCycle - flashIn) / flashOut)) * 0.8f;

                    float popDuration = 0.15f;
                    if (selTimer < popDuration)
                    {
                        float p = selTimer / popDuration;
                        if (p < 0.2f)
                        {
                            float inP = p / 0.2f;
                            selectionScaleMult = MathHelper.Lerp(1f, 1.4f, Easing.EaseOutCubic(inP));
                            selectionRotOffset = MathHelper.Lerp(0f, targetRot, Easing.EaseOutCubic(inP));
                        }
                        else
                        {
                            float outP = (p - 0.2f) / 0.8f;
                            selectionScaleMult = MathHelper.Lerp(1.4f, 1f, Easing.EaseOutBack(outP));
                            selectionRotOffset = MathHelper.Lerp(targetRot, 0f, Easing.EaseOutBack(outP));
                        }
                    }
                }

                pScale *= selectionScaleMult;
                pRot += selectionRotOffset;

                if (Math.Abs(offset) < 1)
                {
                    PlayerSpriteType bodyType = (spriteType == PlayerSpriteType.Alt) ? PlayerSpriteType.BodyAlt : PlayerSpriteType.BodyNormal;
                    var bodySourceRect = _spriteManager.GetPlayerSourceRect(spriteIndex, bodyType);

                    spriteBatch.Draw(sheet, bodyPosition, bodySourceRect, Color.White * finalOpacity, pRot, origin, pScale, SpriteEffects.None, 0f);

                    if (_isPlinkingIn && plink.FlashTint.HasValue && silhouette != null)
                    {
                        spriteBatch.Draw(silhouette, bodyPosition, bodySourceRect, plink.FlashTint.Value, pRot, origin, pScale, SpriteEffects.None, 0f);
                    }

                    if (selectionFlashAlpha > 0f && silhouette != null)
                    {
                        spriteBatch.Draw(silhouette, bodyPosition, bodySourceRect, Color.White * selectionFlashAlpha * finalOpacity, pRot, origin, pScale, SpriteEffects.None, 0f);
                    }
                }

                var sourceRect = _spriteManager.GetPlayerSourceRect(spriteIndex, spriteType);
                spriteBatch.Draw(sheet, headPosition, sourceRect, Color.White * finalOpacity, pRot, origin, pScale, SpriteEffects.None, 0f);

                if (_isPlinkingIn && plink.FlashTint.HasValue && silhouette != null)
                {
                    spriteBatch.Draw(silhouette, headPosition, sourceRect, plink.FlashTint.Value, pRot, origin, pScale, SpriteEffects.None, 0f);
                }

                if (selectionFlashAlpha > 0f && silhouette != null)
                {
                    spriteBatch.Draw(silhouette, headPosition, sourceRect, Color.White * selectionFlashAlpha * finalOpacity, pRot, origin, pScale, SpriteEffects.None, 0f);
                }

                if (isCenter && GameDataCache.WizardCats.TryGetValue(charId, out var data))
                {
                    string name = data.Name.ToUpper();
                    Vector2 nameSize = font.MeasureString(name);
                    Vector2 namePos = new Vector2(MathF.Round(centerX - nameSize.X / 2f), MathF.Round(centerY + 26));
                    TextAnimator.DrawTextWithEffect(spriteBatch, font, name, namePos, _global.Palette_LightPale, TextEffectType.None, 0f, new Vector2(pScale), null, pRot);

                    string numberText = (spriteIndex + 1).ToString();
                    Vector2 numSize = tertiaryFont.MeasureString(numberText);
                    Vector2 numPos = new Vector2(MathF.Round(centerX - numSize.X / 2f), MathF.Round(centerY + 20));
                    TextAnimator.DrawTextWithEffect(spriteBatch, tertiaryFont, numberText, numPos, _global.Palette_LightPale, TextEffectType.None, 0f, new Vector2(pScale), null, pRot);
                }
            }
        }
    }
}
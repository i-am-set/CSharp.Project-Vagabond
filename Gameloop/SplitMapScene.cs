using BepuPhysics.Trees;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class SplitMapScene : GameScene
    {
        private struct DrawableMapObject
        {
            public enum ObjectType { Node, Player }
            public ObjectType Type;
            public Vector2 Position;
            public object? Data;
        }
        private readonly ProgressionManager _progressionManager;
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly VoidEdgeEffect _voidEdgeEffect;
        private readonly SplitMapHudRenderer _hudRenderer;

        private readonly BirdManager _birdManager;
        private readonly TransitionManager _transitionManager;
        private readonly HapticsManager _hapticsManager;
        private readonly ParticleSystemManager _particleSystemManager;

        private SplitMap? _currentMap;
        private int _playerCurrentNodeId;
        private int _cameraFocusNodeId;
        private readonly PlayerMapIcon _playerIcon;

        private const float CAMERA_LERP_SPEED = 15f;
        private const float POST_EVENT_DELAY = 0.0f;
        private const float PATH_ANIMATION_DURATION = 0.6f;

        private bool _isWalking = false;
        private int _currentMoveStep = 0;
        private float _walkStepTimer = 0f;

        private const int TOTAL_MOVE_STEPS = 8;
        private const float STEP_DURATION = 0.3f;
        private const float JUMP_HEIGHT = 8f;
        private const float STEP_ROTATION = 20f;

        private float _abandonedFadeAmount = 0.5f;
        private int _clickedNodeId = -1;
        private float _clickAnimTimer = 0f;

        private int _clickRotationDir = 1;

        private const float CLICK_ANIM_DURATION = 0.1f;
        private const float CLICK_SCALE_MAX = 1.35f;

        private const float POP_ROTATION_DEG = 15f;

        private const float NODE_LIFT_DURATION = 0.15f;
        private const float PULSE_DURATION = 1.0f;
        private const float NODE_LOWERING_DURATION = 0.75f;
        private const float NODE_LIFT_AMOUNT = 8f;
        private const float ARRIVAL_SCALE_MAX = 1.5f;

        private Vector2 _cameraOffset;
        private Vector2 _targetCameraOffset;

        private float _hudSlideOffset = 0f;
        private const float HUD_SLIDE_DISTANCE = 24f;
        private const float HUD_SLIDE_SPEED = 10f;
        private const float BASE_CAMERA_Y = -50f;

        private int _playerMoveTargetNodeId;
        private SplitMapPath? _playerMovePath;
        private float _playerPathTotalLength = 0f;

        private int _hoveredNodeId = -1;
        private int _lastHoveredNodeId = -1;
        private int _pressedNodeId = -1;

        private readonly HashSet<int> _visitedNodeIds = new HashSet<int>();
        private readonly HashSet<int> _traversedPathIds = new HashSet<int>();
        private int _nodeForPathReveal = -1;

        private readonly Dictionary<int, float> _pathAnimationProgress = new();
        private readonly Dictionary<int, float> _pathAnimationDurations = new();
        private static readonly Random _random = new Random();

        private const float PATH_EXCLUSION_RADIUS = 10f;

        private float _pulseTimer = 0f;

        private bool _wasModalActiveLastFrame = false;

        private float _postEventDelayTimer = 0f;

        private enum SplitMapState { Idle, LiftingNode, PulsingNode, EventInProgress, LoweringNode, Resting, Recruiting, PostEventDelay }
        private SplitMapState _mapState = SplitMapState.Idle;

        private const float NODE_FRAME_DURATION = 0.5f;
        private float _nodeLiftTimer = 0f;

        private List<string>? _pendingCombatArchetypes;
        private bool _waitingForCombatCameraSettle = false;
        private int _framesToWaitAfterSettle = 0;
        private const float CAMERA_SETTLE_THRESHOLD = 0.5f;

        public static bool PlayerWonLastBattle { get; set; } = true;
        public static bool WasMajorBattle { get; set; } = false;

        private ImageButton? _settingsButton;
        private enum SettingsButtonState { Hidden, AnimatingIn, Visible }
        private SettingsButtonState _settingsButtonState = SettingsButtonState.Hidden;
        private float _settingsButtonAnimTimer = 0f;
        private const float SETTINGS_BUTTON_ANIM_DURATION = 0.6f;

        private float _nodeTextWaveTimer = 0f;
        private static readonly RasterizerState _scissorRasterizerState = new RasterizerState { ScissorTestEnable = true };

        private Vector2 _nodeArrivalScale = Vector2.One;
        private Vector2 _nodeArrivalShake = Vector2.Zero;
        private float _nodeArrivalRotation = 0f;
        private int _arrivalRotationDir = 1;

        private int _selectedNodeId = -1;

        // --- Rest Event State ---
        private bool _isRestEventActive = false;
        private bool _isRestEventFadingOut = false;
        private float _restEventFadeTimer = 0f;
        private const float REST_EVENT_FADE_DURATION = 0.3f;
        private float _restHealPercentage = 1.0f; // 1.0 = 100% heal

        private PlinkAnimator _restPromptPlink = new PlinkAnimator();
        private Button _restYesButton;
        private Button _restNoButton;
        private NavigationGroup _restNavGroup = new NavigationGroup(wrapNavigation: true);

        // --- Recruit Event State ---
        private bool _isRecruitEventActive = false;
        private bool _isRecruitEventFadingOut = false;
        private float _recruitEventFadeTimer = 0f;
        private const float RECRUIT_EVENT_FADE_DURATION = 0.3f;
        private List<PartyMemberData> _recruitCandidates = new List<PartyMemberData>();
        private int _selectedRecruitIndex = -1;
        private PlinkAnimator _recruitPromptPlink = new PlinkAnimator();
        private Button _recruitActionButton;
        private Button _replaceButton;
        private PlinkAnimator _partyFullPlink = new PlinkAnimator();
        private bool _wasPartyFullTextVisible = false;
        private NavigationGroup _recruitNavGroup = new NavigationGroup(wrapNavigation: true);
        private Rectangle[] _recruitHitboxes = new Rectangle[2];

        public SplitMapScene()
        {
            _progressionManager = ServiceLocator.Get<ProgressionManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _playerIcon = new PlayerMapIcon();

            _hudRenderer = new SplitMapHudRenderer();

            _birdManager = new BirdManager();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();

            _voidEdgeEffect = new VoidEdgeEffect(
                edgeColor: _global.Palette_Black,
                edgeWidth: 6,
                noiseScale: 0.1f,
                noiseSpeed: 3f
            );
        }

        public override Rectangle GetAnimatedBounds() => new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

        public override void Enter()
        {
            base.Enter();
            _playerIcon.SetIsMoving(false);
            _playerIcon.Rotation = 0f;
            _waitingForCombatCameraSettle = false;
            _pendingCombatArchetypes = null;
            _nodeTextWaveTimer = 0f;

            _nodeArrivalScale = Vector2.One;
            _nodeArrivalRotation = 0f;
            _nodeArrivalShake = Vector2.Zero;

            _selectedNodeId = -1;
            _lastHoveredNodeId = -1;
            _pressedNodeId = -1;
            _hudSlideOffset = 0f;

            _isWalking = false;
            _currentMoveStep = 0;
            _walkStepTimer = 0f;
            _clickedNodeId = -1;
            _clickAnimTimer = 0f;
            _clickRotationDir = 1;
            _playerMoveTargetNodeId = -1;
            _playerMovePath = null;

            InitializeSettingsButton();
            InitializeRestUI();
            InitializeRecruitUI();

            _settingsButtonState = SettingsButtonState.AnimatingIn;
            _settingsButtonAnimTimer = 0f;

            _isRecruitEventActive = false;
            _hudRenderer.IsReplacementMode = false;
            _hudRenderer.SelectedForReplacement = null;

            if (_progressionManager.CurrentSplitMap == null)
            {
                _mapState = SplitMapState.Idle;
                _hoveredNodeId = -1;
                _postEventDelayTimer = 0f;
                _nodeLiftTimer = 0f;
                _pulseTimer = 0f;

                _progressionManager.GenerateNewSplitMap();
                _currentMap = _progressionManager.CurrentSplitMap;
                _playerCurrentNodeId = _currentMap?.StartNodeId ?? -1;
                _cameraFocusNodeId = _playerCurrentNodeId;
                _nodeForPathReveal = _playerCurrentNodeId;
                _pathAnimationProgress.Clear();
                _pathAnimationDurations.Clear();
                _visitedNodeIds.Clear();
                _traversedPathIds.Clear();
                _visitedNodeIds.Add(_playerCurrentNodeId);

                var startNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (startNode != null)
                {
                    _playerIcon.SetPosition(startNode.Position);
                    UpdateCameraTarget(startNode.Position, true);
                    UpdateReachableNodes();
                    StartPathRevealAnimation();
                    if (_currentMap != null) _birdManager.Initialize(_currentMap, _playerIcon.Position, _cameraOffset);
                }
            }
            else
            {
                _currentMap = _progressionManager.CurrentSplitMap;
                _cameraFocusNodeId = _playerCurrentNodeId;
                var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (currentNode != null)
                {
                    currentNode.IsCompleted = true;
                    _playerIcon.SetPosition(currentNode.Position);
                    UpdateCameraTarget(currentNode.Position, false);
                }
                _mapState = SplitMapState.LoweringNode;
                _nodeLiftTimer = 0f;
                if (_currentMap != null) _birdManager.Initialize(_currentMap, _playerIcon.Position, _cameraOffset);
            }
        }

        public override void Exit()
        {
            base.Exit();
            if (BattleSetup.ReturnSceneState != GameSceneState.Split)
            {
                _progressionManager.ClearCurrentSplitMap();
            }
        }

        private void InitializeSettingsButton()
        {
            int buttonSize = 16;
            int offScreenX = Global.VIRTUAL_WIDTH + 20;

            if (_settingsButton == null)
            {
                var sheet = _spriteManager.SplitMapSettingsButton;
                var rects = _spriteManager.SplitMapSettingsButtonSourceRects;
                _settingsButton = new ImageButton(new Rectangle(offScreenX, 2, buttonSize, buttonSize), sheet, rects[0], rects[1], enableHoverSway: true)
                {
                    UseScreenCoordinates = false,
                    TriggerHapticOnHover = true
                };
            }
            _settingsButton.Bounds = new Rectangle(offScreenX, 2, buttonSize, buttonSize);
            _settingsButton.OnClick = null;
            _settingsButton.OnClick += () =>
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                OpenSettings();
            };
            _settingsButton.ResetAnimationState();
        }

        private void InitializeRestUI()
        {
            if (_restYesButton == null)
            {
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                _restYesButton = new Button(Rectangle.Empty, "YES", font: secondaryFont)
                {
                    CustomDefaultTextColor = _global.Palette_Sun,
                    CustomHoverTextColor = _global.ButtonHoverColor,
                    HoverAnimation = HoverAnimationType.Hop
                };
                _restNoButton = new Button(Rectangle.Empty, "NO", font: secondaryFont)
                {
                    CustomDefaultTextColor = _global.Palette_Sun,
                    CustomHoverTextColor = _global.ButtonHoverColor,
                    HoverAnimation = HoverAnimationType.Hop
                };

                _restYesButton.OnClick += () => ResolveRestEvent(true);
                _restNoButton.OnClick += () => ResolveRestEvent(false);

                _restNavGroup.Add(_restYesButton);
                _restNavGroup.Add(_restNoButton);
            }
            _isRestEventActive = false;
        }

        private void InitializeRecruitUI()
        {
            if (_recruitActionButton == null)
            {
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                _recruitActionButton = new Button(Rectangle.Empty, "SKIP", font: secondaryFont)
                {
                    CustomDefaultTextColor = _global.Palette_Sun,
                    CustomHoverTextColor = _global.ButtonHoverColor,
                    HoverAnimation = HoverAnimationType.Hop
                };
                _recruitActionButton.OnClick += () => {
                    if (_selectedRecruitIndex != -1 && _gameState.PlayerState.Party.Count < 4)
                    {
                        ResolveRecruitEvent(_recruitCandidates[_selectedRecruitIndex]);
                    }
                    else
                    {
                        ResolveRecruitEvent(null);
                    }
                };
                _recruitNavGroup.Add(_recruitActionButton);

                _replaceButton = new Button(Rectangle.Empty, "REPLACE", font: secondaryFont)
                {
                    CustomDefaultTextColor = _global.Palette_Sun,
                    CustomHoverTextColor = _global.ButtonHoverColor,
                    HoverAnimation = HoverAnimationType.Hop
                };
                _replaceButton.OnClick += () => {
                    if (_selectedRecruitIndex != -1 && _hudRenderer.SelectedForReplacement != null)
                    {
                        _gameState.PlayerState.Party.Remove(_hudRenderer.SelectedForReplacement);
                        ResolveRecruitEvent(_recruitCandidates[_selectedRecruitIndex]);
                    }
                };
                _recruitNavGroup.Add(_replaceButton);
            }
        }

        private void UpdateReachableNodes()
        {
            if (_currentMap == null || !_currentMap.Nodes.TryGetValue(_playerCurrentNodeId, out var currentNode)) return;
            var reachableNodeIds = new HashSet<int>();
            foreach (var pathId in currentNode.OutgoingPathIds)
            {
                if (_currentMap.Paths.TryGetValue(pathId, out var path)) reachableNodeIds.Add(path.ToNodeId);
            }
            foreach (var (nodeId, node) in _currentMap.Nodes) node.IsReachable = reachableNodeIds.Contains(nodeId);
        }

        public override void Update(GameTime gameTime)
        {
            if (_inputBlockTimer > 0) _inputBlockTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (IsInputBlocked) { base.Update(gameTime); return; }

            var inputManager = ServiceLocator.Get<InputManager>();
            GameTime effectiveGameTime = inputManager.GetEffectiveGameTime(gameTime, _isWalking);
            float deltaTime = (float)effectiveGameTime.ElapsedGameTime.TotalSeconds;

            if (_currentMap != null)
            {
                float fadeSpeed = 3f * deltaTime;
                foreach (var node in _currentMap.Nodes.Values)
                {
                    if (node.IsAbandoned)
                        node.VisualAlpha = MathHelper.Lerp(node.VisualAlpha, _abandonedFadeAmount, fadeSpeed);
                }
                foreach (var path in _currentMap.Paths.Values)
                {
                    if (path.IsAbandoned)
                        path.VisualAlpha = MathHelper.Lerp(path.VisualAlpha, _abandonedFadeAmount, fadeSpeed);
                }
            }

            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            float splitLineY = MathF.Round((Global.VIRTUAL_HEIGHT - SplitMapHudRenderer.HUD_HEIGHT) + _hudSlideOffset);
            bool mouseInMap = virtualMousePos.Y < splitLineY;

            if (mouseInMap && !_hudRenderer.IsDragging)
            {
                _hudRenderer.ResetAllFlips();
            }

            float targetHudOffset = (mouseInMap && !_hudRenderer.IsDragging) ? HUD_SLIDE_DISTANCE : 0f;
            _hudSlideOffset = MathHelper.Lerp(_hudSlideOffset, targetHudOffset, deltaTime * HUD_SLIDE_SPEED);

            _hudRenderer.Update(effectiveGameTime, virtualMousePos, _hudSlideOffset);

            _voidEdgeEffect.Update(effectiveGameTime, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _cameraOffset);
            _birdManager.Update(effectiveGameTime, _currentMap, _playerIcon.Position, _cameraOffset);

            if (_wasModalActiveLastFrame)
            {
                _wasModalActiveLastFrame = false;
                var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (currentNode != null)
                {
                    currentNode.IsCompleted = true;
                    UpdateCameraTarget(currentNode.Position, false);
                }
                _mapState = SplitMapState.LoweringNode;
                _nodeLiftTimer = 0f;
            }

            if (_settingsButton != null)
            {
                if (_settingsButtonState == SettingsButtonState.AnimatingIn)
                {
                    _settingsButtonAnimTimer += deltaTime;
                    float progress = Math.Clamp(_settingsButtonAnimTimer / SETTINGS_BUTTON_ANIM_DURATION, 0f, 1f);
                    float eased = Easing.EaseOutBack(progress);
                    float startX = Global.VIRTUAL_WIDTH + 20;
                    float targetX = Global.VIRTUAL_WIDTH - 16 - 2;
                    float currentX = MathHelper.Lerp(startX, targetX, eased);
                    _settingsButton.Bounds = new Rectangle((int)currentX, 2, 16, 16);
                    if (progress >= 1.0f) _settingsButtonState = SettingsButtonState.Visible;
                }
                else if (_settingsButtonState == SettingsButtonState.Visible)
                {
                    int buttonSize = 16;
                    int padding = 2;
                    int buttonX = Global.VIRTUAL_WIDTH - buttonSize - padding;
                    int buttonY = padding;
                    _settingsButton.Bounds = new Rectangle(buttonX, buttonY, buttonSize, buttonSize);
                }
                if (_settingsButtonState != SettingsButtonState.Hidden) _settingsButton.Update(currentMouseState);
            }

            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState)) OpenSettings();

            float cameraDamping = 1.0f - MathF.Exp(-CAMERA_LERP_SPEED * deltaTime);

            if (_currentMap != null && _currentMap.Nodes.TryGetValue(_cameraFocusNodeId, out var pNode))
            {
                UpdateCameraTarget(pNode.Position, false);
            }

            _cameraOffset = Vector2.Lerp(_cameraOffset, _targetCameraOffset, cameraDamping);

            if (_waitingForCombatCameraSettle)
            {
                if (Vector2.Distance(_cameraOffset, _targetCameraOffset) < CAMERA_SETTLE_THRESHOLD)
                {
                    if (_framesToWaitAfterSettle > 0) _framesToWaitAfterSettle--;
                    else ExecuteCombatTransition();
                }
            }

            var appearingKeys = _pathAnimationProgress.Keys.ToList();
            foreach (var pathId in appearingKeys)
            {
                float duration = _pathAnimationDurations.GetValueOrDefault(pathId, PATH_ANIMATION_DURATION);
                if (_pathAnimationProgress[pathId] < duration) _pathAnimationProgress[pathId] += deltaTime;
            }

            if (_clickAnimTimer > 0f)
            {
                _clickAnimTimer -= deltaTime;
                if (_clickAnimTimer < 0f) _clickAnimTimer = 0f;
            }

            if (_isWalking && _playerMovePath != null)
            {
                _playerIcon.SetIsMoving(true);
                _walkStepTimer += deltaTime;

                float stepProgress = Math.Clamp(_walkStepTimer / STEP_DURATION, 0f, 1f);

                float startRatio = (float)_currentMoveStep / TOTAL_MOVE_STEPS;
                float endRatio = (float)(_currentMoveStep + 1) / TOTAL_MOVE_STEPS;

                float moveT = Easing.EaseOutCubic(stepProgress);
                float globalT = MathHelper.Lerp(startRatio, endRatio, moveT);

                Vector2 pathPos = GetPointOnPath(globalT);

                float jumpY = MathF.Sin(stepProgress * MathHelper.Pi) * -JUMP_HEIGHT;
                _playerIcon.SetPosition(pathPos + new Vector2(0, jumpY));

                float rotationDir = (_currentMoveStep % 2 == 0) ? -1f : 1f;
                float targetRotation = MathHelper.ToRadians(STEP_ROTATION) * rotationDir;

                _playerIcon.Rotation = MathF.Sin(stepProgress * MathHelper.Pi) * targetRotation;

                if (_walkStepTimer >= STEP_DURATION)
                {
                    _walkStepTimer = 0f;
                    _currentMoveStep++;

                    if (_currentMoveStep >= TOTAL_MOVE_STEPS)
                    {
                        _isWalking = false;
                        _playerIcon.Rotation = 0f;
                        _playerIcon.SetIsMoving(false);
                        FinalizeArrival();
                    }
                }
            }

            if (_mapState == SplitMapState.Resting)
            {
                UpdateRestEvent(deltaTime, currentMouseState, effectiveGameTime);
            }
            else if (_mapState == SplitMapState.Recruiting)
            {
                UpdateRecruitEvent(deltaTime, currentMouseState, effectiveGameTime);
            }
            else
            {
                if (!_isWalking)
                {
                    HandleMapInput(effectiveGameTime);
                }

                switch (_mapState)
                {
                    case SplitMapState.LiftingNode: UpdateLiftingNode(deltaTime); break;
                    case SplitMapState.PulsingNode: UpdatePulsingNode(deltaTime); break;
                    case SplitMapState.LoweringNode: UpdateLoweringNode(deltaTime); break;
                    case SplitMapState.PostEventDelay:
                        _postEventDelayTimer -= deltaTime;
                        if (_postEventDelayTimer <= 0)
                        {
                            _mapState = SplitMapState.Idle;
                            UpdateReachableNodes();
                            StartPathRevealAnimation();
                        }
                        break;
                }
            }

            _playerIcon.Update(effectiveGameTime);
            base.Update(effectiveGameTime);
        }

        private void UpdateRestEvent(float dt, MouseState mouseState, GameTime gameTime)
        {
            if (_isRestEventFadingOut)
            {
                _restEventFadeTimer -= dt;
                if (_restEventFadeTimer <= 0)
                {
                    _isRestEventActive = false;
                    var node = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (node != null)
                    {
                        node.IsCompleted = true;
                        UpdateCameraTarget(node.Position, false);
                    }
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                }
                return;
            }

            _restEventFadeTimer += dt;
            if (_restEventFadeTimer > REST_EVENT_FADE_DURATION)
                _restEventFadeTimer = REST_EVENT_FADE_DURATION;

            float mapViewHeight = MathF.Round((Global.VIRTUAL_HEIGHT - SplitMapHudRenderer.HUD_HEIGHT) + _hudSlideOffset);
            Vector2 promptCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, mapViewHeight / 2f - 15);
            _restPromptPlink.Update(gameTime, promptCenter);

            int btnY = (int)(mapViewHeight / 2f + 10);
            int centerX = Global.VIRTUAL_WIDTH / 2;
            _restYesButton.Bounds = new Rectangle(centerX - 40, btnY, 30, 15);
            _restNoButton.Bounds = new Rectangle(centerX + 10, btnY, 30, 15);

            _restYesButton.Update(mouseState);
            _restNoButton.Update(mouseState);

            var inputManager = ServiceLocator.Get<InputManager>();
            if (inputManager.CurrentInputDevice == InputDeviceType.Mouse)
            {
                _restNavGroup.DeselectAll();
            }
            else
            {
                _restNavGroup.UpdateInput(inputManager);
            }
        }

        private void ResolveRestEvent(bool heal)
        {
            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
            if (heal && _gameState.PlayerState != null)
            {
                foreach (var member in _gameState.PlayerState.Party)
                {
                    int healAmount = (int)(member.MaxHP * _restHealPercentage);
                    member.CurrentHP = Math.Min(member.MaxHP, member.CurrentHP + healAmount);
                }
                ServiceLocator.Get<Core>().TriggerFullscreenFlash(_global.Palette_Leaf, 0.2f);
            }
            _isRestEventFadingOut = true;
        }

        private void UpdateRecruitEvent(float dt, MouseState mouseState, GameTime gameTime)
        {
            if (_isRecruitEventFadingOut)
            {
                _recruitEventFadeTimer -= dt;
                if (_recruitEventFadeTimer <= 0)
                {
                    _isRecruitEventActive = false;
                    _hudRenderer.IsReplacementMode = false;
                    var node = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (node != null)
                    {
                        node.IsCompleted = true;
                        UpdateCameraTarget(node.Position, false);
                    }
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                }
                return;
            }

            _recruitEventFadeTimer += dt;
            if (_recruitEventFadeTimer > RECRUIT_EVENT_FADE_DURATION)
                _recruitEventFadeTimer = RECRUIT_EVENT_FADE_DURATION;

            float mapViewHeight = MathF.Round((Global.VIRTUAL_HEIGHT - SplitMapHudRenderer.HUD_HEIGHT) + _hudSlideOffset);
            Vector2 promptCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, mapViewHeight / 2f - 29);
            _recruitPromptPlink.Update(gameTime, promptCenter);

            int btnY = (int)(mapViewHeight / 2f + 25);
            int startY = (int)(mapViewHeight / 2f - 2);
            int centerX = Global.VIRTUAL_WIDTH / 2;

            // Hitbox logic
            var inputManager = ServiceLocator.Get<InputManager>();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            bool clicked = inputManager.IsMouseClickAvailable() && mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;

            for (int i = 0; i < _recruitCandidates.Count; i++)
            {
                int spacing = 60;
                int drawX = centerX + (i == 0 ? -spacing / 2 : spacing / 2);
                _recruitHitboxes[i] = new Rectangle(drawX - 16, startY - 16, 32, 32);

                if (_recruitHitboxes[i].Contains(virtualMousePos))
                {
                    ServiceLocator.Get<CursorManager>().SetState(CursorState.HoverClickable);
                    if (clicked)
                    {
                        inputManager.ConsumeMouseClick();
                        _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

                        if (_selectedRecruitIndex == i)
                        {
                            _selectedRecruitIndex = -1; // Deselect
                            _recruitActionButton.Text = "SKIP";
                        }
                        else
                        {
                            _selectedRecruitIndex = i; // Select
                            if (_gameState.PlayerState.Party.Count < 4)
                            {
                                _recruitActionButton.Text = "SELECT";
                            }
                        }
                    }
                }
            }

            bool isPartyFull = _gameState.PlayerState.Party.Count >= 4;
            bool hasRecruitSelected = _selectedRecruitIndex != -1;
            bool hasReplaceTarget = _hudRenderer.SelectedForReplacement != null;

            // Manage HUD state
            _hudRenderer.IsReplacementMode = isPartyFull && hasRecruitSelected;
            if (!_hudRenderer.IsReplacementMode)
            {
                _hudRenderer.SelectedForReplacement = null;
            }

            // Manage Plinks & Buttons
            bool shouldShowPartyFull = isPartyFull && hasRecruitSelected && !hasReplaceTarget;
            if (shouldShowPartyFull && !_wasPartyFullTextVisible)
            {
                _partyFullPlink.Start(0f);
                _wasPartyFullTextVisible = true;
            }
            else if (!shouldShowPartyFull)
            {
                _wasPartyFullTextVisible = false;
            }

            if (_partyFullPlink.IsActive)
            {
                _partyFullPlink.Update(gameTime, new Vector2(centerX, btnY + 7));
            }

            if (isPartyFull && hasRecruitSelected)
            {
                if (hasReplaceTarget)
                {
                    _replaceButton.Bounds = new Rectangle(centerX - 35, btnY, 70, 15);
                    _replaceButton.Update(mouseState);
                }
            }
            else
            {
                _recruitActionButton.Bounds = new Rectangle(centerX - 25, btnY, 50, 15);
                _recruitActionButton.Update(mouseState);
            }

            if (inputManager.CurrentInputDevice == InputDeviceType.Mouse)
            {
                _recruitNavGroup.DeselectAll();
            }
            else
            {
                _recruitNavGroup.UpdateInput(inputManager);
            }
        }

        private void ResolveRecruitEvent(PartyMemberData selectedData)
        {
            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
            if (selectedData != null && _gameState.PlayerState != null)
            {
                var newMember = PartyMemberFactory.CreateMember(selectedData.MemberID);
                if (newMember != null)
                {
                    _gameState.PlayerState.AddPartyMember(newMember);
                    ServiceLocator.Get<Core>().TriggerFullscreenFlash(_global.Palette_Sky, 0.2f);
                }
            }
            _hudRenderer.IsReplacementMode = false;
            _isRecruitEventFadingOut = true;
        }

        private void HandleMapInput(GameTime gameTime)
        {
            var cursorManager = ServiceLocator.Get<CursorManager>();
            var currentMouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            var cameraTransform = Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0);
            Matrix.Invert(ref cameraTransform, out var inverseCameraTransform);
            var mouseInMapSpace = Vector2.Transform(virtualMousePos, inverseCameraTransform);

            int rawHoveredNodeId = -1;

            if (_currentMap != null && _mapState == SplitMapState.Idle)
            {
                foreach (var node in _currentMap.Nodes.Values)
                {
                    if (node.IsReachable && node.GetBounds().Contains(mouseInMapSpace))
                    {
                        rawHoveredNodeId = node.Id;
                        break;
                    }
                }
            }

            bool hoveringButtons = (_settingsButton?.IsHovered ?? false);
            _hoveredNodeId = rawHoveredNodeId;
            _lastHoveredNodeId = _hoveredNodeId;

            if (_hoveredNodeId != -1) cursorManager.SetState(CursorState.HoverClickable);

            var inputManager = ServiceLocator.Get<InputManager>();
            if (_mapState == SplitMapState.Idle && !hoveringButtons && inputManager.IsMouseClickAvailable())
            {
                bool leftClickPressed = currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
                bool leftClickReleased = currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

                if (leftClickPressed && _hoveredNodeId != -1) _pressedNodeId = _hoveredNodeId;

                if (leftClickReleased)
                {
                    if (_pressedNodeId != -1 && _pressedNodeId == _hoveredNodeId)
                    {
                        if (_currentMap != null && _currentMap.Nodes[_hoveredNodeId].IsReachable)
                        {
                            _clickedNodeId = _hoveredNodeId;
                            _clickAnimTimer = CLICK_ANIM_DURATION;

                            _clickRotationDir = _random.Next(2) == 0 ? 1 : -1;

                            HandleNodeClick(_hoveredNodeId);

                            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                        }
                        _hoveredNodeId = -1;
                        inputManager.ConsumeMouseClick();
                    }
                    _pressedNodeId = -1;
                }
            }
            else _pressedNodeId = -1;
            previousMouseState = currentMouseState;
        }

        private void HandleNodeClick(int targetNodeId)
        {
            _playerMoveTargetNodeId = targetNodeId;
            _currentMoveStep = 0;
            _walkStepTimer = 0f;
            _isWalking = true;

            _playerMovePath = _currentMap?.Paths.Values.FirstOrDefault(p => p.FromNodeId == _playerCurrentNodeId && p.ToNodeId == targetNodeId);

            if (_playerMovePath != null)
            {
                _playerPathTotalLength = 0f;
                if (_playerMovePath.RenderPoints.Count > 1)
                {
                    for (int i = 0; i < _playerMovePath.RenderPoints.Count - 1; i++)
                    {
                        _playerPathTotalLength += Vector2.Distance(_playerMovePath.RenderPoints[i], _playerMovePath.RenderPoints[i + 1]);
                    }
                }
            }
        }

        private Vector2 GetPointOnPath(float progress)
        {
            if (_playerMovePath == null || _playerMovePath.RenderPoints.Count < 2) return _playerIcon.Position;

            float targetDistance = progress * _playerPathTotalLength;
            float currentDist = 0f;

            for (int i = 0; i < _playerMovePath.RenderPoints.Count - 1; i++)
            {
                Vector2 p1 = _playerMovePath.RenderPoints[i];
                Vector2 p2 = _playerMovePath.RenderPoints[i + 1];
                float segmentLength = Vector2.Distance(p1, p2);

                if (currentDist + segmentLength >= targetDistance)
                {
                    float remaining = targetDistance - currentDist;
                    float t = remaining / segmentLength;
                    return Vector2.Lerp(p1, p2, t);
                }
                currentDist += segmentLength;
            }
            return _playerMovePath.RenderPoints.Last();
        }

        private void FinalizeArrival()
        {
            if (_playerMovePath != null) _traversedPathIds.Add(_playerMovePath.Id);
            _playerCurrentNodeId = _playerMoveTargetNodeId;
            _visitedNodeIds.Add(_playerCurrentNodeId);
            _selectedNodeId = -1;

            if (_currentMap != null)
            {
                _currentMap.PruneColumn(_currentMap.Nodes[_playerCurrentNodeId].Floor, _playerCurrentNodeId);
            }

            _playerMovePath = null;
            _playerMoveTargetNodeId = -1;
            _currentMoveStep = 0;

            _mapState = SplitMapState.LiftingNode;
            _nodeLiftTimer = 0f;
        }

        private void ClampCameraOffset()
        {
            if (_currentMap != null)
            {
                float mapContentWidth = _currentMap.MapWidth;
                float screenWidth = Global.VIRTUAL_WIDTH;
                if (mapContentWidth > screenWidth)
                {
                    const float maxOffsetX = 0;
                    float minOffsetX = screenWidth - mapContentWidth;
                    _cameraOffset.X = Math.Clamp(_cameraOffset.X, minOffsetX, maxOffsetX);
                }
                else _cameraOffset.X = (screenWidth - mapContentWidth) / 2;
            }
        }

        private void UpdateLiftingNode(float deltaTime)
        {
            _nodeLiftTimer += deltaTime;
            var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
            if (currentNode != null)
            {
                float progress = Math.Clamp(_nodeLiftTimer / NODE_LIFT_DURATION, 0f, 1f);
                float eased = Easing.EaseOutCubic(progress);
                currentNode.VisualOffset = new Vector2(0, MathHelper.Lerp(0, -NODE_LIFT_AMOUNT, eased));

                _nodeArrivalScale = Vector2.One;
                _nodeArrivalRotation = 0f;
            }
            if (_nodeLiftTimer >= NODE_LIFT_DURATION)
            {
                _mapState = SplitMapState.PulsingNode;
                _pulseTimer = 0f;
                _arrivalRotationDir = _random.Next(2) == 0 ? 1 : -1;
            }
        }

        private void UpdatePulsingNode(float deltaTime)
        {
            _pulseTimer += deltaTime;

            float progress = Math.Clamp(_pulseTimer / PULSE_DURATION, 0f, 1f);
            float countdown = 1.0f - progress;

            float decayCurve = countdown * countdown;

            float scaleBonus = (ARRIVAL_SCALE_MAX - 1.0f) * decayCurve;
            _nodeArrivalScale = new Vector2(1.0f + scaleBonus);

            _nodeArrivalRotation = MathHelper.ToRadians(POP_ROTATION_DEG) * decayCurve * _arrivalRotationDir;

            _nodeArrivalShake = Vector2.Zero;

            if (_pulseTimer >= PULSE_DURATION)
            {
                _mapState = SplitMapState.EventInProgress;
                _nodeArrivalScale = Vector2.One;
                _nodeArrivalRotation = 0f;
                _nodeArrivalShake = Vector2.Zero;
                TriggerNodeEvent(_playerCurrentNodeId);
            }
        }

        private void UpdateLoweringNode(float deltaTime)
        {
            _nodeLiftTimer += deltaTime;
            var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
            if (currentNode != null)
            {
                float progress = Math.Clamp(_nodeLiftTimer / NODE_LOWERING_DURATION, 0f, 1f);
                float eased = Easing.EaseOutBounce(progress);
                currentNode.VisualOffset = new Vector2(0, MathHelper.Lerp(-NODE_LIFT_AMOUNT, 0, eased));

                if (progress > 0.8f)
                {
                    float squashProgress = (progress - 0.8f) / 0.2f;
                    float squash = MathF.Sin(squashProgress * MathHelper.Pi);
                    _nodeArrivalScale = new Vector2(1.0f + (squash * 0.1f), 1.0f - (squash * 0.1f));
                }
                else _nodeArrivalScale = Vector2.One;

                _nodeArrivalRotation = 0f;
            }

            if (_nodeLiftTimer >= NODE_LOWERING_DURATION)
            {
                if (currentNode != null)
                {
                    currentNode.VisualOffset = Vector2.Zero;
                    _cameraFocusNodeId = _playerCurrentNodeId;
                    UpdateCameraTarget(currentNode.Position, false);
                }
                _nodeArrivalScale = Vector2.One;
                _nodeArrivalRotation = 0f;

                _mapState = SplitMapState.PostEventDelay;
                _postEventDelayTimer = POST_EVENT_DELAY;
            }
        }

        private void UpdateCameraTarget(Vector2 targetNodePosition, bool snap)
        {
            if (_currentMap == null) return;

            const float playerScreenAnchorX = 40f;
            float targetX = playerScreenAnchorX - targetNodePosition.X;
            float mapContentWidth = _currentMap.MapWidth;
            float screenWidth = Global.VIRTUAL_WIDTH;

            if (mapContentWidth > screenWidth)
            {
                float minOffsetX = screenWidth - mapContentWidth;
                const float maxOffsetX = 0;
                targetX = Math.Clamp(targetX, minOffsetX, maxOffsetX);
            }
            else targetX = (screenWidth - mapContentWidth) / 2;

            float centerCorrection = _hudSlideOffset / 2f;
            _targetCameraOffset = new Vector2(MathF.Round(targetX), BASE_CAMERA_Y + centerCorrection);

            if (snap) _cameraOffset = _targetCameraOffset;
        }

        private void StartPathRevealAnimation()
        {
            _nodeForPathReveal = _playerCurrentNodeId;
            var currentNode = _currentMap?.Nodes[_nodeForPathReveal];
            if (currentNode == null) return;
            if (_currentMap != null && _currentMap.Nodes.TryGetValue(_nodeForPathReveal, out var nodeForPaths))
            {
                foreach (var pathId in nodeForPaths.OutgoingPathIds)
                {
                    _pathAnimationProgress[pathId] = 0f;
                    float duration = PATH_ANIMATION_DURATION + (float)(_random.NextDouble() * 0.5 - 0.25);
                    _pathAnimationDurations[pathId] = Math.Max(0.5f, duration);
                }
            }
        }

        public void InitiateCombat(List<string> enemyArchetypes)
        {
            _pendingCombatArchetypes = enemyArchetypes;
            ExecuteCombatTransition();
        }

        private void ExecuteCombatTransition()
        {
            _waitingForCombatCameraSettle = false;
            BattleSetup.EnemyArchetypes = _pendingCombatArchetypes;
            BattleSetup.ReturnSceneState = GameSceneState.Split;
            var transitionOut = _transitionManager.GetRandomTransition();
            var transitionIn = _transitionManager.GetRandomTransition();
            _sceneManager.ChangeScene(GameSceneState.Battle, transitionOut, transitionIn, 0.5f);
            _pendingCombatArchetypes = null;
        }

        private void TriggerNodeEvent(int nodeId)
        {
            if (_currentMap == null || !_currentMap.Nodes.TryGetValue(nodeId, out var node)) return;
            switch (node.NodeType)
            {
                case SplitNodeType.Battle:
                case SplitNodeType.MajorBattle:
                    _mapState = SplitMapState.EventInProgress;
                    WasMajorBattle = node.NodeType == SplitNodeType.MajorBattle;
                    InitiateCombat(node.EventData as List<string> ?? new List<string>());
                    break;
                case SplitNodeType.Rest:
                    Debug.WriteLine("[SplitMapScene] Player landed on a Rest node.");
                    _mapState = SplitMapState.Resting;
                    _isRestEventActive = true;
                    _isRestEventFadingOut = false;
                    _restEventFadeTimer = 0f;
                    _restPromptPlink.Start(REST_EVENT_FADE_DURATION);
                    _restYesButton.PlayEntrance(REST_EVENT_FADE_DURATION + 0.15f);
                    _restNoButton.PlayEntrance(REST_EVENT_FADE_DURATION + 0.3f);
                    _restNavGroup.SelectFirst();
                    break;
                case SplitNodeType.Recruit:
                    Debug.WriteLine("[SplitMapScene] Player landed on a Recruit node.");
                    _mapState = SplitMapState.Recruiting;
                    _isRecruitEventActive = true;
                    _isRecruitEventFadingOut = false;
                    _recruitEventFadeTimer = 0f;
                    _selectedRecruitIndex = -1;
                    _hudRenderer.IsReplacementMode = false;
                    _hudRenderer.SelectedForReplacement = null;
                    _recruitActionButton.Text = "SKIP";

                    var validIds = BattleDataCache.PartyMembers.Keys
                        .Where(id => !_gameState.PlayerState.PastMemberIds.Contains(id))
                        .OrderBy(x => _random.Next())
                        .ToList();

                    _recruitCandidates.Clear();
                    foreach (var id in validIds.Take(2))
                    {
                        _recruitCandidates.Add(BattleDataCache.PartyMembers[id]);
                    }

                    _recruitPromptPlink.Start(RECRUIT_EVENT_FADE_DURATION);
                    _recruitActionButton.PlayEntrance(RECRUIT_EVENT_FADE_DURATION + 0.15f);
                    _recruitNavGroup.SelectFirst();
                    break;
                default:
                    node.IsCompleted = true;
                    UpdateCameraTarget(node.Position, false);
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                    break;
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_currentMap == null) return;

            var snappedCameraOffset = new Vector2(MathF.Round(_cameraOffset.X), MathF.Round(_cameraOffset.Y));
            var cameraTransform = Matrix.CreateTranslation(snappedCameraOffset.X, snappedCameraOffset.Y, 0);
            var finalTransform = cameraTransform * transform;

            float mapViewHeight = MathF.Round((Global.VIRTUAL_HEIGHT - SplitMapHudRenderer.HUD_HEIGHT) + _hudSlideOffset);

            var tl = Vector2.Transform(Vector2.Zero, transform);
            var br = Vector2.Transform(new Vector2(Global.VIRTUAL_WIDTH, mapViewHeight), transform);

            var minX = Math.Min(tl.X, br.X);
            var maxX = Math.Max(tl.X, br.X);
            var minY = Math.Min(tl.Y, br.Y);
            var maxY = Math.Max(tl.Y, br.Y);

            var scissorRect = new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
            var viewport = spriteBatch.GraphicsDevice.Viewport.Bounds;
            scissorRect = Rectangle.Intersect(scissorRect, viewport);
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;

            spriteBatch.End();
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: finalTransform, rasterizerState: _scissorRasterizerState);

            var pixel = ServiceLocator.Get<Texture2D>();

            if (_global.ShowSplitMapGrid)
            {
                Color gridColor = _global.Palette_DarkShadow * 0.5f;
                const int gridSize = Global.SPLIT_MAP_GRID_SIZE;
                Matrix.Invert(ref cameraTransform, out var inverseCameraTransform);
                var topLeftGrid = Vector2.Transform(Vector2.Zero, inverseCameraTransform);
                var bottomRightGrid = Vector2.Transform(new Vector2(Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), inverseCameraTransform);
                int startX = (int)Math.Floor(topLeftGrid.X / gridSize) * gridSize;
                int endX = (int)Math.Ceiling(bottomRightGrid.X / gridSize) * gridSize;
                int startY = (int)Math.Floor(topLeftGrid.Y / gridSize) * gridSize;
                int endY = (int)Math.Ceiling(bottomRightGrid.Y / gridSize) * gridSize;
                for (int x = startX; x <= endX; x += gridSize) spriteBatch.DrawLineSnapped(new Vector2(x, startY), new Vector2(x, endY), gridColor);
                for (int y = startY; y <= endY; y += gridSize) spriteBatch.DrawLineSnapped(new Vector2(startX, y), new Vector2(endX, y), gridColor);
            }

            var drawableObjects = new List<DrawableMapObject>();
            drawableObjects.AddRange(_currentMap.Nodes.Values.Select(n => new DrawableMapObject { Type = DrawableMapObject.ObjectType.Node, Position = n.Position, Data = n }));

            if (_currentMap.BakedSceneryTexture != null) spriteBatch.DrawSnapped(_currentMap.BakedSceneryTexture, Vector2.Zero, Color.White);

            drawableObjects.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));
            DrawAllPaths(spriteBatch, pixel);

            foreach (var obj in drawableObjects)
            {
                if (obj.Type == DrawableMapObject.ObjectType.Node) DrawNode(spriteBatch, (SplitMapNode)obj.Data!, gameTime);
            }

            _playerIcon.Draw(spriteBatch, (float)gameTime.ElapsedGameTime.TotalSeconds);
            _birdManager.Draw(spriteBatch, snappedCameraOffset);

            spriteBatch.End();

            _particleSystemManager.Draw(spriteBatch, finalTransform);

            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

            var mapBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            _voidEdgeEffect.Draw(spriteBatch, mapBounds);

            // --- REST EVENT OVERLAY ---
            if (_isRestEventActive)
            {
                float alpha = _restEventFadeTimer / REST_EVENT_FADE_DURATION;
                alpha = Math.Clamp(alpha, 0f, 1f);

                var maskRect = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, (int)mapViewHeight);
                spriteBatch.DrawSnapped(pixel, maskRect, _global.Palette_Off * (alpha * 0.95f));

                if (alpha >= 1f || _restPromptPlink.IsActive)
                {
                    string prompt = "SHOULD THE PARTY REST?";
                    var defaultFont = ServiceLocator.Get<Core>().DefaultFont;
                    var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

                    Vector2 promptSize = defaultFont.MeasureString(prompt);
                    Vector2 promptOrigin = promptSize / 2f;
                    Vector2 promptPos = new Vector2(Global.VIRTUAL_WIDTH / 2f, mapViewHeight / 2f - 15);

                    float pScale = _restPromptPlink.IsActive ? _restPromptPlink.Scale : 1f;
                    float pRot = _restPromptPlink.IsActive ? _restPromptPlink.Rotation : 0f;

                    if (pScale > 0.01f && !_isRestEventFadingOut)
                    {
                        spriteBatch.DrawStringSnapped(defaultFont, prompt, promptPos, _global.Palette_LightPale, pRot, promptOrigin, pScale, SpriteEffects.None, 0f);
                    }

                    if (!_isRestEventFadingOut)
                    {
                        _restYesButton.Draw(spriteBatch, secondaryFont, gameTime, transform);
                        _restNoButton.Draw(spriteBatch, secondaryFont, gameTime, transform);
                    }
                }
            }

            // --- RECRUIT EVENT OVERLAY ---
            if (_isRecruitEventActive)
            {
                float alpha = _recruitEventFadeTimer / RECRUIT_EVENT_FADE_DURATION;
                alpha = Math.Clamp(alpha, 0f, 1f);

                var maskRect = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, (int)mapViewHeight);
                spriteBatch.DrawSnapped(pixel, maskRect, _global.Palette_Off * (alpha * 0.95f));

                if (alpha >= 1f || _recruitPromptPlink.IsActive)
                {
                    string prompt = "CHOOSE A RECRUIT";
                    var defaultFont = ServiceLocator.Get<Core>().DefaultFont;
                    var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                    var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

                    Vector2 promptSize = defaultFont.MeasureString(prompt);
                    Vector2 promptOrigin = promptSize / 2f;
                    Vector2 promptPos = new Vector2(Global.VIRTUAL_WIDTH / 2f, mapViewHeight / 2f - 29);

                    float pScale = _recruitPromptPlink.IsActive ? _recruitPromptPlink.Scale : 1f;
                    float pRot = _recruitPromptPlink.IsActive ? _recruitPromptPlink.Rotation : 0f;

                    if (pScale > 0.01f && !_isRecruitEventFadingOut)
                    {
                        spriteBatch.DrawStringSnapped(defaultFont, prompt, promptPos, _global.Palette_LightPale, pRot, promptOrigin, pScale, SpriteEffects.None, 0f);
                    }

                    if (!_isRecruitEventFadingOut)
                    {
                        int centerX = Global.VIRTUAL_WIDTH / 2;
                        int startY = (int)(mapViewHeight / 2f - 2);
                        int spacing = 60;

                        var mousePos = Core.TransformMouse(Mouse.GetState().Position);
                        var silhouette = _spriteManager.PlayerMasterSpriteSheetSilhouette;
                        var sheet = _spriteManager.PlayerMasterSpriteSheet;

                        Action<Texture2D, Vector2, Rectangle, Color, int, bool> drawOutlines = (tex, pos, rect, col, dist, diag) => {
                            Vector2 origin = new Vector2(16, 16);
                            if (diag)
                            {
                                spriteBatch.DrawSnapped(tex, pos + new Vector2(-dist, -dist), rect, col, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                                spriteBatch.DrawSnapped(tex, pos + new Vector2(dist, -dist), rect, col, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                                spriteBatch.DrawSnapped(tex, pos + new Vector2(-dist, dist), rect, col, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                                spriteBatch.DrawSnapped(tex, pos + new Vector2(dist, dist), rect, col, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                            }
                            else
                            {
                                spriteBatch.DrawSnapped(tex, pos + new Vector2(-dist, 0), rect, col, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                                spriteBatch.DrawSnapped(tex, pos + new Vector2(dist, 0), rect, col, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                                spriteBatch.DrawSnapped(tex, pos + new Vector2(0, -dist), rect, col, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                                spriteBatch.DrawSnapped(tex, pos + new Vector2(0, dist), rect, col, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                            }
                        };

                        for (int i = 0; i < _recruitCandidates.Count; i++)
                        {
                            var candidate = _recruitCandidates[i];
                            int drawX = centerX + (i == 0 ? -spacing / 2 : spacing / 2);

                            _recruitHitboxes[i] = new Rectangle(drawX - 16, startY - 16, 32, 32);
                            bool isHovered = _recruitHitboxes[i].Contains(mousePos);
                            bool isSelected = _selectedRecruitIndex == i;

                            bool isPopped = isHovered || isSelected;

                            int spriteIndex = int.TryParse(candidate.MemberID, out int id) ? id : 0;

                            Vector2 origin = new Vector2(16, 16);
                            Vector2 drawPos = new Vector2(drawX, startY);

                            Color outlineColor = Color.Transparent;
                            if (isSelected) outlineColor = _global.Palette_DarkSun;
                            else if (isHovered) outlineColor = _global.Palette_Sun;

                            float bobOffset = 0f;
                            bool useAltFrame = false;

                            if (isPopped)
                            {
                                float time = (float)gameTime.TotalGameTime.TotalSeconds;
                                float bobSpeed = 4f;
                                float sineValue = MathF.Sin(time * bobSpeed);
                                useAltFrame = sineValue < 0;
                                bobOffset = useAltFrame ? -1f : 0f;

                                PlayerSpriteType bodyType = useAltFrame ? PlayerSpriteType.BodyAlt : PlayerSpriteType.BodyNormal;
                                var bodyRect = _spriteManager.GetPlayerSourceRect(spriteIndex, bodyType);

                                PlayerSpriteType headType = useAltFrame ? PlayerSpriteType.Alt : PlayerSpriteType.Normal;
                                var headRect = _spriteManager.GetPlayerSourceRect(spriteIndex, headType);

                                // Draw Outline
                                if (outlineColor != Color.Transparent && silhouette != null)
                                {
                                    // Outer Color
                                    drawOutlines(silhouette, drawPos, bodyRect, outlineColor, 2, false);
                                    drawOutlines(silhouette, drawPos, bodyRect, outlineColor, 1, true);
                                    drawOutlines(silhouette, drawPos + new Vector2(0, bobOffset), headRect, outlineColor, 2, false);
                                    drawOutlines(silhouette, drawPos + new Vector2(0, bobOffset), headRect, outlineColor, 1, true);

                                    // Inner Off Color
                                    drawOutlines(silhouette, drawPos, bodyRect, _global.Palette_Off, 1, false);
                                    drawOutlines(silhouette, drawPos + new Vector2(0, bobOffset), headRect, _global.Palette_Off, 1, false);
                                }

                                // Draw Body and Head
                                spriteBatch.DrawSnapped(sheet, drawPos, bodyRect, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                                spriteBatch.DrawSnapped(sheet, drawPos + new Vector2(0, bobOffset), headRect, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);

                                // Draw Name
                                Vector2 nameSize = tertiaryFont.MeasureString(candidate.Name.ToUpper());
                                spriteBatch.DrawStringSnapped(tertiaryFont, candidate.Name.ToUpper(), new Vector2(drawX - nameSize.X / 2f, startY + 20), isSelected ? _global.Palette_Sun : _global.Palette_LightPale);
                            }
                            else
                            {
                                // Static smaller head (Portrait8x8 is Frame 2)
                                var headRect = _spriteManager.GetPlayerSourceRect(spriteIndex, PlayerSpriteType.Portrait8x8);

                                // Draw Outline
                                if (outlineColor != Color.Transparent && silhouette != null)
                                {
                                    drawOutlines(silhouette, drawPos, headRect, outlineColor, 2, false);
                                    drawOutlines(silhouette, drawPos, headRect, outlineColor, 1, true);
                                    drawOutlines(silhouette, drawPos, headRect, _global.Palette_Off, 1, false);
                                }

                                // Draw Sprite
                                spriteBatch.DrawSnapped(sheet, drawPos, headRect, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                            }
                        }

                        bool isPartyFull = _gameState.PlayerState.Party.Count >= 4;
                        bool hasRecruitSelected = _selectedRecruitIndex != -1;
                        bool hasReplaceTarget = _hudRenderer.SelectedForReplacement != null;

                        int btnY = (int)(mapViewHeight / 2f + 25);

                        if (isPartyFull && hasRecruitSelected && !hasReplaceTarget)
                        {
                            if (_partyFullPlink.IsActive)
                            {
                                float pfScale = _partyFullPlink.Scale;
                                float pfRot = _partyFullPlink.Rotation;
                                if (pfScale > 0.01f)
                                {
                                    string fullText = "PARTY FULL";
                                    Vector2 fullSize = secondaryFont.MeasureString(fullText);
                                    Vector2 fullOrigin = fullSize / 2f;
                                    Vector2 fullPos = new Vector2(centerX, btnY + 7);
                                    spriteBatch.DrawStringSnapped(secondaryFont, fullText, fullPos, _global.Palette_Rust, pfRot, fullOrigin, pfScale, SpriteEffects.None, 0f);
                                }
                            }
                            else
                            {
                                string fullText = "PARTY FULL";
                                Vector2 fullSize = secondaryFont.MeasureString(fullText);
                                Vector2 fullOrigin = fullSize / 2f;
                                Vector2 fullPos = new Vector2(centerX, btnY + 7);
                                spriteBatch.DrawStringSnapped(secondaryFont, fullText, fullPos, _global.Palette_Rust, 0f, fullOrigin, 1.0f, SpriteEffects.None, 0f);
                            }
                        }
                        else if (isPartyFull && hasRecruitSelected && hasReplaceTarget)
                        {
                            _replaceButton.Bounds = new Rectangle(centerX - 35, btnY, 70, 15);
                            _replaceButton.Draw(spriteBatch, secondaryFont, gameTime, transform);
                        }
                        else
                        {
                            _recruitActionButton.Bounds = new Rectangle(centerX - 25, btnY, 50, 15);
                            _recruitActionButton.Draw(spriteBatch, secondaryFont, gameTime, transform);
                        }
                    }
                }
            }

            _hudRenderer.Draw(spriteBatch, gameTime, _hudSlideOffset);

            _settingsButton?.Draw(spriteBatch, font, gameTime, transform);

            if (_hoveredNodeId != -1 && _mapState == SplitMapState.Idle && !_isRestEventActive && !_isRecruitEventActive)
            {
                if (_currentMap.Nodes.TryGetValue(_hoveredNodeId, out var hoveredNode) && hoveredNode.IsReachable)
                {
                    string nodeText = hoveredNode.NodeType switch
                    {
                        SplitNodeType.Battle => hoveredNode.Difficulty switch
                        {
                            BattleDifficulty.Easy => "EASY COMBAT",
                            BattleDifficulty.Hard => "HARD COMBAT",
                            _ => "COMBAT",
                        },
                        SplitNodeType.MajorBattle => "MAJOR BATTLE",
                        SplitNodeType.Rest => "REST",
                        SplitNodeType.Recruit => "RECRUIT",
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(nodeText))
                    {
                        var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                        var nodeTextSize = secondaryFont.MeasureString(nodeText);
                        Vector2 nodeScreenPos = hoveredNode.Position + snappedCameraOffset;
                        float textX = nodeScreenPos.X - (nodeTextSize.Width / 2f);
                        float textY = nodeScreenPos.Y + 8f;
                        var textPosition = new Vector2(textX, textY);
                        _nodeTextWaveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                        TextAnimator.DrawTextWithEffectSquareOutlined(spriteBatch, secondaryFont, nodeText, textPosition, _global.Palette_DarkSun, _global.Palette_Black, TextEffectType.Wave, _nodeTextWaveTimer);
                    }
                }
            }
            else _nodeTextWaveTimer = 0f;
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime) { }

        private void DrawAllPaths(SpriteBatch spriteBatch, Texture2D pixel)
        {
            if (_currentMap == null) return;

            SplitMapPath? highlightedPath = null;
            if (_hoveredNodeId != -1 && _mapState == SplitMapState.Idle && !_isRestEventActive && !_isRecruitEventActive)
                highlightedPath = _currentMap.Paths.Values.FirstOrDefault(p => p.FromNodeId == _playerCurrentNodeId && p.ToNodeId == _hoveredNodeId);

            foreach (var path in _currentMap.Paths.Values)
            {
                if (path == highlightedPath) continue;

                if (path.IsAbandoned)
                {
                    float fadeT = Math.Clamp((1.0f - path.VisualAlpha) / 0.5f, 0f, 1f);
                    Color lerpedColor = Color.Lerp(_global.Palette_DarkestPale, _global.Palette_DarkShadow, fadeT);
                    DrawPath(spriteBatch, pixel, path, lerpedColor, false, path.VisualAlpha);
                    continue;
                }

                var fromNode = _currentMap.Nodes[path.FromNodeId];
                var toNode = _currentMap.Nodes[path.ToNodeId];
                bool isPathFromCurrentNode = fromNode.Id == _playerCurrentNodeId;
                bool isPathToReachableNode = toNode.IsReachable;
                bool isPathTraversed = _traversedPathIds.Contains(path.Id);
                bool isAnimating = isPathFromCurrentNode && isPathToReachableNode && !_visitedNodeIds.Contains(toNode.Id);

                if (isPathTraversed || isAnimating)
                    DrawPath(spriteBatch, pixel, path, _global.SplitMapPathColor, isAnimating, path.VisualAlpha);
                else
                    DrawPath(spriteBatch, pixel, path, _global.Palette_DarkestPale, false, path.VisualAlpha);
            }

            if (highlightedPath != null) DrawPath(spriteBatch, pixel, highlightedPath, _global.SplitMapNodeColor, false, 1.0f);
        }

        private void DrawPath(SpriteBatch spriteBatch, Texture2D pixel, SplitMapPath path, Color pathColor, bool isAnimating, float alpha)
        {
            if (_currentMap == null || path.PixelPoints.Count < 2) return;
            var fromNode = _currentMap.Nodes[path.FromNodeId];
            var toNode = _currentMap.Nodes[path.ToNodeId];
            int numPixelsToDraw;
            if (isAnimating)
            {
                float duration = _pathAnimationDurations.GetValueOrDefault(path.Id, PATH_ANIMATION_DURATION);
                float animationTimer = _pathAnimationProgress.GetValueOrDefault(path.Id, 0f);
                float linearProgress = Math.Clamp(animationTimer / duration, 0f, 1f);
                if (linearProgress <= 0f) return;
                float easedProgress = Easing.EaseOutCubic(linearProgress);
                numPixelsToDraw = (int)(easedProgress * path.PixelPoints.Count);
            }
            else numPixelsToDraw = path.PixelPoints.Count;

            if (numPixelsToDraw <= 0) return;

            Color finalColor = pathColor * alpha;

            for (int i = 0; i < numPixelsToDraw; i++)
            {
                var point = path.PixelPoints[i];
                var pointVec = point.ToVector2();
                float distFrom = Vector2.Distance(pointVec, fromNode.Position);
                float distTo = Vector2.Distance(pointVec, toNode.Position);
                if (distFrom <= PATH_EXCLUSION_RADIUS || distTo <= PATH_EXCLUSION_RADIUS) continue;
                if (i % 4 < 2) spriteBatch.Draw(pixel, pointVec, finalColor);
            }
        }

        private void DrawNode(SpriteBatch spriteBatch, SplitMapNode node, GameTime gameTime)
        {
            var (texture, silhouette, sourceRect, origin) = GetNodeDrawData(node, gameTime);
            var bounds = node.GetBounds();

            var color = _global.SplitMapNodeColor;
            float alpha = node.VisualAlpha;

            if (node.IsAbandoned)
            {
                float fadeT = Math.Clamp((1.0f - node.VisualAlpha) / 0.5f, 0f, 1f);
                color = Color.Lerp(_global.Palette_DarkestPale, _global.Palette_DarkShadow, fadeT);
            }
            else if (node.IsCompleted) color = _global.SplitMapPathColor;
            else if (node.NodeType != SplitNodeType.Origin && node.Id != _playerCurrentNodeId && !node.IsReachable) color = _global.Palette_DarkestPale;

            bool isSelected = (node.Id == _selectedNodeId);
            bool isHovered = (node.Id == _hoveredNodeId && !_isRestEventActive && !_isRecruitEventActive);
            bool isAnimatingThisNode = (node.Id == _playerCurrentNodeId) &&
                                       (_mapState == SplitMapState.LiftingNode ||
                                        _mapState == SplitMapState.PulsingNode ||
                                        _mapState == SplitMapState.EventInProgress ||
                                        _mapState == SplitMapState.LoweringNode);

            if (node.IsReachable && (isHovered || isSelected || isAnimatingThisNode)) color = _global.ButtonHoverColor;

            Vector2 scale = Vector2.One;
            Vector2 shake = Vector2.Zero;
            float rotation = 0f;

            if (node.Id == _playerCurrentNodeId)
            {
                scale = _nodeArrivalScale;
                shake = _nodeArrivalShake;
                rotation = _nodeArrivalRotation;
            }

            if (node.Id == _clickedNodeId && _clickAnimTimer > 0f)
            {
                float t = _clickAnimTimer / CLICK_ANIM_DURATION;
                float decay = t * t;

                float scaleBonus = (CLICK_SCALE_MAX - 1.0f) * decay;
                scale = new Vector2(1.0f + scaleBonus);

                rotation = MathHelper.ToRadians(POP_ROTATION_DEG) * decay * _clickRotationDir;
            }

            var position = bounds.Center.ToVector2() + node.VisualOffset + shake;

            Color outlineColor = node.IsAbandoned ? _global.Palette_Black * alpha : _global.Palette_Black;
            Color finalColor = color * alpha;

            if (silhouette != null)
            {
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(-1, 0), sourceRect, outlineColor, rotation, origin, scale, SpriteEffects.None, 0.4f);
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(1, 0), sourceRect, outlineColor, rotation, origin, scale, SpriteEffects.None, 0.4f);
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(0, -1), sourceRect, outlineColor, rotation, origin, scale, SpriteEffects.None, 0.4f);
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(0, 1), sourceRect, outlineColor, rotation, origin, scale, SpriteEffects.None, 0.4f);
            }
            spriteBatch.DrawSnapped(texture, position, sourceRect, finalColor, rotation, origin, scale, SpriteEffects.None, 0.4f);
        }

        private (Texture2D texture, Texture2D? silhouette, Rectangle? sourceRect, Vector2 origin) GetNodeDrawData(SplitMapNode node, GameTime gameTime)
        {
            Texture2D texture;
            Texture2D? silhouette;
            switch (node.NodeType)
            {
                case SplitNodeType.Origin:
                    texture = _spriteManager.SplitNodeStart;
                    silhouette = _spriteManager.SplitNodeStartSilhouette;
                    break;
                case SplitNodeType.Battle:
                    if (node.Difficulty == BattleDifficulty.Easy)
                    {
                        texture = _spriteManager.SplitNodeEasyCombat;
                        silhouette = _spriteManager.SplitNodeEasyCombatSilhouette;
                    }
                    else if (node.Difficulty == BattleDifficulty.Hard)
                    {
                        texture = _spriteManager.SplitNodeHardCombat;
                        silhouette = _spriteManager.SplitNodeHardCombatSilhouette;
                    }
                    else
                    {
                        texture = _spriteManager.SplitNodeCombat;
                        silhouette = _spriteManager.SplitNodeCombatSilhouette;
                    }
                    break;
                case SplitNodeType.MajorBattle:
                    texture = _spriteManager.SplitNodeCombat;
                    silhouette = _spriteManager.SplitNodeCombatSilhouette;
                    break;
                case SplitNodeType.Rest:
                    texture = _spriteManager.SplitNodeRest;
                    silhouette = _spriteManager.SplitNodeRestSilhouette;
                    break;
                case SplitNodeType.Recruit:
                    texture = _spriteManager.SplitNodeRecruit;
                    silhouette = _spriteManager.SplitNodeRecruitSilhouette;
                    break;
                default:
                    texture = _spriteManager.SplitNodeCombat;
                    silhouette = _spriteManager.SplitNodeCombatSilhouette;
                    break;
            }
            int frameIndex = 0;
            bool shouldAnimate = node.IsReachable || (node.NodeType == SplitNodeType.Origin && !node.IsCompleted);
            if (shouldAnimate)
            {
                float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;
                frameIndex = (int)((totalTime + node.AnimationOffset) / NODE_FRAME_DURATION) % 2;
            }
            var sourceRect = new Rectangle(frameIndex * 32, 0, 32, 32);
            var origin = new Vector2(16, 16);
            return (texture, silhouette, sourceRect, origin);
        }

        private void OpenSettings()
        {
            _sceneManager.ShowModal(GameSceneState.Settings);
        }
    }
}

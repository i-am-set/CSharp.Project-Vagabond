using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
using ProjectVagabond.Items;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class SplitMapScene : GameScene
    {
        private enum SplitMapView { Map, Inventory, Shop, Rest, Recruit } // Removed Settings
        private SplitMapView _currentView = SplitMapView.Map;
        private SplitMapView _viewToReturnTo = SplitMapView.Map;

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
        private readonly DiceRollingSystem _diceRollingSystem;
        private readonly StoryNarrator _resultNarrator;
        private readonly ComponentStore _componentStore;
        private readonly VoidEdgeEffect _voidEdgeEffect;
        private readonly SplitMapInventoryOverlay _inventoryOverlay;
        // Removed SplitMapSettingsOverlay
        private readonly SplitMapShopOverlay _shopOverlay;
        private readonly SplitMapRestOverlay _restOverlay;
        private readonly SplitMapRecruitOverlay _recruitOverlay;
        private readonly BirdManager _birdManager;
        private readonly TransitionManager _transitionManager;

        private SplitMap? _currentMap;
        private int _playerCurrentNodeId;
        private readonly PlayerMapIcon _playerIcon;
        private NarrativeDialog _narrativeDialog;

        private const float PLAYER_MOVE_SPEED = 30f;
        private const float CAMERA_LERP_SPEED = 5f;
        private const float POST_EVENT_DELAY = 0.25f;
        private const float PATH_ANIMATION_DURATION = 0.75f;

        // --- ANIMATION TUNING ---
        private const float NODE_LIFT_DURATION = 0.25f;     // Slightly longer for anticipation
        private const float PULSE_DURATION = 0.4f;          // Longer for the elastic wobble
        private const float NODE_LOWERING_DURATION = 0.5f;  // Longer for the bounce
        private const float NODE_LIFT_AMOUNT = 12f;         // Height of the jump

        // --- ARRIVAL SHAKE TUNING ---
        private const float NODE_ARRIVAL_SHAKE_MAGNITUDE = 1.5f; // Reduced from ~3.0
        private const float NODE_ARRIVAL_SHAKE_FREQUENCY = 25.0f; // Controlled frequency (Sine wave)

        private Vector2 _cameraOffset;
        private Vector2 _targetCameraOffset;
        // Removed RoundedCameraOffset to allow smooth sub-pixel panning

        private float _playerMoveTimer;
        private float _playerMoveDuration;
        private int _playerMoveTargetNodeId;
        private SplitMapPath? _playerMovePath;

        private int _hoveredNodeId = -1;
        private int _lastHoveredNodeId = -1; // Track previous hover to trigger haptics once
        private int _pressedNodeId = -1; // Track which node is currently being held down

        private readonly HashSet<int> _visitedNodeIds = new HashSet<int>();
        private readonly HashSet<int> _traversedPathIds = new HashSet<int>();
        private int _nodeForPathReveal = -1;

        private readonly Dictionary<int, float> _pathAnimationProgress = new();
        private readonly Dictionary<int, float> _pathAnimationDurations = new();
        private static readonly Random _random = new Random();

        private const float PATH_EXCLUSION_RADIUS = 10f;

        private float _pulseTimer = 0f;
        private const float PULSE_AMOUNT = 0.3f;

        private bool _wasModalActiveLastFrame = false;

        private enum EventState { Idle, AwaitingDiceRoll, NarratingResult }
        private EventState _eventState = EventState.Idle;
        private NarrativeChoice? _pendingChoiceForDiceRoll;
        private float _postEventDelayTimer = 0f;

        private enum SplitMapState { Idle, PlayerMoving, LiftingNode, PulsingNode, EventInProgress, LoweringNode, PostEventDelay }
        private SplitMapState _mapState = SplitMapState.Idle;

        private const float NODE_FRAME_DURATION = 0.5f;
        private float _nodeLiftTimer = 0f;

        private bool _isPanning = false;
        private Point _panStartMousePosition;
        private Point _lastPanMousePosition;
        private Vector2 _panStartCameraOffset;
        private Vector2 _cameraVelocity = Vector2.Zero;
        private const float PAN_SENSITIVITY = 1.0f;
        private const float PAN_FRICTION = 10f;
        private float _snapBackDelayTimer = 0f;
        private const float SNAP_BACK_DELAY = 1f;
        private const float SCROLL_PAN_SPEED = 1f;

        private List<string>? _pendingCombatArchetypes;
        private bool _waitingForCombatCameraSettle = false;
        private int _framesToWaitAfterSettle = 0;
        private const float CAMERA_SETTLE_THRESHOLD = 0.5f;

        public static bool PlayerWonLastBattle { get; set; } = true;
        public static bool WasMajorBattle { get; set; } = false;

        private ImageButton? _settingsButton;

        // --- SETTINGS BUTTON ANIMATION STATE ---
        private enum SettingsButtonState { Hidden, AnimatingIn, Visible }
        private SettingsButtonState _settingsButtonState = SettingsButtonState.Hidden;
        private float _settingsButtonAnimTimer = 0f;
        private const float SETTINGS_BUTTON_ANIM_DURATION = 0.6f;

        // --- REPLACED CONTROLLER WITH SIMPLE TIMER ---
        private float _nodeTextWaveTimer = 0f;

        // Haptics Manager
        private readonly HapticsManager _hapticsManager;

        // Rasterizer state for clipping
        private static readonly RasterizerState _scissorRasterizerState = new RasterizerState { ScissorTestEnable = true };

        // --- NODE HOVER ANIMATION STATE ---
        private readonly Dictionary<int, float> _nodeHoverTimers = new Dictionary<int, float>();
        private const float NODE_HOVER_POP_SCALE_TARGET = 1.5f;
        private const float NODE_HOVER_POP_SPEED = 12.0f;

        // --- Node Hover Float/Rotate/Pulse Tuning ---
        private const float NODE_HOVER_FLOAT_SPEED = 3.0f;
        private const float NODE_HOVER_FLOAT_AMP = 2.0f;
        private const float NODE_HOVER_ROT_SPEED = 2.0f;
        private const float NODE_HOVER_ROT_AMT = 0.05f;
        private const float NODE_HOVER_PULSE_SPEED = 2.0f;
        private const float NODE_HOVER_PULSE_AMOUNT = 0.05f;

        // --- Node Arrival Animation State ---
        private Vector2 _nodeArrivalScale = Vector2.One;
        private Vector2 _nodeArrivalShake = Vector2.Zero;

        // --- Node Selection Animation State ---
        private int _selectedNodeId = -1;
        private float _nodeSelectionAnimTimer = 0f;
        private const float NODE_SELECTION_POP_DURATION = 0.3f; // Increased for elastic effect
        private const float NODE_SELECTION_SCALE_EXTRA = 0.8f; // Extra scale for the spring
        private const float NODE_SELECTION_SHAKE_MAGNITUDE = 1.0f;
        private const float NODE_SELECTION_SHAKE_FREQUENCY = 50.0f;

        // --- Node Press Animation State ---
        private const float NODE_PRESS_SCALE_TARGET = 0.85f; // Scale down when held
        private const float NODE_PRESS_SPEED = 20.0f; // Fast squash
        private readonly Dictionary<int, float> _nodePressTimers = new Dictionary<int, float>();

        public SplitMapScene()
        {
            _progressionManager = ServiceLocator.Get<ProgressionManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _diceRollingSystem = ServiceLocator.Get<DiceRollingSystem>();
            _playerIcon = new PlayerMapIcon();
            _narrativeDialog = new NarrativeDialog(this);
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _inventoryOverlay = new SplitMapInventoryOverlay();
            _shopOverlay = new SplitMapShopOverlay();
            _restOverlay = new SplitMapRestOverlay(this);
            _recruitOverlay = new SplitMapRecruitOverlay(this);
            _birdManager = new BirdManager();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();

            var narratorBounds = new Rectangle(0, Global.VIRTUAL_HEIGHT - 50, Global.VIRTUAL_WIDTH, 50);
            _resultNarrator = new StoryNarrator(narratorBounds);
            _resultNarrator.OnFinished += OnResultNarrationFinished;

            _voidEdgeEffect = new VoidEdgeEffect(
                edgeColor: _global.Palette_Black,
                edgeWidth: 6,
                noiseScale: 0.1f,
                noiseSpeed: 3f
            );

            _inventoryOverlay.OnInventoryButtonClicked += () =>
            {
                if (_currentView == SplitMapView.Inventory)
                {
                    _hapticsManager.TriggerZoomPulse(0.995f, 0.1f);
                    SetView(_viewToReturnTo, snap: true);
                }
                else
                {
                    _hapticsManager.TriggerZoomPulse(1.005f, 0.1f);
                    _viewToReturnTo = _currentView;
                    SetView(SplitMapView.Inventory, snap: true);
                }
            };

            _shopOverlay.OnLeaveRequested += () =>
            {
                _hapticsManager.TriggerZoomPulse(1.01f, 0.1f);
                _transitionManager.StartTransition(TransitionType.Diamonds, TransitionType.Diamonds, () =>
                {
                    _viewToReturnTo = SplitMapView.Map;
                    SetView(SplitMapView.Map, snap: true);

                    var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (currentNode != null)
                    {
                        currentNode.IsCompleted = true;
                        UpdateCameraTarget(currentNode.Position, false);
                    }
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                });
            };

            _restOverlay.OnLeaveRequested += () =>
            {
                _hapticsManager.TriggerZoomPulse(1.01f, 0.1f);
                _transitionManager.StartTransition(TransitionType.Diamonds, TransitionType.Diamonds, () =>
                {
                    _viewToReturnTo = SplitMapView.Map;
                    SetView(SplitMapView.Map, snap: true);

                    var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (currentNode != null)
                    {
                        currentNode.IsCompleted = true;
                        UpdateCameraTarget(currentNode.Position, false);
                    }
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                });
            };

            _restOverlay.OnRestCompleted += () =>
            {
                _transitionManager.StartTransition(TransitionType.Diamonds, TransitionType.Diamonds, () =>
                {
                    _viewToReturnTo = SplitMapView.Map;
                    SetView(SplitMapView.Map, snap: true);

                    var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (currentNode != null)
                    {
                        currentNode.IsCompleted = true;
                        UpdateCameraTarget(currentNode.Position, false);
                    }
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                });
            };

            _recruitOverlay.OnRecruitComplete += () =>
            {
                _hapticsManager.TriggerZoomPulse(1.01f, 0.1f);
                _transitionManager.StartTransition(TransitionType.Diamonds, TransitionType.Diamonds, () =>
                {
                    _viewToReturnTo = SplitMapView.Map;
                    SetView(SplitMapView.Map, snap: true);

                    var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (currentNode != null)
                    {
                        currentNode.IsCompleted = true;
                        UpdateCameraTarget(currentNode.Position, false);
                    }
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                });
            };
        }

        public override Rectangle GetAnimatedBounds() => new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

        public override void Enter()
        {
            base.Enter();
            _playerIcon.SetIsMoving(false);
            _diceRollingSystem.OnRollCompleted += OnDiceRollCompleted;
            _isPanning = false;
            _waitingForCombatCameraSettle = false;
            _pendingCombatArchetypes = null;
            _viewToReturnTo = SplitMapView.Map;
            _nodeTextWaveTimer = 0f; // Reset wave timer
            _nodeHoverTimers.Clear(); // Reset hover timers
            _nodePressTimers.Clear(); // Reset press timers
            _nodeArrivalScale = Vector2.One;
            _nodeArrivalShake = Vector2.Zero;
            _selectedNodeId = -1; // Reset selection
            _lastHoveredNodeId = -1; // Reset hover tracking
            _pressedNodeId = -1; // Reset press tracking

            _inventoryOverlay.Initialize();

            InitializeSettingsButton();

            // Trigger the settings button animation
            _settingsButtonState = SettingsButtonState.AnimatingIn;
            _settingsButtonAnimTimer = 0f;

            if (_progressionManager.CurrentSplitMap == null)
            {
                _mapState = SplitMapState.Idle;
                _eventState = EventState.Idle;
                _playerMoveTimer = 0f;
                _playerMoveDuration = 0f;
                _playerMoveTargetNodeId = -1;
                _playerMovePath = null;
                _hoveredNodeId = -1;
                _pendingChoiceForDiceRoll = null;
                _postEventDelayTimer = 0f;
                _nodeLiftTimer = 0f;
                _pulseTimer = 0f;

                _progressionManager.GenerateNewSplitMap();
                _currentMap = _progressionManager.CurrentSplitMap;
                _playerCurrentNodeId = _currentMap?.StartNodeId ?? -1;
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

                    if (_currentMap != null)
                        _birdManager.Initialize(_currentMap, _playerIcon.Position);
                }
                SetView(SplitMapView.Map, snap: true);
            }
            else
            {
                _currentMap = _progressionManager.CurrentSplitMap;
                if (WasMajorBattle && PlayerWonLastBattle)
                {
                    WasMajorBattle = false;
                    var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (currentNode != null)
                    {
                        currentNode.IsCompleted = true;
                        UpdateCameraTarget(currentNode.Position, false);
                    }
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                }
                else
                {
                    var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (currentNode != null)
                    {
                        currentNode.IsCompleted = true;
                        UpdateCameraTarget(currentNode.Position, false);
                    }
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                }
                SetView(SplitMapView.Map, snap: true);

                if (_currentMap != null)
                    _birdManager.Initialize(_currentMap, _playerIcon.Position);
            }
        }

        public override void Exit()
        {
            base.Exit();
            _diceRollingSystem.OnRollCompleted -= OnDiceRollCompleted;
            if (BattleSetup.ReturnSceneState != GameSceneState.Split)
            {
                _progressionManager.ClearCurrentSplitMap();
            }
        }

        private void InitializeSettingsButton()
        {
            int buttonSize = 16;
            int offScreenX = Global.VIRTUAL_WIDTH + 20; // Safe off-screen position

            if (_settingsButton == null)
            {
                var sheet = _spriteManager.SplitMapSettingsButton;
                var rects = _spriteManager.SplitMapSettingsButtonSourceRects;

                // Initialize off-screen
                _settingsButton = new ImageButton(new Rectangle(offScreenX, 2, buttonSize, buttonSize), sheet, rects[0], rects[1], enableHoverSway: true)
                {
                    UseScreenCoordinates = false,
                    TriggerHapticOnHover = true // Enable UI Shake
                };
            }

            // Always reset position to off-screen on initialization/re-entry
            _settingsButton.Bounds = new Rectangle(offScreenX, 2, buttonSize, buttonSize);

            _settingsButton.OnClick = null;
            _settingsButton.OnClick += () =>
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                OpenSettings();
            };
            _settingsButton.ResetAnimationState();
        }

        private void SetView(SplitMapView view, bool snap = true)
        {
            _currentView = view;
            _isPanning = false;
            _cameraVelocity = Vector2.Zero;
            _snapBackDelayTimer = 0f;

            if (_settingsButton != null)
            {
                _settingsButton.SetSprites(
                    _spriteManager.SplitMapSettingsButton,
                    _spriteManager.SplitMapSettingsButtonSourceRects[0],
                    _spriteManager.SplitMapSettingsButtonSourceRects[1]
                );
            }

            switch (_currentView)
            {
                case SplitMapView.Map:
                    _inventoryOverlay.Hide();
                    _shopOverlay.Hide();
                    _restOverlay.Hide();
                    _recruitOverlay.Hide();
                    var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (currentNode != null)
                    {
                        UpdateCameraTarget(currentNode.Position, snap);
                    }
                    break;
                case SplitMapView.Inventory:
                    _inventoryOverlay.Show();
                    _shopOverlay.Hide();
                    _restOverlay.Hide();
                    _recruitOverlay.Hide();
                    _targetCameraOffset = new Vector2(0, -200);
                    if (snap) _cameraOffset = _targetCameraOffset;
                    break;
                case SplitMapView.Shop:
                    _inventoryOverlay.Hide();
                    _shopOverlay.Resume();
                    _restOverlay.Hide();
                    _recruitOverlay.Hide();
                    _targetCameraOffset = new Vector2(0, -600);
                    if (snap) _cameraOffset = _targetCameraOffset;
                    break;
                case SplitMapView.Rest:
                    _inventoryOverlay.Hide();
                    _shopOverlay.Hide();
                    _restOverlay.Show();
                    _recruitOverlay.Hide();
                    _targetCameraOffset = new Vector2(0, -600);
                    if (snap) _cameraOffset = _targetCameraOffset;
                    break;
                case SplitMapView.Recruit:
                    _inventoryOverlay.Hide();
                    _shopOverlay.Hide();
                    _restOverlay.Hide();
                    _recruitOverlay.Show();
                    _targetCameraOffset = new Vector2(0, -600);
                    if (snap) _cameraOffset = _targetCameraOffset;
                    break;
            }
        }

        private void UpdateReachableNodes()
        {
            if (_currentMap == null || !_currentMap.Nodes.TryGetValue(_playerCurrentNodeId, out var currentNode)) return;

            var reachableNodeIds = new HashSet<int>();
            foreach (var pathId in currentNode.OutgoingPathIds)
            {
                if (_currentMap.Paths.TryGetValue(pathId, out var path))
                {
                    reachableNodeIds.Add(path.ToNodeId);
                }
            }

            foreach (var (nodeId, node) in _currentMap.Nodes)
            {
                node.IsReachable = reachableNodeIds.Contains(nodeId);
            }
        }

        private void StartPlayerMove(int targetNodeId)
        {
            if (_currentMap == null) return;

            _playerMovePath = _currentMap.Paths.Values.FirstOrDefault(p => p.FromNodeId == _playerCurrentNodeId && p.ToNodeId == targetNodeId);
            if (_playerMovePath == null) return;

            float pathLength = 0f;
            if (_playerMovePath.PixelPoints.Count > 1)
            {
                for (int i = 0; i < _playerMovePath.PixelPoints.Count - 1; i++)
                {
                    pathLength += Vector2.Distance(_playerMovePath.PixelPoints[i].ToVector2(), _playerMovePath.PixelPoints[i + 1].ToVector2());
                }
            }
            _playerMoveDuration = (pathLength > 0) ? pathLength / PLAYER_MOVE_SPEED : 0f;

            _mapState = SplitMapState.PlayerMoving;
            _playerIcon.SetIsMoving(true);
            _playerMoveTimer = 0f;
            _playerMoveTargetNodeId = targetNodeId;

            var currentNode = _currentMap.Nodes[_playerCurrentNodeId];
            var reachableNodeIds = currentNode.OutgoingPathIds
                .Select(pathId => _currentMap.Paths[pathId].ToNodeId)
                .ToList();

            foreach (var nodeId in reachableNodeIds)
            {
                if (nodeId != targetNodeId)
                {
                    _currentMap.Nodes[nodeId].IsReachable = false;
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_inputBlockTimer > 0)
            {
                _inputBlockTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            if (IsInputBlocked)
            {
                base.Update(gameTime);
                return;
            }

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();

            var viewAtStartOfFrame = _currentView;

            _voidEdgeEffect.Update(gameTime, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _cameraOffset);

            _birdManager.Update(gameTime, _currentMap, _playerIcon.Position, _cameraOffset);

            if (_narrativeDialog.IsActive || _sceneManager.IsModalActive)
            {
                if (_narrativeDialog.IsActive)
                {
                    _narrativeDialog.Update(gameTime);
                }
                _wasModalActiveLastFrame = true;
                base.Update(gameTime);
                return;
            }

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

            if (_eventState == EventState.AwaitingDiceRoll)
            {
                base.Update(gameTime);
                return;
            }
            if (_eventState == EventState.NarratingResult)
            {
                _resultNarrator.Update(gameTime);
                base.Update(gameTime);
                return;
            }

            // Use _cameraOffset directly for smooth panning
            var cameraTransform = Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0);

            bool isRestNarrating = _restOverlay.IsNarrating;
            bool allowInventoryInteraction = !isRestNarrating && (_mapState == SplitMapState.Idle || _currentView == SplitMapView.Shop || _currentView == SplitMapView.Rest || _currentView == SplitMapView.Recruit);

            _inventoryOverlay.Update(gameTime, currentMouseState, currentKeyboardState, allowInventoryInteraction, cameraTransform);

            if (!isRestNarrating)
            {
                // Removed _settingsOverlay.Update
                _shopOverlay.Update(gameTime, currentMouseState, cameraTransform);
                _recruitOverlay.Update(gameTime, currentMouseState, cameraTransform);
            }

            _restOverlay.Update(gameTime, currentMouseState, cameraTransform);

            // Update Settings Button Animation & Logic
            if (_settingsButton != null && !isRestNarrating)
            {
                if (_settingsButtonState == SettingsButtonState.AnimatingIn)
                {
                    _settingsButtonAnimTimer += deltaTime;
                    float progress = Math.Clamp(_settingsButtonAnimTimer / SETTINGS_BUTTON_ANIM_DURATION, 0f, 1f);
                    float eased = Easing.EaseOutBack(progress);

                    float startX = Global.VIRTUAL_WIDTH + 20;
                    float targetX = Global.VIRTUAL_WIDTH - 16 - 2; // 16 size, 2 padding

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

                // Only update input if visible
                if (_settingsButtonState != SettingsButtonState.Hidden)
                {
                    _settingsButton.Update(currentMouseState);
                }
            }

            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState) && !isRestNarrating)
            {
                if (_currentView == SplitMapView.Inventory)
                {
                    SetView(_viewToReturnTo, snap: true);
                }
                else
                {
                    OpenSettings();
                }
            }

            if (_currentView == SplitMapView.Map)
            {
                if (!_isPanning)
                {
                    if (_cameraVelocity.LengthSquared() > 0.1f)
                    {
                        _cameraOffset += _cameraVelocity;
                        float frictionDamping = 1.0f - MathF.Exp(-PAN_FRICTION * deltaTime);
                        _cameraVelocity = Vector2.Lerp(_cameraVelocity, Vector2.Zero, frictionDamping);
                        ClampCameraOffset();

                        // Sync target so the main lerp doesn't pull us back
                        _targetCameraOffset.X = _cameraOffset.X;

                        _snapBackDelayTimer = SNAP_BACK_DELAY;
                    }
                    else
                    {
                        _cameraVelocity = Vector2.Zero;

                        // Settle Logic (Only if waiting to snap back)
                        if (_snapBackDelayTimer > 0)
                        {
                            _snapBackDelayTimer -= deltaTime;

                            // Settle to nearest pixel
                            float currentX = _cameraOffset.X;
                            float targetPixelX = MathF.Round(currentX);

                            // Use a fast damping to snap
                            float snapDamping = 1.0f - MathF.Exp(-15f * deltaTime);
                            _cameraOffset.X = MathHelper.Lerp(currentX, targetPixelX, snapDamping);

                            // Snap if very close
                            if (Math.Abs(_cameraOffset.X - targetPixelX) < 0.01f)
                            {
                                _cameraOffset.X = targetPixelX;
                            }

                            _targetCameraOffset.X = _cameraOffset.X;

                            if (_snapBackDelayTimer <= 0)
                            {
                                var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                                if (currentNode != null)
                                {
                                    UpdateCameraTarget(currentNode.Position, false);
                                }
                            }
                        }
                    }
                }
            }

            // FIX: Use Time-Corrected Damping for camera movement
            float cameraDamping = 1.0f - MathF.Exp(-CAMERA_LERP_SPEED * deltaTime);
            _cameraOffset = Vector2.Lerp(_cameraOffset, _targetCameraOffset, cameraDamping);

            if (_waitingForCombatCameraSettle)
            {
                if (Vector2.Distance(_cameraOffset, _targetCameraOffset) < CAMERA_SETTLE_THRESHOLD)
                {
                    if (_framesToWaitAfterSettle > 0)
                    {
                        _framesToWaitAfterSettle--;
                    }
                    else
                    {
                        ExecuteCombatTransition();
                    }
                }
            }

            var appearingKeys = _pathAnimationProgress.Keys.ToList();
            foreach (var pathId in appearingKeys)
            {
                float duration = _pathAnimationDurations.GetValueOrDefault(pathId, PATH_ANIMATION_DURATION);
                if (_pathAnimationProgress[pathId] < duration)
                {
                    _pathAnimationProgress[pathId] += deltaTime;
                }
            }

            if (_currentView == SplitMapView.Map)
            {
                HandleMapInput(gameTime);
            }
            else
            {
                _hoveredNodeId = -1;
                _pressedNodeId = -1; // Reset press state if view changes
            }

            // --- UPDATE NODE HOVER & PRESS TIMERS ---
            UpdateNodeAnimationTimers(deltaTime);

            // --- UPDATE SELECTION ANIMATION ---
            if (_selectedNodeId != -1)
            {
                _nodeSelectionAnimTimer += deltaTime;
            }

            switch (_mapState)
            {
                case SplitMapState.PlayerMoving:
                    UpdatePlayerMove(deltaTime);
                    break;
                case SplitMapState.LiftingNode:
                    UpdateLiftingNode(deltaTime);
                    break;
                case SplitMapState.PulsingNode:
                    UpdatePulsingNode(deltaTime);
                    break;
                case SplitMapState.LoweringNode:
                    UpdateLoweringNode(deltaTime);
                    break;
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

            _playerIcon.Update(gameTime);
            base.Update(gameTime);
        }

        private void UpdateNodeAnimationTimers(float dt)
        {
            if (_currentMap != null)
            {
                foreach (var node in _currentMap.Nodes.Values)
                {
                    // --- HOVER TIMER ---
                    if (!_nodeHoverTimers.ContainsKey(node.Id)) _nodeHoverTimers[node.Id] = 0f;

                    // Treat as hovered if it is the actual hovered node OR if it is the selected node
                    bool isHovered = (node.Id == _hoveredNodeId) || (node.Id == _selectedNodeId);
                    float hoverChange = dt * NODE_HOVER_POP_SPEED;

                    if (isHovered)
                        _nodeHoverTimers[node.Id] = Math.Min(_nodeHoverTimers[node.Id] + hoverChange, 1f);
                    else
                        _nodeHoverTimers[node.Id] = Math.Max(_nodeHoverTimers[node.Id] - hoverChange, 0f);

                    // --- PRESS TIMER ---
                    if (!_nodePressTimers.ContainsKey(node.Id)) _nodePressTimers[node.Id] = 0f;

                    bool isPressed = (node.Id == _pressedNodeId) && (node.Id == _hoveredNodeId);
                    float pressChange = dt * NODE_PRESS_SPEED;

                    if (isPressed)
                        _nodePressTimers[node.Id] = Math.Min(_nodePressTimers[node.Id] + pressChange, 1f);
                    else
                        _nodePressTimers[node.Id] = Math.Max(_nodePressTimers[node.Id] - pressChange, 0f);
                }
            }
        }

        private void HandleMapInput(GameTime gameTime)
        {
            var cursorManager = ServiceLocator.Get<CursorManager>();
            var currentMouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            // Use _cameraOffset directly for smooth panning
            var cameraTransform = Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0);
            Matrix.Invert(ref cameraTransform, out var inverseCameraTransform);
            var mouseInMapSpace = Vector2.Transform(virtualMousePos, inverseCameraTransform);

            // 1. Determine Raw Hover (Geometry Check)
            int rawHoveredNodeId = -1;

            // FIX: Only allow hover detection if state is Idle
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

            // 2. Check UI Hover
            bool hoveringButtons = (_inventoryOverlay.IsHovered || (_settingsButton?.IsHovered ?? false));

            // 3. Handle Panning (Updates _isPanning)
            if (!hoveringButtons)
            {
                // Pass rawHoveredNodeId so we don't start panning if clicking a node
                HandleCameraPan(currentMouseState, virtualMousePos, rawHoveredNodeId);
            }

            // 4. Determine Final Hover ID
            // If panning, we suppress hover.
            if (_isPanning)
            {
                _hoveredNodeId = -1;
            }
            else
            {
                _hoveredNodeId = rawHoveredNodeId;
            }

            // 5. Haptics
            // Explicitly check !_isPanning to be safe
            if (!_isPanning && _hoveredNodeId != -1 && _hoveredNodeId != _lastHoveredNodeId)
            {
                // _hapticsManager.TriggerUICompoundShake(_global.HoverHapticStrength); // disabled for now; I dont like how it looks
            }
            _lastHoveredNodeId = _hoveredNodeId;

            // 6. Cursor State
            if (_isPanning)
            {
                cursorManager.SetState(CursorState.Dragging);
            }
            else if (_hoveredNodeId != -1)
            {
                cursorManager.SetState(CursorState.HoverClickable);
            }
            else if (_mapState == SplitMapState.Idle)
            {
                cursorManager.SetState(CursorState.HoverDraggable);
            }

            // 7. Click Logic (Press and Release)
            if (!_isPanning && _mapState == SplitMapState.Idle && !hoveringButtons && UIInputManager.CanProcessMouseClick() && _currentView == SplitMapView.Map)
            {
                bool leftClickPressed = currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
                bool leftClickReleased = currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

                // On Press: Record the node we started clicking on
                if (leftClickPressed)
                {
                    if (_hoveredNodeId != -1)
                    {
                        _pressedNodeId = _hoveredNodeId;
                        // Do NOT consume click here, wait for release
                    }
                }

                // On Release: Check if we are still hovering the SAME node we pressed
                if (leftClickReleased)
                {
                    if (_pressedNodeId != -1 && _pressedNodeId == _hoveredNodeId)
                    {
                        // Valid Click! Trigger Selection.
                        var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                        if (currentNode != null)
                        {
                            UpdateCameraTarget(currentNode.Position, false);
                        }
                        _snapBackDelayTimer = 0f;
                        _cameraVelocity = Vector2.Zero;

                        // --- SELECTION LOGIC ---
                        _selectedNodeId = _hoveredNodeId;
                        _nodeSelectionAnimTimer = 0f;
                        _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); // Small haptic feedback

                        StartPlayerMove(_hoveredNodeId);
                        _hoveredNodeId = -1;
                        UIInputManager.ConsumeMouseClick();
                    }

                    // Always reset press state on release
                    _pressedNodeId = -1;
                }
            }
            else
            {
                // If panning started or state changed, cancel any pending press
                _pressedNodeId = -1;
            }
        }

        private void HandleCameraPan(MouseState currentMouseState, Vector2 virtualMousePos, int rawHoveredNodeId)
        {
            if (_currentView != SplitMapView.Map)
            {
                _isPanning = false;
                return;
            }

            bool leftClickPressed = currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickHeld = currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickReleased = currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            int scrollDelta = currentMouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0 && _mapState == SplitMapState.Idle)
            {
                _cameraVelocity.X -= Math.Sign(scrollDelta) * SCROLL_PAN_SPEED;
                _targetCameraOffset.X = _cameraOffset.X;
                _snapBackDelayTimer = SNAP_BACK_DELAY;
            }

            // Only start panning if we are NOT hovering a node
            if (leftClickPressed && rawHoveredNodeId == -1 && _mapState == SplitMapState.Idle && UIInputManager.CanProcessMouseClick())
            {
                _isPanning = true;
                _panStartMousePosition = currentMouseState.Position;
                _lastPanMousePosition = currentMouseState.Position;
                _panStartCameraOffset = _cameraOffset;
                _cameraVelocity = Vector2.Zero;
                UIInputManager.ConsumeMouseClick();
            }

            if (leftClickReleased)
            {
                if (_isPanning)
                {
                    _isPanning = false;
                }
            }

            if (_isPanning && leftClickHeld)
            {
                if (_mapState != SplitMapState.Idle)
                {
                    _isPanning = false;
                    return;
                }

                _snapBackDelayTimer = SNAP_BACK_DELAY;

                // Use raw screen delta scaled down for smooth panning
                float scale = ServiceLocator.Get<Core>().FinalScale;
                Vector2 screenDelta = (currentMouseState.Position - _lastPanMousePosition).ToVector2();
                Vector2 virtualDelta = screenDelta / scale;

                _cameraVelocity.X = virtualDelta.X * PAN_SENSITIVITY;
                _cameraVelocity.Y = 0;

                _cameraOffset.X += _cameraVelocity.X;
                ClampCameraOffset();

                _targetCameraOffset.X = _cameraOffset.X;
                _lastPanMousePosition = currentMouseState.Position;
            }
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
                else
                {
                    _cameraOffset.X = (screenWidth - mapContentWidth) / 2;
                }
            }
        }

        private void UpdatePlayerMove(float deltaTime)
        {
            _playerMoveTimer += deltaTime;
            float progress = _playerMoveDuration > 0 ? Math.Clamp(_playerMoveTimer / _playerMoveDuration, 0f, 1f) : 1f;

            if (_playerMovePath != null && _playerMovePath.PixelPoints.Any())
            {
                int targetIndex = (int)Math.Clamp(progress * (_playerMovePath.PixelPoints.Count - 1), 0, _playerMovePath.PixelPoints.Count - 1);
                _playerIcon.SetPosition(_playerMovePath.PixelPoints[targetIndex].ToVector2());
            }

            if (progress >= 1f)
            {
                _playerIcon.SetIsMoving(false);
                if (_playerMovePath != null)
                {
                    _traversedPathIds.Add(_playerMovePath.Id);
                }
                _playerCurrentNodeId = _playerMoveTargetNodeId;
                _visitedNodeIds.Add(_playerCurrentNodeId);

                // --- RESET SELECTION ON ARRIVAL ---
                _selectedNodeId = -1;

                var toNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (toNode != null)
                {
                    _playerIcon.SetPosition(toNode.Position);
                }

                _mapState = SplitMapState.LiftingNode;
                _nodeLiftTimer = 0f;
            }
        }

        private void UpdateLiftingNode(float deltaTime)
        {
            _nodeLiftTimer += deltaTime;
            var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
            if (currentNode != null)
            {
                float progress = Math.Clamp(_nodeLiftTimer / NODE_LIFT_DURATION, 0f, 1f);
                float eased = Easing.EaseOutBack(progress); // Use EaseOutBack for jump feel

                // Lift
                currentNode.VisualOffset = new Vector2(0, MathHelper.Lerp(0, -NODE_LIFT_AMOUNT, eased));

                // Stretch (Taller, Thinner)
                // Scale X: 1.0 -> 0.8 -> 1.0
                // Scale Y: 1.0 -> 1.2 -> 1.0
                float stretch = MathF.Sin(progress * MathHelper.Pi); // 0 -> 1 -> 0
                _nodeArrivalScale = new Vector2(1.0f - (stretch * 0.2f), 1.0f + (stretch * 0.2f));
            }

            if (_nodeLiftTimer >= NODE_LIFT_DURATION)
            {
                _mapState = SplitMapState.PulsingNode;
                _pulseTimer = 0f;
                _nodeArrivalScale = Vector2.One; // Reset scale for next phase
            }
        }

        private void UpdatePulsingNode(float deltaTime)
        {
            _pulseTimer += deltaTime;
            float progress = Math.Clamp(_pulseTimer / PULSE_DURATION, 0f, 1f);

            // Elastic Pop (Big -> Normal)
            float elastic = Easing.EaseOutElastic(progress);
            // Map 0..1 to 1.4..1.0
            float scale = 1.0f + (0.4f * (1.0f - elastic));
            _nodeArrivalScale = new Vector2(scale);

            // Violent Shake
            float shakeDecay = 1.0f - progress;
            float shakeX = MathF.Sin(_pulseTimer * NODE_ARRIVAL_SHAKE_FREQUENCY) * NODE_ARRIVAL_SHAKE_MAGNITUDE * shakeDecay;
            float shakeY = MathF.Cos(_pulseTimer * NODE_ARRIVAL_SHAKE_FREQUENCY * 0.9f) * NODE_ARRIVAL_SHAKE_MAGNITUDE * shakeDecay;
            _nodeArrivalShake = new Vector2(shakeX, shakeY);

            if (_pulseTimer >= PULSE_DURATION)
            {
                _mapState = SplitMapState.EventInProgress;
                _nodeArrivalScale = Vector2.One;
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
                float eased = Easing.EaseOutBounce(progress); // Bounce on landing

                // Lower
                currentNode.VisualOffset = new Vector2(0, MathHelper.Lerp(-NODE_LIFT_AMOUNT, 0, eased));

                // Squash on impact (only at the very end of the bounce)
                if (progress > 0.8f)
                {
                    float squashProgress = (progress - 0.8f) / 0.2f;
                    float squash = MathF.Sin(squashProgress * MathHelper.Pi); // 0 -> 1 -> 0
                    _nodeArrivalScale = new Vector2(1.0f + (squash * 0.3f), 1.0f - (squash * 0.3f));
                }
                else
                {
                    _nodeArrivalScale = Vector2.One;
                }
            }

            if (_nodeLiftTimer >= NODE_LOWERING_DURATION)
            {
                if (currentNode != null)
                {
                    currentNode.VisualOffset = Vector2.Zero;
                    UpdateCameraTarget(currentNode.Position, false);
                }
                _nodeArrivalScale = Vector2.One;
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
            else
            {
                targetX = (screenWidth - mapContentWidth) / 2;
            }

            // Round target for pixel-perfect final position
            _targetCameraOffset = new Vector2(MathF.Round(targetX), 0);

            if (snap)
            {
                _cameraOffset = _targetCameraOffset;
            }
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

            if (_currentView != SplitMapView.Map)
            {
                SetView(SplitMapView.Map, snap: true);
                _waitingForCombatCameraSettle = true;
                _framesToWaitAfterSettle = 1;
            }
            else
            {
                ExecuteCombatTransition();
            }
        }

        private void ExecuteCombatTransition()
        {
            _waitingForCombatCameraSettle = false;
            BattleSetup.EnemyArchetypes = _pendingCombatArchetypes;
            BattleSetup.ReturnSceneState = GameSceneState.Split;

            // Use a random combat transition (Diamonds, Shutters, Blocks)
            var transitionType = _transitionManager.GetRandomCombatTransition();
            // Use the same transition for In and Out for consistency
            _sceneManager.ChangeScene(GameSceneState.Battle, transitionType, transitionType, 0.5f);

            _pendingCombatArchetypes = null;
        }

        private void TriggerNodeEvent(int nodeId)
        {
            if (_currentMap == null || !_currentMap.Nodes.TryGetValue(nodeId, out var node)) return;

            _mapState = SplitMapState.EventInProgress;

            switch (node.NodeType)
            {
                case SplitNodeType.Battle:
                case SplitNodeType.MajorBattle:
                    WasMajorBattle = node.NodeType == SplitNodeType.MajorBattle;
                    InitiateCombat(node.EventData as List<string> ?? new List<string>());
                    break;

                case SplitNodeType.Narrative:
                    if (node.EventData is string narrativeEventId)
                    {
                        var narrativeEvent = _progressionManager.GetNarrativeEvent(narrativeEventId);
                        if (narrativeEvent != null)
                        {
                            _narrativeDialog.Show(narrativeEvent, OnNarrativeChoiceSelected);
                            _wasModalActiveLastFrame = true;
                        }
                        else
                        {
                            var fallbackEvent = _progressionManager.GetRandomNarrative();
                            if (fallbackEvent != null)
                            {
                                Debug.WriteLine($"[SplitMapScene] Recovered from missing event ID '{narrativeEventId}' by using '{fallbackEvent.EventID}'.");
                                _narrativeDialog.Show(fallbackEvent, OnNarrativeChoiceSelected);
                                _wasModalActiveLastFrame = true;
                            }
                            else
                            {
                                Debug.WriteLine($"[SplitMapScene] [ERROR] Failed to recover missing event ID '{narrativeEventId}'. Skipping node.");
                                node.IsCompleted = true;
                                UpdateCameraTarget(node.Position, false);
                                _mapState = SplitMapState.LoweringNode;
                                _nodeLiftTimer = 0f;
                            }
                        }
                    }
                    else
                    {
                        node.IsCompleted = true;
                        UpdateCameraTarget(node.Position, false);
                        _mapState = SplitMapState.LoweringNode;
                        _nodeLiftTimer = 0f;
                    }
                    break;

                case SplitNodeType.Recruit:
                    _transitionManager.StartTransition(TransitionType.Diamonds, TransitionType.Diamonds, () =>
                    {
                        _recruitOverlay.GenerateNewCandidates();
                        SetView(SplitMapView.Recruit, snap: true);
                    });
                    break;

                case SplitNodeType.Rest:
                    _transitionManager.StartTransition(TransitionType.Diamonds, TransitionType.Diamonds, () =>
                    {
                        SetView(SplitMapView.Rest, snap: true);
                    });
                    break;

                case SplitNodeType.Shop:
                    _transitionManager.StartTransition(TransitionType.Diamonds, TransitionType.Diamonds, () => OpenRandomShop());
                    break;

                default:
                    node.IsCompleted = true;
                    UpdateCameraTarget(node.Position, false);
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                    break;
            }
        }

        public void DebugTriggerShop()
        {
            OpenRandomShop();
        }

        public void DebugTriggerRest()
        {
            SetView(SplitMapView.Rest, snap: true);
        }

        public void DebugTriggerRecruit()
        {
            _recruitOverlay.GenerateNewCandidates();
            SetView(SplitMapView.Recruit, snap: true);
        }

        private void OpenRandomShop()
        {
            var premiumStock = new List<ShopItem>();

            int premiumCount = _random.Next(3, 5);
            var allPremium = new List<ShopItem>();

            foreach (var w in BattleDataCache.Weapons.Values) allPremium.Add(new ShopItem { ItemId = w.WeaponID, DisplayName = w.WeaponName, Type = "Weapon", Price = PriceCalculator.CalculatePrice(w, 1.0f), DataObject = w });
            foreach (var r in BattleDataCache.Relics.Values) allPremium.Add(new ShopItem { ItemId = r.RelicID, DisplayName = r.RelicName, Type = "Relic", Price = PriceCalculator.CalculatePrice(r, 1.0f), DataObject = r });

            for (int i = 0; i < premiumCount; i++)
            {
                if (allPremium.Any())
                {
                    var item = allPremium[_random.Next(allPremium.Count)];
                    premiumStock.Add(item);
                    allPremium.Remove(item);
                }
            }

            _shopOverlay.Show(premiumStock);
            SetView(SplitMapView.Shop, snap: true);
        }

        private void OnNarrativeChoiceSelected(NarrativeChoice choice)
        {
            if (string.IsNullOrEmpty(choice.Dice))
            {
                ResolveNarrativeChoice(choice, -1);
            }
            else
            {
                _eventState = EventState.AwaitingDiceRoll;
                _pendingChoiceForDiceRoll = choice;
                var (numDice, numSides, modifier) = DiceParser.Parse(choice.Dice);
                var dieType = numSides == 4 ? DieType.D4 : DieType.D6;

                _diceRollingSystem.Roll(new List<DiceGroup>
    {
        new DiceGroup
        {
            GroupId = "narrative_check",
            NumberOfDice = numDice,
            DieType = dieType,
            ResultProcessing = DiceResultProcessing.Sum,
            Modifier = modifier,
            Tint = Color.White,
            AnimateSum = false,
            ShowResultText = false
        }
    });
            }
        }

        private void OnDiceRollCompleted(DiceRollResult result)
        {
            if (_eventState != EventState.AwaitingDiceRoll || _pendingChoiceForDiceRoll == null) return;

            if (result.ResultsByGroup.TryGetValue("narrative_check", out var values) && values.Any())
            {
                int rollResult = values.First();
                ResolveNarrativeChoice(_pendingChoiceForDiceRoll, rollResult);
            }

            _pendingChoiceForDiceRoll = null;
        }

        private void ResolveNarrativeChoice(NarrativeChoice choice, int diceRoll)
        {
            var possibleOutcomes = choice.Outcomes;

            if (diceRoll != -1)
            {
                possibleOutcomes = choice.Outcomes
                    .Where(o => o.DifficultyClass == null || o.DifficultyClass.Contains(diceRoll))
                    .ToList();
            }

            WeightedOutcome? selectedOutcome = null;
            if (possibleOutcomes.Any())
            {
                int totalWeight = possibleOutcomes.Sum(o => o.Weight);
                if (totalWeight > 0)
                {
                    int roll = _random.Next(totalWeight);
                    foreach (var outcome in possibleOutcomes)
                    {
                        if (roll < outcome.Weight)
                        {
                            selectedOutcome = outcome;
                            break;
                        }
                        roll -= outcome.Weight;
                    }
                }
                else
                {
                    selectedOutcome = possibleOutcomes.FirstOrDefault();
                }
            }

            if (selectedOutcome != null)
            {
                var combatOutcome = selectedOutcome.Outcomes.FirstOrDefault(o => o.OutcomeType == "StartCombat");

                _gameState.ApplyNarrativeOutcomes(selectedOutcome.Outcomes);

                if (combatOutcome != null)
                {
                    InitiateCombat(new List<string> { combatOutcome.Value });
                    _eventState = EventState.Idle;
                    return;
                }

                if (!string.IsNullOrEmpty(selectedOutcome.ResultText))
                {
                    _eventState = EventState.NarratingResult;
                    _resultNarrator.Show(selectedOutcome.ResultText);
                }
                else
                {
                    OnResultNarrationFinished();
                }
            }
            else
            {
                OnResultNarrationFinished();
            }
        }

        private void OnResultNarrationFinished()
        {
            _eventState = EventState.Idle;
            var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
            if (currentNode != null)
            {
                currentNode.IsCompleted = true;
            }
            _mapState = SplitMapState.LoweringNode;
            _nodeLiftTimer = 0f;
            _resultNarrator.Clear();
        }

        private void FinalizeVictory()
        {
            SplitMapScene.PlayerWonLastBattle = true;
            DecrementTemporaryBuffs();

            // Use random transition
            var transition = _transitionManager.GetRandomTransition();
            _sceneManager.ChangeScene(BattleSetup.ReturnSceneState, transition, transition);
        }

        private void DecrementTemporaryBuffs()
        {
            var gameState = ServiceLocator.Get<GameState>();
            var buffsComp = _componentStore.GetComponent<TemporaryBuffsComponent>(gameState.PlayerEntityId);
            if (buffsComp == null) return;

            for (int i = buffsComp.Buffs.Count - 1; i >= 0; i--)
            {
                buffsComp.Buffs[i].RemainingBattles--;
                if (buffsComp.Buffs[i].RemainingBattles <= 0)
                {
                    buffsComp.Buffs.RemoveAt(i);
                }
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_currentMap == null) return;

            // Use _cameraOffset directly for smooth panning
            var cameraTransform = Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0);
            var finalTransform = cameraTransform * transform;

            // Calculate Scissor Rect for clipping map content to the virtual screen area
            var topLeft = Vector2.Transform(Vector2.Zero, transform);
            var bottomRight = Vector2.Transform(new Vector2(Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), transform);

            // Calculate AABB of the transformed corners to handle potential rotation from shake
            var tl = Vector2.Transform(Vector2.Zero, transform);
            var tr = Vector2.Transform(new Vector2(Global.VIRTUAL_WIDTH, 0), transform);
            var bl = Vector2.Transform(new Vector2(0, Global.VIRTUAL_HEIGHT), transform);
            var br = Vector2.Transform(new Vector2(Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), transform);

            var minX = Math.Min(Math.Min(tl.X, tr.X), Math.Min(bl.X, br.X));
            var maxX = Math.Max(Math.Max(tl.X, tr.X), Math.Max(bl.X, br.X));
            var minY = Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(bl.Y, br.Y));
            var maxY = Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(bl.Y, br.Y));

            var scissorRect = new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));

            // Clamp to viewport to prevent crashes if shake moves it off-screen
            var viewport = spriteBatch.GraphicsDevice.Viewport.Bounds;
            scissorRect = Rectangle.Intersect(scissorRect, viewport);

            // Apply Scissor
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;

            spriteBatch.End();
            // Use _scissorRasterizerState to enable clipping
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

                for (int x = startX; x <= endX; x += gridSize)
                {
                    spriteBatch.DrawLineSnapped(new Vector2(x, startY), new Vector2(x, endY), gridColor);
                }
                for (int y = startY; y <= endY; y += gridSize)
                {
                    spriteBatch.DrawLineSnapped(new Vector2(startX, y), new Vector2(endX, y), gridColor);
                }
            }

            var drawableObjects = new List<DrawableMapObject>();
            drawableObjects.AddRange(_currentMap.Nodes.Values.Select(n => new DrawableMapObject { Type = DrawableMapObject.ObjectType.Node, Position = n.Position, Data = n }));

            if (_currentMap.BakedSceneryTexture != null)
            {
                spriteBatch.DrawSnapped(_currentMap.BakedSceneryTexture, Vector2.Zero, Color.White);
            }

            drawableObjects.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));

            DrawAllPaths(spriteBatch, pixel);

            foreach (var obj in drawableObjects)
            {
                if (obj.Type == DrawableMapObject.ObjectType.Node)
                {
                    DrawNode(spriteBatch, (SplitMapNode)obj.Data!, gameTime);
                }
            }

            _playerIcon.Draw(spriteBatch);

            _birdManager.Draw(spriteBatch, _cameraOffset);

            _inventoryOverlay.DrawWorld(spriteBatch, font, gameTime);
            _shopOverlay.Draw(spriteBatch, font, gameTime);
            _restOverlay.Draw(spriteBatch, font, gameTime);

            // FIX: Pass cameraTransform to RecruitOverlay so it can calculate Virtual Screen Coords for tooltips
            _recruitOverlay.Draw(spriteBatch, font, gameTime, cameraTransform);

            spriteBatch.End();

            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

            var mapBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            _voidEdgeEffect.Draw(spriteBatch, mapBounds);

            _inventoryOverlay.DrawScreen(spriteBatch, font, gameTime, transform);
            _settingsButton?.Draw(spriteBatch, font, gameTime, transform);

            if (_hoveredNodeId != -1 && _mapState == SplitMapState.Idle && _currentView == SplitMapView.Map)
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
                        SplitNodeType.Narrative => "EVENT",
                        SplitNodeType.MajorBattle => "MAJOR BATTLE",
                        SplitNodeType.Recruit => "RECRUIT",
                        SplitNodeType.Rest => "REST",
                        SplitNodeType.Shop => "SHOP",
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(nodeText))
                    {
                        var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                        var nodeTextSize = secondaryFont.MeasureString(nodeText);

                        // Removed bob offset calculation
                        var textPosition = new Vector2((Global.VIRTUAL_WIDTH - nodeTextSize.Width) / 2f, Global.VIRTUAL_HEIGHT - nodeTextSize.Height - 3);

                        // --- NEW: Use TextWaveController for animation ---
                        bool isHovering = true; // We are inside the hover block

                        // Use the simple timer logic instead of the controller
                        _nodeTextWaveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                        // Use Wave effect
                        TextAnimator.DrawTextWithEffect(spriteBatch, secondaryFont, nodeText, textPosition, _global.Palette_DarkSun, TextEffectType.Wave, _nodeTextWaveTimer);
                    }
                }
            }
            else
            {
                // Reset timer when not hovering
                _nodeTextWaveTimer = 0f;
            }

            if (_narrativeDialog.IsActive) _narrativeDialog.DrawContent(spriteBatch, font, gameTime, transform);
            if (_eventState == EventState.NarratingResult) _resultNarrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);

            _restOverlay.DrawDialogContent(spriteBatch, font, gameTime);
            _recruitOverlay.DrawDialogContent(spriteBatch, font, gameTime);
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_narrativeDialog.IsActive)
            {
                _narrativeDialog.DrawOverlay(spriteBatch);
            }
            _restOverlay.DrawDialogOverlay(spriteBatch);
            _recruitOverlay.DrawDialogOverlay(spriteBatch);
        }

        private void DrawAllPaths(SpriteBatch spriteBatch, Texture2D pixel)
        {
            if (_currentMap == null) return;

            foreach (var path in _currentMap.Paths.Values)
            {
                DrawPath(spriteBatch, pixel, path, _global.Palette_DarkShadow, false);
            }

            SplitMapPath? highlightedPath = null;
            if (_hoveredNodeId != -1 && _mapState == SplitMapState.Idle)
            {
                highlightedPath = _currentMap.Paths.Values.FirstOrDefault(p => p.FromNodeId == _playerCurrentNodeId && p.ToNodeId == _hoveredNodeId);
            }

            foreach (var path in _currentMap.Paths.Values)
            {
                if (path == highlightedPath) continue;

                var fromNode = _currentMap.Nodes[path.FromNodeId];
                var toNode = _currentMap.Nodes[path.ToNodeId];

                bool isPathFromCurrentNode = fromNode.Id == _playerCurrentNodeId;
                bool isPathToReachableNode = toNode.IsReachable;
                bool isPathTraversed = _traversedPathIds.Contains(path.Id);
                bool isAnimating = isPathFromCurrentNode && isPathToReachableNode && !_visitedNodeIds.Contains(toNode.Id);

                if (isPathTraversed || isAnimating)
                {
                    // Use the new global color for traversed/active paths
                    DrawPath(spriteBatch, pixel, path, _global.SplitMapPathColor, isAnimating);
                }
            }

            if (highlightedPath != null)
            {
                DrawPath(spriteBatch, pixel, highlightedPath, _global.SplitMapNodeColor, false);
            }
        }

        private void DrawPath(SpriteBatch spriteBatch, Texture2D pixel, SplitMapPath path, Color pathColor, bool isAnimating)
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
            else
            {
                numPixelsToDraw = path.PixelPoints.Count;
            }

            if (numPixelsToDraw <= 0) return;

            // New Pattern Logic
            for (int i = 0; i < numPixelsToDraw; i++)
            {
                var point = path.PixelPoints[i];
                var pointVec = point.ToVector2();

                // Check distance to nodes for hard cropping
                float distFrom = Vector2.Distance(pointVec, fromNode.Position);
                float distTo = Vector2.Distance(pointVec, toNode.Position);

                // Hard crop
                if (distFrom <= PATH_EXCLUSION_RADIUS || distTo <= PATH_EXCLUSION_RADIUS)
                {
                    continue; // Skip drawing this pixel
                }

                // 2 pixels on, 2 pixels off
                // i % 4 returns 0, 1, 2, 3.
                // We want to draw on 0 and 1.
                if (i % 4 < 2)
                {
                    spriteBatch.Draw(pixel, pointVec, pathColor);
                }
            }
        }

        private void DrawNode(SpriteBatch spriteBatch, SplitMapNode node, GameTime gameTime)
        {
            // --- UPDATED: Get silhouette ---
            var (texture, silhouette, sourceRect, origin) = GetNodeDrawData(node, gameTime);

            var bounds = node.GetBounds();
            var color = _global.SplitMapNodeColor;
            float scale = 1.0f;

            if (node.IsCompleted)
            {
                color = _global.Palette_DarkShadow;
            }
            else if (node.NodeType != SplitNodeType.Origin && node.Id != _playerCurrentNodeId && !node.IsReachable)
            {
                color = _global.Palette_DarkShadow;
            }

            bool isSelected = (node.Id == _selectedNodeId);
            bool isHovered = (node.Id == _hoveredNodeId);
            bool isPressed = (node.Id == _pressedNodeId) && isHovered;

            // --- FIX: Force Red Color during Animation ---
            bool isAnimatingThisNode = (node.Id == _playerCurrentNodeId) &&
                                       (_mapState == SplitMapState.LiftingNode ||
                                        _mapState == SplitMapState.PulsingNode ||
                                        _mapState == SplitMapState.EventInProgress ||
                                        _mapState == SplitMapState.LoweringNode);

            if (node.IsReachable && (isHovered || isSelected || isAnimatingThisNode))
            {
                color = _global.ButtonHoverColor;
            }

            if (_mapState == SplitMapState.PulsingNode && node.Id == _playerCurrentNodeId)
            {
                float pulseProgress = Math.Clamp(_pulseTimer / PULSE_DURATION, 0f, 1f);
                float pulseWave = MathF.Sin(pulseProgress * MathF.PI);
                scale += pulseWave * PULSE_AMOUNT;
            }

            // --- HOVER POP ANIMATION ---
            float hoverT = _nodeHoverTimers.ContainsKey(node.Id) ? _nodeHoverTimers[node.Id] : 0f;

            // If selected, force the hover timer to max (1.0) so it stays popped up
            if (isSelected) hoverT = 1.0f;

            float popScale = 1.0f + (NODE_HOVER_POP_SCALE_TARGET - 1.0f) * Easing.EaseOutBack(hoverT);
            scale *= popScale;

            // --- PRESS SQUASH ANIMATION ---
            float pressT = _nodePressTimers.ContainsKey(node.Id) ? _nodePressTimers[node.Id] : 0f;
            if (pressT > 0)
            {
                // Lerp from 1.0 (Normal) to Target (Squashed)
                float pressScaleFactor = MathHelper.Lerp(1.0f, NODE_PRESS_SCALE_TARGET, Easing.EaseOutQuad(pressT));
                scale *= pressScaleFactor;
            }

            // --- HOVER FLOAT & ROTATE ---
            float floatOffset = 0f;
            float rotation = 0f;

            if (hoverT > 0)
            {
                float time = (float)gameTime.TotalGameTime.TotalSeconds;
                float phase = node.Id * 0.5f; // Randomize phase by ID

                // Blend the effect in based on hover timer so it doesn't snap
                float blend = Easing.EaseOutQuad(hoverT);

                floatOffset = MathF.Sin(time * NODE_HOVER_FLOAT_SPEED + phase) * NODE_HOVER_FLOAT_AMP * blend;
                rotation = MathF.Sin(time * NODE_HOVER_ROT_SPEED + phase) * NODE_HOVER_ROT_AMT * blend;

                // New Pulse Logic
                float pulse = MathF.Sin(time * NODE_HOVER_PULSE_SPEED + phase) * NODE_HOVER_PULSE_AMOUNT * blend;
                scale += pulse;
            }

            Vector2 arrivalScale = Vector2.One;
            Vector2 arrivalShake = Vector2.Zero;

            // Only apply arrival animation effects to the current node
            if (node.Id == _playerCurrentNodeId)
            {
                arrivalScale = _nodeArrivalScale;
                arrivalShake = _nodeArrivalShake;
            }

            // --- SELECTION JUICE (Spring Release) ---
            float selectionScale = 0f;
            Vector2 selectionShake = Vector2.Zero;
            float selectionMult = 1.0f;

            if (isSelected)
            {
                // Spring: Elastic Out for a bouncy release
                float t = Math.Clamp(_nodeSelectionAnimTimer / NODE_SELECTION_POP_DURATION, 0f, 1f);

                // Use EaseOutElastic for the spring back
                float elastic = Easing.EaseOutElastic(t);

                // Map 0..1 to a scale bump. 
                // We want it to start small (from the press) and spring big.
                // Since 'scale' already includes the base hover size (1.5), we add extra juice on top.
                selectionMult = MathHelper.Lerp(NODE_PRESS_SCALE_TARGET, 1.0f, elastic);

                // Shake: Sine wave decaying over time
                float shakeDecay = 1.0f - t;
                float shakeX = MathF.Sin(_nodeSelectionAnimTimer * NODE_SELECTION_SHAKE_FREQUENCY) * NODE_SELECTION_SHAKE_MAGNITUDE * shakeDecay;
                float shakeY = MathF.Cos(_nodeSelectionAnimTimer * NODE_SELECTION_SHAKE_FREQUENCY * 0.85f) * NODE_SELECTION_SHAKE_MAGNITUDE * shakeDecay;
                selectionShake = new Vector2(shakeX, shakeY);
            }

            // Combine scales
            // Base Scale (1.0) * Hover Pop (1.5) * Press Squash (0.85) * Arrival Scale (Squash/Stretch) + Selection Juice
            // Note: Selection Juice is additive to the popped scale
            Vector2 finalScale = new Vector2(
                (scale * selectionMult) * arrivalScale.X,
                (scale * selectionMult) * arrivalScale.Y
            );

            var position = bounds.Center.ToVector2() + node.VisualOffset + new Vector2(0, floatOffset) + arrivalShake + selectionShake;

            Color outlineColor = _global.Palette_Black;

            // --- NEW: Draw Outline using Silhouette ---
            if (silhouette != null)
            {
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(-1, 0), sourceRect, outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0.4f);
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(1, 0), sourceRect, outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0.4f);
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(0, -1), sourceRect, outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0.4f);
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(0, 1), sourceRect, outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0.4f);
            }

            spriteBatch.DrawSnapped(texture, position, sourceRect, color, rotation, origin, finalScale, SpriteEffects.None, 0.4f);
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
                    texture = _spriteManager.SplitNodeCombat;
                    silhouette = _spriteManager.SplitNodeCombatSilhouette;
                    break;
                case SplitNodeType.Narrative:
                    texture = _spriteManager.SplitNodeNarrative;
                    silhouette = _spriteManager.SplitNodeNarrativeSilhouette;
                    break;
                case SplitNodeType.MajorBattle:
                    texture = _spriteManager.SplitNodeCombat; 
                    
                    silhouette = _spriteManager.SplitNodeCombatSilhouette;
                    break;
                case SplitNodeType.Recruit:
                    texture = _spriteManager.SplitNodeRecruit;
                    silhouette = _spriteManager.SplitNodeRecruitSilhouette;
                    break;
                case SplitNodeType.Rest:
                    texture = _spriteManager.SplitNodeRest;
                    silhouette = _spriteManager.SplitNodeRestSilhouette;
                    break;
                case SplitNodeType.Shop:
                    texture = _spriteManager.SplitNodeShop;
                    silhouette = _spriteManager.SplitNodeShopSilhouette;
                    break;
                default:
                    texture = _spriteManager.SplitNodeCombat;
                    silhouette = _spriteManager.SplitNodeCombatSilhouette;
                    break;
            }

            int frameIndex = 0;
            if (node.IsReachable || node.NodeType == SplitNodeType.Origin || node.IsCompleted)
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
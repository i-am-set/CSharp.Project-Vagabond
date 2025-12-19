using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
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
        private enum SplitMapView { Map, Inventory, Settings, Shop, Rest, Recruit } // Added Recruit
        private SplitMapView _currentView = SplitMapView.Map;

        // Tracks where to go back to when closing Inventory or Settings (Map, Shop, or Rest)
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
        private readonly ChoiceGenerator _choiceGenerator;
        private readonly ComponentStore _componentStore;
        private readonly VoidEdgeEffect _voidEdgeEffect;
        private readonly SplitMapInventoryOverlay _inventoryOverlay;
        private readonly SplitMapSettingsOverlay _settingsOverlay;
        private readonly SplitMapShopOverlay _shopOverlay;
        private readonly SplitMapRestOverlay _restOverlay;
        private readonly SplitMapRecruitOverlay _recruitOverlay; // Added
        private readonly BirdManager _birdManager;

        private SplitMap? _currentMap;
        private int _playerCurrentNodeId;
        private readonly PlayerMapIcon _playerIcon;
        private NarrativeDialog _narrativeDialog;

        // --- Animation Tuning ---
        private const float PLAYER_MOVE_SPEED = 50f; // Pixels per second
        private const float CAMERA_LERP_SPEED = 5f;
        private const float NODE_LIFT_DURATION = 0.2f;
        private const float PULSE_DURATION = 0.15f;
        private const float NODE_LOWERING_DURATION = 0.2f;
        private const float POST_EVENT_DELAY = 0.25f;
        private const float PATH_ANIMATION_DURATION = 0.75f;

        private Vector2 _cameraOffset;
        private Vector2 _targetCameraOffset;
        private Vector2 RoundedCameraOffset => new Vector2(MathF.Round(_cameraOffset.X), MathF.Round(_cameraOffset.Y));

        private float _playerMoveTimer;
        private float _playerMoveDuration;
        private int _playerMoveTargetNodeId;
        private SplitMapPath? _playerMovePath;

        // Node interaction state
        private int _hoveredNodeId = -1;
        private readonly HashSet<int> _visitedNodeIds = new HashSet<int>();
        private readonly HashSet<int> _traversedPathIds = new HashSet<int>();
        private int _nodeForPathReveal = -1;

        // Path animation state
        private readonly Dictionary<int, float> _pathAnimationProgress = new();
        private readonly Dictionary<int, float> _pathAnimationDurations = new();
        private const string PATH_DRAW_PATTERN = "1111110111010111111001110111110011111111110101100"; // 1 = draw, 0 = skip. Creates a dashed line effect.
        private static readonly Random _random = new Random();

        // Path Color Variation Tuning
        private const float PATH_COLOR_VARIATION_MIN = 0.1f;
        private const float PATH_COLOR_VARIATION_MAX = 1.0f;
        private const float PATH_COLOR_NOISE_SCALE = 0.3f;
        private const float PATH_EXCLUSION_RADIUS = 10f; // Half of a 20x20 exclusion zone
        private const float PATH_FADE_DISTANCE = 12f;

        // Node pulse animation state
        private float _pulseTimer = 0f;
        private const float PULSE_AMOUNT = 0.3f;

        private bool _wasModalActiveLastFrame = false;

        // Event/State Machine
        private enum EventState { Idle, AwaitingDiceRoll, NarratingResult }
        private EventState _eventState = EventState.Idle;
        private NarrativeChoice? _pendingChoiceForDiceRoll;
        private float _postEventDelayTimer = 0f;

        private enum SplitMapState { Idle, PlayerMoving, LiftingNode, PulsingNode, EventInProgress, LoweringNode, PostEventDelay }
        private SplitMapState _mapState = SplitMapState.Idle;

        // Node Animation
        private const float NODE_FRAME_DURATION = 0.5f;
        private float _nodeHoverTextBobTimer = 0f;
        private float _nodeLiftTimer = 0f;
        private const float NODE_LIFT_AMOUNT = 15f;

        // Camera Panning State
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

        // Combat Transition State
        private List<string>? _pendingCombatArchetypes;
        private bool _waitingForCombatCameraSettle = false;
        private int _framesToWaitAfterSettle = 0;
        private const float CAMERA_SETTLE_THRESHOLD = 0.5f;

        public static bool PlayerWonLastBattle { get; set; } = true;
        public static bool WasMajorBattle { get; set; } = false;

        // UI Buttons
        private ImageButton? _settingsButton;

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
            _choiceGenerator = new ChoiceGenerator();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _inventoryOverlay = new SplitMapInventoryOverlay();
            _settingsOverlay = new SplitMapSettingsOverlay(this);
            _shopOverlay = new SplitMapShopOverlay();
            _restOverlay = new SplitMapRestOverlay(this);
            _recruitOverlay = new SplitMapRecruitOverlay(this); // Added
            _birdManager = new BirdManager();

            var narratorBounds = new Rectangle(0, Global.VIRTUAL_HEIGHT - 50, Global.VIRTUAL_WIDTH, 50);
            _resultNarrator = new StoryNarrator(narratorBounds);
            _resultNarrator.OnFinished += OnResultNarrationFinished;

            _voidEdgeEffect = new VoidEdgeEffect(
                edgeColor: _global.Palette_Black,
                edgeWidth: 6,
                noiseScale: 0.1f,
                noiseSpeed: 3f
            );

            // --- INVENTORY BUTTON LOGIC ---
            _inventoryOverlay.OnInventoryButtonClicked += () =>
            {
                // 1. If in Settings, try to close it first
                if (_currentView == SplitMapView.Settings)
                {
                    _settingsOverlay.AttemptClose(() =>
                    {
                        // After closing settings, go to Inventory
                        // Keep _viewToReturnTo as whatever it was (Map, Shop, Rest, Recruit)
                        SetView(SplitMapView.Inventory, snap: true);
                    });
                }
                // 2. If in Inventory, close it and return to previous view
                else if (_currentView == SplitMapView.Inventory)
                {
                    SetView(_viewToReturnTo, snap: true);
                }
                // 3. If in Map, Shop, Rest, or Recruit, open Inventory
                else
                {
                    // Remember where we came from
                    _viewToReturnTo = _currentView;
                    SetView(SplitMapView.Inventory, snap: true);
                }
            };

            // --- SETTINGS OVERLAY CLOSE REQUEST ---
            _settingsOverlay.OnCloseRequested += () =>
            {
                // Return to wherever we were before opening settings
                SetView(_viewToReturnTo, snap: true);
            };

            // --- SHOP LEAVE REQUEST ---
            _shopOverlay.OnLeaveRequested += () =>
            {
                // Leaving the shop always goes back to the Map and completes the node
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
            };

            // --- REST LEAVE REQUEST ---
            _restOverlay.OnLeaveRequested += () =>
            {
                // Leaving the rest site always goes back to the Map and completes the node
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
            };

            // --- REST COMPLETE LOGIC ---
            _restOverlay.OnRestCompleted += () =>
            {
                // Close Rest Overlay
                _viewToReturnTo = SplitMapView.Map;
                SetView(SplitMapView.Map, snap: true);

                // Mark node complete and lower
                var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (currentNode != null)
                {
                    currentNode.IsCompleted = true;
                    UpdateCameraTarget(currentNode.Position, false);
                }
                _mapState = SplitMapState.LoweringNode;
                _nodeLiftTimer = 0f;
            };

            // --- RECRUIT COMPLETE LOGIC ---
            _recruitOverlay.OnRecruitComplete += () =>
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
            _viewToReturnTo = SplitMapView.Map; // Reset return view on entry

            _inventoryOverlay.Initialize();
            _settingsOverlay.Initialize();

            // Initialize Settings Button
            if (_settingsButton == null)
            {
                var settingsSheet = _spriteManager.SplitMapSettingsButton;
                var settingsRects = _spriteManager.SplitMapSettingsButtonSourceRects;
                int buttonSize = 16;
                int settingsX = Global.VIRTUAL_WIDTH - 7 - buttonSize;

                _settingsButton = new ImageButton(new Rectangle(settingsX, 10, buttonSize, buttonSize), settingsSheet, settingsRects[0], settingsRects[1], enableHoverSway: true);

                // --- SETTINGS BUTTON LOGIC ---
                _settingsButton.OnClick += () =>
                {
                    // 1. If in Settings, try to close it
                    if (_currentView == SplitMapView.Settings)
                    {
                        _settingsOverlay.AttemptClose(() => SetView(_viewToReturnTo, snap: true));
                    }
                    // 2. If in Inventory, switch to Settings
                    else if (_currentView == SplitMapView.Inventory)
                    {
                        // Don't update _viewToReturnTo, keep it as Map, Shop, Rest, or Recruit
                        SetView(SplitMapView.Settings, snap: true);
                    }
                    // 3. If in Map, Shop, Rest, or Recruit, open Settings
                    else
                    {
                        _viewToReturnTo = _currentView;
                        SetView(SplitMapView.Settings, snap: true);
                    }
                };
            }
            _settingsButton.ResetAnimationState();

            if (_progressionManager.CurrentSplitMap == null)
            {
                // Full reset
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

                    // Initialize birds
                    if (_currentMap != null)
                        _birdManager.Initialize(_currentMap, _playerIcon.Position);
                }
                SetView(SplitMapView.Map, snap: true); // Default view
            }
            else
            {
                // Returning from battle
                _currentMap = _progressionManager.CurrentSplitMap;
                if (WasMajorBattle && PlayerWonLastBattle)
                {
                    WasMajorBattle = false;
                    // Major battle won - mark complete
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
                SetView(SplitMapView.Map, snap: true); // Default view

                // Ensure birds are initialized when returning
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

        private void SetView(SplitMapView view, bool snap = true)
        {
            _currentView = view;
            _isPanning = false;
            _cameraVelocity = Vector2.Zero;
            _snapBackDelayTimer = 0f;

            // Update Settings Button Sprite based on view
            if (_settingsButton != null)
            {
                if (view == SplitMapView.Settings)
                {
                    // Show "Map" icon (Close Settings)
                    _settingsButton.SetSprites(
                        _spriteManager.SplitMapCloseInventoryButton,
                        _spriteManager.SplitMapCloseInventoryButtonSourceRects[0],
                        _spriteManager.SplitMapCloseInventoryButtonSourceRects[1]
                    );
                }
                else
                {
                    // Show "Gear" icon (Open Settings)
                    _settingsButton.SetSprites(
                        _spriteManager.SplitMapSettingsButton,
                        _spriteManager.SplitMapSettingsButtonSourceRects[0],
                        _spriteManager.SplitMapSettingsButtonSourceRects[1]
                    );
                }
            }

            switch (_currentView)
            {
                case SplitMapView.Map:
                    _inventoryOverlay.Hide();
                    _settingsOverlay.Hide();
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
                    _settingsOverlay.Hide();
                    _shopOverlay.Hide();
                    _restOverlay.Hide();
                    _recruitOverlay.Hide();
                    _targetCameraOffset = new Vector2(0, -200);
                    if (snap) _cameraOffset = _targetCameraOffset;
                    break;
                case SplitMapView.Settings:
                    _inventoryOverlay.Hide();
                    _settingsOverlay.Show();
                    _shopOverlay.Hide();
                    _restOverlay.Hide();
                    _recruitOverlay.Hide();
                    _targetCameraOffset = new Vector2(0, -400);
                    if (snap) _cameraOffset = _targetCameraOffset;
                    break;
                case SplitMapView.Shop:
                    _inventoryOverlay.Hide();
                    _settingsOverlay.Hide();
                    _shopOverlay.Resume(); // Resume to preserve stock
                    _restOverlay.Hide();
                    _recruitOverlay.Hide();
                    _targetCameraOffset = new Vector2(0, -600);
                    if (snap) _cameraOffset = _targetCameraOffset;
                    break;
                case SplitMapView.Rest:
                    _inventoryOverlay.Hide();
                    _settingsOverlay.Hide();
                    _shopOverlay.Hide();
                    _restOverlay.Show();
                    _recruitOverlay.Hide();
                    _targetCameraOffset = new Vector2(0, -600); // Same offset as shop for now
                    if (snap) _cameraOffset = _targetCameraOffset;
                    break;
                case SplitMapView.Recruit:
                    _inventoryOverlay.Hide();
                    _settingsOverlay.Hide();
                    _shopOverlay.Hide();
                    _restOverlay.Hide();
                    _recruitOverlay.Show();
                    _targetCameraOffset = new Vector2(0, -600); // Same offset as shop for now
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

            // Capture the view state at the start of the frame to detect changes by overlays
            var viewAtStartOfFrame = _currentView;

            _voidEdgeEffect.Update(gameTime, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _cameraOffset);

            // Update Birds - Pass camera offset for parallax
            _birdManager.Update(gameTime, _currentMap, _playerIcon.Position, _cameraOffset);

            if (_hoveredNodeId != -1)
            {
                _nodeHoverTextBobTimer += deltaTime;
            }

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

            var cameraTransform = Matrix.CreateTranslation(RoundedCameraOffset.X, RoundedCameraOffset.Y, 0);

            // Update Overlays
            // Allow inventory interaction if Map is Idle OR if we are in the Shop/Rest/Recruit view
            // BUT: If Rest Overlay is narrating, block everything.
            bool isRestNarrating = _restOverlay.IsNarrating;
            bool allowInventoryInteraction = !isRestNarrating && (_mapState == SplitMapState.Idle || _currentView == SplitMapView.Shop || _currentView == SplitMapView.Rest || _currentView == SplitMapView.Recruit);

            _inventoryOverlay.Update(gameTime, currentMouseState, currentKeyboardState, allowInventoryInteraction, cameraTransform);

            if (!isRestNarrating)
            {
                _settingsOverlay.Update(gameTime, currentMouseState, currentKeyboardState, cameraTransform);
                _shopOverlay.Update(gameTime, currentMouseState, cameraTransform);
                _recruitOverlay.Update(gameTime, currentMouseState, cameraTransform);
            }

            _restOverlay.Update(gameTime, currentMouseState, cameraTransform);

            // Update Settings Button
            if (!isRestNarrating)
            {
                _settingsButton?.Update(currentMouseState);
            }

            // Handle Escape Key for Navigation
            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState) && !isRestNarrating)
            {
                // Only act if the view hasn't already changed this frame (e.g., by an overlay closing itself)
                if (_currentView == viewAtStartOfFrame)
                {
                    if (_currentView == SplitMapView.Inventory)
                    {
                        SetView(_viewToReturnTo, snap: true);
                    }
                    else if (_currentView == SplitMapView.Settings)
                    {
                        // Settings overlay handles its own dirty check on Escape
                    }
                    else if (_currentView == SplitMapView.Map || _currentView == SplitMapView.Shop || _currentView == SplitMapView.Rest || _currentView == SplitMapView.Recruit)
                    {
                        _viewToReturnTo = _currentView;
                        SetView(SplitMapView.Settings, snap: true);
                    }
                }
            }

            // Handle camera logic
            if (_currentView == SplitMapView.Map)
            {
                if (!_isPanning)
                {
                    if (_cameraVelocity.LengthSquared() > 0.1f)
                    {
                        _cameraOffset += _cameraVelocity;
                        _cameraVelocity = Vector2.Lerp(_cameraVelocity, Vector2.Zero, PAN_FRICTION * deltaTime);
                        ClampCameraOffset();
                        _targetCameraOffset.X = _cameraOffset.X;
                        _snapBackDelayTimer = SNAP_BACK_DELAY;
                    }
                    else
                    {
                        _cameraVelocity = Vector2.Zero;
                        if (_snapBackDelayTimer > 0)
                        {
                            _snapBackDelayTimer -= deltaTime;
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

            _cameraOffset = Vector2.Lerp(_cameraOffset, _targetCameraOffset, deltaTime * CAMERA_LERP_SPEED);

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
                // FIX: Reset hovered node if not in Map view to prevent interaction/text drawing
                _hoveredNodeId = -1;
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

        private void HandleMapInput(GameTime gameTime)
        {
            var cursorManager = ServiceLocator.Get<CursorManager>();
            var currentMouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            var cameraTransform = Matrix.CreateTranslation(RoundedCameraOffset.X, RoundedCameraOffset.Y, 0);
            Matrix.Invert(ref cameraTransform, out var inverseCameraTransform);
            var mouseInMapSpace = Vector2.Transform(virtualMousePos, inverseCameraTransform);

            _hoveredNodeId = -1;
            if (_currentMap != null)
            {
                foreach (var node in _currentMap.Nodes.Values)
                {
                    if (node.IsReachable && node.GetBounds().Contains(mouseInMapSpace))
                    {
                        _hoveredNodeId = node.Id;
                        break;
                    }
                }
            }

            if (_hoveredNodeId != -1)
            {
                cursorManager.SetState(CursorState.HoverClickable);
            }
            else if (_mapState == SplitMapState.Idle)
            {
                cursorManager.SetState(CursorState.HoverDraggable);
            }

            // Only allow panning if not hovering buttons
            bool hoveringButtons = (_inventoryOverlay.IsHovered || (_settingsButton?.IsHovered ?? false));
            if (!hoveringButtons)
            {
                HandleCameraPan(currentMouseState, virtualMousePos);
            }

            if (_isPanning)
            {
                cursorManager.SetState(CursorState.Dragging);
            }

            if (!_isPanning && _mapState == SplitMapState.Idle)
            {
                bool leftClickPressed = currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;

                // FIX: Ensure we are in Map view before processing map clicks
                if (leftClickPressed && _hoveredNodeId != -1 && UIInputManager.CanProcessMouseClick() && _currentView == SplitMapView.Map)
                {
                    var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (currentNode != null)
                    {
                        UpdateCameraTarget(currentNode.Position, false);
                    }
                    _snapBackDelayTimer = 0f;
                    _cameraVelocity = Vector2.Zero;

                    StartPlayerMove(_hoveredNodeId);
                    _hoveredNodeId = -1;
                    UIInputManager.ConsumeMouseClick();
                }
            }
        }

        private void HandleCameraPan(MouseState currentMouseState, Vector2 virtualMousePos)
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

            if (leftClickPressed && _hoveredNodeId == -1 && _mapState == SplitMapState.Idle && UIInputManager.CanProcessMouseClick())
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

                Vector2 virtualCurrentPos = Core.TransformMouse(currentMouseState.Position);
                Vector2 virtualLastPos = Core.TransformMouse(_lastPanMousePosition);
                Vector2 virtualDelta = virtualCurrentPos - virtualLastPos;

                _cameraVelocity.X = virtualDelta.X * PAN_SENSITIVITY;
                _cameraVelocity.Y = 0;

                _cameraOffset.X += _cameraVelocity.X;
                ClampCameraOffset();

                _targetCameraOffset.X = _cameraOffset.X;
                _lastPanMousePosition = currentMouseState.Position;

                Mouse.SetPosition(currentMouseState.Position.X, _panStartMousePosition.Y);
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
                currentNode.VisualOffset = new Vector2(0, MathHelper.Lerp(0, -NODE_LIFT_AMOUNT, Easing.EaseOutCubic(progress)));
            }

            if (_nodeLiftTimer >= NODE_LIFT_DURATION)
            {
                _mapState = SplitMapState.PulsingNode;
                _pulseTimer = 0f;
            }
        }

        private void UpdatePulsingNode(float deltaTime)
        {
            _pulseTimer += deltaTime;
            if (_pulseTimer >= PULSE_DURATION)
            {
                _mapState = SplitMapState.EventInProgress;
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
                currentNode.VisualOffset = new Vector2(0, MathHelper.Lerp(-NODE_LIFT_AMOUNT, 0, Easing.EaseOutCubic(progress)));
            }

            if (_nodeLiftTimer >= NODE_LOWERING_DURATION)
            {
                if (currentNode != null)
                {
                    currentNode.VisualOffset = Vector2.Zero;
                    UpdateCameraTarget(currentNode.Position, false);
                }
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

            _targetCameraOffset = new Vector2(targetX, 0);

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
            _sceneManager.ChangeScene(GameSceneState.Battle);
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
                            node.IsCompleted = true;
                            UpdateCameraTarget(node.Position, false);
                            _mapState = SplitMapState.LoweringNode;
                            _nodeLiftTimer = 0f;
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
                    SetView(SplitMapView.Recruit, snap: true);
                    break;

                case SplitNodeType.Rest:
                    SetView(SplitMapView.Rest, snap: true);
                    break;

                case SplitNodeType.Shop:
                    OpenRandomShop();
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
            SetView(SplitMapView.Recruit, snap: true);
        }

        private void OpenRandomShop()
        {
            // Generate Shop Stock
            var premiumStock = new List<ShopItem>();
            var consumableStock = new List<ShopItem>();

            // Randomly pick 3-4 premium items
            int premiumCount = _random.Next(3, 5);
            var allPremium = new List<ShopItem>();

            foreach (var w in BattleDataCache.Weapons.Values) allPremium.Add(new ShopItem { ItemId = w.WeaponID, DisplayName = w.WeaponName, Type = "Weapon", Price = PriceCalculator.CalculatePrice(w, 1.0f), DataObject = w });
            foreach (var a in BattleDataCache.Armors.Values) allPremium.Add(new ShopItem { ItemId = a.ArmorID, DisplayName = a.ArmorName, Type = "Armor", Price = PriceCalculator.CalculatePrice(a, 1.0f), DataObject = a });
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

            // Randomly pick 2-3 consumables
            int consumableCount = _random.Next(2, 4);
            var allConsumables = BattleDataCache.Consumables.Values.ToList();
            for (int i = 0; i < consumableCount; i++)
            {
                if (allConsumables.Any())
                {
                    var c = allConsumables[_random.Next(allConsumables.Count)];
                    consumableStock.Add(new ShopItem { ItemId = c.ItemID, DisplayName = c.ItemName, Type = "Consumable", Price = PriceCalculator.CalculatePrice(c, 1.0f), DataObject = c });
                }
            }

            _shopOverlay.Show(premiumStock, consumableStock);
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
                _gameState.ApplyNarrativeOutcome(selectedOutcome.Outcome);
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

        private void TriggerReward()
        {
            var choiceMenu = _sceneManager.GetScene(GameSceneState.ChoiceMenu) as ChoiceMenuScene;
            if (choiceMenu == null)
            {
                _mapState = SplitMapState.LoweringNode;
                _nodeLiftTimer = 0f;
                return;
            }

            var choices = new List<object>();
            const int numberOfChoices = 3;
            int gameStage = 1;

            var playerAbilities = _componentStore.GetComponent<PassiveAbilitiesComponent>(_gameState.PlayerEntityId)?.RelicIDs;
            var excludeIds = playerAbilities != null ? new HashSet<string>(playerAbilities) : null;
            choices.AddRange(_choiceGenerator.GenerateAbilityChoices(gameStage, numberOfChoices, excludeIds));

            if (!choices.Any())
            {
                _mapState = SplitMapState.LoweringNode;
                _nodeLiftTimer = 0f;
                return;
            }

            Action onChoiceMade = () => _sceneManager.HideModal();

            choiceMenu.Show(choices, onChoiceMade);
            _wasModalActiveLastFrame = true;
        }

        private void FinalizeVictory()
        {
            SplitMapScene.PlayerWonLastBattle = true;
            DecrementTemporaryBuffs();
            _sceneManager.ChangeScene(BattleSetup.ReturnSceneState);
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

            var cameraTransform = Matrix.CreateTranslation(RoundedCameraOffset.X, RoundedCameraOffset.Y, 0);
            var finalTransform = cameraTransform * transform;

            // --- Pass 1: World-space elements (Map Content) ---
            spriteBatch.End();
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: finalTransform);

            var pixel = ServiceLocator.Get<Texture2D>();

            if (_global.ShowSplitMapGrid)
            {
                Color gridColor = _global.Palette_DarkGray * 0.5f;
                const int gridSize = Global.SPLIT_MAP_GRID_SIZE;

                Matrix.Invert(ref cameraTransform, out var inverseCameraTransform);
                var topLeft = Vector2.Transform(Vector2.Zero, inverseCameraTransform);
                var bottomRight = Vector2.Transform(new Vector2(Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), inverseCameraTransform);

                int startX = (int)Math.Floor(topLeft.X / gridSize) * gridSize;
                int endX = (int)Math.Ceiling(bottomRight.X / gridSize) * gridSize;
                int startY = (int)Math.Floor(topLeft.Y / gridSize) * gridSize;
                int endY = (int)Math.Ceiling(bottomRight.Y / gridSize) * gridSize;

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

            // Draw Birds (World Space)
            _birdManager.Draw(spriteBatch, _cameraOffset);

            // Draw Overlays in World Space
            _inventoryOverlay.DrawWorld(spriteBatch, font, gameTime);
            // Pass the finalTransform to the settings overlay so it can restore it after drawing dialogs
            _settingsOverlay.Draw(spriteBatch, font, gameTime, finalTransform);
            _shopOverlay.Draw(spriteBatch, font, gameTime); // Draw Shop
            _restOverlay.Draw(spriteBatch, font, gameTime); // Draw Rest
            _recruitOverlay.Draw(spriteBatch, font, gameTime); // Draw Recruit

            spriteBatch.End();

            // --- Pass 2: Screen-space UI (Void Edge, Hover Text, Dialogs) ---
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

            var mapBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            _voidEdgeEffect.Draw(spriteBatch, mapBounds);

            _inventoryOverlay.DrawScreen(spriteBatch, font, gameTime, transform);
            _settingsButton?.Draw(spriteBatch, font, gameTime, transform);

            // FIX: Only draw node hover text if we are actually in the Map view
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
                        float yOffset = (MathF.Sin(_nodeHoverTextBobTimer * 4f) > 0) ? -1f : 0f;
                        var textPosition = new Vector2((Global.VIRTUAL_WIDTH - nodeTextSize.Width) / 2f, Global.VIRTUAL_HEIGHT - nodeTextSize.Height - 3 + yOffset);
                        spriteBatch.DrawStringSnapped(secondaryFont, nodeText, textPosition, _global.Palette_Yellow);
                    }
                }
            }

            if (_narrativeDialog.IsActive) _narrativeDialog.DrawContent(spriteBatch, font, gameTime, transform);
            if (_eventState == EventState.NarratingResult) _resultNarrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);

            // Draw Rest Overlay Dialogs (Screen Space)
            _restOverlay.DrawDialogContent(spriteBatch, font, gameTime);
            // Draw Recruit Overlay Dialogs (Screen Space)
            _recruitOverlay.DrawDialogContent(spriteBatch, font, gameTime);
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_narrativeDialog.IsActive)
            {
                _narrativeDialog.DrawOverlay(spriteBatch);
            }
            // Draw Rest Overlay Dialog Background
            _restOverlay.DrawDialogOverlay(spriteBatch);
            // Draw Recruit Overlay Dialog Background
            _recruitOverlay.DrawDialogOverlay(spriteBatch);
        }

        private void DrawAllPaths(SpriteBatch spriteBatch, Texture2D pixel)
        {
            if (_currentMap == null) return;

            foreach (var path in _currentMap.Paths.Values)
            {
                DrawPath(spriteBatch, pixel, path, _global.Palette_Gray, false);
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
                    DrawPath(spriteBatch, pixel, path, _global.Palette_White, isAnimating);
                }
            }

            if (highlightedPath != null)
            {
                DrawPath(spriteBatch, pixel, highlightedPath, _global.Palette_Red, false);
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

            for (int i = 0; i < numPixelsToDraw; i++)
            {
                var point = path.PixelPoints[i];
                int patternIndex = Math.Abs(point.X * 7 + point.Y * 13) % PATH_DRAW_PATTERN.Length;
                if (PATH_DRAW_PATTERN[patternIndex] == '1')
                {
                    var pointVec = point.ToVector2();
                    float distFrom = Vector2.Distance(pointVec, fromNode.Position);
                    float distTo = Vector2.Distance(pointVec, toNode.Position);

                    float alpha = 1.0f;
                    float fadeStartDistance = PATH_EXCLUSION_RADIUS + PATH_FADE_DISTANCE;

                    if (distFrom < fadeStartDistance)
                    {
                        if (distFrom <= PATH_EXCLUSION_RADIUS) alpha = 0f;
                        else alpha = Math.Min(alpha, (distFrom - PATH_EXCLUSION_RADIUS) / PATH_FADE_DISTANCE);
                    }

                    if (distTo < fadeStartDistance)
                    {
                        if (distTo <= PATH_EXCLUSION_RADIUS) alpha = 0f;
                        else alpha = Math.Min(alpha, (distTo - PATH_EXCLUSION_RADIUS) / PATH_FADE_DISTANCE);
                    }

                    if (alpha > 0.01f)
                    {
                        float noise = _gameState.NoiseManager.GetNoiseValue(NoiseMapType.Resources, point.X * PATH_COLOR_NOISE_SCALE, point.Y * PATH_COLOR_NOISE_SCALE);
                        float multiplier = MathHelper.Lerp(PATH_COLOR_VARIATION_MIN, PATH_COLOR_VARIATION_MAX, noise);
                        Color pixelColor = new Color(pathColor.ToVector3() * multiplier);
                        spriteBatch.Draw(pixel, point.ToVector2(), pixelColor * alpha);
                    }
                }
            }
        }

        private void DrawNode(SpriteBatch spriteBatch, SplitMapNode node, GameTime gameTime)
        {
            var (texture, sourceRect, origin) = GetNodeDrawData(node, gameTime);
            var bounds = node.GetBounds();
            var color = Color.White;
            float scale = 1.0f;

            if (node.IsCompleted)
            {
                color = _global.Palette_Gray;
            }
            else if (node.NodeType != SplitNodeType.Origin && node.Id != _playerCurrentNodeId && !node.IsReachable)
            {
                color = _global.Palette_Gray;
            }

            if (node.IsReachable && node.Id == _hoveredNodeId)
            {
                color = _global.ButtonHoverColor;
            }

            if (_mapState == SplitMapState.PulsingNode && node.Id == _playerCurrentNodeId)
            {
                float pulseProgress = Math.Clamp(_pulseTimer / PULSE_DURATION, 0f, 1f);
                float pulseWave = MathF.Sin(pulseProgress * MathF.PI);
                scale += pulseWave * PULSE_AMOUNT;
            }

            var position = bounds.Center.ToVector2() + node.VisualOffset;
            spriteBatch.DrawSnapped(texture, position, sourceRect, color, 0f, origin, scale, SpriteEffects.None, 0.4f);
        }

        private (Texture2D texture, Rectangle? sourceRect, Vector2 origin) GetNodeDrawData(SplitMapNode node, GameTime gameTime)
        {
            Texture2D texture;

            switch (node.NodeType)
            {
                case SplitNodeType.Origin:
                    texture = _spriteManager.SplitNodeStart;
                    break;
                case SplitNodeType.Battle:
                    texture = _spriteManager.SplitNodeCombat;
                    break;
                case SplitNodeType.Narrative:
                    texture = _spriteManager.SplitNodeNarrative;
                    break;
                case SplitNodeType.MajorBattle:
                    texture = _spriteManager.SplitNodeCombat;
                    break;
                case SplitNodeType.Recruit:
                    texture = _spriteManager.SplitNodeRecruit;
                    break;
                case SplitNodeType.Rest:
                    texture = _spriteManager.SplitNodeRest;
                    break;
                case SplitNodeType.Shop:
                    texture = _spriteManager.SplitNodeShop;
                    break;
                default:
                    texture = _spriteManager.SplitNodeCombat;
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
            return (texture, sourceRect, origin);
        }
    }
}
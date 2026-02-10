using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
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
        private readonly StoryNarrator _resultNarrator;
        private readonly VoidEdgeEffect _voidEdgeEffect;

        private readonly SplitMapHudRenderer _hudRenderer;
        private readonly PostBattleMenu _postBattleMenu;

        private readonly BirdManager _birdManager;
        private readonly TransitionManager _transitionManager;
        private readonly HapticsManager _hapticsManager;

        private SplitMap? _currentMap;
        private int _playerCurrentNodeId;
        private readonly PlayerMapIcon _playerIcon;

        private const float PLAYER_MOVE_SPEED = 50f;
        private const float CAMERA_LERP_SPEED = 15f;
        private const float POST_EVENT_DELAY = 0.0f;
        private const float PATH_ANIMATION_DURATION = 1.25f;

        private const float NODE_LIFT_DURATION = 0.1f;
        private const float PULSE_DURATION = 0.1f;
        private const float NODE_LOWERING_DURATION = 0.1f;
        private const float NODE_LIFT_AMOUNT = 4f;

        private Vector2 _cameraOffset;
        private Vector2 _targetCameraOffset;

        // --- Dynamic Viewport State ---
        private float _hudSlideOffset = 0f;
        // CHANGED: Updated to 22f
        private const float HUD_SLIDE_DISTANCE = 52f;
        private const float HUD_SLIDE_SPEED = 10f;
        private const float BASE_CAMERA_Y = -50f;

        private float _playerMoveTimer;
        private float _playerMoveDuration;
        private int _playerMoveTargetNodeId;
        private SplitMapPath? _playerMovePath;

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
        private const float PULSE_AMOUNT = 0.3f;

        private bool _wasModalActiveLastFrame = false;

        private enum EventState { Idle, NarratingResult }
        private EventState _eventState = EventState.Idle;
        private float _postEventDelayTimer = 0f;

        private enum SplitMapState { Idle, PlayerMoving, LiftingNode, PulsingNode, EventInProgress, LoweringNode, Resting, PostEventDelay }
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

        private readonly Dictionary<int, float> _nodeHoverTimers = new Dictionary<int, float>();
        private const float NODE_HOVER_POP_SCALE_TARGET = 1.1f;
        private const float NODE_HOVER_POP_SPEED = 10.0f;

        private const float NODE_HOVER_FLOAT_SPEED = 1.0f;
        private const float NODE_HOVER_FLOAT_AMP = 1.0f;
        private const float NODE_HOVER_ROT_SPEED = 1.0f;
        private const float NODE_HOVER_ROT_AMT = 0.05f;
        private const float NODE_HOVER_PULSE_SPEED = 1.0f;
        private const float NODE_HOVER_PULSE_AMOUNT = 0.1f;

        private Vector2 _nodeArrivalScale = Vector2.One;
        private Vector2 _nodeArrivalShake = Vector2.Zero;

        private int _selectedNodeId = -1;
        private float _nodeSelectionAnimTimer = 0f;
        private const float NODE_SELECTION_POP_DURATION = 0.3f;
        private const float NODE_SELECTION_SCALE_EXTRA = 0.8f;
        private const float NODE_SELECTION_SHAKE_MAGNITUDE = 1.0f;
        private const float NODE_SELECTION_SHAKE_FREQUENCY = 50.0f;

        private const float NODE_PRESS_SCALE_TARGET = 0.85f;
        private const float NODE_PRESS_SPEED = 30.0f;
        private readonly Dictionary<int, float> _nodePressTimers = new Dictionary<int, float>();

        public SplitMapScene()
        {
            _progressionManager = ServiceLocator.Get<ProgressionManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _playerIcon = new PlayerMapIcon();

            _hudRenderer = new SplitMapHudRenderer();
            _postBattleMenu = new PostBattleMenu();
            _postBattleMenu.OnComplete += () =>
            {
                _mapState = SplitMapState.Idle;
                UpdateReachableNodes();
                StartPathRevealAnimation();
            };

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
        }

        public override Rectangle GetAnimatedBounds() => new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

        public override void Enter()
        {
            base.Enter();
            _playerIcon.SetIsMoving(false);
            _waitingForCombatCameraSettle = false;
            _pendingCombatArchetypes = null;
            _nodeTextWaveTimer = 0f;
            _nodeHoverTimers.Clear();
            _nodePressTimers.Clear();
            _nodeArrivalScale = Vector2.One;
            _nodeArrivalShake = Vector2.Zero;
            _selectedNodeId = -1;
            _lastHoveredNodeId = -1;
            _pressedNodeId = -1;
            _hudSlideOffset = 0f;

            _postBattleMenu.Hide();
            InitializeSettingsButton();
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
                    if (_currentMap != null) _birdManager.Initialize(_currentMap, _playerIcon.Position, _cameraOffset);
                }
            }
            else
            {
                _currentMap = _progressionManager.CurrentSplitMap;
                var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (currentNode != null)
                {
                    currentNode.IsCompleted = true;
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

        private void StartPlayerMove(int targetNodeId)
        {
            if (_currentMap == null) return;
            _playerMovePath = _currentMap.Paths.Values.FirstOrDefault(p => p.FromNodeId == _playerCurrentNodeId && p.ToNodeId == targetNodeId);
            if (_playerMovePath == null) return;

            float pathLength = 0f;
            if (_playerMovePath.PixelPoints.Count > 1)
            {
                for (int i = 0; i < _playerMovePath.PixelPoints.Count - 1; i++)
                    pathLength += Vector2.Distance(_playerMovePath.PixelPoints[i].ToVector2(), _playerMovePath.PixelPoints[i + 1].ToVector2());
            }
            _playerMoveDuration = (pathLength > 0) ? pathLength / PLAYER_MOVE_SPEED : 0f;
            _mapState = SplitMapState.PlayerMoving;
            _playerIcon.SetIsMoving(true);
            _playerMoveTimer = 0f;
            _playerMoveTargetNodeId = targetNodeId;

            var currentNode = _currentMap.Nodes[_playerCurrentNodeId];
            var reachableNodeIds = currentNode.OutgoingPathIds.Select(pathId => _currentMap.Paths[pathId].ToNodeId).ToList();
            foreach (var nodeId in reachableNodeIds)
            {
                if (nodeId != targetNodeId) _currentMap.Nodes[nodeId].IsReachable = false;
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_inputBlockTimer > 0) _inputBlockTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (IsInputBlocked) { base.Update(gameTime); return; }

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            // --- Dynamic Viewport Logic ---
            // FIX: Include _hudSlideOffset in the split line calculation so the trigger moves with the HUD
            float splitLineY = (Global.VIRTUAL_HEIGHT - SplitMapHudRenderer.HUD_HEIGHT) + _hudSlideOffset;
            bool mouseInMap = virtualMousePos.Y < splitLineY;

            float targetHudOffset = (mouseInMap && !_hudRenderer.IsDragging) ? HUD_SLIDE_DISTANCE : 0f;
            _hudSlideOffset = MathHelper.Lerp(_hudSlideOffset, targetHudOffset, deltaTime * HUD_SLIDE_SPEED);

            _hudRenderer.Update(gameTime, virtualMousePos, _hudSlideOffset);

            _voidEdgeEffect.Update(gameTime, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _cameraOffset);
            _birdManager.Update(gameTime, _currentMap, _playerIcon.Position, _cameraOffset);

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

            if (_eventState == EventState.NarratingResult)
            {
                _resultNarrator.Update(gameTime);
                base.Update(gameTime);
                return;
            }

            if (_mapState == SplitMapState.Resting) _postBattleMenu.Update(gameTime, currentMouseState);

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

            // Camera Update
            float cameraDamping = 1.0f - MathF.Exp(-CAMERA_LERP_SPEED * deltaTime);

            if (_currentMap != null && _currentMap.Nodes.TryGetValue(_playerCurrentNodeId, out var pNode))
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

            HandleMapInput(gameTime);
            UpdateNodeAnimationTimers(deltaTime);
            if (_selectedNodeId != -1) _nodeSelectionAnimTimer += deltaTime;

            switch (_mapState)
            {
                case SplitMapState.PlayerMoving: UpdatePlayerMove(deltaTime); break;
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

            _playerIcon.Update(gameTime);
            base.Update(gameTime);
        }

        private void UpdateNodeAnimationTimers(float dt)
        {
            if (_currentMap != null)
            {
                foreach (var node in _currentMap.Nodes.Values)
                {
                    if (!_nodeHoverTimers.ContainsKey(node.Id)) _nodeHoverTimers[node.Id] = 0f;
                    bool isHovered = (node.Id == _hoveredNodeId) || (node.Id == _selectedNodeId);
                    float hoverChange = dt * NODE_HOVER_POP_SPEED;
                    if (isHovered) _nodeHoverTimers[node.Id] = Math.Min(_nodeHoverTimers[node.Id] + hoverChange, 1f);
                    else _nodeHoverTimers[node.Id] = Math.Max(_nodeHoverTimers[node.Id] - hoverChange, 0f);

                    if (!_nodePressTimers.ContainsKey(node.Id)) _nodePressTimers[node.Id] = 0f;
                    bool isPressed = (node.Id == _pressedNodeId) && (node.Id == _hoveredNodeId);
                    float pressChange = dt * NODE_PRESS_SPEED;
                    if (isPressed) _nodePressTimers[node.Id] = Math.Min(_nodePressTimers[node.Id] + pressChange, 1f);
                    else _nodePressTimers[node.Id] = Math.Max(_nodePressTimers[node.Id] - pressChange, 0f);
                }
            }
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
            bool rawPlayerHovered = false;

            if (_currentMap != null && _mapState == SplitMapState.Idle)
            {
                foreach (var node in _currentMap.Nodes.Values)
                {
                    if ((node.IsReachable || node.Id == _playerCurrentNodeId) && node.GetBounds().Contains(mouseInMapSpace))
                    {
                        rawHoveredNodeId = node.Id;
                        break;
                    }
                }
            }

            bool hoveringButtons = (_settingsButton?.IsHovered ?? false);
            _hoveredNodeId = rawPlayerHovered ? -1 : rawHoveredNodeId;
            _lastHoveredNodeId = _hoveredNodeId;

            if (_hoveredNodeId != -1) cursorManager.SetState(CursorState.HoverClickable);

            if (_mapState == SplitMapState.Idle && !hoveringButtons && UIInputManager.CanProcessMouseClick())
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
                            var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                            if (currentNode != null) UpdateCameraTarget(currentNode.Position, false);
                            _selectedNodeId = _hoveredNodeId;
                            _nodeSelectionAnimTimer = 0f;
                            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                            StartPlayerMove(_hoveredNodeId);
                        }
                        _hoveredNodeId = -1;
                        UIInputManager.ConsumeMouseClick();
                    }
                    _pressedNodeId = -1;
                }
            }
            else _pressedNodeId = -1;
            previousMouseState = currentMouseState;
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
                if (_playerMovePath != null) _traversedPathIds.Add(_playerMovePath.Id);
                _playerCurrentNodeId = _playerMoveTargetNodeId;
                _visitedNodeIds.Add(_playerCurrentNodeId);
                _selectedNodeId = -1;
                var toNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (toNode != null) _playerIcon.SetPosition(toNode.Position);
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
                float eased = Easing.EaseOutBack(progress);
                currentNode.VisualOffset = new Vector2(0, MathHelper.Lerp(0, -NODE_LIFT_AMOUNT, eased));
                float stretch = MathF.Sin(progress * MathHelper.Pi);
                _nodeArrivalScale = new Vector2(1.0f - (stretch * 0.2f), 1.0f + (stretch * 0.2f));
            }
            if (_nodeLiftTimer >= NODE_LIFT_DURATION)
            {
                _mapState = SplitMapState.PulsingNode;
                _pulseTimer = 0f;
                _nodeArrivalScale = Vector2.One;
            }
        }

        private void UpdatePulsingNode(float deltaTime)
        {
            _pulseTimer += deltaTime;
            float progress = Math.Clamp(_pulseTimer / PULSE_DURATION, 0f, 1f);
            float pulse = MathF.Sin(progress * MathHelper.Pi);
            float scaleAmount = 0.3f;
            _nodeArrivalScale = new Vector2(1.0f + (pulse * scaleAmount));
            _nodeArrivalShake = Vector2.Zero;
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
                float eased = Easing.EaseOutBounce(progress);
                currentNode.VisualOffset = new Vector2(0, MathHelper.Lerp(-NODE_LIFT_AMOUNT, 0, eased));
                if (progress > 0.8f)
                {
                    float squashProgress = (progress - 0.8f) / 0.2f;
                    float squash = MathF.Sin(squashProgress * MathHelper.Pi);
                    _nodeArrivalScale = new Vector2(1.0f + (squash * 0.15f), 1.0f - (squash * 0.15f));
                }
                else _nodeArrivalScale = Vector2.One;
            }

            if (_nodeLiftTimer >= NODE_LOWERING_DURATION)
            {
                if (currentNode != null)
                {
                    currentNode.VisualOffset = Vector2.Zero;
                    UpdateCameraTarget(currentNode.Position, false);
                }
                _nodeArrivalScale = Vector2.One;
                bool wasBattle = currentNode != null && (currentNode.NodeType == SplitNodeType.Battle || currentNode.NodeType == SplitNodeType.MajorBattle);
                if (wasBattle)
                {
                    _mapState = SplitMapState.Resting;
                    _postBattleMenu.Show();
                }
                else
                {
                    _mapState = SplitMapState.PostEventDelay;
                    _postEventDelayTimer = POST_EVENT_DELAY;
                }
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
            _mapState = SplitMapState.EventInProgress;
            switch (node.NodeType)
            {
                case SplitNodeType.Battle:
                case SplitNodeType.MajorBattle:
                    WasMajorBattle = node.NodeType == SplitNodeType.MajorBattle;
                    InitiateCombat(node.EventData as List<string> ?? new List<string>());
                    break;
                default:
                    node.IsCompleted = true;
                    UpdateCameraTarget(node.Position, false);
                    _mapState = SplitMapState.LoweringNode;
                    _nodeLiftTimer = 0f;
                    break;
            }
        }

        private void OnResultNarrationFinished()
        {
            _eventState = EventState.Idle;
            var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
            if (currentNode != null) currentNode.IsCompleted = true;
            _mapState = SplitMapState.LoweringNode;
            _nodeLiftTimer = 0f;
            _resultNarrator.Clear();
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_currentMap == null) return;

            var cameraTransform = Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0);
            var finalTransform = cameraTransform * transform;

            float mapViewHeight = (Global.VIRTUAL_HEIGHT - SplitMapHudRenderer.HUD_HEIGHT) + _hudSlideOffset;

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
            _birdManager.Draw(spriteBatch, _cameraOffset);

            spriteBatch.End();
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

            var mapBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            _voidEdgeEffect.Draw(spriteBatch, mapBounds);

            _hudRenderer.Draw(spriteBatch, gameTime, _hudSlideOffset);

            _settingsButton?.Draw(spriteBatch, font, gameTime, transform);
            _postBattleMenu.Draw(spriteBatch, gameTime);

            if (_hoveredNodeId != -1 && _mapState == SplitMapState.Idle)
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
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(nodeText))
                    {
                        var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                        var nodeTextSize = secondaryFont.MeasureString(nodeText);
                        Vector2 nodeScreenPos = hoveredNode.Position + _cameraOffset;
                        float textX = nodeScreenPos.X - (nodeTextSize.Width / 2f);
                        float textY = nodeScreenPos.Y - 16f - nodeTextSize.Height - 4f;
                        var textPosition = new Vector2(textX, textY);
                        _nodeTextWaveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                        TextAnimator.DrawTextWithEffect(spriteBatch, secondaryFont, nodeText, textPosition, _global.Palette_DarkSun, TextEffectType.Wave, _nodeTextWaveTimer);
                    }
                }
            }
            else _nodeTextWaveTimer = 0f;

            if (_eventState == EventState.NarratingResult) _resultNarrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime) { }

        private void DrawAllPaths(SpriteBatch spriteBatch, Texture2D pixel)
        {
            if (_currentMap == null) return;
            foreach (var path in _currentMap.Paths.Values) DrawPath(spriteBatch, pixel, path, _global.Palette_DarkShadow, false);
            SplitMapPath? highlightedPath = null;
            if (_hoveredNodeId != -1 && _mapState == SplitMapState.Idle)
                highlightedPath = _currentMap.Paths.Values.FirstOrDefault(p => p.FromNodeId == _playerCurrentNodeId && p.ToNodeId == _hoveredNodeId);

            foreach (var path in _currentMap.Paths.Values)
            {
                if (path == highlightedPath) continue;
                var fromNode = _currentMap.Nodes[path.FromNodeId];
                var toNode = _currentMap.Nodes[path.ToNodeId];
                bool isPathFromCurrentNode = fromNode.Id == _playerCurrentNodeId;
                bool isPathToReachableNode = toNode.IsReachable;
                bool isPathTraversed = _traversedPathIds.Contains(path.Id);
                bool isAnimating = isPathFromCurrentNode && isPathToReachableNode && !_visitedNodeIds.Contains(toNode.Id);
                if (isPathTraversed || isAnimating) DrawPath(spriteBatch, pixel, path, _global.SplitMapPathColor, isAnimating);
            }
            if (highlightedPath != null) DrawPath(spriteBatch, pixel, highlightedPath, _global.SplitMapNodeColor, false);
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
            else numPixelsToDraw = path.PixelPoints.Count;

            if (numPixelsToDraw <= 0) return;
            for (int i = 0; i < numPixelsToDraw; i++)
            {
                var point = path.PixelPoints[i];
                var pointVec = point.ToVector2();
                float distFrom = Vector2.Distance(pointVec, fromNode.Position);
                float distTo = Vector2.Distance(pointVec, toNode.Position);
                if (distFrom <= PATH_EXCLUSION_RADIUS || distTo <= PATH_EXCLUSION_RADIUS) continue;
                if (i % 4 < 2) spriteBatch.Draw(pixel, pointVec, pathColor);
            }
        }

        private void DrawNode(SpriteBatch spriteBatch, SplitMapNode node, GameTime gameTime)
        {
            var (texture, silhouette, sourceRect, origin) = GetNodeDrawData(node, gameTime);
            var bounds = node.GetBounds();
            var color = _global.SplitMapNodeColor;
            float scale = 1.0f;

            if (node.IsCompleted) color = _global.Palette_DarkShadow;
            else if (node.NodeType != SplitNodeType.Origin && node.Id != _playerCurrentNodeId && !node.IsReachable) color = _global.Palette_DarkShadow;

            bool isSelected = (node.Id == _selectedNodeId);
            bool isHovered = (node.Id == _hoveredNodeId);
            bool isPressed = (node.Id == _pressedNodeId) && isHovered;
            bool isAnimatingThisNode = (node.Id == _playerCurrentNodeId) &&
                                       (_mapState == SplitMapState.LiftingNode ||
                                        _mapState == SplitMapState.PulsingNode ||
                                        _mapState == SplitMapState.EventInProgress ||
                                        _mapState == SplitMapState.LoweringNode);

            if (node.IsReachable && (isHovered || isSelected || isAnimatingThisNode)) color = _global.ButtonHoverColor;
            if (_mapState == SplitMapState.PulsingNode && node.Id == _playerCurrentNodeId)
            {
                float pulseProgress = Math.Clamp(_pulseTimer / PULSE_DURATION, 0f, 1f);
                float pulseWave = MathF.Sin(pulseProgress * MathF.PI);
                scale += pulseWave * PULSE_AMOUNT;
            }

            float hoverT = _nodeHoverTimers.ContainsKey(node.Id) ? _nodeHoverTimers[node.Id] : 0f;
            if (isSelected) hoverT = 1.0f;
            float popScale = 1.0f + (NODE_HOVER_POP_SCALE_TARGET - 1.0f) * Easing.EaseOutBack(hoverT);
            scale *= popScale;

            float pressT = _nodePressTimers.ContainsKey(node.Id) ? _nodePressTimers[node.Id] : 0f;
            if (pressT > 0)
            {
                float pressScaleFactor = MathHelper.Lerp(1.0f, NODE_PRESS_SCALE_TARGET, Easing.EaseOutQuad(pressT));
                scale *= pressScaleFactor;
            }

            float floatOffset = 0f;
            float rotation = 0f;
            if (hoverT > 0)
            {
                float time = (float)gameTime.TotalGameTime.TotalSeconds;
                float phase = node.Id * 0.5f;
                float blend = Easing.EaseOutQuad(hoverT);
                floatOffset = MathF.Sin(time * NODE_HOVER_FLOAT_SPEED + phase) * NODE_HOVER_FLOAT_AMP * blend;
                rotation = MathF.Sin(time * NODE_HOVER_ROT_SPEED + phase) * NODE_HOVER_ROT_AMT * blend;
                float pulse = MathF.Sin(time * NODE_HOVER_PULSE_SPEED + phase) * NODE_HOVER_PULSE_AMOUNT * blend;
                scale += pulse;
            }

            Vector2 arrivalScale = Vector2.One;
            Vector2 arrivalShake = Vector2.Zero;
            if (node.Id == _playerCurrentNodeId)
            {
                arrivalScale = _nodeArrivalScale;
                arrivalShake = _nodeArrivalShake;
            }

            float selectionScale = 0f;
            Vector2 selectionShake = Vector2.Zero;
            float selectionMult = 1.0f;
            if (isSelected)
            {
                float t = Math.Clamp(_nodeSelectionAnimTimer / NODE_SELECTION_POP_DURATION, 0f, 1f);
                float elastic = Easing.EaseOutElastic(t);
                selectionMult = MathHelper.Lerp(NODE_PRESS_SCALE_TARGET, 1.0f, elastic);
                float shakeDecay = 1.0f - t;
                float shakeX = MathF.Sin(_nodeSelectionAnimTimer * NODE_SELECTION_SHAKE_FREQUENCY) * NODE_SELECTION_SHAKE_MAGNITUDE * shakeDecay;
                float shakeY = MathF.Cos(_nodeSelectionAnimTimer * NODE_SELECTION_SHAKE_FREQUENCY * 0.85f) * NODE_SELECTION_SHAKE_MAGNITUDE * shakeDecay;
                selectionShake = new Vector2(shakeX, shakeY);
            }

            Vector2 finalScale = new Vector2((scale * selectionMult) * arrivalScale.X, (scale * selectionMult) * arrivalScale.Y);
            var position = bounds.Center.ToVector2() + node.VisualOffset + new Vector2(0, floatOffset) + arrivalShake + selectionShake;
            Color outlineColor = _global.Palette_Black;

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
                case SplitNodeType.MajorBattle:
                    texture = _spriteManager.SplitNodeCombat;
                    silhouette = _spriteManager.SplitNodeCombatSilhouette;
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
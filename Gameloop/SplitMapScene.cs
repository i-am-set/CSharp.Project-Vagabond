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
        private readonly PostBattleMenu _postBattleMenu;

        private readonly BirdManager _birdManager;
        private readonly TransitionManager _transitionManager;
        private readonly HapticsManager _hapticsManager;
        private readonly ParticleSystemManager _particleSystemManager;

        private SplitMap? _currentMap;
        private int _playerCurrentNodeId;
        private int _cameraFocusNodeId;
        private readonly PlayerMapIcon _playerIcon;

        // --- MOVEMENT TUNING ---
        private const float CAMERA_LERP_SPEED = 15f;
        private const float POST_EVENT_DELAY = 0.0f;
        private const float PATH_ANIMATION_DURATION = 0.6f;

        // --- AUTO WALK STATE ---
        private bool _isWalking = false;
        private int _currentMoveStep = 0;
        private float _walkStepTimer = 0f;

        private const int TOTAL_MOVE_STEPS = 5; 
        private const float STEP_DURATION = 0.5f; 
        private const float JUMP_HEIGHT = 8f;
        private const float STEP_ROTATION = 15f;

        private int _clickedNodeId = -1;
        private float _clickAnimTimer = 0f;
        private const float CLICK_ANIM_DURATION = 0.25f; // Slightly longer to allow settle
        private const float CLICK_SCALE_MAX = 1.35f; // Snappy max scale
        private const float CLICK_ROTATION_MAGNITUDE = 0.1f;

        // --- NODE ANIMATION TUNING ---
        // Made these much faster for "snappy" feel
        private const float NODE_LIFT_DURATION = 0.25f; // Fast pop up
        private const float PULSE_DURATION = 1.0f; // Settle time
        private const float NODE_LOWERING_DURATION = 0.75f;
        private const float NODE_LIFT_AMOUNT = 8f; // Higher lift for drama
        private const float ARRIVAL_SCALE_MAX = 2.0f; // Big pop on arrival

        private Vector2 _cameraOffset;
        private Vector2 _targetCameraOffset;

        // --- Dynamic Viewport State ---
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

        private enum SplitMapState { Idle, LiftingNode, PulsingNode, EventInProgress, LoweringNode, Resting, PostEventDelay }
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

        private int _selectedNodeId = -1;

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
            _playerMoveTargetNodeId = -1;
            _playerMovePath = null;

            _postBattleMenu.Hide();
            InitializeSettingsButton();
            _settingsButtonState = SettingsButtonState.AnimatingIn;
            _settingsButtonAnimTimer = 0f;

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

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            // --- Dynamic Viewport Logic ---
            float splitLineY = (Global.VIRTUAL_HEIGHT - SplitMapHudRenderer.HUD_HEIGHT) + _hudSlideOffset;
            bool mouseInMap = virtualMousePos.Y < splitLineY;

            if (mouseInMap && !_hudRenderer.IsDragging)
            {
                _hudRenderer.ResetAllFlips();
            }

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

            // Update Click Animation
            if (_clickAnimTimer > 0f)
            {
                _clickAnimTimer -= deltaTime;
                if (_clickAnimTimer < 0f) _clickAnimTimer = 0f;
            }

            // --- AUTO WALK LOGIC ---
            if (_isWalking && _playerMovePath != null)
            {
                _playerIcon.SetIsMoving(true);
                _walkStepTimer += deltaTime;

                float stepProgress = Math.Clamp(_walkStepTimer / STEP_DURATION, 0f, 1f);

                // Calculate Global Progress for path interpolation
                float startRatio = (float)_currentMoveStep / TOTAL_MOVE_STEPS;
                float endRatio = (float)(_currentMoveStep + 1) / TOTAL_MOVE_STEPS;

                // Use EaseOut for "Start fast, end slow" movement within the step
                float moveT = Easing.EaseOutCubic(stepProgress);
                float globalT = MathHelper.Lerp(startRatio, endRatio, moveT);

                Vector2 pathPos = GetPointOnPath(globalT);

                // Add Jump Arc (Sine wave)
                float jumpY = MathF.Sin(stepProgress * MathHelper.Pi) * -JUMP_HEIGHT;
                _playerIcon.SetPosition(pathPos + new Vector2(0, jumpY));

                // Add Rotation (Waddle)
                // Alternate direction based on step index (Even = Left, Odd = Right)
                float rotationDir = (_currentMoveStep % 2 == 0) ? -1f : 1f;
                float targetRotation = MathHelper.ToRadians(STEP_ROTATION) * rotationDir;

                _playerIcon.Rotation = MathF.Sin(stepProgress * MathHelper.Pi) * targetRotation;

                if (_walkStepTimer >= STEP_DURATION)
                {
                    // Step Complete (Landing)
                    _walkStepTimer = 0f;
                    _currentMoveStep++;

                    // Trigger Haptic on landing
                    _hapticsManager.TriggerShake(1.5f, 0.1f);

                    if (_currentMoveStep >= TOTAL_MOVE_STEPS)
                    {
                        // Arrival
                        _isWalking = false;
                        _playerIcon.Rotation = 0f;
                        _playerIcon.SetIsMoving(false);
                        FinalizeArrival();
                    }
                }
            }

            // Only process input if NOT auto-walking
            if (!_isWalking)
            {
                HandleMapInput(gameTime);
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

            _playerIcon.Update(gameTime);
            base.Update(gameTime);
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
                            // Visual Click Effect
                            _clickedNodeId = _hoveredNodeId;
                            _clickAnimTimer = CLICK_ANIM_DURATION;

                            // Logic
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
            // Reset for new movement
            _playerMoveTargetNodeId = targetNodeId;
            _currentMoveStep = 0;
            _walkStepTimer = 0f;
            _isWalking = true;

            // Find path
            _playerMovePath = _currentMap?.Paths.Values.FirstOrDefault(p => p.FromNodeId == _playerCurrentNodeId && p.ToNodeId == targetNodeId);

            // Calculate total length for interpolation
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

            // Reset move state
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

                // No stretch during lift, we rely on the pulse for impact
                _nodeArrivalScale = Vector2.One;
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

            // "Scale instantly to its max, then ease back down"
            // We implement this as a Quadratic Decay from MAX to 1.0.
            float progress = Math.Clamp(_pulseTimer / PULSE_DURATION, 0f, 1f); // 0 -> 1
            float countdown = 1.0f - progress; // 1 -> 0

            // Squared decay creates a fast dropoff: 1.0 -> 0.25 -> 0.0
            float decayCurve = countdown * countdown;

            float scaleBonus = (ARRIVAL_SCALE_MAX - 1.0f) * decayCurve;
            _nodeArrivalScale = new Vector2(1.0f + scaleBonus);
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

                // Slight squash on impact
                if (progress > 0.8f)
                {
                    float squashProgress = (progress - 0.8f) / 0.2f;
                    float squash = MathF.Sin(squashProgress * MathHelper.Pi);
                    _nodeArrivalScale = new Vector2(1.0f + (squash * 0.1f), 1.0f - (squash * 0.1f));
                }
                else _nodeArrivalScale = Vector2.One;
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

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_currentMap == null) return;

            // Snap camera offset to nearest pixel to prevent subpixel artifacts on pixel art
            var snappedCameraOffset = new Vector2(MathF.Round(_cameraOffset.X), MathF.Round(_cameraOffset.Y));
            var cameraTransform = Matrix.CreateTranslation(snappedCameraOffset.X, snappedCameraOffset.Y, 0);
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
            _birdManager.Draw(spriteBatch, snappedCameraOffset);

            spriteBatch.End();

            _particleSystemManager.Draw(spriteBatch, finalTransform);

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
                        Vector2 nodeScreenPos = hoveredNode.Position + snappedCameraOffset;
                        float textX = nodeScreenPos.X - (nodeTextSize.Width / 2f);
                        float textY = nodeScreenPos.Y - 16f - nodeTextSize.Height - 4f;
                        var textPosition = new Vector2(textX, textY);
                        _nodeTextWaveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                        TextAnimator.DrawTextWithEffect(spriteBatch, secondaryFont, nodeText, textPosition, _global.Palette_DarkSun, TextEffectType.Wave, _nodeTextWaveTimer);
                    }
                }
            }
            else _nodeTextWaveTimer = 0f;
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

            if (node.IsCompleted) color = _global.Palette_DarkShadow;
            else if (node.NodeType != SplitNodeType.Origin && node.Id != _playerCurrentNodeId && !node.IsReachable) color = _global.Palette_DarkShadow;

            bool isSelected = (node.Id == _selectedNodeId);
            bool isHovered = (node.Id == _hoveredNodeId);
            bool isAnimatingThisNode = (node.Id == _playerCurrentNodeId) &&
                                       (_mapState == SplitMapState.LiftingNode ||
                                        _mapState == SplitMapState.PulsingNode ||
                                        _mapState == SplitMapState.EventInProgress ||
                                        _mapState == SplitMapState.LoweringNode);

            if (node.IsReachable && (isHovered || isSelected || isAnimatingThisNode)) color = _global.ButtonHoverColor;

            Vector2 scale = Vector2.One;
            Vector2 shake = Vector2.Zero;

            if (node.Id == _playerCurrentNodeId)
            {
                scale = _nodeArrivalScale;
                shake = _nodeArrivalShake;
            }

            // Apply Click Scale & Rotation Effect
            float rotation = 0f;
            if (node.Id == _clickedNodeId && _clickAnimTimer > 0f)
            {
                // Quadratic Decay from Max Scale to 1.0
                float t = _clickAnimTimer / CLICK_ANIM_DURATION; // 1 -> 0
                float decay = t * t; // Sharp dropoff

                // Base 1.0 + Extra
                float scaleBonus = (CLICK_SCALE_MAX - 1.0f) * decay;
                scale = new Vector2(1.0f + scaleBonus);

                // Quick shake rotation
                rotation = MathF.Sin(t * MathHelper.TwoPi) * CLICK_ROTATION_MAGNITUDE;
            }

            var position = bounds.Center.ToVector2() + node.VisualOffset + shake;
            Color outlineColor = _global.Palette_Black;

            if (silhouette != null)
            {
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(-1, 0), sourceRect, outlineColor, rotation, origin, scale, SpriteEffects.None, 0.4f);
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(1, 0), sourceRect, outlineColor, rotation, origin, scale, SpriteEffects.None, 0.4f);
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(0, -1), sourceRect, outlineColor, rotation, origin, scale, SpriteEffects.None, 0.4f);
                spriteBatch.DrawSnapped(silhouette, position + new Vector2(0, 1), sourceRect, outlineColor, rotation, origin, scale, SpriteEffects.None, 0.4f);
            }
            spriteBatch.DrawSnapped(texture, position, sourceRect, color, rotation, origin, scale, SpriteEffects.None, 0.4f);
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
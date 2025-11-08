#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
using ProjectVagabond.Progression;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class SplitMapScene : GameScene
    {
        private class FadingElement
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Age;
            public float Lifetime;
            public Texture2D? Texture;
            public Rectangle? SourceRect;
            public Vector2 Origin;
            public float Scale;
            public float Rotation;
            public float RotationSpeed;
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


        private SplitMap? _currentMap;
        private int _playerCurrentNodeId;
        private readonly PlayerMapIcon _playerIcon;
        private NarrativeDialog _narrativeDialog;

        // --- Animation Tuning ---
        private const float PLAYER_MOVE_SPEED = 150f; // Pixels per second
        private const float CAMERA_LERP_SPEED = 5f;
        private const float NODE_LIFT_DURATION = 0.2f;
        private const float PULSE_DURATION = 0.15f;
        private const float NODE_LOWERING_DURATION = 0.2f;
        private const float POST_EVENT_DELAY = 0.25f;
        private const float PATH_ANIMATION_DURATION = 0.75f;
        private const float UNSELECTED_FADE_OUT_DURATION = 0.6f;


        private Vector2 _cameraOffset;
        private Vector2 _targetCameraOffset;

        private float _playerMoveTimer;
        private float _playerMoveDuration;
        private int _playerMoveTargetNodeId;
        private SplitMapPath? _playerMovePath;

        // Node interaction state
        private int _hoveredNodeId = -1;
        private readonly HashSet<int> _visitedNodeIds = new HashSet<int>();
        private int _nodeForPathReveal = -1;
        private int _lastAnimatedFloor = -1;

        // Path animation state
        private readonly Dictionary<int, float> _pathAnimationProgress = new();
        private readonly Dictionary<int, float> _pathAnimationDurations = new();
        private const string PATH_DRAW_PATTERN = "1111110111010111111001110111110011111111110101100"; // 1 = draw, 0 = skip. Creates a dashed line effect.
        private static readonly Random _random = new Random();

        // Path Color Variation Tuning
        private const float PATH_COLOR_VARIATION_MIN = 0.1f;
        private const float PATH_COLOR_VARIATION_MAX = 1.0f;
        private const float PATH_COLOR_NOISE_SCALE = 0.3f;
        private const float PATH_EXCLUSION_RADIUS = 5f; // Half of a 20x20 exclusion zone
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

        // Camera Tuning
        private const float CAMERA_TOP_PADDING = 20f;

        // Floor Reveal Animation
        private bool _isFadingInNextFloor = false;
        private float _floorFadeInTimer = 0f;
        private const float FLOOR_FADE_IN_DURATION = 0.5f;

        // Node Animation
        private const float NODE_FRAME_DURATION = 0.5f;
        private float _nodeHoverTextBobTimer = 0f;
        private float _nodeLiftTimer = 0f;
        private const float NODE_LIFT_AMOUNT = 15f;
        private readonly List<FadingElement> _fadingElements = new();
        private readonly HashSet<int> _fadingPathIds = new();
        private readonly HashSet<int> _fadingNodeIds = new();


        public static bool PlayerWonLastBattle { get; set; } = true;
        public static bool WasMajorBattle { get; set; } = false;

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

            var narratorBounds = new Rectangle(0, Global.VIRTUAL_HEIGHT - 50, Global.VIRTUAL_WIDTH, 50);
            _resultNarrator = new StoryNarrator(narratorBounds);
            _resultNarrator.OnFinished += OnResultNarrationFinished;
        }

        public override Rectangle GetAnimatedBounds() => new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

        public override void Enter()
        {
            base.Enter();
            _playerIcon.SetIsMoving(false);
            _diceRollingSystem.OnRollCompleted += OnDiceRollCompleted;

            if (_progressionManager.CurrentSplitMap == null)
            {
                _progressionManager.GenerateNewSplitMap();
                _currentMap = _progressionManager.CurrentSplitMap;
                _playerCurrentNodeId = _currentMap?.StartNodeId ?? -1;
                _nodeForPathReveal = _playerCurrentNodeId;
                _lastAnimatedFloor = -1;
                _pathAnimationProgress.Clear();
                _pathAnimationDurations.Clear();
                _fadingElements.Clear();
                _fadingPathIds.Clear();
                _fadingNodeIds.Clear();

                _visitedNodeIds.Clear();
                _visitedNodeIds.Add(_playerCurrentNodeId);

                var startNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (startNode != null)
                {
                    _playerIcon.SetPosition(startNode.Position);
                    UpdateCameraTarget(startNode.Position, true);
                    // The initial reveal happens after the "event" (which is none for the start node)
                    GenerateAndRevealNextColumn();
                }
            }
            else
            {
                _currentMap = _progressionManager.CurrentSplitMap;
                // This handles returning from a battle/event
                if (WasMajorBattle && PlayerWonLastBattle)
                {
                    WasMajorBattle = false;
                    TriggerReward();
                }
                else
                {
                    // On returning from a normal battle, mark the node as completed and start the lowering animation.
                    var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (currentNode != null)
                    {
                        currentNode.IsCompleted = true;
                        UpdateCameraTarget(currentNode.Position, false); // Ensure camera is centered
                    }
                    _mapState = SplitMapState.LoweringNode; // Transition to lowering state
                    _nodeLiftTimer = 0f; // Reset timer for the lowering animation
                    GenerateAndRevealNextColumn(); // Reveal next floor immediately
                }
            }
        }

        public override void Exit()
        {
            base.Exit();
            _diceRollingSystem.OnRollCompleted -= OnDiceRollCompleted;
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

            // Concurrently update camera and start pruning animations
            var targetNode = _currentMap.Nodes[targetNodeId];
            UpdateCameraTarget(targetNode.Position, false);

            // Identify unselected paths and nodes to start their fade/retraction animations.
            if (_currentMap.Nodes.TryGetValue(_playerCurrentNodeId, out var currentNode))
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                var visitedPathFillColor = _global.Palette_White;
                var selectedPathPixels = new HashSet<Point>(_playerMovePath.PixelPoints);

                foreach (var pathId in currentNode.OutgoingPathIds)
                {
                    if (pathId != _playerMovePath.Id)
                    {
                        if (_currentMap.Paths.TryGetValue(pathId, out var unselectedPath))
                        {
                            _fadingPathIds.Add(unselectedPath.Id);
                            _fadingNodeIds.Add(unselectedPath.ToNodeId);

                            // Create particles for the path pixels THAT ARE NOT SHARED
                            foreach (var point in unselectedPath.PixelPoints)
                            {
                                if (!selectedPathPixels.Contains(point))
                                {
                                    var element = new FadingElement
                                    {
                                        Position = point.ToVector2(),
                                        Velocity = new Vector2((float)(_random.NextDouble() * 4 - 2), (float)(_random.NextDouble() * -6 - 1)),
                                        Color = visitedPathFillColor,
                                        Age = 0f,
                                        Lifetime = UNSELECTED_FADE_OUT_DURATION,
                                        Texture = pixel,
                                        SourceRect = null,
                                        Origin = new Vector2(0.5f),
                                        Scale = 1f,
                                        Rotation = 0f,
                                        RotationSpeed = 0f
                                    };
                                    _fadingElements.Add(element);
                                }
                            }

                            // Create a particle for the destination node
                            if (_currentMap.Nodes.TryGetValue(unselectedPath.ToNodeId, out var unselectedNode))
                            {
                                var (tex, src, origin) = GetNodeDrawData(unselectedNode, new GameTime());
                                var nodeElement = new FadingElement
                                {
                                    Position = unselectedNode.Position,
                                    Velocity = new Vector2((float)(_random.NextDouble() * 2 - 1), (float)(_random.NextDouble() * -4 - 1)),
                                    Color = Color.White,
                                    Age = 0f,
                                    Lifetime = UNSELECTED_FADE_OUT_DURATION,
                                    Texture = tex,
                                    SourceRect = src,
                                    Origin = origin,
                                    Scale = 1f,
                                    Rotation = 0f,
                                    RotationSpeed = (float)(_random.NextDouble() * 2 - 1)
                                };
                                _fadingElements.Add(nodeElement);
                            }
                        }
                    }
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_hoveredNodeId != -1)
            {
                _nodeHoverTextBobTimer += deltaTime;
            }

            // Handle modal dialogs first, as they pause the main scene logic
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

            // Check if a modal was just closed
            if (_wasModalActiveLastFrame)
            {
                _wasModalActiveLastFrame = false;
                var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (currentNode != null)
                {
                    currentNode.IsCompleted = true;
                }
                _mapState = SplitMapState.LoweringNode;
                _nodeLiftTimer = 0f;
                GenerateAndRevealNextColumn();
            }

            // Handle event states that pause map interaction
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

            // Smooth camera scrolling
            _cameraOffset = Vector2.Lerp(_cameraOffset, _targetCameraOffset, deltaTime * CAMERA_LERP_SPEED);

            // Update floor fade-in animation
            if (_isFadingInNextFloor)
            {
                _floorFadeInTimer += deltaTime;
                if (_floorFadeInTimer >= FLOOR_FADE_IN_DURATION)
                {
                    _floorFadeInTimer = FLOOR_FADE_IN_DURATION;
                    _isFadingInNextFloor = false;
                }
            }

            // Update active path animations
            var appearingKeys = _pathAnimationProgress.Keys.ToList();
            foreach (var pathId in appearingKeys)
            {
                float duration = _pathAnimationDurations.GetValueOrDefault(pathId, PATH_ANIMATION_DURATION);
                if (_pathAnimationProgress[pathId] < duration)
                {
                    _pathAnimationProgress[pathId] += deltaTime;
                }
            }

            // Update fading elements
            for (int i = _fadingElements.Count - 1; i >= 0; i--)
            {
                var element = _fadingElements[i];
                element.Age += deltaTime;
                if (element.Age >= element.Lifetime)
                {
                    _fadingElements.RemoveAt(i);
                }
                else
                {
                    element.Position += element.Velocity * deltaTime;
                    element.Rotation += element.RotationSpeed * deltaTime;
                }
            }

            switch (_mapState)
            {
                case SplitMapState.Idle:
                    HandleNodeInput();
                    break;
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
                    }
                    break;
            }

            _playerIcon.Update(gameTime);

            base.Update(gameTime);
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
                int fromNodeId = _playerCurrentNodeId;
                _playerCurrentNodeId = _playerMoveTargetNodeId;
                _visitedNodeIds.Add(_playerCurrentNodeId);

                var toNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (_currentMap != null && toNode != null)
                {
                    _playerIcon.SetPosition(toNode.Position);
                    var fromNode = _currentMap.Nodes[fromNodeId];
                    _currentMap.PruneColumn(fromNode.Floor, fromNode.Id);
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
                // Animate from lifted position back to zero
                currentNode.VisualOffset = new Vector2(0, MathHelper.Lerp(-NODE_LIFT_AMOUNT, 0, Easing.EaseOutCubic(progress)));
            }

            if (_nodeLiftTimer >= NODE_LOWERING_DURATION)
            {
                // Snap to final position and transition to the next state
                if (currentNode != null)
                {
                    currentNode.VisualOffset = Vector2.Zero;
                }
                _mapState = SplitMapState.PostEventDelay;
                _postEventDelayTimer = POST_EVENT_DELAY;
            }
        }

        private void HandleNodeInput()
        {
            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);

            var cameraTransform = Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0);
            Matrix.Invert(ref cameraTransform, out var inverseCameraTransform);
            var mouseInMapSpace = Vector2.Transform(virtualMousePos, inverseCameraTransform);

            bool leftClickPressed = mouseState.LeftButton == ButtonState.Pressed && base.previousMouseState.LeftButton == ButtonState.Released;

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

            if (leftClickPressed && _hoveredNodeId != -1 && UIInputManager.CanProcessMouseClick())
            {
                StartPlayerMove(_hoveredNodeId);
                _hoveredNodeId = -1; // Clear hover state immediately on click
                UIInputManager.ConsumeMouseClick();
            }
        }


        private void UpdateCameraTarget(Vector2 targetNodePosition, bool snap)
        {
            if (_currentMap == null) return;

            // This logic now centers the camera on the target node, positioning it in the left quarter of the screen.
            float targetX = (Global.VIRTUAL_WIDTH / 4f) - targetNodePosition.X;
            float targetY = (Global.VIRTUAL_HEIGHT / 2f) - targetNodePosition.Y;

            _targetCameraOffset = new Vector2(targetX, targetY);

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

            if (currentNode.Floor != _lastAnimatedFloor)
            {
                _isFadingInNextFloor = true;
                _floorFadeInTimer = 0f;

                _lastAnimatedFloor = currentNode.Floor;
                if (_currentMap != null && _currentMap.Nodes.TryGetValue(_nodeForPathReveal, out var nodeForPaths))
                {
                    foreach (var pathId in nodeForPaths.OutgoingPathIds)
                    {
                        if (!_pathAnimationProgress.ContainsKey(pathId))
                        {
                            _pathAnimationProgress[pathId] = 0f;
                            float duration = PATH_ANIMATION_DURATION + (float)(_random.NextDouble() * 2.0 - 1.0);
                            _pathAnimationDurations[pathId] = Math.Max(1.0f, duration);
                        }
                    }
                }
            }
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
                    BattleSetup.EnemyArchetypes = node.EventData as List<string>;
                    BattleSetup.ReturnSceneState = GameSceneState.Split;
                    _sceneManager.ChangeScene(GameSceneState.Battle);
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
                    }
                    break;

                case SplitNodeType.Reward:
                    TriggerReward();
                    break;
            }
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
            GenerateAndRevealNextColumn();
            _resultNarrator.Clear();
        }

        private void GenerateAndRevealNextColumn()
        {
            if (_currentMap == null) return;

            var currentNode = _currentMap.Nodes.GetValueOrDefault(_playerCurrentNodeId);
            if (currentNode == null) return;

            int highestFloorInMap = _currentMap.Nodes.Values.Max(n => n.Floor);
            if (currentNode.Floor == highestFloorInMap && currentNode.Floor < _currentMap.TargetColumnCount - 1)
            {
                SplitMapGenerator.GenerateNextFloor(_currentMap, _progressionManager.CurrentSplit, currentNode.Id);
            }

            UpdateReachableNodes();
            StartPathRevealAnimation();
            UpdateCameraTarget(currentNode.Position, false);
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

            var playerAbilities = _componentStore.GetComponent<PassiveAbilitiesComponent>(_gameState.PlayerEntityId)?.AbilityIDs;
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

            var cameraTransform = Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0);
            var finalTransform = cameraTransform * transform;

            spriteBatch.End();
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: finalTransform);

            var pixel = ServiceLocator.Get<Texture2D>();
            var visitedPathFillColor = _global.Palette_White;
            var highlightedPathFillColor = _global.Palette_Yellow;

            SplitMapPath? highlightedPath = null;
            if (_hoveredNodeId != -1 && _mapState == SplitMapState.Idle)
            {
                highlightedPath = _currentMap.Paths.Values.FirstOrDefault(p => p.FromNodeId == _playerCurrentNodeId && p.ToNodeId == _hoveredNodeId);
            }

            foreach (var path in _currentMap.Paths.Values)
            {
                if (path == highlightedPath) continue;
                DrawPath(spriteBatch, pixel, path, visitedPathFillColor);
            }

            if (highlightedPath != null)
            {
                DrawPath(spriteBatch, pixel, highlightedPath, highlightedPathFillColor);
            }

            int currentPlayerFloor = _currentMap.Nodes[_playerCurrentNodeId].Floor;
            foreach (var node in _currentMap.Nodes.Values)
            {
                if (_fadingNodeIds.Contains(node.Id)) continue;

                float nodeAlpha = 0f;
                bool isNextFloor = node.Floor == currentPlayerFloor + 1;

                if (node.Floor <= currentPlayerFloor || _visitedNodeIds.Contains(node.Id))
                {
                    nodeAlpha = 1.0f;
                }
                else if (isNextFloor)
                {
                    if (_isFadingInNextFloor)
                    {
                        float progress = Math.Clamp(_floorFadeInTimer / FLOOR_FADE_IN_DURATION, 0f, 1f);
                        nodeAlpha = Easing.EaseOutQuad(progress);
                    }
                    else if (_lastAnimatedFloor == currentPlayerFloor)
                    {
                        nodeAlpha = 1.0f;
                    }
                }

                if (nodeAlpha <= 0.01f) continue;

                var (texture, sourceRect, origin) = GetNodeDrawData(node, gameTime);
                var bounds = node.GetBounds();
                var color = Color.White;
                float scale = 1.0f;

                if (node.IsCompleted)
                {
                    color = _global.Palette_DarkGray;
                }
                else if (!node.IsReachable)
                {
                    color = _global.Palette_DarkGray;
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
                spriteBatch.DrawSnapped(texture, position, sourceRect, color * nodeAlpha, 0f, origin, scale, SpriteEffects.None, 0.4f);
            }

            foreach (var element in _fadingElements)
            {
                float progress = element.Age / element.Lifetime;
                float alpha = 0.25f * (1.0f - Easing.EaseOutQuad(progress));
                spriteBatch.DrawSnapped(element.Texture, element.Position, element.SourceRect, element.Color * alpha, element.Rotation, element.Origin, element.Scale, SpriteEffects.None, 0.35f);
            }

            _playerIcon.Draw(spriteBatch);

            spriteBatch.End();
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

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
                        SplitNodeType.Narrative => "EVENT",
                        SplitNodeType.Reward => "REWARD",
                        SplitNodeType.MajorBattle => "MAJOR BATTLE",
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(nodeText))
                    {
                        var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                        var textSize = secondaryFont.MeasureString(nodeText);
                        float yOffset = (MathF.Sin(_nodeHoverTextBobTimer * 4f) > 0) ? -1f : 0f;
                        var textPosition = new Vector2((Global.VIRTUAL_WIDTH - textSize.Width) / 2f, Global.VIRTUAL_HEIGHT - textSize.Height - 3 + yOffset);
                        spriteBatch.DrawStringSnapped(secondaryFont, nodeText, textPosition, _global.Palette_Yellow);
                    }
                }
            }

            if (_narrativeDialog.IsActive) _narrativeDialog.DrawContent(spriteBatch, font, gameTime, transform);
            if (_eventState == EventState.NarratingResult) _resultNarrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);
        }

        private void DrawPath(SpriteBatch spriteBatch, Texture2D pixel, SplitMapPath path, Color fillColor)
        {
            if (_currentMap == null || path.PixelPoints.Count < 2 || _fadingPathIds.Contains(path.Id)) return;

            var fromNode = _currentMap.Nodes[path.FromNodeId];
            var toNode = _currentMap.Nodes[path.ToNodeId];

            int numPixelsToDraw;
            float pathAlpha = 1.0f;

            if (fromNode.Id == _nodeForPathReveal)
            {
                float duration = _pathAnimationDurations.GetValueOrDefault(path.Id, PATH_ANIMATION_DURATION);
                float animationTimer = _pathAnimationProgress.GetValueOrDefault(path.Id, 0f);
                float linearProgress = Math.Clamp((animationTimer * 2f) / duration, 0f, 1f);
                if (linearProgress <= 0f) return;
                float easedProgress = Easing.EaseOutCubic(linearProgress);
                numPixelsToDraw = (int)(easedProgress * path.PixelPoints.Count);
            }
            else if (_visitedNodeIds.Contains(fromNode.Id))
            {
                numPixelsToDraw = path.PixelPoints.Count;
            }
            else
            {
                return;
            }

            if (numPixelsToDraw <= 0 || pathAlpha <= 0.01f) return;

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

                    alpha *= pathAlpha;

                    if (alpha > 0.01f)
                    {
                        float noise = _gameState.NoiseManager.GetNoiseValue(NoiseMapType.Resources, point.X * PATH_COLOR_NOISE_SCALE, point.Y * PATH_COLOR_NOISE_SCALE);
                        float multiplier = MathHelper.Lerp(PATH_COLOR_VARIATION_MIN, PATH_COLOR_VARIATION_MAX, noise);
                        Color pixelColor = new Color(fillColor.ToVector3() * multiplier);
                        spriteBatch.Draw(pixel, point.ToVector2(), pixelColor * alpha);
                    }
                }
            }
        }


        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_narrativeDialog.IsActive)
            {
                _narrativeDialog.DrawOverlay(spriteBatch);
            }
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
                    texture = node.Difficulty switch
                    {
                        BattleDifficulty.Easy => _spriteManager.CombatNodeEasySprite,
                        BattleDifficulty.Hard => _spriteManager.CombatNodeHardSprite,
                        _ => _spriteManager.CombatNodeNormalSprite,
                    };
                    break;
                case SplitNodeType.Narrative:
                    texture = _spriteManager.SplitNodeNarrative;
                    break;
                case SplitNodeType.Reward:
                    texture = _spriteManager.SplitNodeReward;
                    break;
                case SplitNodeType.MajorBattle:
                    texture = _spriteManager.SplitNodeBoss;
                    break;
                default:
                    texture = _spriteManager.CombatNodeNormalSprite; // Fallback
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
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
        private readonly ProgressionManager _progressionManager;
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly DiceRollingSystem _diceRollingSystem;
        private readonly StoryNarrator _resultNarrator;
        private readonly ChoiceGenerator _choiceGenerator;


        private SplitMap? _currentMap;
        private int _playerCurrentNodeId;
        private readonly PlayerMapIcon _playerIcon;
        private NarrativeDialog _narrativeDialog;

        private float _cameraYOffset;
        private float _targetCameraYOffset;

        private bool _isPlayerMoving;
        private float _playerMoveTimer;
        private float _playerMoveDuration;
        private const float PLAYER_MOVE_SPEED = 15f; // Pixels per second
        private int _playerMoveTargetNodeId;
        private SplitMapPath? _playerMovePath;

        // Node interaction state
        private int _hoveredNodeId = -1;
        private int _pressedNodeId = -1;
        private readonly HashSet<int> _visitedNodeIds = new HashSet<int>();
        private int _nodeForPathReveal = -1;
        private int _lastAnimatedFloor = -1;

        // Path animation state
        private readonly Dictionary<int, float> _pathAnimationProgress = new();
        private readonly Dictionary<int, float> _pathRetractionProgress = new();
        private readonly Dictionary<int, float> _pathAnimationDurations = new();
        private const float PATH_ANIMATION_DURATION = 3.0f; // Seconds for the path to draw
        private const string PATH_DRAW_PATTERN = "1111110111010111111001110111110011111111110101100"; // 1 = draw, 0 = skip. Creates a dashed line effect.
        private static readonly Random _random = new Random();

        // Path Color Variation Tuning
        private const float PATH_COLOR_VARIATION_MIN = 0.1f;
        private const float PATH_COLOR_VARIATION_MAX = 1.0f;
        private const float PATH_COLOR_NOISE_SCALE = 0.3f;
        private const float PATH_EXCLUSION_RADIUS = 5f; // Half of a 20x20 exclusion zone
        private const float PATH_FADE_DISTANCE = 12f;


        // Node pulse animation state
        private bool _isPulsingNode = false;
        private int _pulseNodeId = -1;
        private float _pulseTimer = 0f;
        private const float PULSE_DURATION = 0.4f;
        private const float PULSE_AMOUNT = 0.3f;

        private bool _wasModalActiveLastFrame = false;

        // Dice Roll State
        private enum EventState { Idle, AwaitingDiceRoll, NarratingResult }
        private EventState _eventState = EventState.Idle;
        private NarrativeChoice? _pendingChoiceForDiceRoll;

        // Camera Tuning
        private const float CAMERA_TOP_PADDING = 20f;

        // Floor Reveal Animation
        private bool _isFadingInNextFloor = false;
        private float _floorFadeInTimer = 0f;
        private const float FLOOR_FADE_IN_DURATION = 0.5f;

        // Node Animation
        private const float NODE_FRAME_DURATION = 0.5f;
        private float _nodeHoverTextBobTimer = 0f;


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

            var narratorBounds = new Rectangle(0, Global.VIRTUAL_HEIGHT - 80, Global.VIRTUAL_WIDTH, 80);
            _resultNarrator = new StoryNarrator(narratorBounds);
            _resultNarrator.OnFinished += OnResultNarrationFinished;
        }

        public override Rectangle GetAnimatedBounds() => new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

        public override void Enter()
        {
            base.Enter();
            _isPlayerMoving = false;
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
                _pathRetractionProgress.Clear();
                _pathAnimationDurations.Clear();

                _visitedNodeIds.Clear();
                _visitedNodeIds.Add(_playerCurrentNodeId);

                var startNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (startNode != null)
                {
                    _playerIcon.SetPosition(startNode.Position);
                    UpdateCameraTarget(startNode.Position, true);
                    StartPathRevealAnimation();
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
                    // On returning to the scene (e.g., from battle)
                    var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
                    if (currentNode != null)
                    {
                        UpdateCameraTarget(currentNode.Position, false);
                        StartPathRevealAnimation();
                    }
                }
            }

            UpdateReachableNodes();
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

            _isPlayerMoving = true;
            _playerIcon.SetIsMoving(true);
            _playerMoveTimer = 0f;
            _playerMoveTargetNodeId = targetNodeId;

            // Identify the winning path and start retraction on all other outgoing paths
            if (_currentMap.Nodes.TryGetValue(_playerCurrentNodeId, out var currentNode))
            {
                foreach (var pathId in currentNode.OutgoingPathIds)
                {
                    if (pathId != _playerMovePath.Id)
                    {
                        _pathAnimationProgress.Remove(pathId);
                        _pathRetractionProgress[pathId] = 0f;
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
                    UpdateCameraTarget(currentNode.Position, false);
                    StartPathRevealAnimation();
                    UpdateReachableNodes();
                }
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
            _cameraYOffset = MathHelper.Lerp(_cameraYOffset, _targetCameraYOffset, deltaTime * 5f);

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

            // Update retracting path animations
            var retractingKeys = _pathRetractionProgress.Keys.ToList();
            foreach (var pathId in retractingKeys)
            {
                float duration = _pathAnimationDurations.GetValueOrDefault(pathId, PATH_ANIMATION_DURATION);
                if (_pathRetractionProgress[pathId] < duration)
                {
                    _pathRetractionProgress[pathId] += deltaTime;
                }
                else
                {
                    _pathRetractionProgress.Remove(pathId);
                }
            }


            if (_isPlayerMoving)
            {
                _playerMoveTimer += deltaTime;
                float progress = 1f;
                if (_playerMoveDuration > 0)
                {
                    progress = Math.Clamp(_playerMoveTimer / _playerMoveDuration, 0f, 1f);
                }

                if (_playerMovePath != null && _playerMovePath.PixelPoints.Any())
                {
                    int targetIndex = (int)Math.Clamp(progress * (_playerMovePath.PixelPoints.Count - 1), 0, _playerMovePath.PixelPoints.Count - 1);
                    Vector2 newPosition = _playerMovePath.PixelPoints[targetIndex].ToVector2();
                    _playerIcon.SetPosition(newPosition);
                }

                if (progress >= 1f)
                {
                    _isPlayerMoving = false;
                    _playerIcon.SetIsMoving(false);
                    _playerCurrentNodeId = _playerMoveTargetNodeId;
                    _visitedNodeIds.Add(_playerCurrentNodeId);

                    if (_currentMap != null && _currentMap.Nodes.TryGetValue(_playerCurrentNodeId, out var endNode))
                    {
                        _playerIcon.SetPosition(endNode.Position); // Snap to final position
                    }

                    // Start the pulse animation and enter the pulsing state
                    _pulseNodeId = _playerCurrentNodeId;
                    _pulseTimer = 0f;
                    _isPulsingNode = true;
                }
            }
            else if (_isPulsingNode)
            {
                _pulseTimer += deltaTime;
                if (_pulseTimer >= PULSE_DURATION)
                {
                    _isPulsingNode = false;
                    _pulseNodeId = -1; // Reset for drawing
                    _pulseTimer = 0f;
                    TriggerNodeEvent(_playerCurrentNodeId);
                }
            }
            else if (_eventState == EventState.Idle)
            {
                HandleNodeInput();
            }

            _playerIcon.Update(gameTime);

            base.Update(gameTime); // Call base update at the end to correctly update previous input states for the next frame.
        }

        private void HandleNodeInput()
        {
            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);

            var cameraTransform = Matrix.CreateTranslation(0, _cameraYOffset, 0);
            Matrix.Invert(ref cameraTransform, out var inverseCameraTransform);
            var mouseInMapSpace = Vector2.Transform(virtualMousePos, inverseCameraTransform);

            bool leftClickPressed = mouseState.LeftButton == ButtonState.Pressed && base.previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released && base.previousMouseState.LeftButton == ButtonState.Pressed;

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

            if (leftClickPressed && _hoveredNodeId != -1)
            {
                _pressedNodeId = _hoveredNodeId;
            }

            if (leftClickReleased)
            {
                if (_pressedNodeId != -1 && _pressedNodeId == _hoveredNodeId && UIInputManager.CanProcessMouseClick())
                {
                    StartPlayerMove(_pressedNodeId);
                    UIInputManager.ConsumeMouseClick();
                }
                _pressedNodeId = -1;
            }
        }


        private void UpdateCameraTarget(Vector2 playerPosition, bool snap)
        {
            if (_currentMap == null) return;
            float mapHeight = _currentMap.MapHeight;
            float viewHeight = Global.VIRTUAL_HEIGHT;

            var currentNode = _currentMap.Nodes.GetValueOrDefault(_playerCurrentNodeId);
            if (currentNode != null)
            {
                int nextFloor = currentNode.Floor + 1;
                var allNextFloorNodes = _currentMap.Nodes.Values
                    .Where(n => n.Floor == nextFloor)
                    .ToList();

                if (allNextFloorNodes.Any())
                {
                    // Find the highest node (minimum Y value) among ALL nodes on the next floor.
                    float highestNodeY = allNextFloorNodes.Min(n => n.Position.Y);

                    // The target Y for the top of the screen is `highestNodeY - topPadding`.
                    // The camera offset is the negative of this value.
                    _targetCameraYOffset = -(highestNodeY - CAMERA_TOP_PADDING);
                }
                else
                {
                    // Fallback for the last node (boss) or any node with no outgoing paths.
                    // Center the player in the lower third of the screen.
                    float targetScreenY = viewHeight * 2 / 3f;
                    _targetCameraYOffset = targetScreenY - playerPosition.Y;
                }
            }
            else // Fallback if current node is somehow not found
            {
                float targetScreenY = viewHeight * 2 / 3f;
                _targetCameraYOffset = targetScreenY - playerPosition.Y;
            }

            // Clamp the camera offset so we don't see beyond the map's top or bottom
            float minOffset = viewHeight - mapHeight;
            _targetCameraYOffset = Math.Clamp(_targetCameraYOffset, minOffset, 0);

            if (snap)
            {
                _cameraYOffset = _targetCameraYOffset;
            }
        }

        private void StartPathRevealAnimation()
        {
            _nodeForPathReveal = _playerCurrentNodeId;
            var currentNode = _currentMap?.Nodes[_nodeForPathReveal];
            if (currentNode == null) return;

            // Check if we need to start new animations for a new node/floor
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
                            // Assign a random duration for this path's animation
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
                // No dice roll, resolve outcome immediately
                ResolveNarrativeChoice(choice, -1);
            }
            else
            {
                // Dice roll required
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
                // Filter outcomes by the dice roll if a DC is present
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
            _resultNarrator.Clear();
            var currentNode = _currentMap?.Nodes[_playerCurrentNodeId];
            if (currentNode != null)
            {
                UpdateCameraTarget(currentNode.Position, false);
                StartPathRevealAnimation();
                UpdateReachableNodes();
            }
        }


        private void TriggerReward()
        {
            var choiceMenu = _sceneManager.GetScene(GameSceneState.ChoiceMenu) as ChoiceMenuScene;
            if (choiceMenu == null) return;

            // Generate choices
            var choices = _choiceGenerator.GenerateSpellChoices(1, 3).Cast<object>().ToList(); // TODO: Use real game stage

            // Define the action to take after a choice is made
            Action onChoiceMade = () => _sceneManager.HideModal();

            // Show the menu
            choiceMenu.Show(choices, onChoiceMade);
            _wasModalActiveLastFrame = true;
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_currentMap == null) return;

            var cameraTransform = Matrix.CreateTranslation(0, _cameraYOffset, 0);
            var finalTransform = cameraTransform * transform;

            spriteBatch.End(); // End the default batch
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: finalTransform);

            var pixel = ServiceLocator.Get<Texture2D>();
            var visitedPathFillColor = _global.Palette_Gray;
            var highlightedPathFillColor = _global.Palette_Yellow;

            // Find highlighted path before drawing
            SplitMapPath? highlightedPath = null;
            if (_hoveredNodeId != -1 && !_isPlayerMoving)
            {
                highlightedPath = _currentMap.Paths.Values.FirstOrDefault(p => p.FromNodeId == _playerCurrentNodeId && p.ToNodeId == _hoveredNodeId);
            }

            // Draw all non-highlighted paths first
            foreach (var path in _currentMap.Paths.Values)
            {
                if (path == highlightedPath) continue;
                DrawPath(spriteBatch, pixel, path, visitedPathFillColor);
            }

            // Draw highlighted path on top
            if (highlightedPath != null)
            {
                DrawPath(spriteBatch, pixel, highlightedPath, highlightedPathFillColor);
            }

            // Draw Nodes
            int currentPlayerFloor = _currentMap.Nodes[_playerCurrentNodeId].Floor;
            foreach (var node in _currentMap.Nodes.Values)
            {
                if (node.NodeType == SplitNodeType.Origin) continue; // Skip drawing the origin node

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
                        // If the fade-in is complete for this floor, it should be fully visible.
                        nodeAlpha = 1.0f;
                    }
                }

                if (nodeAlpha <= 0.01f) continue;

                var (texture, sourceRect, origin) = GetNodeDrawData(node, gameTime);
                var bounds = node.GetBounds();
                var color = Color.White;
                float scale = 1.0f;
                Vector2 hoverOffset = Vector2.Zero;

                if (!node.IsReachable)
                {
                    color = _global.Palette_DarkGray;
                }
                else if (node.Id == _pressedNodeId)
                {
                    scale = 0.9f;
                    color = _global.ButtonHoverColor;
                }
                else if (node.Id == _hoveredNodeId)
                {
                    hoverOffset.Y = -1f;
                    color = _global.ButtonHoverColor;
                }

                if (node.Id == _pulseNodeId)
                {
                    float pulseProgress = Math.Clamp(_pulseTimer / PULSE_DURATION, 0f, 1f);
                    float pulseWave = MathF.Sin(pulseProgress * MathF.PI); // Creates a 0 -> 1 -> 0 wave
                    scale += pulseWave * PULSE_AMOUNT;
                }

                var position = bounds.Center.ToVector2() + hoverOffset;
                spriteBatch.DrawSnapped(texture, position, sourceRect, color * nodeAlpha, 0f, origin, scale, SpriteEffects.None, 0.4f);
            }


            // Draw Player Icon
            _playerIcon.Draw(spriteBatch);

            spriteBatch.End(); // End the camera-transformed batch
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform); // Re-begin the original batch

            // Draw Node Hover Text
            if (_hoveredNodeId != -1 && !_isPlayerMoving && !_narrativeDialog.IsActive && _eventState == EventState.Idle)
            {
                if (_currentMap.Nodes.TryGetValue(_hoveredNodeId, out var hoveredNode) && hoveredNode.IsReachable)
                {
                    string nodeText = "";
                    switch (hoveredNode.NodeType)
                    {
                        case SplitNodeType.Battle:
                            nodeText = hoveredNode.Difficulty switch
                            {
                                BattleDifficulty.Easy => "EASY COMBAT",
                                BattleDifficulty.Hard => "HARD COMBAT",
                                _ => "COMBAT",
                            };
                            break;
                        case SplitNodeType.Narrative:
                            nodeText = "EVENT";
                            break;
                        case SplitNodeType.Reward:
                            nodeText = "REWARD";
                            break;
                        case SplitNodeType.MajorBattle:
                            nodeText = "MAJOR BATTLE";
                            break;
                    }

                    if (!string.IsNullOrEmpty(nodeText))
                    {
                        var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                        var textSize = secondaryFont.MeasureString(nodeText);

                        // Bobbing animation
                        const float bobSpeed = 4f;
                        float yOffset = (MathF.Sin(_nodeHoverTextBobTimer * bobSpeed) > 0) ? -1f : 0f;

                        var textPosition = new Vector2(
                            (Global.VIRTUAL_WIDTH - textSize.Width) / 2f,
                            Global.VIRTUAL_HEIGHT - textSize.Height - 3 + yOffset
                        );

                        spriteBatch.DrawStringSnapped(secondaryFont, nodeText, textPosition, _global.Palette_Yellow);
                    }
                }
            }

            if (_narrativeDialog.IsActive)
            {
                _narrativeDialog.DrawContent(spriteBatch, font, gameTime, transform);
            }

            if (_eventState == EventState.NarratingResult)
            {
                _resultNarrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);
            }
        }

        private void DrawPath(SpriteBatch spriteBatch, Texture2D pixel, SplitMapPath path, Color fillColor)
        {
            if (_currentMap == null || path.PixelPoints.Count < 2) return;

            var fromNode = _currentMap.Nodes[path.FromNodeId];
            var toNode = _currentMap.Nodes[path.ToNodeId];

            int numPixelsToDraw;

            if (_pathRetractionProgress.ContainsKey(path.Id))
            {
                float duration = _pathAnimationDurations.GetValueOrDefault(path.Id, PATH_ANIMATION_DURATION);
                float animationTimer = _pathRetractionProgress.GetValueOrDefault(path.Id, 0f);
                float linearProgress = Math.Clamp(animationTimer / duration, 0f, 1f);
                if (linearProgress >= 1f) return;
                float easedProgress = Easing.EaseOutCubic(linearProgress);
                numPixelsToDraw = (int)((1f - easedProgress) * path.PixelPoints.Count);
            }
            else if (fromNode.Id == _nodeForPathReveal)
            {
                float duration = _pathAnimationDurations.GetValueOrDefault(path.Id, PATH_ANIMATION_DURATION);
                float animationTimer = _pathAnimationProgress.GetValueOrDefault(path.Id, 0f);
                float linearProgress = Math.Clamp(animationTimer / duration, 0f, 1f);
                if (linearProgress <= 0f) return;
                float easedProgress = Easing.EaseOutCubic(linearProgress);
                numPixelsToDraw = (int)(easedProgress * path.PixelPoints.Count);
            }
            else if (_visitedNodeIds.Contains(fromNode.Id) && _visitedNodeIds.Contains(path.ToNodeId))
            {
                numPixelsToDraw = path.PixelPoints.Count;
            }
            else
            {
                return; // Don't draw hidden paths
            }

            if (numPixelsToDraw <= 0) return;

            // Draw Fill
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

                    // Check against the 'from' node
                    if (distFrom < fadeStartDistance)
                    {
                        if (distFrom <= PATH_EXCLUSION_RADIUS) alpha = 0f;
                        else alpha = Math.Min(alpha, (distFrom - PATH_EXCLUSION_RADIUS) / PATH_FADE_DISTANCE);
                    }

                    // Check against the 'to' node
                    if (distTo < fadeStartDistance)
                    {
                        if (distTo <= PATH_EXCLUSION_RADIUS) alpha = 0f;
                        else alpha = Math.Min(alpha, (distTo - PATH_EXCLUSION_RADIUS) / PATH_FADE_DISTANCE);
                    }

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
            Rectangle? sourceRect = null;
            Vector2 origin;

            if (node.NodeType == SplitNodeType.Battle)
            {
                texture = node.Difficulty switch
                {
                    BattleDifficulty.Easy => _spriteManager.CombatNodeEasySprite,
                    BattleDifficulty.Hard => _spriteManager.CombatNodeHardSprite,
                    _ => _spriteManager.CombatNodeNormalSprite,
                };

                int frameIndex = 0;
                if (node.IsReachable)
                {
                    float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;
                    frameIndex = (int)((totalTime + node.AnimationOffset) / NODE_FRAME_DURATION) % 2;
                }

                sourceRect = new Rectangle(frameIndex * 32, 0, 32, 32);
                origin = new Vector2(16, 16);
            }
            else if (node.NodeType == SplitNodeType.MajorBattle)
            {
                texture = _spriteManager.SplitNodeBoss;
                sourceRect = null;
                origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            }
            else
            {
                texture = node.NodeType switch
                {
                    SplitNodeType.Origin => _spriteManager.SplitNodeStart,
                    SplitNodeType.Narrative => _spriteManager.SplitNodeNarrative,
                    SplitNodeType.Reward => _spriteManager.SplitNodeReward,
                    _ => _spriteManager.SplitNodeBattle, // Fallback
                };
                origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            }

            return (texture, sourceRect, origin);
        }
    }
}
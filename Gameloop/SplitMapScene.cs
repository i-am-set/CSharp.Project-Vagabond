#nullable enable
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
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class SplitMapScene : GameScene
    {
        private class PathChoice
        {
            public float Angle { get; }
            public Vector2 ArrowPosition { get; }
            public Rectangle InteractionBounds { get; }
            public bool IsHovered { get; set; }
            public int? TargetNodeId { get; }
            public int? PathId { get; }

            // Constructor for new, unexplored paths
            public PathChoice(float angle, Vector2 startPosition)
            {
                Angle = angle;
                const float ARROW_DISTANCE = 20f;
                const int ARROW_INTERACTION_SIZE = 10;

                ArrowPosition = startPosition + new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * ARROW_DISTANCE;
                InteractionBounds = new Rectangle(
                    (int)(ArrowPosition.X - ARROW_INTERACTION_SIZE / 2f),
                    (int)(ArrowPosition.Y - ARROW_INTERACTION_SIZE / 2f),
                    ARROW_INTERACTION_SIZE,
                    ARROW_INTERACTION_SIZE
                );
                TargetNodeId = null;
                PathId = null;
            }

            // Constructor for existing paths (forward or backward)
            public PathChoice(Vector2 startPosition, SplitMapNode targetNode, SplitMapPath path)
            {
                var direction = Vector2.Normalize(targetNode.Position - startPosition);
                Angle = MathF.Atan2(direction.Y, direction.X);
                const float ARROW_DISTANCE = 20f;
                const int ARROW_INTERACTION_SIZE = 10;

                ArrowPosition = startPosition + new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * ARROW_DISTANCE;
                InteractionBounds = new Rectangle(
                    (int)(ArrowPosition.X - ARROW_INTERACTION_SIZE / 2f),
                    (int)(ArrowPosition.Y - ARROW_INTERACTION_SIZE / 2f),
                    ARROW_INTERACTION_SIZE,
                    ARROW_INTERACTION_SIZE
                );
                TargetNodeId = targetNode.Id;
                PathId = path.Id;
            }
        }

        private enum SceneState { Choosing, PathDrawing, PlayerMoving, Arrived, NodeTypeReveal }
        private SceneState _sceneState = SceneState.Choosing;

        private readonly ProgressionManager _progressionManager;
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly DiceRollingSystem _diceRollingSystem;
        private readonly StoryNarrator _resultNarrator;
        private readonly ChoiceGenerator _choiceGenerator;
        private readonly ComponentStore _componentStore;

        // Map State
        private readonly Dictionary<int, SplitMapNode> _nodes = new();
        private readonly Dictionary<int, SplitMapPath> _paths = new();
        private readonly Stack<int> _playerHistory = new();
        private readonly HashSet<int> _visitedNodeIds = new HashSet<int>();

        private SplitMapNode? _currentNode;
        private SplitMapNode? _targetNode;
        private SplitMapPath? _activePath;
        private readonly List<PathChoice> _currentChoices = new List<PathChoice>();
        private PathChoice? _hoveredChoice;
        private Vector2 _playerIconPosition;
        private Vector2 _cameraPosition;
        private Vector2 _cameraTargetPosition;

        private NarrativeDialog _narrativeDialog;
        private static readonly Random _random = new Random();

        // Animation State
        private float _pathDrawTimer;
        private const float PATH_DRAW_DURATION = 4.0f;
        private float _playerMoveDelayTimer;
        private const float PLAYER_MOVE_DELAY = 1.0f;
        private float _playerMoveTimer;
        private const string PATH_DRAW_PATTERN = "1111110111010111111001110111110011111111110101100";
        private float _nodeInflationTimer;
        private const float NODE_INFLATE_DURATION = 0.5f;
        private const float NODE_SHAKE_FREQUENCY = 25f;
        private const float NODE_SHAKE_MAGNITUDE = 2f;
        private float _nodeRevealTimer;
        private const float NODE_LIFT_DURATION = 0.4f;
        private const float NODE_SHRINK_DURATION = 0.2f;
        private const float NODE_EXPAND_DURATION = 0.5f;
        private const float NODE_REVEAL_HOLD_DURATION = 0.5f;
        private const float NODE_SETTLE_DURATION = 0.2f;
        private const float NODE_LIFT_AMOUNT = 15f;
        private const float NODE_REVEAL_SHAKE_FREQUENCY = 90f;
        private const float NODE_REVEAL_SHAKE_MAGNITUDE = 2f;


        private bool _wasModalActiveLastFrame = false;

        // Dice Roll State
        private enum EventState { Idle, AwaitingDiceRoll, NarratingResult }
        private EventState _eventState = EventState.Idle;
        private NarrativeChoice? _pendingChoiceForDiceRoll;

        // Node Animation
        private const float NODE_FRAME_DURATION = 0.5f;

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
            _diceRollingSystem.OnRollCompleted += OnDiceRollCompleted;

            if (_currentNode == null) // This is a fresh start
            {
                _progressionManager.StartNewRun();
                _nodes.Clear();
                _paths.Clear();
                _playerHistory.Clear();
                _visitedNodeIds.Clear();
                SplitMapNode.ResetIdCounter();

                _currentNode = new SplitMapNode(0, new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f))
                {
                    NodeType = SplitNodeType.Origin
                };
                _nodes.Add(_currentNode.Id, _currentNode);
                _playerHistory.Push(_currentNode.Id);
                _visitedNodeIds.Add(_currentNode.Id);

                _playerIconPosition = _currentNode.Position;
                _cameraPosition = _currentNode.Position;
                _cameraTargetPosition = _currentNode.Position;
            }

            _sceneState = SceneState.Choosing;
            GenerateChoices();
        }

        public override void Exit()
        {
            base.Exit();
            _diceRollingSystem.OnRollCompleted -= OnDiceRollCompleted;
        }

        private void GenerateChoices()
        {
            if (_currentNode == null) return;

            _currentChoices.Clear();
            var usedAngles = new HashSet<float>();
            float? arrivalAngle = null;

            // 1. Determine the arrival angle from the previous node
            if (_playerHistory.Count > 1)
            {
                var previousNodeId = _playerHistory.ElementAt(1);
                if (_nodes.TryGetValue(previousNodeId, out var previousNode))
                {
                    var arrivalVector = _currentNode.Position - previousNode.Position;
                    if (arrivalVector.LengthSquared() > 0)
                    {
                        arrivalAngle = MathF.Atan2(arrivalVector.Y, arrivalVector.X);
                    }
                    var backtrackPath = _paths.Values.First(p => (p.FromNodeId == _currentNode.Id && p.ToNodeId == previousNodeId) || (p.FromNodeId == previousNodeId && p.ToNodeId == _currentNode.Id));
                    var choice = new PathChoice(_currentNode.Position, previousNode, backtrackPath);
                    _currentChoices.Add(choice);
                    usedAngles.Add(SnapAngle(choice.Angle));
                }
            }

            // 2. Add existing forward choices
            foreach (var pathId in _currentNode.OutgoingPathIds)
            {
                if (_paths.TryGetValue(pathId, out var path) && _nodes.TryGetValue(path.ToNodeId, out var targetNode))
                {
                    var choice = new PathChoice(_currentNode.Position, targetNode, path);
                    _currentChoices.Add(choice);
                    usedAngles.Add(SnapAngle(choice.Angle));
                }
            }

            // 3. Generate new, unique, snapped paths
            int newPathsToGenerate = 3 - _currentChoices.Count;
            if (newPathsToGenerate > 0)
            {
                var availableAngles = new List<float>();
                for (int i = 0; i < 8; i++)
                {
                    availableAngles.Add(i * MathHelper.PiOver4);
                }

                var validAngles = availableAngles.Where(angle => !IsAngleTooClose(angle, usedAngles, arrivalAngle)).ToList();
                var shuffledAngles = validAngles.OrderBy(a => _random.Next()).ToList();

                foreach (var angle in shuffledAngles.Take(newPathsToGenerate))
                {
                    _currentChoices.Add(new PathChoice(angle, _currentNode.Position));
                }
            }
        }

        private float SnapAngle(float angle)
        {
            return MathF.Round(angle / MathHelper.PiOver4) * MathHelper.PiOver4;
        }

        private bool IsAngleTooClose(float newAngle, HashSet<float> usedAngles, float? arrivalAngle)
        {
            const float ANGLE_EPSILON = 0.01f; // Small tolerance for float comparison
            const float ARRIVAL_REPULSION_CONE = MathHelper.Pi / 3f; // 60 degrees

            foreach (var usedAngle in usedAngles)
            {
                if (Math.Abs(MathHelper.WrapAngle(newAngle - usedAngle)) < ANGLE_EPSILON)
                {
                    return true;
                }
            }

            if (arrivalAngle.HasValue)
            {
                if (Math.Abs(MathHelper.WrapAngle(newAngle - arrivalAngle.Value)) < ARRIVAL_REPULSION_CONE)
                {
                    return true;
                }
            }

            return false;
        }


        public override void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Smooth camera follow logic
            _cameraPosition = Vector2.Lerp(_cameraPosition, _cameraTargetPosition, deltaTime * 1.5f);

            if (_narrativeDialog.IsActive || _sceneManager.IsModalActive)
            {
                if (_narrativeDialog.IsActive) _narrativeDialog.Update(gameTime);
                _wasModalActiveLastFrame = true;
                base.Update(gameTime);
                return;
            }

            if (_wasModalActiveLastFrame)
            {
                _wasModalActiveLastFrame = false;
            }

            if (_eventState == EventState.AwaitingDiceRoll || _eventState == EventState.NarratingResult)
            {
                if (_eventState == EventState.NarratingResult) _resultNarrator.Update(gameTime);
                base.Update(gameTime);
                return;
            }

            switch (_sceneState)
            {
                case SceneState.Choosing:
                    HandleChoiceInput();
                    break;
                case SceneState.PathDrawing:
                    _pathDrawTimer += deltaTime;
                    _playerMoveDelayTimer -= deltaTime;
                    if (_targetNode != null && _targetNode.IsVisible) _nodeInflationTimer += deltaTime;
                    if (_playerMoveDelayTimer <= 0)
                    {
                        _sceneState = SceneState.PlayerMoving;
                        _playerMoveTimer = 0f;
                    }
                    break;
                case SceneState.PlayerMoving:
                    _pathDrawTimer += deltaTime;
                    _playerMoveTimer += deltaTime;

                    if (_activePath != null && _activePath.RenderPoints.Count > 1 && _activePath.Length > 0)
                    {
                        float playerProgress = Math.Clamp(_playerMoveTimer / PATH_DRAW_DURATION, 0f, 1f);
                        float distanceToTravel = playerProgress * _activePath.Length;
                        float distanceCovered = 0f;
                        for (int i = 0; i < _activePath.RenderPoints.Count - 1; i++)
                        {
                            Vector2 start = _activePath.RenderPoints[i];
                            Vector2 end = _activePath.RenderPoints[i + 1];
                            float segmentLength = Vector2.Distance(start, end);
                            if (distanceCovered + segmentLength >= distanceToTravel)
                            {
                                float distanceIntoSegment = distanceToTravel - distanceCovered;
                                float segmentProgress = (segmentLength > 0) ? distanceIntoSegment / segmentLength : 0;
                                _playerIconPosition = Vector2.Lerp(start, end, segmentProgress);
                                break;
                            }
                            distanceCovered += segmentLength;
                        }
                        if (playerProgress >= 1.0f) _playerIconPosition = _activePath.RenderPoints.Last();
                    }


                    if (_targetNode != null && _targetNode.IsVisible) _nodeInflationTimer += deltaTime;
                    if (_playerMoveTimer >= PATH_DRAW_DURATION) _sceneState = SceneState.Arrived;
                    break;
                case SceneState.Arrived:
                    _currentNode = _targetNode;
                    _playerIconPosition = _currentNode.Position;
                    _cameraTargetPosition = _currentNode.Position;
                    _targetNode = null;
                    _activePath = null;
                    _sceneState = SceneState.NodeTypeReveal;
                    _nodeRevealTimer = 0f;
                    break;
                case SceneState.NodeTypeReveal:
                    _nodeRevealTimer += deltaTime;
                    if (_nodeRevealTimer >= NODE_LIFT_DURATION + NODE_SHRINK_DURATION + NODE_EXPAND_DURATION + NODE_REVEAL_HOLD_DURATION + NODE_SETTLE_DURATION)
                    {
                        _visitedNodeIds.Add(_currentNode.Id);
                        TriggerNodeEvent(_currentNode);
                    }
                    break;
            }

            base.Update(gameTime);
        }

        private void HandleChoiceInput()
        {
            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);

            var cameraTransform = Matrix.CreateTranslation(-_cameraPosition.X + Global.VIRTUAL_WIDTH / 2f, -_cameraPosition.Y + Global.VIRTUAL_HEIGHT / 2f, 0);
            Matrix.Invert(ref cameraTransform, out var inverseCameraTransform);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, inverseCameraTransform);

            _hoveredChoice = null;
            foreach (var choice in _currentChoices)
            {
                choice.IsHovered = choice.InteractionBounds.Contains(mouseInWorldSpace);
                if (choice.IsHovered) _hoveredChoice = choice;
            }

            if (_hoveredChoice != null && mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed)
            {
                if (UIInputManager.CanProcessMouseClick())
                {
                    SelectChoice(_hoveredChoice);
                    UIInputManager.ConsumeMouseClick();
                }
            }
        }

        private void SelectChoice(PathChoice choice)
        {
            if (_currentNode == null) return;
            _currentChoices.Clear();

            if (choice.TargetNodeId.HasValue) // Existing path
            {
                _targetNode = _nodes[choice.TargetNodeId.Value];
                _activePath = _paths[choice.PathId.Value];
                _activePath.CalculateLength();
                _cameraTargetPosition = _targetNode.Position;

                if (_playerHistory.Count > 1 && _playerHistory.ElementAt(1) == choice.TargetNodeId.Value)
                {
                    _playerHistory.Pop(); // Backtracking
                }
                else
                {
                    _playerHistory.Push(choice.TargetNodeId.Value); // Moving forward
                }

                _sceneState = SceneState.PlayerMoving;
                _playerMoveTimer = 0f;
                _pathDrawTimer = PATH_DRAW_DURATION; // Path is already drawn
            }
            else // New path
            {
                const float NODE_DISTANCE = 80f;
                const float NODE_POS_VARIANCE = 15f;
                float distance = NODE_DISTANCE + ((float)_random.NextDouble() * 2f - 1f) * NODE_POS_VARIANCE;
                Vector2 newNodePos = _currentNode.Position + new Vector2(MathF.Cos(choice.Angle), MathF.Sin(choice.Angle)) * distance;

                _targetNode = new SplitMapNode(_currentNode.Floor + 1, newNodePos) { IsVisible = true };

                const float BATTLE_CHANCE = 0.7f;
                if (_random.NextDouble() < BATTLE_CHANCE)
                {
                    _targetNode.NodeType = SplitNodeType.Battle;
                    _targetNode.Difficulty = (BattleDifficulty)_random.Next(3);
                    _targetNode.EventData = _progressionManager.GetRandomBattle(_targetNode.Difficulty);
                }
                else
                {
                    _targetNode.NodeType = SplitNodeType.Narrative;
                    _targetNode.EventData = _progressionManager.GetRandomNarrative()?.EventID;
                }

                _nodes.Add(_targetNode.Id, _targetNode);
                _nodeInflationTimer = 0f;
                _cameraTargetPosition = _targetNode.Position;

                _activePath = new SplitMapPath(_currentNode.Id, _targetNode.Id);
                _paths.Add(_activePath.Id, _activePath);
                _currentNode.OutgoingPathIds.Add(_activePath.Id);
                _targetNode.IncomingPathIds.Add(_activePath.Id);
                _playerHistory.Push(_targetNode.Id);

                _activePath.RenderPoints = GenerateWigglyPathPoints(_currentNode.Position, _targetNode.Position, new List<int> { _currentNode.Id, _targetNode.Id }, _nodes.Values.ToList());
                _activePath.CalculateLength();

                _activePath.PixelPoints.Clear();
                if (_activePath.RenderPoints.Count >= 2)
                {
                    for (int i = 0; i < _activePath.RenderPoints.Count - 1; i++)
                    {
                        var segmentPoints = SpriteBatchExtensions.GetBresenhamLinePoints(_activePath.RenderPoints[i], _activePath.RenderPoints[i + 1]);
                        if (i == 0) _activePath.PixelPoints.AddRange(segmentPoints);
                        else if (segmentPoints.Count > 1) _activePath.PixelPoints.AddRange(segmentPoints.Skip(1));
                    }
                }

                _sceneState = SceneState.PathDrawing;
                _pathDrawTimer = 0f;
                _playerMoveDelayTimer = PLAYER_MOVE_DELAY;
            }
        }


        private void TriggerNodeEvent(SplitMapNode? node)
        {
            if (node == null) return;
            _sceneState = SceneState.PathDrawing;

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
            _resultNarrator.Clear();
            _sceneState = SceneState.Choosing;
            GenerateChoices();
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var cameraTransform = Matrix.CreateTranslation(-_cameraPosition.X + Global.VIRTUAL_WIDTH / 2f, -_cameraPosition.Y + Global.VIRTUAL_HEIGHT / 2f, 0);
            var finalTransform = cameraTransform * transform;

            spriteBatch.End();
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: finalTransform);

            foreach (var path in _paths.Values)
            {
                bool isVisited = _visitedNodeIds.Contains(path.FromNodeId) && _visitedNodeIds.Contains(path.ToNodeId);
                DrawPath(spriteBatch, ServiceLocator.Get<Texture2D>(), path, isVisited ? _global.Palette_Gray : _global.Palette_White);
            }

            // Draw preview nodes for choices
            if (_sceneState == SceneState.Choosing)
            {
                const float NODE_DISTANCE = 80f; // Base distance for new nodes
                foreach (var choice in _currentChoices)
                {
                    // Determine the opacity based on hover state
                    float alpha = (choice == _hoveredChoice) ? 0.25f : 0.1f;
                    Color drawColor = Color.White * alpha;

                    if (choice.TargetNodeId.HasValue)
                    {
                        if (_nodes.TryGetValue(choice.TargetNodeId.Value, out var targetNode))
                        {
                            if (targetNode == _currentNode) continue; // Don't preview the current node (backtracking)
                            var (texture, sourceRect, origin) = GetNodeDrawData(targetNode, gameTime);
                            spriteBatch.DrawSnapped(texture, targetNode.Position, sourceRect, drawColor, 0f, origin, 1f, SpriteEffects.None, 0.3f);
                        }
                    }
                    else
                    {
                        Vector2 newNodePos = _currentNode.Position + new Vector2(MathF.Cos(choice.Angle), MathF.Sin(choice.Angle)) * NODE_DISTANCE;
                        var (texture, sourceRect, origin) = GetNodeDrawData(null, gameTime, SplitNodeType.Hidden);
                        spriteBatch.DrawSnapped(texture, newNodePos, sourceRect, drawColor, 0f, origin, 1f, SpriteEffects.None, 0.3f);
                    }
                }
            }

            foreach (var node in _nodes.Values)
            {
                if (node.Id == _currentNode?.Id && _sceneState == SceneState.NodeTypeReveal)
                {
                    DrawNodeRevealAnimation(spriteBatch, gameTime);
                }
                else if (node.IsVisible)
                {
                    var (texture, sourceRect, origin) = GetNodeDrawData(node, gameTime);
                    spriteBatch.DrawSnapped(texture, node.Position, sourceRect, Color.White, 0f, origin, 1f, SpriteEffects.None, 0.4f);
                }
            }

            if (_sceneState == SceneState.Choosing)
            {
                var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
                int[] directionToSpriteIndex = { 4, 5, 6, 7, 0, 1, 2, 3 }; // E, SE, S, SW, W, NW, N, NE

                foreach (var choice in _currentChoices)
                {
                    float angle = choice.Angle;
                    if (angle < 0) angle += MathHelper.TwoPi;
                    int direction = (int)Math.Round(angle / (MathHelper.TwoPi / 8f)) % 8;
                    int spriteIndex = directionToSpriteIndex[direction];

                    var arrowSourceRect = _spriteManager.ArrowIconSourceRects[spriteIndex];
                    var arrowOrigin = new Vector2(arrowSourceRect.Width / 2f, arrowSourceRect.Height / 2f);
                    Color arrowColor = choice.IsHovered ? _global.Palette_Yellow : _global.Palette_White;

                    var topLeftPosition = choice.ArrowPosition - arrowOrigin;
                    spriteBatch.DrawSnapped(arrowSheet, topLeftPosition, arrowSourceRect, arrowColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.5f);
                }
            }
            DrawPlayerIcon(spriteBatch, gameTime);

            spriteBatch.End();
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

            if (_narrativeDialog.IsActive) _narrativeDialog.DrawContent(spriteBatch, font, gameTime, transform);
            if (_eventState == EventState.NarratingResult) _resultNarrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);
        }

        private void DrawNodeRevealAnimation(SpriteBatch spriteBatch, GameTime gameTime)
        {
            if (_currentNode == null) return;

            float scale = 1f;
            float rotation = 0f;
            SplitNodeType nodeTypeToDraw;
            float yOffset = 0f;
            float xOffset = 0f;
            Vector2 nodePosition = _currentNode.Position;

            float liftEndTime = NODE_LIFT_DURATION;
            float shrinkEndTime = liftEndTime + NODE_SHRINK_DURATION;
            float expandEndTime = shrinkEndTime + NODE_EXPAND_DURATION;
            float holdEndTime = expandEndTime + NODE_REVEAL_HOLD_DURATION;

            if (_nodeRevealTimer < liftEndTime)
            {
                float progress = _nodeRevealTimer / NODE_LIFT_DURATION;
                yOffset = -Easing.EaseOutQuad(progress) * NODE_LIFT_AMOUNT;
                nodeTypeToDraw = SplitNodeType.Hidden;
            }
            else if (_nodeRevealTimer < shrinkEndTime)
            {
                yOffset = -NODE_LIFT_AMOUNT;
                float progress = (_nodeRevealTimer - liftEndTime) / NODE_SHRINK_DURATION;
                scale = 1f - Easing.EaseInCubic(progress);
                nodeTypeToDraw = SplitNodeType.Hidden;
            }
            else if (_nodeRevealTimer < expandEndTime)
            {
                yOffset = -NODE_LIFT_AMOUNT;
                float progress = (_nodeRevealTimer - shrinkEndTime) / NODE_EXPAND_DURATION;
                scale = Easing.EaseOutBack(progress);
                nodeTypeToDraw = _currentNode.NodeType;
            }
            else if (_nodeRevealTimer < holdEndTime)
            {
                yOffset = -NODE_LIFT_AMOUNT;
                scale = 1.0f;
                nodeTypeToDraw = _currentNode.NodeType;

                float holdProgress = (_nodeRevealTimer - expandEndTime) / NODE_REVEAL_HOLD_DURATION;
                float magnitude = (1f - Easing.EaseOutQuad(holdProgress)) * NODE_REVEAL_SHAKE_MAGNITUDE;
                xOffset = MathF.Sin(holdProgress * NODE_REVEAL_SHAKE_FREQUENCY) * magnitude;
            }
            else
            {
                float progress = (_nodeRevealTimer - holdEndTime) / NODE_SETTLE_DURATION;
                yOffset = -Easing.EaseInQuad(1f - progress) * NODE_LIFT_AMOUNT;
                nodeTypeToDraw = _currentNode.NodeType;
            }

            var (texture, sourceRect, origin) = GetNodeDrawData(_currentNode, gameTime, nodeTypeToDraw);
            spriteBatch.DrawSnapped(texture, nodePosition + new Vector2(xOffset, yOffset), sourceRect, Color.White, rotation, origin, scale, SpriteEffects.None, 0.4f);
        }

        private void DrawPlayerIcon(SpriteBatch spriteBatch, GameTime gameTime)
        {
            var texture = _spriteManager.CombatNodePlayerSprite;
            if (texture == null) return;

            float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;
            int frameIndex = (int)(totalTime / NODE_FRAME_DURATION) % 2;
            var sourceRectangle = new Rectangle(frameIndex * 32, 0, 32, 32);
            var origin = new Vector2(16, 16);

            spriteBatch.DrawSnapped(texture, _playerIconPosition, sourceRectangle, Color.White, 0f, origin, 1f, SpriteEffects.None, 0.5f);
        }

        private void DrawPath(SpriteBatch spriteBatch, Texture2D pixel, SplitMapPath path, Color fillColor)
        {
            if (path.PixelPoints.Count < 2) return;

            int numPixelsToDraw = path.PixelPoints.Count;
            if (path == _activePath)
            {
                float progress = Math.Clamp(_pathDrawTimer / PATH_DRAW_DURATION, 0f, 1f);
                numPixelsToDraw = (int)(progress * path.PixelPoints.Count);
            }
            if (numPixelsToDraw <= 0) return;

            var fromNode = _nodes[path.FromNodeId];
            var toNode = _nodes[path.ToNodeId];

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
                    const float PATH_EXCLUSION_RADIUS = 5f;
                    const float PATH_FADE_DISTANCE = 12f;
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
                        spriteBatch.Draw(pixel, point.ToVector2(), fillColor * alpha);
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

        private (Texture2D texture, Rectangle? sourceRect, Vector2 origin) GetNodeDrawData(SplitMapNode? node, GameTime gameTime, SplitNodeType? typeOverride = null)
        {
            Texture2D texture;
            var nodeType = typeOverride ?? node?.NodeType ?? SplitNodeType.Hidden;

            if (!typeOverride.HasValue && node != null && !_visitedNodeIds.Contains(node.Id))
            {
                nodeType = SplitNodeType.Hidden;
            }

            switch (nodeType)
            {
                case SplitNodeType.Origin:
                    texture = _spriteManager.SplitNodeStart;
                    break;
                case SplitNodeType.Battle:
                    var difficulty = node?.Difficulty ?? BattleDifficulty.Normal;
                    texture = difficulty switch
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
                case SplitNodeType.Hidden:
                    texture = _spriteManager.SplitNodeHidden;
                    break;
                default:
                    texture = _spriteManager.CombatNodeNormalSprite; // Fallback
                    break;
            }

            int frameIndex = 0;
            float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;
            float animationOffset = node?.AnimationOffset ?? 0f;
            frameIndex = (int)((totalTime + animationOffset) / NODE_FRAME_DURATION) % 2;

            var sourceRect = new Rectangle(frameIndex * 32, 0, 32, 32);
            var origin = new Vector2(16, 16);
            return (texture, sourceRect, origin);
        }

        private static List<Vector2> GenerateWigglyPathPoints(Vector2 start, Vector2 end, List<int> ignoreNodeIds, List<SplitMapNode> allNodes)
        {
            const float PATH_SEGMENT_LENGTH = 10f;
            const float PATH_MAX_OFFSET = 5f;
            const float NODE_REPULSION_RADIUS = 30f;
            const float NODE_REPULSION_STRENGTH = 15f;

            var points = new List<Vector2> { start };
            var mainVector = end - start;
            var totalDistance = mainVector.Length();

            if (totalDistance < PATH_SEGMENT_LENGTH)
            {
                points.Add(end);
                return points;
            }

            var direction = Vector2.Normalize(mainVector);
            var perpendicular = new Vector2(-direction.Y, direction.X);
            int numSegments = (int)(totalDistance / PATH_SEGMENT_LENGTH);

            for (int i = 1; i < numSegments; i++)
            {
                float progress = (float)i / numSegments;
                var pointOnLine = start + direction * progress * totalDistance;

                float randomOffset = ((float)_random.NextDouble() * 2f - 1f) * PATH_MAX_OFFSET;
                float taper = MathF.Sin(progress * MathF.PI);

                Vector2 totalRepulsion = Vector2.Zero;
                foreach (var otherNode in allNodes)
                {
                    if (ignoreNodeIds.Contains(otherNode.Id)) continue;

                    float distanceToNode = Vector2.Distance(pointOnLine, otherNode.Position);
                    if (distanceToNode < NODE_REPULSION_RADIUS)
                    {
                        Vector2 repulsionVector = pointOnLine - otherNode.Position;
                        if (repulsionVector.LengthSquared() > 0)
                        {
                            repulsionVector.Normalize();
                            float falloff = 1.0f - (distanceToNode / NODE_REPULSION_RADIUS);
                            float strength = NODE_REPULSION_STRENGTH * Easing.EaseOutQuad(falloff);
                            totalRepulsion += repulsionVector * strength;
                        }
                    }
                }

                var finalPoint = pointOnLine + perpendicular * randomOffset * taper + totalRepulsion;
                points.Add(finalPoint);
            }

            points.Add(end);
            return points;
        }
    }
}
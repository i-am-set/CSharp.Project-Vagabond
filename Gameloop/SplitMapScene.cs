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


        private SplitMap? _currentMap;
        private int _playerCurrentNodeId;
        private readonly PlayerMapIcon _playerIcon;
        private NarrativeDialog _narrativeDialog;

        private float _cameraYOffset;
        private float _targetCameraYOffset;

        private bool _isPlayerMoving;
        private float _playerMoveTimer;
        private const float PLAYER_MOVE_DURATION = 3.0f;
        private int _playerMoveTargetNodeId;
        private SplitMapPath? _playerMovePath;

        // Node interaction state
        private int _hoveredNodeId = -1;
        private int _pressedNodeId = -1;
        private readonly HashSet<int> _visitedNodeIds = new HashSet<int>();
        private int _nodeForPathReveal = -1;
        private int _lastAnimatedNodeId = -1;

        // Path animation state
        private readonly Dictionary<int, float> _pathAnimationProgress = new();
        private readonly Dictionary<int, float> _pathRetractionProgress = new();
        private readonly Dictionary<int, float> _pathAnimationDurations = new();
        private const float PATH_ANIMATION_DURATION = 3.0f; // Seconds for the path to draw
        private static readonly Random _random = new Random();

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
                _lastAnimatedNodeId = -1;
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
                float progress = Math.Clamp(_playerMoveTimer / PLAYER_MOVE_DURATION, 0f, 1f); // Pillar 1: Linear progress

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

            // Target Y position for the player icon on screen (lower third)
            float targetScreenY = viewHeight * 2 / 3f;

            // Calculate the required camera offset to place the player at the target screen Y
            _targetCameraYOffset = targetScreenY - playerPosition.Y;

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

            // Check if we need to start new animations for a new node
            if (_nodeForPathReveal != _lastAnimatedNodeId)
            {
                _lastAnimatedNodeId = _nodeForPathReveal;
                if (_currentMap != null && _currentMap.Nodes.TryGetValue(_nodeForPathReveal, out var currentNode))
                {
                    foreach (var pathId in currentNode.OutgoingPathIds)
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
                    if (node.EventData is NarrativeEvent narrativeEvent)
                    {
                        _narrativeDialog.Show(narrativeEvent, OnNarrativeChoiceSelected);
                        _wasModalActiveLastFrame = true;
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
            choiceMenu?.Show(ChoiceType.Spell, 3);
            _sceneManager.ShowModal(GameSceneState.ChoiceMenu);
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
            var visitedPathColor = _global.Palette_DarkGray;

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
                DrawPath(spriteBatch, pixel, path, visitedPathColor);
            }

            // Draw highlighted path on top
            if (highlightedPath != null)
            {
                DrawPath(spriteBatch, pixel, highlightedPath, _global.Palette_Yellow);
            }

            // Draw Nodes
            foreach (var node in _currentMap.Nodes.Values)
            {
                if (node.NodeType == SplitNodeType.Origin) continue; // Skip drawing the origin node

                var texture = GetNodeTexture(node.NodeType);
                var bounds = node.GetBounds();
                var color = Color.White;
                float scale = 1.0f;

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
                    scale = 1.1f;
                    color = _global.ButtonHoverColor;
                }

                if (node.Id == _pulseNodeId)
                {
                    float pulseProgress = Math.Clamp(_pulseTimer / PULSE_DURATION, 0f, 1f);
                    float pulseWave = MathF.Sin(pulseProgress * MathF.PI); // Creates a 0 -> 1 -> 0 wave
                    scale += pulseWave * PULSE_AMOUNT;
                }

                var origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
                var position = bounds.Center.ToVector2();
                spriteBatch.DrawSnapped(texture, position, null, color, 0f, origin, scale, SpriteEffects.None, 0.4f);
            }


            // Draw Player Icon
            _playerIcon.Draw(spriteBatch);

            spriteBatch.End(); // End the camera-transformed batch
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform); // Re-begin the original batch

            if (_narrativeDialog.IsActive)
            {
                _narrativeDialog.DrawContent(spriteBatch, font, gameTime, transform);
            }

            if (_eventState == EventState.NarratingResult)
            {
                _resultNarrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);
            }
        }

        private void DrawPath(SpriteBatch spriteBatch, Texture2D pixel, SplitMapPath path, Color defaultColor)
        {
            if (_currentMap == null) return;

            var fromNode = _currentMap.Nodes[path.FromNodeId];
            var toNode = _currentMap.Nodes[path.ToNodeId];
            Color pathColor;
            bool isAnimatingAppearance = false;
            bool isAnimatingRetraction = false;

            if (_pathRetractionProgress.ContainsKey(path.Id))
            {
                isAnimatingRetraction = true;
                pathColor = defaultColor;
            }
            else if (fromNode.Id == _nodeForPathReveal)
            {
                isAnimatingAppearance = true;
                pathColor = defaultColor;
            }
            else if (_visitedNodeIds.Contains(fromNode.Id) && _visitedNodeIds.Contains(toNode.Id))
            {
                pathColor = defaultColor; // Visited path
            }
            else
            {
                return; // Don't draw hidden paths
            }

            if (path.PixelPoints.Count < 2) return;

            if (isAnimatingAppearance)
            {
                float duration = _pathAnimationDurations.GetValueOrDefault(path.Id, PATH_ANIMATION_DURATION);
                float animationTimer = _pathAnimationProgress.GetValueOrDefault(path.Id, 0f);
                float linearProgress = Math.Clamp(animationTimer / duration, 0f, 1f);
                if (linearProgress <= 0f) return;

                float easedProgress = Easing.EaseOutCubic(linearProgress);
                int numPixelsToDraw = (int)(easedProgress * path.PixelPoints.Count);

                for (int i = 0; i < numPixelsToDraw; i++)
                {
                    spriteBatch.Draw(pixel, path.PixelPoints[i].ToVector2(), pathColor);
                }
            }
            else if (isAnimatingRetraction)
            {
                float duration = _pathAnimationDurations.GetValueOrDefault(path.Id, PATH_ANIMATION_DURATION);
                float animationTimer = _pathRetractionProgress.GetValueOrDefault(path.Id, 0f);
                float linearProgress = Math.Clamp(animationTimer / duration, 0f, 1f);
                if (linearProgress >= 1f) return; // Fully retracted

                float easedProgress = Easing.EaseOutCubic(linearProgress);
                int numPixelsToDraw = (int)((1f - easedProgress) * path.PixelPoints.Count);

                for (int i = 0; i < numPixelsToDraw; i++)
                {
                    spriteBatch.Draw(pixel, path.PixelPoints[i].ToVector2(), pathColor);
                }
            }
            else // Draw the full path (for visited paths)
            {
                foreach (var point in path.PixelPoints)
                {
                    spriteBatch.Draw(pixel, point.ToVector2(), pathColor);
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

        private Texture2D GetNodeTexture(SplitNodeType type)
        {
            return type switch
            {
                SplitNodeType.Origin => _spriteManager.SplitNodeStart,
                SplitNodeType.Battle => _spriteManager.SplitNodeBattle,
                SplitNodeType.Narrative => _spriteManager.SplitNodeNarrative,
                SplitNodeType.Reward => _spriteManager.SplitNodeReward,
                SplitNodeType.MajorBattle => _spriteManager.SplitNodeBoss,
                _ => _spriteManager.SplitNodeBattle,
            };
        }
    }
}
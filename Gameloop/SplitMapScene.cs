#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
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
        private readonly ProgressionManager _progressionManager;
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;

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

        public static bool PlayerWonLastBattle { get; set; } = true;
        public static bool WasMajorBattle { get; set; } = false;

        public SplitMapScene()
        {
            _progressionManager = ServiceLocator.Get<ProgressionManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _playerIcon = new PlayerMapIcon();
            _narrativeDialog = new NarrativeDialog(this);
        }

        public override Rectangle GetAnimatedBounds() => new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

        public override void Enter()
        {
            base.Enter();
            _isPlayerMoving = false;
            _playerIcon.SetIsMoving(false);

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
            }

            UpdateReachableNodes();
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

            if (_narrativeDialog.IsActive)
            {
                _narrativeDialog.Update(gameTime);
                base.Update(gameTime); // Still call base update to handle its timers and input state for the next frame
                return;
            }

            // Smooth camera scrolling
            _cameraYOffset = MathHelper.Lerp(_cameraYOffset, _targetCameraYOffset, deltaTime * 5f);

            // Update path visibility based on camera position
            if (!_isPlayerMoving && Math.Abs(_cameraYOffset - _targetCameraYOffset) < 0.1f)
            {
                // Once camera is settled, reveal paths from the player's current logical position.
                _nodeForPathReveal = _playerCurrentNodeId;

                // Check if we need to start new animations
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
                        UpdateCameraTarget(endNode.Position, false);
                    }

                    TriggerNodeEvent(_playerCurrentNodeId);
                }
            }
            else
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
                        _narrativeDialog.Show(narrativeEvent, () => UpdateReachableNodes());
                    }
                    break;

                case SplitNodeType.Reward:
                    TriggerReward();
                    break;
            }
        }

        private void TriggerReward()
        {
            var choiceMenu = _sceneManager.GetScene(GameSceneState.ChoiceMenu) as ChoiceMenuScene;
            choiceMenu?.Show(ChoiceType.Spell, 3);
            _sceneManager.ShowModal(GameSceneState.ChoiceMenu);
            // After the modal closes, we need to re-enable the next set of nodes.
            // We can do this in the Update loop by checking if the modal is no longer active.
            // For now, we'll just call it directly after a delay in a real game.
            // For this implementation, we'll re-enable in Enter() when returning.
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_currentMap == null) return;

            var cameraTransform = Matrix.CreateTranslation(0, _cameraYOffset, 0);
            var finalTransform = cameraTransform * transform;

            spriteBatch.End(); // End the default batch
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: finalTransform);

            var pixel = ServiceLocator.Get<Texture2D>();
            var visitedPathColor = new Color(_global.Palette_DarkGray, 100);

            // Draw Paths
            foreach (var path in _currentMap.Paths.Values)
            {
                var fromNode = _currentMap.Nodes[path.FromNodeId];
                var toNode = _currentMap.Nodes[path.ToNodeId];
                Color pathColor;
                bool isAnimatingAppearance = false;
                bool isAnimatingRetraction = false;

                if (_pathRetractionProgress.ContainsKey(path.Id))
                {
                    isAnimatingRetraction = true;
                    pathColor = _global.Palette_DarkGray;
                }
                else if (fromNode.Id == _nodeForPathReveal)
                {
                    isAnimatingAppearance = true;
                    pathColor = _global.Palette_DarkGray; // Active path
                }
                else if (_visitedNodeIds.Contains(fromNode.Id) && _visitedNodeIds.Contains(toNode.Id))
                {
                    pathColor = visitedPathColor; // Traversed path
                }
                else
                {
                    continue; // Don't draw hidden paths
                }

                if (path.PixelPoints.Count < 2) continue;

                if (isAnimatingAppearance)
                {
                    float duration = _pathAnimationDurations.GetValueOrDefault(path.Id, PATH_ANIMATION_DURATION);
                    float animationTimer = _pathAnimationProgress.GetValueOrDefault(path.Id, 0f);
                    float linearProgress = Math.Clamp(animationTimer / duration, 0f, 1f);
                    if (linearProgress <= 0f) continue;

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
                    if (linearProgress >= 1f) continue; // Fully retracted

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

            // Draw Nodes
            foreach (var node in _currentMap.Nodes.Values)
            {
                var texture = GetNodeTexture(node.NodeType);
                var bounds = node.GetBounds();
                var color = Color.White;
                float scale = 1.0f;

                if (!node.IsReachable)
                {
                    color = Color.Gray * 0.5f;
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
                SplitNodeType.Start => _spriteManager.SplitNodeStart,
                SplitNodeType.Battle => _spriteManager.SplitNodeBattle,
                SplitNodeType.Narrative => _spriteManager.SplitNodeNarrative,
                SplitNodeType.Reward => _spriteManager.SplitNodeReward,
                SplitNodeType.MajorBattle => _spriteManager.SplitNodeBoss,
                _ => _spriteManager.SplitNodeBattle,
            };
        }
    }
}
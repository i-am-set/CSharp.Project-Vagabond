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
        private readonly Dictionary<int, Button> _nodeButtons = new();
        private readonly PlayerMapIcon _playerIcon;
        private NarrativeDialog _narrativeDialog;

        private float _cameraYOffset;
        private float _targetCameraYOffset;

        private bool _isPlayerMoving;
        private float _playerMoveTimer;
        private const float PLAYER_MOVE_DURATION = 0.5f;
        private Vector2 _playerMoveStart;
        private Vector2 _playerMoveEnd;
        private int _playerMoveTargetNodeId;

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

            if (_progressionManager.CurrentSplitMap == null)
            {
                _progressionManager.GenerateNewSplitMap();
                _currentMap = _progressionManager.CurrentSplitMap;
                _playerCurrentNodeId = _currentMap?.StartNodeId ?? -1;
                var startNode = _currentMap?.Nodes[_playerCurrentNodeId];
                if (startNode != null)
                {
                    _playerIcon.SetPosition(startNode.Position);
                    UpdateCameraTarget(startNode.Position, true);
                }
                CreateNodeButtons();
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

        private void CreateNodeButtons()
        {
            _nodeButtons.Clear();
            if (_currentMap == null) return;

            foreach (var node in _currentMap.Nodes.Values)
            {
                var button = new ImageButton(node.GetBounds(), GetNodeTexture(node.NodeType))
                {
                    IsEnabled = false
                };
                int nodeId = node.Id; // Capture node Id for the lambda
                button.OnClick += () =>
                {
                    if (!_isPlayerMoving)
                    {
                        StartPlayerMove(nodeId);
                    }
                };
                _nodeButtons[node.Id] = button;
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

            foreach (var (nodeId, button) in _nodeButtons)
            {
                button.IsEnabled = reachableNodeIds.Contains(nodeId);
            }
        }

        private void StartPlayerMove(int targetNodeId)
        {
            if (_currentMap == null) return;

            _isPlayerMoving = true;
            _playerMoveTimer = 0f;
            _playerMoveStart = _playerIcon.Position;
            _playerMoveEnd = _currentMap.Nodes[targetNodeId].Position;
            _playerMoveTargetNodeId = targetNodeId;

            // Disable all buttons during movement
            foreach (var button in _nodeButtons.Values)
            {
                button.IsEnabled = false;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_narrativeDialog.IsActive)
            {
                _narrativeDialog.Update(gameTime);
                return;
            }

            // Smooth camera scrolling
            _cameraYOffset = MathHelper.Lerp(_cameraYOffset, _targetCameraYOffset, deltaTime * 5f);

            if (_isPlayerMoving)
            {
                _playerMoveTimer += deltaTime;
                float progress = Math.Clamp(_playerMoveTimer / PLAYER_MOVE_DURATION, 0f, 1f);
                _playerIcon.SetPosition(Vector2.Lerp(_playerMoveStart, _playerMoveEnd, Easing.EaseInOutQuad(progress)));

                if (progress >= 1f)
                {
                    _isPlayerMoving = false;
                    _playerCurrentNodeId = _playerMoveTargetNodeId;
                    UpdateCameraTarget(_playerMoveEnd, false);
                    TriggerNodeEvent(_playerCurrentNodeId);
                }
            }
            else
            {
                var mouseState = Mouse.GetState();
                foreach (var button in _nodeButtons.Values)
                {
                    button.Update(mouseState);
                }
            }

            _playerIcon.Update(gameTime);
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

            // Draw Paths
            foreach (var path in _currentMap.Paths.Values)
            {
                var fromNode = _currentMap.Nodes[path.FromNodeId];
                var toNode = _currentMap.Nodes[path.ToNodeId];
                spriteBatch.DrawLineSnapped(fromNode.Position, toNode.Position, _global.Palette_DarkGray, 2f);
            }

            // Draw Nodes (as buttons)
            foreach (var button in _nodeButtons.Values)
            {
                button.Draw(spriteBatch, font, gameTime, Matrix.Identity);
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
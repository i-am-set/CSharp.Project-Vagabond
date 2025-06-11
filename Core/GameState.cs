using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ProjectVagabond
{
    // --- ActionType Enum and PendingAction Class ---
    public enum ActionType
    {
        Move,
        ShortRest,
        LongRest
    }

    public class PendingAction
    {
        public ActionType Type { get; }
        public Vector2 Position { get; }
        public RestType? ActionRestType { get; }

        // Constructor for Move action
        public PendingAction(Vector2 position)
        {
            Type = ActionType.Move;
            Position = position;
            ActionRestType = null;
        }

        // Constructor for Rest action
        public PendingAction(RestType restType, Vector2 position)
        {
            Type = restType == RestType.ShortRest ? ActionType.ShortRest : ActionType.LongRest;
            Position = position;
            this.ActionRestType = restType;
        }
    }

    public class GameState
    {
        private Vector2 _playerWorldPos;
        private NoiseMapManager _noiseManager;
        private PlayerStats _playerStats;
        private List<PendingAction> _pendingActions = new List<PendingAction>();
        private float _moveTimer = 0f;
        private bool _isExecutingPath = false;
        private int _currentPathIndex = 0;
        private bool _isFreeMoveMode = false;

        public Vector2 PlayerWorldPos => _playerWorldPos;
        public List<PendingAction> PendingActions => _pendingActions;
        public bool IsExecutingPath => _isExecutingPath;
        public bool IsFreeMoveMode => _isFreeMoveMode;
        public int CurrentPathIndex => _currentPathIndex;
        public NoiseMapManager NoiseManager => _noiseManager;
        public PlayerStats PlayerStats => _playerStats;
        public (int finalEnergy, bool possible) PendingQueueSimulationResult => SimulateActionQueueEnergy();

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        
        public GameState()
        {
            _playerWorldPos = new Vector2(0, 0);

            int masterSeed = RandomNumberGenerator.GetInt32(1, 99999) + Environment.TickCount;
            _noiseManager = new NoiseMapManager(masterSeed);
            _playerStats = new PlayerStats(5, 5, 5, 5, 5);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void ClearPendingActions()
        {
            _pendingActions.Clear();
        }

        public void ToggleExecutingPath(bool toggle)
        {
            ToggleIsFreeMoveMode(false);
            _isExecutingPath = toggle;
        }

        public void ToggleIsFreeMoveMode(bool toggle)
        {
            _isFreeMoveMode = toggle;

            if (toggle)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("[gold]Free move enabled.");
            }
            else
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("[gold]Free move disabled.");
            }
        }

        public void SetCurrentPathIndex(int index)
        {
            _currentPathIndex = index;
        }

        public bool IsPositionPassable(Vector2 position)
        {
            var mapData = GetMapDataAt((int)position.X, (int)position.Y);
            string terrainType = mapData.TerrainType;

            return terrainType != "WATER" && terrainType != "PEAKS";
        }

        public int GetMovementEnergyCost(Vector2 position)
        {
            var mapData = GetMapDataAt((int)position.X, (int)position.Y);
            string terrainType = mapData.TerrainType;

            return terrainType switch
            {
                "FLATLANDS" => 1,
                "HILLS" => 2,
                "MOUNTAINS" => 3,
                "WATER" => 1,
                "PEAKS" => 1,
                _ => 1
            };
        }

        public (int finalEnergy, bool possible) SimulateActionQueueEnergy(List<PendingAction> customQueue = null)
        {
            var queueToSimulate = customQueue ?? _pendingActions;
            int currentEnergy = _playerStats.CurrentEnergyPoints;
            int maxEnergy = _playerStats.MaxEnergyPoints;

            foreach (var action in queueToSimulate)
            {
                switch (action.Type)
                {
                    case ActionType.Move:
                        int cost = GetMovementEnergyCost(action.Position);
                        if (currentEnergy < cost)
                        {
                            return (currentEnergy, false);
                        }
                        currentEnergy -= cost;
                        break;
                    case ActionType.ShortRest:
                        currentEnergy += (int)Math.Floor((double)maxEnergy * 0.8f);
                        currentEnergy = Math.Min(currentEnergy, maxEnergy);
                        break;
                    case ActionType.LongRest:
                        currentEnergy = maxEnergy;
                        break;
                }
            }
            return (currentEnergy, true);
        }

        public void QueueRest(string[] args)
        {
            if (_isExecutingPath)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("Cannot queue actions while executing a path.");
                return;
            }

            RestType restType = RestType.ShortRest;
            if (args.Length > 1 && args[1].ToLower() == "long")
            {
                restType = RestType.LongRest;
            }

            Vector2 restPosition = _pendingActions.Any() ? _pendingActions.Last().Position : _playerWorldPos;

            _pendingActions.Add(new PendingAction(restType, restPosition));

            int finalEnergy = SimulateActionQueueEnergy().finalEnergy;
            Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued a {restType.ToString().ToLower()} rest. Predicted final energy: {finalEnergy}/{_playerStats.MaxEnergyPoints}");
        }

        public void QueueMovement(Vector2 direction, string[] args)
        {
            if (_isExecutingPath)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("Cannot queue movements while executing a path.");
                return;
            }

            int count = 1;
            if (args.Length > 1 && int.TryParse(args[1], out int parsedCount))
            {
                count = Math.Max(1, Math.Min(Global.MAX_SINGLE_MOVE_LIMIT, parsedCount));
            }

            Vector2 oppositeDirection = -direction;
            int removedSteps = 0;

            while (_pendingActions.Count > 0 && removedSteps < count)
            {
                PendingAction lastAction = _pendingActions.Last();
                if (lastAction.Type != ActionType.Move) break;

                Vector2 lastStep = lastAction.Position;
                Vector2 prevPos = _pendingActions.Count > 1 ? _pendingActions[_pendingActions.Count - 2].Position : _playerWorldPos;
                Vector2 lastDirection = lastStep - prevPos;

                if (lastDirection == oppositeDirection)
                {
                    _pendingActions.RemoveAt(_pendingActions.Count - 1);
                    removedSteps++;
                }
                else
                {
                    break;
                }
            }

            int remainingSteps = count - removedSteps;
            if (remainingSteps > 0)
            {
                Vector2 currentPos = _pendingActions.Any() ? _pendingActions.Last().Position : _playerWorldPos;
                int validSteps = 0;

                for (int i = 0; i < remainingSteps; i++)
                {
                    Vector2 nextPos = currentPos + direction;

                    if (!IsPositionPassable(nextPos))
                    {
                        var mapData = GetMapDataAt((int)nextPos.X, (int)nextPos.Y);
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[crimson]Cannot move here... terrain is impassable! <{mapData.TerrainType.ToLower()}>");
                        break;
                    }

                    var tempQueue = new List<PendingAction>(_pendingActions) { new PendingAction(nextPos) };
                    var simulationResult = SimulateActionQueueEnergy(tempQueue);

                    if (!simulationResult.possible)
                    {
                        if (_isFreeMoveMode)
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory("[gold]Not enough energy. Auto-queuing a short rest.");
                            Vector2 restPosition = _pendingActions.Any() ? _pendingActions.Last().Position : _playerWorldPos;
                            
                            var tempQueueWithRest = new List<PendingAction>(_pendingActions);
                            tempQueueWithRest.Add(new PendingAction(RestType.ShortRest, restPosition));
                            tempQueueWithRest.Add(new PendingAction(nextPos));

                            if (SimulateActionQueueEnergy(tempQueueWithRest).possible)
                            {
                                _pendingActions.Add(new PendingAction(RestType.ShortRest, restPosition));
                            }
                            else
                            {
                                Core.CurrentTerminalRenderer.AddOutputToHistory($"[crimson]Cannot move here... Not enough energy even after a rest!");
                                break;
                            }
                        }
                        else
                        {
                            int stepCost = GetMovementEnergyCost(nextPos);
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[crimson]Cannot move here... Not enough energy! <Requires {stepCost} EP>");
                            break;
                        }
                    }

                    currentPos = nextPos;
                    _pendingActions.Add(new PendingAction(currentPos));
                    validSteps++;
                }

                if (validSteps > 0)
                {
                    int finalEnergy = SimulateActionQueueEnergy().finalEnergy;
                    if (removedSteps > 0)
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[teal]Backtracked {removedSteps} time(s), added {validSteps} move(s) (Final EP: {finalEnergy})");
                    }
                    else
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued {validSteps} move(s) {args[0].ToLower()} (Final EP: {finalEnergy})");
                    }
                }
                else if (removedSteps > 0)
                {
                    int finalEnergy = SimulateActionQueueEnergy().finalEnergy;
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[teal]Backtracked {removedSteps} time(s) (Final EP: {finalEnergy})");
                }
            }
            else if (removedSteps > 0)
            {
                int finalEnergy = SimulateActionQueueEnergy().finalEnergy;
                Core.CurrentTerminalRenderer.AddOutputToHistory($"[teal]Backtracked {removedSteps} time(s) (Final EP: {finalEnergy})");
            }
        }

        public void UpdateMovement(GameTime gameTime)
        {
            if (_isExecutingPath && _pendingActions.Count > 0)
            {
                _moveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_moveTimer >= Global.MOVE_DELAY_SECONDS)
                {
                    if (_currentPathIndex < _pendingActions.Count)
                    {
                        PendingAction nextAction = _pendingActions[_currentPathIndex];

                        switch (nextAction.Type)
                        {
                            case ActionType.Move:
                                Vector2 nextPosition = nextAction.Position;
                                if (!IsPositionPassable(nextPosition))
                                {
                                    var mapData = GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[crimson]Path blocked at ({(int)nextPosition.X}, {(int)nextPosition.Y}) - {mapData.TerrainType.ToLower()} is impassable!");
                                    CancelPathExecution();
                                    return;
                                }

                                int energyCost = GetMovementEnergyCost(nextPosition);
                                if (!_playerStats.CanExertEnergy(energyCost))
                                {
                                    var mapData = GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[crimson]Not enough energy to continue! Need {energyCost} for {mapData.TerrainType.ToLower()}, have {_playerStats.CurrentEnergyPoints}");
                                    CancelPathExecution();
                                    return;
                                }

                                _playerWorldPos = nextPosition;
                                _playerStats.ExertEnergy(energyCost);
                                var currentMapData = GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                                Core.CurrentTerminalRenderer.AddOutputToHistory($"[darkgray]Moved to ({(int)nextPosition.X}, {(int)nextPosition.Y}) - {currentMapData.TerrainType.ToLower()} (-{energyCost} energy, {_playerStats.CurrentEnergyPoints} remaining)");
                                break;

                            case ActionType.ShortRest:
                            case ActionType.LongRest:
                                _playerStats.Rest(nextAction.ActionRestType.Value);
                                Core.CurrentTerminalRenderer.AddOutputToHistory($"[green]Completed {nextAction.ActionRestType.Value.ToString().ToLower()} rest. Energy is now {_playerStats.CurrentEnergyPoints}.");
                                break;
                        }

                        _currentPathIndex++;
                        _moveTimer = 0f;

                        if (_currentPathIndex >= _pendingActions.Count)
                        {
                            _isExecutingPath = false;
                            _pendingActions.Clear();
                            _currentPathIndex = 0;
                            ToggleExecutingPath(false);
                            Core.CurrentTerminalRenderer.AddOutputToHistory("Action queue completed.");
                        }
                    }
                }
            }
        }

        public void CancelPathExecution()
        {
            if (_isExecutingPath)
            {
                _isExecutingPath = false;
                _pendingActions.Clear();
                _currentPathIndex = 0;
                ToggleExecutingPath(false);
                ToggleIsFreeMoveMode(false);
                Core.CurrentTerminalRenderer.AddOutputToHistory("[crimson]Action queue cancelled.");
            }
        }

        public MapData GetMapDataAt(int x, int y)
        {
            return _noiseManager.GetMapData(x, y);
        }

        public float GetNoiseAt(int x, int y)
        {
            return _noiseManager.GetNoiseValue(NoiseMapType.TerrainHeight, x, y);
        }

        public string GetTerrainDescription(float noise)
        {
            if (noise < Global.Instance.WaterLevel) return "deep water";
            if (noise < Global.Instance.FlatlandsLevel) return "flatlands";
            if (noise < Global.Instance.HillsLevel) return "hills";
            if (noise < Global.Instance.MountainsLevel) return "mountains";
            return "peaks";
        }

        public string GetTerrainDescription(int x, int y)
        {
            var mapData = GetMapDataAt(x, y);
            return mapData.TerrainType.ToLower();
        }

        public Texture2D GetTerrainTexture(float noise)
        {
            if (noise < Global.Instance.WaterLevel) return Core.CurrentSpriteManager.WaterSprite;
            if (noise < Global.Instance.FlatlandsLevel) return Core.CurrentSpriteManager.FlatlandSprite;
            if (noise < Global.Instance.HillsLevel) return Core.CurrentSpriteManager.HillSprite;
            if (noise < Global.Instance.MountainsLevel) return Core.CurrentSpriteManager.MountainSprite;
            return Core.CurrentSpriteManager.PeakSprite;
        }

        public Texture2D GetTerrainTexture(int x, int y)
        {
            float noise = GetNoiseAt(x, y);
            return GetTerrainTexture(noise);
        }
    }
}
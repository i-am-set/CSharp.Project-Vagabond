using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ProjectVagabond
{
    public enum ActionType
    {
        WalkMove,
        RunMove,
        ShortRest,
        LongRest
    }

    public class PendingAction
    {
        public ActionType Type { get; }
        public Vector2 Position { get; }
        public RestType? ActionRestType { get; }

        public PendingAction(Vector2 position, bool isRunning = true)
        {
            Type = isRunning ? ActionType.RunMove : ActionType.WalkMove;
            Position = position;
            ActionRestType = null;
        }

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
        public (int finalEnergy, bool possible, int minutesPassed) PendingQueueSimulationResult => SimulateActionQueueEnergy();

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        
        public GameState()
        {
            _playerWorldPos = new Vector2(0, 0);

            int masterSeed = RandomNumberGenerator.GetInt32(1, 99999) + Environment.TickCount;
            _noiseManager = new NoiseMapManager(masterSeed);
            _playerStats = new PlayerStats(5, 10, 5, 5, 5);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void CancelPendingActions()
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

        public int GetMovementEnergyCost(PendingAction action)
        {
            if (action.Type == ActionType.WalkMove)
            {
                return 0;
            }

            if (action.Type == ActionType.RunMove)
            {
                var mapData = GetMapDataAt((int)action.Position.X, (int)action.Position.Y);
                string terrainType = mapData.TerrainType;

                return terrainType switch
                {
                    "FLATLANDS" => 1,
                    "HILLS" => 2,
                    "MOUNTAINS" => 3,
                    _ => 1
                };
            }

            return 0;
        }

        public int GetMinutesPassedDuringMovement(ActionType actionType, string terrainType)
        {
            int timePassed= 0;

            timePassed += actionType switch
            {
                ActionType.WalkMove => (int)Math.Ceiling(6/_playerStats.WalkSpeed),
                ActionType.RunMove => (int)Math.Ceiling(6/_playerStats.RunSpeed),
                _ => timePassed
            };

            timePassed += terrainType.ToUpper() switch
            {
                "FLATLANDS" => 0,
                "HILLS" => (int)Math.Ceiling(timePassed*0.5f),
                "MOUNTAINS" => timePassed+5,
                _ => 0
            };

            

            return timePassed;
        }

        public (int finalEnergy, bool possible, int minutesPassed) SimulateActionQueueEnergy(List<PendingAction> customQueue = null)
        {
            var queueToSimulate = customQueue ?? _pendingActions;
            int currentEnergy = _playerStats.CurrentEnergyPoints;
            int maxEnergy = _playerStats.MaxEnergyPoints;
            int minutesPassed = 0;

            foreach (var action in queueToSimulate)
            {
                switch (action.Type)
                {
                    case ActionType.WalkMove:
                    case ActionType.RunMove:
                        minutesPassed += GetMinutesPassedDuringMovement(action.Type, GetTerrainDescription((int)action.Position.X, (int)action.Position.Y));
                        int cost = GetMovementEnergyCost(action);
                        if (currentEnergy < cost)
                        {
                            return (currentEnergy, false, minutesPassed);
                        }
                        currentEnergy -= cost;
                        break;
                    case ActionType.ShortRest:
                        currentEnergy += (int)Math.Floor((double)maxEnergy * 0.8f);
                        currentEnergy = Math.Min(currentEnergy, maxEnergy);

                        minutesPassed += _playerStats.ShortRestDuration;
                        break;
                    case ActionType.LongRest:
                        currentEnergy = maxEnergy;

                        minutesPassed += _playerStats.LongRestDuration;
                        break;
                }
            }
            return (currentEnergy, true, minutesPassed);
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
            Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued a {restType.ToString().ToLower()} rest.");
        }
        
        private void QueueMovementInternal(Vector2 direction, string[] args, bool isRunning)
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
                // Find the index of the last movement action (Move or RunMove).
                int lastMoveIndex = -1;
                for (int i = _pendingActions.Count - 1; i >= 0; i--)
                {
                    var action = _pendingActions[i];
                    if (action.Type == ActionType.WalkMove || action.Type == ActionType.RunMove)
                    {
                        lastMoveIndex = i;
                        break;
                    }
                }

                // If there are no moves to backtrack, exit the loop.
                if (lastMoveIndex == -1)
                {
                    break;
                }

                PendingAction lastMoveAction = _pendingActions[lastMoveIndex];
        
                // Determine the position before this move to calculate its direction.
                Vector2 prevPos = (lastMoveIndex > 0) ? _pendingActions[lastMoveIndex - 1].Position : _playerWorldPos;

                Vector2 lastDirection = lastMoveAction.Position - prevPos;

                // Check if the last move was in the opposite direction of the new move.
                if (lastDirection == oppositeDirection)
                {
                    // Backtrack: Remove the move and any subsequent rests.
                    int actionsToRemoveCount = _pendingActions.Count - lastMoveIndex;
                    _pendingActions.RemoveRange(lastMoveIndex, actionsToRemoveCount);
                    removedSteps++;
                }
                else
                {
                    // The last move was not a backtrack, so stop.
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
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... terrain is impassable! <{mapData.TerrainType.ToLower()}>");
                        break;
                    }
            
                    var nextAction = new PendingAction(nextPos, isRunning);
                    var tempQueue = new List<PendingAction>(_pendingActions) { nextAction };
                    var simulationResult = SimulateActionQueueEnergy(tempQueue);

                    if (!simulationResult.possible)
                    {
                        if (_isFreeMoveMode)
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory("[gold]Not enough energy. Auto-queuing a short rest.");
                            Vector2 restPosition = _pendingActions.Any() ? _pendingActions.Last().Position : _playerWorldPos;
                    
                            var tempQueueWithRest = new List<PendingAction>(_pendingActions);
                            tempQueueWithRest.Add(new PendingAction(RestType.ShortRest, restPosition));
                            tempQueueWithRest.Add(nextAction);

                            if (SimulateActionQueueEnergy(tempQueueWithRest).possible)
                            {
                                _pendingActions.Add(new PendingAction(RestType.ShortRest, restPosition));
                            }
                            else
                            {
                                Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... Not enough energy even after a rest!");
                                break;
                            }
                        }
                        else
                        {
                            int stepCost = GetMovementEnergyCost(nextAction);
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... Not enough energy! <Requires {stepCost} EP>");
                            break;
                        }
                    }

                    currentPos = nextPos;
                    _pendingActions.Add(nextAction);
                    validSteps++;
                }

                if (validSteps > 0)
                {
                    int finalEnergy = SimulateActionQueueEnergy().finalEnergy;
                    string moveType = isRunning ? "run" : "walk";
                    if (removedSteps > 0)
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[undo]Backtracked {removedSteps} time(s), added {validSteps} {moveType}(s)");
                    }
                    else
                    {
                        if (moveType == "walk")
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued move {validSteps} {args[0].ToLower()}");
                        }
                        else
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued run {validSteps} {args[0].ToLower()}");
                        }
                    }
                }
                else if (removedSteps > 0)
                {
                    int finalEnergy = SimulateActionQueueEnergy().finalEnergy;
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[undo]Backtracked {removedSteps} time(s)");
                }
            }
            else if (removedSteps > 0)
            {
                int finalEnergy = SimulateActionQueueEnergy().finalEnergy;
                Core.CurrentTerminalRenderer.AddOutputToHistory($"[undo]Backtracked {removedSteps} time(s)");
            }
        }

        // --- NEW: Public method for running (energy cost) ---
        public void QueueRunMovement(Vector2 direction, string[] args)
        {
            QueueMovementInternal(direction, args, true);
        }

        // --- NEW: Public method for walking (zero energy cost) ---
        public void QueueWalkMovement(Vector2 direction, string[] args)
        {
            QueueMovementInternal(direction, args, false);
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
                            case ActionType.WalkMove:
                            case ActionType.RunMove:
                                Vector2 nextPosition = nextAction.Position;
                                var mapData = GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                                if (!IsPositionPassable(nextPosition))
                                {
                                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... terrain is impassable! <{mapData.TerrainType.ToLower()}>  {(int)nextPosition.X}, {(int)nextPosition.Y}");
                                    CancelPathExecution();
                                    return;
                                }

                                int energyCost = GetMovementEnergyCost(nextAction);
                                if (!_playerStats.CanExertEnergy(energyCost))
                                {
                                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Not enough energy to continue! Need {energyCost} for {mapData.TerrainType.ToLower()}, have {_playerStats.CurrentEnergyPoints}");
                                    CancelPathExecution();
                                    return;
                                }

                                int minutesPassed = GetMinutesPassedDuringMovement(nextAction.Type, mapData.TerrainType);
                                _playerWorldPos = nextPosition;
                                _playerStats.ExertEnergy(energyCost);
                                var currentMapData = GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                                string moveType = nextAction.Type == ActionType.RunMove ? "Ran" : "Walked";
                                Core.CurrentTerminalRenderer.AddOutputToHistory($"[khaki]{moveType} through [gold]{currentMapData.TerrainType.ToLower()}[khaki] in {minutesPassed} minutes.[/o] <{(int)nextPosition.X}, {(int)nextPosition.Y}>");
                                Core.CurrentWorldClockManager.PassTime(minutes: minutesPassed);
                                break;

                            case ActionType.ShortRest:
                            case ActionType.LongRest:
                                _playerStats.Rest(nextAction.ActionRestType.Value);
                                string restType = nextAction.ActionRestType.Value == RestType.ShortRest ? "short" : "long";
                                Core.CurrentTerminalRenderer.AddOutputToHistory($"[green]Completed {restType.ToLower()} rest. Energy is now {_playerStats.CurrentEnergyPoints}.");

                                if (nextAction.ActionRestType.Value == RestType.ShortRest)
                                {
                                    Core.CurrentWorldClockManager.PassTime(minutes: _playerStats.ShortRestDuration);
                                }
                                else
                                {
                                    Core.CurrentWorldClockManager.PassTime(minutes: _playerStats.LongRestDuration);
                                }
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
                Core.CurrentTerminalRenderer.AddOutputToHistory("[error]Action queue cancelled.");
            }
        }

        public MapData GetMapDataAt(int x, int y) => _noiseManager.GetMapData(x, y);
        public float GetNoiseAt(int x, int y) => _noiseManager.GetNoiseValue(NoiseMapType.TerrainHeight, x, y);
        public string GetTerrainDescription(float noise)
        {
            if (noise < Global.Instance.WaterLevel) return "deep water";
            if (noise < Global.Instance.FlatlandsLevel) return "flatlands";
            if (noise < Global.Instance.HillsLevel) return "hills";
            if (noise < Global.Instance.MountainsLevel) return "mountains";
            return "peaks";
        }
        public string GetTerrainDescription(int x, int y) => GetMapDataAt(x, y).TerrainType.ToLower();
        public Texture2D GetTerrainTexture(float noise)
        {
            if (noise < Global.Instance.WaterLevel) return Core.CurrentSpriteManager.WaterSprite;
            if (noise < Global.Instance.FlatlandsLevel) return Core.CurrentSpriteManager.FlatlandSprite;
            if (noise < Global.Instance.HillsLevel) return Core.CurrentSpriteManager.HillSprite;
            if (noise < Global.Instance.MountainsLevel) return Core.CurrentSpriteManager.MountainSprite;
            return Core.CurrentSpriteManager.PeakSprite;
        }
        public Texture2D GetTerrainTexture(int x, int y) => GetTerrainTexture(GetNoiseAt(x, y));
    }
}
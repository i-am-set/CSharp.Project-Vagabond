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
        LongRest,
        FullRest
    }

    public enum MapView { World, Local }

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
            if (restType == RestType.ShortRest) Type = ActionType.ShortRest;
            else if (restType == RestType.LongRest) Type = ActionType.LongRest;
            else if (restType == RestType.FullRest) Type = ActionType.FullRest;

            Position = position;
            this.ActionRestType = restType;
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

    public class GameState
    {
        private Player _player;
        private NoiseMapManager _noiseManager;
        private bool _isExecutingPath = false;
        private int _currentPathIndex = 0;
        private bool _isFreeMoveMode = false;
        private bool _isPaused = false;

        private PendingAction _actionAwaitingExecution = null;
        private MapView _pathExecutionMapView;

        private List<Entity> _worldEntities = new List<Entity>();
        private List<Entity> _tetheredEntities = new List<Entity>();
        private const int TETHER_RANGE = 32;

        public Vector2 PlayerWorldPos => _player.WorldPosition;
        public Vector2 PlayerLocalPos => _player.LocalPosition;
        public List<PendingAction> PendingActions => _player.ActionQueue;
        public bool IsExecutingPath => _isExecutingPath;
        public bool IsPaused => _isPaused;
        public bool IsFreeMoveMode => _isFreeMoveMode;
        public int CurrentPathIndex => _currentPathIndex;
        public NoiseMapManager NoiseManager => _noiseManager;
        public PlayerStats PlayerStats => _player.Stats;
        public MapView CurrentMapView { get; private set; } = MapView.World;
        public (int finalEnergy, bool possible, int secondsPassed) PendingQueueSimulationResult => SimulateActionQueueEnergy();

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public GameState()
        {
            int masterSeed = RandomNumberGenerator.GetInt32(1, 99999) + Environment.TickCount;

            _player = new Player(new Vector2(0, 0));
            _worldEntities.Add(_player);

            _noiseManager = new NoiseMapManager(masterSeed);

            UpdateTetheredEntities();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void ToggleMapView()
        {
            CancelPathExecution();
            CurrentMapView = (CurrentMapView == MapView.World) ? MapView.Local : MapView.World;
            Core.CurrentTerminalRenderer.AddOutputToHistory($"[undo]Switched to {CurrentMapView} map view.");
        }

        public void TogglePause()
        {
            if (_isExecutingPath)
            {
                _isPaused = !_isPaused;
            }
        }

        public void QueueNewPath(List<Vector2> path, bool isRunning)
        {
            CancelPendingActions();
            AppendPath(path, isRunning);
        }

        public void AppendPath(List<Vector2> path, bool isRunning)
        {
            if (path == null) return;

            bool isLocalPath = CurrentMapView == MapView.Local;

            if (!isRunning || isLocalPath) // For walking or any local path, just add. No energy cost.
            {
                foreach (var pos in path)
                {
                    _player.ActionQueue.Add(new PendingAction(pos, isRunning: isRunning));
                }
                return;
            }

            // For running on the world map, check energy for each step.
            foreach (var nextPos in path)
            {
                var nextAction = new PendingAction(nextPos, isRunning: true);
                var tempQueue = new List<PendingAction>(_player.ActionQueue) { nextAction };
                var simulationResult = SimulateActionQueueEnergy(tempQueue);

                if (!simulationResult.possible)
                {
                    Vector2 restPosition = _player.ActionQueue.Any() ? _player.ActionQueue.Last().Position : _player.WorldPosition;
                    var restAction = new PendingAction(RestType.ShortRest, restPosition);
                    var tempQueueWithRest = new List<PendingAction>(_player.ActionQueue) { restAction, nextAction };

                    if (SimulateActionQueueEnergy(tempQueueWithRest).possible)
                    {
                        _player.ActionQueue.Add(restAction);
                        _player.ActionQueue.Add(nextAction);
                    }
                    else
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot queue path. Not enough energy even after a short rest.");
                        return; // Stop adding the rest of the path
                    }
                }
                else
                {
                    _player.ActionQueue.Add(nextAction);// Enough energy, just add the action
                }
            }
        }

        public void CancelPendingActions()
        {
            _player.ActionQueue.Clear();
            Core.CurrentTerminalRenderer.AddOutputToHistory("Pending actions cleared.");
        }

        public void ToggleExecutingPath(bool toggle)
        {
            ToggleIsFreeMoveMode(false);
            if (toggle)
            {
                _pathExecutionMapView = CurrentMapView;
            }
            _isExecutingPath = toggle;
        }

        public void ToggleIsFreeMoveMode(bool toggle)
        {
            bool cachedFreeMoveMode = _isFreeMoveMode;
            _isFreeMoveMode = toggle;

            if (cachedFreeMoveMode != _isFreeMoveMode)
            {
                if (toggle)
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory("[warning]Free move enabled.");
                }
                else
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory("[warning]Free move disabled.");
                }
            }
        }

        public void SetCurrentPathIndex(int index)
        {
            _currentPathIndex = index;
        }

        public bool IsPositionPassable(Vector2 position, MapView view)
        {
            if (view == MapView.Local)
            {
                return position.X >= 0 && position.X < Global.LOCAL_GRID_SIZE && position.Y >= 0 && position.Y < Global.LOCAL_GRID_SIZE;
            }

            var mapData = GetMapDataAt((int)position.X, (int)position.Y);
            string terrainType = mapData.TerrainType;

            string upperTerrainType = terrainType.ToUpper();
            return upperTerrainType != "WATER" && upperTerrainType != "PEAKS";
        }

        public int GetMovementEnergyCost(PendingAction action, bool isLocalMove = false)
        {
            if (isLocalMove || action.Type == ActionType.WalkMove)
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

        public int GetSecondsPassedDuringMovement(ActionType actionType, string terrainType, Vector2 moveDirection, bool isLocalMove = false)
        {
            float secondsPassed = 0;
            float timeMultiplier = 1.0f;

            if (moveDirection.X != 0 && moveDirection.Y != 0)
            {
                timeMultiplier = 1.5f;
            }

            float baseTime = actionType switch
            {
                ActionType.WalkMove => 360f / _player.Stats.WalkSpeed,
                ActionType.RunMove => 360f / _player.Stats.RunSpeed,
                _ => 0
            };

            if (isLocalMove)
            {
                secondsPassed += baseTime / Global.LOCAL_GRID_SIZE;
            }
            else
            {
                secondsPassed += baseTime;
                secondsPassed += terrainType.ToUpper() switch
                {
                    "FLATLANDS" => 0,
                    "HILLS" => secondsPassed * 0.5f,
                    "MOUNTAINS" => secondsPassed + 300,
                    _ => 0
                };
            }

            return (int)Math.Ceiling(secondsPassed * timeMultiplier);
        }

        public (int finalEnergy, bool possible, int secondsPassed) SimulateActionQueueEnergy(List<PendingAction> customQueue = null)
        {
            var queueToSimulate = customQueue ?? _player.ActionQueue;
            if (!queueToSimulate.Any()) return (_player.Stats.CurrentEnergyPoints, true, 0);

            bool isLocalSim = CurrentMapView == MapView.Local;
            int finalEnergy = _player.Stats.CurrentEnergyPoints;
            int maxEnergy = _player.Stats.MaxEnergyPoints;
            int secondsPassed = 0;
            Vector2 lastPosition = isLocalSim ? _player.LocalPosition : _player.WorldPosition;
            bool isFirstMoveInQueue = true;
            bool localRunCostApplied = false;

            foreach (var action in queueToSimulate)
            {
                switch (action.Type)
                {
                    case ActionType.WalkMove:
                    case ActionType.RunMove:
                        Vector2 moveDirection = action.Position - lastPosition;
                        string terrain = isLocalSim ? "LOCAL" : GetTerrainDescription((int)action.Position.X, (int)action.Position.Y);

                        int moveDuration = GetSecondsPassedDuringMovement(action.Type, terrain, moveDirection, isLocalSim);

                        if (isFirstMoveInQueue && !isLocalSim)
                        {
                            float timeScaleFactor = GetFirstMoveTimeScaleFactor(moveDirection);
                            moveDuration = (int)Math.Ceiling(moveDuration * timeScaleFactor);
                        }

                        secondsPassed += moveDuration;
                        int cost = GetMovementEnergyCost(action, isLocalSim);

                        if (isLocalSim && action.Type == ActionType.RunMove && !localRunCostApplied)
                        {
                            cost = 1;
                            localRunCostApplied = true;
                        }

                        if (finalEnergy < cost)
                        {
                            return (finalEnergy, false, secondsPassed);
                        }
                        finalEnergy -= cost;
                        lastPosition = action.Position;
                        break;
                    case ActionType.ShortRest:
                        finalEnergy += _player.Stats.ShortRestEnergyRestored;
                        finalEnergy = Math.Min(finalEnergy, maxEnergy);
                        secondsPassed += _player.Stats.ShortRestDuration * 60;
                        lastPosition = action.Position;
                        break;
                    case ActionType.LongRest:
                        finalEnergy += _player.Stats.LongRestEnergyRestored;
                        finalEnergy = Math.Min(finalEnergy, maxEnergy);
                        secondsPassed += _player.Stats.LongRestDuration * 60;
                        lastPosition = action.Position;
                        break;
                    case ActionType.FullRest:
                        finalEnergy += _player.Stats.FullRestEnergyRestored;
                        finalEnergy = Math.Min(finalEnergy, maxEnergy);
                        secondsPassed += _player.Stats.FullRestDuration * 60;
                        lastPosition = action.Position;
                        break;
                }
                isFirstMoveInQueue = false;
            }
            return (finalEnergy, true, secondsPassed);
        }

        public void QueueRest(string[] args)
        {
            if (_isExecutingPath)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("Cannot queue actions while executing a path.");
                return;
            }

            RestType restType = RestType.ShortRest;
            if (args.Length > 1)
            {
                if (args[1].ToLower() == "short")
                {
                    restType = RestType.ShortRest;
                }
                else if (args[1].ToLower() == "long")
                {
                    restType = RestType.LongRest;
                }
                else if (args[1].ToLower() == "full")
                {
                    restType = RestType.FullRest;
                }

                Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued a {args[1].ToLower()} rest.");
            }
            else
            {
                restType = RestType.ShortRest;
                Core.CurrentTerminalRenderer.AddOutputToHistory("Queued a short rest.");
            }

            Vector2 restPosition = _player.ActionQueue.Any() ? _player.ActionQueue.Last().Position : (CurrentMapView == MapView.Local ? _player.LocalPosition : _player.WorldPosition);
            _player.ActionQueue.Add(new PendingAction(restType, restPosition));
        }

        private void QueueMovementInternal(Vector2 direction, string[] args, bool isRunning)
        {
            if (_isExecutingPath)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("Cannot queue movements while executing a path.");
                return;
            }

            bool isLocalMove = CurrentMapView == MapView.Local;
            int count = 1;
            if (args.Length > 1 && int.TryParse(args[1], out int parsedCount))
            {
                count = Math.Max(1, Math.Min(Global.MAX_SINGLE_MOVE_LIMIT, parsedCount));
            }

            if (isLocalMove && isRunning)
            {
                if (!_player.ActionQueue.Any(a => a.Type == ActionType.RunMove))
                {
                    if (!_player.Stats.CanExertEnergy(1))
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Not enough energy to start a local run! <Requires 1 EP>");
                        return;
                    }
                }
            }

            Vector2 oppositeDirection = -direction;
            int removedSteps = 0;

            while (_player.ActionQueue.Count > 0 && removedSteps < count)
            {
                int lastMoveIndex = _player.ActionQueue.FindLastIndex(a => a.Type == ActionType.WalkMove || a.Type == ActionType.RunMove);
                if (lastMoveIndex == -1) break;

                PendingAction lastMoveAction = _player.ActionQueue[lastMoveIndex];
                Vector2 prevPos = (lastMoveIndex > 0) ? _player.ActionQueue[lastMoveIndex - 1].Position : (isLocalMove ? _player.LocalPosition : _player.WorldPosition);
                Vector2 lastDirection = lastMoveAction.Position - prevPos;

                if (lastDirection == oppositeDirection)
                {
                    _player.ActionQueue.RemoveAt(lastMoveIndex);
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
                Vector2 currentPos = _player.ActionQueue.Any() ? _player.ActionQueue.Last().Position : (isLocalMove ? _player.LocalPosition : _player.WorldPosition);
                int validSteps = 0;

                for (int i = 0; i < remainingSteps; i++)
                {
                    Vector2 nextPos = currentPos + direction;

                    if (!IsPositionPassable(nextPos, CurrentMapView))
                    {
                        if (isLocalMove)
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... edge of the area.");
                        }
                        else
                        {
                            var mapData = GetMapDataAt((int)nextPos.X, (int)nextPos.Y);
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... terrain is impassable! <{mapData.TerrainType.ToLower()}>");
                        }
                        break;
                    }

                    var nextAction = new PendingAction(nextPos, isRunning);
                    var tempQueue = new List<PendingAction>(_player.ActionQueue) { nextAction };
                    var simulationResult = SimulateActionQueueEnergy(tempQueue);

                    if (!simulationResult.possible)
                    {
                        if (_isFreeMoveMode && !isLocalMove)
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory("[warning]Not enough energy. Auto-queuing a short rest.");
                            Vector2 restPosition = _player.ActionQueue.Any() ? _player.ActionQueue.Last().Position : _player.WorldPosition;

                            var tempQueueWithRest = new List<PendingAction>(_player.ActionQueue);
                            tempQueueWithRest.Add(new PendingAction(RestType.ShortRest, restPosition));
                            tempQueueWithRest.Add(nextAction);

                            if (SimulateActionQueueEnergy(tempQueueWithRest).possible)
                            {
                                _player.ActionQueue.Add(new PendingAction(RestType.ShortRest, restPosition));
                            }
                            else
                            {
                                Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... Not enough energy even after a rest!");
                                break;
                            }
                        }
                        else
                        {
                            int stepCost = GetMovementEnergyCost(nextAction, isLocalMove);
                            if (isLocalMove && isRunning) stepCost = 1;
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... Not enough energy! <Requires {stepCost} EP>");
                            break;
                        }
                    }

                    currentPos = nextPos;
                    _player.ActionQueue.Add(nextAction);
                    validSteps++;
                }

                if (validSteps > 0)
                {
                    string moveType = isRunning ? "run" : "walk";
                    if (removedSteps > 0)
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[undo]Backtracked {removedSteps} time(s), added {validSteps} {moveType}(s)");
                    }
                    else
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued {moveType} {validSteps} {args[0].ToLower()}");
                    }
                }
                else if (removedSteps > 0)
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[undo]Backtracked {removedSteps} time(s)");
                }
            }
            else if (removedSteps > 0)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory($"[undo]Backtracked {removedSteps} time(s)");
            }
        }

        public void QueueRunMovement(Vector2 direction, string[] args)
        {
            QueueMovementInternal(direction, args, true);
        }

        public void QueueWalkMovement(Vector2 direction, string[] args)
        {
            QueueMovementInternal(direction, args, false);
        }

        public void UpdateMovement(GameTime gameTime)
        {
            if (_isPaused) return;

            if (Core.CurrentWorldClockManager.IsInterpolatingTime)
            {
                return;
            }

            if (_actionAwaitingExecution != null)
            {
                bool success = ApplyActionEffects(_actionAwaitingExecution);
                _actionAwaitingExecution = null;

                if (success)
                {
                    _currentPathIndex++;
                    if (_currentPathIndex >= _player.ActionQueue.Count)
                    {
                        _isExecutingPath = false;
                        _player.ActionQueue.Clear();
                        _currentPathIndex = 0;
                        Core.CurrentTerminalRenderer.AddOutputToHistory("Action queue completed.");
                    }
                }
                else
                {
                    CancelPathExecution(true);
                }
                return;
            }

            if (_isExecutingPath && _currentPathIndex < _player.ActionQueue.Count)
            {
                PendingAction nextAction = _player.ActionQueue[_currentPathIndex];
                _actionAwaitingExecution = nextAction;
                int secondsPassed = CalculateSecondsForAction(nextAction);
                Core.CurrentWorldClockManager.PassTime(seconds: secondsPassed);
            }
        }

        private int CalculateSecondsForAction(PendingAction action)
        {
            bool isLocalMove = _pathExecutionMapView == MapView.Local;
            switch (action.Type)
            {
                case ActionType.WalkMove:
                case ActionType.RunMove:
                    Vector2 previousPosition;
                    string terrainType;
                    if (isLocalMove)
                    {
                        previousPosition = (_currentPathIndex > 0) ? _player.ActionQueue[_currentPathIndex - 1].Position : _player.LocalPosition;
                        terrainType = "LOCAL";
                    }
                    else
                    {
                        previousPosition = (_currentPathIndex > 0) ? _player.ActionQueue[_currentPathIndex - 1].Position : _player.WorldPosition;
                        terrainType = GetMapDataAt((int)action.Position.X, (int)action.Position.Y).TerrainType;
                    }
                    Vector2 moveDirection = action.Position - previousPosition;
                    int fullDuration = GetSecondsPassedDuringMovement(action.Type, terrainType, moveDirection, isLocalMove);

                    if (_currentPathIndex == 0 && !isLocalMove)
                    {
                        float timeScaleFactor = GetFirstMoveTimeScaleFactor(moveDirection);
                        return (int)Math.Ceiling(fullDuration * timeScaleFactor);
                    }

                    return fullDuration;
                case ActionType.ShortRest:
                    return _player.Stats.ShortRestDuration * 60;
                case ActionType.LongRest:
                    return _player.Stats.LongRestDuration * 60;
                case ActionType.FullRest:
                    return _player.Stats.FullRestDuration * 60;
                default:
                    return 0;
            }
        }

        private bool ApplyActionEffects(PendingAction action)
        {
            bool isLocalMove = _pathExecutionMapView == MapView.Local;
            switch (action.Type)
            {
                case ActionType.WalkMove:
                case ActionType.RunMove:
                    Vector2 nextPosition = action.Position;
                    if (!IsPositionPassable(nextPosition, _pathExecutionMapView))
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Movement blocked at {nextPosition}.");
                        return false;
                    }

                    string moveType = action.Type == ActionType.RunMove ? "Ran" : "Walked";
                    int secondsPassedForAction = CalculateSecondsForAction(action);
                    string timeString = Core.CurrentWorldClockManager.GetCommaFormattedTimeFromSeconds(secondsPassedForAction);

                    if (isLocalMove)
                    {
                        if (action.Type == ActionType.RunMove)
                        {
                            int firstRunIndex = _player.ActionQueue.FindIndex(a => a.Type == ActionType.RunMove);
                            if (_currentPathIndex == firstRunIndex)
                            {
                                _player.Stats.ExertEnergy(1);
                            }
                        }
                        _player.SetLocalPosition(nextPosition);
                    }
                    else
                    {
                        int energyCost = GetMovementEnergyCost(action, false);
                        if (!_player.Stats.CanExertEnergy(energyCost))
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Not enough energy to continue! Need {energyCost}, have {_player.Stats.CurrentEnergyPoints}");
                            return false;
                        }
                        _player.Stats.ExertEnergy(energyCost);

                        Vector2 oldWorldPos = _player.WorldPosition;
                        _player.SetPosition(nextPosition);

                        Vector2 moveDir = nextPosition - oldWorldPos;
                        Vector2 newLocalPos = new Vector2(32, 32);
                        if (moveDir.X > 0) newLocalPos.X = 0; else if (moveDir.X < 0) newLocalPos.X = 63;
                        if (moveDir.Y > 0) newLocalPos.Y = 0; else if (moveDir.Y < 0) newLocalPos.Y = 63;
                        if (moveDir.X != 0 && moveDir.Y == 0) newLocalPos.Y = 32;
                        if (moveDir.Y != 0 && moveDir.X == 0) newLocalPos.X = 32;
                        _player.SetLocalPosition(newLocalPos);

                        var mapData = GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[khaki]{moveType} through[gold] {mapData.TerrainType.ToLower()}[khaki].[dim] ({timeString})");
                    }
                    break;

                case ActionType.ShortRest:
                case ActionType.LongRest:
                case ActionType.FullRest:
                    _player.Stats.Rest(action.ActionRestType.Value);
                    string restType = action.ActionRestType.Value.ToString().Replace("Rest", "");
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[rest]Completed {restType.ToLower()} rest. Energy is now {_player.Stats.CurrentEnergyPoints}/{_player.Stats.MaxEnergyPoints}.");
                    break;
            }

            UpdateTetheredEntities();
            return true;
        }

        private void UpdateTetheredEntities()
        {
            _tetheredEntities.Clear();
            var playerPos = _player.WorldPosition;
            var tetherBounds = new Rectangle(
                (int)playerPos.X - TETHER_RANGE,
                (int)playerPos.Y - TETHER_RANGE,
                TETHER_RANGE * 2,
                TETHER_RANGE * 2);

            foreach (var entity in _worldEntities)
            {
                if (entity == _player) continue;

                if (tetherBounds.Contains(entity.WorldPosition))
                {
                    _tetheredEntities.Add(entity);
                }
            }
        }

        public void CancelPathExecution(bool interrupted = false)
        {
            if (_isExecutingPath)
            {
                if (_actionAwaitingExecution != null && (_actionAwaitingExecution.Type == ActionType.RunMove || _actionAwaitingExecution.Type == ActionType.WalkMove))
                {
                    float progress = Core.CurrentWorldClockManager.GetInterpolationProgress();
                    if (progress > 0 && progress < 1)
                    {
                        Vector2 newLocalPos;
                        if (_pathExecutionMapView == MapView.World)
                        {
                            Vector2 worldMoveDirection = _actionAwaitingExecution.Position - _player.WorldPosition;
                            Vector2 exitLocalPos = new Vector2(32, 32);
                            if (worldMoveDirection.X > 0) exitLocalPos.X = 63; else if (worldMoveDirection.X < 0) exitLocalPos.X = 0;
                            if (worldMoveDirection.Y > 0) exitLocalPos.Y = 63; else if (worldMoveDirection.Y < 0) exitLocalPos.Y = 0;
                            if (worldMoveDirection.X != 0 && worldMoveDirection.Y == 0) exitLocalPos.Y = 32;
                            if (worldMoveDirection.Y != 0 && worldMoveDirection.X == 0) exitLocalPos.X = 32;
                            newLocalPos = Vector2.Lerp(_player.LocalPosition, exitLocalPos, progress);
                        }
                        else // Local move
                        {
                            Vector2 startPos = (_currentPathIndex > 0) ? _player.ActionQueue[_currentPathIndex - 1].Position : _player.LocalPosition;
                            Vector2 endPos = _actionAwaitingExecution.Position;
                            newLocalPos = Vector2.Lerp(startPos, endPos, progress);
                        }
                        _player.SetLocalPosition(new Vector2((int)Math.Round(newLocalPos.X), (int)Math.Round(newLocalPos.Y)));
                    }
                }

                _isExecutingPath = false;
                _isPaused = false;
                _player.ActionQueue.Clear();
                _currentPathIndex = 0;
                _actionAwaitingExecution = null;
                ToggleIsFreeMoveMode(false);
                Core.CurrentWorldClockManager.CancelInterpolation();
                if (interrupted)
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory("[cancel]Action queue interrupted.");
                }
                else
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory("[cancel]Action queue cancelled.");
                }
            }
        }

        private float GetFirstMoveTimeScaleFactor(Vector2 worldMoveDirection)
        {
            Vector2 startPos = _player.LocalPosition;

            const float localGridMax = Global.LOCAL_GRID_SIZE - 1;

            Vector2 exitPos = startPos;

            if (worldMoveDirection.X > 0) exitPos.X = localGridMax;
            else if (worldMoveDirection.X < 0) exitPos.X = 0;

            if (worldMoveDirection.Y > 0) exitPos.Y = localGridMax;
            else if (worldMoveDirection.Y < 0) exitPos.Y = 0;

            float remainingDistance = Vector2.Distance(startPos, exitPos);

            Vector2 entryPos = startPos;
            if (worldMoveDirection.X > 0) entryPos.X = 0;
            else if (worldMoveDirection.X < 0) entryPos.X = localGridMax;

            if (worldMoveDirection.Y > 0) entryPos.Y = 0;
            else if (worldMoveDirection.Y < 0) entryPos.Y = localGridMax;

            if (worldMoveDirection.X == 0) entryPos.X = startPos.X;
            if (worldMoveDirection.Y == 0) entryPos.Y = startPos.Y;

            float totalDistance = Vector2.Distance(entryPos, exitPos);

            if (totalDistance <= 0.01f) return 1.0f;

            return Math.Clamp(remainingDistance / totalDistance, 0.0f, 1.0f);
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
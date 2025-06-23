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
        private float _moveTimer = 0f;
        private bool _isExecutingPath = false;
        private int _currentPathIndex = 0;
        private bool _isFreeMoveMode = false;

        private List<Entity> _worldEntities = new List<Entity>();
        private List<Entity> _tetheredEntities = new List<Entity>();
        private const int TETHER_RANGE = 32;

        public Vector2 PlayerWorldPos => _player.WorldPosition;
        public List<PendingAction> PendingActions => _player.ActionQueue;
        public bool IsExecutingPath => _isExecutingPath;
        public bool IsFreeMoveMode => _isFreeMoveMode;
        public int CurrentPathIndex => _currentPathIndex;
        public NoiseMapManager NoiseManager => _noiseManager;
        public PlayerStats PlayerStats => _player.Stats;
        public (int finalEnergy, bool possible, int minutesPassed) PendingQueueSimulationResult => SimulateActionQueueEnergy();

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

        public void QueueNewPath(List<Vector2> path, bool isRunning)
        {
            CancelPendingActions();
            AppendPath(path, isRunning);
        }

        public void AppendPath(List<Vector2> path, bool isRunning)
        {
            if (path == null) return;

            if (!isRunning) // For walking, just add the path. No energy cost, so no complex checks needed.
            {
                foreach (var pos in path)
                {
                    _player.ActionQueue.Add(new PendingAction(pos, isRunning: false));
                }
                return;
            }

            foreach (var nextPos in path)// For running, check energy for each step, auto-queuing rests if needed.
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
                Core.CurrentTerminalRenderer.AddOutputToHistory("[warning]Free move enabled.");
            }
            else
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("[warning]Free move disabled.");
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

            string upperTerrainType = terrainType.ToUpper();
            return upperTerrainType != "WATER" && upperTerrainType != "PEAKS";
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
            int timePassed = 0;

            timePassed += actionType switch
            {
                ActionType.WalkMove => (int)Math.Ceiling(6 / _player.Stats.WalkSpeed),
                ActionType.RunMove => (int)Math.Ceiling(6 / _player.Stats.RunSpeed),
                _ => timePassed
            };

            timePassed += terrainType.ToUpper() switch
            {
                "FLATLANDS" => 0,
                "HILLS" => (int)Math.Ceiling(timePassed * 0.5f),
                "MOUNTAINS" => timePassed + 5,
                _ => 0
            };



            return timePassed;
        }

        public (int finalEnergy, bool possible, int minutesPassed) SimulateActionQueueEnergy(List<PendingAction> customQueue = null)
        {
            var queueToSimulate = customQueue ?? _player.ActionQueue;
            int finalEnergy = _player.Stats.CurrentEnergyPoints;
            int maxEnergy = _player.Stats.MaxEnergyPoints;
            int minutesPassed = 0;

            foreach (var action in queueToSimulate)
            {
                switch (action.Type)
                {
                    case ActionType.WalkMove:
                    case ActionType.RunMove:
                        minutesPassed += GetMinutesPassedDuringMovement(action.Type, GetTerrainDescription((int)action.Position.X, (int)action.Position.Y));
                        int cost = GetMovementEnergyCost(action);
                        if (finalEnergy < cost)
                        {
                            return (finalEnergy, false, minutesPassed);
                        }
                        finalEnergy -= cost;
                        break;
                    case ActionType.ShortRest:
                        finalEnergy += _player.Stats.ShortRestEnergyRestored;
                        finalEnergy = Math.Min(finalEnergy, maxEnergy);

                        minutesPassed += _player.Stats.ShortRestDuration;
                        break;
                    case ActionType.LongRest:
                        finalEnergy += _player.Stats.LongRestEnergyRestored;
                        finalEnergy = Math.Min(finalEnergy, maxEnergy);

                        minutesPassed += _player.Stats.LongRestDuration;
                        break;
                    case ActionType.FullRest:
                        finalEnergy += _player.Stats.FullRestEnergyRestored;
                        finalEnergy = Math.Min(finalEnergy, maxEnergy);

                        minutesPassed += _player.Stats.FullRestDuration;
                        break;
                }
            }
            return (finalEnergy, true, minutesPassed);
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

            Vector2 restPosition = _player.ActionQueue.Any() ? _player.ActionQueue.Last().Position : _player.WorldPosition;
            _player.ActionQueue.Add(new PendingAction(restType, restPosition));
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

            while (_player.ActionQueue.Count > 0 && removedSteps < count)
            {
                int lastMoveIndex = -1;
                for (int i = _player.ActionQueue.Count - 1; i >= 0; i--)
                {
                    var action = _player.ActionQueue[i];
                    if (action.Type == ActionType.WalkMove || action.Type == ActionType.RunMove)
                    {
                        lastMoveIndex = i;
                        break;
                    }
                }

                if (lastMoveIndex == -1)
                {
                    break;
                }

                PendingAction lastMoveAction = _player.ActionQueue[lastMoveIndex];

                Vector2 prevPos = (lastMoveIndex > 0) ? _player.ActionQueue[lastMoveIndex - 1].Position : _player.WorldPosition;

                Vector2 lastDirection = lastMoveAction.Position - prevPos;

                if (lastDirection == oppositeDirection)
                {
                    int actionsToRemoveCount = _player.ActionQueue.Count - lastMoveIndex;
                    _player.ActionQueue.RemoveRange(lastMoveIndex, actionsToRemoveCount);
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
                Vector2 currentPos = _player.ActionQueue.Any() ? _player.ActionQueue.Last().Position : _player.WorldPosition;
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
                    var tempQueue = new List<PendingAction>(_player.ActionQueue) { nextAction };
                    var simulationResult = SimulateActionQueueEnergy(tempQueue);

                    if (!simulationResult.possible)
                    {
                        if (_isFreeMoveMode)
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
                            int stepCost = GetMovementEnergyCost(nextAction);
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
            if (_isExecutingPath && _player.ActionQueue.Count > 0)
            {
                _moveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_moveTimer >= Global.MOVE_DELAY_SECONDS)
                {
                    if (_currentPathIndex < _player.ActionQueue.Count)
                    {
                        PendingAction nextAction = _player.ActionQueue[_currentPathIndex];

                        int minutesPassed = 0;
                        switch (nextAction.Type)
                        {
                            case ActionType.WalkMove:
                            case ActionType.RunMove:
                                var mapData = GetMapDataAt((int)nextAction.Position.X, (int)nextAction.Position.Y);
                                minutesPassed = GetMinutesPassedDuringMovement(nextAction.Type, mapData.TerrainType);
                                break;
                            case ActionType.ShortRest:
                                minutesPassed = _player.Stats.ShortRestDuration;
                                break;
                            case ActionType.LongRest:
                                minutesPassed = _player.Stats.LongRestDuration;
                                break;
                            case ActionType.FullRest:
                                minutesPassed = _player.Stats.FullRestDuration;
                                break;
                        }

                        bool success = SimulateWorldTick(nextAction, minutesPassed);

                        if (success)
                        {
                            _currentPathIndex++;
                            _moveTimer = 0f;

                            if (_currentPathIndex >= _player.ActionQueue.Count)
                            {
                                _isExecutingPath = false;
                                _player.ActionQueue.Clear();
                                _currentPathIndex = 0;
                                ToggleExecutingPath(false);
                                Core.CurrentTerminalRenderer.AddOutputToHistory("Action queue completed.");
                            }
                        }
                        else
                        {
                            CancelPathExecution();
                        }
                    }
                }
            }
        }

        private bool SimulateWorldTick(PendingAction playerAction, int minutesPassed)
        {
            // Simulate Player Action //
            switch (playerAction.Type)
            {
                case ActionType.WalkMove:
                case ActionType.RunMove:
                    Vector2 nextPosition = playerAction.Position;
                    var mapData = GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                    if (!IsPositionPassable(nextPosition))
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... terrain is impassable! <{mapData.TerrainType.ToLower()}>  {(int)nextPosition.X}, {(int)nextPosition.Y}");
                        return false;
                    }

                    int energyCost = GetMovementEnergyCost(playerAction);
                    if (!_player.Stats.CanExertEnergy(energyCost))
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Not enough energy to continue! Need {energyCost} for {mapData.TerrainType.ToLower()}, have {_player.Stats.CurrentEnergyPoints}");
                        return false;
                    }

                    _player.SetPosition(nextPosition);
                    _player.Stats.ExertEnergy(energyCost);
                    string moveType = playerAction.Type == ActionType.RunMove ? "Ran" : "Walked";
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[khaki]{moveType} through[gold] {mapData.TerrainType.ToLower()}[khaki].[/o] <{(int)nextPosition.X}, {(int)nextPosition.Y}>");
                    break;

                case ActionType.ShortRest:
                case ActionType.LongRest:
                case ActionType.FullRest:
                    _player.Stats.Rest(playerAction.ActionRestType.Value);
                    string restType = playerAction.ActionRestType.Value.ToString().Replace("Rest", "");
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[rest]Completed {restType.ToLower()} rest. Energy is now {_player.Stats.CurrentEnergyPoints}/{_player.Stats.MaxEnergyPoints}.");
                    break;
            }

            // Simulate Tethered Entities Actions //
            foreach (var entity in _tetheredEntities)
            {
                entity.Update(minutesPassed, this);
            }

            // Advance World Time //
            Core.CurrentWorldClockManager.PassTime(minutes: minutesPassed);

            // Update Tethered List //
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

        public void CancelPathExecution()
        {
            if (_isExecutingPath)
            {
                _isExecutingPath = false;
                _player.ActionQueue.Clear();
                _currentPathIndex = 0;
                ToggleExecutingPath(false);
                ToggleIsFreeMoveMode(false);
                Core.CurrentTerminalRenderer.AddOutputToHistory("[cancel]Action queue cancelled.");
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

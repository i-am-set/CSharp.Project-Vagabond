using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ProjectVagabond
{
    public enum MapView { World, Local }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

    public class GameState
    {
        private NoiseMapManager _noiseManager;
        private bool _isExecutingPath = false;
        private bool _isFreeMoveMode = false;
        private bool _isPaused = false;

        public MapView PathExecutionMapView { get; private set; }

        public int PlayerEntityId { get; private set; }
        public Vector2 PlayerWorldPos => Core.ComponentStore.GetComponent<PositionComponent>(PlayerEntityId).WorldPosition;
        public Vector2 PlayerLocalPos => Core.ComponentStore.GetComponent<LocalPositionComponent>(PlayerEntityId).LocalPosition;
        public Queue<IAction> PendingActions => Core.ComponentStore.GetComponent<ActionQueueComponent>(PlayerEntityId).ActionQueue;
        public bool IsExecutingPath => _isExecutingPath;
        public bool IsPaused => _isPaused;
        public bool IsFreeMoveMode => _isFreeMoveMode;
        public NoiseMapManager NoiseManager => _noiseManager;
        public StatsComponent PlayerStats => Core.ComponentStore.GetComponent<StatsComponent>(PlayerEntityId);
        public MapView CurrentMapView { get; private set; } = MapView.World;
        public (int finalEnergy, bool possible, int secondsPassed) PendingQueueSimulationResult => SimulateActionQueueEnergy();
        public List<int> ActiveEntities { get; private set; } = new List<int>();
        public int InitialActionCount { get; private set; }
        public bool IsActionQueueDirty { get; set; } = true;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public GameState()
        {
            int masterSeed = RandomNumberGenerator.GetInt32(1, 99999) + Environment.TickCount;
            _noiseManager = new NoiseMapManager(masterSeed);

            // Create Player
            PlayerEntityId = Core.EntityManager.CreateEntity();
            Core.ComponentStore.AddComponent(PlayerEntityId, new PositionComponent { WorldPosition = new Vector2(0, 0) });
            Core.ComponentStore.AddComponent(PlayerEntityId, new LocalPositionComponent { LocalPosition = new Vector2(32, 32) });
            Core.ComponentStore.AddComponent(PlayerEntityId, new StatsComponent(5, 5, 5, 5, 5));
            Core.ComponentStore.AddComponent(PlayerEntityId, new ActionQueueComponent());
            Core.ComponentStore.AddComponent(PlayerEntityId, new PlayerTagComponent());
            Core.ChunkManager.RegisterEntity(PlayerEntityId, PlayerWorldPos);

            // Create NPC
            int npcId = Core.EntityManager.CreateEntity();
            var npcWorldPos = new Vector2(0, 0); // Start in the same chunk as the player
            Core.ComponentStore.AddComponent(npcId, new PositionComponent { WorldPosition = npcWorldPos });
            Core.ComponentStore.AddComponent(npcId, new LocalPositionComponent { LocalPosition = new Vector2(10, 10) });
            Core.ComponentStore.AddComponent(npcId, new ActionQueueComponent());
            Core.ComponentStore.AddComponent(npcId, new AIComponent());
            Core.ComponentStore.AddComponent(npcId, new NPCTagComponent());
            Core.ChunkManager.RegisterEntity(npcId, npcWorldPos);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void UpdateActiveEntities()
        {
            var playerPosComp = Core.ComponentStore.GetComponent<PositionComponent>(PlayerEntityId);
            if (playerPosComp == null)
            {
                ActiveEntities.Clear();
                return;
            }

            var playerChunkCoords = ChunkManager.WorldToChunkCoords(playerPosComp.WorldPosition);
            var tetheredEntities = Core.ChunkManager.GetEntitiesInTetherRange(playerChunkCoords);

            var highImportanceEntities = Core.ComponentStore.GetAllEntitiesWithComponent<HighImportanceComponent>();

            ActiveEntities = tetheredEntities.Union(highImportanceEntities).ToList();
        }

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

        public void ToggleExecutingPath(bool toggle)
        {
            ToggleIsFreeMoveMode(false);
            if (toggle)
            {
                InitialActionCount = PendingActions.Count;
                PathExecutionMapView = CurrentMapView;
                Core.ActionExecutionSystem.StartExecution();
            }
            else
            {
                InitialActionCount = 0;
                Core.ActionExecutionSystem.StopExecution();
            }
            _isExecutingPath = toggle;
            IsActionQueueDirty = true;
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
            IsActionQueueDirty = true;
        }

        public bool IsPositionPassable(Vector2 position, MapView view, out MapData mapData)
        {
            mapData = default;

            if (view == MapView.Local)
            {
                return position.X >= 0 && position.X < Global.LOCAL_GRID_SIZE && position.Y >= 0 && position.Y < Global.LOCAL_GRID_SIZE;
            }

            mapData = GetMapDataAt((int)position.X, (int)position.Y);
            return mapData.IsPassable;
        }

        public bool IsPositionPassable(Vector2 position, MapView view)
        {
            return IsPositionPassable(position, view, out _);
        }

        public int GetMovementEnergyCost(MoveAction action, bool isLocalMove = false)
        {
            if (isLocalMove || !action.IsRunning)
            {
                return 0;
            }

            if (action.IsRunning)
            {
                var mapData = GetMapDataAt((int)action.Destination.X, (int)action.Destination.Y);
                return mapData.EnergyCost;
            }

            return 0;
        }

        public int GetSecondsPassedDuringMovement(bool isRunning, MapData mapData, Vector2 moveDirection, bool isLocalMove = false)
        {
            float secondsPassed = 0;
            float timeMultiplier = 1.0f;

            if (moveDirection.X != 0 && moveDirection.Y != 0)
            {
                timeMultiplier = 1.5f;
            }

            float baseTime = isRunning ? (360f / PlayerStats.RunSpeed) : (360f / PlayerStats.WalkSpeed);

            if (isLocalMove)
            {
                secondsPassed += baseTime / Global.LOCAL_GRID_SIZE;
            }
            else
            {
                secondsPassed += baseTime;
                secondsPassed += mapData.TerrainHeight switch
                {
                    var height when height < Global.Instance.FlatlandsLevel => 0, // Flatlands (and water, though we can't enter it)
                    var height when height < Global.Instance.HillsLevel => secondsPassed * 0.5f, // Hills
                    var height when height < Global.Instance.MountainsLevel => secondsPassed + 300, // Mountains
                    _ => 0
                };
            }

            return (int)Math.Ceiling(secondsPassed * timeMultiplier);
        }

        public (int finalEnergy, bool possible, int secondsPassed) SimulateActionQueueEnergy(IEnumerable<IAction> customQueue = null)
        {
            var queueToSimulate = customQueue ?? PendingActions;
            if (!queueToSimulate.Any()) return (PlayerStats.CurrentEnergyPoints, true, 0);

            bool isLocalSim = CurrentMapView == MapView.Local;
            int finalEnergy = PlayerStats.CurrentEnergyPoints;
            int maxEnergy = PlayerStats.MaxEnergyPoints;
            int secondsPassed = 0;
            Vector2 lastPosition = isLocalSim ? PlayerLocalPos : PlayerWorldPos;
            bool isFirstMoveInQueue = true;
            bool localRunCostApplied = false;

            foreach (var action in queueToSimulate)
            {
                if (action is MoveAction moveAction)
                {
                    Vector2 moveDirection = moveAction.Destination - lastPosition;
                    MapData mapData = isLocalSim ? default : GetMapDataAt((int)moveAction.Destination.X, (int)moveAction.Destination.Y);

                    int moveDuration = GetSecondsPassedDuringMovement(moveAction.IsRunning, mapData, moveDirection, isLocalSim);

                    secondsPassed += moveDuration;
                    int cost = GetMovementEnergyCost(moveAction, isLocalSim);

                    if (isLocalSim && moveAction.IsRunning && !localRunCostApplied)
                    {
                        cost = 1;
                        localRunCostApplied = true;
                    }

                    if (finalEnergy < cost)
                    {
                        return (finalEnergy, false, secondsPassed);
                    }
                    finalEnergy -= cost;
                    lastPosition = moveAction.Destination;
                }
                else if (action is RestAction restAction)
                {
                    switch (restAction.RestType)
                    {
                        case RestType.ShortRest:
                            finalEnergy += PlayerStats.ShortRestEnergyRestored;
                            secondsPassed += PlayerStats.ShortRestDuration * 60;
                            break;
                        case RestType.LongRest:
                            finalEnergy += PlayerStats.LongRestEnergyRestored;
                            secondsPassed += PlayerStats.LongRestDuration * 60;
                            break;
                        case RestType.FullRest:
                            finalEnergy += PlayerStats.FullRestEnergyRestored;
                            secondsPassed += PlayerStats.FullRestDuration * 60;
                            break;
                    }
                    finalEnergy = Math.Min(finalEnergy, maxEnergy);
                    lastPosition = restAction.Position;
                }
                isFirstMoveInQueue = false;
            }
            return (finalEnergy, true, secondsPassed);
        }

        public void CancelPathExecution(bool interrupted = false)
        {
            if (_isExecutingPath)
            {
                // The new ActionExecutionSystem does not use _actionAwaitingExecution.
                // The cancellation logic is simplified. We just stop execution and clear the queue.
                // Any mid-action progress is handled by WorldClockManager.CancelInterpolation.

                ToggleExecutingPath(false);
                _isPaused = false;
                PendingActions.Clear();
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

        public float GetFirstMoveTimeScaleFactor(Vector2 worldMoveDirection)
        {
            Vector2 startPos = PlayerLocalPos;

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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ProjectVagabond
{
    public enum MapView
    {
        World,
        Local
    }

    public class GameState
    {
        // Injected Dependencies
        private readonly NoiseMapManager _noiseManager;
        private readonly ComponentStore _componentStore;
        private readonly WorldClockManager _worldClockManager;
        private readonly ChunkManager _chunkManager;
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;

        // Lazyloaded System Dependencies
        private ActionExecutionSystem _actionExecutionSystem;
        private CombatTurnSystem _combatTurnSystem;

        private bool _isExecutingActions = false;
        private bool _isFreeMoveMode = false;
        private bool _isPaused = false;
        private readonly Random _random = new Random();

        // Combat Initiation State
        public bool IsCombatInitiationPending { get; private set; } = false;
        public List<int> PendingCombatants { get; private set; } = new List<int>();


        public MapView PathExecutionMapView { get; private set; }

        public int PlayerEntityId { get; private set; }
        public Vector2 PlayerWorldPos => _componentStore.GetComponent<PositionComponent>(PlayerEntityId).WorldPosition;
        public Vector2 PlayerLocalPos => _componentStore.GetComponent<LocalPositionComponent>(PlayerEntityId).LocalPosition;
        public Queue<IAction> PendingActions => _componentStore.GetComponent<ActionQueueComponent>(PlayerEntityId).ActionQueue;
        public bool IsExecutingActions => _isExecutingActions;
        public bool IsPaused => _isPaused;
        public bool IsFreeMoveMode => _isFreeMoveMode;
        public NoiseMapManager NoiseManager => _noiseManager;
        public StatsComponent PlayerStats => _componentStore.GetComponent<StatsComponent>(PlayerEntityId);
        public MapView CurrentMapView { get; private set; } = MapView.World;
        public (int finalEnergy, bool possible, float secondsPassed) PendingQueueSimulationResult => SimulateActionQueueEnergy();
        public List<int> ActiveEntities { get; private set; } = new List<int>();
        public int InitialActionCount { get; private set; }
        public bool IsActionQueueDirty { get; set; } = true;
        public Dictionary<int, List<(Vector2 Position, MovementMode Mode)>> AIPreviewPaths { get; set; } = new Dictionary<int, List<(Vector2, MovementMode)>>();

        // Combat State
        public bool IsInCombat { get; private set; } = false;
        public List<int> Combatants { get; private set; } = new List<int>();
        public List<int> InitiativeOrder { get; private set; } = new List<int>();
        public int CurrentTurnEntityId { get; private set; }
        public CombatUIState UIState { get; set; } = CombatUIState.Default;
        public int? SelectedTargetId { get; set; } = null;
        public List<(Vector2 Position, MovementMode Mode)> CombatMovePreviewPath { get; set; } = new List<(Vector2, MovementMode)>();
        public MovementMode CombatMovePreviewMode { get; set; } = MovementMode.Walk;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public GameState(NoiseMapManager noiseManager, ComponentStore componentStore, WorldClockManager worldClockManager, ChunkManager chunkManager, Global global, SpriteManager spriteManager)
        {
            _noiseManager = noiseManager;
            _componentStore = componentStore;
            _worldClockManager = worldClockManager;
            _chunkManager = chunkManager;
            _global = global;
            _spriteManager = spriteManager;

            EventBus.Subscribe<GameEvents.ActionQueueChanged>(e => IsActionQueueDirty = true); // Subscribe to the event to mark the queue as dirty whenever it's changed.
        }

        public void InitializeWorld()
        {
            PlayerEntityId = Spawner.Spawn("player", worldPosition: new Vector2(0, 0), localPosition: new Vector2(32, 32));
            _componentStore.AddComponent(PlayerEntityId, new MovementProgressComponent());
            Spawner.Spawn("bandit", worldPosition: new Vector2(0, 0), localPosition: new Vector2(34, 36));
        }

        public void InitializeRenderableEntities()
        {
            // Player
            var playerRenderable = _componentStore.GetComponent<RenderableComponent>(PlayerEntityId);
            if (playerRenderable != null)
            {
                playerRenderable.Texture = _spriteManager.PlayerSprite;
            }

            // NPCs
            var npcEntities = _componentStore.GetAllEntitiesWithComponent<NPCTagComponent>().ToList();
            foreach (var npcId in npcEntities)
            {
                // Get the component that the Spawner already created from the JSON file.
                var renderable = _componentStore.GetComponent<RenderableComponent>(npcId);
                // If the component exists but doesn't have a texture yet, assign one.
                // This preserves the color and other properties loaded from the JSON.
                if (renderable != null && renderable.Texture == null)
                {
                    // For now, all NPCs get a generic pixel sprite.
                    // In the future, you could have a lookup here based on archetype ID.
                    renderable.Texture = ServiceLocator.Get<Texture2D>();
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // COMBAT MANAGEMENT
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        public void RequestCombatInitiation(int requestingEntityId)
        {
            if (IsCombatInitiationPending || IsInCombat) return;

            IsCombatInitiationPending = true;
            PendingCombatants.Clear();
            PendingCombatants.Add(PlayerEntityId);
            PendingCombatants.Add(requestingEntityId);
        }

        public void ClearCombatInitiationRequest()
        {
            IsCombatInitiationPending = false;
            PendingCombatants.Clear();
        }

        public void InitiateCombat(List<int> initialCombatants)
        {
            if (IsInCombat) return;

            EventBus.Publish(new GameEvents.CombatStateChanged { IsInCombat = true });
            IsInCombat = true;
            CurrentMapView = MapView.Local;
            Combatants = new List<int>(initialCombatants);

            // Resolve any positional overlaps before starting combat.
            ResolveCombatantOverlaps();

            InitiativeOrder.Clear();

            var initiativeScores = new Dictionary<int, int>();

            foreach (var entityId in Combatants)
            {
                var stats = _componentStore.GetComponent<StatsComponent>(entityId);
                int agility = stats?.Agility ?? 0;
                int initiative = _random.Next(1, 21) + agility;
                initiativeScores[entityId] = initiative;
            }

            InitiativeOrder = Combatants.OrderByDescending(id => initiativeScores[id]).ToList();

            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[palette_yellow]Combat has begun! Initiative Order: " });

            var uniqueNames = EntityNamer.GetUniqueNames(InitiativeOrder);
            for (int i = 0; i < InitiativeOrder.Count; i++)
            {
                var entityId = InitiativeOrder[i];
                string name = uniqueNames[entityId];
                string lineMessage = $"[palette_yellow]  {i + 1}. {name} ({initiativeScores[entityId]})";
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = lineMessage });
            }

            // No automatic target selection. Player must choose.
            SelectedTargetId = null;

            _combatTurnSystem ??= ServiceLocator.Get<CombatTurnSystem>();
            _combatTurnSystem.StartCombat();
        }

        private void ResolveCombatantOverlaps()
        {
            bool overlapsResolved = false;
            int safetyBreak = 0; // To prevent infinite loops in edge cases
            bool loggedMessage = false;

            while (!overlapsResolved && safetyBreak < 10)
            {
                overlapsResolved = true; // Assume resolved until an overlap is found
                var occupiedTiles = new Dictionary<Vector2, int>();

                foreach (var entityId in Combatants)
                {
                    var posComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                    if (posComp == null) continue;

                    if (occupiedTiles.ContainsKey(posComp.LocalPosition))
                    {
                        // Overlap detected!
                        overlapsResolved = false;
                        if (!loggedMessage)
                        {
                            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[dim]Adjusting starting positions to avoid overlap." });
                            loggedMessage = true;
                        }

                        // Find a new position for the current entity
                        var newPos = FindUnoccupiedNeighbor(posComp.LocalPosition, occupiedTiles.Keys);
                        if (newPos.HasValue)
                        {
                            posComp.LocalPosition = newPos.Value;
                        }
                        else
                        {
                            // This is a fallback, should rarely happen.
                            // If no free space, just move it one tile right.
                            // The next loop iteration will resolve this new potential conflict.
                            posComp.LocalPosition = new Vector2(posComp.LocalPosition.X + 1, posComp.LocalPosition.Y);
                        }
                    }
                    occupiedTiles[posComp.LocalPosition] = entityId;
                }
                safetyBreak++;
            }
        }

        private Vector2? FindUnoccupiedNeighbor(Vector2 origin, ICollection<Vector2> occupiedTiles)
        {
            var neighborOffsets = new Vector2[]
            {
                new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0),
                new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1)
            }.OrderBy(v => _random.Next()).ToList();

            foreach (var offset in neighborOffsets)
            {
                var potentialPos = origin + offset;

                // Check bounds
                if (potentialPos.X < 0 || potentialPos.X >= Global.LOCAL_GRID_SIZE ||
                    potentialPos.Y < 0 || potentialPos.Y >= Global.LOCAL_GRID_SIZE)
                {
                    continue;
                }

                // Check if occupied
                if (!occupiedTiles.Contains(potentialPos))
                {
                    return potentialPos;
                }
            }

            return null; // No free neighbor found
        }

        public void AddEntityToCombat(int entityId)
        {
            // Logic to add an entity to an ongoing combat will be implemented later.
        }

        public void EndCombat()
        {
            if (!IsInCombat) return;

            IsInCombat = false;
            UIState = CombatUIState.Default; // Return control to the player
            Combatants.Clear();
            InitiativeOrder.Clear();
            SelectedTargetId = null;

            // FIX: Clear any leftover actions from combat and cancel any time interpolation.
            _componentStore.GetComponent<ActionQueueComponent>(PlayerEntityId)?.ActionQueue.Clear();
            _worldClockManager.CancelInterpolation();

            EventBus.Publish(new GameEvents.CombatStateChanged { IsInCombat = false });
        }

        public void RemoveEntityFromCombat(int entityId)
        {
            Combatants.Remove(entityId);
            InitiativeOrder.Remove(entityId);
        }

        public void SetCurrentTurnEntity(int entityId)
        {
            CurrentTurnEntityId = entityId;
        }

        /// <summary>
        /// Checks if the player has enough time remaining in their turn to make at least one move.
        /// </summary>
        /// <returns>True if the player can still move, false otherwise.</returns>
        public bool CanPlayerMoveInCombat()
        {
            if (!IsInCombat) return false;

            var turnStats = _componentStore.GetComponent<TurnStatsComponent>(PlayerEntityId);
            var playerStats = _componentStore.GetComponent<StatsComponent>(PlayerEntityId);

            if (turnStats == null || playerStats == null) return false;

            float remainingTime = Global.COMBAT_TURN_DURATION_SECONDS - turnStats.MovementTimeUsedThisTurn;

            // Calculate the cost of the cheapest possible move (a non-diagonal walk)
            float cheapestMoveCost = GetSecondsPassedDuringMovement(playerStats, MovementMode.Walk, default, new Vector2(1, 0), true);

            return remainingTime >= cheapestMoveCost;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void UpdateActiveEntities()
        {
            var playerPosComp = _componentStore.GetComponent<PositionComponent>(PlayerEntityId);
            if (playerPosComp == null)
            {
                ActiveEntities.Clear();
                return;
            }

            var playerChunkCoords = ChunkManager.WorldToChunkCoords(playerPosComp.WorldPosition);
            var tetheredEntities = _chunkManager.GetEntitiesInTetherRange(playerChunkCoords);
            var highImportanceEntities = _componentStore.GetAllEntitiesWithComponent<HighImportanceComponent>();
            ActiveEntities = tetheredEntities.Union(highImportanceEntities).ToList();
        }

        public void ToggleMapView()
        {
            ClearPendingActions();
            CancelExecutingActions();
            CurrentMapView = (CurrentMapView == MapView.World) ? MapView.Local : MapView.World;
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[dim]Switched to {CurrentMapView} map view." });
        }

        public void TogglePause()
        {
            if (_isExecutingActions)
            {
                _isPaused = !_isPaused;
            }
        }

        public void ToggleExecutingActions(bool toggle)
        {
            _actionExecutionSystem ??= ServiceLocator.Get<ActionExecutionSystem>();
            ToggleIsFreeMoveMode(false);
            if (toggle)
            {
                InitialActionCount = PendingActions.Count;
                PathExecutionMapView = CurrentMapView;
                if (PathExecutionMapView == MapView.World)
                {
                    _actionExecutionSystem.StartExecution();
                }
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Executing queue of[undo] {PendingActions.Count}[gray] action(s)..." });
            }
            else
            {
                InitialActionCount = 0;
                _actionExecutionSystem.StopExecution();
            }
            _isExecutingActions = toggle;
            IsActionQueueDirty = true;
            ClearAIPreviewPaths();
        }

        public void ToggleIsFreeMoveMode(bool toggle)
        {
            bool cachedFreeMoveMode = _isFreeMoveMode;
            _isFreeMoveMode = toggle;

            if (cachedFreeMoveMode != _isFreeMoveMode)
            {
                if (toggle) EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[warning]Free move enabled." });
                else EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[warning]Free move disabled." });
            }
            IsActionQueueDirty = true;
        }

        public bool IsPositionPassable(Vector2 position, MapView view, int pathfindingEntityId, Vector2 targetDestination, out MapData mapData)
        {
            mapData = default;

            // 1. Bounds Check
            if (view == MapView.Local)
            {
                if (!(position.X >= 0 && position.X < Global.LOCAL_GRID_SIZE && position.Y >= 0 && position.Y < Global.LOCAL_GRID_SIZE))
                    return false;
            }

            // 2. Terrain Check
            if (view == MapView.World)
            {
                mapData = GetMapDataAt((int)position.X, (int)position.Y);
                if (!(mapData.TerrainHeight >= _global.WaterLevel && mapData.TerrainHeight < _global.MountainsLevel))
                    return false;
            }

            // 3. Entity Blocking Check
            // An entity can always path *to* the target destination, even if it's occupied.
            // The logic that *uses* the path is responsible for stopping before the final tile if needed.
            if (position == targetDestination)
            {
                return true;
            }

            // Check if any *other* entity occupies this tile.
            if (IsTileOccupied(position, pathfindingEntityId, view))
            {
                return false;
            }

            return true; // All checks passed
        }

        public bool IsPositionPassable(Vector2 position, MapView view, out MapData mapData)
        {
            return IsPositionPassable(position, view, -1, new Vector2(-1, -1), out mapData);
        }

        public bool IsPositionPassable(Vector2 position, MapView view)
        {
            return IsPositionPassable(position, view, out _);
        }

        public bool IsTileOccupied(Vector2 position, int askingEntityId, MapView view)
        {
            var entitiesToCheck = IsInCombat ? Combatants : ActiveEntities;
            foreach (var entityId in entitiesToCheck)
            {
                if (entityId == askingEntityId) continue;

                if (view == MapView.Local)
                {
                    var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                    if (localPosComp != null && localPosComp.LocalPosition == position)
                    {
                        return true; // Blocked by another entity.
                    }
                }
                else // World View
                {
                    var worldPosComp = _componentStore.GetComponent<PositionComponent>(entityId);
                    if (worldPosComp != null && worldPosComp.WorldPosition == position)
                    {
                        return true; // Blocked by another entity.
                    }
                }
            }
            return false;
        }

        public int GetMovementEnergyCost(MoveAction action, bool isLocalMove = false)
        {
            if (action.Mode != MovementMode.Run)
            {
                return 0; // Walking and Jogging are free.
            }

            // Only running costs energy.
            if (isLocalMove || IsInCombat)
            {
                return 1; // Running costs 1 EP per tile on the local map.
            }
            else // World map
            {
                var mapData = GetMapDataAt((int)action.Destination.X, (int)action.Destination.Y);
                return GetTerrainEnergyCost(mapData.TerrainHeight); // Running cost is based on terrain.
            }
        }

        public float GetSecondsPassedDuringMovement(StatsComponent stats, MovementMode mode, MapData mapData, Vector2 moveDirection, bool isLocalMove = false)
        {
            float distanceInFeet = isLocalMove ? Global.FEET_PER_LOCAL_TILE : Global.FEET_PER_WORLD_TILE;
            float baseSpeedStat = mode switch
            {
                MovementMode.Walk => stats.WalkSpeed,
                MovementMode.Jog => stats.JogSpeed,
                MovementMode.Run => stats.RunSpeed,
                _ => stats.WalkSpeed
            };
            float speedInFtPerSec = baseSpeedStat * Global.FEET_PER_SECOND_PER_SPEED_UNIT;

            if (speedInFtPerSec <= 0) return float.MaxValue;

            float secondsPassed = distanceInFeet / speedInFtPerSec;

            // Apply diagonal movement penalty
            if (moveDirection.X != 0 && moveDirection.Y != 0)
            {
                secondsPassed *= 1.414f; // Approx. sqrt(2)
            }

            // Apply world map terrain penalties
            if (!isLocalMove)
            {
                secondsPassed += mapData.TerrainHeight switch
                {
                    var height when height < _global.FlatlandsLevel => 0,
                    var height when height < _global.HillsLevel => secondsPassed * 0.5f,
                    var height when height < _global.MountainsLevel => secondsPassed + 300,
                    _ => 0
                };
            }

            return secondsPassed;
        }

        public (int finalEnergy, bool possible, float secondsPassed) SimulateActionQueueEnergy(IEnumerable<IAction> customQueue = null)
        {
            var queueToSimulate = customQueue ?? PendingActions;
            if (!queueToSimulate.Any()) return (PlayerStats.CurrentEnergyPoints, true, 0f);

            bool isLocalMove = CurrentMapView == MapView.Local;
            int finalEnergy = PlayerStats.CurrentEnergyPoints;
            int maxEnergy = PlayerStats.MaxEnergyPoints;
            float secondsPassed = 0f;
            Vector2 lastPosition = isLocalMove ? PlayerLocalPos : PlayerWorldPos;
            bool isFirstMoveInQueue = true;

            foreach (var action in queueToSimulate)
            {
                if (action is MoveAction moveAction)
                {
                    // Determine the actual mode of movement based on available energy
                    MovementMode actualMode = moveAction.Mode;
                    int energyCost = GetMovementEnergyCost(moveAction, isLocalMove);
                    if (moveAction.Mode == MovementMode.Run && finalEnergy < energyCost)
                    {
                        actualMode = MovementMode.Jog;
                    }

                    // Check if the originally intended action is possible (for world map auto-resting)
                    if (finalEnergy < energyCost && !isLocalMove)
                    {
                        return (finalEnergy, false, secondsPassed);
                    }

                    // Calculate time passed based on the actual movement mode
                    Vector2 moveDirection = moveAction.Destination - lastPosition;
                    MapData mapData = isLocalMove ? default : GetMapDataAt((int)moveAction.Destination.X, (int)moveAction.Destination.Y);
                    float moveDuration = GetSecondsPassedDuringMovement(PlayerStats, actualMode, mapData, moveDirection, isLocalMove);

                    if (!isLocalMove && isFirstMoveInQueue)
                    {
                        float scaleFactor = GetFirstMoveTimeScaleFactor(moveDirection);
                        moveDuration *= scaleFactor;
                    }
                    secondsPassed += moveDuration;

                    // Deduct energy based on the actual movement mode
                    int actualEnergyCost = GetMovementEnergyCost(new MoveAction(moveAction.ActorId, moveAction.Destination, actualMode), isLocalMove);
                    finalEnergy -= actualEnergyCost;

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

        public List<(Vector2 Position, MovementMode Mode)> GetAffordablePath(int entityId, Vector2 start, Vector2 end, MovementMode mode, out float totalTimeCost)
        {
            totalTimeCost = 0f;
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            var turnStats = _componentStore.GetComponent<TurnStatsComponent>(entityId);
            if (stats == null || turnStats == null)
            {
                return null;
            }

            float timeBudget = Global.COMBAT_TURN_DURATION_SECONDS - turnStats.MovementTimeUsedThisTurn;
            if (timeBudget <= 0)
            {
                return new List<(Vector2, MovementMode)>();
            }

            var fullPath = Pathfinder.FindPath(entityId, start, end, this, mode, PathfindingMode.Time, MapView.Local);
            if (fullPath == null || !fullPath.Any())
            {
                return null;
            }

            var affordablePath = new List<(Vector2 Position, MovementMode Mode)>();
            var lastPos = start;
            int simulatedEnergy = stats.CurrentEnergyPoints;

            foreach (var step in fullPath)
            {
                var moveDirection = step - lastPos;
                var currentMode = mode;

                int energyCostForRun = GetMovementEnergyCost(new MoveAction(entityId, step, MovementMode.Run), true);
                if (currentMode == MovementMode.Run && simulatedEnergy < energyCostForRun)
                {
                    currentMode = MovementMode.Jog;
                }

                float stepCost = GetSecondsPassedDuringMovement(stats, currentMode, default, moveDirection, true);

                if (totalTimeCost + stepCost <= timeBudget)
                {
                    totalTimeCost += stepCost;
                    affordablePath.Add((step, currentMode));
                    if (currentMode == MovementMode.Run)
                    {
                        simulatedEnergy -= energyCostForRun;
                    }
                    lastPos = step;
                }
                else
                {
                    break;
                }
            }
            return affordablePath;
        }

        public void ExecuteActions()
        {
            ToggleExecutingActions(false);
        }

        public void ClearPendingActions()
        {
            PendingActions.Clear();
            ClearAIPreviewPaths();
        }

        public bool CancelExecutingActions(bool interrupted = false)
        {
            if (_isExecutingActions)
            {
                _actionExecutionSystem ??= ServiceLocator.Get<ActionExecutionSystem>();
                _actionExecutionSystem.HandleInterruption();
                ToggleExecutingActions(false);
                _isPaused = false;
                PendingActions.Clear();
                ToggleIsFreeMoveMode(false);
                _worldClockManager.CancelInterpolation();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = interrupted ? "[cancel]Action queue interrupted." : "[cancel]Action queue cancelled." });
                return true;
            }
            return false;
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
            if (noise < _global.WaterLevel) return "deep water";
            if (noise < _global.FlatlandsLevel) return "flatlands";
            if (noise < _global.HillsLevel) return "hills";
            if (noise < _global.MountainsLevel) return "mountains";
            return "peaks";
        }

        public string GetTerrainDescription(MapData data) => GetTerrainDescription(data.TerrainHeight);
        public string GetTerrainDescription(int x, int y) => GetTerrainDescription(GetMapDataAt(x, y).TerrainHeight);

        public Texture2D GetTerrainTexture(float noise)
        {
            if (noise < _global.WaterLevel) return _spriteManager.WaterSprite;
            if (noise < _global.FlatlandsLevel) return _spriteManager.FlatlandSprite;
            if (noise < _global.HillsLevel) return _spriteManager.HillSprite;
            if (noise < _global.MountainsLevel) return _spriteManager.MountainSprite;
            return _spriteManager.PeakSprite;
        }

        public Texture2D GetTerrainTexture(int x, int y) => GetTerrainTexture(GetNoiseAt(x, y));

        private int GetTerrainEnergyCost(float height)
        {
            if (height < _global.WaterLevel) return 3;
            if (height < _global.FlatlandsLevel) return 1;
            if (height < _global.HillsLevel) return 2;
            if (height < _global.MountainsLevel) return 4;
            return 5;
        }

        public int? GetEntityIdAtGridPos(Vector2 gridPos, MapView view)
        {
            foreach (var entityId in ActiveEntities)
            {
                if (view == MapView.Local)
                {
                    var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                    if (localPosComp != null && (int)localPosComp.LocalPosition.X == (int)gridPos.X && (int)localPosComp.LocalPosition.Y == (int)gridPos.Y)
                    {
                        return entityId;
                    }
                }
                else // World View
                {
                    var worldPosComp = _componentStore.GetComponent<PositionComponent>(entityId);
                    if (worldPosComp != null && (int)worldPosComp.WorldPosition.X == (int)gridPos.X && (int)worldPosComp.WorldPosition.Y == (int)gridPos.Y)
                    {
                        return entityId;
                    }
                }
            }
            return null;
        }

        public void ClearAIPreviewPaths()
        {
            AIPreviewPaths.Clear();
        }

        /// <summary>
        /// Returns a set of all tile positions the player is queued to move to.
        /// </summary>
        public HashSet<Vector2> GetPlayerQueuedMovePositions()
        {
            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(PlayerEntityId);
            if (actionQueue == null)
            {
                return new HashSet<Vector2>();
            }
            return actionQueue.ActionQueue.OfType<MoveAction>().Select(ma => ma.Destination).ToHashSet();
        }
    }
}
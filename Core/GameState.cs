using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ProjectVagabond
{
    public enum MapView { World, Local }

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
        private AISystem _aiSystem;
        private CombatTurnSystem _combatTurnSystem;

        private bool _isExecutingActions = false;
        private bool _isFreeMoveMode = false;
        private bool _isPaused = false;
        private readonly Random _random = new Random();

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

        // Combat State
        public const int COMBAT_TURN_DURATION_SECONDS = 15;
        public bool IsInCombat { get; private set; } = false;
        public List<int> Combatants { get; private set; } = new List<int>();
        public List<int> InitiativeOrder { get; private set; } = new List<int>();
        public int CurrentTurnEntityId { get; private set; }
        public CombatUIState UIState { get; set; } = CombatUIState.Default;
        public int? SelectedTargetId { get; set; } = null;
        public List<Vector2> CombatMovePreviewPath { get; set; } = new List<Vector2>();
        public bool IsCombatMovePreviewRunning { get; set; } = false;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public GameState(NoiseMapManager noiseManager, ComponentStore componentStore, WorldClockManager worldClockManager, ChunkManager chunkManager, Global global, SpriteManager spriteManager)
        {
            _noiseManager = noiseManager;
            _componentStore = componentStore;
            _worldClockManager = worldClockManager;
            _chunkManager = chunkManager;
            _global = global;
            _spriteManager = spriteManager;
        }

        public void InitializeWorld()
        {
            PlayerEntityId = Spawner.Spawn("player", worldPosition: new Vector2(0, 0), localPosition: new Vector2(32, 32));
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
        public void InitiateCombat(List<int> initialCombatants)
        {
            CancelExecutingActions(true);

            if (IsInCombat) return;

            EventBus.Publish(new GameEvents.CombatStateChanged { IsInCombat = true });
            IsInCombat = true;
            CurrentMapView = MapView.Local;
            Combatants = new List<int>(initialCombatants);
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

            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[yellow]Combat has begun! Initiative Order: " });

            var uniqueNames = EntityNamer.GetUniqueNames(InitiativeOrder);
            for (int i = 0; i < InitiativeOrder.Count; i++)
            {
                var entityId = InitiativeOrder[i];
                string name = uniqueNames[entityId];
                string lineMessage = $"[yellow]  {i + 1}. {name} ({initiativeScores[entityId]})";
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = lineMessage });
            }

            // --- Automatic Target Selection Logic ---
            var enemies = new List<int>(Combatants);
            enemies.Remove(PlayerEntityId);

            if (enemies.Count == 1)
            {
                SelectedTargetId = enemies[0];
            }
            else if (enemies.Count > 1)
            {
                int closestEnemyId = -1;
                float minDistance = float.MaxValue;
                var playerPosComp = _componentStore.GetComponent<LocalPositionComponent>(PlayerEntityId);

                if (playerPosComp != null)
                {
                    foreach (var enemyId in enemies)
                    {
                        var enemyPosComp = _componentStore.GetComponent<LocalPositionComponent>(enemyId);
                        if (enemyPosComp != null)
                        {
                            float distance = Vector2.Distance(playerPosComp.LocalPosition, enemyPosComp.LocalPosition);
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                closestEnemyId = enemyId;
                            }
                        }
                    }
                }

                if (closestEnemyId != -1)
                {
                    SelectedTargetId = closestEnemyId;
                }
                else
                {
                    // Failsafe in case positions couldn't be determined
                    SelectedTargetId = null;
                }
            }
            else
            {
                SelectedTargetId = null;
            }

            // Log the auto-selected target if one was found
            if (SelectedTargetId.HasValue)
            {
                var targetName = EntityNamer.GetName(SelectedTargetId.Value);
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[dim]Auto-targeting {targetName}." });
            }
            // --- End of Automatic Target Selection Logic ---

            _combatTurnSystem ??= ServiceLocator.Get<CombatTurnSystem>();
            _combatTurnSystem.StartCombat();
        }

        public void AddEntityToCombat(int entityId)
        {
            // Logic to add an entity to an ongoing combat will be implemented later.
        }

        public void EndCombat()
        {
            // Logic to clean up and end combat will be implemented later.
            UIState = CombatUIState.Default;
            EventBus.Publish(new GameEvents.CombatStateChanged { IsInCombat = false });
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

            float remainingTime = COMBAT_TURN_DURATION_SECONDS - turnStats.MovementTimeUsedThisTurn;

            // Calculate the cost of the cheapest possible move (a non-diagonal walk)
            float baseTime = (Global.FeetPerWorldTile * Global.SECONDS_PER_FOOT_SCALING_FACTOR) / playerStats.WalkSpeed;
            float secondsPassed = baseTime / Global.LOCAL_GRID_SIZE;
            float cheapestMoveCost = (float)Math.Ceiling(secondsPassed * 1.0f); // timeMultiplier is 1.0 for non-diagonal

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
                _actionExecutionSystem.StartExecution();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Executing queue of[undo] {PendingActions.Count}[gray] action(s)..." });
            }
            else
            {
                InitialActionCount = 0;
                _actionExecutionSystem.StopExecution();
            }
            _isExecutingActions = toggle;
            IsActionQueueDirty = true;
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

            // Basic terrain/bounds checks
            if (view == MapView.Local)
            {
                if (!(position.X >= 0 && position.X < Global.LOCAL_GRID_SIZE && position.Y >= 0 && position.Y < Global.LOCAL_GRID_SIZE))
                {
                    return false; // Out of bounds
                }
            }
            else // World view
            {
                mapData = GetMapDataAt((int)position.X, (int)position.Y);
                if (!(mapData.TerrainHeight >= _global.WaterLevel && mapData.TerrainHeight < _global.MountainsLevel))
                {
                    return false; // Impassable terrain
                }
            }

            // Combatant check (only for local view during combat)
            if (IsInCombat && view == MapView.Local && pathfindingEntityId != -1)
            {
                // The destination tile itself is ALWAYS considered valid for the pathfinder to reach.
                // The calling system is responsible for stopping before it if needed.
                if (position == targetDestination)
                {
                    return true;
                }

                // Check if any other combatant (that isn't the one moving) is on this tile.
                foreach (var combatantId in Combatants)
                {
                    if (combatantId == pathfindingEntityId) continue;

                    var posComp = _componentStore.GetComponent<LocalPositionComponent>(combatantId);
                    if (posComp != null && posComp.LocalPosition == position)
                    {
                        return false; // Occupied
                    }
                }
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

        public int GetMovementEnergyCost(MoveAction action, bool isLocalMove = false)
        {
            if (isLocalMove || !action.IsRunning)
            {
                return 0;
            }

            if (action.IsRunning)
            {
                var mapData = GetMapDataAt((int)action.Destination.X, (int)action.Destination.Y);
                return GetTerrainEnergyCost(mapData.TerrainHeight);
            }

            return 0;
        }

        public float GetSecondsPassedDuringMovement(StatsComponent stats, bool isRunning, MapData mapData, Vector2 moveDirection, bool isLocalMove = false)
        {
            float secondsPassed = 0;
            float timeMultiplier = 1.0f;

            if (moveDirection.X != 0 && moveDirection.Y != 0)
            {
                timeMultiplier = 1.5f;
            }

            float baseTime = (Global.FeetPerWorldTile * Global.SECONDS_PER_FOOT_SCALING_FACTOR) / (isRunning ? stats.RunSpeed : stats.WalkSpeed);

            if (isLocalMove)
            {
                secondsPassed += baseTime / Global.LOCAL_GRID_SIZE;
            }
            else
            {
                secondsPassed += baseTime;
                secondsPassed += mapData.TerrainHeight switch
                {
                    var height when height < _global.FlatlandsLevel => 0,
                    var height when height < _global.HillsLevel => secondsPassed * 0.5f,
                    var height when height < _global.MountainsLevel => secondsPassed + 300,
                    _ => 0
                };
            }

            return (float)Math.Ceiling(secondsPassed * timeMultiplier);
        }

        public (int finalEnergy, bool possible, float secondsPassed) SimulateActionQueueEnergy(IEnumerable<IAction> customQueue = null)
        {
            var queueToSimulate = customQueue ?? PendingActions;
            if (!queueToSimulate.Any()) return (PlayerStats.CurrentEnergyPoints, true, 0);

            bool isLocalMove = CurrentMapView == MapView.Local;
            int finalEnergy = PlayerStats.CurrentEnergyPoints;
            int maxEnergy = PlayerStats.MaxEnergyPoints;
            float secondsPassed = 0f;
            Vector2 lastPosition = isLocalMove ? PlayerLocalPos : PlayerWorldPos;
            bool isFirstMoveInQueue = true;
            bool localRunCostApplied = false;

            foreach (var action in queueToSimulate)
            {
                if (action is MoveAction moveAction)
                {
                    Vector2 moveDirection = moveAction.Destination - lastPosition;
                    MapData mapData = isLocalMove ? default : GetMapDataAt((int)moveAction.Destination.X, (int)moveAction.Destination.Y);

                    float moveDuration = GetSecondsPassedDuringMovement(PlayerStats, moveAction.IsRunning, mapData, moveDirection, isLocalMove);

                    if (!isLocalMove && isFirstMoveInQueue)
                    {
                        float scaleFactor = GetFirstMoveTimeScaleFactor(moveDirection);
                        moveDuration *= scaleFactor;
                    }

                    secondsPassed += moveDuration;
                    int cost = GetMovementEnergyCost(moveAction, isLocalMove);

                    if (isLocalMove && moveAction.IsRunning && !localRunCostApplied)
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

        public List<Vector2> GetAffordablePath(int entityId, Vector2 start, Vector2 end, bool isRunning, out float totalTimeCost)
        {
            totalTimeCost = 0f;
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            var turnStats = _componentStore.GetComponent<TurnStatsComponent>(entityId);
            if (stats == null || turnStats == null)
            {
                return null;
            }

            float timeBudget = COMBAT_TURN_DURATION_SECONDS - turnStats.MovementTimeUsedThisTurn;
            if (timeBudget <= 0)
            {
                return new List<Vector2>();
            }

            var fullPath = Pathfinder.FindPath(entityId, start, end, this, isRunning, PathfindingMode.Time, MapView.Local);
            if (fullPath == null || !fullPath.Any())
            {
                return null;
            }

            var affordablePath = new List<Vector2>();
            var lastPos = start;

            foreach (var step in fullPath)
            {
                var moveDirection = step - lastPos;
                float stepCost = GetSecondsPassedDuringMovement(stats, isRunning, default, moveDirection, true);

                if (totalTimeCost + stepCost <= timeBudget)
                {
                    totalTimeCost += stepCost;
                    affordablePath.Add(step);
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
    }
}
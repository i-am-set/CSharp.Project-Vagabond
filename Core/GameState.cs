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
        World
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
        private bool _isPaused = false;
        private readonly Random _random = new Random();
        private static readonly Queue<IAction> _emptyActionQueue = new Queue<IAction>();

        // Combat Initiation State
        public bool IsCombatInitiationPending { get; private set; } = false;
        public List<int> PendingCombatants { get; private set; } = new List<int>();


        public MapView PathExecutionMapView { get; private set; }

        public int PlayerEntityId { get; private set; }
        public Vector2 PlayerWorldPos
        {
            get
            {
                var posComp = _componentStore.GetComponent<PositionComponent>(PlayerEntityId);
                return posComp != null ? posComp.WorldPosition : Vector2.Zero;
            }
        }
        public Queue<IAction> PendingActions
        {
            get
            {
                var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(PlayerEntityId);
                return actionQueueComp?.ActionQueue ?? _emptyActionQueue;
            }
        }
        public bool IsExecutingActions => _isExecutingActions;
        public bool IsPaused => _isPaused;
        public NoiseMapManager NoiseManager => _noiseManager;
        public StatsComponent PlayerStats => _componentStore.GetComponent<StatsComponent>(PlayerEntityId);
        public (int finalEnergy, bool possible, float secondsPassed) PendingQueueSimulationResult => SimulateActionQueueEnergy();
        public List<int> ActiveEntities { get; private set; } = new List<int>();
        public int InitialActionCount { get; private set; }
        public bool IsActionQueueDirty { get; set; } = true;

        // Combat State
        public bool IsInCombat { get; private set; } = false;
        public List<int> Combatants { get; private set; } = new List<int>();
        public List<int> InitiativeOrder { get; private set; } = new List<int>();
        public int CurrentTurnEntityId { get; private set; }
        public int? SelectedTargetId { get; set; } = null;
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
            PlayerEntityId = Spawner.Spawn("player", worldPosition: new Vector2(0, 0));
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

        public void AddEntityToCombat(int entityId)
        {
            // Logic to add an entity to an ongoing combat will be implemented later.
        }

        public void EndCombat()
        {
            if (!IsInCombat) return;

            IsInCombat = false;
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

        public bool CanPlayerMoveInCombat()
        {
            return false; // Placeholder until combat is refactored
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
            if (toggle)
            {
                InitialActionCount = PendingActions.Count;
                PathExecutionMapView = MapView.World;
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

        public bool IsPositionPassable(Vector2 position, MapView view, int pathfindingEntityId, Vector2 targetDestination, out MapData mapData)
        {
            mapData = default;

            // 1. Terrain Check
            mapData = GetMapDataAt((int)position.X, (int)position.Y);
            if (!(mapData.TerrainHeight >= _global.WaterLevel && mapData.TerrainHeight < _global.MountainsLevel))
                return false;

            // 2. Entity Blocking Check
            if (position == targetDestination)
            {
                return true;
            }

            if (IsTileOccupied(position, pathfindingEntityId))
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

        public bool IsTileOccupied(Vector2 position, int askingEntityId)
        {
            var entitiesToCheck = IsInCombat ? Combatants : ActiveEntities;
            foreach (var entityId in entitiesToCheck)
            {
                if (entityId == askingEntityId) continue;

                var worldPosComp = _componentStore.GetComponent<PositionComponent>(entityId);
                if (worldPosComp != null && worldPosComp.WorldPosition == position)
                {
                    return true; // Blocked by another entity.
                }
            }
            return false;
        }

        public int GetMovementEnergyCost(MoveAction action)
        {
            if (action.Mode != MovementMode.Run)
            {
                return 0; // Walking and Jogging are free.
            }

            var mapData = GetMapDataAt((int)action.Destination.X, (int)action.Destination.Y);
            return GetTerrainEnergyCost(mapData.TerrainHeight); // Running cost is based on terrain.
        }

        public float GetSecondsPassedDuringMovement(StatsComponent stats, MovementMode mode, MapData mapData, Vector2 moveDirection)
        {
            float distanceInFeet = Global.FEET_PER_WORLD_TILE;
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
            secondsPassed += mapData.TerrainHeight switch
            {
                var height when height < _global.FlatlandsLevel => 0,
                var height when height < _global.HillsLevel => secondsPassed * 0.5f,
                var height when height < _global.MountainsLevel => secondsPassed + 300,
                _ => 0
            };

            return secondsPassed;
        }

        public (int finalEnergy, bool possible, float secondsPassed) SimulateActionQueueEnergy(IEnumerable<IAction> customQueue = null)
        {
            var playerStats = PlayerStats;
            if (playerStats == null) return (0, true, 0f); // Prevent crash if stats aren't loaded yet

            var queueToSimulate = customQueue ?? PendingActions;
            if (!queueToSimulate.Any()) return (playerStats.CurrentEnergyPoints, true, 0f);

            int finalEnergy = playerStats.CurrentEnergyPoints;
            int maxEnergy = playerStats.MaxEnergyPoints;
            float secondsPassed = 0f;
            Vector2 lastPosition = PlayerWorldPos;

            foreach (var action in queueToSimulate)
            {
                if (action is MoveAction moveAction)
                {
                    MovementMode actualMode = moveAction.Mode;
                    int energyCost = GetMovementEnergyCost(moveAction);
                    if (moveAction.Mode == MovementMode.Run && finalEnergy < energyCost)
                    {
                        actualMode = MovementMode.Jog;
                    }

                    if (finalEnergy < energyCost)
                    {
                        return (finalEnergy, false, secondsPassed);
                    }

                    Vector2 moveDirection = moveAction.Destination - lastPosition;
                    MapData mapData = GetMapDataAt((int)moveAction.Destination.X, (int)moveAction.Destination.Y);
                    float moveDuration = GetSecondsPassedDuringMovement(playerStats, actualMode, mapData, moveDirection);

                    secondsPassed += moveDuration;

                    int actualEnergyCost = GetMovementEnergyCost(new MoveAction(moveAction.ActorId, moveAction.Destination, actualMode));
                    finalEnergy -= actualEnergyCost;

                    lastPosition = moveAction.Destination;
                }
                else if (action is RestAction restAction)
                {
                    switch (restAction.RestType)
                    {
                        case RestType.ShortRest:
                            finalEnergy += playerStats.ShortRestEnergyRestored;
                            secondsPassed += playerStats.ShortRestDuration * 60;
                            break;
                        case RestType.LongRest:
                            finalEnergy += playerStats.LongRestEnergyRestored;
                            secondsPassed += playerStats.LongRestDuration * 60;
                            break;
                        case RestType.FullRest:
                            finalEnergy += playerStats.FullRestEnergyRestored;
                            secondsPassed += playerStats.FullRestDuration * 60;
                            break;
                    }
                    finalEnergy = Math.Min(finalEnergy, maxEnergy);
                    lastPosition = restAction.Position;
                }
            }
            return (finalEnergy, true, secondsPassed);
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
                _worldClockManager.CancelInterpolation();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = interrupted ? "[cancel]Action queue interrupted." : "[cancel]Action queue cancelled." });
                return true;
            }
            return false;
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

        public int? GetEntityIdAtGridPos(Vector2 gridPos)
        {
            foreach (var entityId in ActiveEntities)
            {
                var worldPosComp = _componentStore.GetComponent<PositionComponent>(entityId);
                if (worldPosComp != null && (int)worldPosComp.WorldPosition.X == (int)gridPos.X && (int)worldPosComp.WorldPosition.Y == (int)gridPos.Y)
                {
                    return entityId;
                }
            }
            return null;
        }
    }
}
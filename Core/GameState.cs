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
        private readonly ChunkManager _chunkManager;
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;

        // Lazyloaded System Dependencies
        private ActionExecutionSystem _actionExecutionSystem;

        private bool _isExecutingActions = false;
        private bool _isPaused = false;
        private readonly Random _random = new Random();
        private static readonly Queue<IAction> _emptyActionQueue = new Queue<IAction>();

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
        public (int finalEnergy, bool possible, int ticksPassed) PendingQueueSimulationResult => SimulateActionQueue();
        public List<int> ActiveEntities { get; private set; } = new List<int>();
        public int InitialActionCount { get; private set; }
        public bool IsActionQueueDirty { get; set; } = true;

        public bool IsInCombat { get; private set; } = false;
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public GameState(NoiseMapManager noiseManager, ComponentStore componentStore, ChunkManager chunkManager, Global global, SpriteManager spriteManager)
        {
            _noiseManager = noiseManager;
            _componentStore = componentStore;
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
            // Initialize textures for all entities that have a RenderableComponent but no texture assigned yet.
            // This is a robust way to handle the player, NPCs, POIs, and any other future renderable entity type.
            var allEntitiesWithRenderable = _componentStore.GetAllEntitiesWithComponent<RenderableComponent>().ToList();
            foreach (var entityId in allEntitiesWithRenderable)
            {
                var renderable = _componentStore.GetComponent<RenderableComponent>(entityId);

                // If a texture is already assigned (e.g., by a more specific system), leave it alone.
                if (renderable.Texture != null)
                {
                    continue;
                }

                if (entityId == PlayerEntityId)
                {
                    renderable.Texture = _spriteManager.PlayerSprite;
                }
                else
                {
                    // For any other entity (NPCs, POIs, etc.), assign a default 1x1 pixel.
                    // The color property, loaded from the archetype JSON, will give it its appearance.
                    renderable.Texture = ServiceLocator.Get<Texture2D>();
                }
            }
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
            var entitiesToCheck = ActiveEntities;
            foreach (var entityId in entitiesToCheck)
            {
                if (entityId == askingEntityId) continue;

                var worldPosComp = _componentStore.GetComponent<PositionComponent>(entityId);
                if (worldPosComp != null && worldPosComp.WorldPosition == position)
                {
                    // An entity is on this tile. Check if it's a blocking entity.
                    // Blocking entities are mobile characters (Player or NPCs). POIs without these tags are passable.
                    if (_componentStore.HasComponent<PlayerTagComponent>(entityId) || _componentStore.HasComponent<NPCTagComponent>(entityId))
                    {
                        return true; // It's a character, so the tile is occupied.
                    }
                }
            }
            return false; // No blocking entities found.
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

        public int GetMovementTickCost(MovementMode mode, MapData mapData)
        {
            int ticks = mode switch
            {
                MovementMode.Walk => 2,
                MovementMode.Jog => 1,
                MovementMode.Run => 1,
                _ => 2
            };

            // Apply world map terrain penalties
            ticks += mapData.TerrainHeight switch
            {
                var height when height < _global.FlatlandsLevel => 0, // Flatlands are the baseline
                var height when height < _global.HillsLevel => 1,     // Hills cost 1 extra tick
                var height when height < _global.MountainsLevel => 2, // Mountains cost 2 extra ticks
                _ => 99 // Impassable, but should be caught earlier
            };

            return ticks;
        }

        public (int finalEnergy, bool possible, int ticksPassed) SimulateActionQueue(IEnumerable<IAction> customQueue = null)
        {
            var playerStats = PlayerStats;
            if (playerStats == null) return (0, true, 0); // Prevent crash if stats aren't loaded yet

            var queueToSimulate = customQueue ?? PendingActions;
            if (!queueToSimulate.Any()) return (playerStats.CurrentEnergyPoints, true, 0);

            int finalEnergy = playerStats.CurrentEnergyPoints;
            int maxEnergy = playerStats.MaxEnergyPoints;
            int ticksPassed = 0;

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
                        return (finalEnergy, false, ticksPassed);
                    }

                    MapData mapData = GetMapDataAt((int)moveAction.Destination.X, (int)moveAction.Destination.Y);
                    int moveDuration = GetMovementTickCost(actualMode, mapData);

                    ticksPassed += moveDuration;

                    int actualEnergyCost = GetMovementEnergyCost(new MoveAction(moveAction.ActorId, moveAction.Destination, actualMode));
                    finalEnergy -= actualEnergyCost;
                }
                else if (action is RestAction restAction)
                {
                    switch (restAction.RestType)
                    {
                        case RestType.ShortRest:
                            finalEnergy += playerStats.ShortRestEnergyRestored;
                            ticksPassed += 1;
                            break;
                        case RestType.LongRest:
                            finalEnergy += playerStats.LongRestEnergyRestored;
                            ticksPassed += 5;
                            break;
                        case RestType.FullRest:
                            finalEnergy += playerStats.FullRestEnergyRestored;
                            ticksPassed += 10;
                            break;
                    }
                    finalEnergy = Math.Min(finalEnergy, maxEnergy);
                }
            }
            return (finalEnergy, true, ticksPassed);
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

        public List<int> GetEntitiesAtGridPos(Vector2 gridPos)
        {
            var entitiesOnTile = new List<int>();
            var entitiesToCheck = ActiveEntities;
            foreach (var entityId in entitiesToCheck)
            {
                var worldPosComp = _componentStore.GetComponent<PositionComponent>(entityId);
                if (worldPosComp != null && worldPosComp.WorldPosition == gridPos)
                {
                    entitiesOnTile.Add(entityId);
                }
            }
            return entitiesOnTile;
        }
    }
}
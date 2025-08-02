using Microsoft.Xna.Framework;
using ProjectVagabond.Encounters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A system responsible for the procedural generation and management of Points of Interest (POIs) in the world.
    /// </summary>
    public class POIManagerSystem : ISystem
    {
        /// <summary>
        /// A private data structure to track information about destroyed, non-persistent POIs for respawning.
        /// Nesting it inside the class makes it a private implementation detail.
        /// </summary>
        private struct DestroyedPOIData
        {
            public string ArchetypeId;
            public Vector2 Position;
            public double TimeDestroyed; // Using TotalSeconds from TimeSpan for tracking
        }

        private GameState _gameState;
        private ComponentStore _componentStore;
        private WorldClockManager _worldClockManager;
        private readonly Random _random = new();

        private const int POI_COUNT = 10;
        private const int SPAWN_RADIUS = 100;
        private const float POI_RESPAWN_DURATION_SECONDS = 60 * 30; // 30 in-game minutes
        private const float UPDATE_INTERVAL = 1.0f; // Check for updates once per real-world second
        private float _updateAccumulator = 0f;

        private readonly List<DestroyedPOIData> _destroyedPOIs = new();

        public POIManagerSystem() { }

        /// <summary>
        /// Procedurally spawns a set number of POIs in the world around the player's starting position.
        /// This should be called once during world initialization.
        /// </summary>
        public void GeneratePOIs()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            var playerStartPos = _gameState.PlayerWorldPos;

            int spawnedCount = 0;
            int attempts = 0;
            while (spawnedCount < POI_COUNT && attempts < POI_COUNT * 5)
            {
                attempts++;
                int x = _random.Next((int)playerStartPos.X - SPAWN_RADIUS, (int)playerStartPos.X + SPAWN_RADIUS);
                int y = _random.Next((int)playerStartPos.Y - SPAWN_RADIUS, (int)playerStartPos.Y + SPAWN_RADIUS);
                var spawnPos = new Vector2(x, y);

                if (spawnPos != playerStartPos && _gameState.IsPositionPassable(spawnPos, MapView.World) && !_gameState.IsTileOccupied(spawnPos, -1))
                {
                    // Alternate between the two test POI types
                    string archetype = (spawnedCount % 2 == 0) ? "poi_test" : "poi_timed_delete";
                    Spawner.Spawn(archetype, spawnPos);
                    spawnedCount++;
                }
            }
        }

        /// <summary>
        /// Called by other systems to register a non-persistent POI that has been destroyed,
        /// so it can be considered for respawning.
        /// </summary>
        public void TrackDestroyedPOI(int entityId, Vector2 position)
        {
            _componentStore ??= ServiceLocator.Get<ComponentStore>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();

            var archetypeIdComp = _componentStore.GetComponent<ArchetypeIdComponent>(entityId);
            if (archetypeIdComp == null) return;

            _destroyedPOIs.Add(new DestroyedPOIData
            {
                ArchetypeId = archetypeIdComp.ArchetypeId,
                Position = position,
                TimeDestroyed = _worldClockManager.CurrentTimeSpan.TotalSeconds
            });
        }

        public void Update(GameTime gameTime)
        {
            _updateAccumulator += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_updateAccumulator < UPDATE_INTERVAL)
            {
                return;
            }

            float deltaTime = _updateAccumulator;
            _updateAccumulator = 0f;

            ProcessRespawns();
            ProcessTimedEvents(deltaTime);
        }

        private void ProcessRespawns()
        {
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();
            _gameState ??= ServiceLocator.Get<GameState>();

            double currentTime = _worldClockManager.CurrentTimeSpan.TotalSeconds;

            for (int i = _destroyedPOIs.Count - 1; i >= 0; i--)
            {
                var poi = _destroyedPOIs[i];
                if (currentTime - poi.TimeDestroyed >= POI_RESPAWN_DURATION_SECONDS)
                {
                    // Check if the tile is clear before respawning
                    if (!_gameState.IsTileOccupied(poi.Position, -1))
                    {
                        Spawner.Spawn(poi.ArchetypeId, poi.Position);
                        _destroyedPOIs.RemoveAt(i);
                    }
                }
            }
        }

        private void ProcessTimedEvents(float deltaTime)
        {
            _componentStore ??= ServiceLocator.Get<ComponentStore>();
            var poiEntities = _componentStore.GetAllEntitiesWithComponent<POIComponent>().ToList();

            foreach (var entityId in poiEntities)
            {
                var poiComp = _componentStore.GetComponent<POIComponent>(entityId);
                if (poiComp?.TimedEvent == null) continue;

                poiComp.TimedEventTimer -= deltaTime * Global.GAME_SECONDS_PER_REAL_SECOND;

                if (poiComp.TimedEventTimer <= 0)
                {
                    ExecuteTimedEvent(entityId, poiComp);
                    poiComp.TimedEventTimer = poiComp.TimedEvent.Duration; // Reset timer
                }
            }
        }

        private void ExecuteTimedEvent(int entityId, POIComponent poiComp)
        {
            var entityManager = ServiceLocator.Get<EntityManager>();
            var chunkManager = ServiceLocator.Get<ChunkManager>();

            switch (poiComp.TimedEvent.EventType.ToLowerInvariant())
            {
                case "delete":
                    var posComp = _componentStore.GetComponent<PositionComponent>(entityId);
                    if (posComp != null)
                    {
                        chunkManager.UnregisterEntity(entityId, posComp.WorldPosition);
                    }
                    _componentStore.EntityDestroyed(entityId);
                    entityManager.DestroyEntity(entityId);
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[dim]A timed event occurred: an object was removed from the world." });
                    break;

                case "move":
                    // Placeholder for move logic
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[dim]A timed event occurred: an object would have moved." });
                    break;

                case "level_up":
                    // Placeholder for level up logic
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[dim]A timed event occurred: an object would have leveled up." });
                    break;

                default:
                    Console.WriteLine($"[WARNING] POIManagerSystem: Unknown timed event type '{poiComp.TimedEvent.EventType}' for entity {entityId}.");
                    break;
            }
        }
    }
}
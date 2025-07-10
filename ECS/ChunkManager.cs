using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the collection of all chunks in the world, providing methods
    /// to register, unregister, and query entities based on their spatial location.
    /// </summary>
    public class ChunkManager
    {
        private readonly Dictionary<Point, Chunk> _chunks = new Dictionary<Point, Chunk>();
        private readonly ComponentStore _componentStore;

        public ChunkManager()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        /// <summary>
        /// A static helper method that converts a Vector2 world position into a Point chunk coordinate.
        /// </summary>
        /// <param name="worldPosition">The world position vector.</param>
        /// <returns>The corresponding chunk coordinate point.</returns>
        public static Point WorldToChunkCoords(Vector2 worldPosition)
        {
            return new Point((int)worldPosition.X, (int)worldPosition.Y);
        }

        /// <summary>
        /// Registers an entity with a chunk based on its world position.
        /// </summary>
        /// <param name="entityId">The ID of the entity to register.</param>
        /// <param name="worldPosition">The entity's current world position.</param>
        public void RegisterEntity(int entityId, Vector2 worldPosition)
        {
            Point chunkCoords = WorldToChunkCoords(worldPosition);

            if (!_chunks.TryGetValue(chunkCoords, out var chunk))
            {
                chunk = new Chunk(chunkCoords);
                _chunks[chunkCoords] = chunk;
            }

            chunk.EntityIds.Add(entityId);

            var posComponent = _componentStore.GetComponent<PositionComponent>(entityId);
            if (posComponent != null)
            {
                posComponent.CurrentChunk = chunkCoords;
            }
        }

        /// <summary>
        /// Removes an entity from its chunk.
        /// </summary>
        /// <param name="entityId">The ID of the entity to unregister.</param>
        /// <param name="worldPosition">The entity's current world position.</param>
        public void UnregisterEntity(int entityId, Vector2 worldPosition)
        {
            Point chunkCoords = WorldToChunkCoords(worldPosition);
            if (_chunks.TryGetValue(chunkCoords, out var chunk))
            {
                chunk.EntityIds.Remove(entityId);
            }
        }

        /// <summary>
        /// Moves an entity from its old chunk to a new one if its position has changed chunks.
        /// </summary>
        /// <param name="entityId">The ID of the entity to update.</param>
        /// <param name="oldPosition">The entity's previous world position.</param>
        /// <param name="newPosition">The entity's new world position.</param>
        public void UpdateEntityChunk(int entityId, Vector2 oldPosition, Vector2 newPosition)
        {
            Point oldChunkCoords = WorldToChunkCoords(oldPosition);
            Point newChunkCoords = WorldToChunkCoords(newPosition);

            if (oldChunkCoords != newChunkCoords)
            {
                UnregisterEntity(entityId, oldPosition);
                RegisterEntity(entityId, newPosition);
            }
        }

        /// <summary>
        /// Retrieves a list of all entity IDs within a specific chunk.
        /// </summary>
        /// <param name="chunkCoords">The coordinates of the chunk to query.</param>
        /// <returns>A list of entity IDs, or an empty list if the chunk doesn't exist.</returns>
        public List<int> GetEntitiesInChunk(Point chunkCoords)
        {
            if (_chunks.TryGetValue(chunkCoords, out var chunk))
            {
                return chunk.EntityIds.ToList();
            }
            return new List<int>();
        }

        /// <summary>
        /// Gets all entities relevant to the player, which for now is the player's current chunk.
        /// </summary>
        /// <param name="centralChunkCoords">The coordinates of the central chunk (usually the player's).</param>
        /// <returns>A list of entity IDs in the specified chunk.</returns>
        public List<int> GetEntitiesInTetherRange(Point centralChunkCoords)
        {
            // TODO: In the future, this could be expanded to include entities from neighboring chunks
            // or other "artificially tethered" entities based on game logic.
            // For now, it only includes entities in the player's current chunk.
            return GetEntitiesInChunk(centralChunkCoords);
        }
    }
}
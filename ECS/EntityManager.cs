using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the creation and destruction of entity IDs.
    /// It recycles destroyed IDs to keep the number of active IDs manageable.
    /// </summary>
    public class EntityManager
    {
        // A queue of previously used entity IDs that are now available.
        private readonly Queue<int> _availableIds = new();

        // The next available ID to be assigned if no recycled IDs are available.
        private int _nextId = 0;

        /// <summary>
        /// Creates a new, unique entity ID.
        /// If an old ID has been recycled, it will be reused. Otherwise, a new ID is generated.
        /// </summary>
        /// <returns>A unique integer representing the new entity.</returns>
        public int CreateEntity()
        {
            if (_availableIds.Count > 0)
            {
                return _availableIds.Dequeue();
            }
            return _nextId++;
        }

        /// <summary>
        /// Marks an entity ID as available for reuse.
        /// This should be called when an entity is permanently removed from the game.
        /// </summary>
        /// <param name="entityId">The ID of the entity to destroy.</param>
        public void DestroyEntity(int entityId)
        {
            // To prevent re-adding and potential issues, you might want to add a check
            // to ensure the ID isn't already in the queue, though it's not strictly required
            // if the DestroyEntity logic is only called once per entity lifetime.
            _availableIds.Enqueue(entityId);
        }
    }
}
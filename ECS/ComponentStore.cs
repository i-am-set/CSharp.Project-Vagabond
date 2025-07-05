using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A centralized storage for all components of all entities.
    /// Components are stored in a dictionary keyed by their type, allowing for
    /// efficient lookups.
    /// </summary>
    public class ComponentStore
    {
        /// <summary>
        /// The main data structure for storing components.
        /// Outer Dictionary Key: The Type of the component (e.g., typeof(PositionComponent)).
        /// Inner Dictionary Key: The entity ID (int).
        /// Inner Dictionary Value: The component instance (IComponent).
        /// </summary>
        private readonly Dictionary<Type, Dictionary<int, IComponent>> _components = new();

        /// <summary>
        /// Adds a component to an entity.
        /// </summary>
        /// <typeparam name="T">The type of the component to add.</typeparam>
        /// <param name="entityId">The ID of the entity.</param>
        /// <param name="component">The component instance to add.</param>
        public void AddComponent<T>(int entityId, T component) where T : IComponent
        {
            var componentType = typeof(T);
            if (!_components.ContainsKey(componentType))
            {
                _components[componentType] = new Dictionary<int, IComponent>();
            }
            _components[componentType][entityId] = component;
        }

        /// <summary>
        /// Retrieves a component of a specific type for a given entity.
        /// </summary>
        /// <typeparam name="T">The type of the component to retrieve.</typeparam>
        /// <param name="entityId">The ID of the entity.</param>
        /// <returns>The component instance, or default(T) if not found.</returns>
        public T GetComponent<T>(int entityId) where T : IComponent
        {
            if (_components.TryGetValue(typeof(T), out var componentMap))
            {
                if (componentMap.TryGetValue(entityId, out var component))
                {
                    return (T)component;
                }
            }
            return default;
        }

        /// <summary>
        /// Checks if an entity has a component of a specific type.
        /// </summary>
        /// <typeparam name="T">The type of the component to check for.</typeparam>
        /// <param name="entityId">The ID of the entity.</param>
        /// <returns>True if the entity has the component, otherwise false.</returns>
        public bool HasComponent<T>(int entityId) where T : IComponent
        {
            if (_components.TryGetValue(typeof(T), out var componentMap))
            {
                return componentMap.ContainsKey(entityId);
            }
            return false;
        }

        /// <summary>
        /// Removes a component of a specific type from an entity.
        /// </summary>
        /// <typeparam name="T">The type of the component to remove.</typeparam>
        /// <param name="entityId">The ID of the entity.</param>
        public void RemoveComponent<T>(int entityId) where T : IComponent
        {
            if (_components.TryGetValue(typeof(T), out var componentMap))
            {
                componentMap.Remove(entityId);
            }
        }

        /// <summary>
        /// Removes all components associated with a specific entity ID.
        /// This is a crucial cleanup step to be called when an entity is destroyed.
        /// </summary>
        /// <param name="entityId">The ID of the entity being destroyed.</param>
        public void EntityDestroyed(int entityId)
        {
            foreach (var componentMap in _components.Values)
            {
                componentMap.Remove(entityId);
            }
        }

        /// <summary>
        /// Gets an enumerable collection of all entity IDs that have a specific component.
        /// </summary>
        /// <typeparam name="T">The type of the component to query for.</typeparam>
        /// <returns>An enumerable of entity IDs.</returns>
        public IEnumerable<int> GetAllEntitiesWithComponent<T>() where T : IComponent
        {
            if (_components.TryGetValue(typeof(T), out var componentMap))
            {
                return componentMap.Keys;
            }
            return Enumerable.Empty<int>();
        }
    }
}
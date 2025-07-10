using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// A static helper class for getting user-friendly, unique names for entities.
    /// </summary>
    public static class EntityNamer
    {
        /// <summary>
        /// Gets the base display name for a single entity.
        /// </summary>
        /// <param name="entityId">The ID of the entity.</param>
        /// <returns>The entity's display name (e.g., "Player", "Bandit").</returns>
        public static string GetName(int entityId)
        {
            var gameState = ServiceLocator.Get<GameState>();
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();

            if (entityId == gameState.PlayerEntityId)
            {
                return "Player";
            }

            var archetypeIdComp = componentStore.GetComponent<ArchetypeIdComponent>(entityId);
            var archetype = archetypeManager.GetArchetype(archetypeIdComp?.ArchetypeId ?? "Unknown");
            return archetype?.Name ?? $"Entity {entityId}";
        }

        /// <summary>
        /// Generates a dictionary of unique names for a collection of entities,
        /// handling duplicates by appending numbers.
        /// </summary>
        /// <param name="entityIds">An enumerable of entity IDs.</param>
        /// <returns>A dictionary mapping each entity ID to its unique display name.</returns>
        public static Dictionary<int, string> GetUniqueNames(IEnumerable<int> entityIds)
        {
            var displayNames = new Dictionary<int, string>();
            var nameCounts = new Dictionary<string, int>();
            var playerEntityId = ServiceLocator.Get<GameState>().PlayerEntityId;

            foreach (var entityId in entityIds)
            {
                string baseName = GetName(entityId);

                // The player's name is always unique and doesn't get a number.
                if (entityId == playerEntityId)
                {
                    displayNames[entityId] = baseName;
                    continue;
                }

                // Count occurrences of non-player names
                if (nameCounts.TryGetValue(baseName, out int count))
                {
                    nameCounts[baseName] = count + 1;
                }
                else
                {
                    nameCounts[baseName] = 1;
                }

                // Append the count to make the name unique if there's more than one
                if (nameCounts[baseName] > 1)
                {
                    displayNames[entityId] = $"{baseName} {nameCounts[baseName]}";
                }
                else
                {
                    displayNames[entityId] = baseName;
                }
            }

            // A second pass is needed to correctly name the first instance of a duplicated name
            var finalNames = new Dictionary<int, string>();
            var finalNameCounts = new Dictionary<string, int>();
            foreach (var entityId in entityIds)
            {
                string baseName = GetName(entityId);
                if (entityId == playerEntityId)
                {
                    finalNames[entityId] = baseName;
                    continue;
                }

                if (nameCounts.ContainsKey(baseName) && nameCounts[baseName] > 1)
                {
                    if (finalNameCounts.TryGetValue(baseName, out int count))
                    {
                        finalNameCounts[baseName] = count + 1;
                    }
                    else
                    {
                        finalNameCounts[baseName] = 1;
                    }
                    finalNames[entityId] = $"{baseName} {finalNameCounts[baseName]}";
                }
                else
                {
                    finalNames[entityId] = baseName;
                }
            }


            return finalNames;
        }
    }
}
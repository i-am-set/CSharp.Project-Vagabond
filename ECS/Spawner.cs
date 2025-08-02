using Microsoft.Xna.Framework;
using System;
using System.Reflection;
using System.Text.Json;

namespace ProjectVagabond
{
    /// <summary>
    /// A static helper class responsible for creating entities from archetypes.
    /// </summary>
    public static class Spawner
    {
        /// <summary>
        /// Spawns a new entity based on a specified archetype at a given position.
        /// This method uses a fast cloning mechanism and avoids runtime reflection for property setting.
        /// </summary>
        /// <param name="archetypeId">The ID of the archetype to spawn (e.g., "player", "wanderer_npc").</param>
        /// <param name="worldPosition">The world position where the entity should be spawned.</param>
        /// <returns>The entity ID of the newly spawned entity, or -1 if spawning fails.</returns>
        public static int Spawn(string archetypeId, Vector2 worldPosition)
        {
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            var entityManager = ServiceLocator.Get<EntityManager>();
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var chunkManager = ServiceLocator.Get<ChunkManager>();

            var template = archetypeManager.GetArchetypeTemplate(archetypeId);
            if (template == null)
            {
                Console.WriteLine($"[ERROR] Failed to spawn entity. Archetype template '{archetypeId}' not found.");
                return -1;
            }

            int entityId = entityManager.CreateEntity();

            foreach (var templateComponent in template.TemplateComponents)
            {
                try
                {
                    // Clone the component from the pre-baked template
                    IComponent clonedComponent = ((ICloneableComponent)templateComponent).Clone();
                    Type componentType = clonedComponent.GetType();

                    // Add the fully populated component to the store
                    // We use reflection to call the generic AddComponent method, which is acceptable and necessary.
                    MethodInfo addComponentMethod = typeof(ComponentStore).GetMethod("AddComponent").MakeGenericMethod(componentType);
                    addComponentMethod.Invoke(componentStore, new object[] { entityId, clonedComponent });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to clone or add component for archetype '{archetypeId}': {ex.Message}");
                }
            }

            // After all components are added, set the specific spawn positions
            var posComp = componentStore.GetComponent<PositionComponent>(entityId);
            if (posComp != null)
            {
                posComp.WorldPosition = worldPosition;
            }

            // Register the new entity with the spatial partitioning system
            chunkManager.RegisterEntity(entityId, worldPosition);

            return entityId;
        }
    }
}
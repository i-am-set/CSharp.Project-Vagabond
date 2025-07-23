using Microsoft.Xna.Framework;
using ProjectVagabond.Particles; // Added using directive
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
        /// <param name="localPosition">The local position where the entity should be spawned within its world chunk.</param>
        /// <returns>The entity ID of the newly spawned entity, or -1 if spawning fails.</returns>
        public static int Spawn(string archetypeId, Vector2 worldPosition, Vector2 localPosition)
        {
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            var entityManager = ServiceLocator.Get<EntityManager>();
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var chunkManager = ServiceLocator.Get<ChunkManager>();
            var particleManager = ServiceLocator.Get<ParticleSystemManager>(); // Get the particle manager

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

            var localPosComp = componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (localPosComp != null)
            {
                localPosComp.LocalPosition = localPosition;
            }

            // --- NEW: Add Particle Emitter Component ---
            // For now, we add it to any entity that can move (has a position).
            // This could be made more specific later (e.g., based on a tag in the archetype).
            if (posComp != null && localPosComp != null)
            {
                var emitterComp = new ParticleEmitterComponent();
                var dirtSpraySettings = ParticleEffects.CreateDirtSpray();
                var dirtSprayEmitter = particleManager.CreateEmitter(dirtSpraySettings);
                emitterComp.Emitters["DirtSpray"] = dirtSprayEmitter;
                componentStore.AddComponent(entityId, emitterComp);
            }
            // --- END NEW ---

            // Register the new entity with the spatial partitioning system
            chunkManager.RegisterEntity(entityId, worldPosition);

            return entityId;
        }
    }
}
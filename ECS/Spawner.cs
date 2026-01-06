using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ProjectVagabond
{
    /// <summary>
    /// A static helper class responsible for creating entities from archetypes.
    /// </summary>
    public static class Spawner
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Spawns a new entity based on a specified archetype.
        /// </summary>
        /// <param name="archetypeId">The ID of the archetype to spawn.</param>
        /// <param name="worldPosition">Legacy parameter, ignored.</param>
        /// <returns>The entity ID of the newly spawned entity, or -1 if spawning fails.</returns>
        public static int Spawn(string archetypeId, Vector2 worldPosition)
        {
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            var entityManager = ServiceLocator.Get<EntityManager>();
            var componentStore = ServiceLocator.Get<ComponentStore>();

            var template = archetypeManager.GetArchetypeTemplate(archetypeId);
            if (template == null)
            {
                Console.WriteLine($"[ERROR] Failed to spawn entity. Archetype template '{archetypeId}' not found.");
                return -1;
            }

            int entityId = entityManager.CreateEntity();

            componentStore.EntityDestroyed(entityId);

            foreach (var templateComponent in template.TemplateComponents)
            {
                try
                {
                    // Clone the component from the pre-baked template
                    IComponent clonedComponent = ((ICloneableComponent)templateComponent).Clone();
                    Type componentType = clonedComponent.GetType();

                    MethodInfo addComponentMethod = typeof(ComponentStore).GetMethod("AddComponent").MakeGenericMethod(componentType);
                    addComponentMethod.Invoke(componentStore, new object[] { entityId, clonedComponent });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to clone or add component for archetype '{archetypeId}': {ex.Message}");
                }
            }

            // After all template components are added, check for special profile components to generate live stats.
            if (componentStore.GetComponent<EnemyStatProfileComponent>(entityId) is EnemyStatProfileComponent profile)
            {
                var liveStats = GenerateStatsFromProfile(profile);
                componentStore.AddComponent(entityId, liveStats);
            }

            return entityId;
        }

        /// <summary>
        /// Generates a live CombatantStatsComponent from an enemy's stat profile.
        /// </summary>
        private static CombatantStatsComponent GenerateStatsFromProfile(EnemyStatProfileComponent profile)
        {
            var liveStats = new CombatantStatsComponent();

            liveStats.MaxHP = _random.Next(profile.MinHP, profile.MaxHP + 1);
            liveStats.CurrentHP = liveStats.MaxHP;
            liveStats.MaxMana = profile.MaxMana;
            liveStats.CurrentMana = profile.MaxMana;
            liveStats.Strength = _random.Next(profile.MinStrength, profile.MaxStrength + 1);
            liveStats.Intelligence = _random.Next(profile.MinIntelligence, profile.MaxIntelligence + 1);
            liveStats.Tenacity = _random.Next(profile.MinTenacity, profile.MaxTenacity + 1);
            liveStats.Agility = _random.Next(profile.MinAgility, profile.MaxAgility + 1);
            liveStats.WeaknessElementIDs = new List<int>(profile.WeaknessElementIDs);
            liveStats.ResistanceElementIDs = new List<int>(profile.ResistanceElementIDs);

            // Randomly select moves from the learnset
            if (profile.MoveLearnset.Any() && profile.MaxNumberOfMoves > 0)
            {
                int numMoves = _random.Next(profile.MinNumberOfMoves, profile.MaxNumberOfMoves + 1);
                var shuffledMoves = profile.MoveLearnset.OrderBy(x => _random.Next()).ToList();
                liveStats.AvailableMoveIDs = shuffledMoves.Take(numMoves).ToList();
            }

            return liveStats;
        }
    }
}
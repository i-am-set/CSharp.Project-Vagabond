using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ProjectVagabond
{
    public static class Spawner
    {
        private static readonly Random _random = new Random();

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

            if (componentStore.GetComponent<EnemyStatProfileComponent>(entityId) is EnemyStatProfileComponent profile)
            {
                var liveStats = GenerateStatsFromProfile(profile);
                componentStore.AddComponent(entityId, liveStats);
            }

            return entityId;
        }

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

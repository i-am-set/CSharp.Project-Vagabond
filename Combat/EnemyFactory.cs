using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Responsible for creating and configuring combat entities based on a blueprint.
    /// </summary>
    public class EnemyFactory
    {
        private readonly ComponentStore _componentStore;
        private readonly Random _random = new Random();

        // The random range is inclusive of min and exclusive of max, so we add 1 to the variance.
        private const int STAT_VARIANCE_MODIFIER = 1;

        public EnemyFactory()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        /// <summary>
        /// Creates a list of combat-ready entities from an encounter blueprint.
        /// </summary>
        /// <param name="encounterData">The data defining the encounter.</param>
        /// <returns>A list of newly created and configured CombatEntity objects.</returns>
        public List<CombatEntity> CreateEnemies(CombatEncounterData encounterData)
        {
            var createdEnemies = new List<CombatEntity>();
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();

            foreach (var enemyDef in encounterData.Enemies)
            {
                // 1. Spawn the entity from its archetype
                int entityId = Spawner.Spawn(enemyDef.ArchetypeId, enemyDef.Position.ToVector2());
                if (entityId == -1) continue;

                var archetype = archetypeManager.GetArchetypeTemplate(enemyDef.ArchetypeId);
                Debug.WriteLine($"[EnemyFactory] Creating entity '{archetype.Name}' (ID: {entityId}) from archetype '{enemyDef.ArchetypeId}'.");


                // 2. Apply stat randomization
                var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);
                if (statsComp != null && enemyDef.StatVariances != null)
                {
                    foreach (var variance in enemyDef.StatVariances)
                    {
                        int mod = _random.Next(-variance.Value, variance.Value + STAT_VARIANCE_MODIFIER);
                        switch (variance.Key.ToLowerInvariant())
                        {
                            case "s": statsComp.Strength += mod; break;
                            case "a": statsComp.Agility += mod; break;
                            case "t": statsComp.Tenacity += mod; break;
                            case "i": statsComp.Intelligence += mod; break;
                            case "c": statsComp.Charm += mod; break;
                        }
                    }
                    statsComp.Initialize(); // Re-clamp and recalculate derived stats
                }

                // 3. Determine the correct texture for the visual CombatEntity
                var renderable = _componentStore.GetComponent<RenderableComponent>(entityId);
                Texture2D textureToUse = null;
                if (renderable != null && !string.IsNullOrEmpty(renderable.SpritePath))
                {
                    Debug.WriteLine($"[EnemyFactory] Requesting sprite for '{archetype.Name}' with path: '{renderable.SpritePath}'.");
                    textureToUse = spriteManager.GetEnemySprite(renderable.SpritePath);
                }

                // If no specific sprite was found, use the 1x1 pixel as a fallback.
                if (textureToUse == null)
                {
                    Debug.WriteLine($"[EnemyFactory] Sprite NOT found in manager for path '{renderable?.SpritePath ?? "NULL"}'. Using fallback pixel texture.");
                    textureToUse = ServiceLocator.Get<Texture2D>();
                }
                else
                {
                    Debug.WriteLine($"[EnemyFactory] Sprite found in manager. Assigning texture '{textureToUse.Name}'.");
                }

                // 4. Create the visual CombatEntity object
                var newEnemy = new CombatEntity(entityId, textureToUse);
                createdEnemies.Add(newEnemy);
            }

            return createdEnemies;
        }
    }
}
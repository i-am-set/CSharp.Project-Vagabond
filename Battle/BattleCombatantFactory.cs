using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// A static factory class responsible for creating BattleCombatant instances from overworld entity data.
    /// </summary>
    public static class BattleCombatantFactory
    {
        /// <summary>
        /// Creates a BattleCombatant instance from a given entity ID.
        /// </summary>
        /// <param name="entityId">The ID of the entity to convert.</param>
        /// <param name="combatantId">A unique ID for the combatant within the battle context.</param>
        /// <returns>A fully populated BattleCombatant object, or null if the entity is missing required components.</returns>
        public static BattleCombatant CreateFromEntity(int entityId, string combatantId)
        {
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            var gameState = ServiceLocator.Get<GameState>();
            // An entity must have stats to be a combatant.
            var statsComponent = componentStore.GetComponent<CombatantStatsComponent>(entityId);
            if (statsComponent == null)
            {
                Debug.WriteLine($"[BattleCombatantFactory] [ERROR] Entity {entityId} cannot be a combatant: Missing CombatantStatsComponent.");
                return null;
            }

            var archetypeIdComp = componentStore.GetComponent<ArchetypeIdComponent>(entityId);
            var archetype = archetypeManager.GetArchetypeTemplate(archetypeIdComp?.ArchetypeId);
            if (archetype == null)
            {
                Debug.WriteLine($"[BattleCombatantFactory] [ERROR] Could not find archetype for entity {entityId}.");
                return null;
            }

            var combatant = new BattleCombatant
            {
                EntityId = entityId,
                ArchetypeId = archetype.Id,
                CombatantID = combatantId,
                Name = archetype.Name,
                IsPlayerControlled = componentStore.HasComponent<PlayerTagComponent>(entityId),
                Stats = new CombatantStats
                {
                    Level = statsComponent.Level,
                    MaxHP = statsComponent.MaxHP,
                    CurrentHP = statsComponent.CurrentHP, // Use current health from component
                    MaxMana = statsComponent.MaxMana,
                    CurrentMana = statsComponent.CurrentMana, // Use current mana from component
                    Strength = statsComponent.Strength,
                    Intelligence = statsComponent.Intelligence,
                    Tenacity = statsComponent.Tenacity,
                    Agility = statsComponent.Agility
                },
                DefensiveElementIDs = new List<int>(statsComponent.DefensiveElementIDs),
                EscalationStacks = 0 // Ensure stacks are reset at the start of battle
            };

            // Initialize visual HP to be the same as logical HP at the start of battle.
            combatant.VisualHP = combatant.Stats.CurrentHP;

            // Populate passive abilities from the entity's component
            var abilitiesComponent = componentStore.GetComponent<PassiveAbilitiesComponent>(entityId);
            if (abilitiesComponent != null)
            {
                foreach (var abilityId in abilitiesComponent.AbilityIDs)
                {
                    if (BattleDataCache.Abilities.TryGetValue(abilityId, out var abilityData))
                    {
                        combatant.ActiveAbilities.Add(abilityData);
                    }
                    else
                    {
                        Debug.WriteLine($"[BattleCombatantFactory] [WARNING] AbilityID '{abilityId}' not found in cache for combatant '{combatant.Name}'.");
                    }
                }
            }

            if (combatant.IsPlayerControlled)
            {
                combatant.DefaultStrikeMoveID = gameState.PlayerState.DefaultStrikeMoveID;
                combatant.EquippedSpells = gameState.PlayerState.EquippedSpells;

                // Apply temporary buffs from narrative choices
                var tempBuffsComp = componentStore.GetComponent<TemporaryBuffsComponent>(entityId);
                if (tempBuffsComp != null)
                {
                    foreach (var buff in tempBuffsComp.Buffs)
                    {
                        // Apply with a long duration; they are managed by battle count, not turns.
                        combatant.AddStatusEffect(new StatusEffectInstance(buff.EffectType, 99));
                    }
                }
            }
            else
            {
                // For non-player combatants, populate their static move list from the archetype.
                var staticMoves = new List<MoveData>();
                foreach (var moveId in statsComponent.AvailableMoveIDs)
                {
                    if (BattleDataCache.Moves.TryGetValue(moveId, out var moveData))
                    {
                        staticMoves.Add(moveData);
                    }
                    else
                    {
                        Debug.WriteLine($"[BattleCombatantFactory] [WARNING] MoveID '{moveId}' not found in cache for combatant '{combatant.Name}'.");
                    }
                }
                combatant.SetStaticMoves(staticMoves);
            }

            return combatant;
        }
    }
}
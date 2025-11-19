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
        public static BattleCombatant CreateFromEntity(int entityId, string combatantId)
        {
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            var gameState = ServiceLocator.Get<GameState>();

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
                    CurrentHP = statsComponent.CurrentHP,
                    MaxMana = statsComponent.MaxMana,
                    CurrentMana = statsComponent.CurrentMana,
                    Strength = statsComponent.Strength,
                    Intelligence = statsComponent.Intelligence,
                    Tenacity = statsComponent.Tenacity,
                    Agility = statsComponent.Agility
                },
                DefensiveElementIDs = new List<int>(statsComponent.DefensiveElementIDs),
                EscalationStacks = 0
            };

            combatant.VisualHP = combatant.Stats.CurrentHP;

            if (combatant.IsPlayerControlled)
            {
                // Player setup from PlayerState
                combatant.DefaultStrikeMoveID = gameState.PlayerState.DefaultStrikeMoveID;
                combatant.EquippedSpells = gameState.PlayerState.EquippedSpells;

                // Load Passive Abilities from EQUIPPED relics only
                foreach (var relicId in gameState.PlayerState.EquippedRelics)
                {
                    if (!string.IsNullOrEmpty(relicId))
                    {
                        // Note: We assume RelicID matches AbilityID in data
                        if (BattleDataCache.Abilities.TryGetValue(relicId, out var abilityData))
                        {
                            combatant.ActiveAbilities.Add(abilityData);
                        }
                        else
                        {
                            Debug.WriteLine($"[BattleCombatantFactory] [WARNING] Relic/Ability ID '{relicId}' not found for player.");
                        }
                    }
                }

                // Apply temporary buffs
                var tempBuffsComp = componentStore.GetComponent<TemporaryBuffsComponent>(entityId);
                if (tempBuffsComp != null)
                {
                    foreach (var buff in tempBuffsComp.Buffs)
                    {
                        combatant.AddStatusEffect(new StatusEffectInstance(buff.EffectType, 99));
                    }
                }
            }
            else
            {
                // Enemy setup from component
                var staticMoves = new List<MoveData>();
                foreach (var moveId in statsComponent.AvailableMoveIDs)
                {
                    if (BattleDataCache.Moves.TryGetValue(moveId, out var moveData))
                    {
                        staticMoves.Add(moveData);
                    }
                }
                combatant.SetStaticMoves(staticMoves);

                // Load passive abilities from component (Enemies don't have inventory)
                var abilitiesComponent = componentStore.GetComponent<PassiveAbilitiesComponent>(entityId);
                if (abilitiesComponent != null)
                {
                    foreach (var abilityId in abilitiesComponent.AbilityIDs)
                    {
                        if (BattleDataCache.Abilities.TryGetValue(abilityId, out var abilityData))
                        {
                            combatant.ActiveAbilities.Add(abilityData);
                        }
                    }
                }
            }

            return combatant;
        }
    }
}
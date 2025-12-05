using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ProjectVagabond.Battle;

namespace ProjectVagabond.Battle
{
    public static class BattleCombatantFactory
    {
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
                EscalationStacks = 0,
                IsPlayerControlled = componentStore.HasComponent<PlayerTagComponent>(entityId)
            };

            combatant.VisualHP = combatant.Stats.CurrentHP;

            if (combatant.IsPlayerControlled)
            {
                // 1. Set Default Strike Move
                // If a weapon is equipped, use its move. Otherwise use the base default.
                if (!string.IsNullOrEmpty(gameState.PlayerState.EquippedWeaponId) &&
                    BattleDataCache.Weapons.TryGetValue(gameState.PlayerState.EquippedWeaponId, out var weaponData))
                {
                    combatant.DefaultStrikeMoveID = weaponData.MoveID;

                    // 2. Apply Weapon Passives
                    // We create a temporary RelicData object to represent the weapon's effects in combat.
                    // This allows the existing SecondaryEffectSystem to process weapon effects seamlessly.
                    var weaponAsRelic = new RelicData
                    {
                        RelicID = weaponData.WeaponID,
                        RelicName = weaponData.WeaponName,
                        AbilityName = "Weapon Ability", // Generic name or add specific field to WeaponData later
                        Effects = weaponData.Effects,
                        StatModifiers = weaponData.StatModifiers
                    };
                    combatant.ActiveRelics.Add(weaponAsRelic);
                }
                else
                {
                    combatant.DefaultStrikeMoveID = gameState.PlayerState.DefaultStrikeMoveID;
                }

                // 3. Apply Armor Passives
                if (!string.IsNullOrEmpty(gameState.PlayerState.EquippedArmorId) &&
                    BattleDataCache.Armors.TryGetValue(gameState.PlayerState.EquippedArmorId, out var armorData))
                {
                    // Only create a passive effect entry if the armor actually has effects or a named ability
                    if (armorData.Effects != null && armorData.Effects.Count > 0)
                    {
                        var armorAsRelic = new RelicData
                        {
                            RelicID = armorData.ArmorID,
                            RelicName = armorData.ArmorName,
                            AbilityName = armorData.AbilityName ?? "Armor Ability",
                            Effects = armorData.Effects,
                            StatModifiers = armorData.StatModifiers
                        };
                        combatant.ActiveRelics.Add(armorAsRelic);
                    }
                }

                combatant.EquippedSpells = gameState.PlayerState.EquippedSpells;

                // Apply Effective Stats from PlayerState (Base + Relic + Weapon + Armor Modifiers)
                // We overwrite the stats loaded from the component with the calculated effective stats.
                combatant.Stats.MaxHP = gameState.PlayerState.GetEffectiveStat("MaxHP");
                combatant.Stats.MaxMana = gameState.PlayerState.GetEffectiveStat("MaxMana");
                combatant.Stats.Strength = gameState.PlayerState.GetEffectiveStat("Strength");
                combatant.Stats.Intelligence = gameState.PlayerState.GetEffectiveStat("Intelligence");
                combatant.Stats.Tenacity = gameState.PlayerState.GetEffectiveStat("Tenacity");
                combatant.Stats.Agility = gameState.PlayerState.GetEffectiveStat("Agility");

                // Load Passive Abilities from EQUIPPED relics only
                foreach (var relicId in gameState.PlayerState.EquippedRelics)
                {
                    if (!string.IsNullOrEmpty(relicId))
                    {
                        if (BattleDataCache.Relics.TryGetValue(relicId, out var relicData))
                        {
                            combatant.ActiveRelics.Add(relicData);
                        }
                        else
                        {
                            Debug.WriteLine($"[BattleCombatantFactory] [WARNING] Relic ID '{relicId}' not found for player.");
                        }
                    }
                }

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
                    foreach (var relicId in abilitiesComponent.RelicIDs)
                    {
                        if (BattleDataCache.Relics.TryGetValue(relicId, out var relicData))
                        {
                            combatant.ActiveRelics.Add(relicData);
                        }
                    }
                }
            }

            return combatant;
        }
    }
}
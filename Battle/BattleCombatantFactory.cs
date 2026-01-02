using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle
{
    public static class BattleCombatantFactory
    {
        private static readonly Random _random = new Random();
        public static BattleCombatant CreateFromEntity(int entityId, string combatantId)
        {
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            var gameState = ServiceLocator.Get<GameState>();
            var global = ServiceLocator.Get<Global>();

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
                WeaknessElementIDs = new List<int>(statsComponent.WeaknessElementIDs),
                ResistanceElementIDs = new List<int>(statsComponent.ResistanceElementIDs),
                IsPlayerControlled = componentStore.HasComponent<PlayerTagComponent>(entityId)
            };

            combatant.VisualHP = combatant.Stats.CurrentHP;

            if (combatant.IsPlayerControlled)
            {
                // Find the party member corresponding to this combatant
                var partyMember = gameState.PlayerState.Party.FirstOrDefault(m => m.Name == combatant.Name);
                if (partyMember == null && entityId == gameState.PlayerEntityId)
                {
                    partyMember = gameState.PlayerState.Leader;
                }

                if (partyMember != null)
                {
                    combatant.Name = partyMember.Name; // Sync name
                    combatant.Spells = partyMember.Spells;
                    combatant.PortraitIndex = partyMember.PortraitIndex;
                    combatant.EquippedWeaponId = partyMember.EquippedWeaponId;
                    combatant.EquippedArmorId = partyMember.EquippedArmorId;
                    combatant.EquippedRelicId = partyMember.EquippedRelicId;
                    combatant.DefaultStrikeMoveID = partyMember.DefaultStrikeMoveID;
                }

                // 1. Apply Armor Passives
                if (!string.IsNullOrEmpty(combatant.EquippedArmorId) &&
                    BattleDataCache.Armors.TryGetValue(combatant.EquippedArmorId, out var armorData))
                {
                    // Register Armor Abilities
                    var armorAbilities = AbilityFactory.CreateAbilitiesFromData(armorData.Effects, armorData.StatModifiers);
                    combatant.RegisterAbilities(armorAbilities);

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

                // 2. Apply Weapon Passives (Stats only, effects are on the move)
                if (!string.IsNullOrEmpty(combatant.EquippedWeaponId) &&
                    BattleDataCache.Weapons.TryGetValue(combatant.EquippedWeaponId, out var weaponData))
                {
                    // Only register stat modifiers from the weapon as global passives.
                    // The move-specific effects are handled by the MoveData generated in BattleCombatant.StrikeMove.
                    if (weaponData.StatModifiers != null && weaponData.StatModifiers.Count > 0)
                    {
                        var weaponStatAbilities = AbilityFactory.CreateAbilitiesFromData(null, weaponData.StatModifiers);
                        combatant.RegisterAbilities(weaponStatAbilities);
                    }

                    // Add to ActiveRelics for UI tooltip purposes
                    var weaponAsRelic = new RelicData
                    {
                        RelicID = weaponData.WeaponID,
                        RelicName = weaponData.WeaponName,
                        AbilityName = "Weapon Ability",
                        Effects = weaponData.Effects,
                        StatModifiers = weaponData.StatModifiers
                    };
                    combatant.ActiveRelics.Add(weaponAsRelic);
                }

                // Apply Effective Stats from PlayerState
                var statSource = partyMember ?? gameState.PlayerState.Leader;

                combatant.Stats.MaxHP = gameState.PlayerState.GetEffectiveStat(statSource, "MaxHP");
                combatant.Stats.MaxMana = gameState.PlayerState.GetEffectiveStat(statSource, "MaxMana");
                combatant.Stats.Strength = gameState.PlayerState.GetEffectiveStat(statSource, "Strength");
                combatant.Stats.Intelligence = gameState.PlayerState.GetEffectiveStat(statSource, "Intelligence");
                combatant.Stats.Tenacity = gameState.PlayerState.GetEffectiveStat(statSource, "Tenacity");
                combatant.Stats.Agility = gameState.PlayerState.GetEffectiveStat(statSource, "Agility");
                combatant.Stats.CurrentHP = statSource.CurrentHP;
                combatant.Stats.CurrentMana = statSource.CurrentMana;
                combatant.VisualHP = combatant.Stats.CurrentHP;

                // 3. Load Relic Abilities (Single Slot)
                if (!string.IsNullOrEmpty(combatant.EquippedRelicId))
                {
                    if (BattleDataCache.Relics.TryGetValue(combatant.EquippedRelicId, out var relicData))
                    {
                        // Register Relic Abilities
                        var relicAbilities = AbilityFactory.CreateAbilitiesFromData(relicData.Effects, relicData.StatModifiers);
                        combatant.RegisterAbilities(relicAbilities);

                        combatant.ActiveRelics.Add(relicData);
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
                // --- ENEMY LOGIC ---

                // 1. Calculate "Power Score" based on raw stats
                float powerScore = (combatant.Stats.MaxHP * 0.2f) +
                                   (combatant.Stats.Strength * 1.0f) +
                                   (combatant.Stats.Intelligence * 1.0f) +
                                   (combatant.Stats.Tenacity * 1.0f) +
                                   (combatant.Stats.Agility * 1.0f);

                // 2. Apply Economy Logic
                float calculatedValue = global.Economy_BaseDrop + (powerScore * global.Economy_GlobalScalar);

                // 3. Apply Random Variance (+/- 20%)
                float variance = (float)(_random.NextDouble() * (global.Economy_Variance * 2) - global.Economy_Variance);
                float finalValue = calculatedValue * (1.0f + variance);

                combatant.CoinReward = Math.Max(1, (int)Math.Round(finalValue));

                var staticMoves = new List<MoveData>();
                foreach (var moveId in statsComponent.AvailableMoveIDs)
                {
                    if (BattleDataCache.Moves.TryGetValue(moveId, out var moveData))
                    {
                        staticMoves.Add(moveData);
                    }
                }
                combatant.SetStaticMoves(staticMoves);

                // Load passive abilities from component (Enemies)
                var abilitiesComponent = componentStore.GetComponent<PassiveAbilitiesComponent>(entityId);
                if (abilitiesComponent != null)
                {
                    foreach (var relicId in abilitiesComponent.RelicIDs)
                    {
                        if (BattleDataCache.Relics.TryGetValue(relicId, out var relicData))
                        {
                            // Register Enemy Abilities
                            var enemyAbilities = AbilityFactory.CreateAbilitiesFromData(relicData.Effects, relicData.StatModifiers);
                            combatant.RegisterAbilities(enemyAbilities);

                            combatant.ActiveRelics.Add(relicData);
                        }
                    }
                }
            }

            return combatant;
        }
    }
}
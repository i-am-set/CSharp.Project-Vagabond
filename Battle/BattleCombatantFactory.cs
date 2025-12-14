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
                WeaknessElementIDs = new List<int>(statsComponent.WeaknessElementIDs),
                ResistanceElementIDs = new List<int>(statsComponent.ResistanceElementIDs),
                IsPlayerControlled = componentStore.HasComponent<PlayerTagComponent>(entityId)
            };

            combatant.VisualHP = combatant.Stats.CurrentHP;

            if (combatant.IsPlayerControlled)
            {
                // 1. Set Default Strike Move
                if (!string.IsNullOrEmpty(gameState.PlayerState.EquippedWeaponId) &&
                    BattleDataCache.Weapons.TryGetValue(gameState.PlayerState.EquippedWeaponId, out var weaponData))
                {
                    combatant.DefaultStrikeMoveID = weaponData.MoveID;

                    // Register Weapon Abilities
                    var weaponAbilities = AbilityFactory.CreateAbilitiesFromData(weaponData.Effects, weaponData.StatModifiers);
                    combatant.RegisterAbilities(weaponAbilities);

                    // Keep legacy list for UI tooltips
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
                else
                {
                    combatant.DefaultStrikeMoveID = gameState.PlayerState.DefaultStrikeMoveID;
                }

                // 2. Apply Armor Passives
                if (!string.IsNullOrEmpty(gameState.PlayerState.EquippedArmorId) &&
                    BattleDataCache.Armors.TryGetValue(gameState.PlayerState.EquippedArmorId, out var armorData))
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

                // Find the party member corresponding to this combatant to get their spells and portrait
                var partyMember = gameState.PlayerState.Party.FirstOrDefault(m => m.Name == combatant.Name);
                if (partyMember != null)
                {
                    combatant.Spells = partyMember.Spells;
                    combatant.PortraitIndex = partyMember.PortraitIndex; // Set Portrait Index
                }

                // Apply Effective Stats from PlayerState
                combatant.Stats.MaxHP = gameState.PlayerState.GetEffectiveStat("MaxHP");
                combatant.Stats.MaxMana = gameState.PlayerState.GetEffectiveStat("MaxMana");
                combatant.Stats.Strength = gameState.PlayerState.GetEffectiveStat("Strength");
                combatant.Stats.Intelligence = gameState.PlayerState.GetEffectiveStat("Intelligence");
                combatant.Stats.Tenacity = gameState.PlayerState.GetEffectiveStat("Tenacity");
                combatant.Stats.Agility = gameState.PlayerState.GetEffectiveStat("Agility");
                combatant.Stats.CurrentHP = gameState.PlayerState.Leader.CurrentHP;
                combatant.Stats.CurrentMana = gameState.PlayerState.Leader.CurrentMana;
                combatant.VisualHP = combatant.Stats.CurrentHP;

                // 3. Load Relic Abilities (Single Slot)
                if (!string.IsNullOrEmpty(gameState.PlayerState.EquippedRelicId))
                {
                    if (BattleDataCache.Relics.TryGetValue(gameState.PlayerState.EquippedRelicId, out var relicData))
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
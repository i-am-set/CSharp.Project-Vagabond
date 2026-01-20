using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
                // Priority 1: Match by Name (if unique)
                var partyMember = gameState.PlayerState.Party.FirstOrDefault(m => m.Name == combatant.Name);

                // Priority 2: If this is the Player Entity (ID 0), it MUST be the Leader
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
                    combatant.EquippedRelicId = partyMember.EquippedRelicId;
                    combatant.DefaultStrikeMoveID = partyMember.DefaultStrikeMoveID;

                    // --- Populate Narration Data from PartyMemberData ---
                    var data = BattleDataCache.PartyMembers.Values.FirstOrDefault(p => p.Name == partyMember.Name);
                    if (data != null)
                    {
                        combatant.Gender = data.Gender;
                        combatant.IsProperNoun = data.IsProperNoun;
                    }

                    // --- 0. Apply Intrinsic Passive Abilities ---
                    if (partyMember.IntrinsicAbilities != null && partyMember.IntrinsicAbilities.Count > 0)
                    {
                        // Create abilities from the dictionary. Pass empty stat modifiers as intrinsics are currently effect-based.
                        var intrinsicAbilities = AbilityFactory.CreateAbilitiesFromData(partyMember.IntrinsicAbilities, new Dictionary<string, int>());
                        combatant.RegisterAbilities(intrinsicAbilities);
                        Debug.WriteLine($"[BattleCombatantFactory] Registered intrinsics for {combatant.Name}: {string.Join(", ", partyMember.IntrinsicAbilities.Keys)}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[BattleCombatantFactory] [WARNING] Could not find PartyMember for player entity {entityId} ({combatant.Name}). Intrinsics skipped.");
                }

                // 1. Apply Weapon Passives (Stats only, effects are on the move)
                if (!string.IsNullOrEmpty(combatant.EquippedWeaponId) &&
                    BattleDataCache.Weapons.TryGetValue(combatant.EquippedWeaponId, out var weaponData))
                {
                    if (weaponData.StatModifiers != null && weaponData.StatModifiers.Count > 0)
                    {
                        var weaponStatAbilities = AbilityFactory.CreateAbilitiesFromData(null, weaponData.StatModifiers);
                        combatant.RegisterAbilities(weaponStatAbilities);
                    }

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

                // 2. Load Relic Abilities (Single Slot)
                if (!string.IsNullOrEmpty(combatant.EquippedRelicId))
                {
                    if (BattleDataCache.Relics.TryGetValue(combatant.EquippedRelicId, out var relicData))
                    {
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

                var profile = archetype.TemplateComponents.OfType<EnemyStatProfileComponent>().FirstOrDefault();
                if (profile != null)
                {
                    combatant.Gender = profile.Gender;
                    combatant.IsProperNoun = profile.IsProperNoun;
                }

                float powerScore = (combatant.Stats.MaxHP * 0.2f) +
                                   (combatant.Stats.Strength * 1.0f) +
                                   (combatant.Stats.Intelligence * 1.0f) +
                                   (combatant.Stats.Tenacity * 1.0f) +
                                   (combatant.Stats.Agility * 1.0f);

                float calculatedValue = global.Economy_BaseDrop + (powerScore * global.Economy_GlobalScalar);
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

                var abilitiesComponent = componentStore.GetComponent<PassiveAbilitiesComponent>(entityId);
                if (abilitiesComponent != null)
                {
                    foreach (var relicId in abilitiesComponent.RelicIDs)
                    {
                        if (BattleDataCache.Relics.TryGetValue(relicId, out var relicData))
                        {
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
using Microsoft.Xna.Framework;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
            if (statsComponent == null) return null;

            var archetypeIdComp = componentStore.GetComponent<ArchetypeIdComponent>(entityId);
            var archetype = archetypeManager.GetArchetypeTemplate(archetypeIdComp?.ArchetypeId);
            if (archetype == null) return null;

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
                IsPlayerControlled = componentStore.HasComponent<PlayerTagComponent>(entityId)
            };

            // --- Initialize Tenacity Shield ---
            // Tenacity starts full at the beginning of combat.
            combatant.CurrentTenacity = combatant.Stats.Tenacity;

            combatant.VisualHP = combatant.Stats.CurrentHP;

            if (combatant.IsPlayerControlled)
            {
                var partyMember = gameState.PlayerState.Party.FirstOrDefault(m => m.Name == combatant.Name);
                if (partyMember == null && entityId == gameState.PlayerEntityId) partyMember = gameState.PlayerState.Leader;

                if (partyMember != null)
                {
                    combatant.Name = partyMember.Name;
                    combatant.Spells = partyMember.Spells;
                    combatant.PortraitIndex = partyMember.PortraitIndex;
                    combatant.DefaultStrikeMoveID = partyMember.DefaultStrikeMoveID;

                    var data = BattleDataCache.PartyMembers.Values.FirstOrDefault(p => p.Name == partyMember.Name);
                    if (data != null)
                    {
                        combatant.Gender = data.Gender;
                        combatant.IsProperNoun = data.IsProperNoun;
                    }

                    if (partyMember.IntrinsicAbilities != null && partyMember.IntrinsicAbilities.Count > 0)
                    {
                        var intrinsicAbilities = AbilityFactory.CreateAbilitiesFromData(partyMember.IntrinsicAbilities, new Dictionary<string, int>());
                        combatant.RegisterAbilities(intrinsicAbilities);
                    }
                }

                foreach (var relicId in gameState.PlayerState.GlobalRelics)
                {
                    if (BattleDataCache.Relics.TryGetValue(relicId, out var relicData))
                    {
                        var relicAbilities = AbilityFactory.CreateAbilitiesFromData(relicData.Effects, relicData.StatModifiers);
                        combatant.RegisterAbilities(relicAbilities);
                        combatant.ActiveRelics.Add(relicData);
                    }
                }

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

                // Re-sync Tenacity after applying relics/stats
                combatant.CurrentTenacity = combatant.Stats.Tenacity;

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
                var profile = archetype.TemplateComponents.OfType<EnemyStatProfileComponent>().FirstOrDefault();
                if (profile != null)
                {
                    combatant.Gender = profile.Gender;
                    combatant.IsProperNoun = profile.IsProperNoun;
                }

                float powerScore = (combatant.Stats.MaxHP * 0.2f) + (combatant.Stats.Strength * 1.0f) + (combatant.Stats.Intelligence * 1.0f) + (combatant.Stats.Tenacity * 1.0f) + (combatant.Stats.Agility * 1.0f);
                float calculatedValue = global.Economy_BaseDrop + (powerScore * global.Economy_GlobalScalar);
                float variance = (float)(_random.NextDouble() * (global.Economy_Variance * 2) - global.Economy_Variance);
                combatant.CoinReward = Math.Max(1, (int)Math.Round(calculatedValue * (1.0f + variance)));

                var staticMoves = new List<MoveData>();
                foreach (var moveId in statsComponent.AvailableMoveIDs)
                {
                    if (BattleDataCache.Moves.TryGetValue(moveId, out var moveData)) staticMoves.Add(moveData);
                }
                combatant.SetStaticMoves(staticMoves);
            }

            return combatant;
        }
    }
}
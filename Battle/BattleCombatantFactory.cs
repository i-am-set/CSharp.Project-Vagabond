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

        public static BattleCombatant CreatePlayer(PartyMember member, string combatantId)
        {
            var gameState = ServiceLocator.Get<GameState>();
            var global = ServiceLocator.Get<Global>();

            var combatant = new BattleCombatant
            {
                CombatantID = combatantId,
                ArchetypeId = "player", // Visuals use player master sheet
                Name = member.Name,
                IsPlayerControlled = true,
                Stats = new CombatantStats
                {
                    MaxHP = gameState.PlayerState.GetEffectiveStat(member, "MaxHP"),
                    CurrentHP = member.CurrentHP,
                    MaxMana = gameState.PlayerState.GetEffectiveStat(member, "MaxMana"),
                    CurrentMana = member.CurrentMana,
                    Strength = gameState.PlayerState.GetEffectiveStat(member, "Strength"),
                    Intelligence = gameState.PlayerState.GetEffectiveStat(member, "Intelligence"),
                    Tenacity = gameState.PlayerState.GetEffectiveStat(member, "Tenacity"),
                    Agility = gameState.PlayerState.GetEffectiveStat(member, "Agility")
                },
                Spells = member.Spells,
                PortraitIndex = member.PortraitIndex,
                DefaultStrikeMoveID = member.DefaultStrikeMoveID
            };

            // Initialize Tenacity Shield
            combatant.CurrentTenacity = combatant.Stats.Tenacity;
            combatant.VisualHP = combatant.Stats.CurrentHP;

            // Load Metadata
            var data = BattleDataCache.PartyMembers.Values.FirstOrDefault(p => p.Name == member.Name);
            if (data != null)
            {
                combatant.Gender = data.Gender;
                combatant.IsProperNoun = data.IsProperNoun;
            }

            // Intrinsic Abilities
            if (member.IntrinsicAbilities != null && member.IntrinsicAbilities.Count > 0)
            {
                var intrinsicAbilities = AbilityFactory.CreateAbilitiesFromData(member.IntrinsicAbilities, new Dictionary<string, int>());
                combatant.RegisterAbilities(intrinsicAbilities);
            }

            // Global Relics
            foreach (var relicId in gameState.PlayerState.GlobalRelics)
            {
                if (BattleDataCache.Relics.TryGetValue(relicId, out var relicData))
                {
                    var relicAbilities = AbilityFactory.CreateAbilitiesFromData(relicData.Effects, relicData.StatModifiers);
                    combatant.RegisterAbilities(relicAbilities);
                    combatant.ActiveRelics.Add(relicData);
                }
            }

            // Temporary Buffs
            foreach (var buff in member.ActiveBuffs)
            {
                combatant.AddStatusEffect(new StatusEffectInstance(buff.EffectType, 99));
            }

            return combatant;
        }

        public static BattleCombatant CreateEnemy(string archetypeId, string combatantId)
        {
            var dataManager = ServiceLocator.Get<DataManager>();
            var global = ServiceLocator.Get<Global>();

            var enemyData = dataManager.GetEnemyData(archetypeId);
            if (enemyData == null)
            {
                Debug.WriteLine($"[BattleCombatantFactory] ERROR: Enemy data for '{archetypeId}' not found.");
                return null;
            }

            var combatant = new BattleCombatant
            {
                CombatantID = combatantId,
                ArchetypeId = enemyData.Id,
                Name = enemyData.Name,
                IsPlayerControlled = false,
                Gender = enemyData.Gender,
                IsProperNoun = enemyData.IsProperNoun,
                Stats = new CombatantStats()
            };

            // Roll Stats
            combatant.Stats.MaxHP = _random.Next(enemyData.MinHP, enemyData.MaxHP + 1);
            combatant.Stats.CurrentHP = combatant.Stats.MaxHP;
            combatant.Stats.MaxMana = enemyData.MaxMana;
            combatant.Stats.CurrentMana = enemyData.MaxMana;
            combatant.Stats.Strength = _random.Next(enemyData.MinStrength, enemyData.MaxStrength + 1);
            combatant.Stats.Intelligence = _random.Next(enemyData.MinIntelligence, enemyData.MaxIntelligence + 1);
            combatant.Stats.Tenacity = _random.Next(enemyData.MinTenacity, enemyData.MaxTenacity + 1);
            combatant.Stats.Agility = _random.Next(enemyData.MinAgility, enemyData.MaxAgility + 1);

            combatant.CurrentTenacity = combatant.Stats.Tenacity;
            combatant.VisualHP = combatant.Stats.CurrentHP;

            // Moves
            var staticMoves = new List<MoveData>();
            if (enemyData.MoveLearnset.Any() && enemyData.MaxNumberOfMoves > 0)
            {
                int numMoves = _random.Next(enemyData.MinNumberOfMoves, enemyData.MaxNumberOfMoves + 1);
                var shuffledMoves = enemyData.MoveLearnset.OrderBy(x => _random.Next()).ToList();
                var selectedIds = shuffledMoves.Take(numMoves).ToList();

                foreach (var moveId in selectedIds)
                {
                    if (BattleDataCache.Moves.TryGetValue(moveId, out var moveData))
                        staticMoves.Add(moveData);
                }
            }
            combatant.SetStaticMoves(staticMoves);

            // Passive Relics (Abilities)
            foreach (var relicId in enemyData.PassiveRelicIDs)
            {
                if (BattleDataCache.Relics.TryGetValue(relicId, out var relicData))
                {
                    var abilities = AbilityFactory.CreateAbilitiesFromData(relicData.Effects, relicData.StatModifiers);
                    combatant.RegisterAbilities(abilities);
                    combatant.ActiveRelics.Add(relicData);
                }
            }

            // Coin Reward Calculation
            float powerScore = (combatant.Stats.MaxHP * 0.2f) + (combatant.Stats.Strength * 1.0f) + (combatant.Stats.Intelligence * 1.0f) + (combatant.Stats.Tenacity * 1.0f) + (combatant.Stats.Agility * 1.0f);
            float calculatedValue = global.Economy_BaseDrop + (powerScore * global.Economy_GlobalScalar);
            float variance = (float)(_random.NextDouble() * (global.Economy_Variance * 2) - global.Economy_Variance);
            combatant.CoinReward = Math.Max(1, (int)Math.Round(calculatedValue * (1.0f + variance)));

            return combatant;
        }
    }
}
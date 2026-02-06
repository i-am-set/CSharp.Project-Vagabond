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

            var combatant = new BattleCombatant
            {
                CombatantID = combatantId,
                ArchetypeId = "player",
                Name = member.Name,
                IsPlayerControlled = true,
                Stats = new CombatantStats
                {
                    MaxHP = gameState.PlayerState.GetEffectiveStat(member, "MaxHP"),
                    CurrentHP = member.CurrentHP,
                    Strength = gameState.PlayerState.GetEffectiveStat(member, "Strength"),
                    Intelligence = gameState.PlayerState.GetEffectiveStat(member, "Intelligence"),
                    Tenacity = gameState.PlayerState.GetEffectiveStat(member, "Tenacity"),
                    Agility = gameState.PlayerState.GetEffectiveStat(member, "Agility")
                },
                BasicMove = member.BasicMove,
                CoreMove = member.CoreMove,
                AltMove = member.AltMove,
                PortraitIndex = member.PortraitIndex
            };

            combatant.Tags.Add("Type.Player");
            combatant.Tags.Add("Type.Ally");

            combatant.CurrentTenacity = combatant.Stats.Tenacity;
            combatant.VisualHP = combatant.Stats.CurrentHP;

            var data = BattleDataCache.PartyMembers.Values.FirstOrDefault(p => p.Name == member.Name);
            if (data != null)
            {
                combatant.Gender = data.Gender;
                combatant.IsProperNoun = data.IsProperNoun;

                combatant.Tags.Add($"Gender.{data.Gender}");
                if (data.IsProperNoun) combatant.Tags.Add("Prop.ProperNoun");
            }

            if (member.IntrinsicAbilities != null && member.IntrinsicAbilities.Count > 0)
            {
                var intrinsicAbilities = AbilityFactory.CreateAbilitiesFromData(null, member.IntrinsicAbilities, new Dictionary<string, int>());
                combatant.RegisterAbilities(intrinsicAbilities);
            }

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

            combatant.Tags.Add("Type.Enemy");
            combatant.Tags.Add($"Gender.{enemyData.Gender}");
            if (enemyData.IsProperNoun) combatant.Tags.Add("Prop.ProperNoun");

            combatant.Stats.MaxHP = _random.Next(enemyData.MinHP, enemyData.MaxHP + 1);
            combatant.Stats.CurrentHP = combatant.Stats.MaxHP;
            combatant.Stats.Strength = _random.Next(enemyData.MinStrength, enemyData.MaxStrength + 1);
            combatant.Stats.Intelligence = _random.Next(enemyData.MinIntelligence, enemyData.MaxIntelligence + 1);
            combatant.Stats.Tenacity = _random.Next(enemyData.MinTenacity, enemyData.MaxTenacity + 1);
            combatant.Stats.Agility = _random.Next(enemyData.MinAgility, enemyData.MaxAgility + 1);

            combatant.CurrentTenacity = combatant.Stats.Tenacity;
            combatant.VisualHP = combatant.Stats.CurrentHP;

            // Assign Basic Move
            if (enemyData.BasicMoves != null && enemyData.BasicMoves.Any())
            {
                string moveId = enemyData.BasicMoves[_random.Next(enemyData.BasicMoves.Count)];
                if (BattleDataCache.Moves.ContainsKey(moveId))
                {
                    combatant.BasicMove = new MoveEntry(moveId, 0);
                }
            }
            else if (BattleDataCache.Moves.ContainsKey("6")) // Fallback to stall if no basic move defined
            {
                combatant.BasicMove = new MoveEntry("6", 0);
            }

            // Assign Core Move
            if (enemyData.CoreMoves != null && enemyData.CoreMoves.Any())
            {
                string moveId = enemyData.CoreMoves[_random.Next(enemyData.CoreMoves.Count)];
                if (BattleDataCache.Moves.ContainsKey(moveId))
                {
                    combatant.CoreMove = new MoveEntry(moveId, 0);
                }
            }

            // Assign Alt Move
            if (enemyData.AltMoves != null && enemyData.AltMoves.Any())
            {
                string moveId = enemyData.AltMoves[_random.Next(enemyData.AltMoves.Count)];
                if (BattleDataCache.Moves.ContainsKey(moveId))
                {
                    combatant.AltMove = new MoveEntry(moveId, 0);
                }
            }

            float powerScore = (combatant.Stats.MaxHP * 0.2f) + (combatant.Stats.Strength * 1.0f) + (combatant.Stats.Intelligence * 1.0f) + (combatant.Stats.Tenacity * 1.0f) + (combatant.Stats.Agility * 1.0f);
            float calculatedValue = global.Economy_BaseDrop + (powerScore * global.Economy_GlobalScalar);
            float variance = (float)(_random.NextDouble() * (global.Economy_Variance * 2) - global.Economy_Variance);
            combatant.CoinReward = Math.Max(1, (int)Math.Round(calculatedValue * (1.0f + variance)));

            return combatant;
        }
    }
}
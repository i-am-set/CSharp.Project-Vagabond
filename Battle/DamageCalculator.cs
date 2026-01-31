using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    public static class DamageCalculator
    {
        private static readonly Random _random = new Random();
        private const float GLOBAL_DAMAGE_SCALAR = 0.125f;
        private const int FLAT_DAMAGE_BONUS = 1;
        private const float BASELINE_DEFENSE_DIVISOR = 5.0f;

        public struct DamageResult
        {
            public int DamageAmount;
            public bool WasCritical;
            public bool WasGraze;
            public bool WasProtected;
            public bool WasVulnerable;
        }

        public static DamageResult CalculateDamage(QueuedAction action, BattleCombatant target, MoveData move, float multiTargetModifier = 1.0f, bool? overrideCrit = null, bool isSimulation = false)
        {
            var attacker = action.Actor;
            var result = new DamageResult
            {
                WasProtected = false,
                WasVulnerable = false
            };

            // 1. Accuracy Check
            if (move.Accuracy != -1)
            {
                var hitEvt = new CheckHitChanceEvent(attacker, target, move, move.Accuracy);
                attacker.NotifyAbilities(hitEvt);
                target.NotifyAbilities(hitEvt);
                foreach (var ab in move.Abilities) ab.OnEvent(hitEvt);

                if (_random.Next(1, 101) > hitEvt.FinalAccuracy) result.WasGraze = true;
            }

            // 2. Base Damage Calculation
            float offensiveStat = GetOffensiveStat(attacker, move.OffensiveStat);
            float defensiveStat = BASELINE_DEFENSE_DIVISOR;

            // Defense Penetration (Simulated via event if needed, but for now simplified)
            // If we had a CalculateDefenseEvent, we'd use it here.
            // Assuming standard defense for now.

            float statRatio = offensiveStat / defensiveStat;
            float baseDamage = (move.Power * statRatio * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;

            // 3. Critical Hit Check
            bool isCrit = false;
            if (!result.WasGraze)
            {
                // We don't have a CheckCritEvent in Phase 1, so we use standard logic.
                // If we added it, we'd use it here.
                isCrit = overrideCrit ?? (_random.NextDouble() < BattleConstants.CRITICAL_HIT_CHANCE);
                if (isCrit) result.WasCritical = true;
            }

            // 4. Calculate Damage Event
            var dmgEvt = new CalculateDamageEvent(attacker, target, move, baseDamage, isCrit, result.WasGraze);

            // Notify in order: Attacker -> Move -> Target
            attacker.NotifyAbilities(dmgEvt);
            foreach (var ab in move.Abilities) ab.OnEvent(dmgEvt);
            target.NotifyAbilities(dmgEvt);

            // 5. Apply Multipliers
            float currentDamage = (dmgEvt.FinalDamage + dmgEvt.FlatBonus) * dmgEvt.DamageMultiplier * multiTargetModifier;

            // Tenacity Vulnerability
            if (target.CurrentTenacity <= 0)
            {
                currentDamage *= 2.0f;
                result.WasVulnerable = true;
            }

            // Random Variance
            currentDamage *= (float)(_random.NextDouble() * (BattleConstants.RANDOM_VARIANCE_MAX - BattleConstants.RANDOM_VARIANCE_MIN) + BattleConstants.RANDOM_VARIANCE_MIN);

            if (result.WasGraze) currentDamage *= BattleConstants.GRAZE_MULTIPLIER;

            // Check for Protected Tag (UI Flag)
            if (target.Tags.Has("State.Protected") && dmgEvt.DamageMultiplier <= 0)
            {
                result.WasProtected = true;
            }

            int finalDamageAmount = (int)Math.Floor(currentDamage);
            if (currentDamage > 0 && finalDamageAmount == 0) finalDamageAmount = 1;
            if (finalDamageAmount < 0) finalDamageAmount = 0;

            result.DamageAmount = finalDamageAmount;
            return result;
        }

        public static int CalculateBaselineDamage(BattleCombatant attacker, BattleCombatant target, MoveData move)
        {
            // Simplified simulation
            if (move.Power == 0) return 0;

            float offensiveStat = GetOffensiveStat(attacker, move.OffensiveStat);
            float defensiveStat = BASELINE_DEFENSE_DIVISOR;
            float baseDamage = (move.Power * (offensiveStat / defensiveStat) * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;

            var dmgEvt = new CalculateDamageEvent(attacker, target, move, baseDamage, false, false);
            attacker.NotifyAbilities(dmgEvt);
            foreach (var ab in move.Abilities) ab.OnEvent(dmgEvt);
            target.NotifyAbilities(dmgEvt);

            float currentDamage = (dmgEvt.FinalDamage + dmgEvt.FlatBonus) * dmgEvt.DamageMultiplier;

            if (target.CurrentTenacity <= 0) currentDamage *= 2.0f;
            currentDamage *= BattleConstants.RANDOM_VARIANCE_MAX;

            int finalDamageAmount = (int)Math.Floor(currentDamage);
            if (currentDamage > 0 && finalDamageAmount == 0) finalDamageAmount = 1;
            return finalDamageAmount;
        }

        private static float GetOffensiveStat(BattleCombatant attacker, OffensiveStatType type)
        {
            // Use CalculateStatEvent to get effective stat
            float baseVal = type switch
            {
                OffensiveStatType.Strength => attacker.Stats.Strength,
                OffensiveStatType.Intelligence => attacker.Stats.Intelligence,
                OffensiveStatType.Tenacity => attacker.Stats.Tenacity,
                OffensiveStatType.Agility => attacker.Stats.Agility,
                _ => attacker.Stats.Strength
            };

            var evt = new CalculateStatEvent(attacker, type, baseVal);
            attacker.NotifyAbilities(evt);

            // Apply Stage Multiplier
            float multiplier = BattleConstants.StatStageMultipliers[attacker.StatStages[type]];
            return evt.FinalValue * multiplier;
        }
    }
}
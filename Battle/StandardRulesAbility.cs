using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle.Abilities
{
    public class StandardRulesAbility : IAbility
    {
        public string Name => "Standard Rules";
        public string Description => "Applies standard combat math.";
        public int Priority => 0; // Base logic

        private static readonly Random _random = new Random();
        private const float GLOBAL_DAMAGE_SCALAR = 0.125f;
        private const int FLAT_DAMAGE_BONUS = 1;
        private const float BASELINE_DEFENSE_DIVISOR = 5.0f;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CheckHitChanceEvent hitEvent)
            {
                // Standard Accuracy Logic
                hitEvent.FinalAccuracy = hitEvent.BaseAccuracy;
            }
            else if (e is CalculateDamageEvent dmgEvent)
            {
                // 1. Calculate Stats
                float offense = GetOffensiveStat(dmgEvent.Actor, dmgEvent.Move.OffensiveStat, context);
                float defense = BASELINE_DEFENSE_DIVISOR;

                // 2. Base Damage Formula
                float baseDamage = (dmgEvent.Move.Power * (offense / defense) * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;

                // 3. Apply Multipliers
                float multiplier = dmgEvent.DamageMultiplier;

                // Tenacity Vulnerability
                if (dmgEvent.Target.CurrentTenacity <= 0)
                {
                    multiplier *= 2.0f;
                    dmgEvent.WasVulnerable = true;
                }

                // Critical Hit
                if (dmgEvent.IsCritical)
                {
                    multiplier *= BattleConstants.CRITICAL_HIT_MULTIPLIER;
                }

                // Graze
                if (dmgEvent.IsGraze)
                {
                    multiplier *= BattleConstants.GRAZE_MULTIPLIER;
                }

                // Random Variance
                float variance = (float)(_random.NextDouble() * (BattleConstants.RANDOM_VARIANCE_MAX - BattleConstants.RANDOM_VARIANCE_MIN) + BattleConstants.RANDOM_VARIANCE_MIN);
                multiplier *= variance;

                // Protection Check
                if (dmgEvent.Target.Tags.Has(GameplayTags.States.Protected))
                {
                    multiplier = 0f;
                    dmgEvent.WasProtected = true;
                }

                // 4. Final Calculation
                float finalValue = (baseDamage + dmgEvent.FlatBonus) * multiplier;
                dmgEvent.FinalDamage = (int)Math.Floor(finalValue);

                // Minimum Damage Clamp
                if (finalValue > 0 && dmgEvent.FinalDamage == 0) dmgEvent.FinalDamage = 1;
                if (dmgEvent.FinalDamage < 0) dmgEvent.FinalDamage = 0;
            }
        }

        private float GetOffensiveStat(BattleCombatant attacker, OffensiveStatType type, BattleContext context)
        {
            float baseVal = type switch
            {
                OffensiveStatType.Strength => attacker.Stats.Strength,
                OffensiveStatType.Intelligence => attacker.Stats.Intelligence,
                OffensiveStatType.Tenacity => attacker.Stats.Tenacity,
                OffensiveStatType.Agility => attacker.Stats.Agility,
                _ => attacker.Stats.Strength
            };

            var evt = new CalculateStatEvent(attacker, type, baseVal);
            attacker.NotifyAbilities(evt, context);

            float multiplier = BattleConstants.StatStageMultipliers[attacker.StatStages[type]];
            return evt.FinalValue * multiplier;
        }
    }
}
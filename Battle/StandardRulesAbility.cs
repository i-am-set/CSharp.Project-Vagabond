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
                float baseDamage = 0f;

                // 3. Apply Multipliers
                float multiplier = dmgEvent.DamageMultiplier;

                // Prevent damage from none damaging moves
                if (dmgEvent.Move.Power > 0)
                {
                    baseDamage = (dmgEvent.Move.Power * (offense / defense) * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;
                }

                // --- Tenacity, Critical, and Graze Logic ---
                if (dmgEvent.Target.CurrentTenacity > 0)
                {
                    // TARGET HAS SHIELD
                    if (dmgEvent.IsCritical)
                    {
                        // Critical vs Shield:
                        // Pierce the shield (ignore SHIELDED_DAMAGE_MULT).
                        // Do NOT apply CRITICAL_HIT_MULTIPLIER.
                        // Result: Normal Damage (1.0x).
                    }
                    else
                    {
                        // Normal or Graze vs Shield:
                        // Apply Shield Penalty.
                        multiplier *= BattleConstants.SHIELDED_DAMAGE_MULT;

                        // Note: If it is a Graze, we do NOT apply the Graze multiplier (0.25f).
                        // The damage is simply capped at the Shielded rate (0.1f).
                    }
                }
                else
                {
                    // TARGET IS BROKEN (NO SHIELD)
                    // Base damage is Normal (1.0x).

                    if (dmgEvent.IsCritical)
                    {
                        // Critical vs Broken:
                        // Apply full Critical Multiplier (2.0x).
                        multiplier *= BattleConstants.CRITICAL_HIT_MULTIPLIER;
                    }

                    if (dmgEvent.IsGraze)
                    {
                        // Graze vs Broken:
                        // Apply normal Graze Multiplier (0.25x).
                        multiplier *= BattleConstants.GRAZE_MULTIPLIER;
                    }

                    dmgEvent.WasVulnerable = true;
                }

                // Random Variance
                if (context.IsSimulation)
                {
                    float variance = context.SimulationVariance switch
                    {
                        VarianceMode.Min => BattleConstants.RANDOM_VARIANCE_MIN,
                        VarianceMode.Max => BattleConstants.RANDOM_VARIANCE_MAX,
                        _ => (BattleConstants.RANDOM_VARIANCE_MIN + BattleConstants.RANDOM_VARIANCE_MAX) / 2.0f
                    };
                    multiplier *= variance;
                }
                else
                {
                    float variance = (float)(_random.NextDouble() * (BattleConstants.RANDOM_VARIANCE_MAX - BattleConstants.RANDOM_VARIANCE_MIN) + BattleConstants.RANDOM_VARIANCE_MIN);
                    multiplier *= variance;
                }

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
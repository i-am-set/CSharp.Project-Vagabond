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

        // --- TUNING CONSTANTS ---

        // Controls global damage output. Higher = faster battles.
        private const float GLOBAL_DAMAGE_SCALAR = 0.1f;

        // Ensures moves always do at least X damage (unless immune).
        private const int FLAT_DAMAGE_BONUS = 1;

        // How much the secondary stat (Str/Int) contributes to Defense.
        // 0.5f means 2 points of Strength = 1 point of extra Defense.
        private const float RESISTANCE_WEIGHT = 0.5f;

        /* 
         * --- DAMAGE FORMULA DOCUMENTATION ---
         * 
         * BaseDamage = (Power * (Offense / Defense) * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS
         * 
         * Where:
         *   Offense = Attacker's Strength (Physical) or Intelligence (Magical)
         *   
         *   Defense = Tenacity + (ResistanceStat * RESISTANCE_WEIGHT)
         *      -> ResistanceStat is Strength (vs Physical) or Intelligence (vs Magical)
         *      -> Defense is clamped to a minimum of 1.0f to prevent divide-by-zero.
         */

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
                float defense = CalculateDefense(dmgEvent.Target, dmgEvent.Move.ImpactType, context);

                // 2. Base Damage Formula
                float baseDamage = 0f;

                // Prevent damage from non-damaging moves
                if (dmgEvent.Move.Power > 0)
                {
                    baseDamage = (dmgEvent.Move.Power * (offense / defense) * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;
                }

                // 3. Apply Multipliers
                float multiplier = dmgEvent.DamageMultiplier;

                if (dmgEvent.IsCritical)
                {
                    multiplier *= BattleConstants.CRITICAL_HIT_MULTIPLIER;
                }

                if (dmgEvent.IsGraze)
                {
                    multiplier *= BattleConstants.GRAZE_MULTIPLIER;
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
                    float variance = (float)(new Random().NextDouble() * (BattleConstants.RANDOM_VARIANCE_MAX - BattleConstants.RANDOM_VARIANCE_MIN) + BattleConstants.RANDOM_VARIANCE_MIN);
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

        private float CalculateDefense(BattleCombatant target, ImpactType impactType, BattleContext context)
        {
            // Base tankiness comes from Tenacity.
            float tenacity = GetEffectiveStat(target, OffensiveStatType.Tenacity, context);

            // Determine which stat helps resist this specific attack type.
            float resistanceStat = 0f;

            if (impactType == ImpactType.Magical)
            {
                // Intelligence resists Magic (Mental warding/Willpower)
                resistanceStat = GetEffectiveStat(target, OffensiveStatType.Intelligence, context);
            }
            else
            {
                // Strength resists Physical (Muscle density/Armor)
                // Status moves that deal damage (rare) also default to Physical resistance here.
                resistanceStat = GetEffectiveStat(target, OffensiveStatType.Strength, context);
            }

            // Compound the stats.
            // We clamp to 1.0f to ensure we never divide by zero or have negative defense.
            return Math.Max(1.0f, tenacity + (resistanceStat * RESISTANCE_WEIGHT));
        }

        private float GetOffensiveStat(BattleCombatant attacker, OffensiveStatType type, BattleContext context)
        {
            return GetEffectiveStat(attacker, type, context);
        }

        private float GetEffectiveStat(BattleCombatant combatant, OffensiveStatType type, BattleContext context)
        {
            float baseVal = type switch
            {
                OffensiveStatType.Strength => combatant.Stats.Strength,
                OffensiveStatType.Intelligence => combatant.Stats.Intelligence,
                OffensiveStatType.Tenacity => combatant.Stats.Tenacity,
                OffensiveStatType.Agility => combatant.Stats.Agility,
                _ => combatant.Stats.Strength
            };

            var evt = new CalculateStatEvent(combatant, type, baseVal);
            combatant.NotifyAbilities(evt, context);

            float multiplier = 1.0f;
            if (combatant.StatStages.ContainsKey(type))
            {
                multiplier = BattleConstants.StatStageMultipliers[combatant.StatStages[type]];
            }

            return evt.FinalValue * multiplier;
        }
    }
}
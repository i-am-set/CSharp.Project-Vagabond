using System;

namespace ProjectVagabond.Battle.Abilities
{
    public class StandardRulesAbility : IAbility
    {
        public string Name => "Standard Rules";
        public string Description => "Applies standard combat math.";
        public int Priority => AbilityPriority.BaseOverride;
        private static readonly Random _random = new Random();

        private const float STAT_DIVISOR = 3.0f;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CheckHitChanceEvent hitEvent)
            {
                hitEvent.FinalAccuracy = hitEvent.BaseAccuracy;
            }
            else if (e is CalculateDamageEvent dmgEvent)
            {
                float offense = GetOffensiveStat(dmgEvent.Actor, dmgEvent.Move.BaseTemplate.OffensiveStat, context);

                float baseDamage = 0f;

                if (dmgEvent.Move.FinalPower > 0)
                {
                    baseDamage = dmgEvent.Move.FinalPower + (offense / STAT_DIVISOR);
                }

                float multiplier = dmgEvent.DamageMultiplier;

                if (dmgEvent.IsCritical) multiplier *= BattleConstants.CRITICAL_HIT_MULTIPLIER;
                if (dmgEvent.IsGraze) multiplier *= BattleConstants.GRAZE_MULTIPLIER;

                if (dmgEvent.Target.Tags.Has(GameplayTags.States.Protected))
                {
                    multiplier = 0f;
                    dmgEvent.WasProtected = true;
                }

                float finalValue = (baseDamage + dmgEvent.FlatBonus) * multiplier;

                if (finalValue < 1f && dmgEvent.Move.FinalPower > 0 && !dmgEvent.WasProtected)
                {
                    finalValue = 1f;
                }

                int guaranteed = (int)Math.Floor(finalValue);
                float chance = finalValue - guaranteed;

                if (context.IsSimulation)
                {
                    if (context.SimulationVariance == VarianceMode.Max && chance > 0) guaranteed++;
                    dmgEvent.FinalDamage = guaranteed;
                }
                else
                {
                    if (_random.NextDouble() < chance) guaranteed++;
                    dmgEvent.FinalDamage = guaranteed;
                }

                if (dmgEvent.FinalDamage < 0) dmgEvent.FinalDamage = 0;
            }
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
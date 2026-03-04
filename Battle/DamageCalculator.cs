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
        public struct DamageResult
        {
            public int DamageAmount;
            public bool WasCritical;
            public bool WasGraze;
            public bool WasProtected;
            public bool WasVulnerable;
        }

        public static DamageResult CalculateDamage(QueuedAction action, BattleCombatant target, CompiledMove move, float multiTargetModifier, bool? overrideCrit, bool isSimulation, BattleContext context)
        {
            var attacker = action.Actor;
            var random = Random.Shared;

            bool isGraze = false;
            if (context.IsSimulation)
            {
                isGraze = false;
            }
            else if (move.FinalAccuracy != -1)
            {
                var hitEvt = new CheckHitChanceEvent(attacker, target, move, move.FinalAccuracy);
                attacker.NotifyAbilities(hitEvt, context);
                target.NotifyAbilities(hitEvt, context);
                foreach (var ab in move.FinalAbilities) ab.OnEvent(hitEvt, context);

                if (random.Next(1, 101) > hitEvt.FinalAccuracy) isGraze = true;
            }

            bool isCrit = false;
            if (!isGraze && move.BaseTemplate.ImpactType != ImpactType.Status && move.FinalPower > 0)
            {
                if (context.IsSimulation)
                {
                    isCrit = overrideCrit ?? false;
                }
                else
                {
                    var highCrit = move.FinalAbilities.OfType<HighCritAbility>().FirstOrDefault();
                    float critChance = highCrit != null ? highCrit.Chance / 100f : BattleConstants.CRITICAL_HIT_CHANCE;
                    isCrit = overrideCrit ?? (random.NextDouble() < critChance);
                }
            }

            var dmgEvt = new CalculateDamageEvent(attacker, target, move, 0f, isCrit, isGraze);
            dmgEvt.DamageMultiplier = multiTargetModifier;

            attacker.NotifyAbilities(dmgEvt, context);
            foreach (var ab in move.FinalAbilities) ab.OnEvent(dmgEvt, context);
            target.NotifyAbilities(dmgEvt, context);

            return new DamageResult
            {
                DamageAmount = dmgEvt.FinalDamage,
                WasCritical = isCrit,
                WasGraze = isGraze,
                WasProtected = dmgEvt.WasProtected,
                WasVulnerable = dmgEvt.WasVulnerable
            };
        }

        public static int CalculateBaselineDamage(BattleCombatant attacker, BattleCombatant target, CompiledMove move)
        {
            if (move.FinalPower == 0) return 0;

            var context = new BattleContext
            {
                Actor = attacker,
                Target = target,
                Move = move,
                IsSimulation = true
            };

            var dmgEvt = new CalculateDamageEvent(attacker, target, move, 0f, false, false);

            attacker.NotifyAbilities(dmgEvt, context);
            foreach (var ab in move.FinalAbilities) ab.OnEvent(dmgEvt, context);
            target.NotifyAbilities(dmgEvt, context);

            return dmgEvt.FinalDamage;
        }
    }
}
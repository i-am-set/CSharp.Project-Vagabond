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

        public struct DamageResult
        {
            public int DamageAmount;
            public bool WasCritical;
            public bool WasGraze;
            public bool WasProtected;
            public bool WasVulnerable;
        }

        public static DamageResult CalculateDamage(QueuedAction action, BattleCombatant target, MoveData move, float multiTargetModifier, bool? overrideCrit, bool isSimulation, BattleContext context)
        {
            var attacker = action.Actor;

            // 1. Accuracy Check
            bool isGraze = false;
            if (move.Accuracy != -1)
            {
                var hitEvt = new CheckHitChanceEvent(attacker, target, move, move.Accuracy);
                attacker.NotifyAbilities(hitEvt, context);
                target.NotifyAbilities(hitEvt, context);
                foreach (var ab in move.Abilities) ab.OnEvent(hitEvt, context);

                if (_random.Next(1, 101) > hitEvt.FinalAccuracy) isGraze = true;
            }

            // 2. Critical Hit Check
            bool isCrit = false;
            if (!isGraze)
            {
                isCrit = overrideCrit ?? (_random.NextDouble() < BattleConstants.CRITICAL_HIT_CHANCE);
            }

            // 3. Calculate Damage Event
            // BaseDamage is now calculated inside StandardRulesAbility, so we pass 0 here.
            var dmgEvt = new CalculateDamageEvent(attacker, target, move, 0f, isCrit, isGraze);
            dmgEvt.DamageMultiplier = multiTargetModifier;

            // Notify in order: Attacker -> Move -> Target
            attacker.NotifyAbilities(dmgEvt, context);
            foreach (var ab in move.Abilities) ab.OnEvent(dmgEvt, context);
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

        public static int CalculateBaselineDamage(BattleCombatant attacker, BattleCombatant target, MoveData move)
        {
            if (move.Power == 0) return 0;

            // Create a temporary context for baseline calculation
            var context = new BattleContext
            {
                Actor = attacker,
                Target = target,
                Move = move
            };

            var dmgEvt = new CalculateDamageEvent(attacker, target, move, 0f, false, false);

            attacker.NotifyAbilities(dmgEvt, context);
            foreach (var ab in move.Abilities) ab.OnEvent(dmgEvt, context);
            target.NotifyAbilities(dmgEvt, context);

            return dmgEvt.FinalDamage;
        }
    }
}
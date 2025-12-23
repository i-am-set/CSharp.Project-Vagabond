using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle
{
    public static class SecondaryEffectSystem
    {
        private static readonly Random _random = new Random();
        public static void ProcessPrimaryEffects(QueuedAction action, BattleCombatant target)
        {
            var move = action.ChosenMove;
            var attacker = action.Actor;

            if (move?.Effects != null)
            {
                foreach (var effectEntry in move.Effects)
                {
                    switch (effectEntry.Key.ToLowerInvariant())
                    {
                        case "restoremana":
                            HandleRestoreMana(attacker, target, effectEntry.Value);
                            break;
                            // All other effects stripped
                    }
                }
            }
        }

        public static void ProcessSecondaryEffects(QueuedAction action, List<BattleCombatant> finalTargets, List<DamageCalculator.DamageResult> damageResults)
        {
            // Stripped of all logic except the completion event
            EventBus.Publish(new GameEvents.SecondaryEffectComplete());
        }

        private static void HandleRestoreMana(BattleCombatant actor, BattleCombatant target, string value)
        {
            if (EffectParser.TryParseFloat(value, out float percentage))
            {
                int amount = (int)(target.Stats.MaxMana * (percentage / 100f));
                float before = target.Stats.CurrentMana;
                target.Stats.CurrentMana = Math.Min(target.Stats.MaxMana, target.Stats.CurrentMana + amount);
                if (target.Stats.CurrentMana > before)
                {
                    EventBus.Publish(new GameEvents.CombatantManaRestored { Target = target, AmountRestored = (int)(target.Stats.CurrentMana - before), ManaBefore = before, ManaAfter = target.Stats.CurrentMana });
                }
            }
        }
    }
}
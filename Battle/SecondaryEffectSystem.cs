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
            // The hardcoded RestoreMana logic has been removed.
            // It is now handled by the RestoreManaAbility attached to the MoveData.
        }

        public static void ProcessSecondaryEffects(QueuedAction action, List<BattleCombatant> finalTargets, List<DamageCalculator.DamageResult> damageResults)
        {
            // Stripped of all logic except the completion event
            EventBus.Publish(new GameEvents.SecondaryEffectComplete());
        }
    }
}
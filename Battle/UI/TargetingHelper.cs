using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A centralized helper to determine valid targets for moves.
    /// This ensures BattleManager (Logic), BattleRenderer (Visuals), and BattleUIManager (Input)
    /// all agree on who can be targeted.
    /// </summary>
    public static class TargetingHelper
    {
        /// <summary>
        /// Returns a list of all combatants that are valid candidates for a specific targeting type.
        /// </summary>
        public static List<BattleCombatant> GetValidTargets(BattleCombatant actor, TargetType targetType, IEnumerable<BattleCombatant> allCombatants)
        {
            var validTargets = new List<BattleCombatant>();

            // Safety check: If actor is null (e.g. during initialization or scene transition), return empty list to prevent crash.
            if (actor == null) return validTargets;

            var activeCombatants = allCombatants.Where(c => !c.IsDefeated && c.IsActiveOnField && c.Stats.CurrentHP > 0).ToList();

            var enemies = activeCombatants.Where(c => c.IsPlayerControlled != actor.IsPlayerControlled).ToList();
            var allies = activeCombatants.Where(c => c.IsPlayerControlled == actor.IsPlayerControlled && c != actor).ToList();

            switch (targetType)
            {
                case TargetType.Self:
                    validTargets.Add(actor);
                    break;

                case TargetType.Single:
                    // Any enemy OR any ally (but not self)
                    validTargets.AddRange(enemies);
                    validTargets.AddRange(allies);
                    break;

                case TargetType.SingleAll:
                    // Anyone including self
                    validTargets.AddRange(activeCombatants);
                    break;

                case TargetType.SingleTeam:
                    // Self or Ally
                    validTargets.Add(actor);
                    validTargets.AddRange(allies);
                    break;

                case TargetType.Ally:
                    validTargets.AddRange(allies);
                    break;

                case TargetType.Both:
                case TargetType.RandomBoth:
                    validTargets.AddRange(enemies);
                    break;

                case TargetType.Every:
                case TargetType.RandomEvery:
                    validTargets.AddRange(enemies);
                    validTargets.AddRange(allies);
                    break;

                case TargetType.Team:
                    validTargets.Add(actor);
                    validTargets.AddRange(allies);
                    break;

                case TargetType.All:
                case TargetType.RandomAll:
                    validTargets.AddRange(activeCombatants);
                    break;

                case TargetType.None:
                default:
                    break;
            }

            bool isMultiTarget = targetType == TargetType.Both ||
                                 targetType == TargetType.Every ||
                                 targetType == TargetType.All ||
                                 targetType == TargetType.Team;

            if (!isMultiTarget)
            {
                var tauntingEnemies = validTargets
                    .Where(t => t.IsPlayerControlled != actor.IsPlayerControlled && t.HasStatusEffect(StatusEffectType.TargetMe))
                    .ToList();

                if (tauntingEnemies.Any())
                {

                    validTargets.RemoveAll(t =>
                        t.IsPlayerControlled != actor.IsPlayerControlled && 
                        !t.HasStatusEffect(StatusEffectType.TargetMe)       
                    );
                }
            }

            return validTargets;
        }
    }
}
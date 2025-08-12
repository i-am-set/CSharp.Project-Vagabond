using ProjectVagabond.Combat.Effects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// A system responsible for processing a CombatAction and applying its effects to the targets.
    /// It uses the Strategy pattern to map effect types to concrete logic classes.
    /// </summary>
    public class ActionResolver
    {
        private readonly Dictionary<string, IActionEffect> _effectStrategies;

        public ActionResolver()
        {
            _effectStrategies = new Dictionary<string, IActionEffect>(StringComparer.OrdinalIgnoreCase)
            {
                { "DealDamage", new DealDamageEffect() },
                { "Heal", new HealEffect() },
                { "ApplyStatusEffect", new ApplyStatusEffect() }
                // New effects can be registered here
            };
        }

        /// <summary>
        /// Resolves a combat action by executing all of its defined effects.
        /// </summary>
        /// <param name="action">The CombatAction to resolve.</param>
        /// <param name="allCombatants">The list of all entities currently in combat.</param>
        public void Resolve(CombatAction action, List<CombatEntity> allCombatants)
        {
            var caster = allCombatants.FirstOrDefault(c => c.EntityId == action.CasterEntityId);
            if (caster == null)
            {
                Debug.WriteLine($"[ERROR] ActionResolver: Caster with ID {action.CasterEntityId} not found in combat.");
                return;
            }

            var targets = allCombatants.Where(c => action.TargetEntityIds.Contains(c.EntityId)).ToList();
            if (action.TargetEntityIds.Any() && !targets.Any())
            {
                // This can happen if a target dies before the action resolves.
                Debug.WriteLine($"[INFO] ActionResolver: No valid targets found for action '{action.ActionData.Name}'.");
                // We still proceed in case there are self-effects.
            }

            // Handle self-targeting actions
            if (action.ActionData.TargetType == TargetType.Self)
            {
                targets = new List<CombatEntity> { caster };
            }

            foreach (var effectDef in action.ActionData.Effects)
            {
                if (_effectStrategies.TryGetValue(effectDef.Type, out var effectStrategy))
                {
                    effectStrategy.Execute(action, caster, targets, effectDef);
                }
                else
                {
                    Debug.WriteLine($"[WARNING] ActionResolver: No effect strategy found for type '{effectDef.Type}'.");
                }
            }
        }
    }
}
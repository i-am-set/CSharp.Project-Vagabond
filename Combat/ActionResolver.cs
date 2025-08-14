using ProjectVagabond.Combat.Effects;
using ProjectVagabond.Dice;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// A system responsible for processing a CombatAction and applying its effects to the targets.
    /// It orchestrates dice rolls, calculates final damage/healing based on stats and resistances,
    /// and applies the results to the combatants.
    /// </summary>
    public class ActionResolver
    {
        // --- TUNING CONSTANTS ---
        private const float WEAKNESS_MULTIPLIER = 1.5f;
        private const float RESISTANCE_MULTIPLIER = 0.5f;

        private readonly ComponentStore _componentStore;

        public ActionResolver()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        /// <summary>
        /// Synchronously resolves a combat action, calculating and applying all its effects.
        /// </summary>
        public void Resolve(CombatAction action, List<CombatEntity> allCombatants)
        {
            var caster = allCombatants.FirstOrDefault(c => c.EntityId == action.CasterEntityId);

            if (caster == null)
            {
                Debug.WriteLine($"[ERROR] ActionResolver: Caster with ID {action.CasterEntityId} not found. Aborting action.");
                EventBus.Publish(new GameEvents.ActionAnimationComplete());
                return;
            }

            List<CombatEntity> targets;
            // Determine targets based on the action's data
            if (action.ActionData.TargetType == TargetType.Self)
            {
                targets = new List<CombatEntity> { caster };
            }
            else
            {
                targets = allCombatants.Where(c => action.TargetEntityIds.Contains(c.EntityId)).ToList();
            }

            var casterStats = _componentStore.GetComponent<StatsComponent>(caster.EntityId);
            var logBuilder = new StringBuilder();
            string casterName = EntityNamer.GetName(caster.EntityId);
            logBuilder.Append($"{casterName}'s {action.ActionData.Name}");

            bool actionHit = false;

            foreach (var effectDef in action.ActionData.Effects)
            {
                // Parse the amount as a flat value. Dice notation like "1d6+5" will result in just the modifier "5".
                // A simple number like "10" will be parsed as a modifier of 10.
                var (_, _, baseAmount) = DiceParser.Parse(effectDef.Amount);

                // Add stat-based modifier
                baseAmount += GetStatModifierForEffect(casterStats, effectDef);

                switch (effectDef.Type)
                {
                    case "DealDamage":
                        actionHit = true;
                        HandleDamageEffect(effectDef, baseAmount, targets, logBuilder);
                        break;
                    case "Heal":
                        actionHit = true;
                        HandleHealEffect(effectDef, baseAmount, targets, logBuilder);
                        break;
                }
            }

            if (!actionHit)
            {
                logBuilder.Append(" has no effect.");
            }

            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = logBuilder.ToString() });

            // Signal completion
            EventBus.Publish(new GameEvents.ActionAnimationComplete());
        }

        private void HandleDamageEffect(EffectDefinition effectDef, int baseAmount, List<CombatEntity> targets, StringBuilder logBuilder)
        {
            var targetNames = new List<string>();
            foreach (var target in targets)
            {
                var targetCombatantComp = _componentStore.GetComponent<CombatantComponent>(target.EntityId);
                var targetHealthComp = _componentStore.GetComponent<HealthComponent>(target.EntityId);
                if (targetHealthComp == null) continue;

                float finalDamage = baseAmount;

                // Apply Weakness/Resistance
                if (targetCombatantComp != null)
                {
                    if (targetCombatantComp.Weaknesses.Contains(effectDef.DamageType))
                    {
                        finalDamage *= WEAKNESS_MULTIPLIER;
                    }
                    if (targetCombatantComp.Resistances.Contains(effectDef.DamageType))
                    {
                        finalDamage *= RESISTANCE_MULTIPLIER;
                    }
                }

                int damageToDeal = Math.Max(0, (int)Math.Round(finalDamage));
                targetHealthComp.TakeDamage(damageToDeal);
                targetNames.Add($"{EntityNamer.GetName(target.EntityId)} for {damageToDeal} {effectDef.DamageType} damage");
            }

            if (targetNames.Any())
            {
                logBuilder.Append($" hits {string.Join(", ", targetNames)}!");
            }
        }

        private void HandleHealEffect(EffectDefinition effectDef, int baseAmount, List<CombatEntity> targets, StringBuilder logBuilder)
        {
            var targetNames = new List<string>();
            foreach (var target in targets)
            {
                var targetHealthComp = _componentStore.GetComponent<HealthComponent>(target.EntityId);
                if (targetHealthComp == null) continue;

                int amountToHeal = Math.Max(0, baseAmount);
                targetHealthComp.Heal(amountToHeal);
                targetNames.Add($"{EntityNamer.GetName(target.EntityId)} for {amountToHeal} health");
            }
            if (targetNames.Any())
            {
                logBuilder.Append($" heals {string.Join(", ", targetNames)}.");
            }
        }

        private int GetStatModifierForEffect(StatsComponent stats, EffectDefinition effectDef)
        {
            if (stats == null) return 0;

            switch (effectDef.Type)
            {
                case "DealDamage":
                    return IsPhysical(effectDef.DamageType)
                        ? stats.GetStatModifier(StatType.Strength)
                        : stats.GetStatModifier(StatType.Intelligence);
                case "Heal":
                    return stats.GetStatModifier(StatType.Intelligence);
                default:
                    return 0;
            }
        }

        private bool IsPhysical(DamageType type)
        {
            return type == DamageType.Slashing || type == DamageType.Blunt;
        }
    }
}
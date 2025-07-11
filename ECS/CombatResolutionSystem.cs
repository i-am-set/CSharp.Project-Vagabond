using Microsoft.Xna.Framework;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Processes combat actions as they happen.
    /// </summary>
    public class CombatResolutionSystem : ISystem
    {
        private readonly ComponentStore _componentStore;

        public CombatResolutionSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        // This system is not updated by the SystemManager, but called explicitly.
        public void Update(GameTime gameTime) { }

        /// <summary>
        /// Resolves a single attack action immediately.
        /// </summary>
        /// <param name="attackerId">The ID of the entity performing the attack.</param>
        /// <param name="chosenAttack">The component containing the attack details.</param>
        public void ResolveAction(int attackerId, ChosenAttackComponent chosenAttack)
        {
            var attackerCombatantComp = _componentStore.GetComponent<CombatantComponent>(attackerId);
            var attackerAttacksComp = _componentStore.GetComponent<AvailableAttacksComponent>(attackerId);
            var targetHealthComp = _componentStore.GetComponent<HealthComponent>(chosenAttack.TargetId);

            // Get attacker and target names for logging
            var attackerName = EntityNamer.GetName(attackerId);
            var targetName = EntityNamer.GetName(chosenAttack.TargetId);

            if (attackerCombatantComp == null || attackerAttacksComp == null || targetHealthComp == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]Could not resolve attack for {attackerName}. Missing components." });
                return;
            }

            var attack = attackerAttacksComp.Attacks.FirstOrDefault(a => a.Name == chosenAttack.AttackName);

            if (attack == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]{attackerName} tried to use an unknown attack: {chosenAttack.AttackName}" });
                return;
            }

            // Calculate damage
            int damage = (int)(attackerCombatantComp.AttackPower * attack.DamageMultiplier);

            // Apply damage
            targetHealthComp.TakeDamage(damage);

            // Log the result
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{attackerName} attacks {targetName} with {attack.Name} for [red]{damage}[/] damage! {targetName} has {targetHealthComp.CurrentHealth}/{targetHealthComp.MaxHealth} HP remaining." });

            // Check for death
            if (targetHealthComp.CurrentHealth <= 0)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[red]{targetName} has been defeated![/]" });
                // TODO: Add logic to remove the entity from combat, drop loot, etc.
            }
        }
    }
}
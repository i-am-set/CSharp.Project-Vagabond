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
        /// <param name="action">The attack action to resolve.</param>
        public void ResolveAction(AttackAction action)
        {
            var attackerId = action.ActorId;
            var targetId = action.TargetId;

            var attackerCombatantComp = _componentStore.GetComponent<CombatantComponent>(attackerId);
            var attackerAttacksComp = _componentStore.GetComponent<AvailableAttacksComponent>(attackerId);
            var targetHealthComp = _componentStore.GetComponent<HealthComponent>(targetId);

            var attackerName = EntityNamer.GetName(attackerId);
            var targetName = EntityNamer.GetName(targetId);

            if (attackerCombatantComp == null || attackerAttacksComp == null || targetHealthComp == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]Could not resolve attack for {attackerName}. Missing components." });
                return;
            }

            var attack = attackerAttacksComp.Attacks.FirstOrDefault(a => a.Name == action.AttackName);

            if (attack == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]{attackerName} tried to use an unknown attack: {action.AttackName}" });
                return;
            }

            int damage = (int)(attackerCombatantComp.AttackPower * attack.DamageMultiplier);
            targetHealthComp.TakeDamage(damage);

            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{attackerName} attacks {targetName} with {attack.Name} for [red]{damage}[/] damage! {targetName} has {targetHealthComp.CurrentHealth}/{targetHealthComp.MaxHealth} HP remaining." });

            if (targetHealthComp.CurrentHealth <= 0)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[red]{targetName} has been defeated![/]" });
                // TODO: Add logic to remove the entity from combat, drop loot, etc.
            }
        }
    }
}
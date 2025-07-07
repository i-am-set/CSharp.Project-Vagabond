using Microsoft.Xna.Framework;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Processes all chosen attacks at the end of a combat round.
    /// </summary>
    public class CombatResolutionSystem : ISystem
    {
        // This system is not updated by the SystemManager, but called explicitly.
        public void Update(GameTime gameTime) { }

        /// <summary>
        /// Resolves all queued attacks for the turn.
        /// </summary>
        public void ResolveTurn()
        {
            var attackers = Core.ComponentStore.GetAllEntitiesWithComponent<ChosenAttackComponent>().ToList();

            foreach (var attackerId in attackers)
            {
                var chosenAttackComp = Core.ComponentStore.GetComponent<ChosenAttackComponent>(attackerId);
                var attackerCombatantComp = Core.ComponentStore.GetComponent<CombatantComponent>(attackerId);
                var attackerAttacksComp = Core.ComponentStore.GetComponent<AvailableAttacksComponent>(attackerId);
                var targetHealthComp = Core.ComponentStore.GetComponent<HealthComponent>(chosenAttackComp.TargetId);

                // Get attacker and target names for logging
                var attackerArchetype = ArchetypeManager.Instance.GetArchetype(Core.ComponentStore.GetComponent<RenderableComponent>(attackerId)?.Texture?.Name ?? "Unknown");
                var attackerName = attackerArchetype?.Name ?? $"Entity {attackerId}";

                var targetArchetype = ArchetypeManager.Instance.GetArchetype(Core.ComponentStore.GetComponent<RenderableComponent>(chosenAttackComp.TargetId)?.Texture?.Name ?? "Unknown");
                var targetName = targetArchetype?.Name ?? $"Entity {chosenAttackComp.TargetId}";

                if (chosenAttackComp == null || attackerCombatantComp == null || attackerAttacksComp == null || targetHealthComp == null)
                {
                    CombatLog.Log($"[error]Could not resolve attack for {attackerName}. Missing components.");
                    // Clean up the component anyway to prevent an infinite loop
                    Core.ComponentStore.RemoveComponent<ChosenAttackComponent>(attackerId);
                    continue;
                }

                var attack = attackerAttacksComp.Attacks.FirstOrDefault(a => a.Name == chosenAttackComp.AttackName);

                if (attack == null)
                {
                    CombatLog.Log($"[error]{attackerName} tried to use an unknown attack: {chosenAttackComp.AttackName}");
                    Core.ComponentStore.RemoveComponent<ChosenAttackComponent>(attackerId);
                    continue;
                }

                // Calculate damage
                int damage = (int)(attackerCombatantComp.AttackPower * attack.DamageMultiplier);

                // Apply damage
                targetHealthComp.TakeDamage(damage);

                // Log the result
                CombatLog.Log($"{attackerName} attacks {targetName} with {attack.Name} for [red]{damage}[/] damage! {targetName} has {targetHealthComp.CurrentHealth}/{targetHealthComp.MaxHealth} HP remaining.");

                // Remove the temporary component
                Core.ComponentStore.RemoveComponent<ChosenAttackComponent>(attackerId);
            }
        }
    }
}
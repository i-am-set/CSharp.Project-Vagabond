using Microsoft.Xna.Framework;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Processes combat actions as they happen.
    /// </summary>
    public class CombatResolutionSystem : ISystem
    {
        // This system is not updated by the SystemManager, but called explicitly.
        public void Update(GameTime gameTime) { }

        /// <summary>
        /// Resolves a single attack action immediately.
        /// </summary>
        /// <param name="attackerId">The ID of the entity performing the attack.</param>
        /// <param name="chosenAttack">The component containing the attack details.</param>
        public void ResolveAction(int attackerId, ChosenAttackComponent chosenAttack)
        {
            var attackerCombatantComp = Core.ComponentStore.GetComponent<CombatantComponent>(attackerId);
            var attackerAttacksComp = Core.ComponentStore.GetComponent<AvailableAttacksComponent>(attackerId);
            var targetHealthComp = Core.ComponentStore.GetComponent<HealthComponent>(chosenAttack.TargetId);

            // Get attacker and target names for logging
            var attackerArchetypeIdComp = Core.ComponentStore.GetComponent<ArchetypeIdComponent>(attackerId);
            var attackerArchetype = ArchetypeManager.Instance.GetArchetype(attackerArchetypeIdComp?.ArchetypeId ?? "Unknown");
            var attackerName = attackerArchetype?.Name ?? $"Entity {attackerId}";

            var targetArchetypeIdComp = Core.ComponentStore.GetComponent<ArchetypeIdComponent>(chosenAttack.TargetId);
            var targetArchetype = ArchetypeManager.Instance.GetArchetype(targetArchetypeIdComp?.ArchetypeId ?? "Unknown");
            var targetName = targetArchetype?.Name ?? $"Entity {chosenAttack.TargetId}";

            if (attackerCombatantComp == null || attackerAttacksComp == null || targetHealthComp == null)
            {
                Core.CurrentTerminalRenderer.AddCombatLog($"[error]Could not resolve attack for {attackerName}. Missing components.");
                return;
            }

            var attack = attackerAttacksComp.Attacks.FirstOrDefault(a => a.Name == chosenAttack.AttackName);

            if (attack == null)
            {
                Core.CurrentTerminalRenderer.AddCombatLog($"[error]{attackerName} tried to use an unknown attack: {chosenAttack.AttackName}");
                return;
            }

            // Calculate damage
            int damage = (int)(attackerCombatantComp.AttackPower * attack.DamageMultiplier);

            // Apply damage
            targetHealthComp.TakeDamage(damage);

            // Log the result
            Core.CurrentTerminalRenderer.AddCombatLog($"{attackerName} attacks {targetName} with {attack.Name} for [red]{damage}[/] damage! {targetName} has {targetHealthComp.CurrentHealth}/{targetHealthComp.MaxHealth} HP remaining.");
        }
    }
}
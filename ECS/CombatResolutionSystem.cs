﻿using Microsoft.Xna.Framework;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Processes combat actions as they happen.
    /// </summary>
    public class CombatResolutionSystem : ISystem
    {
        private readonly ComponentStore _componentStore;
        private GameState _gameState; // Lazy loaded
        private EntityManager _entityManager; // Lazy loaded

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
            _gameState ??= ServiceLocator.Get<GameState>();
            _entityManager ??= ServiceLocator.Get<EntityManager>();

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

            var attackerStatusEffects = _componentStore.GetComponent<ActiveStatusEffectComponent>(attackerId);
            bool isWeakened = attackerStatusEffects?.ActiveEffects.Any(e => e.BaseEffect.Name == "Weakness") ?? false;

            int damage = (int)(attackerCombatantComp.AttackPower * attack.DamageMultiplier);
            if (isWeakened)
            {
                damage /= 2;
            }

            targetHealthComp.TakeDamage(damage);

            // Publish the event so other systems (like Haptics) can react.
            EventBus.Publish(new GameEvents.EntityTookDamage { EntityId = targetId, DamageAmount = damage });

            var playerTag = _componentStore.GetComponent<PlayerTagComponent>(attackerId);
            if (playerTag != null)
            {
                var targetPersonality = _componentStore.GetComponent<AIPersonalityComponent>(targetId);
                if (targetPersonality != null && !targetPersonality.IsProvoked)
                {
                    if (targetPersonality.Personality == AIPersonalityType.Neutral || targetPersonality.Personality == AIPersonalityType.Passive)
                    {
                        targetPersonality.IsProvoked = true;
                        EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{targetName} becomes hostile!" });
                    }
                }
            }

            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{attackerName} attacks {targetName} with {attack.Name} for [red]{damage}[/] damage! {targetName} has {targetHealthComp.CurrentHealth}/{targetHealthComp.MaxHealth} HP remaining." });

            if (attack.StatusEffectsToApply != null && attack.StatusEffectsToApply.Any())
            {
                var statusEffectSystem = ServiceLocator.Get<StatusEffectSystem>();
                foreach (var effectName in attack.StatusEffectsToApply)
                {
                    var effect = statusEffectSystem.CreateEffectFromName(effectName, attack.Name);
                    if (effect != null)
                    {
                        float durationInRounds = 3f; // Lasts 3 rounds
                        statusEffectSystem.ApplyEffect(targetId, effect, durationInRounds, attackerId);
                    }
                }
            }

            if (targetHealthComp.CurrentHealth <= 0)
            {
                // Check for player death first
                if (targetId == _gameState.PlayerEntityId)
                {
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[red]You have been defeated!" });
                    _gameState.EndCombat();
                    // TODO: Implement proper game over screen/logic
                    return; // Stop processing, combat is over.
                }

                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[red]{targetName} has been defeated![/]" });

                // Spawn a corpse
                var worldPos = _componentStore.GetComponent<PositionComponent>(targetId)?.WorldPosition ?? Vector2.Zero;
                var localPos = _componentStore.GetComponent<LocalPositionComponent>(targetId)?.LocalPosition ?? Vector2.Zero;
                int corpseId = Spawner.Spawn("corpse", worldPos, localPos);
                var corpseComp = _componentStore.GetComponent<CorpseComponent>(corpseId);
                if (corpseComp != null)
                {
                    corpseComp.OriginalEntityId = targetId;
                }

                // Remove the defeated entity from the game
                _gameState.RemoveEntityFromCombat(targetId);
                _componentStore.EntityDestroyed(targetId);
                _entityManager.DestroyEntity(targetId);

                // Check for victory condition
                bool enemiesRemain = _gameState.Combatants.Any(id => id != _gameState.PlayerEntityId);
                if (!enemiesRemain)
                {
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[palette_yellow]Victory! All enemies have been defeated." });
                    _gameState.EndCombat();
                }
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class StatusEffectSystem
    {
        private readonly ComponentStore _componentStore;
        private GameState _gameState;

        public StatusEffectSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        /// <summary>
        /// Processes status effects for entities OUTSIDE of combat.
        /// Ticks are based on real-time passage equivalent to combat rounds.
        /// </summary>
        public void ProcessTimePassed(float secondsPassed, ActivityType activity)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (secondsPassed <= 0 || _gameState.IsInCombat) return;

            var entitiesWithEffects = _componentStore.GetAllEntitiesWithComponent<ActiveStatusEffectComponent>().ToList();
            var effectsToRemove = new List<(int entityId, ActiveStatusEffect effect)>();

            foreach (var entityId in entitiesWithEffects)
            {
                var statusEffectComp = _componentStore.GetComponent<ActiveStatusEffectComponent>(entityId);
                if (statusEffectComp == null || !statusEffectComp.ActiveEffects.Any()) continue;

                foreach (var effect in statusEffectComp.ActiveEffects)
                {
                    effect.TimeSinceLastTick += secondsPassed;
                    while (effect.TimeSinceLastTick >= Global.COMBAT_TURN_DURATION_SECONDS)
                    {
                        effect.BaseEffect.OnTick(entityId, _componentStore, effect.Amount);
                        effect.Duration -= 1; // Duration is in rounds
                        effect.TimeSinceLastTick -= Global.COMBAT_TURN_DURATION_SECONDS;

                        if (effect.Duration <= 0)
                        {
                            effectsToRemove.Add((entityId, effect));
                            break; // Stop processing this effect if it expired
                        }
                    }
                }
            }

            RemoveExpiredEffects(effectsToRemove);
        }

        /// <summary>
        /// Processes status effects for a single entity at the start of its turn IN COMBAT.
        /// </summary>
        public void ProcessCombatTurnStart(int entityId)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (!_gameState.IsInCombat) return;

            var statusEffectComp = _componentStore.GetComponent<ActiveStatusEffectComponent>(entityId);
            if (statusEffectComp == null || !statusEffectComp.ActiveEffects.Any()) return;

            var effectsToRemove = new List<(int entityId, ActiveStatusEffect effect)>();

            foreach (var effect in statusEffectComp.ActiveEffects)
            {
                // Tick the effect at the start of the turn
                effect.BaseEffect.OnTick(entityId, _componentStore, effect.Amount);

                // Reduce duration by one round
                effect.Duration -= 1;

                if (effect.Duration <= 0)
                {
                    effectsToRemove.Add((entityId, effect));
                }
            }

            RemoveExpiredEffects(effectsToRemove);
        }

        private void RemoveExpiredEffects(List<(int entityId, ActiveStatusEffect effect)> effectsToRemove)
        {
            foreach (var (entityId, effect) in effectsToRemove)
            {
                var statusEffectComp = _componentStore.GetComponent<ActiveStatusEffectComponent>(entityId);
                if (statusEffectComp != null && statusEffectComp.ActiveEffects.Remove(effect))
                {
                    effect.BaseEffect.OnRemove(entityId, _componentStore);
                }
            }
        }

        public void ApplyEffect(int targetId, StatusEffect effect, float durationInRounds, int sourceId, int amount)
        {
            var statusEffectComp = _componentStore.GetComponent<ActiveStatusEffectComponent>(targetId);
            if (statusEffectComp == null)
            {
                statusEffectComp = new ActiveStatusEffectComponent();
                _componentStore.AddComponent(targetId, statusEffectComp);
            }

            // Check if an effect of the same type already exists.
            var existingEffect = statusEffectComp.ActiveEffects.FirstOrDefault(e => e.BaseEffect.Name == effect.Name);

            if (existingEffect != null)
            {
                // If it exists, refresh its duration and stack the amount.
                existingEffect.Duration += durationInRounds;
                existingEffect.Amount += amount;
                var targetName = EntityNamer.GetName(targetId);
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{targetName}'s {effect.Name} has been intensified and extended." });
            }
            else
            {
                // If it's a new effect, apply it normally.
                var activeEffect = new ActiveStatusEffect(effect, durationInRounds, sourceId, amount);
                statusEffectComp.ActiveEffects.Add(activeEffect);
                effect.OnApply(targetId, _componentStore, amount);
            }
        }

        public StatusEffect CreateEffectFromName(string name, string source)
        {
            switch (name.ToLower())
            {
                case "poison":
                    return new PoisonStatusEffect(source);
                case "weakness":
                    return new WeaknessStatusEffect(source);
                default:
                    return null;
            }
        }
    }
}